using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(PlayerCharacter))]
[RequireComponent(typeof(PlayerActionController))]
public sealed class PlayerAbilityController : MonoBehaviour
{
    [Header("Temporary HUD")]
    [SerializeField]
    private Slider specialGaugeSlider;

    [Header("Debug")]
    [SerializeField]
    private bool debugLog = true;

    private PlayerCharacter character;
    private PlayerActionController actionController;


    private void Awake()
    {
        character = GetComponent<PlayerCharacter>();

        actionController =
            GetComponent<PlayerActionController>();
    }


    private void OnEnable()
    {
        actionController.CoreAbilityRequested +=
            OnAbilityRequested;
    }


    private void Start()
    {
        if (specialGaugeSlider == null)
        {
            Debug.LogWarning(
                $"{name}: Special Gauge Slider가 연결되지 않았습니다.",
                this
            );
        }
        else
        {
            // 이 Slider는 표시 전용이다.
            specialGaugeSlider.interactable = false;
            specialGaugeSlider.wholeNumbers = false;
        }

        RefreshSlider();
    }


    private void Update()
    {
        CharacterAbilityRuntime ability =
            character.AbilityRuntime;

        // 활성 여부와 무관하게 매 프레임 갱신.
        // 버서커의 비활성 상태 게이지 회복도 여기서 처리한다.
        if (ability != null && !ability.IsDisposed)
        {
            ability.UpdateAbility(Time.deltaTime);
        }

        RefreshSlider();
    }


    private void OnAbilityRequested()
    {
        CharacterAbilityRuntime ability =
            character.AbilityRuntime;

        if (ability == null || ability.IsDisposed)
            return;

        bool accepted = ability.RequestAbility();

        if (debugLog)
        {
            SpecialGaugeRuntime gauge =
                character.SpecialGauge;

            float current =
                gauge != null ? gauge.Current : 0f;

            float maximum =
                gauge != null ? gauge.Maximum : 0f;

            Debug.Log(
                $"[Ability] Accepted={accepted}, " +
                $"Active={ability.IsActive}, " +
                $"Gauge={current:F1}/{maximum:F1}, " +
                $"MoveSpeed={character.Stats.MoveSpeed:F2}",
                this
            );
        }

        RefreshSlider();
    }


    private void RefreshSlider()
    {
        if (specialGaugeSlider == null)
            return;

        SpecialGaugeRuntime gauge =
            character.SpecialGauge;

        specialGaugeSlider.minValue = 0f;

        if (gauge == null)
        {
            specialGaugeSlider.maxValue = 1f;
            specialGaugeSlider.SetValueWithoutNotify(0f);
            return;
        }

        specialGaugeSlider.maxValue = gauge.Maximum;

        specialGaugeSlider.SetValueWithoutNotify(
            gauge.Current
        );
    }


    private void OnDisable()
    {
        if (actionController != null)
        {
            actionController.CoreAbilityRequested -=
                OnAbilityRequested;
        }

        // 컨트롤러가 비활성화될 때
        // 버프가 캐릭터에 남지 않도록 종료.
        if (character == null)
            return;

        CharacterAbilityRuntime ability =
            character.AbilityRuntime;

        if (ability != null &&
            !ability.IsDisposed &&
            ability.IsActive)
        {
            ability.Deactivate();
        }
    }
}