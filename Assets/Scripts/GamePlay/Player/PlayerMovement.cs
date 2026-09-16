using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerStats))]
public sealed class PlayerMovement : MonoBehaviour
{
    private Rigidbody2D rb;
    private PlayerStats stats;

    private InputReader inputReader;


    private void Awake()
    {
        rb =
            GetComponent<Rigidbody2D>();

        stats =
            GetComponent<PlayerStats>();
    }


    private void Start()
    {
        inputReader =
            AppRoot.Instance.Input;
    }


    private void FixedUpdate()
    {
        Vector2 direction =
            inputReader.Move;

        if (direction.sqrMagnitude > 1f)
        {
            direction.Normalize();
        }


        Vector2 targetVelocity =
            direction *
            stats.MoveSpeed;


        float changeRate =
            direction.sqrMagnitude > 0f
                ? stats.Acceleration
                : stats.Deceleration;


        rb.linearVelocity =
            Vector2.MoveTowards(
                rb.linearVelocity,
                targetVelocity,
                changeRate *
                Time.fixedDeltaTime
            );
    }
}