using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine.UIElements;


// from this forum post, just curious about this coyote jump stuff and acme anvils.
// https://forum.unity.com/threads/coyote-time-variable-height-jump-with-touch-controls.1192519/
//
// NOTE: you need to make and set what you consider "Ground layer"
//
// NOTE: you should probably turn down Default Contact Offset in Physics2D setup
//

public partial class Hero : MonoBehaviour
{
    [Tooltip("Lateral walk speed.")]
    public float LateralSpeed = 5.0f;
    [Tooltip("Lateral walk acceleration.")]
    public float LateralAcceleration = 20.0f;

    [Tooltip("How much vertical speed to add for each jump.")]
    public float JumpVerticalSpeed = 6.0f;

    [Tooltip("How long after stepping off a ledge can you still jump?")]
    public float OffLedgeStillJumpTime = 0.25f;

    [Tooltip("How many jumps can you do in total after being grounded?")]
    public int TotalJumpCount = 2;

    [Tooltip("How early before ground contact can you say jump?")]
    public float PreLandingJumpTime = 0.15f;

    [Tooltip("Set this to ONLY the layers you want for ground.")]
    public LayerMask GroundMask;
    public Transform groundCheck;
    public float groundRadius = .2f;

    [Header("Dash")]
    public float dashSpeed = 12f;
    public float dashDuration = 0.15f;
    public float dashCooldown = 0.5f;

    bool isDashing;
    float dashTimer;
    float dashCooldownTimer;
    float facingDirection = 1f;



    bool airDashAvailable;
    float dashDirection = 1f;

    [Tooltip("Delay before jumps refill after landing.")]
    public float jumpRefillCooldown = 0.1f;
    //public GameObject RightSlash;
    //public GameObject LeftSlash;

    //public GameObject RightDash;
    //public GameObject LeftDash;
    /// <summary>
    /// public GameObject IdlePose;
    /// </summary>
    //public float attackOffsetX = 0.5f; // tweak this in Inspector

    [SerializeField] private Animator animator;

    [Tooltip("The circular collider to enable during attacks.")]
    public CircleCollider2D attackCollider;



    // [Header("Onscreen debugging:")]
    // public GameObject MarkerGroundedActual;
    // public GameObject MarkerGroundedCoyote;
    // public Text TextJumpsAvailable;




    Rigidbody2D rb2d;

    float GroundedTimer;

    bool CombinedIsGrounded
    {
        get
        {
            return GroundedTimer > 0;
        }
    }

    int jumpCounter;

    // Input System actions
    private InputAction moveAction;
    private InputAction jumpAction;
    private InputAction dashAction;
    private InputAction attackAction;
    public UIDocument uiDocument;
    private Label UICountersText;
    void Awake()
    {
        moveAction = new InputAction("Move", InputActionType.Value);

        // Keyboard
        moveAction.AddCompositeBinding("1DAxis")
            .With("negative", "<Keyboard>/leftArrow")
            .With("positive", "<Keyboard>/rightArrow");

        moveAction.AddCompositeBinding("1DAxis")
            .With("negative", "<Keyboard>/a")
            .With("positive", "<Keyboard>/d");

        // Gamepad
        moveAction.AddBinding("<Gamepad>/leftStick/x");
        moveAction.AddCompositeBinding("1DAxis")
            .With("negative", "<Gamepad>/dpad/left")
            .With("positive", "<Gamepad>/dpad/right");

        jumpAction = new InputAction("Jump", InputActionType.Button);
        jumpAction.AddBinding("<Keyboard>/space");
        jumpAction.AddBinding("<Gamepad>/buttonSouth");


        dashAction = new InputAction("Dash", InputActionType.Button);
        dashAction.AddBinding("<Gamepad>/rightShoulder"); // RB / R1

        attackAction = new InputAction("Attack", InputActionType.Button);
        attackAction.AddBinding("<Gamepad>/buttonEast");


    }

    void OnEnable()
    {
        moveAction.Enable();
        jumpAction.Enable();
        dashAction.Enable();
        attackAction.Enable();
    }

    void OnDisable()
    {
        moveAction.Disable();
        jumpAction.Disable();
        dashAction.Disable();
        attackAction.Disable();
    }

    void OnDestroy()
    {
        moveAction.Dispose();
        jumpAction.Dispose();
        dashAction.Dispose();
        attackAction.Dispose();
    }

    void Start()
    {
        rb2d = GetComponent<Rigidbody2D>();
        rb2d.freezeRotation = true;

        // if (RightSlash != null && RightSlash.GetComponent<SlashPushback>() == null)
        //     RightSlash.AddComponent<SlashPushback>();
        // if (LeftSlash != null && LeftSlash.GetComponent<SlashPushback>() == null)
        //     LeftSlash.AddComponent<SlashPushback>();

        jumpCounter = TotalJumpCount; // use them up initially
        airDashAvailable = true;
        Debug.Log(uiDocument.name);
        //Debug.Log(UICountersText);
        if (uiDocument != null)
        {
            UICountersText = uiDocument.rootVisualElement.Q<Label>("Counters");
        }
        // Log the found element
        // if (UICountersText == null)
        // {
        //     Debug.LogError("UICountersText not found. Check UI Document setup.");
        // }
    }

    bool leftIntent, rightIntent;

    bool jumpIntent;              // actual intent
    float preJumpIntent;          // timer
    bool CombinedJumpIntent
    {
        get
        {
            return jumpIntent || (preJumpIntent > 0);
        }
    }

    void UpdateAdjustTimers()
    {
        if (GroundedTimer > 0)
        {
            GroundedTimer -= Time.deltaTime;
            if (GroundedTimer <= 0)
            {
                jumpCounter = 0;
                //if (rb2d.linearVelocity.y == 0)
                //animator.SetBool("isJumping", false);
            }
        }

        if (preJumpIntent > 0)
        {
            preJumpIntent -= Time.deltaTime;

            if (preJumpIntent <= 0)
            {

            }
        }
        if (dashCooldownTimer > 0)
            dashCooldownTimer -= Time.deltaTime;

        if (isDashing)
        {
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0f)
            {
                isDashing = false;
                animator.SetBool("isDashing", false);
            }
        }

    }

    void UpdateGatherInputs()
    {
        // continuous intents
        leftIntent = false;
        rightIntent = false;

        float moveValue = moveAction.ReadValue<float>();

        if (moveValue < -0.1f) leftIntent = true;
        if (moveValue > 0.1f) rightIntent = true;

        // eventful intents
        if (jumpAction.WasPressedThisFrame())
        {
            jumpIntent = true;
            preJumpIntent = PreLandingJumpTime;
            Debug.Log("Jump intent! preJumpIntent timer set to " + preJumpIntent);
        }

        if (dashAction.WasPressedThisFrame() && airDashAvailable && !isDashing)
        {
            isDashing = true;
            animator.SetBool("isDashing", true);
            dashTimer = dashDuration;
            airDashAvailable = false;

            if (rightIntent) dashDirection = 1f;
            else if (leftIntent) dashDirection = -1f;
            else dashDirection = transform.localScale.x >= 0 ? 1f : -1f;
        }
        if (rightIntent) facingDirection = 1f;
        if (leftIntent) facingDirection = -1f;


    }

    bool PrevActualGrounded;
    bool ActualGrounded;


    void DoGroundChecks()
    {
        ActualGrounded = Physics2D.OverlapCircle(groundCheck.position, groundRadius, GroundMask) != null;

        if (ActualGrounded && !PrevActualGrounded)
        {


            airDashAvailable = true;
        }

        if (ActualGrounded)
        {
            GroundedTimer = OffLedgeStillJumpTime;

        }

        PrevActualGrounded = ActualGrounded;
    }

    void ProcessJumping()
    {
        //Debug.Log("Can jump! Grounded: " + ActualGrounded + " JumpCounter: " + jumpCounter);
        {
            if (ActualGrounded || (jumpCounter < TotalJumpCount - 1))
                if (CombinedJumpIntent)
                {
                    if (ActualGrounded || (jumpCounter < TotalJumpCount - 1))
                    {
                        preJumpIntent = 0;
                        GroundedTimer = 0;
                        ActualGrounded = false;   // important
                        PrevActualGrounded = false;


                        animator.SetBool("isJumping", true);
                        jumpCounter++;
                        Debug.Log(jumpCounter);

                        var vel = rb2d.linearVelocity;
                        if (vel.y < 0) vel.y = 0;
                        vel.y += JumpVerticalSpeed;
                        rb2d.linearVelocity = vel;


                        Debug.Log("Jumping! JumpCounter: " + jumpCounter);
                    }
                }
        }

        jumpIntent = false;
    }

    void ProcessLeftRightMovement()
    {
        var vel = rb2d.linearVelocity;

        if (isDashing)
        {
            // Dash in facing / input direction
            float dir = rightIntent ? 1 : (leftIntent ? -1 : Mathf.Sign(vel.x));
            if (dir == 0) dir = 1; // default forward

            vel.x = dir * dashSpeed;
            rb2d.linearVelocity = vel;
            return;
        }

        float desiredXMovement = 0;

        if (leftIntent) desiredXMovement = -1;
        if (rightIntent) desiredXMovement = 1;

        var desiredXSpeed = desiredXMovement * LateralSpeed;

        vel.x = Mathf.MoveTowards(vel.x, desiredXSpeed, LateralAcceleration * Time.deltaTime);

        rb2d.linearVelocity = vel;
    }

    void Update()
    {
        UpdateAdjustTimers();
        UpdateGatherInputs();
        bool isAttacking = attackAction.IsPressed();
        animator.SetBool("isAttacking", isAttacking);
        if (attackCollider != null)
        {
            attackCollider.enabled = isAttacking;
        }
        // if (facingDirection == 1)
        // {
        //     if (RightSlash != null || LeftSlash != null)
        //     {
        //         RightSlash.SetActive(isAttacking);
        //         LeftSlash.SetActive(false);
        //     }
        // }
        // if (RightSlash != null || LeftSlash != null)
        // {
        //     if (facingDirection == -1)
        //     {
        //         LeftSlash.SetActive(isAttacking);
        //         RightSlash.SetActive(false);
        //     }
        // }
        // bool isDashing = dashAction.IsPressed();
        // if (facingDirection == 1)
        // {
        //     RightDash.SetActive(isDashing);
        //     LeftDash.SetActive(false);
        // }
        // if (facingDirection == -1)
        // {
        //     LeftDash.SetActive(isDashing);
        //     RightDash.SetActive(false);
        // }
        //RightSlash.flipX = true;
        //IdlePose.SetActive(!isAttacking && !isDashing);
        // Flip slash based on facing direction

        // if (IdlePose != null)
        // {

        //     Vector3 idlescale = IdlePose.transform.localScale;
        //     idlescale.x = Mathf.Abs(idlescale.x) * facingDirection * -1;//to reverse left right
        //     IdlePose.transform.localScale = idlescale;
        // }
        Vector3 currentScale = transform.localScale;
        currentScale.x = Mathf.Abs(currentScale.x) * facingDirection;// * -1;
        transform.localScale = currentScale;

        if (UICountersText != null)
        {
            UICountersText.text = "Coins: " + PlayerData.Coins;
            //Debug.Log("UICountersText updated: " + UICountersText.text);
        }





    }

    void FixedUpdate()
    {
        //rb2d.rotation = 0f;
        DoGroundChecks();
        ProcessJumping();
        ProcessLeftRightMovement();
        if (rb2d.linearVelocity.y == 0)
            animator.SetBool("isJumping", false);

    }

    void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(groundCheck.position, groundRadius);
    }
}