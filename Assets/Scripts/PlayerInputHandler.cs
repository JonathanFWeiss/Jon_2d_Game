using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour
{


    private JonCharacterController characterController;

    private InputAction moveAction, jumpAction, dashAction, attackAction, jumpcutAction, PogoAction, UpSlashAction, inputAction, SpellAction;


    void Awake()
    {
        characterController = GetComponent<JonCharacterController>();

        moveAction = InputSystem.actions.FindAction("Move");
        jumpAction = InputSystem.actions.FindAction("Jump");
        dashAction = InputSystem.actions.FindAction("Dash");
        attackAction = InputSystem.actions.FindAction("Attack");
        jumpcutAction = InputSystem.actions.FindAction("JumpCut");
        PogoAction = InputSystem.actions.FindAction("Pogo");
        UpSlashAction = InputSystem.actions.FindAction("UpSlash");
        SpellAction = InputSystem.actions.FindAction("Spell");

        jumpAction.performed += Jump;
        dashAction.performed += Dash;
        dashAction.canceled += DashCanceled;
        attackAction.performed += Attack;
        jumpcutAction.performed += JumpCut;
        PogoAction.performed += Pogo;
        UpSlashAction.performed += UpSlash;
        SpellAction.performed += Spell;

    }

    // Update is called once per frame
    void Update()
    {
        Vector2 moveInput = moveAction.ReadValue<Vector2>();
        //Debug.Log("Move input from input handler: " + moveInput);
        characterController.Move(moveInput);
        characterController.SetDashHeld(dashAction.IsPressed());


    }

    private void OnDisable()
    {
        if (characterController != null)
        {
            characterController.SetDashHeld(false);
        }
    }



    private void Jump(InputAction.CallbackContext context)
    {
        Debug.Log("Jump action triggered from input system");
        characterController.Jump();
    }

    public void Move()
    {
        Vector2 moveInput = moveAction.ReadValue<Vector2>();
        characterController.Move(moveInput);
    }

    public void Dash(InputAction.CallbackContext context)
    {
        if (dashAction.triggered)
        {
            Debug.Log("Dash action triggered from input system");
        }
        characterController.SetDashHeld(true);
        characterController.Dash();
    }

    public void DashCanceled(InputAction.CallbackContext context)
    {
        characterController.SetDashHeld(false);
    }

    public void Attack(InputAction.CallbackContext context)
    {
        if (attackAction.triggered)
        {
            Debug.Log("Attack action triggered from input system");
        }
        characterController.Attack();
    }

    public void JumpCut(InputAction.CallbackContext context)
    {
        if (jumpcutAction.triggered)
        {
            Debug.Log("JumpCut action triggered from input system");
        }
        characterController.JumpCut();
    }

    public void Pogo(InputAction.CallbackContext context)
    {
        if (PogoAction.triggered)
        {
            Debug.Log("Pogo action triggered from input system");
        }
        characterController.Pogo();
    }

    public void UpSlash(InputAction.CallbackContext context)
    {
        if (UpSlashAction.triggered)
        {
            Debug.Log("UpSlash action triggered from input system");
        }
        characterController.UpSlash();
    }

    public void Spell(InputAction.CallbackContext context)
    {
        if (SpellAction.triggered)
        {
            Debug.Log("Spell action triggered from input system");
        }
        characterController.Spell();
    }

}
