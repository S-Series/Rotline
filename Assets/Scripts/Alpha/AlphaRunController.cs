using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(SurvivalRunController))]
public sealed class AlphaRunController : MonoBehaviour
{
    private SurvivalRunController run;
    private SpawnDirector spawner;
    private AlphaArena arena;
    [SerializeField] private AlphaSystemView ui;
    private int roomKills;
    private bool cleared;
    private bool treasureTaken;
    private bool settled;
    private bool leaving;
    private string runId;
    private EnemyActor boss;
    private string systemPage;
    private float lastHealth;
    private readonly HashSet<int> purchases = new();
    public int Room { get; private set; }
    public int Coins { get; private set; }
    public int EarnedShards { get; private set; }
    public bool SettlementFailed => !settled && AlphaSession.Profile.IsSettlementPending(runId);
    public bool RoomCleared => cleared;
    public string Objective
    {
        get
        {
            if (!AlphaSession.Expedition) return $"10분 생존 · 동전 {Coins} · 정화로 오염 확산을 억제하세요.";
            string[] names = { "첫 번째 격리 구역", "잊힌 보물", "오염의 심장부", "피난처 상점", "군집의 지배자" };
            string detail = cleared ? "문이 열렸습니다. 통로를 따라 다음 방으로 이동하세요." : Room == 1 ? "방 중앙에서 F: 보물 획득" : Room == 3 ? "방 중앙에서 F: 상점 이용" : Room == 4 ? $"최종 보스 체력 {boss?.CurrentHealth:0} · 붉은 예고를 피하세요." : $"처치 {roomKills}/{(Room == 0 ? 15 : 25)}";
            if (cleared && Room < 4) detail += arena.Centers[Room + 1].x > arena.Centers[Room].x ? " → 동쪽" : " ↑ 북쪽";
            return $"구역 {Room + 1}/5 · {names[Room]} · 동전 {Coins}\n{detail}\n경로: 전투 → 보물 → 전투 → 상점 → 보스";
        }
    }
    private void Start()
    {
        if (!AlphaSession.Active) { enabled = false; return; }
        runId = AlphaSession.RunId;
        run = GetComponent<SurvivalRunController>(); spawner = FindFirstObjectByType<SpawnDirector>(); arena = FindFirstObjectByType<AlphaArena>();
        foreach (var renderer in run.Player.GetComponentsInChildren<SpriteRenderer>()) { renderer.sprite = AlphaVisuals.Square; renderer.color = new Color(0.35f, 0.8f, 1f); }
        ui.Initialize(this);
        gameObject.AddComponent<AlphaHostileShots>();
        spawner.EnemyKilled += OnKill; run.StateChanged += OnState;
        run.Player.GetComponent<PlayerActionController>().InteractRequested += Interact;
        run.Player.GetComponent<PlayerHealth>().Changed += OnHealthChanged;
        lastHealth = run.Player.GetComponent<PlayerHealth>().Current;
        if (AlphaSession.Expedition)
        {
            run.Player.transform.position = arena.Centers[0] + Vector2.one * 0.5f;
            spawner.RestrictToRoom = true; BeginRoom(0);
        }
    }
    private void Update()
    {
        if (!AlphaSession.Active || run == null || !run.IsRunning || !AlphaSession.Expedition) return;
        if (cleared && Room + 1 < arena.RoomCount && arena.RoomBounds(Room + 1).Contains(run.Player.transform.position)) BeginRoom(Room + 1);
    }
    private void BeginRoom(int room)
    {
        Room = room; roomKills = 0; cleared = false; purchases.Clear();
        spawner.ClearEnemies(); AlphaHostileShots.Instance?.Clear();
        spawner.SpawnBounds = arena.RoomBounds(room);
        spawner.SuspendSpawning = room == 1 || room == 3 || room == 4;
        if (room == 4)
        {
            boss = spawner.SpawnSpecific(AlphaSession.Catalog.boss, arena.Centers[4] + Vector2.right * 5);
            boss.Died += OnBossDied;
        }
    }
    private void OnKill(Vector2 position)
    {
        if (!run.IsRunning) return;
        Coins += 2; roomKills++;
        AlphaAudio.Play(520);
        if (AlphaSession.Expedition && !cleared && (Room == 0 || Room == 2) && roomKills >= (Room == 0 ? 15 : 25)) ClearRoom();
    }
    private void ClearRoom()
    {
        cleared = true; spawner.SuspendSpawning = true; arena.OpenDoor(Room);
        spawner.ClearEnemies(); AlphaHostileShots.Instance?.Clear();
        AlphaAudio.Play(880);
    }
    private void OnBossDied(EnemyActor enemy, Vector2 position) { cleared = true; run.Finish(true); }
    public void Interact()
    {
        if (!run.IsRunning || !AlphaSession.Expedition || Vector2.Distance(run.Player.transform.position, arena.Centers[Room]) > 3f) return;
        if (Room == 1 && !treasureTaken) { treasureTaken = true; ClearRoom(); run.QueueReward(true); }
        else if (Room == 3) OpenPage("shop");
    }
    public void OpenPage(string page)
    {
        if (run.State != SurvivalRunState.Running && run.State != SurvivalRunState.Paused && run.State != SurvivalRunState.SystemMenu) return;
        systemPage = page;
        if (run.State != SurvivalRunState.SystemMenu) run.OpenSystemMenu();
        ui.Show(page);
    }
    public bool CanBuy(int offer)
    {
        if (offer < 0 || offer > 2 || purchases.Contains(offer)) return false;
        int price = offer == 0 ? 8 : 12;
        return Coins >= price && (offer != 0 || run.Health.Current < run.Health.Maximum);
    }
    public bool Buy(int offer)
    {
        if (run.State != SurvivalRunState.SystemMenu || systemPage != "shop" || offer < 0 || offer > 2 || purchases.Contains(offer)) return false;
        int price = offer == 0 ? 8 : 12;
        if (Coins < price || (offer == 0 && run.Health.Current >= run.Health.Maximum)) return false;
        if (offer == 0) run.Health.Heal(30f);
        else
        {
            var item = run.Definition.levelRewards[offer == 1 ? 2 : 1];
            if (!run.Player.GetComponent<PlayerItemInventory>().AddItem(item)) return false;
            AlphaSession.Profile.Discover(item.Id);
            run.RecordAcquisition(item.DisplayName);
        }
        Coins -= price; purchases.Add(offer); ui.Refresh("shop"); return true;
    }
    public void ClosePage()
    {
        if (systemPage == "shop" && !cleared) ClearRoom();
        ui.Hide(); run.CloseSystemMenu();
    }
    private void OnState()
    {
        if (run.State == SurvivalRunState.Won || run.State == SurvivalRunState.Lost)
        {
            TrySettle();
            spawner.SuspendSpawning = true; AlphaHostileShots.Instance?.Clear();
        }
    }
    private void TrySettle()
    {
        if (settled || run == null || (run.State != SurvivalRunState.Won && run.State != SurvivalRunState.Lost)) return;
        int result = AlphaSession.Profile.Settle(runId, run.State == SurvivalRunState.Won, run.Kills, run.Elapsed);
        if (result < 0) return;
        EarnedShards = result;
        settled = true;
    }
    private void OnHealthChanged()
    {
        float value = run.Player.GetComponent<PlayerHealth>().Current;
        if (value < lastHealth) AlphaAudio.Play(180);
        lastHealth = value;
    }
    public void ReturnToMenu()
    {
        if (leaving) return; leaving = true;
        if (!settled) run.Finish(false);
        TrySettle();
        AlphaSession.End(); SceneManager.LoadScene("MainMenu");
    }
    private void OnDestroy()
    {
        if (!settled) TrySettle();
        if (spawner != null) spawner.EnemyKilled -= OnKill;
        if (run != null)
        {
            run.StateChanged -= OnState;
            if (run.Player != null) run.Player.GetComponent<PlayerActionController>().InteractRequested -= Interact;
            if (run.Player != null) run.Player.GetComponent<PlayerHealth>().Changed -= OnHealthChanged;
        }
    }
}
