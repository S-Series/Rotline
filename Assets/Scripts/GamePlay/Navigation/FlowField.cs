using System.Collections.Generic;
using UnityEngine;

public sealed class FlowField
{
    private readonly NavigationGrid grid;


    public Vector3Int TargetCell { get; private set; }

    public bool HasTarget { get; private set; }


    public FlowField(NavigationGrid grid)
    {
        this.grid = grid;
    }


    public bool Build(Vector3Int targetPosition)
    {
        ResetCells();


        if (!grid.TryFindNearestWalkableCell(
                targetPosition,
                out NavigationCell targetCell))
        {
            HasTarget = false;
            return false;
        }


        TargetCell = targetCell.Position;
        HasTarget = true;


        Queue<NavigationCell> open =
            new Queue<NavigationCell>();


        targetCell.IntegrationCost = 0;

        open.Enqueue(targetCell);


        while (open.Count > 0)
        {
            NavigationCell current =
                open.Dequeue();


            for (int i = 0;
                 i < grid.NeighborCount;
                 i++)
            {
                if (!grid.TryGetWalkableNeighbor(
                        current.Position,
                        i,
                        out NavigationCell neighbor))
                {
                    continue;
                }


                int newCost =
                    current.IntegrationCost + 1;


                if (newCost >=
                    neighbor.IntegrationCost)
                {
                    continue;
                }


                neighbor.IntegrationCost =
                    newCost;

                open.Enqueue(neighbor);
            }
        }


        BuildDirections();

        return true;
    }


    private void ResetCells()
    {
        foreach (NavigationCell cell in grid.Cells)
        {
            cell.ResetFlowData();
        }
    }


    private void BuildDirections()
    {
        foreach (NavigationCell cell in grid.Cells)
        {
            if (!cell.IsWalkable)
                continue;

            if (cell.IntegrationCost ==
                int.MaxValue)
            {
                continue;
            }

            if (cell.Position ==
                TargetCell)
            {
                cell.Direction =
                    Vector2.zero;

                continue;
            }


            NavigationCell bestCell = null;

            int bestCost =
                cell.IntegrationCost;


            for (int i = 0;
                 i < grid.NeighborCount;
                 i++)
            {
                if (!grid.TryGetWalkableNeighbor(
                        cell.Position,
                        i,
                        out NavigationCell neighbor))
                {
                    continue;
                }


                if (neighbor.IntegrationCost >=
                    bestCost)
                {
                    continue;
                }


                bestCost =
                    neighbor.IntegrationCost;

                bestCell =
                    neighbor;
            }


            if (bestCell == null)
            {
                cell.Direction =
                    Vector2.zero;

                continue;
            }


            Vector2 currentWorld =
                grid.CellToWorldCenter(
                    cell.Position
                );

            Vector2 nextWorld =
                grid.CellToWorldCenter(
                    bestCell.Position
                );


            cell.Direction =
                (nextWorld - currentWorld)
                .normalized;
        }
    }
}