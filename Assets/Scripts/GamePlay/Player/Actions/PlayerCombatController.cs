using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PlayerCharacter))]
[RequireComponent(typeof(PlayerActionController))]
[RequireComponent(typeof(PlayerModeController))]
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
    [SerializeField] private List<AttackRecipeModifier> startingAttackItems = new();
    private readonly Dictionary<AttackRecipeModifier, int> attackItems = new();
    private AttackEffectDefinition[] effectSnapshot = System.Array.Empty<AttackEffectDefinition>();
    private AttackEffectDefinition[] currentAttackEffects = System.Array.Empty<AttackEffectDefinition>();
    private float nextAttackTime;
    private readonly List<AttackProjectile> effectProjectiles = new();

    private PlayerCharacter character;
    private PlayerActionController actionController;
    private PlayerModeController modeController;
    private InputReader inputReader;

    private WeaponAttackRuntime weaponRuntime;

    private bool started;


    private void Awake()
    {
        character = GetComponent<PlayerCharacter>();

        actionController =
            GetComponent<PlayerActionController>();

        modeController =
            GetComponent<PlayerModeController>();

        foreach (AttackEffectDefinition effect in startingEffects)
        {
            EquipEffect(effect);
        }
        foreach (var item in startingAttackItems) EquipAttackItem(item);
    }


    private void OnEnable()
    {
        actionController.PrimaryStarted += OnPrimaryStarted;
        actionController.PrimaryHeld += OnPrimaryHeld;
        actionController.PrimaryReleased += OnPrimaryReleased;
        actionController.PrimaryCanceled += OnPrimaryCanceled;

        modeController.ModeChanged += OnModeChanged;

        // Start는 최초 1회만 실행된다.
        // 재활성화했을 때도 무기를 다시 장착한다.
        if (started)
        {
            CreateRuntime();
        }
    }


    private void Start()
    {
        inputReader = AppRoot.Instance.Input;

        if (gameplayCamera == null)
        {
            gameplayCamera = Camera.main;
        }

        started = true;

        CreateRuntime();
    }


    private void Update()
    {
        weaponRuntime?.Tick(Time.deltaTime);
        effectProjectiles.RemoveAll(projectile => projectile == null);
    }


    private void FixedUpdate()
    {
        weaponRuntime?.FixedTick();
    }


    private void OnPrimaryStarted(PlayerMode mode) => OnPrimary(mode, 0);
    private void OnPrimaryHeld(PlayerMode mode) => OnPrimary(mode, 1);
    private void OnPrimaryReleased(PlayerMode mode) => OnPrimary(mode, 2);

    private void OnPrimary(PlayerMode mode, int phase)
    {
        if (!isActiveAndEnabled)
            return;

        if (mode != PlayerMode.Combat)
            return;

        // 입력 이벤트의 mode와 현재 모드가 일치하는지
        // 한 번 더 확인한다.
        if (!modeController.IsMode(PlayerMode.Combat))
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

        if (!weaponRuntime.IsAttacking && !weaponRuntime.IsCharging)
            currentAttackEffects = effectSnapshot;
        if (phase == 0) weaponRuntime.PrimaryStarted(direction);
        else if (phase == 1) weaponRuntime.PrimaryHeld(direction);
        else weaponRuntime.PrimaryReleased(direction);
    }


    private void OnPrimaryCanceled(PlayerMode mode)
    {
        if (mode != PlayerMode.Combat)
            return;

        CancelCombat();
    }


    private void OnModeChanged(PlayerMode mode)
    {
        // 좌클릭을 이미 놓았어도 공격 후딜레이가
        // 남아 있을 수 있으므로 모드 자체를 확인한다.
        if (mode != PlayerMode.Combat)
        {
            CancelCombat();
        }
    }

    // Death, stun, run end and other interruptions can share this cleanup path.
    public void CancelCombat()
    {
        weaponRuntime?.CancelAttack();
        ClearEffectProjectiles();
    }


    private void CreateRuntime()
    {
        if (!isActiveAndEnabled)
            return;

        if (weaponRuntime != null ||
            equippedWeapon == null ||
            character.Stats == null)
        {
            return;
        }

        weaponRuntime = equippedWeapon.CreateRuntime(
            character.Stats,
            transform,
            enemyLayers,
            AttackRecipeResolver.Resolve(equippedWeapon.BaseRecipe, attackItems)
        );

        if (weaponRuntime != null)
        {
            weaponRuntime.NextAttackTime = nextAttackTime;
            weaponRuntime.AttackActiveStarted +=
                OnAttackActiveStarted;
        }
    }


    // 유물 획득
    public bool EquipEffect(AttackEffectDefinition effect)
    {
        if (effect == null ||
            equippedEffects.Contains(effect))
        {
            return false;
        }

        equippedEffects.Add(effect);
        effectSnapshot = equippedEffects.ToArray();

        return true;
    }


    // 유물 제거
    public bool UnequipEffect(AttackEffectDefinition effect)
    {
        if (effect == null)
            return false;

        bool removed = equippedEffects.Remove(effect);
        if (removed) effectSnapshot = equippedEffects.ToArray();
        return removed;
    }

    public bool EquipAttackItem(AttackRecipeModifier item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.Id)) return false;
        foreach (var other in attackItems.Keys)
            if (other != item && other.Id == item.Id) return false;
        attackItems.TryGetValue(item, out int count);
        if (count >= item.MaximumStacks) return false;
        attackItems[item] = count + 1;
        try
        {
            if (equippedWeapon != null) AttackRecipeResolver.Resolve(equippedWeapon.BaseRecipe, attackItems);
        }
        catch
        {
            if (count == 0) attackItems.Remove(item); else attackItems[item] = count;
            throw;
        }
        RebuildRuntime();
        return true;
    }

    public bool UnequipAttackItem(AttackRecipeModifier item)
    {
        if (item == null || !attackItems.TryGetValue(item, out int count)) return false;
        if (count == 1) attackItems.Remove(item); else attackItems[item] = count - 1;
        try
        {
            if (equippedWeapon != null) AttackRecipeResolver.Resolve(equippedWeapon.BaseRecipe, attackItems);
        }
        catch
        {
            attackItems[item] = count;
            throw;
        }
        RebuildRuntime();
        return true;
    }

    public void EquipWeapon(WeaponAttackDefinition weapon)
    {
        if (equippedWeapon == weapon) return;
        equippedWeapon = weapon;
        RebuildRuntime();
    }

    private void RebuildRuntime()
    {
        // Loadout changes cancel the current attack and its owned entities.
        // Preserve recovery so item swapping cannot bypass attack speed.
        StopRuntime();
        if (started) CreateRuntime();
    }


    // 공격이 실제 활성화된 순간 발동한다.
    private void OnAttackActiveStarted(AttackSnapshot attack)
    {
        if (!isActiveAndEnabled)
            return;

        if (character.Stats == null)
            return;

        var context = new AttackEffectContext(
            attack,
            transform,
            enemyLayers,
            attack.ProjectileSpeedMultiplier,
            projectile => effectProjectiles.Add(projectile)
        );

        // 효과 실행 도중 장착 목록이 바뀌어도
        // 이번 공격의 효과 목록은 유지한다.
        AttackEffectDefinition[] effects =
            currentAttackEffects;
        WeaponAttackRuntime sourceRuntime = weaponRuntime;

        foreach (AttackEffectDefinition effect in effects)
        {
            // 앞선 효과가 전투를 종료하거나
            // 컴포넌트를 비활성화했을 수 있다.
            if (!isActiveAndEnabled ||
                weaponRuntime != sourceRuntime ||
                !sourceRuntime.IsAttacking)
            {
                break;
            }

            if (effect != null)
            {
                effect.OnAttack(in context);
            }
        }
    }


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
        actionController.PrimaryStarted -= OnPrimaryStarted;
        actionController.PrimaryHeld -= OnPrimaryHeld;
        actionController.PrimaryReleased -= OnPrimaryReleased;
        actionController.PrimaryCanceled -= OnPrimaryCanceled;

        modeController.ModeChanged -= OnModeChanged;

        StopRuntime();
    }


    private void OnDestroy()
    {
        StopRuntime();
    }


    private void StopRuntime()
    {
        ClearEffectProjectiles();
        if (weaponRuntime == null)
            return;

        // 이벤트를 먼저 해제해 Dispose 도중
        // 이 컨트롤러로 콜백이 들어오지 않게 한다.
        WeaponAttackRuntime runtime = weaponRuntime;
        nextAttackTime = Mathf.Max(nextAttackTime, runtime.NextAttackTime);

        weaponRuntime = null;

        runtime.AttackActiveStarted -=
            OnAttackActiveStarted;

        // 진행 중인 공격을 먼저 취소하고
        // 무기 자체를 정리한다.
        runtime.Dispose();
    }

    private void ClearEffectProjectiles()
    {
        foreach (var projectile in effectProjectiles)
            if (projectile != null) projectile.Finish();
        effectProjectiles.Clear();
    }
}
