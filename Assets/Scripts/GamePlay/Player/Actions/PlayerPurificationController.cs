using UnityEngine;

[RequireComponent(typeof(PlayerActionController))]
[RequireComponent(typeof(PlayerStats))]
public sealed class PlayerPurificationController : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private CorruptionField corruptionField;

    [SerializeField]
    private Camera gameplayCamera;

    [Header("Purification")]
    [SerializeField, Min(0)]
    private int purificationRadiusCells = 0;

    private PlayerActionController actionController;
    private InputReader inputReader;
    private PlayerStats stats;

    private float nextPurificationTime;


    private void Awake()
    {
        stats = GetComponent<PlayerStats>();
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
        if (!isActiveAndEnabled || mode != PlayerMode.Purification)
            return;

        if (corruptionField == null ||
            gameplayCamera == null ||
            inputReader == null)
        {
            return;
        }

        if (Time.time < nextPurificationTime)
            return;
        float purificationRange = stats.PurificationRange;
        float power = stats.PurificationPower;
        if (power <= 0f || purificationRange <= 0f) return;

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

        int removed = corruptionField.Purify(
            targetPosition,
            purificationRadiusCells
        );

        nextPurificationTime =
            Time.time + Mathf.Max(1, removed) / power;
    }


    private void OnDisable()
    {
        if (actionController == null)
            return;

        actionController.PrimaryStarted -= OnPrimary;
        actionController.PrimaryHeld -= OnPrimary;
    }
}
