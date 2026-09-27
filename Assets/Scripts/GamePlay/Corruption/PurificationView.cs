using UnityEngine;
using UnityEngine.UI;

// All display objects are authored in Gameplay.unity; only their values and geometry change.
public sealed class PurificationView : MonoBehaviour
{
    [SerializeField] private PlayerPurificationController player;
    [SerializeField] private SurvivalRunController run;
    [SerializeField] private Canvas hud;
    [SerializeField] private Text status;
    [SerializeField] private LineRenderer rangeRing;
    [SerializeField] private LineRenderer targetRing;
    [SerializeField] private LineRenderer beam;
    [SerializeField] private Color readyColor = new(0.2f,1f,0.8f,0.7f);
    [SerializeField] private Color blockedColor = new(1f,0.25f,0.2f,0.7f);
    private PlayerCombatController combat;
    private void Awake() => combat = player != null ? player.GetComponent<PlayerCombatController>() : null;
    private void LateUpdate()
    {
        bool playing = run == null || run.IsRunning;
        hud.enabled = playing;
        bool aiming = playing && player.IsPurificationMode;
        rangeRing.enabled = targetRing.enabled = aiming;
        beam.enabled = aiming && player.IsChanneling;
        if(!playing || player.Field == null)return;
        var field=player.Field;
        status.text=$"오염 {field.CorruptedCellCount}칸 · 정화 누적 {field.TotalPurifiedCells}칸\n"+
            (aiming?$"정화 모드 · Q: 전투로 복귀\n{player.TargetStatus}":$"전투 모드 · 공격 사거리 {combat?.PreviewRange:0.#}\nE: 조준한 오염 자동 정화");
        if(!aiming)return;
        Ring(rangeRing,player.transform.position,player.Range);
        targetRing.positionCount=4;
        for(int i=0;i<4;i++)targetRing.SetPosition(i,field.AreaCorner(player.Target,player.RadiusCells,i));
        targetRing.startColor=targetRing.endColor=player.CanReach?readyColor:blockedColor;
        if(beam.enabled){beam.SetPosition(0,player.transform.position);beam.SetPosition(1,player.Target);}
    }
    private static void Ring(LineRenderer line,Vector2 center,float radius)
    {
        for(int i=0;i<line.positionCount;i++)
        {
            float angle=i*Mathf.PI*2/line.positionCount;
            line.SetPosition(i,center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius);
        }
    }
    private void OnDisable(){if(hud!=null)hud.enabled=false;if(rangeRing!=null)rangeRing.enabled=false;if(targetRing!=null)targetRing.enabled=false;if(beam!=null)beam.enabled=false;}
}
