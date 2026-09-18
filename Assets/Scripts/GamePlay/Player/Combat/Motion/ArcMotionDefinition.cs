using UnityEngine;

[CreateAssetMenu(
    fileName = "ArcMotion",
    menuName = "Game/Combat/Motion/Arc"
)]
public sealed class ArcMotionDefinition : AttackMotionDefinition
{
    [Header("Arc")]
    [SerializeField, Range(0f, 360f)]
    private float sweepAngle = 100f;

    [SerializeField]
    private bool alternateDirection = true;

    public override bool AlternateDirection =>
        alternateDirection;


    public override void ApplyPose(
        Transform attackTransform,
        AttackSnapshot attack,
        float progress)
    {
        float directionAngle =
            Mathf.Atan2(
                attack.Direction.y,
                attack.Direction.x
            ) * Mathf.Rad2Deg;

        float offset = Mathf.Lerp(
            -sweepAngle * 0.5f,
            sweepAngle * 0.5f,
            Mathf.Clamp01(progress)
        );

        attackTransform.localPosition = Vector3.zero;

        attackTransform.rotation = Quaternion.Euler(
            0f,
            0f,
            directionAngle + offset
        );
    }
}