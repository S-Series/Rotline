using UnityEngine;
using UnityEngine.UI;

// The canvas, pages, rows and buttons are saved in Gameplay.unity.
public sealed class AlphaSystemView : MonoBehaviour
{
    [SerializeField] private Canvas canvas;
    [SerializeField] private GameObject shopPage;
    [SerializeField] private GameObject settingsPage;
    [SerializeField] private GameObject inventoryPage;
    [SerializeField] private Text heading;
    [SerializeField] private Text coins;
    [SerializeField] private Button[] shopOffers;
    [SerializeField] private Button volumeButton;
    [SerializeField] private Text volumeLabel;
    [SerializeField] private Text statsLabel;
    [SerializeField] private Text[] itemRows;
    [SerializeField] private Text[] attackRows;
    [SerializeField] private Button backButton;

    private AlphaRunController controller;

    public void Initialize(AlphaRunController owner)
    {
        controller = owner;
        canvas.worldCamera = Camera.main;
        for (int i = 0; i < shopOffers.Length; i++)
        {
            int offer = i;
            shopOffers[i].onClick.AddListener(() => controller.Buy(offer));
        }
        volumeButton.onClick.AddListener(CycleVolume);
        backButton.onClick.AddListener(controller.ClosePage);
        gameObject.SetActive(false);
    }

    public void Show(string page)
    {
        gameObject.SetActive(true);
        shopPage.SetActive(page == "shop");
        settingsPage.SetActive(page == "settings");
        inventoryPage.SetActive(page == "inventory");
        heading.text = page == "shop" ? "피난처의 거래" : page == "settings" ? "설정과 조작" : "이번 원정의 장비";
        Refresh(page);
    }

    public void Refresh(string page)
    {
        if (page == "shop")
        {
            coins.text = $"동전 {controller.Coins} · 각 물품은 한 번씩 구입할 수 있습니다.";
            for (int i = 0; i < shopOffers.Length; i++) shopOffers[i].interactable = controller.CanBuy(i);
        }
        else if (page == "settings") volumeLabel.text = $"음량 {AlphaSession.Profile.Data.volume:P0}";
        else if (page == "inventory")
        {
            var run = controller.GetComponent<SurvivalRunController>();
            var player = run.Player;
            var stats = player.Stats;
            statsLabel.text = $"공격력 {stats.AttackPower:0.#}  ·  공격속도 {stats.AttackSpeed:0.##}  ·  행운 {stats.Luck:0}\n경험치 배율 {stats.ExperienceGainRate:0.##}  ·  탄속 배율 {stats.ProjectileSpeedMultiplier:0.##}";
            var items = player.GetComponent<PlayerItemInventory>();
            for (int i = 0; i < itemRows.Length; i++)
            {
                bool available = i < run.Definition.levelRewards.Length;
                itemRows[i].gameObject.SetActive(available);
                if (available)
                {
                    var item = run.Definition.levelRewards[i];
                    itemRows[i].text = $"{item.DisplayName} ×{items.Count(item)}   {item.Description}";
                }
            }
            var combat = player.GetComponent<PlayerCombatController>();
            for (int i = 0; i < attackRows.Length; i++)
            {
                bool available = i < run.Definition.treasures.Length;
                attackRows[i].gameObject.SetActive(available);
                if (available)
                {
                    var reward = run.Definition.treasures[i];
                    attackRows[i].text = $"{reward.title} ×{combat.CountAttackItem(reward.attack)}";
                }
            }
        }
    }

    public void Hide() => gameObject.SetActive(false);

    private void CycleVolume()
    {
        var profile = AlphaSession.Profile;
        float previous = profile.Data.volume;
        profile.Data.volume = previous >= 0.99f ? 0 : Mathf.Min(1f, previous + 0.2f);
        if (!profile.Save()) profile.Data.volume = previous;
        AlphaSession.ApplySettings();
        Refresh("settings");
    }
}
