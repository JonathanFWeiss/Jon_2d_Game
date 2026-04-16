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
    [SerializeField] private float jumpCutMultiplier = .5f;
    private bool doubleJumpAvailable = true; // Tracks if the player can still double jump
    [SerializeField] private bool canDoubleJump = true;//for if the player can double jump or not, set in inspector
    private Rigidbody2D rb;
    public bool isGrounded { get; private set; }

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheckTransform;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer;
    private bool jumpRequested, dashRequested, attackRequested, isDashing, jumpcutRequested;
    private float dashDirection = 1f;
    private Vector3 localScale;


    private Vector2 movementVector;

    public Animator animator;


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
animator = GetComponent<Animator>();
    }

    // Update is called once per frame
    void Update()
    {
        animator.SetFloat("xSpeedABS", Mathf.Abs(movementVector.x));
        animator.SetBool("isGrounded", isGrounded);

    }

    void FixedUpdate()
    {
        doGroundCheck();
        if (isGrounded)
        { doubleJumpAvailable = canDoubleJump; } // Reset double jump availability when jump cut is applied
        // Don't apply normal movement during dash
        if (!isDashing)
        {
            rb.linearVelocityX = movementVector.x * movementSpeed;
        }
        //Debug.Log("Current velocity: " + rb.linearVelocityX);

        if (jumpRequested && (isGrounded || doubleJumpAvailable))
        {
            Debug.Log("Processing jump request. Jumprequested: " + jumpRequested + ", isGrounded: " + isGrounded + ", doubleJumpAvailable: " + doubleJumpAvailable);
            if (rb.linearVelocity.y < 0)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0); // Reset downward velocity for consistent jumps
                Debug.Log("Resetting downward velocity for consistent jump height");
            }
            rb.AddForce(new Vector2(0, jumpForce), ForceMode2D.Impulse);
            jumpRequested = false;

            if (!isGrounded && doubleJumpAvailable)
            {
                doubleJumpAvailable = false; // Consume double jump
                Debug.Log("Double jump used");
            }

        }

        if (jumpcutRequested && rb.linearVelocity.y > 0)
        {

            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * jumpCutMultiplier);
            jumpcutRequested = false;
            Debug.Log("Jump cut applied");
        }


        if (dashRequested)
        {
            // Store current direction before starting dash
            if (movementVector.x != 0)
            {
                dashDirection = Mathf.Sign(movementVector.x);
            }
            else if (movementVector.x == 0f)
            {
                dashDirection = Mathf.Sign(transform.localScale.x); // Default to facing direction if no input
            }
            // Implement dash logic here
            //rb.AddForce(new Vector2(dashForce, 0), ForceMode2D.Impulse);
            StartCoroutine(DashCoroutine(dashDuration));
            dashRequested = false;
        }
        if (Mathf.Abs(movementVector.x) > 0.1f)
        {
            localScale = new Vector3(Mathf.Sign(movementVector.x) * Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
            transform.localScale = localScale;
        }

    }

    public void Jump()
    {
        if (jumpRequested || isDashing || (!doubleJumpAvailable && !isGrounded))
        {
            Debug.Log("Jump conditions not met, all must be false: jumpRequested: " + jumpRequested + ", isDashing: " + isDashing + ", (!doubleJumpAvailable && !isGrounded) " + (!doubleJumpAvailable && !isGrounded));
            return;
        }
        Debug.Log("Jump conditions met, processing jump jumpRequested: " + jumpRequested + ", isDashing: " + isDashing + ", (!doubleJumpAvailable && !isGrounded) " + (!doubleJumpAvailable && !isGrounded));

        jumpRequested = true;
        Debug.Log("Jump action triggered");
        jumpcutRequested = false; // Reset jump cut request when a new jump is initiated
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

    public void JumpCut()
    {
        jumpcutRequested = true;
        Debug.Log("JumpCut action triggered");
    }

    private void doGroundCheck()
    {
        Collider2D groundCollider = Physics2D.OverlapCircle(groundCheckTransform.position, groundCheckRadius, groundLayer);
        Debug.Log("Ground check result: " + (groundCollider != null ? "Grounded" : "Not Grounded"));
        if (groundCollider) isGrounded = true;
        else isGrounded = false;
    }


    private void OnDrawGizmosSelected()
    {
        if (groundCheckTransform != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(groundCheckTransform.position, groundCheckRadius);
        }
    }
}