using UnityEngine;

[RequireComponent(typeof(PlayerStats))]
public sealed class PlayerCharacter : MonoBehaviour
{
    [SerializeField]
    private CharacterDefinition definition;

    private PlayerStats stats;
    private SpecialGaugeRuntime specialGauge;
    private CharacterAbilityRuntime abilityRuntime;

    public CharacterDefinition Definition => definition;
    public PlayerStats Stats => stats;

    public SpecialGaugeRuntime SpecialGauge => specialGauge;
    public bool HasSpecialGauge => specialGauge != null;

    public CharacterAbilityRuntime AbilityRuntime => abilityRuntime;


    private void Awake()
    {
        stats = GetComponent<PlayerStats>();

        if (definition == null)
        {
            Debug.LogError(
                $"{name}: CharacterDefinition이 설정되지 않았습니다.",
                this
            );

            return;
        }

        // 1. 캐릭터의 기본 스탯 초기화.
        stats.Initialize(definition.BaseStats);

        // 2. 캐릭터가 소유할 특수 게이지 생성.
        float maxResource = stats.MaxResource;

        specialGauge = maxResource > 0f
            ? new SpecialGaugeRuntime(maxResource)
            : null;

        Debug.Log(
            $"[SpecialGauge] Character={name}, " +
            $"Created={HasSpecialGauge}, " +
            $"Current={specialGauge?.Current}, " +
            $"Maximum={specialGauge?.Maximum}",
            this
        );

        // 3. 캐릭터의 어빌리티 설정 가져오기.
        CharacterAbilityDefinition abilityDefinition =
            definition.AbilityDefinition;

        // 어빌리티가 없는 캐릭터도 허용.
        if (abilityDefinition == null)
            return;

        // 4. 공용 실행 환경 생성.
        var context = new AbilityContext(
            stats,
            specialGauge
        );

        // 5. 각 Definition이 자신의 Runtime을 생성한다.
        abilityRuntime = abilityDefinition.CreateRuntime(context);

        if (abilityRuntime == null)
        {
            Debug.LogError(
                $"{name}: 어빌리티 런타임 생성에 실패했습니다.",
                this
            );

            return;
        }

        Debug.Log(
            $"[Ability] Character={name}, " +
            $"Runtime={abilityRuntime.GetType().Name}, " +
            $"Active={abilityRuntime.IsActive}",
            this
        );
    }


    private void OnDestroy()
    {
        // 능력이 적용한 Modifier 등을 해제한다.
        abilityRuntime?.Dispose();
        abilityRuntime = null;

        specialGauge = null;
    }
}