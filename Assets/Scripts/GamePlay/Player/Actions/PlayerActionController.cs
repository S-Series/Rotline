using System;
using UnityEngine;

[RequireComponent(typeof(PlayerModeController))]
public sealed class PlayerActionController : MonoBehaviour
{
    [SerializeField]
    private bool debugLog = true;

    private InputReader inputReader;
    private PlayerModeController modeController;

    private bool primaryActive;
    private bool waitForPrimaryRelease;

    private PlayerMode activePrimaryMode;

    // PrimaryAction의 실제 모드를 이벤트 인자로 전달한다.
    public event Action<PlayerMode> PrimaryStarted;
    public event Action<PlayerMode> PrimaryHeld;
    public event Action<PlayerMode> PrimaryReleased;
    public event Action<PlayerMode> PrimaryCanceled;

    // 모드와 관계없이 사용할 행동
    public event Action CoreAbilityRequested;
    public event Action DodgeRequested;
    public event Action InteractRequested;


    private void Awake()
    {
        modeController =
            GetComponent<PlayerModeController>();
    }


    private void OnEnable()
    {
        modeController.ModeChanged += OnModeChanged;
    }


    private void Start()
    {
        inputReader = AppRoot.Instance.Input;
    }


    private void Update()
    {
        if (inputReader == null)
            return;

        HandlePrimary();
        HandleCommonActions();
    }


    private void HandlePrimary()
    {
        // 모드 변경과 LMB가 같은 프레임에 입력되더라도
        // 모드 변경을 우선한다.
        if (inputReader.CombatModePressed ||
            inputReader.PurificationModePressed)
        {
            CancelPrimary();

            if (inputReader.PrimaryHeld)
                waitForPrimaryRelease = true;

            return;
        }

        // 모드 변경 이후 LMB를 놓을 때까지
        // 새로운 행동을 시작하지 않는다.
        if (waitForPrimaryRelease)
        {
            if (!inputReader.PrimaryHeld)
                waitForPrimaryRelease = false;

            return;
        }

        // 새로운 PrimaryAction 시작
        if (!primaryActive && inputReader.PrimaryPressed)
        {
            primaryActive = true;
            activePrimaryMode = modeController.CurrentMode;

            PrimaryStarted?.Invoke(activePrimaryMode);

            Log($"Primary Started: {activePrimaryMode}");
        }

        // 이벤트 구독자가 모드를 바꾸거나 행동을
        // 취소했을 수 있으므로 다시 검사한다.
        if (!primaryActive)
            return;

        // 누르고 있는 동안 실행
        if (inputReader.PrimaryHeld)
        {
            PrimaryHeld?.Invoke(activePrimaryMode);
        }

        if (!primaryActive)
            return;

        // 정상적으로 버튼을 놓았을 때
        if (inputReader.PrimaryReleased ||
            !inputReader.PrimaryHeld)
        {
            PlayerMode completedMode = activePrimaryMode;

            primaryActive = false;

            PrimaryReleased?.Invoke(completedMode);

            Log($"Primary Released: {completedMode}");
        }
    }


    private void HandleCommonActions()
    {
        if (inputReader.CoreAbilityPressed)
        {
            CoreAbilityRequested?.Invoke();
            Log("Core Ability Requested");
        }

        if (inputReader.DodgePressed)
        {
            DodgeRequested?.Invoke();
            Log("Dodge Requested");
        }

        if (inputReader.InteractPressed)
        {
            InteractRequested?.Invoke();
            Log("Interact Requested");
        }
    }


    private void OnModeChanged(PlayerMode newMode)
    {
        CancelPrimary();

        // 모드를 변경할 당시 LMB를 누르고 있었다면
        // 새 모드의 행동이 자동 실행되지 않도록 한다.
        if (inputReader != null && inputReader.PrimaryHeld)
        {
            waitForPrimaryRelease = true;
        }
    }


    // 일시정지, 사망, 스턴 등의 시스템에서도 호출 가능
    public void CancelPrimary()
    {
        if (!primaryActive)
            return;

        PlayerMode canceledMode = activePrimaryMode;

        primaryActive = false;

        PrimaryCanceled?.Invoke(canceledMode);

        Log($"Primary Canceled: {canceledMode}");
    }


    private void OnDisable()
    {
        modeController.ModeChanged -= OnModeChanged;

        CancelPrimary();

        waitForPrimaryRelease = false;
    }


    private void Log(string message)
    {
        if (debugLog)
        {
            Debug.Log(message, this);
        }
    }
}