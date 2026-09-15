using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class NavigationService : MonoBehaviour
{
    [Header("Tilemaps")]

    [SerializeField]
    private Tilemap groundTilemap;

    [SerializeField]
    private Tilemap obstacleTilemap;


    [Header("Target")]

    [SerializeField]
    private Transform target;


    private NavigationGrid navigationGrid;
    private FlowField flowField;

    private Vector3Int lastTargetCell;

    private bool hasTargetCell;


    private void Awake()
    {
        BuildNavigation();
    }


    private void Update()
    {
        UpdateTarget();
    }


    private void BuildNavigation()
    {
        if (groundTilemap == null)
        {
            Debug.LogError(
                $"{name}: Ground Tilemap이 설정되지 않았습니다.",
                this
            );

            return;
        }


        navigationGrid =
            new NavigationGrid(
                groundTilemap,
                obstacleTilemap
            );

        flowField =
            new FlowField(
                navigationGrid
            );


        hasTargetCell = false;
    }


    private void UpdateTarget()
    {
        if (target == null ||
            navigationGrid == null ||
            flowField == null)
        {
            return;
        }


        Vector3Int targetCell =
            navigationGrid.WorldToCell(
                target.position
            );


        if (hasTargetCell &&
            targetCell == lastTargetCell)
        {
            return;
        }


        RebuildFlowField(targetCell);
    }


    private void RebuildFlowField(
        Vector3Int targetCell)
    {
        if (!flowField.Build(targetCell))
            return;


        lastTargetCell =
            flowField.TargetCell;

        hasTargetCell = true;
    }


    public void SetTarget(Transform newTarget)
    {
        target = newTarget;

        hasTargetCell = false;

        UpdateTarget();
    }


    public Vector2 GetDirection(
        Vector2 worldPosition)
    {
        if (navigationGrid == null ||
            flowField == null ||
            target == null)
        {
            return Vector2.zero;
        }


        Vector3Int currentCell =
            navigationGrid.WorldToCell(
                worldPosition
            );


        if (flowField.HasTarget &&
            currentCell ==
            flowField.TargetCell)
        {
            return
                ((Vector2)target.position -
                 worldPosition)
                .normalized;
        }


        if (!navigationGrid.TryGetCell(
                currentCell,
                out NavigationCell cell))
        {
            return Vector2.zero;
        }


        if (!cell.IsWalkable)
            return Vector2.zero;


        return cell.Direction;
    }


    public void RefreshGrid()
    {
        if (navigationGrid == null)
            return;


        navigationGrid.Build();

        hasTargetCell = false;

        UpdateTarget();
    }
}