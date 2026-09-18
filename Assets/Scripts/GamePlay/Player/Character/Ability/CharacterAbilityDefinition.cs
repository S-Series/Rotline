using System;
using UnityEngine;

public abstract class CharacterAbilityDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField]
    private string abilityId;

    [SerializeField]
    private string displayName;

    [Header("Settings")]
    [Min(0f)]
    [SerializeField]
    private float cooldown = 10f;

    public string AbilityId => abilityId;
    public string DisplayName => displayName;
    public float Cooldown => cooldown;


    // 새로운 공용 생성 경로.
    // 각 능력은 필요하다면 이 메서드를 재정의하여
    // Context에서 특수 게이지 등의 의존성을 전달받는다.
    public virtual CharacterAbilityRuntime CreateRuntime(
        AbilityContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        // 아직 Context 방식을 구현하지 않은 능력은
        // 기존 생성 메서드로 연결하여 호환성을 유지한다.
        return CreateRuntime(context.Stats);
    }


    // 기존 생성 경로.
    // 현재 구현된 능력과의 호환성을 위해 유지한다.
    public abstract CharacterAbilityRuntime CreateRuntime(
        PlayerStats stats
    );
}