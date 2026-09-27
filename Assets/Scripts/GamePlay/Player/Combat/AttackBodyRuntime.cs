using System;
using System.Collections.Generic;
using UnityEngine;

// One independently owned attack entity. No state is written to shared assets.
public sealed class AttackBodyRuntime : IDisposable
{
    private readonly AttackRecipe recipe;
    private readonly AttackSnapshot attack;
    private readonly Transform owner;
    private readonly Vector2 origin;
    private Quaternion rotation;
    private Vector2 direction;
    private readonly List<Vector2> points = new();
    private readonly List<Collider2D> overlaps = new();
    private readonly HashSet<(EnemyActor, uint)> hitTargets = new();
    private readonly AttackHitPolicyRuntime hitPolicy;
    private readonly AttackTrajectoryRuntime trajectory;
    private readonly Func<EnemyActor, DamageResult> applyDamage;
    private readonly bool channel;
    private readonly ContactFilter2D filter;
    private readonly GameObject visual;
    private readonly LineRenderer line;
    private readonly Material material;
    private readonly float collisionRadius;
    private readonly float speed;
    private float age;
    private bool disposed;
    private bool scanned;
    private int targetsHit;
    public float Age => age;
    public Vector2 Position => visual != null ? (Vector2)visual.transform.position : origin;
    public bool ExpiredNaturally { get; private set; }
    public bool IsChannel => channel;
    public void SetAim(Vector2 aim)
    {
        direction = aim.normalized;
        rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        trajectory?.SetAim(direction);
    }

    public AttackBodyRuntime(AttackRecipe recipe, AttackSnapshot attack, Vector2 direction,
        Transform owner, LayerMask layers, Vector2? spawnOrigin = null,
        Func<EnemyActor, DamageResult> applyDamage = null, bool channel = false)
    {
        this.recipe = recipe.Copy();
        this.attack = attack;
        this.owner = owner;
        this.direction = direction;
        origin = spawnOrigin ?? (Vector2)owner.position;
        this.applyDamage = applyDamage;
        this.channel = channel;
        hitPolicy = recipe.CreateHitPolicy();
        rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        speed = recipe.speed * attack.ProjectileSpeedMultiplier;
        trajectory = recipe.trajectory != null ? recipe.trajectory.CreateRuntime(owner, layers, origin, direction, speed) : null;
        if (recipe.trajectory != null && trajectory == null) throw new InvalidOperationException("Trajectory returned no runtime.");
        float radius = recipe.radius * (recipe.radiusScalesWithCharge ? Mathf.Max(0.05f, attack.Charge) : 1f);
        recipe.shape.BuildPoints(points, attack.Range, radius);
        if (points.Count == 0 || points.Count > 512) throw new ArgumentException("Shape must contain 1..512 points.");
        foreach (var point in points)
            if (float.IsNaN(point.x) || float.IsNaN(point.y) || float.IsInfinity(point.x) || float.IsInfinity(point.y))
                throw new ArgumentException("Shape contains a non-finite point.");
        collisionRadius = points.Count == 1 ? radius : recipe.thickness * 0.5f;
        filter = new ContactFilter2D { useTriggers = true };
        filter.SetLayerMask(layers);
        visual = new GameObject("AttackBody");
        // Place visuals in the owner's scene so scene unload also cleans them up.
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(visual, owner.gameObject.scene);
        line = visual.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.startWidth = line.endWidth = collisionRadius * 2f;
        line.startColor = line.endColor = new Color(0.4f, 1f, 1f, 1f);
        line.sortingOrder = 20;
        var shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            material = new Material(shader);
            line.sharedMaterial = material;
        }
        line.positionCount = Mathf.Max(2, points.Count);
        for (int i = 0; i < points.Count; i++) line.SetPosition(i, points[i]);
        if (points.Count == 1)
        {
            line.SetPosition(1, points[0] + Vector2.right * 0.001f);
            line.numCapVertices = 12;
        }
        SetPose(0f);
    }

    public bool Tick(float deltaTime)
    {
        if (disposed) return false;
        if (owner == null || !owner.gameObject.activeInHierarchy) return false;
        Physics2D.SyncTransforms();
        float end = channel ? age + deltaTime : Mathf.Min(recipe.lifetime, age + deltaTime);
        // Bound temporal and spatial gaps, including a lifetime crossed by one frame.
        // This is sampled collision, not an exact continuous solver for arbitrary curves.
        float maximumSpeed = trajectory != null ? trajectory.MaximumSpeed : 0f;
        float stepTime = Mathf.Min(1f / 120f, collisionRadius / Mathf.Max(1f, maximumSpeed));
        if (recipe.repeatHitSeconds > 0f) stepTime = Mathf.Min(stepTime, recipe.repeatHitSeconds);
        int steps = Mathf.Clamp(Mathf.CeilToInt((end - age) / stepTime), 1, 2048);
        float start = age;
        for (int i = scanned ? 1 : 0; i <= steps; i++)
        {
            float time = Mathf.Lerp(start, end, i / (float)steps);
            SetPose(time);
            Scan(time);
            scanned = true;
            if (disposed) return false;
        }
        age = end;
        ExpiredNaturally = !channel && age >= recipe.lifetime;
        return !ExpiredNaturally;
    }

    private void SetPose(float time)
    {
        AttackPose pose = trajectory != null ? trajectory.Evaluate(time) : new AttackPose(origin, rotation);
        visual.transform.SetPositionAndRotation(pose.Position, pose.Rotation);
    }

    private void Scan(float time)
    {
        hitPolicy.BeginSample(time);
        for (int i = 0; i < points.Count && !disposed; i++)
        {
            Vector2 a = visual.transform.TransformPoint(points[i]);
            overlaps.Clear();
            if (points.Count == 1)
                Physics2D.OverlapCircle(a, collisionRadius, filter, overlaps);
            else
            {
                if (i == points.Count - 1) break;
                Vector2 b = visual.transform.TransformPoint(points[i + 1]);
                Vector2 delta = b - a;
                float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
                Physics2D.OverlapCapsule((a + b) * 0.5f,
                    new Vector2(delta.magnitude + collisionRadius * 2f, collisionRadius * 2f),
                    CapsuleDirection2D.Horizontal, angle, filter, overlaps);
            }
            foreach (var other in overlaps)
            {
                if (disposed) break;
                if (other == null) continue;
                var enemy = other.GetComponentInParent<EnemyActor>();
                if (enemy == null || enemy.IsDead || !enemy.isActiveAndEnabled) continue;
                var key = (enemy, enemy.SpawnVersion);
                if (!hitPolicy.TryHit(enemy, time)) continue;
                bool firstHit = hitTargets.Add(key);
                var result = applyDamage != null ? applyDamage(enemy)
                    : enemy.TakeDamage(new DamageContext(owner, attack.Damage, direction));
                if (result.AppliedDamage > 0f && firstHit && !recipe.unlimitedTargets && ++targetsHit > recipe.pierceCount)
                    Dispose();
            }
        }
        hitPolicy.EndSample();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (visual != null) visual.SetActive(false);
        DestroyOwned(visual);
        DestroyOwned(material);
    }

    private static void DestroyOwned(UnityEngine.Object value)
    {
        if (value == null) return;
        if (Application.isPlaying) UnityEngine.Object.Destroy(value);
        else UnityEngine.Object.DestroyImmediate(value);
    }
}
