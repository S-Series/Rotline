using UnityEngine;

// The line objects and material are saved in Gameplay.unity. Only their positions change at runtime.
public sealed class AttackFeedbackView : MonoBehaviour
{
    [SerializeField] private PlayerCombatController combat;
    [SerializeField] private PlayerModeController mode;
    [SerializeField] private SurvivalRunController run;
    [SerializeField] private Camera gameplayCamera;
    [SerializeField] private LineRenderer rangeRing;
    [SerializeField] private LineRenderer aimLine;
    [SerializeField] private LineRenderer attackPulse;
    [SerializeField] private Color rangeColor = new Color(1f, 0.75f, 0.3f, 0.7f);
    [SerializeField] private Color chargingColor = new Color(1f, 0.45f, 0.2f, 0.9f);
    [SerializeField, Min(0f)] private float pulseDuration = 0.18f;
    private InputReader input;
    private float pulseUntil;

    private void OnEnable()
    {
        if (combat != null) combat.AttackFeedback += OnAttack;
    }

    private void Start()
    {
        if (gameplayCamera == null) gameplayCamera = Camera.main;
        if (AppRoot.Instance != null) input = AppRoot.Instance.Input;
    }

    private void LateUpdate()
    {
        bool visible = combat != null && mode != null && input != null && gameplayCamera != null &&
            mode.IsMode(PlayerMode.Combat) && (run == null || run.IsRunning) && Time.deltaTime > 0f;
        rangeRing.enabled = aimLine.enabled = visible;
        attackPulse.enabled = visible && Time.time < pulseUntil;
        if (!visible) return;

        Vector2 origin = combat.transform.position;
        Vector2 cursor = gameplayCamera.ScreenToWorldPoint(input.AimPosition);
        Vector2 delta = cursor - origin;
        Vector2 direction = delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector2.right;
        float range = Mathf.Max(0f, combat.PreviewRange);
        if (range <= 0f) { rangeRing.enabled = aimLine.enabled = attackPulse.enabled = false; return; }

        DrawRing(rangeRing, origin, range);
        aimLine.SetPosition(0, origin);
        aimLine.SetPosition(1, origin + direction * Mathf.Min(range, Mathf.Max(0.8f, delta.magnitude)));
        Color color = combat.IsCharging ? chargingColor : rangeColor;
        rangeRing.startColor = rangeRing.endColor = color;
        aimLine.startColor = aimLine.endColor = color;
        if (attackPulse.enabled)
        {
            float fraction = 1f - (pulseUntil - Time.time) / Mathf.Max(0.01f, pulseDuration);
            DrawRing(attackPulse, origin, Mathf.Lerp(range * 0.2f, range, Mathf.Clamp01(fraction)));
            var flash = new Color(1f, 0.9f, 0.4f, 1f - Mathf.Clamp01(fraction));
            attackPulse.startColor = attackPulse.endColor = flash;
        }
    }

    private static void DrawRing(LineRenderer line, Vector2 center, float radius)
    {
        int count = line.positionCount;
        for (int i = 0; i < count; i++)
        {
            float angle = i * Mathf.PI * 2f / count;
            line.SetPosition(i, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
        }
    }

    private void OnAttack(AttackSnapshot _) => pulseUntil = Time.time + pulseDuration;

    private void OnDisable()
    {
        if (combat != null) combat.AttackFeedback -= OnAttack;
        if (rangeRing != null) rangeRing.enabled = false;
        if (aimLine != null) aimLine.enabled = false;
        if (attackPulse != null) attackPulse.enabled = false;
    }
}
