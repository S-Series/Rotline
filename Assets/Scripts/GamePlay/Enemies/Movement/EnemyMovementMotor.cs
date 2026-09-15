using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public sealed class EnemyMovementMotor : MonoBehaviour
{
    private Rigidbody2D rb;

    public Vector2 Position => rb.position;
    public Vector2 Velocity => rb.linearVelocity;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public void SetVelocity(Vector2 velocity)
    {
        rb.linearVelocity = velocity;
    }

    public void Stop()
    {
        rb.linearVelocity = Vector2.zero;
    }
}