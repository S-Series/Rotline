using UnityEngine;

public sealed class InputReader : MonoBehaviour
{
    private PlayerInputActions inputActions;

    public Vector2 Move =>
        inputActions.Player.Move.ReadValue<Vector2>();

    private void Awake()
    {
        inputActions = new PlayerInputActions();
    }

    private void OnEnable()
    {
        inputActions.Player.Enable();
    }

    private void OnDisable()
    {
        inputActions.Player.Disable();
    }

    private void OnDestroy()
    {
        inputActions.Dispose();
    }
}