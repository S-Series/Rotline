using System;
using UnityEngine;

public enum PlayerMode
{
    Combat,
    Purification
}

public sealed class PlayerModeController : MonoBehaviour
{
    [SerializeField]
    private PlayerMode initialMode = PlayerMode.Combat;

    private InputReader inputReader;

    public PlayerMode CurrentMode { get; private set; }

    public event Action<PlayerMode> ModeChanged;


    private void Awake()
    {
        CurrentMode = initialMode;
    }


    private void Start()
    {
        inputReader = AppRoot.Instance.Input;
    }


    private void Update()
    {
        if (inputReader.CombatModePressed)
        {
            SetMode(PlayerMode.Combat);
        }
        else if (inputReader.PurificationModePressed)
        {
            SetMode(PlayerMode.Purification);
        }
    }


    public void SetMode(PlayerMode mode)
    {
        if (CurrentMode == mode)
            return;

        CurrentMode = mode;

        ModeChanged?.Invoke(CurrentMode);

        // Temporary debugging
        Debug.Log($"Player Mode: {CurrentMode}");
    }


    public bool IsMode(PlayerMode mode)
    {
        return CurrentMode == mode;
    }
}