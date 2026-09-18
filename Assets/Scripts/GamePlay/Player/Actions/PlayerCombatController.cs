using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(PlayerCharacter))]
[RequireComponent(typeof(PlayerActionController))]
public sealed class PlayerCombatController : MonoBehaviour
{
    [Header("Weapon")]
    [SerializeField]
    private WeaponAttackDefinition equippedWeapon;

    [SerializeField]
    private LayerMask enemyLayers = ~0;

    [Header("References")]
    [SerializeField]
    private Camera gameplayCamera;

    [Header("Attack Effects")]
    [SerializeField]
    private List<AttackEffectDefinition> startingEffects = new();

    [SerializeField]
    private AttackEffectDefinition debugRelic;

    private readonly List<AttackEffectDefinition> equippedEffects = new();

    private PlayerCharacter character;
    private PlayerActionController actionController;
    private InputReader inputReader;

    private WeaponAttackRuntime weaponRuntime;


    private void Awake()
    {
        character = GetComponent<PlayerCharacter>();

        actionController =
            GetComponent<PlayerActionController>();

            foreach (AttackEffectDefinition effect in startingEffects)
        {
            EquipEffect(effect);
        }
    }


    private void OnEnable()
    {
        actionController.PrimaryStarted += OnPrimary;
        actionController.PrimaryHeld += OnPrimary;
    }


    private void Start()
    {
        inputReader = AppRoot.Instance.Input;

        if (gameplayCamera == null)
            gameplayCamera = Camera.main;

        CreateRuntime();
    }


    private void Update()
    {
        weaponRuntime?.Tick(Time.deltaTime);
    }


    private void FixedUpdate()
    {
        weaponRuntime?.FixedTick();
    }


    private void OnPrimary(PlayerMode mode)
    {
        if (mode != PlayerMode.Combat)
            return;

        if (inputReader == null ||
            gameplayCamera == null)
        {
            return;
        }

        CreateRuntime();

        if (weaponRuntime == null)
            return;

        Vector2 origin = transform.position;

        Vector2 cursor = inputReader.AimPosition;

        float cameraDepth = Mathf.Abs(
            gameplayCamera.transform.position.z -
            transform.position.z
        );

        Vector2 worldCursor =
            gameplayCamera.ScreenToWorldPoint(
                new Vector3(
                    cursor.x,
                    cursor.y,
                    cameraDepth
                )
            );

        Vector2 direction = worldCursor - origin;

        weaponRuntime.RequestAttack(direction);
    }


    private void CreateRuntime()
    {
        if (weaponRuntime != null ||
            equippedWeapon == null ||
            character.Stats == null)
        {
            return;
        }

        weaponRuntime = equippedWeapon.CreateRuntime(
            character.Stats,
            transform,
            enemyLayers
        );

        if (weaponRuntime != null)
        {
            weaponRuntime.AttackActiveStarted +=
                OnAttackActiveStarted;
        }
    }

    // 유물 획득
    public bool EquipEffect(AttackEffectDefinition effect)
    {
        if (effect == null || equippedEffects.Contains(effect))
            return false;

        equippedEffects.Add(effect);
        return true;
    }


    // 유물 제거
    public bool UnequipEffect(AttackEffectDefinition effect)
    {
        if (effect == null)
            return false;

        return equippedEffects.Remove(effect);
    }


    // 공격 활성화 이벤트
    private void OnAttackActiveStarted(AttackSnapshot attack)
    {
        var context = new AttackEffectContext(
            attack,
            transform,
            enemyLayers,
            character.Stats.ProjectileSpeedMultiplier
        );

        // 실행 중 효과 목록이 변경되어도
        // 현재 발동할 효과 집합은 고정한다.
        AttackEffectDefinition[] effects =
            equippedEffects.ToArray();

        foreach (AttackEffectDefinition effect in effects)
        {
            if (effect != null)
            {
                effect.OnAttack(in context);
            }
        }
    }


    // Play Mode 테스트: 유물 획득
    [ContextMenu("Debug/Pick Up Relic (Play Mode)")]
    private void DebugPickupRelic()
    {
        if (!Application.isPlaying)
            return;

        bool success = EquipEffect(debugRelic);

        Debug.Log(
            $"[Relic] Equipped={success}, " +
            $"Relic={debugRelic?.name}",
            this
        );
    }


    // Play Mode 테스트: 유물 제거
    [ContextMenu("Debug/Remove Relic (Play Mode)")]
    private void DebugRemoveRelic()
    {
        if (!Application.isPlaying)
            return;

        bool success = UnequipEffect(debugRelic);

        Debug.Log(
            $"[Relic] Removed={success}, " +
            $"Relic={debugRelic?.name}",
            this
        );
    }


    private void OnDisable()
    {
        if (weaponRuntime != null)
        {
            weaponRuntime.AttackActiveStarted -=
                OnAttackActiveStarted;

            weaponRuntime.Dispose();
            weaponRuntime = null;
        }
    }


    private void OnDestroy()
    {
        weaponRuntime?.Dispose();
        weaponRuntime = null;
    }


}