using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class JonCharacterController : MonoBehaviour
{
    [Header("Movement Parameters")]
    [SerializeField] private float movementSpeed = 5f;
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private float dashSpeed = 10f;
    [SerializeField] private float dashDuration = 0.2f;
    private Rigidbody2D rb;
    public bool isGrounded { get; private set; }

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheckTransform;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer;
    private bool jumpRequested, dashRequested, attackRequested, isDashing = false;
    private float dashDirection = 1f;


    private Vector2 movementVector;


    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        Debug.Log("Rigidbody2D component found: " + rb);
        rb.freezeRotation = true;
    }

    public void Move(Vector2 move)
    {
        movementVector = move;
        //Debug.Log("Move input from character controller: " + move);
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    void FixedUpdate()
    {
        doGroundCheck();
        // Don't apply normal movement during dash
        if (!isDashing)
        {
            rb.linearVelocityX = movementVector.x * movementSpeed;
        }
        //Debug.Log("Current velocity: " + rb.linearVelocityX);

        if (jumpRequested && isGrounded)
        {
            rb.AddForce(new Vector2(0, jumpForce), ForceMode2D.Impulse);
            jumpRequested = false;
        }

        if (dashRequested)
        {
            // Store current direction before starting dash
            if (movementVector.x != 0)
            {
                dashDirection = Mathf.Sign(movementVector.x);
            }
            // Implement dash logic here
            //rb.AddForce(new Vector2(dashForce, 0), ForceMode2D.Impulse);
            StartCoroutine(DashCoroutine(dashDuration));
            dashRequested = false;
        }

    }

    public void Jump()
    {
        if (!isGrounded || jumpRequested || isDashing)
            return;

        jumpRequested = true;
        Debug.Log("Jump action triggered");
    }

    public void Dash()
    {
        dashRequested = true;

        Debug.Log("Dash action triggered");

    }

    IEnumerator DashCoroutine(float seconds)
    {
        Debug.Log("Starting dash coroutine");
        if (isDashing) yield break;
        isDashing = true;
        float prevGravity = rb.gravityScale;
        rb.gravityScale = 0; // Disable gravity
        rb.linearVelocity = new Vector2(dashDirection * dashSpeed, 0); // Apply force
        yield return new WaitForSeconds(seconds); // Wait for dash duration
        rb.gravityScale = prevGravity; // Restore gravity
        isDashing = false;
    }

    public void Attack()
    {
        attackRequested = true;

        Debug.Log("Attack action triggered");

    }

    private void doGroundCheck()
    {
        Collider2D groundCollider = Physics2D.OverlapCircle(groundCheckTransform.position, groundCheckRadius, groundLayer);
        //Debug.Log("Ground check result: " + (groundCollider != null ? "Grounded" : "Not Grounded"));
        if (groundCollider) isGrounded = true;
        else isGrounded = false;
    }
}
