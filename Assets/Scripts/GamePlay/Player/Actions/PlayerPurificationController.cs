using UnityEngine;

[RequireComponent(typeof(PlayerActionController), typeof(PlayerStats), typeof(PlayerModeController))]
public sealed class PlayerPurificationController : MonoBehaviour
{
    [Header("Scene references")]
    [SerializeField] private CorruptionField corruptionField;
    [SerializeField] private Camera gameplayCamera;
    [SerializeField] private SurvivalRunController run;
    [Header("Purification")]
    [SerializeField, Range(0,16)] private int purificationRadiusCells = 1;
    [SerializeField] private LayerMask blockingLayers = 1 << 8;
    private PlayerModeController mode;
    private InputReader input;
    private PlayerStats stats;
    public CorruptionField Field => corruptionField;
    public int RadiusCells => purificationRadiusCells;
    public bool IsPurificationMode => mode != null && mode.CurrentMode == PlayerMode.Purification;
    public float Range => stats != null ? Mathf.Max(0,stats.PurificationRange) : 0;
    public Vector2 Target { get; private set; }
    public bool CanReach { get; private set; }
    public string TargetStatus { get; private set; } = "E: 정화 모드";
    public bool IsChanneling { get; private set; }
    private void Awake() { stats=GetComponent<PlayerStats>();mode=GetComponent<PlayerModeController>(); }
    private void Start() { input=AppRoot.Instance.Input;if(gameplayCamera==null)gameplayCamera=Camera.main; }
    private void Update()
    {
        IsChanneling=false;
        if(input==null || gameplayCamera==null || corruptionField==null)return;
        Target=corruptionField.CellCenter(gameplayCamera.ScreenToWorldPoint(input.AimPosition));
        CanReach=CanPurifyAt(Target);
        if(IsPurificationMode && CanReach && Time.deltaTime>0 && (run==null || run.IsRunning))
        {
            float power=Mathf.Max(0,stats.PurificationPower)*Mathf.Max(0,1f+stats.PurificationEfficiency);
            IsChanneling=power>0 && corruptionField.HasCorruption(Target,purificationRadiusCells);
            if(IsChanneling)corruptionField.ApplyPurification(Target,power*Time.deltaTime,purificationRadiusCells);
        }
        if(!IsPurificationMode)TargetStatus="E: 정화 모드";
        else if(!CanReach)TargetStatus="사거리 밖이거나 장애물에 막혀 있습니다.";
        else if(corruptionField.HasCorruption(Target,purificationRadiusCells))TargetStatus=$"자동 정화 · {purificationRadiusCells*2+1}×{purificationRadiusCells*2+1} · 진행 {corruptionField.Progress(Target,purificationRadiusCells):P0}";
        else TargetStatus=corruptionField.IsProtected(Target)?"정화 완료 · 재오염 보호 중":"깨끗한 지면입니다.";
    }
    public bool CanPurifyAt(Vector2 target)
    {
        if(corruptionField==null || Range<=0 || ((Vector2)transform.position-target).sqrMagnitude>Range*Range)return false;
        return Physics2D.Linecast(transform.position,target,blockingLayers).collider==null;
    }
    private void OnDisable() => IsChanneling=false;
}
