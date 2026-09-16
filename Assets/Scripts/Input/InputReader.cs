using UnityEngine;

public sealed class InputReader : MonoBehaviour
{
    private PlayerInputActions inputActions;

    // Movement
    public Vector2 Move =>
        inputActions.Player.Move.ReadValue<Vector2>();

    // Mouse screen position
    public Vector2 AimPosition =>
        inputActions.Player.Aim.ReadValue<Vector2>();


    // Primary Action
    public bool PrimaryPressed =>
        inputActions.Player.PrimaryAction.WasPressedThisFrame();

    public bool PrimaryHeld =>
        inputActions.Player.PrimaryAction.IsPressed();

    public bool PrimaryReleased =>
        inputActions.Player.PrimaryAction.WasReleasedThisFrame();


    // Secondary Action
    public bool SecondaryPressed =>
        inputActions.Player.SecondaryAction.WasPressedThisFrame();

    public bool SecondaryHeld =>
        inputActions.Player.SecondaryAction.IsPressed();

    public bool SecondaryReleased =>
        inputActions.Player.SecondaryAction.WasReleasedThisFrame();


    // Mode Selection
    public bool CombatModePressed =>
        inputActions.Player.CombatMode.WasPressedThisFrame();

    public bool PurificationModePressed =>
        inputActions.Player.PurificationMode.WasPressedThisFrame();


    // Character Actions
    public bool CoreAbilityPressed =>
        inputActions.Player.CoreAbility.WasPressedThisFrame();

    public bool DodgePressed =>
        inputActions.Player.Dodge.WasPressedThisFrame();

    public bool InteractPressed =>
        inputActions.Player.Interact.WasPressedThisFrame();


    // Global Actions
    public bool PausePressed =>
        inputActions.Global.Pause.WasPressedThisFrame();


    private void Awake()
    {
        inputActions = new PlayerInputActions();
    }

    private void OnEnable()
    {
        inputActions.Player.Enable();
        inputActions.Global.Enable();
    }


    private void OnDisable()
    {
        inputActions.Player.Disable();
        inputActions.Global.Disable();
    }


    private void OnDestroy()
    {
        inputActions?.Dispose();
    }
}