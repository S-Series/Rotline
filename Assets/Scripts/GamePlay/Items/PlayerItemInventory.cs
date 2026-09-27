using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PlayerCharacter))]
public sealed class PlayerItemInventory : MonoBehaviour
{
    [SerializeField] private List<ItemDefinition> startingItems = new();
    [SerializeField] private ItemDefinition debugItem;
    private readonly List<ItemDefinition> items = new();
    private readonly List<ModifierScope> scopes = new();
    private PlayerStats stats;
    private bool started;
    private bool changing;

    public int ItemCount => items.Count;
    public int Count(ItemDefinition item) => items.FindAll(entry => entry == item).Count;

    private void Awake() => stats = GetComponent<PlayerStats>();
    private void Start()
    {
        started = true;
        foreach (var item in startingItems) AddItem(item);
    }
    private void OnEnable()
    {
        if (!started) return;
        changing = true;
        try
        {
            foreach (var item in items)
            {
                var scope = item.Apply(stats);
                if (!isActiveAndEnabled) { scope.Dispose(); return; }
                scopes.Add(scope);
            }
        }
        catch
        {
            ReleaseScopes();
            throw;
        }
        finally { changing = false; }
    }
    public bool AddItem(ItemDefinition item)
    {
        if (!started || !isActiveAndEnabled || changing || item == null) return false;
        changing = true;
        try
        {
            var scope = item.Apply(stats);
            // Stat observers may disable the inventory during application.
            if (!isActiveAndEnabled) { scope.Dispose(); return false; }
            items.Add(item);
            scopes.Add(scope);
            return true;
        }
        finally { changing = false; }
    }
    public bool RemoveItem(ItemDefinition item)
    {
        if (!started || !isActiveAndEnabled || changing || item == null) return false;
        int index = items.LastIndexOf(item);
        if (index < 0) return false;
        changing = true;
        try
        {
            // Remove before callbacks so OnDisable cannot dispose the same slot twice.
            var scope = scopes[index];
            scopes.RemoveAt(index);
            items.RemoveAt(index);
            scope.Dispose();
            return true;
        }
        finally { changing = false; }
    }
    private void ReleaseScopes()
    {
        var active = scopes.ToArray();
        scopes.Clear();
        foreach (var scope in active) scope.Dispose();
    }
    private void OnDisable() => ReleaseScopes();
    private void OnDestroy() => ReleaseScopes();

    [ContextMenu("Debug/Add Selected Item (Play Mode)")]
    private void DebugAdd()
    {
        if (Application.isPlaying)
            Debug.Log($"[Item] Added={AddItem(debugItem)}, Item={debugItem?.DisplayName}", this);
    }
    [ContextMenu("Debug/Remove Selected Item (Play Mode)")]
    private void DebugRemove()
    {
        if (Application.isPlaying)
            Debug.Log($"[Item] Removed={RemoveItem(debugItem)}, Item={debugItem?.DisplayName}", this);
    }
}
