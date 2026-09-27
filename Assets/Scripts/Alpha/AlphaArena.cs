using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[DefaultExecutionOrder(-500)]
public sealed class AlphaArena : MonoBehaviour
{
    [SerializeField] private Tilemap ground;
    [SerializeField] private Tilemap obstacles;
    public readonly List<Vector2> Centers = new();
    private readonly List<GameObject> gates = new();
    private Tile floor;
    private Tile wall;
    private Sprite sprite;
    public int RoomCount => Centers.Count;
    public bool IsDoorOpen(int room) => room >= 0 && room < gates.Count && !gates[room].activeSelf;
    public Rect RoomBounds(int room) => new Rect(Centers[room] - new Vector2(10, 7), new Vector2(20, 14));
    private void Awake()
    {
        if (!AlphaSession.Active || !AlphaSession.Expedition) return;
        Generate(AlphaSession.Seed);
    }
    public void Generate(int seed)
    {
        var random = new System.Random(seed);
        sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f, 2);
        floor = ScriptableObject.CreateInstance<Tile>(); floor.sprite = sprite; floor.color = new Color(0.12f, 0.2f, 0.2f); floor.colliderType = Tile.ColliderType.None;
        wall = ScriptableObject.CreateInstance<Tile>(); wall.sprite = sprite; wall.color = new Color(0.28f, 0.4f, 0.38f); wall.colliderType = Tile.ColliderType.Grid;
        ground.ClearAllTiles(); obstacles.ClearAllTiles();
        Centers.Clear(); Centers.Add(Vector2.zero);
        for (int i = 1; i < 5; i++) Centers.Add(Centers[i - 1] + (random.Next(2) == 0 ? Vector2.right * 30 : Vector2.up * 24));
        foreach (int room in new[] { 1, 3 })
        {
            var marker = new GameObject(room == 1 ? "Treasure pedestal" : "Shop counter", typeof(SpriteRenderer)); marker.transform.SetParent(transform);
            marker.transform.position = Centers[room] + Vector2.one * 0.5f; marker.transform.localScale = new Vector3(1.4f, 1.4f, 1);
            var renderer = marker.GetComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.color = room == 1 ? Color.yellow : Color.cyan;
            renderer.sortingLayerName = "Play Field"; renderer.sortingOrder = 10;
        }
        var cells = new HashSet<Vector3Int>();
        foreach (var center in Centers)
            for (int x = -11; x <= 11; x++) for (int y = -8; y <= 8; y++) cells.Add(new Vector3Int((int)center.x + x, (int)center.y + y));
        for (int i = 1; i < Centers.Count; i++)
        {
            Vector2 delta = Centers[i] - Centers[i - 1];
            Vector2 direction = delta.normalized;
            for (int step = 0; step <= (int)delta.magnitude; step++)
                for (int width = -1; width <= 1; width++)
                {
                    Vector2 point = Centers[i - 1] + direction * step + new Vector2(-direction.y, direction.x) * width;
                    cells.Add(new Vector3Int(Mathf.RoundToInt(point.x), Mathf.RoundToInt(point.y)));
                }
            Vector2 midpoint = (Centers[i] + Centers[i - 1]) * 0.5f + Vector2.one * 0.5f;
            var gate = new GameObject("Locked door " + i, typeof(SpriteRenderer), typeof(BoxCollider2D)); gate.layer = 8;
            gate.transform.SetParent(transform); gate.transform.position = midpoint;
            gate.transform.localScale = direction.x > 0 ? new Vector3(1, 3, 1) : new Vector3(3, 1, 1);
            var renderer = gate.GetComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.color = new Color(0.9f, 0.5f, 0.15f); renderer.sortingLayerName = "Play Field"; renderer.sortingOrder = 30;
            // The collider is added before the sprite; Unity otherwise infers a zero-size box.
            gate.GetComponent<BoxCollider2D>().size = Vector2.one;
            gates.Add(gate);
        }
        Vector3Int[] directions = { Vector3Int.left, Vector3Int.right, Vector3Int.up, Vector3Int.down };
        foreach (var cell in cells)
        {
            ground.SetTile(cell, floor);
            foreach (var direction in directions) if (!cells.Contains(cell + direction)) obstacles.SetTile(cell + direction, wall);
        }
        if (obstacles.GetComponent<TilemapCollider2D>() == null) obstacles.gameObject.AddComponent<TilemapCollider2D>();
        ground.CompressBounds(); obstacles.CompressBounds();
    }
    public void OpenDoor(int room) { if (room >= 0 && room < gates.Count) gates[room].SetActive(false); }
    private void OnDestroy() { if (floor != null) Destroy(floor); if (wall != null) Destroy(wall); if (sprite != null) Destroy(sprite); }
}
