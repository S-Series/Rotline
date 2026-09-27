using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerStats))]
public sealed class PlayerMovement : MonoBehaviour
{
    private Rigidbody2D rb;
    private PlayerStats stats;

    private InputReader inputReader;
    private float dodgeUntil;
    private float nextDodge;
    private Vector2 dodgeVelocity;
    public bool TryDodge()
    {
        if (inputReader == null || Time.timeScale <= 0f || Time.time < nextDodge) return false;
        Vector2 direction = inputReader.Move;
        if (direction.sqrMagnitude < 0.01f) return false;
        dodgeVelocity = direction.normalized * stats.MoveSpeed * 2.5f;
        dodgeUntil = Time.time + 0.18f;
        nextDodge = Time.time + 1.2f;
        GetComponent<PlayerHealth>()?.GrantInvulnerability(0.18f);
        return true;
    }


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
        if (Time.time < dodgeUntil) { rb.linearVelocity = dodgeVelocity; return; }
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
