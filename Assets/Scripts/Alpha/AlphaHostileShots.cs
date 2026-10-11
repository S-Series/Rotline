using System.Collections.Generic;
using UnityEngine;

public sealed class AlphaHostileShots : MonoBehaviour
{
    private sealed class Shot { public Transform view; public Vector2 velocity; public float damage; public float life; }
    public static AlphaHostileShots Instance { get; private set; }
    private readonly List<Shot> active = new();
    public int ActiveCount => active.Count;
    private readonly Stack<Shot> pooled = new();
    private int clearVersion;
    private Sprite sprite;
    private PlayerHealth player;
    private Collider2D playerCollider;
    private void Awake()
    {
        Instance = this;
        sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f, 2);
    }
    private void Start() { player = FindFirstObjectByType<PlayerHealth>(); playerCollider = player.GetComponent<Collider2D>(); }
    public void Fire(Vector2 position, Vector2 direction, float speed, float damage)
    {
        if (active.Count >= 256) return;
        Shot shot;
        if (pooled.Count > 0) shot = pooled.Pop();
        else
        {
            var obj = new GameObject("Hostile shot", typeof(SpriteRenderer)); obj.transform.SetParent(transform);
            var renderer = obj.GetComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.color = new Color(1, 0.25f, 0.15f); renderer.sortingLayerName = "Play Field"; renderer.sortingOrder = 40;
            obj.transform.localScale = Vector3.one * 0.25f;
            shot = new Shot { view = obj.transform };
        }
        shot.view.position = position; shot.velocity = direction.normalized * speed; shot.damage = damage; shot.life = 6f;
        shot.view.gameObject.SetActive(true); active.Add(shot);
    }
    private void Update()
    {
        if (player == null || Time.deltaTime <= 0f) return;
        int version = clearVersion;
        for (int i = active.Count - 1; i >= 0; i--)
        {
            var shot = active[i]; Vector2 from = shot.view.position; Vector2 to = from + shot.velocity * Time.deltaTime;
            Vector2 delta = to - from;
            bool wall = Physics2D.Linecast(from, to, 1 << 8).collider != null;
            float t = delta.sqrMagnitude > 0 ? Mathf.Clamp01(Vector2.Dot((Vector2)player.transform.position - from, delta) / delta.sqrMagnitude) : 0;
            bool hit = Vector2.Distance(playerCollider.ClosestPoint(from + delta * t), from + delta * t) <= 0.15f;
            shot.view.position = to; shot.life -= Time.deltaTime;
            if (!hit && !wall && shot.life > 0f) continue;
            shot.view.gameObject.SetActive(false); pooled.Push(shot); active.RemoveAt(i);
            // Damage listeners can end the run, clear the list and reuse this pooled shot.
            // Retire it first, then stop this update if the remaining list was cleared.
            if (hit && !wall) player.TakeDamage(shot.damage);
            if (version != clearVersion) return;
        }
    }
    public void Clear() { clearVersion++; foreach (var shot in active) { shot.view.gameObject.SetActive(false); pooled.Push(shot); } active.Clear(); }
    private void OnDestroy() { if (Instance == this) Instance = null; if (sprite != null) Destroy(sprite); }
}
