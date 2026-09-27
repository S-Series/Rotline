using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class CorruptionField : MonoBehaviour
{
    [Header("Tilemaps")]
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private Tilemap obstacleTilemap;
    [SerializeField] private Tilemap corruptionTilemap;
    [SerializeField] private TileBase corruptionTile;
    [Header("Initial Corruption (paint on the corruption tilemap)")]
    [SerializeField, Min(0)] private int seedRadius = 1;
    [Header("Spread")]
    [SerializeField, Min(0.05f)] private float spreadInterval = 1f;
    [SerializeField, Min(1)] private int spreadPerTick = 2;
    [SerializeField, Min(0.05f)] private float spreadGrowthSeconds = 1.5f;
    [SerializeField, Min(1)] private int maxCorruptedCells = 1000;
    [Header("Purification")]
    [SerializeField, Min(0.01f)] private float resistancePerCell = 0.2f;
    [SerializeField, Min(0)] private float protectionSeconds = 5f;
    private static readonly Vector3Int[] Directions = { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right };
    private readonly HashSet<Vector3Int> infectedSet = new();
    // Only untouched corruption can be selected as a spreading source.
    private readonly List<Vector3Int> spreadingCells = new();
    private readonly List<Vector3Int> growingCells = new();
    private readonly Dictionary<Vector3Int, float> remaining = new();
    private readonly Dictionary<Vector3Int, float> protectedUntil = new();
    private readonly List<Vector3Int> scratch = new();
    private float spreadTimer;
    public int CorruptedCellCount => infectedSet.Count;
    public int TotalPurifiedCells { get; private set; }
    public float ProtectionSeconds => protectionSeconds;
    public event Action<int> Purified;
    public bool IsCorrupted(Vector2 position) => groundTilemap != null && infectedSet.Contains(groundTilemap.WorldToCell(position));
    public float CorruptionLevel(Vector2 position)
    {
        if (groundTilemap == null) return 0f;
        return remaining.TryGetValue(groundTilemap.WorldToCell(position), out float value)
            ? Mathf.Clamp01(value / Mathf.Max(0.01f, resistancePerCell)) : 0f;
    }
    public bool IsProtected(Vector2 position) => groundTilemap != null && Protected(groundTilemap.WorldToCell(position));
    public Vector2 CellCenter(Vector2 position) => groundTilemap.GetCellCenterWorld(groundTilemap.WorldToCell(position));
    public bool HasCorruption(Vector2 position, int radiusCells) { Collect(position, radiusCells); return scratch.Count > 0; }
    public float Progress(Vector2 position, int radiusCells)
    {
        Collect(position, radiusCells);
        if(scratch.Count == 0)return 0;
        float total=0;
        foreach(var cell in scratch)total+=Mathf.Clamp01(1f-remaining[cell]/Mathf.Max(.01f,resistancePerCell));
        return total/scratch.Count;
    }
    public Vector3 AreaCorner(Vector2 position, int radius, int corner)
    {
        var center = groundTilemap.WorldToCell(position);
        int low = -radius, high = radius + 1;
        return groundTilemap.CellToWorld(center + new Vector3Int(corner == 0 || corner == 3 ? low : high, corner < 2 ? low : high, 0));
    }
    public float Progress(Vector2 position)
    {
        if (groundTilemap == null) return 0;
        var cell = groundTilemap.WorldToCell(position);
        return remaining.TryGetValue(cell, out float value) ? Mathf.Clamp01(1f - value / Mathf.Max(0.01f, resistancePerCell)) : 0f;
    }
    private void Awake()
    {
        if (groundTilemap == null || corruptionTilemap == null || corruptionTile == null || groundTilemap == corruptionTilemap)
        { Debug.LogError($"{name}: CorruptionField tilemaps are not configured.", this); enabled = false; return; }
        // Respect pollution painted in the scene instead of erasing it on Play.
        foreach (var cell in corruptionTilemap.cellBounds.allPositionsWithin)
        {
            if (!corruptionTilemap.HasTile(cell)) continue;
            if (!Walkable(cell) || infectedSet.Count >= Mathf.Max(1, maxCorruptedCells)) { corruptionTilemap.SetTile(cell, null); continue; }
            infectedSet.Add(cell); spreadingCells.Add(cell); remaining[cell] = Mathf.Max(0.01f, resistancePerCell);
        }
        spreadTimer = Mathf.Max(0.05f, spreadInterval);
    }
    private void Update()
    {
        if (Time.deltaTime <= 0) return;
        AdvanceGrowth(Time.deltaTime);
        spreadTimer -= Time.deltaTime;
        if (spreadTimer > 0) return;
        spreadTimer = Mathf.Max(0.05f, spreadInterval);
        scratch.Clear();
        foreach (var entry in protectedUntil) if (entry.Value <= Time.time) scratch.Add(entry.Key);
        foreach (var cell in scratch) protectedUntil.Remove(cell);
        Spread();
    }
    public int Seed(Vector2 position)
    {
        if (!isActiveAndEnabled) return 0;
        var center = groundTilemap.WorldToCell(position); int added = 0;
        for (int x = -seedRadius; x <= seedRadius; x++) for (int y = -seedRadius; y <= seedRadius; y++)
            if (x*x+y*y <= seedRadius*seedRadius && TryInfect(center + new Vector3Int(x,y,0), x != 0 || y != 0)) added++;
        return added;
    }
    // Compatibility entry point for instant cleansing effects.
    public int Purify(Vector2 position, int radiusCells = 0)
    {
        if (!isActiveAndEnabled) return 0;
        Collect(position, radiusCells); int count = scratch.Count;
        foreach (var cell in scratch) Remove(cell);
        Complete(count); return count;
    }
    // Power is a finite work budget; a larger brush divides it across affected cells.
    public int ApplyPurification(Vector2 position, float power, int radiusCells = 0)
    {
        if (!isActiveAndEnabled || power <= 0 || float.IsNaN(power) || float.IsInfinity(power)) return 0;
        Collect(position, radiusCells);
        if (scratch.Count == 0) return 0;
        float share = power / scratch.Count; int count = 0;
        foreach (var cell in scratch)
        {
            float previous = remaining[cell];
            float value = previous - share;
            if (value <= 0.00001f) { Remove(cell); count++; }
            else if (value < previous)
            {
                remaining[cell] = value;
                spreadingCells.Remove(cell);
                growingCells.Remove(cell);
                RefreshTile(cell, value);
            }
        }
        Complete(count); return count;
    }
    private void Collect(Vector2 position, int radius)
    {
        scratch.Clear(); var center = groundTilemap.WorldToCell(position); radius = Mathf.Clamp(radius,0,16);
        for (int x=-radius;x<=radius;x++) for(int y=-radius;y<=radius;y++)
        {
            var cell = center + new Vector3Int(x,y,0);
            if(infectedSet.Contains(cell)) scratch.Add(cell);
        }
    }
    private void Remove(Vector3Int cell)
    {
        infectedSet.Remove(cell); spreadingCells.Remove(cell); growingCells.Remove(cell); remaining.Remove(cell);
        corruptionTilemap.SetTile(cell,null);
        if(protectionSeconds > 0) protectedUntil[cell] = Time.time + protectionSeconds;
    }
    private void Complete(int count) { if(count <= 0) return; TotalPurifiedCells += count; Purified?.Invoke(count); }
    private bool Protected(Vector3Int cell) => protectedUntil.TryGetValue(cell,out float until) && until > Time.time;
    private bool Walkable(Vector3Int cell) => groundTilemap.HasTile(cell) && (obstacleTilemap == null || !obstacleTilemap.HasTile(cell));
    private bool TryInfect(Vector3Int cell, bool grow = false)
    {
        if (infectedSet.Count >= Mathf.Max(1,maxCorruptedCells) || infectedSet.Contains(cell) || Protected(cell) || !Walkable(cell)) return false;
        infectedSet.Add(cell);
        float strength = Mathf.Max(0.01f,resistancePerCell);
        remaining[cell] = grow ? strength * 0.01f : strength;
        if (grow) growingCells.Add(cell); else spreadingCells.Add(cell);
        corruptionTilemap.SetTile(cell,corruptionTile);
        corruptionTilemap.SetTileFlags(cell,TileFlags.None);
        RefreshTile(cell, remaining[cell]);
        return true;
    }
    private void AdvanceGrowth(float deltaTime)
    {
        float maximum = Mathf.Max(0.01f, resistancePerCell);
        float increase = maximum * deltaTime / Mathf.Max(0.05f, spreadGrowthSeconds);
        for (int i = growingCells.Count - 1; i >= 0; i--)
        {
            Vector3Int cell = growingCells[i];
            float strength = Mathf.Min(maximum, remaining[cell] + increase);
            remaining[cell] = strength;
            RefreshTile(cell, strength);
            if (strength < maximum) continue;
            growingCells.RemoveAt(i);
            spreadingCells.Add(cell);
        }
    }
    private void RefreshTile(Vector3Int cell, float strength)
    {
        float amount = Mathf.Clamp01(strength / Mathf.Max(0.01f, resistancePerCell));
        corruptionTilemap.SetColor(cell, Color.Lerp(new Color(0.3f, 1f, 0.85f, 0.08f), Color.white, amount));
    }
    private void Spread()
    {
        if(spreadingCells.Count == 0) return;
        int added=0, attempts=Mathf.Min(spreadingCells.Count*4,128);
        for(int i=0;i<attempts && added<spreadPerTick && infectedSet.Count<maxCorruptedCells;i++)
        {
            var source=spreadingCells[UnityEngine.Random.Range(0,spreadingCells.Count)];
            if(TryInfect(source+Directions[UnityEngine.Random.Range(0,Directions.Length)], true)) added++;
        }
    }
}
