using UnityEngine;

[RequireComponent(typeof(PlayerActionController))]
public sealed class PlayerPurificationController : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private CorruptionField corruptionField;

    [SerializeField]
    private Camera gameplayCamera;

    [Header("Purification")]
    [SerializeField, Min(0f)]
    private float purificationRange = 3f;

    [SerializeField, Min(0.01f)]
    private float purificationInterval = 0.2f;

    [SerializeField, Min(0)]
    private int purificationRadiusCells = 0;

    private PlayerActionController actionController;
    private InputReader inputReader;

    private float nextPurificationTime;


    private void Awake()
    {
        actionController =
            GetComponent<PlayerActionController>();
    }


    private void OnEnable()
    {
        actionController.PrimaryStarted += OnPrimary;
        actionController.PrimaryHeld += OnPrimary;
    }


    private void Start()
    {
        inputReader = AppRoot.Instance.Input;

        if (gameplayCamera == null)
        {
            gameplayCamera = Camera.main;
        }
    }


    private void OnPrimary(PlayerMode mode)
    {
        if (mode != PlayerMode.Purification)
            return;

        if (corruptionField == null ||
            gameplayCamera == null ||
            inputReader == null)
        {
            return;
        }

        if (Time.time < nextPurificationTime)
            return;

        Vector2 targetPosition =
            gameplayCamera.ScreenToWorldPoint(
                inputReader.AimPosition
            );

        float distanceSqr =
            (targetPosition - (Vector2)transform.position)
            .sqrMagnitude;

        if (distanceSqr >
            purificationRange * purificationRange)
        {
            return;
        }

        corruptionField.Purify(
            targetPosition,
            purificationRadiusCells
        );

        nextPurificationTime =
            Time.time + Mathf.Max(0.01f, purificationInterval);
    }


    private void OnDisable()
    {
        if (actionController == null)
            return;

        actionController.PrimaryStarted -= OnPrimary;
        actionController.PrimaryHeld -= OnPrimary;
    }
}