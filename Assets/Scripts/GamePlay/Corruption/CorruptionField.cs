using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class CorruptionField : MonoBehaviour
{
    [Header("Tilemaps")]
    [SerializeField]
    private Tilemap groundTilemap;

    [SerializeField]
    private Tilemap obstacleTilemap;

    [SerializeField]
    private Tilemap corruptionTilemap;

    [SerializeField]
    private TileBase corruptionTile;

    [Header("Initial Corruption")]
    [SerializeField, Min(0)]
    private int seedRadius = 1;

    [Header("Spread")]
    [SerializeField, Min(0.05f)]
    private float spreadInterval = 1f;

    [SerializeField, Min(1)]
    private int spreadPerTick = 2;

    [SerializeField, Min(1)]
    private int maxCorruptedCells = 1000;

    private static readonly Vector3Int[] Directions =
    {
        Vector3Int.up,
        Vector3Int.down,
        Vector3Int.left,
        Vector3Int.right
    };

    private readonly HashSet<Vector3Int> infectedSet =
        new HashSet<Vector3Int>();

    private readonly List<Vector3Int> infectedCells =
        new List<Vector3Int>();

    private float spreadTimer;

    public int CorruptedCellCount => infectedSet.Count;


    private void Awake()
    {
        if (groundTilemap == null ||
            corruptionTilemap == null ||
            corruptionTile == null ||
            groundTilemap == corruptionTilemap)
        {
            Debug.LogError(
                $"{name}: CorruptionField의 Tilemap 설정을 확인하세요.",
                this
            );

            enabled = false;
            return;
        }

        // 오염 전용 Tilemap은 런타임 시작 시 빈 상태로 만든다.
        corruptionTilemap.ClearAllTiles();

        spreadTimer = Mathf.Max(0.05f, spreadInterval);
    }


    private void Update()
    {
        if (infectedSet.Count == 0)
            return;

        spreadTimer -= Time.deltaTime;

        if (spreadTimer > 0f)
            return;

        spreadTimer = Mathf.Max(0.05f, spreadInterval);

        Spread();
    }


    // 적 사망 위치에서 오염을 발생시킨다.
    public int Seed(Vector2 worldPosition)
    {
        if (!isActiveAndEnabled)
            return 0;

        Vector3Int center =
            groundTilemap.WorldToCell(worldPosition);

        int added = 0;

        Debug.Log(
            $"[Corruption Debug] " +
            $"World={worldPosition}, " +
            $"Cell={center}, " +
            $"Ground={groundTilemap.name}, " +
            $"HasGround={groundTilemap.HasTile(center)}, " +
            $"HasObstacle={obstacleTilemap != null && obstacleTilemap.HasTile(center)}, " +
            $"AlreadyInfected={infectedSet.Contains(center)}, " +
            $"Count={infectedSet.Count}/{maxCorruptedCells}",
            this
        );

        for (int x = -seedRadius; x <= seedRadius; x++)
        {
            for (int y = -seedRadius; y <= seedRadius; y++)
            {
                if (x * x + y * y > seedRadius * seedRadius)
                    continue;

                Vector3Int cell =
                    center + new Vector3Int(x, y, 0);

                if (TryInfect(cell))
                    added++;
            }
        }

        return added;
    }


    // 정화 범위에 들어간 오염 타일을 제거한다.
    public int Purify(Vector2 worldPosition, int radiusCells = 0)
    {
        if (!isActiveAndEnabled)
            return 0;

        radiusCells = Mathf.Max(0, radiusCells);

        Vector3Int center =
            groundTilemap.WorldToCell(worldPosition);

        int removed = 0;

        for (int x = -radiusCells; x <= radiusCells; x++)
        {
            for (int y = -radiusCells; y <= radiusCells; y++)
            {
                if (x * x + y * y > radiusCells * radiusCells)
                    continue;

                Vector3Int cell =
                    center + new Vector3Int(x, y, 0);

                if (!infectedSet.Remove(cell))
                    continue;

                infectedCells.Remove(cell);
                corruptionTilemap.SetTile(cell, null);

                removed++;
            }
        }

        return removed;
    }


    private void Spread()
    {
        if (infectedCells.Count == 0 ||
            infectedSet.Count >= maxCorruptedCells)
        {
            return;
        }

        int added = 0;

        

        // 이미 둘러싸인 타일을 계속 선택하는 경우를 대비해
        // 한 번의 확산에서 탐색 횟수를 제한한다.
        int attempts = Mathf.Min(
            infectedCells.Count * 4,
            128
        );

        for (int i = 0;
             i < attempts &&
             added < spreadPerTick &&
             infectedSet.Count < maxCorruptedCells;
             i++)
        {
            Vector3Int source = infectedCells[
                Random.Range(0, infectedCells.Count)
            ];

            Vector3Int direction = Directions[
                Random.Range(0, Directions.Length)
            ];

            if (TryInfect(source + direction))
            {
                added++;
            }
        }
    }


    private bool TryInfect(Vector3Int cell)
    {
        if (infectedSet.Count >= maxCorruptedCells)
            return false;

        if (infectedSet.Contains(cell))
            return false;

        // 지면이 없는 위치에는 오염이 퍼지지 않는다.
        if (!groundTilemap.HasTile(cell))
            return false;

        // 장애물이 있는 칸에도 퍼지지 않도록 설정.
        if (obstacleTilemap != null &&
            obstacleTilemap.HasTile(cell))
        {
            return false;
        }

        infectedSet.Add(cell);
        infectedCells.Add(cell);

        corruptionTilemap.SetTile(cell, corruptionTile);

        return true;
    }
}