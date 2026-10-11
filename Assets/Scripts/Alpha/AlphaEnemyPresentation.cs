using UnityEngine;

// Only explicitly opted-in renderers receive per-enemy data; authored sprites are retained.
public sealed class AlphaEnemyPresentation : MonoBehaviour
{
    [SerializeField] private SpriteRenderer body;
    [SerializeField] private bool useDefinitionTint;
    [SerializeField] private Transform sizeTarget;
    [SerializeField] private bool useDefinitionSize;

    public void Apply(EnemyDefinition definition)
    {
        if (useDefinitionTint && body != null) body.color = definition.tint;
        if (useDefinitionSize && sizeTarget != null) sizeTarget.localScale = Vector3.one * definition.size;
    }
}
