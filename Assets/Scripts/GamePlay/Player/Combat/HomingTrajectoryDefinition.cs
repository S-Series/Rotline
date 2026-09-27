using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Combat/Trajectories/Homing")]
public sealed class HomingTrajectoryDefinition : AttackTrajectoryDefinition
{
    [SerializeField, Min(0.1f)] private float searchRadius = 8f;
    [SerializeField, Min(0f)] private float turnDegreesPerSecond = 180f;
    public override AttackTrajectoryRuntime CreateRuntime(Transform owner, LayerMask layers,
        Vector2 origin, Vector2 direction, float speed) => new Runtime(origin, direction, speed, layers,
            Mathf.Max(0.1f, searchRadius), Mathf.Max(0f, turnDegreesPerSecond));
    private sealed class Runtime : AttackTrajectoryRuntime
    {
        private Vector2 position;
        private float yaw;
        private float age;
        private readonly float speed;
        private readonly float radius;
        private readonly float turn;
        private readonly ContactFilter2D filter;
        private readonly List<Collider2D> overlaps = new();
        public override float MaximumSpeed => speed;
        public Runtime(Vector2 origin, Vector2 direction, float speed, LayerMask layers, float radius, float turn)
        {
            position = origin; yaw = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            this.speed = speed; this.radius = radius; this.turn = turn;
            filter = new ContactFilter2D { useTriggers = true }; filter.SetLayerMask(layers);
        }
        public override AttackPose Evaluate(float time)
        {
            float dt = Mathf.Max(0f, time - age);
            age = time;
            Physics2D.OverlapCircle(position, radius, filter, overlaps);
            EnemyActor closest = null;
            float distance = float.PositiveInfinity;
            foreach (var collider in overlaps)
            {
                if (collider == null) continue;
                var enemy = collider.GetComponentInParent<EnemyActor>();
                if (enemy == null || enemy.IsDead || !enemy.isActiveAndEnabled) continue;
                float candidate = ((Vector2)enemy.transform.position - position).sqrMagnitude;
                if (candidate < distance) { closest = enemy; distance = candidate; }
            }
            if (closest != null)
            {
                Vector2 delta = (Vector2)closest.transform.position - position;
                yaw = Mathf.MoveTowardsAngle(yaw, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, turn * dt);
            }
            var rotation = Quaternion.Euler(0, 0, yaw);
            position += (Vector2)(rotation * Vector2.right) * speed * dt;
            return new AttackPose(position, rotation);
        }
    }
}
