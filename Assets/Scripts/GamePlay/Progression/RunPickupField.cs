using System.Collections.Generic;
using UnityEngine;

// A bounded, reusable set of visible world drops; no collider or Update per gem.
public sealed class RunPickupField : MonoBehaviour
{
    private sealed class Drop
    {
        public Transform view;
        public SpriteRenderer renderer;
        public int kind;
        public float amount;
        public bool attracted;
    }
    private readonly List<Drop> active = new();
    private readonly Stack<Drop> pool = new();
    private Sprite sprite;
    private Texture2D texture;
    private SurvivalRunController run;
    public int ActiveCount => active.Count;
    public void Initialize(SurvivalRunController owner)
    {
        run = owner;
        texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white); texture.Apply();
        sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1);
    }
    public void Spawn(Vector2 position, int kind, float amount)
    {
        if (run == null) return;
        if (active.Count >= 512)
        {
            // Preserve XP instead of deleting uncollected rewards when the field is full.
            if (kind == 0)
                foreach (var existing in active)
                    if (existing.kind == 0) { existing.amount += amount; return; }
            run.Collect(kind, amount);
            return;
        }
        Drop drop;
        if (pool.Count > 0) drop = pool.Pop();
        else
        {
            var obj = new GameObject("Run drop"); obj.transform.SetParent(transform);
            drop = new Drop { view = obj.transform, renderer = obj.AddComponent<SpriteRenderer>() };
            drop.renderer.sprite = sprite; drop.renderer.sortingOrder = 20;
            drop.renderer.sortingLayerName = "Play Field";
        }
        drop.kind = kind; drop.amount = amount; drop.attracted = false;
        drop.view.position = position;
        drop.view.localScale = Vector3.one * (kind == 2 ? 0.5f : 0.22f);
        drop.renderer.color = kind == 0 ? Color.cyan : kind == 1 ? new Color(1f, 0.3f, 0.4f) : Color.yellow;
        drop.view.gameObject.SetActive(true); active.Add(drop);
    }
    private void Update()
    {
        if (run == null || !run.IsRunning) return;
        Vector2 target = run.Player.transform.position;
        float range = 0.65f + run.Player.Stats.PickupRange;
        for (int i = active.Count - 1; i >= 0 && run.IsRunning; i--)
        {
            var drop = active[i];
            float distance = Vector2.Distance(target, drop.view.position);
            if (distance <= range) drop.attracted = true;
            if (drop.attracted) drop.view.position = Vector2.MoveTowards(drop.view.position, target, 12f * Time.deltaTime);
            if (Vector2.Distance(target, drop.view.position) > 0.3f) continue;
            active.RemoveAt(i); drop.view.gameObject.SetActive(false); pool.Push(drop);
            run.Collect(drop.kind, drop.amount);
        }
    }
    private void OnDestroy() { if (sprite != null) Destroy(sprite); if (texture != null) Destroy(texture); }
}
