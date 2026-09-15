using UnityEngine;

public sealed class NavigationCell
{
    public Vector3Int Position { get; }

    public bool IsWalkable { get; }

    public int IntegrationCost { get; set; }

    public Vector2 Direction { get; set; }


    public NavigationCell(
        Vector3Int position,
        bool isWalkable)
    {
        Position = position;
        IsWalkable = isWalkable;

        ResetFlowData();
    }


    public void ResetFlowData()
    {
        IntegrationCost = int.MaxValue;
        Direction = Vector2.zero;
    }
}