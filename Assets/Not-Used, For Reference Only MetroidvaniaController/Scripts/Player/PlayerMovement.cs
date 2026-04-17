using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour {

	public CharacterController2D controller;
	public Animator animator;

	public float runSpeed = 40f;

	float horizontalMove = 0f;
	bool jump = false;
	bool dash = false;

	//bool dashAxis = false;
	
	// Update is called once per frame
	void Update () {

		// Get input from keyboard or controller (D-Pad / Left Stick for movement)
		float horizontalInput = Input.GetAxisRaw("Horizontal");
		
		// Controller support for left stick and D-Pad
		if (Mathf.Abs(Input.GetAxis("Horizontal")) > 0)
		{
			horizontalInput = Input.GetAxisRaw("Horizontal");
		}
		
		horizontalMove = horizontalInput * runSpeed;

		animator.SetFloat("Speed", Mathf.Abs(horizontalMove));

		// Jump: Z key or South Button / A Button (Gamepad)
		if (Input.GetKeyDown(KeyCode.Z) || Input.GetButtonDown("Jump"))
		{
			jump = true;
		}

		// Dash: C key or East Button / B Button (Gamepad)
		if (Input.GetKeyDown(KeyCode.C) || Input.GetButtonDown("Submit"))
		{
			dash = true;
		}

		/*if (Input.GetAxisRaw("Dash") == 1 || Input.GetAxisRaw("Dash") == -1) //RT in Unity 2017 = -1, RT in Unity 2019 = 1
		{
			if (dashAxis == false)
			{
				dashAxis = true;
				dash = true;
			}
		}
		else
		{
			dashAxis = false;
		}
		*/

	}

	public void OnFall()
	{
		animator.SetBool("IsJumping", true);
	}

	public void OnLanding()
	{
		animator.SetBool("IsJumping", false);
	}

	void FixedUpdate ()
	{
		// Move our character
		controller.Move(horizontalMove * Time.fixedDeltaTime, jump, dash);
		jump = false;
		dash = false;
	}
}
