using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// All visual objects and button events are serialized in MainMenu.unity.
public sealed class AlphaMainMenu : MonoBehaviour
{
    [Header("Pages")]
    [SerializeField] private GameObject mainPage;
    [SerializeField] private GameObject selectPage;
    [SerializeField] private GameObject upgradesPage;
    [SerializeField] private GameObject collectionPage;
    [SerializeField] private GameObject settingsPage;
    [Header("Profile")]
    [SerializeField] private Text profileLabel;
    [SerializeField] private Text errorLabel;
    [Header("Character selection")]
    [SerializeField] private Text[] characterLabels;
    [SerializeField] private string[] characterDescriptions;
    [SerializeField] private Text modeLabel;
    [Header("Upgrades")]
    [SerializeField] private Button[] upgradeButtons;
    [SerializeField] private Text[] upgradeLabels;
    [SerializeField] private string[] upgradeTitles;
    [Header("Collection")]
    [SerializeField] private Text[] itemRows;
    [SerializeField] private Text[] attackRows;
    [Header("Settings")]
    [SerializeField] private Text volumeLabel;
    [SerializeField] private Text fullscreenLabel;
    private int selected;
    private bool expedition;

    private void Start()
    {
        AlphaSession.End(); AlphaSession.ApplySettings();
        if (AppRoot.Instance != null) AppRoot.Instance.Input.SetGameplayEnabled(false);
        Show("main");
    }
    public void Show(string next)
    {
        mainPage.SetActive(next == "main"); selectPage.SetActive(next == "select");
        upgradesPage.SetActive(next == "upgrades"); collectionPage.SetActive(next == "collection");
        settingsPage.SetActive(next == "settings");
        Refresh();
    }
    private void Refresh()
    {
        var profile = AlphaSession.Profile;
        profileLabel.text = $"정수 {profile.Data.shards}    원정 {profile.Data.runs}회    승리 {profile.Data.wins}회";
        errorLabel.text = profile.Error ?? "";
        errorLabel.gameObject.SetActive(!string.IsNullOrEmpty(profile.Error));
        var catalog = AlphaSession.Catalog;
        for (int i = 0; i < characterLabels.Length; i++)
        {
            bool available = i < catalog.characters.Length;
            characterLabels[i].transform.parent.gameObject.SetActive(available);
            if (available) characterLabels[i].text = (selected == i ? "✓ " : "") + catalog.characters[i].DisplayName + "\n" + characterDescriptions[i];
        }
        modeLabel.text = expedition ? "모드: 무작위 방 탐험  /  5개 구역 + 최종 보스" : "모드: Gameplay 필드  /  10분 생존";
        for (int i = 0; i < upgradeButtons.Length; i++)
        {
            int level = profile.UpgradeLevel(i);
            upgradeLabels[i].text = upgradeTitles[i] + $"\n{level}/5 단계\n" + (level == 5 ? "최대 강화" : $"정수 {profile.UpgradeCost(i)}");
            upgradeButtons[i].interactable = level < 5 && profile.Data.shards >= profile.UpgradeCost(i);
        }
        for (int i = 0; i < itemRows.Length; i++)
        {
            bool available = i < catalog.run.levelRewards.Length;
            itemRows[i].gameObject.SetActive(available);
            if (!available) continue;
            var item = catalog.run.levelRewards[i];
            itemRows[i].text = profile.Data.discoveries.Contains(item.Id) ? item.DisplayName + "   ·   " + item.Description : "미발견 능력치 아이템";
        }
        for (int i = 0; i < attackRows.Length; i++)
        {
            bool available = i < catalog.run.treasures.Length;
            attackRows[i].gameObject.SetActive(available);
            if (!available) continue;
            var reward = catalog.run.treasures[i];
            attackRows[i].text = reward.attack != null && profile.Data.discoveries.Contains(reward.attack.Id) ? reward.title + "   ·   " + reward.description : "미발견 공격 변형";
        }
        volumeLabel.text = $"전체 음량 {profile.Data.volume:P0}  / 클릭하여 변경";
        fullscreenLabel.text = profile.Data.fullscreen ? "화면: 전체 화면" : "화면: 창 모드";
    }
    public void SelectCharacter(int index) { selected = index; AlphaAudio.Play(600); Refresh(); }
    public void ToggleMode() { expedition = !expedition; AlphaAudio.Play(600); Refresh(); }
    public void BuyUpgrade(int index) { AlphaSession.Profile.BuyUpgrade(index); AlphaAudio.Play(600); Refresh(); }
    public void CycleVolume()
    {
        var profile = AlphaSession.Profile;
        float previous = profile.Data.volume;
        profile.Data.volume = profile.Data.volume >= 0.99f ? 0 : Mathf.Min(1, profile.Data.volume + 0.2f);
        if (!profile.Save()) profile.Data.volume = previous;
        AlphaSession.ApplySettings(); Refresh();
    }
    public void ToggleFullscreen()
    {
        var profile = AlphaSession.Profile; profile.Data.fullscreen = !profile.Data.fullscreen;
        if (!profile.Save()) profile.Data.fullscreen = !profile.Data.fullscreen;
        AlphaSession.ApplySettings(); Refresh();
    }
    public void Quit() => Application.Quit();
    public void Launch()
    {
        AlphaSession.Begin(selected, expedition); SceneManager.LoadScene("Gameplay");
    }
}
