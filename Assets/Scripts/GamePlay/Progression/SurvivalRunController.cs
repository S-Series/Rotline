using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum SurvivalRunState { Running, Paused, ChoosingReward, Won, Lost, SystemMenu }

// Owns one scene-local run. Assets contain tuning only; a new scene starts a clean run.
public sealed class SurvivalRunController : MonoBehaviour
{
    [SerializeField] private SurvivalRunDefinition definition;
    [SerializeField] private PlayerCharacter player;
    [SerializeField] private SpawnDirector spawner;
    private PlayerHealth health;
    private PlayerExperience experience;
    private PlayerItemInventory inventory;
    private RunPickupField pickups;
    private readonly Queue<bool> pendingRewards = new();
    private readonly List<RunReward> choices = new();
    private readonly List<string> acquired = new();
    private bool choosing;
    private bool ready;
    private float previousTimeScale;
    public SurvivalRunState State { get; private set; }
    public bool IsRunning => ready && State == SurvivalRunState.Running;
    public PlayerCharacter Player => player;
    public PlayerHealth Health => health;
    public PlayerExperience Experience => experience;
    public SurvivalRunDefinition Definition => definition;
    public IReadOnlyList<RunReward> Choices => choices;
    public IReadOnlyList<string> Acquired => acquired;
    public void RecordAcquisition(string title) { if (!string.IsNullOrEmpty(title)) acquired.Add(title); }
    public RunPickupField Pickups => pickups;
    public int Kills { get; private set; }
    public float Elapsed { get; private set; }
    public int Stage => 1 + Mathf.FloorToInt(Elapsed / Mathf.Max(1f, definition.stageSeconds));
    public event Action StateChanged;
    private SurvivalRunState beforeSystemMenu;
    public void OpenSystemMenu()
    {
        if (State != SurvivalRunState.Running && State != SurvivalRunState.Paused) return;
        beforeSystemMenu = State; SetState(SurvivalRunState.SystemMenu);
    }
    public void CloseSystemMenu()
    {
        if (State != SurvivalRunState.SystemMenu) return;
        if (beforeSystemMenu == SurvivalRunState.Paused) SetState(SurvivalRunState.Paused); else ShowNextReward();
    }

    private void Start()
    {
        if (definition == null || player == null || spawner == null)
        {
            Debug.LogError("Survival run needs its definition, player and spawn director.", this);
            enabled = false; return;
        }
        previousTimeScale = Time.timeScale;
        health = player.GetComponent<PlayerHealth>();
        experience = player.GetComponent<PlayerExperience>();
        inventory = player.GetComponent<PlayerItemInventory>();
        pickups = gameObject.AddComponent<RunPickupField>(); pickups.Initialize(this);
        health.Died += OnDeath;
        experience.LeveledUp += OnLevelUp;
        spawner.EnemyKilled += OnEnemyKilled;
        ready = true;
        SetState(SurvivalRunState.Running);
    }
    private void Update()
    {
        if (!ready) return;
        if (AppRoot.Instance != null && AppRoot.Instance.Input.PausePressed) TogglePause();
        if (!IsRunning) return;
        Elapsed += Time.deltaTime;
        int stage = Stage;
        spawner.ConfigureDifficulty(1f + (stage - 1) * 0.35f, 1f + (stage - 1) * 0.25f,
            1f + (stage - 1) * 0.15f, definition.maximumEnemies);
        if (Elapsed >= definition.durationSeconds && !(AlphaSession.Active && AlphaSession.Expedition)) Finish(true);
    }
    private void OnEnemyKilled(Vector2 position)
    {
        if (!IsRunning) return;
        Kills++;
        pickups.Spawn(position, 0, definition.experiencePerKill);
        float chance = Mathf.Clamp01(definition.healingDropChance + player.Stats.Luck * 0.005f);
        if (UnityEngine.Random.value < chance) pickups.Spawn(position + Vector2.right * 0.3f, 1, definition.healAmount);
        if (Kills % Mathf.Max(1, definition.treasureEveryKills) == 0) pickups.Spawn(position, 2, 1f);
    }
    public void Collect(int kind, float amount)
    {
        if (!IsRunning) return;
        if (kind == 0) experience.AddExperience(amount);
        else if (kind == 1) health.Heal(amount);
        else if (kind == 2) QueueReward(true);
    }
    private void OnLevelUp(int level) => QueueReward(false);
    public void QueueReward(bool treasure)
    {
        if (!ready || State == SurvivalRunState.Won || State == SurvivalRunState.Lost) return;
        pendingRewards.Enqueue(treasure);
        if (State == SurvivalRunState.Running) ShowNextReward();
    }
    private void ShowNextReward()
    {
        choices.Clear();
        while (pendingRewards.Count > 0)
        {
            bool treasure = pendingRewards.Dequeue();
            var candidates = new List<RunReward>();
            if (treasure)
                foreach (var reward in definition.treasures)
                {
                    if (reward == null) continue;
                    if (reward.requiresModularAttack && !player.GetComponent<PlayerCombatController>().HasModularAttack) continue;
                    if (reward.attack != null && player.GetComponent<PlayerCombatController>().CountAttackItem(reward.attack) >= reward.attack.MaximumStacks) continue;
                    if (reward.item != null || reward.attack != null) candidates.Add(reward);
                }
            if (candidates.Count < 3)
            {
                var stats = new List<RunReward>();
                foreach (var item in definition.levelRewards)
                    if (item != null) stats.Add(new RunReward { title = item.DisplayName, description = item.Description, item = item });
                while (stats.Count > 0 && candidates.Count < 3)
                {
                    int index = UnityEngine.Random.Range(0, stats.Count);
                    candidates.Add(stats[index]); stats.RemoveAt(index);
                }
            }
            while (choices.Count < 3 && candidates.Count > 0)
            {
                int index = UnityEngine.Random.Range(0, candidates.Count);
                choices.Add(candidates[index]); candidates.RemoveAt(index);
            }
            if (choices.Count > 0) { SetState(SurvivalRunState.ChoosingReward); return; }
        }
        SetState(SurvivalRunState.Running);
    }
    public bool ChooseReward(int index)
    {
        if (State != SurvivalRunState.ChoosingReward || choosing || index < 0 || index >= choices.Count) return false;
        choosing = true;
        try
        {
            var reward = choices[index];
            bool granted;
            if (reward.attack != null)
            {
                granted = player.GetComponent<PlayerCombatController>().EquipAttackItem(reward.attack);
            }
            else granted = inventory.AddItem(reward.item);
            if (!granted) return false;
            acquired.Add(reward.title);
            if (AlphaSession.Active) AlphaSession.Profile.Discover(reward.attack != null ? reward.attack.Id : reward.item.Id);
            ShowNextReward();
            return true;
        }
        finally { choosing = false; }
    }
    public void TogglePause()
    {
        if (State == SurvivalRunState.Running) SetState(SurvivalRunState.Paused);
        else if (State == SurvivalRunState.Paused) ShowNextReward();
    }
    private void OnDeath() => Finish(false);
    public void Finish(bool won)
    {
        if (!ready || State == SurvivalRunState.Won || State == SurvivalRunState.Lost) return;
        pendingRewards.Clear(); choices.Clear();
        SetState(won ? SurvivalRunState.Won : SurvivalRunState.Lost);
    }
    private void SetState(SurvivalRunState state)
    {
        State = state;
        bool running = state == SurvivalRunState.Running;
        if (!running)
        {
            player.GetComponent<PlayerActionController>().CancelPrimary();
            player.GetComponent<PlayerCombatController>().CancelCombat();
        }
        Time.timeScale = running ? previousTimeScale : 0f;
        if (AppRoot.Instance != null) AppRoot.Instance.Input.SetGameplayEnabled(running);
        StateChanged?.Invoke();
    }
    public void Restart()
    {
        if (!ready) return;
        if (AlphaSession.Active && State != SurvivalRunState.Won && State != SurvivalRunState.Lost) Finish(false);
        if (AlphaSession.Active) AlphaSession.Begin(AlphaSession.CharacterIndex, AlphaSession.Expedition);
        Time.timeScale = previousTimeScale;
        if (AppRoot.Instance != null) AppRoot.Instance.Input.SetGameplayEnabled(true);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
    private void OnDestroy()
    {
        if (!ready) return;
        if (health != null) health.Died -= OnDeath;
        if (experience != null) experience.LeveledUp -= OnLevelUp;
        if (spawner != null) spawner.EnemyKilled -= OnEnemyKilled;
        Time.timeScale = previousTimeScale;
        if (AppRoot.Instance != null) AppRoot.Instance.Input.SetGameplayEnabled(true);
    }
}
