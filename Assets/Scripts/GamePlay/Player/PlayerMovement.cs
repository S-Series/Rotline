using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public sealed class PlayerMovement : MonoBehaviour
{
    [SerializeField]
    private float moveSpeed = 5f;

    [SerializeField]
    private float acceleration = 25f;

    [SerializeField]
    private float deceleration = 15f;

    private Rigidbody2D rb;
    private InputReader inputReader;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        inputReader = AppRoot.Instance.Input;
    }

    private void FixedUpdate()
    {
        Vector2 direction = inputReader.Move;

        if (direction.sqrMagnitude > 1f)
            direction.Normalize();

        Vector2 targetVelocity = direction * moveSpeed;

        float changeRate = direction.sqrMagnitude > 0f
            ? acceleration
            : deceleration;

        rb.linearVelocity = Vector2.MoveTowards(
            rb.linearVelocity,
            targetVelocity,
            changeRate * Time.fixedDeltaTime
        );
    }
}