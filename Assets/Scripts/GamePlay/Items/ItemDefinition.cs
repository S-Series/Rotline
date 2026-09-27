using System;
using UnityEngine;

[Serializable]
public struct ItemStatModifier
{
    public StatKey stat;
    public ModifierType type;
    public float amount;
}

[CreateAssetMenu(fileName = "Item", menuName = "Game/Items/Stat Item")]
public sealed class ItemDefinition : ScriptableObject
{
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [SerializeField, TextArea] private string description;
    [SerializeField] private Sprite icon;
    [SerializeField] private ItemStatModifier[] modifiers = Array.Empty<ItemStatModifier>();

    public string Id => id;
    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;

    // A separate scope per acquisition: no acquired state is stored on the asset.
    public ModifierScope Apply(PlayerStats stats)
    {
        var scope = new ModifierScope(stats);
        try
        {
            foreach (var modifier in modifiers)
                scope.AddModifier(modifier.stat, modifier.type, modifier.amount);
            return scope;
        }
        catch
        {
            scope.Dispose();
            throw;
        }
    }
}
