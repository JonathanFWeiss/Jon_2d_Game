using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour
{


    private JonCharacterController characterController;

    private InputAction moveAction, jumpAction, dashAction, attackAction, jumpcutAction;


    void Awake()
    {
        characterController = GetComponent<JonCharacterController>();

        moveAction = InputSystem.actions.FindAction("Move");
        jumpAction = InputSystem.actions.FindAction("Jump");
        dashAction = InputSystem.actions.FindAction("Dash");
        attackAction = InputSystem.actions.FindAction("Attack");
        jumpcutAction = InputSystem.actions.FindAction("JumpCut");

        jumpAction.performed += Jump;
        dashAction.performed += Dash;
        attackAction.performed += Attack;
        jumpcutAction.performed += JumpCut;

    }

    // Update is called once per frame
    void Update()
    {
        Vector2 moveInput = moveAction.ReadValue<Vector2>();
        Debug.Log("Move input from input handler: " + moveInput);
        characterController.Move(moveInput);


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
        characterController.Dash();
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


}
