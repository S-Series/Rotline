using System;
using UnityEngine;

[Serializable]
public sealed class RunReward
{
    public string title;
    [TextArea] public string description;
    public ItemDefinition item;
    public AttackRecipeModifier attack;
    public bool requiresModularAttack;
}

[CreateAssetMenu(menuName = "Game/Run/Survival Run")]
public sealed class SurvivalRunDefinition : ScriptableObject
{
    [Min(10f)] public float durationSeconds = 600f;
    [Min(1f)] public float stageSeconds = 60f;
    [Min(1)] public int maximumEnemies = 160;
    [Min(1)] public int treasureEveryKills = 25;
    [Min(0f)] public float experiencePerKill = 1f;
    [Range(0f, 1f)] public float healingDropChance = 0.05f;
    [Min(0f)] public float healAmount = 10f;
    public ItemDefinition[] levelRewards = Array.Empty<ItemDefinition>();
    public RunReward[] treasures = Array.Empty<RunReward>();
}
