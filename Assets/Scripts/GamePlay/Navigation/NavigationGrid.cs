using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class NavigationGrid
{
    private static readonly Vector3Int[] NeighborOffsets =
    {
        new(0, 1, 0),
        new(1, 1, 0),
        new(1, 0, 0),
        new(1, -1, 0),
        new(0, -1, 0),
        new(-1, -1, 0),
        new(-1, 0, 0),
        new(-1, 1, 0)
    };


    private readonly Tilemap groundTilemap;
    private readonly Tilemap obstacleTilemap;

    private readonly Dictionary<Vector3Int, NavigationCell> cells = new();


    public IEnumerable<NavigationCell> Cells => cells.Values;

    public int NeighborCount => NeighborOffsets.Length;


    public NavigationGrid(
        Tilemap groundTilemap,
        Tilemap obstacleTilemap)
    {
        this.groundTilemap = groundTilemap;
        this.obstacleTilemap = obstacleTilemap;

        Build();
    }


    public void Build()
    {
        cells.Clear();

        BoundsInt bounds = groundTilemap.cellBounds;

        foreach (Vector3Int position in bounds.allPositionsWithin)
        {
            if (!groundTilemap.HasTile(position))
                continue;

            bool isBlocked =
                obstacleTilemap != null &&
                obstacleTilemap.HasTile(position);

            cells[position] =
                new NavigationCell(
                    position,
                    !isBlocked
                );
        }
    }


    public Vector3Int WorldToCell(Vector2 worldPosition)
    {
        return groundTilemap.WorldToCell(worldPosition);
    }


    public Vector2 CellToWorldCenter(Vector3Int cellPosition)
    {
        return groundTilemap.GetCellCenterWorld(cellPosition);
    }


    public bool TryGetCell(
        Vector3Int position,
        out NavigationCell cell)
    {
        return cells.TryGetValue(position, out cell);
    }


    public bool TryGetCell(
        Vector2 worldPosition,
        out NavigationCell cell)
    {
        Vector3Int position =
            WorldToCell(worldPosition);

        return TryGetCell(position, out cell);
    }


    public bool TryGetWalkableNeighbor(
        Vector3Int origin,
        int neighborIndex,
        out NavigationCell neighbor)
    {
        neighbor = null;

        if (neighborIndex < 0 ||
            neighborIndex >= NeighborOffsets.Length)
        {
            return false;
        }

        Vector3Int offset =
            NeighborOffsets[neighborIndex];

        Vector3Int target =
            origin + offset;

        if (!cells.TryGetValue(
                target,
                out NavigationCell targetCell))
        {
            return false;
        }

        if (!targetCell.IsWalkable)
            return false;


        // 대각선 이동 시 벽 모서리를 뚫고 지나가는 것 방지
        if (offset.x != 0 &&
            offset.y != 0)
        {
            Vector3Int horizontal =
                origin +
                new Vector3Int(
                    offset.x,
                    0,
                    0
                );

            Vector3Int vertical =
                origin +
                new Vector3Int(
                    0,
                    offset.y,
                    0
                );

            if (!IsWalkable(horizontal) ||
                !IsWalkable(vertical))
            {
                return false;
            }
        }


        neighbor = targetCell;

        return true;
    }


    public bool IsWalkable(Vector3Int position)
    {
        return
            cells.TryGetValue(
                position,
                out NavigationCell cell
            )
            &&
            cell.IsWalkable;
    }


    public bool TryFindNearestWalkableCell(
        Vector3Int origin,
        out NavigationCell result,
        int maxRadius = 3)
    {
        if (TryGetCell(origin, out NavigationCell originCell) &&
            originCell.IsWalkable)
        {
            result = originCell;
            return true;
        }


        for (int radius = 1;
             radius <= maxRadius;
             radius++)
        {
            for (int x = -radius;
                 x <= radius;
                 x++)
            {
                for (int y = -radius;
                     y <= radius;
                     y++)
                {
                    Vector3Int position =
                        origin +
                        new Vector3Int(
                            x,
                            y,
                            0
                        );

                    if (!TryGetCell(
                            position,
                            out NavigationCell cell))
                    {
                        continue;
                    }

                    if (!cell.IsWalkable)
                        continue;

                    result = cell;
                    return true;
                }
            }
        }


        result = null;

        return false;
    }
}