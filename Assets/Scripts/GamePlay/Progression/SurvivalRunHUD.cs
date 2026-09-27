using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(SurvivalRunController))]
public sealed class SurvivalRunHUD : MonoBehaviour
{
    [Header("Authored canvases")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private Canvas legacyHUD;
    [Header("Status")]
    [SerializeField] private GameObject statusPanel;
    [SerializeField] private Text status;
    [SerializeField] private Text inventory;
    [SerializeField] private Text objective;
    [SerializeField] private GameObject hints;
    [SerializeField] private Image healthFill;
    [SerializeField] private Image experienceFill;
    [SerializeField] private Image resourceFill;
    [Header("Overlay")]
    [SerializeField] private GameObject overlay;
    [SerializeField] private Text heading;
    [SerializeField] private Text details;
    [SerializeField] private Button[] options;
    [SerializeField] private Button resume;
    [SerializeField] private Button restart;
    [SerializeField] private Button mainMenu;
    [SerializeField] private Button equipment;
    [SerializeField] private Button settings;

    private SurvivalRunController run;
    private bool legacyWasEnabled;
    private float refreshAt;

    private void Start()
    {
        run = GetComponent<SurvivalRunController>();
        if (legacyHUD != null) { legacyWasEnabled = legacyHUD.enabled; legacyHUD.enabled = false; }
        canvas.worldCamera = Camera.main;
        for (int i = 0; i < options.Length; i++)
        {
            int index = i;
            options[i].onClick.AddListener(() => run.ChooseReward(index));
        }
        resume.onClick.AddListener(run.TogglePause);
        restart.onClick.AddListener(run.Restart);
        mainMenu.onClick.AddListener(ReturnToMenu);
        equipment.onClick.AddListener(() => GetComponent<AlphaRunController>().OpenPage("inventory"));
        settings.onClick.AddListener(() => GetComponent<AlphaRunController>().OpenPage("settings"));
        run.StateChanged += Refresh;
        Refresh();
    }

    private void Update()
    {
        if (Time.unscaledTime < refreshAt) return;
        refreshAt = Time.unscaledTime + 0.15f;
        Refresh();
    }

    private void ReturnToMenu()
    {
        if (AlphaSession.Active) GetComponent<AlphaRunController>().ReturnToMenu();
        else UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }

    private void Refresh()
    {
        if (run.Health == null || run.Experience == null) return;
        canvas.enabled = run.State != SurvivalRunState.SystemMenu;
        var alpha = GetComponent<AlphaRunController>();
        objective.gameObject.SetActive(AlphaSession.Active && run.IsRunning);
        if (AlphaSession.Active && alpha != null) objective.text = alpha.Objective;
        status.text = $"{(int)run.Elapsed / 60:00}:{(int)run.Elapsed % 60:00} / {(int)run.Definition.durationSeconds / 60:00}:{(int)run.Definition.durationSeconds % 60:00}    단계 {run.Stage}    처치 {run.Kills}\n체력 {run.Health.Current:0}/{run.Health.Maximum:0}    레벨 {run.Experience.Level}    경험치 {run.Experience.Current:0.#}/{run.Experience.Required:0.#}";
        var gauge = run.Player.SpecialGauge;
        status.text += gauge != null ? $"\n고유 자원 {gauge.Current:0}/{gauge.Maximum:0}" : "\n고유 자원 없음";
        healthFill.fillAmount = run.Health.Maximum > 0 ? run.Health.Current / run.Health.Maximum : 0;
        experienceFill.fillAmount = (float)(run.Experience.Current / run.Experience.Required);
        resourceFill.fillAmount = gauge != null ? gauge.Ratio : 0f;
        inventory.text = "획득한 보상\n";
        int first = Mathf.Max(0, run.Acquired.Count - 10);
        for (int i = first; i < run.Acquired.Count; i++) inventory.text += "· " + run.Acquired[i] + "\n";
        bool choosing = run.State == SurvivalRunState.ChoosingReward;
        bool paused = run.State == SurvivalRunState.Paused;
        overlay.SetActive(!run.IsRunning);
        statusPanel.SetActive(run.IsRunning);
        inventory.gameObject.SetActive(run.IsRunning);
        hints.SetActive(run.IsRunning);
        heading.text = choosing ? "성장의 선택" : paused ? "잠시 쉬어가기" : run.State == SurvivalRunState.Won ? "생존 성공" : "이번 여정은 여기까지";
        details.text = choosing ? "하나의 보상을 선택하세요. 선택하는 동안 시간은 멈춥니다.\n같은 능력치 아이템을 다시 얻으면 효과가 중첩됩니다." : $"생존 {(int)run.Elapsed / 60}분 {(int)run.Elapsed % 60}초 · 처치 {run.Kills} · 레벨 {run.Experience.Level} · 획득 보상 {run.Acquired.Count}개";
        if (AlphaSession.Active && alpha != null && (run.State == SurvivalRunState.Won || run.State == SurvivalRunState.Lost))
            details.text += alpha.SettlementFailed
                ? "\n보상 저장 실패 · 저장 경로를 확인한 뒤 메뉴로 돌아가 재시도하세요."
                : $"\n획득 정수 {alpha.EarnedShards} · 메뉴의 영구 성장에서 사용할 수 있습니다.";
        for (int i = 0; i < options.Length; i++)
        {
            options[i].gameObject.SetActive(choosing && i < run.Choices.Count);
            if (choosing && i < run.Choices.Count) options[i].GetComponentInChildren<Text>().text = run.Choices[i].title + "\n\n" + run.Choices[i].description;
        }
        resume.gameObject.SetActive(paused);
        restart.gameObject.SetActive(!choosing);
        mainMenu.gameObject.SetActive(!choosing);
        equipment.gameObject.SetActive(paused && AlphaSession.Active);
        settings.gameObject.SetActive(paused && AlphaSession.Active);
    }

    private void OnDestroy()
    {
        if (run != null) run.StateChanged -= Refresh;
        if (legacyHUD != null) legacyHUD.enabled = legacyWasEnabled;
    }
}
