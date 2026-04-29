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
    [SerializeField] private float dashHeldSpeedMultiplier = 2f;
    [SerializeField][Range(0f, 1f)] private float dashHeldInputThreshold = 0.5f;
    [SerializeField] private float dashHeldGroundedCarryTime = 0.25f;
    [SerializeField] private float jumpCutMultiplier = .5f;
    [SerializeField] private float maxFallSpeed = 20f;
    [SerializeField] private float coyoteTime = 0.3f;
    [SerializeField] private float jumpBufferTime = 0.15f;
    private bool doubleJumpAvailable = true; // Tracks if the player can still double jump
    private bool airDashAvailable = true; // Tracks if the player can still air dash
    [SerializeField] private bool canDash = true; // For if the player can air dash or not, set in inspector
    [SerializeField] private bool canDoubleJump = true;//for if the player can double jump or not, set in inspector
    [SerializeField] private bool canWallJump = true;//for if the player can wall jump or not, set in inspector
    private Rigidbody2D rb;
    public bool isGrounded { get; private set; }
    private float canBeGroundedTime = 0f;
    //private float delayBeforeGrounded = 0.2f;

    [Header("Melee Attack")]
    [SerializeField] private Vector2 attackBoxSize = new Vector2(1.25f, 0.85f);
    [SerializeField] private Vector2 attackBoxOffset = new Vector2(0.9f, 0f);

    [SerializeField] private LayerMask attackLayerMask;
    [SerializeField] private int attackDamage = 1;
    [SerializeField] private int energyGainPerSuccessfulHit = 1;
    [SerializeField] private Vector2 attackPushbackImpulse = new Vector2(4f, 1.5f);
    [SerializeField] private float attackDuration = 1f;
    [SerializeField] private float attackHitDelay = 0f;
    [SerializeField] private GameObject attackSlashEffect;

    [Header("Attack Audio")]
    [SerializeField] private AudioClip[] attackVoiceClips;
    [SerializeField] private AudioSource attackVoiceAudioSource;

    [Header("Pogo Parameters")]
    //[SerializeField] private float pogoForce = 10f;
    [SerializeField] private float pogoDuration = 1.5f;
    //[SerializeField] private float pogoAttackDelay = 0.5f;
    [SerializeField] private Vector2 pogoAttackBoxSize = new Vector2(1.25f, 0.85f);
    [SerializeField] private Vector2 pogoAttackBoxOffset = new Vector2(0f, -2f);
    [SerializeField] private float pogoBounceCooldown = 0.2f;
    [SerializeField] private GameObject pogoSlashEffect;
    private float nextPogoBounceTime;

    [Header("UpSlash Parameters")]
    [SerializeField] private float UpSlashDuration = .5f;
    [SerializeField] private Vector2 upSlashBoxSize = new Vector2(1.25f, 0.85f);
    [SerializeField] private Vector2 upSlashBoxOffset = new Vector2(0f, 2f);
    //[SerializeField] private float upSlashCooldown = 0.5f;
    [SerializeField] private GameObject upSlashEffect;

    [Header("Hit State")]
    [SerializeField] private float gettingHitDuration = 1f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheckTransform;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer;
    [Header("Wall Jump")]
    [SerializeField] private float wallCheckDistance = 0.1f;
    [SerializeField][Range(0.25f, 1f)] private float wallCheckHeightMultiplier = 0.8f;
    [SerializeField] private float wallContactGraceTime = 0.15f;
    [SerializeField] private Vector2 wallJumpImpulse = new Vector2(6f, 5f);
    [SerializeField] private float wallJumpMovementLockTime = 0.15f;
    [SerializeField] private float sameWallJumpLockTime = 0.2f;
    [SerializeField] private float wallSlideMaxFallSpeed = 3f;
    [Header("Ledge Grab")]
    [SerializeField] private bool canLedgeGrab = true;
    [SerializeField] private LayerMask ledgeGrabLayer;
    [SerializeField] private bool ledgeGrabRequiresForwardInput = true;
    [SerializeField][Range(0f, 1f)] private float ledgeGrabInputThreshold = 0.2f;
    [SerializeField] private float ledgeGrabForwardDistance = 0.45f;
    [SerializeField][Range(0.1f, 0.9f)] private float ledgeGrabWallRayHeight = 0.55f;
    [SerializeField][Range(0.5f, 1f)] private float ledgeGrabClearRayHeight = 0.9f;
    [SerializeField] private float ledgeTopProbeHeight = 0.4f;
    [SerializeField] private float ledgeTopRayDistance = 1.1f;
    [SerializeField] private float ledgeTopProbeInset = 0.08f;
    [SerializeField] private float ledgeHangHorizontalOffset = 0.05f;
    [SerializeField] private float ledgeHangVerticalOffset = 0.12f;
    [SerializeField] private float ledgePullUpForwardOffset = 0.12f;
    [SerializeField] private float ledgePullUpGroundClearance = 0.03f;
    [SerializeField] private float ledgePullUpDuration = 0.16f;
    [SerializeField] private float LedgeClingMinumHangTime = 0.3f;
    [SerializeField] private float ledgeRegrabCooldown = 0.18f;
    [SerializeField] private float ledgeGrabMaxUpwardSpeed = 0.75f;
    [SerializeField][Range(0.1f, 1f)] private float ledgeTopNormalMinY = 0.65f;
    [SerializeField] private float ledgeClearanceSkin = 0.04f;
    private bool jumpRequested, dashRequested, attackRequested, isDashing, jumpcutRequested, pogoRequested, upSlashRequested;
    private bool isDashButtonHeld;
    private bool isDashSpeedBoostActive;
    private bool isDashSpeedBoostArmed;
    private float dashDirection = 1f;
    private float dashSpeedBoostDirection = 1f;
    private bool dashSpeedBoostWentAirborne;
    private float dashSpeedBoostGroundedSince = float.NegativeInfinity;
    private float nextDashTime;
    private float jumpBufferExpireTime = float.NegativeInfinity;
    private float lastGroundedTime = float.NegativeInfinity;
    private float defaultGravityScale = 3f;
    private Vector3 lastGroundedPosition;
    private bool hasLastGroundedPosition;
    private Vector3 localScale;
    private Collider2D bodyCollider;
    private bool isTouchingWallLeft;
    private bool isTouchingWallRight;
    private bool isWallSliding;
    private float lastWallContactTime = float.NegativeInfinity;
    private int lastWallContactDirection;
    private int lastWallJumpedFromDirection;
    private float sameWallJumpLockUntil = float.NegativeInfinity;
    private float wallJumpMovementLockUntil = float.NegativeInfinity;
    private bool isLedgeGrabbing;
    private bool isLedgePullingUp;
    private int ledgeGrabDirection;
    private Vector2 ledgeHangPosition;
    private Vector2 ledgeClimbTargetPosition;
    private float ledgeGrabStartedTime = float.NegativeInfinity;
    private float ledgeGrabDisabledUntil = float.NegativeInfinity;
    private Coroutine dashCoroutine;
    private Coroutine ledgePullUpCoroutine;


    private Vector2 movementVector;
    private bool isAttacking;
    private bool isPogoing;
    private bool isJumping = false;
    private bool isAttackHitActive;
    public bool isGettingHit { get; private set; }
    private bool isUpSlashing;
    private Coroutine gettingHitCoroutine;

    public Animator animator;
    private bool isSpellCasting;
    private bool SpellCastRequested;

    [Header("Spell")]
    [SerializeField] private int spellEnergyCost = 5;
    [SerializeField] private AudioClip insufficientSpellEnergyClip;
    public GameObject SpellPrefab;

    [Header("Swimming Parameters")]
    [SerializeField] private float swimSpeed = 5f;
    [SerializeField] private float buoyancy = 6f;
    [SerializeField] private float waterDrag = 3f;
    public bool isSwimming;
    private float normalDrag;


    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        animator = GetComponent<Animator>();
        attackVoiceAudioSource = attackVoiceAudioSource != null ? attackVoiceAudioSource : GetComponent<AudioSource>();

        //        Debug.Log("Rigidbody2D component found: " + rb);
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
        animator.SetBool("isUpSlashing", isUpSlashing);
        animator.SetBool("isLedgeGrabbing", isLedgeGrabbing);
        animator.SetBool("isLedgePullingUp", isLedgePullingUp);
        animator.SetBool("isSwimming", isSwimming);
        animator.SetBool("isSpellCasting", isSpellCasting);

    }





    void FixedUpdate()
    {
        doGroundCheck();
        doWallCheck();

        if (isLedgePullingUp)
        {
            return;
        }

        if (isLedgeGrabbing)
        {
            if (UpdateLedgeGrabState())
            {
                return;
            }
        }
        else if (TryStartLedgeGrab())
        {
            return;
        }


        if (pogoRequested && !isGettingHit)
        {

            pogoRequested = false;
            attackRequested = false; // Cancel attack if pogo is performed
            upSlashRequested = false; // Cancel upslash if pogo is performed
            SpellCastRequested = false; // Cancel spell cast if pogo is performed
            StartCoroutine(PogoAttackCoroutine(pogoDuration));
            Debug.Log("Pogo executed with jump force: " + jumpForce);
        }
        if (upSlashRequested && !isGettingHit)
        {
            upSlashRequested = false;
            attackRequested = false; // Cancel attack if upslash is performed
            SpellCastRequested = false; // Cancel spell cast if upslash is performed
            StartCoroutine(UpSlashCoroutine(
                UpSlashDuration));

        }
        if (attackRequested && !isGettingHit)
        {
            StartCoroutine(AttackCoroutine(attackDuration));
            attackRequested = false;
            SpellCastRequested = false;
        }
        if (isGrounded)
        {
            doubleJumpAvailable = canDoubleJump;
            airDashAvailable = canDash;
        }
        if (SpellCastRequested && !isGettingHit)
        {
            SpellCastRequested = false;

            StartCoroutine(SpellCastCoroutine());//Probably change to a coroutine if there will be a casting time or animation
        }

        bool wallJumpMovementLocked = Time.time < wallJumpMovementLockUntil;

        UpdateDashSpeedBoost();

        // Don't apply normal movement during dash or attack
        if (!isDashing && !isAttacking && !isGettingHit && !wallJumpMovementLocked && !isSwimming)
        {
            rb.linearVelocityX = movementVector.x * GetCurrentMovementSpeed();
        }

        if (isSwimming)
        {
            rb.linearVelocity = new Vector2(movementVector.x * swimSpeed, movementVector.y * swimSpeed);
        }
        //Debug.Log("Current velocity: " + rb.linearVelocityX);

        bool hasBufferedJumpRequest = HasBufferedJumpRequest();
        if (!hasBufferedJumpRequest)
        {
            ClearBufferedJump();
        }

        bool hasGroundJumpAvailable = HasGroundJumpAvailable();
        bool hasWallJumpAvailable = HasWallJumpAvailable();

        if (hasBufferedJumpRequest && !isDashing && hasWallJumpAvailable)
        {
            PerformWallJump();
            ClearBufferedJump();
            Debug.Log("Wall jump performed");
        }
        else if (hasBufferedJumpRequest && !isDashing && !isGettingHit && (hasGroundJumpAvailable || doubleJumpAvailable || isSwimming))
        {
            bool usingGroundJump = hasGroundJumpAvailable;

            if (rb.linearVelocity.y != 0)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0);
            }

            rb.AddForce(new Vector2(0, jumpForce), ForceMode2D.Impulse);

            canBeGroundedTime = Time.time + coyoteTime;
            ClearBufferedJump();
            isJumping = true;
            lastGroundedTime = float.NegativeInfinity;

            if (!usingGroundJump && doubleJumpAvailable)
            {
                doubleJumpAvailable = false;
                Debug.Log("Double jump used");
            }


        }

        if (isWallSliding && rb.linearVelocity.y < -wallSlideMaxFallSpeed)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, -wallSlideMaxFallSpeed);
        }
        else if (rb.linearVelocity.y < -maxFallSpeed)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, -maxFallSpeed);
        }

        if (jumpcutRequested && rb.linearVelocity.y > 0)
        {

            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * jumpCutMultiplier);
            jumpcutRequested = false;
//            Debug.Log("Jump cut applied");
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
            dashCoroutine = StartCoroutine(DashCoroutine(dashDuration));
            dashRequested = false;
        }

        if (!isDashing && !isAttacking && !isGettingHit && Time.time >= wallJumpMovementLockUntil)
        {
            if (Mathf.Abs(movementVector.x) > 0.1f)
            {
                localScale = new Vector3(Mathf.Sign(movementVector.x) * Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
                transform.localScale = localScale;
            }
        }
        if (rb.gravityScale < defaultGravityScale && !isDashing)
        {
            rb.gravityScale = defaultGravityScale;

        }
    }

    public void Jump()
    {
        BufferJumpRequest();
        Debug.Log("Jump input buffered");
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

    public void SetDashHeld(bool isHeld)
    {
        if (isDashButtonHeld == isHeld)
        {
            return;
        }

        isDashButtonHeld = isHeld;

        if (isDashButtonHeld)
        {
            isDashSpeedBoostArmed = true;
        }
        else
        {
            isDashSpeedBoostArmed = false;
            StopDashSpeedBoost();
        }
    }

    private void StartDashSpeedBoost()
    {
        dashSpeedBoostDirection = GetDashBoostDirection();
        dashSpeedBoostWentAirborne = false;
        dashSpeedBoostGroundedSince = isGrounded
            ? Time.time
            : float.NegativeInfinity;
        isDashSpeedBoostArmed = false;
        isDashSpeedBoostActive = true;
    }

    private void StopDashSpeedBoost()
    {
        isDashSpeedBoostActive = false;
        dashSpeedBoostWentAirborne = false;
        dashSpeedBoostGroundedSince = float.NegativeInfinity;
    }

    private void CancelDashSpeedBoost()
    {
        isDashSpeedBoostArmed = false;
        StopDashSpeedBoost();
    }

    private void UpdateDashSpeedBoost()
    {
        if (!isDashButtonHeld)
        {
            StopDashSpeedBoost();
            return;
        }

        if (isSwimming)
        {
            CancelDashSpeedBoost();
            return;
        }

        if (isDashSpeedBoostActive)
        {
            if (ShouldStopDashSpeedBoostFromInput())
            {
                StopDashSpeedBoost();
                return;
            }

            //UpdateDashSpeedBoostGroundedCarry();
        }

        if (!isDashSpeedBoostActive && CanStartDashSpeedBoost())
        {
            StartDashSpeedBoost();
        }
    }

    private bool ShouldStopDashSpeedBoostFromInput()
    {
        float directionalInput = movementVector.x * dashSpeedBoostDirection;
        return directionalInput < dashHeldInputThreshold;
    }

    // private void UpdateDashSpeedBoostGroundedCarry()
    // {
    //     if (!isGrounded)
    //     {
    //         dashSpeedBoostWentAirborne = true;
    //         dashSpeedBoostGroundedSince = float.NegativeInfinity;
    //         return;
    //     }

    //     if (!dashSpeedBoostWentAirborne)
    //     {
    //         dashSpeedBoostGroundedSince = Time.time;
    //         return;
    //     }

    //     if (float.IsNegativeInfinity(dashSpeedBoostGroundedSince))
    //     {
    //         dashSpeedBoostGroundedSince = Time.time;
    //     }

    //     if (Time.time - dashSpeedBoostGroundedSince > dashHeldGroundedCarryTime)
    //     {
    //         StopDashSpeedBoost();
    //     }
    // }

    private bool CanStartDashSpeedBoost()
    {
        return isGrounded &&
            isDashSpeedBoostArmed &&
            !isSwimming &&
            Mathf.Abs(movementVector.x) >= dashHeldInputThreshold;
    }

    private float GetCurrentMovementSpeed()
    {
        return isDashSpeedBoostActive
            ? movementSpeed * dashHeldSpeedMultiplier
            : movementSpeed;
    }

    private float GetDashBoostDirection()
    {
        if (Mathf.Abs(movementVector.x) >= dashHeldInputThreshold)
        {
            return Mathf.Sign(movementVector.x);
        }

        float facing = Mathf.Sign(transform.localScale.x);
        return facing == 0f ? 1f : facing;
    }

    IEnumerator DashCoroutine(float seconds)
    {
//        Debug.Log("Starting dash coroutine");
        if (isDashing)
        {
            dashCoroutine = null;
            yield break;
        }
        isDashing = true;
        float prevGravity = rb.gravityScale;
        rb.gravityScale = 0; // Disable gravity
        rb.linearVelocity = new Vector2(dashDirection * dashSpeed, 0); // Apply force
        yield return new WaitForSeconds(seconds); // Wait for dash duration
        rb.gravityScale = prevGravity; // Restore gravity
        isDashing = false;
        dashCoroutine = null;
    }

    public void Attack()
    {
        if (isAttacking || attackRequested || isPogoing || isUpSlashing || isLedgeGrabbing || isLedgePullingUp)
            return;

        attackRequested = true;

        Debug.Log("Attack action triggered");

    }

    public void Pogo()
    {
        if (isAttacking || pogoRequested || isPogoing || isUpSlashing || isLedgeGrabbing || isLedgePullingUp)
            return;
        if (isGrounded)
            return;

        // Implement pogo logic here, for example:
        pogoRequested = true; // Bounce up with jump force
        Debug.Log("Pogo action triggered");
    }

    public void UpSlash()
    {
        if (isAttacking || upSlashRequested || isPogoing || isUpSlashing || isLedgeGrabbing || isLedgePullingUp)
            return;

        upSlashRequested = true;
        Debug.Log("UpSlash action triggered");
    }

    public void Spell()
    {
        if (isAttacking || isPogoing || isUpSlashing || isSpellCasting || SpellCastRequested || isLedgeGrabbing || isLedgePullingUp)
            return;

        if (PlayerData.Energy < spellEnergyCost)
        {
            PlayInsufficientSpellEnergyVoice();
            Debug.Log("Not enough energy to cast spell.");
            return;
        }

        SpellCastRequested = true;
        Debug.Log("Spell action triggered");
    }

    public IEnumerator SpellCastCoroutine()
    {
        if (isSpellCasting)
            yield break;

        isSpellCasting = true;
        Debug.Log("Casting spell...");
        yield return new WaitForSeconds(0.5f); // start the spell 
        isSpellCasting = false;
        GameObject spell = Instantiate(SpellPrefab, rb.position, Quaternion.identity);
        PlayerData.RestoreFullHP(); //do the effect now
        PlayerData.RemoveEnergy(spellEnergyCost);
        yield return new WaitForSeconds(3f); // visual after effect
        Destroy(spell);
        
        
    }

    public void JumpCut()
    {
        jumpcutRequested = true;
//        Debug.Log("JumpCut action triggered");
    }

    public void StartGettingHit()
    {
        CancelDashSpeedBoost();
        CancelLedgeGrabAndPullUp(true);

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

        CancelDash();
        CancelLedgeGrabAndPullUp(false);

        transform.position = teleportTarget;
        rb.gravityScale = defaultGravityScale;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        lastGroundedTime = Time.time;
        doubleJumpAvailable = canDoubleJump;
        airDashAvailable = canDash;

        ClearBufferedJump();
        dashRequested = false;
        attackRequested = false;
        jumpcutRequested = false;
        pogoRequested = false;
        SetDashHeld(false);
        isDashing = false;
        isAttacking = false;
        isPogoing = false;
        isWallSliding = false;
        isLedgeGrabbing = false;
        isLedgePullingUp = false;
        ledgeGrabDirection = 0;
        ledgeHangPosition = Vector2.zero;
        ledgeClimbTargetPosition = Vector2.zero;
        ledgeGrabDisabledUntil = Time.time + ledgeRegrabCooldown;
        isTouchingWallLeft = false;
        isTouchingWallRight = false;
        lastWallContactTime = float.NegativeInfinity;
        lastWallContactDirection = 0;
        lastWallJumpedFromDirection = 0;
        sameWallJumpLockUntil = float.NegativeInfinity;
        wallJumpMovementLockUntil = float.NegativeInfinity;
    }

    private void doGroundCheck()
    {
        if (Time.time < canBeGroundedTime)
        {
            isGrounded = false;
            return;
        }
        Collider2D groundCollider = Physics2D.OverlapCircle(groundCheckTransform.position, groundCheckRadius, groundLayer);
        //Debug.Log("Ground check result: " + (groundCollider != null ? "Grounded" : "Not Grounded"));
        if (groundCollider)
        {
            isGrounded = true;
            isJumping = false;
            isWallSliding = false;
            lastGroundedTime = Time.time;
            lastGroundedPosition = transform.position;
            hasLastGroundedPosition = true;
            lastWallJumpedFromDirection = 0;
            sameWallJumpLockUntil = float.NegativeInfinity;
        }
        else isGrounded = false;
    }

    private void doWallCheck()
    {
        isTouchingWallLeft = false;
        isTouchingWallRight = false;
        isWallSliding = false;

        if (GetBodyCollider() == null || isGrounded)
        {
            return;
        }

        Vector2 wallCheckSize = GetWallCheckSize();
        if (wallCheckSize == Vector2.zero)
        {
            return;
        }

        isTouchingWallLeft = Physics2D.OverlapBox(GetWallCheckCenter(Vector2.left), wallCheckSize, 0f, groundLayer) != null;
        isTouchingWallRight = Physics2D.OverlapBox(GetWallCheckCenter(Vector2.right), wallCheckSize, 0f, groundLayer) != null;

        int currentWallDirection = 0;
        if (isTouchingWallLeft && !isTouchingWallRight)
        {
            currentWallDirection = -1;
        }
        else if (isTouchingWallRight && !isTouchingWallLeft)
        {
            currentWallDirection = 1;
        }
        else if (isTouchingWallLeft && isTouchingWallRight)
        {
            currentWallDirection = movementVector.x != 0f
                ? (int)Mathf.Sign(movementVector.x)
                : (int)GetFacingDirection();
        }

        if (currentWallDirection == 0)
        {
            return;
        }

        lastWallContactTime = Time.time;
        lastWallContactDirection = currentWallDirection;

        bool pressingIntoWall = currentWallDirection < 0
            ? movementVector.x < -0.01f
            : movementVector.x > 0.01f;

        isWallSliding = rb.linearVelocity.y < 0f && pressingIntoWall;
    }

    private bool TryStartLedgeGrab()
    {
        if (!TryFindLedgeGrab(out LedgeGrabInfo ledgeGrabInfo))
        {
            return false;
        }

        BeginLedgeGrab(ledgeGrabInfo);
        return true;
    }

    private bool UpdateLedgeGrabState()
    {
        if (isGrounded || isSwimming || isGettingHit)
        {
            EndLedgeGrab(false);
            return false;
        }

        CancelDashSpeedBoost();
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.position = ledgeHangPosition;
        transform.position = ledgeHangPosition;
        isWallSliding = false;

        bool wantsAway = movementVector.x * ledgeGrabDirection < -ledgeGrabInputThreshold;
        bool wantsDown = movementVector.y < -ledgeGrabInputThreshold;
        bool canPullUp = CanPullUpFromLedge();

        if (HasBufferedJumpRequest())
        {
            if (wantsAway)
            {
                ClearBufferedJump();
                PerformLedgeJumpAway();
                return false;
            }

            if (canPullUp)
            {
                ClearBufferedJump();
                StartLedgePullUp();
                return true;
            }
        }

        if (canPullUp && movementVector.y > ledgeGrabInputThreshold)
        {
            StartLedgePullUp();
            return true;
        }

        if (wantsDown || wantsAway || dashRequested || attackRequested || pogoRequested || upSlashRequested || SpellCastRequested)
        {
            EndLedgeGrab(true);
            return false;
        }

        return true;
    }

    private bool TryFindLedgeGrab(out LedgeGrabInfo ledgeGrabInfo)
    {
        ledgeGrabInfo = default(LedgeGrabInfo);

        Collider2D collider = GetBodyCollider();
        LayerMask ledgeMask = GetLedgeGrabMask();
        if (!canLedgeGrab ||
            Time.time < ledgeGrabDisabledUntil ||
            collider == null ||
            ledgeMask.value == 0 ||
            isGrounded ||
            isSwimming ||
            isGettingHit ||
            isLedgePullingUp ||
            isAttacking ||
            isPogoing ||
            isUpSlashing ||
            isSpellCasting ||
            attackRequested ||
            pogoRequested ||
            upSlashRequested ||
            SpellCastRequested ||
            rb.linearVelocity.y > ledgeGrabMaxUpwardSpeed ||
            movementVector.y < -ledgeGrabInputThreshold)
        {
            return false;
        }

        int direction = GetLedgeGrabCheckDirection();
        if (direction == 0)
        {
            return false;
        }

        Bounds bounds = collider.bounds;
        Vector2 rayDirection = Vector2.right * direction;
        Vector2 wallOrigin = new Vector2(
            bounds.center.x,
            bounds.min.y + bounds.size.y * ledgeGrabWallRayHeight
        );
        Vector2 clearOrigin = new Vector2(
            bounds.center.x,
            bounds.min.y + bounds.size.y * ledgeGrabClearRayHeight
        );

        RaycastHit2D wallHit = Physics2D.Raycast(wallOrigin, rayDirection, ledgeGrabForwardDistance, ledgeMask);
        if (wallHit.collider == null)
        {
            return false;
        }

        RaycastHit2D clearHit = Physics2D.Raycast(clearOrigin, rayDirection, ledgeGrabForwardDistance, ledgeMask);
        if (clearHit.collider != null)
        {
            return false;
        }

        float wallX = wallHit.point.x;
        Vector2 topOrigin = new Vector2(
            wallX + direction * ledgeTopProbeInset,
            bounds.max.y + ledgeTopProbeHeight
        );
        RaycastHit2D topHit = Physics2D.Raycast(topOrigin, Vector2.down, ledgeTopRayDistance, ledgeMask);
        if (topHit.collider == null || topHit.normal.y < ledgeTopNormalMinY)
        {
            return false;
        }

        float minimumLedgeTopY = bounds.min.y + bounds.size.y * ledgeGrabWallRayHeight;
        if (topHit.point.y < minimumLedgeTopY)
        {
            return false;
        }

        Vector2 colliderCenterOffset = (Vector2)bounds.center - (Vector2)transform.position;
        Vector2 hangCenter = new Vector2(
            wallX - direction * (bounds.extents.x + ledgeHangHorizontalOffset),
            topHit.point.y - ledgeHangVerticalOffset - bounds.extents.y
        );
        Vector2 climbCenter = new Vector2(
            wallX + direction * (bounds.extents.x + ledgePullUpForwardOffset),
            topHit.point.y + ledgePullUpGroundClearance + bounds.extents.y
        );

        Vector2 hangPosition = hangCenter - colliderCenterOffset;
        Vector2 climbPosition = climbCenter - colliderCenterOffset;
        if (!IsBodyClearAt(climbPosition, bounds, ledgeMask))
        {
            return false;
        }

        ledgeGrabInfo = new LedgeGrabInfo
        {
            Direction = direction,
            HangPosition = hangPosition,
            ClimbPosition = climbPosition
        };
        return true;
    }

    private void BeginLedgeGrab(LedgeGrabInfo ledgeGrabInfo)
    {
        CancelDash();
        CancelDashSpeedBoost();

        isLedgeGrabbing = true;
        isLedgePullingUp = false;
        isWallSliding = false;
        ledgeGrabDirection = ledgeGrabInfo.Direction;
        ledgeHangPosition = ledgeGrabInfo.HangPosition;
        ledgeClimbTargetPosition = ledgeGrabInfo.ClimbPosition;
        ledgeGrabStartedTime = Time.time;
        lastWallContactTime = Time.time;
        lastWallContactDirection = ledgeGrabDirection;
        doubleJumpAvailable = canDoubleJump;
        airDashAvailable = canDash;

        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.position = ledgeHangPosition;
        transform.position = ledgeHangPosition;

        localScale = new Vector3(ledgeGrabDirection * Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
        transform.localScale = localScale;
    }

    private void EndLedgeGrab(bool applyCooldown)
    {
        if (!isLedgeGrabbing)
        {
            return;
        }

        isLedgeGrabbing = false;
        isWallSliding = false;
        ledgeGrabStartedTime = float.NegativeInfinity;
        rb.gravityScale = defaultGravityScale;

        if (applyCooldown)
        {
            ledgeGrabDisabledUntil = Time.time + ledgeRegrabCooldown;
        }
    }

    private void StartLedgePullUp()
    {
        if (isLedgePullingUp)
        {
            return;
        }

        EndLedgeGrab(false);
        ledgePullUpCoroutine = StartCoroutine(LedgePullUpCoroutine(ledgeClimbTargetPosition, ledgePullUpDuration));
    }

    private bool CanPullUpFromLedge()
    {
        return Time.time >= ledgeGrabStartedTime + Mathf.Max(0f, LedgeClingMinumHangTime);
    }

    private IEnumerator LedgePullUpCoroutine(Vector2 targetPosition, float seconds)
    {
        isLedgePullingUp = true;
        CancelDash();
        CancelDashSpeedBoost();
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        Vector2 startPosition = rb.position;
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, seconds);

        while (elapsed < duration)
        {
            if (isGettingHit || isSwimming)
            {
                break;
            }

            float progress = Mathf.Clamp01(elapsed / duration);
            float smoothedProgress = progress * progress * (3f - 2f * progress);
            Vector2 nextPosition = Vector2.Lerp(startPosition, targetPosition, smoothedProgress);
            rb.position = nextPosition;
            transform.position = nextPosition;
            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        if (!isGettingHit && !isSwimming)
        {
            rb.position = targetPosition;
            transform.position = targetPosition;
            rb.linearVelocity = Vector2.zero;
            isGrounded = true;
            isJumping = false;
            lastGroundedTime = Time.time;
            lastGroundedPosition = transform.position;
            hasLastGroundedPosition = true;
            doubleJumpAvailable = canDoubleJump;
            airDashAvailable = canDash;
        }

        rb.gravityScale = defaultGravityScale;
        isLedgePullingUp = false;
        ledgePullUpCoroutine = null;
        ledgeGrabDisabledUntil = Time.time + ledgeRegrabCooldown;
    }

    private void PerformLedgeJumpAway()
    {
        int jumpDirection = ledgeGrabDirection;
        EndLedgeGrab(true);
        CancelDashSpeedBoost();

        Vector2 force = new Vector2(-jumpDirection * wallJumpImpulse.x, wallJumpImpulse.y);
        rb.gravityScale = defaultGravityScale;
        rb.linearVelocity = Vector2.zero;
        rb.AddForce(force, ForceMode2D.Impulse);

        isJumping = true;
        lastGroundedTime = float.NegativeInfinity;
        canBeGroundedTime = Time.time + coyoteTime;
        lastWallJumpedFromDirection = jumpDirection;
        sameWallJumpLockUntil = Time.time + sameWallJumpLockTime;
        wallJumpMovementLockUntil = Time.time + wallJumpMovementLockTime;
        doubleJumpAvailable = canDoubleJump;
        airDashAvailable = canDash;

        localScale = new Vector3(Mathf.Sign(force.x) * Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
        transform.localScale = localScale;
    }

    private void CancelLedgeGrabAndPullUp(bool restoreGravity)
    {
        if (ledgePullUpCoroutine != null)
        {
            StopCoroutine(ledgePullUpCoroutine);
            ledgePullUpCoroutine = null;
        }

        isLedgePullingUp = false;
        isLedgeGrabbing = false;
        ledgeGrabDirection = 0;
        ledgeGrabStartedTime = float.NegativeInfinity;
        isWallSliding = false;

        if (restoreGravity)
        {
            rb.gravityScale = defaultGravityScale;
        }
    }

    private void CancelDash()
    {
        if (dashCoroutine != null)
        {
            StopCoroutine(dashCoroutine);
            dashCoroutine = null;
        }

        isDashing = false;
    }

    private bool IsBodyClearAt(Vector2 targetPosition, Bounds currentBounds, LayerMask obstacleMask)
    {
        Vector2 colliderCenterOffset = (Vector2)currentBounds.center - (Vector2)transform.position;
        Vector2 targetCenter = targetPosition + colliderCenterOffset;
        float skin = Mathf.Max(0f, ledgeClearanceSkin);
        Vector2 checkSize = new Vector2(
            Mathf.Max(0.01f, currentBounds.size.x - skin * 2f),
            Mathf.Max(0.01f, currentBounds.size.y - skin * 2f)
        );

        return Physics2D.OverlapBox(targetCenter, checkSize, 0f, obstacleMask) == null;
    }

    private int GetLedgeGrabCheckDirection()
    {
        if (Mathf.Abs(movementVector.x) >= ledgeGrabInputThreshold)
        {
            return (int)Mathf.Sign(movementVector.x);
        }

        if (ledgeGrabRequiresForwardInput)
        {
            return 0;
        }

        return (int)GetFacingDirection();
    }

    private LayerMask GetLedgeGrabMask()
    {
        return ledgeGrabLayer.value != 0
            ? ledgeGrabLayer
            : groundLayer;
    }

    private struct LedgeGrabInfo
    {
        public int Direction;
        public Vector2 HangPosition;
        public Vector2 ClimbPosition;
    }

    private bool HasGroundJumpAvailable()
    {
        //return isGrounded || Time.time <= lastGroundedTime + coyoteTime;
        return isGrounded && Time.time > canBeGroundedTime || Time.time <= lastGroundedTime + coyoteTime && !isJumping;
        // This allows the player to still jump for a short time after leaving the ground, making the controls feel more responsive.
    }

    private void BufferJumpRequest()
    {
        jumpRequested = true;
        jumpBufferExpireTime = Time.time + jumpBufferTime;
    }

    private bool HasBufferedJumpRequest()
    {
        return jumpRequested && Time.time <= jumpBufferExpireTime;
    }

    private void ClearBufferedJump()
    {
        jumpRequested = false;
        jumpBufferExpireTime = float.NegativeInfinity;
    }

    private bool HasWallJumpAvailable()
    {
        if (!canWallJump)
        {
            return false;
        }
        //Debug.Log("Checking wall jump availability. isGrounded: " + isGrounded + ", Time since last wall contact: " + (Time.time - lastWallContactTime) + ", wallContactGraceTime: " + wallContactGraceTime + ", sameWallJumpLockUntil: " + sameWallJumpLockUntil + ", lastWallJumpedFromDirection: " + lastWallJumpedFromDirection);
        if (isGrounded || Time.time > lastWallContactTime + wallContactGraceTime)
        {
            return false;
        }

        int wallDirection = GetWallDirectionForJump();
        if (wallDirection == 0)
        {
            return false;
        }

        if (Time.time < sameWallJumpLockUntil && wallDirection == lastWallJumpedFromDirection)
        {
            return false;
        }

        return true;
    }

    private void PerformWallJump()
    {
        int wallDirection = GetWallDirectionForJump();
        if (wallDirection == 0)
        {
            return;
        }

        CancelDashSpeedBoost();

        Vector2 force = new Vector2(-wallDirection * wallJumpImpulse.x, wallJumpImpulse.y);

        jumpRequested = false;
        isJumping = true;
        isWallSliding = false;
        lastGroundedTime = float.NegativeInfinity;
        canBeGroundedTime = Time.time + coyoteTime;
        lastWallJumpedFromDirection = wallDirection;
        sameWallJumpLockUntil = Time.time + sameWallJumpLockTime;
        wallJumpMovementLockUntil = Time.time + wallJumpMovementLockTime;
        doubleJumpAvailable = canDoubleJump;
        airDashAvailable = canDash;

        if (Mathf.Sign(rb.linearVelocity.x) != Mathf.Sign(force.x))
        {
            force.x -= rb.linearVelocity.x;
        }

        if (rb.linearVelocity.y < 0f)
        {
            force.y -= rb.linearVelocity.y;
        }

        rb.AddForce(force, ForceMode2D.Impulse);

        localScale = new Vector3(Mathf.Sign(force.x) * Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
        transform.localScale = localScale;

        Debug.Log("Wall jump used");
    }

    private int GetWallDirectionForJump()
    {
        if (Time.time > lastWallContactTime + wallContactGraceTime)
        {
            return 0;
        }

        if (isTouchingWallLeft && !isTouchingWallRight)
        {
            return -1;
        }

        if (isTouchingWallRight && !isTouchingWallLeft)
        {
            return 1;
        }

        return lastWallContactDirection;
    }

    private Vector2 GetWallCheckSize()
    {
        Collider2D collider = GetBodyCollider();
        if (collider == null)
        {
            return Vector2.zero;
        }

        Bounds bounds = collider.bounds;
        float checkWidth = Mathf.Max(0.01f, wallCheckDistance);
        float checkHeight = Mathf.Max(0.1f, bounds.size.y * wallCheckHeightMultiplier);
        return new Vector2(checkWidth, checkHeight);
    }

    private Vector2 GetWallCheckCenter(Vector2 direction)
    {
        Collider2D collider = GetBodyCollider();
        if (collider == null)
        {
            return transform.position;
        }

        Bounds bounds = collider.bounds;
        float horizontalOffset = bounds.extents.x + (Mathf.Max(0.01f, wallCheckDistance) * 0.5f);
        return (Vector2)bounds.center + direction.normalized * horizontalOffset;
    }

    private Collider2D GetBodyCollider()
    {
        if (bodyCollider == null)
        {
            bodyCollider = GetComponent<Collider2D>();
        }

        return bodyCollider;
    }

    IEnumerator AttackCoroutine(float seconds)
    {
        Debug.Log("Starting attack coroutine");
        if (isAttacking) yield break;

        isAttacking = true;
        PlayRandomAttackVoice();
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

    private void PlayRandomAttackVoice()
    {
        if (attackVoiceClips == null || attackVoiceClips.Length == 0)
        {
            return;
        }

        AudioClip attackVoiceClip = attackVoiceClips[Random.Range(0, attackVoiceClips.Length)];
        if (attackVoiceClip == null)
        {
            return;
        }

        PlayVoiceClip(attackVoiceClip);
    }

    private void PlayInsufficientSpellEnergyVoice()
    {
        PlayVoiceClip(insufficientSpellEnergyClip);
    }

    private void PlayVoiceClip(AudioClip clip)
    {
        if (clip == null)
        {
            return;
        }

        AudioSource audioSource = GetAttackVoiceAudioSource();
        if (audioSource != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    private AudioSource GetAttackVoiceAudioSource()
    {
        if (attackVoiceAudioSource != null)
        {
            return attackVoiceAudioSource;
        }

        attackVoiceAudioSource = GetComponent<AudioSource>();
        if (attackVoiceAudioSource == null)
        {
            attackVoiceAudioSource = gameObject.AddComponent<AudioSource>();
            attackVoiceAudioSource.playOnAwake = false;
            attackVoiceAudioSource.spatialBlend = 0f;
        }

        return attackVoiceAudioSource;
    }


    IEnumerator PogoAttackCoroutine(float seconds)
    {
        Debug.Log("Starting pogo attack coroutine");
        if (isPogoing) yield break;


        isPogoing = true;
        PlayRandomAttackVoice();
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

    IEnumerator UpSlashCoroutine(float seconds)
    {
        Debug.Log("Starting up slash attack coroutine");
        if (isUpSlashing) yield break;


        isUpSlashing = true;
        PlayRandomAttackVoice();
        //rb.linearVelocityX = 0; // Stop horizontal movement during attack 


        isAttackHitActive = true;
        PerformUpSlashHit();
        yield return null;
        isAttackHitActive = false;

        float remainingUpSlashTime = Mathf.Max(0f, seconds);
        if (remainingUpSlashTime > 0f)
        {
            yield return new WaitForSeconds(remainingUpSlashTime);
        }

        isUpSlashing = false;
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
                {
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0); // Reset vertical velocity for consistent pogo bounces
                    rb.AddForce(new Vector2(0, 1.2f * jumpForce), ForceMode2D.Impulse);
                    nextPogoBounceTime = Time.time + pogoBounceCooldown;
                    Debug.Log($"Pogo bounce applied to {rb.gameObject.name} with jump force: {1.2 * jumpForce}");
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


    private void PerformUpSlashHit()
    {
        Vector2 attackCenter = GetUpSlashCenter();
        Collider2D[] hits = Physics2D.OverlapBoxAll(attackCenter, upSlashBoxSize, 0f, attackLayerMask);
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

    private Vector2 GetUpSlashCenter()
    {
        return (Vector2)transform.position + new Vector2(
            Mathf.Abs(upSlashBoxOffset.x) * GetFacingDirection(),
            upSlashBoxOffset.y
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

            bool trackedHealthBeforeDamage = TryGetTrackedHealthValue(component, out int healthBeforeDamage);
            takeDamageMethod.Invoke(component, new object[] { attackDamage });

            if (trackedHealthBeforeDamage &&
                (!TryGetTrackedHealthValue(component, out int healthAfterDamage) || healthAfterDamage >= healthBeforeDamage))
            {
                return false;
            }

            PlayerData.AddEnergy(energyGainPerSuccessfulHit);
            Debug.Log($"Energy gained. Total energy: {PlayerData.Energy}");
            return true;
        }


        return false;
    }

    private static bool TryGetTrackedHealthValue(MonoBehaviour component, out int healthValue)
    {
        string[] trackedHealthMemberNames = { "hp", "HP", "health", "Health" };
        System.Type componentType = component.GetType();

        foreach (string memberName in trackedHealthMemberNames)
        {
            FieldInfo healthField = componentType.GetField(
                memberName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            );

            if (healthField?.FieldType == typeof(int))
            {
                healthValue = (int)healthField.GetValue(component);
                return true;
            }

            PropertyInfo healthProperty = componentType.GetProperty(
                memberName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            );

            if (healthProperty?.PropertyType == typeof(int) &&
                healthProperty.CanRead &&
                healthProperty.GetIndexParameters().Length == 0)
            {
                healthValue = (int)healthProperty.GetValue(component);
                return true;
            }
        }

        healthValue = 0;
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


    private void OnDrawGizmos()
    {

    }

    private void OnDrawGizmosSelected()
    {
        DrawWallCheckGizmos();
        DrawLedgeGrabGizmos();
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
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(GetUpSlashCenter(), upSlashBoxSize);

    }

    private void DrawWallCheckGizmos()
    {
        Vector2 wallCheckSize = GetWallCheckSize();
        if (wallCheckSize == Vector2.zero)
        {
            return;
        }

        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(GetWallCheckCenter(Vector2.left), wallCheckSize);
        Gizmos.DrawWireCube(GetWallCheckCenter(Vector2.right), wallCheckSize);
    }

    private void DrawLedgeGrabGizmos()
    {
        Collider2D collider = GetBodyCollider();
        if (collider == null)
        {
            return;
        }

        Bounds bounds = collider.bounds;
        int direction = Mathf.Abs(movementVector.x) >= ledgeGrabInputThreshold
            ? (int)Mathf.Sign(movementVector.x)
            : (transform.localScale.x < 0f ? -1 : 1);
        Vector3 rayDirection = Vector3.right * direction;
        Vector3 wallOrigin = new Vector3(
            bounds.center.x,
            bounds.min.y + bounds.size.y * ledgeGrabWallRayHeight,
            transform.position.z
        );
        Vector3 clearOrigin = new Vector3(
            bounds.center.x,
            bounds.min.y + bounds.size.y * ledgeGrabClearRayHeight,
            transform.position.z
        );
        Vector3 topOrigin = new Vector3(
            bounds.center.x + direction * (bounds.extents.x + ledgeGrabForwardDistance + ledgeTopProbeInset),
            bounds.max.y + ledgeTopProbeHeight,
            transform.position.z
        );

        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(wallOrigin, wallOrigin + rayDirection * ledgeGrabForwardDistance);
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(clearOrigin, clearOrigin + rayDirection * ledgeGrabForwardDistance);
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(topOrigin, topOrigin + Vector3.down * ledgeTopRayDistance);

        if (isLedgeGrabbing)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(ledgeHangPosition, 0.08f);
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(ledgeClimbTargetPosition, 0.08f);
        }
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
        SyncSlashEffectVisibility();
    }

    public void ResetGravity()
    {
        rb.gravityScale = defaultGravityScale;
    }

    private void SyncSlashEffectVisibility()
    {
        if (pogoSlashEffect != null)
        {
            bool pogoShouldBeVisible = isPogoing; ;
            if (pogoSlashEffect.activeSelf != pogoShouldBeVisible)
            {
                pogoSlashEffect.SetActive(pogoShouldBeVisible);
            }
        }
        if (upSlashEffect != null)
        {
            bool upSlashShouldBeVisible = isUpSlashing; ;
            if (upSlashEffect.activeSelf != upSlashShouldBeVisible)
            {
                upSlashEffect.SetActive(upSlashShouldBeVisible);
            }
        }
        if (attackSlashEffect != null)
        {
            bool AttackSlashShouldBeVisible = isAttacking; ;
            if (attackSlashEffect.activeSelf != AttackSlashShouldBeVisible)
            {
                attackSlashEffect.SetActive(AttackSlashShouldBeVisible);
            }
        }
    }
}
