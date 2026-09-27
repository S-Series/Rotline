using UnityEngine;

public sealed class AlphaEnemyOffense : MonoBehaviour
{
    private EnemyActor actor;
    private PlayerHealth target;
    private float remaining;
    private LineRenderer warning;
    public void Initialize(EnemyActor owner, PlayerHealth player)
    {
        actor = owner; target = player; remaining = owner.Definition.shotInterval;
        if (warning == null)
        {
            warning = new GameObject("Attack warning").AddComponent<LineRenderer>(); warning.transform.SetParent(transform, false);
            warning.material = new Material(Shader.Find("Sprites/Default")); warning.startWidth = warning.endWidth = 0.07f;
            warning.startColor = warning.endColor = new Color(1f, 0.3f, 0.15f); warning.sortingLayerName = "Play Field"; warning.sortingOrder = 50;
            warning.loop = true; warning.positionCount = 32;
        }
        warning.enabled = false;
    }
    private void Update()
    {
        if (actor == null || actor.Definition == null || actor.IsDead || target == null || Time.deltaTime <= 0f) return;
        int style = actor.Definition.rangedStyle;
        if (style == 0) return;
        remaining -= Time.deltaTime;
        warning.enabled = style == 2 && remaining < 0.8f;
        if (warning.enabled)
            for (int i = 0; i < 32; i++) { float angle = i * Mathf.PI * 2 / 32; warning.SetPosition(i, transform.position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * 2.5f); }
        if (remaining > 0f) return;
        bool enraged = style == 2 && actor.CurrentHealth < actor.MaximumHealth * 0.5f;
        remaining = actor.Definition.shotInterval * (enraged ? 0.7f : 1f);
        Vector2 aim = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;
        int count = style == 2 ? (enraged ? 16 : 10) : 1;
        for (int i = 0; i < count; i++)
        {
            Vector2 direction = count == 1 ? aim : new Vector2(Mathf.Cos(i * Mathf.PI * 2 / count), Mathf.Sin(i * Mathf.PI * 2 / count));
            AlphaHostileShots.Instance?.Fire(transform.position, direction, style == 2 ? 5f : 7f, style == 2 ? 8f : 4f);
        }
    }
    private void OnDisable() { if (warning != null) warning.enabled = false; }
    private void OnDestroy() { if (warning != null && warning.sharedMaterial != null) Destroy(warning.sharedMaterial); }
}
