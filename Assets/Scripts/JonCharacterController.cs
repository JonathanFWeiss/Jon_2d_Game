using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

public class JonCharacterController : MonoBehaviour
{
    [Header("Movement Parameters")]
    [SerializeField] private float movementSpeed = 5f;
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private float dashSpeed = 10f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashCooldown = 0.5f;
    [SerializeField] private float jumpCutMultiplier = .5f;
    [SerializeField] private float coyoteTime = 0.3f;
    private bool doubleJumpAvailable = true; // Tracks if the player can still double jump
    private bool airDashAvailable = true; // Tracks if the player can still air dash
    [SerializeField] private bool canDash = true; // For if the player can air dash or not, set in inspector
    [SerializeField] private bool canDoubleJump = true;//for if the player can double jump or not, set in inspector
    private Rigidbody2D rb;
    public bool isGrounded { get; private set; }

    [Header("Melee Attack")]
    [SerializeField] private Vector2 attackBoxSize = new Vector2(1.25f, 0.85f);
    [SerializeField] private Vector2 attackBoxOffset = new Vector2(0.9f, 0f);

    [SerializeField] private LayerMask attackLayerMask;
    [SerializeField] private int attackDamage = 1;
    [SerializeField] private Vector2 attackPushbackImpulse = new Vector2(4f, 1.5f);
    [SerializeField] private float attackDuration = 1f;
    [SerializeField] private float attackHitDelay = 0.5f;
    [Header("Pogo Parameters")]
    //[SerializeField] private float pogoForce = 10f;
    [SerializeField] private float pogoDuration = 1.5f;
    [SerializeField] private float pogoAttackDelay = 0.5f;
    [SerializeField] private Vector2 pogoAttackBoxSize = new Vector2(1.25f, 0.85f);
    [SerializeField] private Vector2 pogoAttackBoxOffset = new Vector2(0f, -2f);
    [SerializeField] private float pogoBounceCooldown = 0.5f;
    private float nextPogoBounceTime;

    [Header("Hit State")]
    [SerializeField] private float gettingHitDuration = 1f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheckTransform;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer;
    private bool jumpRequested, dashRequested, attackRequested, isDashing, jumpcutRequested, pogoRequested;
    private float dashDirection = 1f;
    private float nextDashTime;
    private float lastGroundedTime = float.NegativeInfinity;
    private float defaultGravityScale = 1f;
    private Vector3 lastGroundedPosition;
    private bool hasLastGroundedPosition;
    private Vector3 localScale;


    private Vector2 movementVector;
    private bool isAttacking;
    private bool isPogoing;
    private bool isAttackHitActive;
    public bool isGettingHit { get; private set; }
    private Coroutine gettingHitCoroutine;

    public Animator animator;
    [SerializeField] private GameObject pogoSlashEffect;


    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        
        Debug.Log("Rigidbody2D component found: " + rb);
        defaultGravityScale = rb.gravityScale;
        rb.freezeRotation = true;
        lastGroundedPosition = transform.position;
        hasLastGroundedPosition = true;

        if (attackLayerMask == 0)
        {
            attackLayerMask = LayerMask.GetMask("Player", "NPCs", "Enemy");
        }
    }

    public void Move(Vector2 move)
    {
        movementVector = move;
        //Debug.Log("Move input from character controller: " + move);
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
    }

    // Update is called once per frame
    void Update()
    {
        

        if (animator == null)
            return;

        animator.SetFloat("xSpeedABS", Mathf.Abs(movementVector.x));
        animator.SetFloat("ySpeed", rb.linearVelocity.y);
        animator.SetBool("isGrounded", isGrounded);
        animator.SetBool("isAttacking", isAttacking);
        animator.SetBool("isGettingHit", isGettingHit);
        animator.SetBool("isDashing", isDashing);
        animator.SetBool("isPogoing", isPogoing);

    }

    

    

    void FixedUpdate()
    {
        doGroundCheck();

        if (pogoRequested)
        {

            pogoRequested = false;
            attackRequested = false; // Cancel attack if pogo is performed
            StartCoroutine(PogoAttackCoroutine(pogoDuration));
            Debug.Log("Pogo executed with jump force: " + jumpForce);
        }
        if (attackRequested)
        {
            StartCoroutine(AttackCoroutine(attackDuration));
            attackRequested = false;
        }
        if (isGrounded)
        {
            doubleJumpAvailable = canDoubleJump;
            airDashAvailable = canDash;
        }
        // Don't apply normal movement during dash or attack
        if (!isDashing && !isAttacking && !isGettingHit)
        {
            rb.linearVelocityX = movementVector.x * movementSpeed;
        }
        //Debug.Log("Current velocity: " + rb.linearVelocityX);

        bool hasGroundJumpAvailable = HasGroundJumpAvailable();
        if (jumpRequested && (hasGroundJumpAvailable || doubleJumpAvailable))
        {
            Debug.Log("Processing jump request. Jumprequested: " + jumpRequested + ", isGrounded: " + isGrounded + ", doubleJumpAvailable: " + doubleJumpAvailable);
            if (rb.linearVelocity.y < 0)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0); // Reset downward velocity for consistent jumps
                Debug.Log("Resetting downward velocity for consistent jump height");
            }
            if (rb.linearVelocity.y > 0)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0); // Reset upward velocity for consistent jumps
                Debug.Log("Resetting upward velocity for consistent jump height");
            }
            rb.AddForce(new Vector2(0, jumpForce), ForceMode2D.Impulse);
            jumpRequested = false;
            lastGroundedTime = float.NegativeInfinity;

            if (!hasGroundJumpAvailable && doubleJumpAvailable)
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

        if (!isDashing && !isAttacking && !isGettingHit)
        {
            if (Mathf.Abs(movementVector.x) > 0.1f)
            {
                localScale = new Vector3(Mathf.Sign(movementVector.x) * Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
                transform.localScale = localScale;
            }
        }

    }

    public void Jump()
    {
        bool hasGroundJumpAvailable = HasGroundJumpAvailable();
        if (jumpRequested || isDashing || (!doubleJumpAvailable && !hasGroundJumpAvailable))
        {
            Debug.Log("Jump conditions not met, all must be false: jumpRequested: " + jumpRequested + ", isDashing: " + isDashing + ", (!doubleJumpAvailable && !hasGroundJumpAvailable) " + (!doubleJumpAvailable && !hasGroundJumpAvailable));
            return;
        }
        Debug.Log("Jump conditions met, processing jump jumpRequested: " + jumpRequested + ", isDashing: " + isDashing + ", (!doubleJumpAvailable && !hasGroundJumpAvailable) " + (!doubleJumpAvailable && !hasGroundJumpAvailable));

        jumpRequested = true;
        Debug.Log("Jump action triggered");
        jumpcutRequested = false; // Reset jump cut request when a new jump is initiated
    }

    public void Dash()
    {
        if (isDashing || Time.time < nextDashTime)
        {
            return;
        }

        if (!isGrounded && !airDashAvailable)
        {
            return;
        }

        dashRequested = true;
        nextDashTime = Time.time + dashCooldown;

        if (!isGrounded)
        {
            airDashAvailable = false;
        }

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
        if (isAttacking || attackRequested)
            return;

        attackRequested = true;

        Debug.Log("Attack action triggered");

    }

    public void Pogo()
    {
        if (isGrounded)
            return;

        // Implement pogo logic here, for example:
        pogoRequested = true; // Bounce up with jump force
        Debug.Log("Pogo action triggered");
    }

    public void JumpCut()
    {
        jumpcutRequested = true;
        Debug.Log("JumpCut action triggered");
    }

    public void StartGettingHit()
    {
        if (gettingHitCoroutine != null)
        {
            StopCoroutine(gettingHitCoroutine);
        }

        gettingHitCoroutine = StartCoroutine(GettingHitCoroutine(gettingHitDuration));
    }

    public void TeleportToLastGroundedPosition(Vector2 offset)
    {
        Vector3 teleportTarget = hasLastGroundedPosition
            ? lastGroundedPosition
            : transform.position;

        teleportTarget += (Vector3)offset;

        transform.position = teleportTarget;
        rb.gravityScale = defaultGravityScale;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        lastGroundedTime = Time.time;
        doubleJumpAvailable = canDoubleJump;
        airDashAvailable = canDash;

        jumpRequested = false;
        dashRequested = false;
        attackRequested = false;
        jumpcutRequested = false;
        pogoRequested = false;
        isDashing = false;
        isAttacking = false;
        isPogoing = false;
    }

    private void doGroundCheck()
    {
        Collider2D groundCollider = Physics2D.OverlapCircle(groundCheckTransform.position, groundCheckRadius, groundLayer);
        //Debug.Log("Ground check result: " + (groundCollider != null ? "Grounded" : "Not Grounded"));
        if (groundCollider)
        {
            isGrounded = true;
            lastGroundedTime = Time.time;
            lastGroundedPosition = transform.position;
            hasLastGroundedPosition = true;
        }
        else isGrounded = false;
    }

    private bool HasGroundJumpAvailable()
    {
        return isGrounded || Time.time <= lastGroundedTime + coyoteTime;
        // This allows the player to still jump for a short time after leaving the ground, making the controls feel more responsive.
    }

    IEnumerator AttackCoroutine(float seconds)
    {
        Debug.Log("Starting attack coroutine");
        if (isAttacking) yield break;

        isAttacking = true;
        rb.linearVelocityX = 0; // Stop horizontal movement during attack 
        float clampedHitDelay = Mathf.Max(0f, attackHitDelay);

        if (clampedHitDelay > 0f)
        {
            yield return new WaitForSeconds(clampedHitDelay);
        }

        isAttackHitActive = true;
        PerformAttackHit();
        yield return null;
        isAttackHitActive = false;

        float remainingAttackTime = Mathf.Max(0f, seconds - clampedHitDelay);
        if (remainingAttackTime > 0f)
        {
            yield return new WaitForSeconds(remainingAttackTime);
        }

        isAttacking = false;
        isAttackHitActive = false;
    }


    IEnumerator PogoAttackCoroutine(float seconds)
    {
        Debug.Log("Starting pogo attack coroutine");
        if (isPogoing) yield break;


        isPogoing = true;
        //rb.linearVelocityX = 0; // Stop horizontal movement during attack 


        isAttackHitActive = true;
        PerformPogoHit();
        yield return null;
        isAttackHitActive = false;

        float remainingPogoTime = Mathf.Max(0f, seconds);
        if (remainingPogoTime > 0f)
        {
            yield return new WaitForSeconds(remainingPogoTime);
        }

        isPogoing = false;
        isAttackHitActive = false;
    }

    IEnumerator GettingHitCoroutine(float seconds)
    {
        isGettingHit = true;
        yield return new WaitForSeconds(seconds);
        isGettingHit = false;
        Debug.Log("Finished getting hit state");
        gettingHitCoroutine = null;
    }

    private void PerformAttackHit()
    {
        Vector2 attackCenter = GetAttackCenter();
        Collider2D[] hits = Physics2D.OverlapBoxAll(attackCenter, attackBoxSize, 0f, attackLayerMask);
        HashSet<Rigidbody2D> processedBodies = new HashSet<Rigidbody2D>();

        foreach (Collider2D hit in hits)
        {
            Debug.Log($"Attack hit detected on {hit.gameObject.name} at position {hit.transform.position}");
            if (hit == null)
                continue;

            Rigidbody2D hitRigidbody = hit.attachedRigidbody;
            if (hitRigidbody == null || hitRigidbody == rb)
                continue;

            if (hitRigidbody.transform.root == transform.root)
                continue;

            if (!processedBodies.Add(hitRigidbody))
                continue;

            if (!TryDealDamage(hitRigidbody.gameObject))
                continue;
            Debug.Log($"Damage successfully dealt to {hitRigidbody.gameObject.name}");

            ApplyPushback(hitRigidbody, attackCenter);
        }
    }

    private void PerformPogoHit()
    {
        Vector2 attackCenter = GetPogoCenter();
        Collider2D[] hits = Physics2D.OverlapBoxAll(attackCenter, pogoAttackBoxSize, 0f, attackLayerMask);
        HashSet<Rigidbody2D> processedBodies = new HashSet<Rigidbody2D>();

        foreach (Collider2D hit in hits)
        {
            Debug.Log($"Attack hit detected on {hit.gameObject.name} at position {hit.transform.position}");
            if (hit == null)
                continue;
            if (hit.attachedRigidbody != null)
                if (Time.time >= nextPogoBounceTime)
                { rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0); // Reset vertical velocity for consistent pogo bounces
                    rb.AddForce(new Vector2(0, 1.2f*jumpForce), ForceMode2D.Impulse);
                    nextPogoBounceTime = Time.time + pogoBounceCooldown;
                    Debug.Log($"Pogo bounce applied to {rb.gameObject.name} with jump force: {1.2*jumpForce}");
                }



            Rigidbody2D hitRigidbody = hit.attachedRigidbody;
            if (hitRigidbody == null || hitRigidbody == rb)
                continue;

            if (hitRigidbody.transform.root == transform.root)
                continue;

            if (!processedBodies.Add(hitRigidbody))
                continue;

            if (!TryDealDamage(hitRigidbody.gameObject))
                continue;
            Debug.Log($"Damage successfully dealt to {hitRigidbody.gameObject.name}");

            ApplyPushback(hitRigidbody, attackCenter);
        }
    }

    private Vector2 GetAttackCenter()
    {
        return (Vector2)transform.position + new Vector2(
            Mathf.Abs(attackBoxOffset.x) * GetFacingDirection(),
            attackBoxOffset.y
        );
    }

    private Vector2 GetPogoCenter()
    {
        return (Vector2)transform.position + new Vector2(
            Mathf.Abs(pogoAttackBoxOffset.x) * GetFacingDirection(),
            pogoAttackBoxOffset.y
        );
    }

    private float GetFacingDirection()
    {
        if (movementVector.x != 0f)
            return Mathf.Sign(movementVector.x);

        float facing = Mathf.Sign(transform.localScale.x);
        return facing == 0f ? 1f : facing;
    }

    private bool TryDealDamage(GameObject targetObject)
    {
        foreach (MonoBehaviour component in targetObject.GetComponents<MonoBehaviour>())
        {
            if (component == null)
                continue;

            MethodInfo takeDamageMethod = component.GetType().GetMethod(
                "TakeDamage",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(int) },
                null
            );

            if (takeDamageMethod == null)
                continue;

            takeDamageMethod.Invoke(component, new object[] { attackDamage });
            return true;
        }


        return false;
    }

    private void ApplyPushback(Rigidbody2D hitRigidbody, Vector2 attackCenter)
    {
        Vector2 direction = hitRigidbody.worldCenterOfMass - attackCenter;
        float horizontalDirection = Mathf.Sign(direction.x);

        if (horizontalDirection == 0f)
        {
            horizontalDirection = GetFacingDirection();
        }

        Vector2 impulse = new Vector2(
            horizontalDirection * attackPushbackImpulse.x,
            attackPushbackImpulse.y
        );
        Debug.Log($"Applying pushback to {hitRigidbody.gameObject.name} with impulse {impulse}");

        hitRigidbody.AddForce(impulse, ForceMode2D.Impulse);
        //otherRb.AddForce(direction * pushForce, ForceMode2D.Impulse);
    }


    private void OnDrawGizmosSelected()
    {
        if (groundCheckTransform != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(groundCheckTransform.position, groundCheckRadius);
        }

        if (isAttackHitActive)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(GetEditorAttackCenter(), attackBoxSize);
        }
        Gizmos.color = Color.orange;
        Gizmos.DrawWireCube(GetEditorAttackCenter(), attackBoxSize);
        Gizmos.color = Color.white;
        Gizmos.DrawWireCube(GetEditorPogoAttackCenter(), pogoAttackBoxSize);

    }

    private Vector2 GetEditorAttackCenter()
    {
        float facing = transform.localScale.x < 0f ? -1f : 1f;

        return (Vector2)transform.position + new Vector2(
            Mathf.Abs(attackBoxOffset.x) * facing,
            attackBoxOffset.y
        );
    }

    private Vector2 GetEditorPogoAttackCenter()
    {
        float facing = transform.localScale.x < 0f ? -1f : 1f;

        return (Vector2)transform.position + new Vector2(
            Mathf.Abs(pogoAttackBoxOffset.x) * facing,
            pogoAttackBoxOffset.y
        );
    }

    private void LateUpdate()
    {
        SyncPogoSlashEffectVisibility();
    }

    private void SyncPogoSlashEffectVisibility()
    {
        if (pogoSlashEffect != null)
        {
            bool shouldBeVisible = isPogoing;;
            if (pogoSlashEffect.activeSelf != shouldBeVisible)
            {
                pogoSlashEffect.SetActive(shouldBeVisible);
            }
        }
    }
}
