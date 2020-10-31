﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class mouseLook : MonoBehaviour
{
	public float mouseSensitivity = 100f;
	public float lookSensitivity = 100f;
	public Transform playerBody;
	float xRotation = 0f;
	// Start is called before the first frame update
	void Start()
	{
		Cursor.lockState = CursorLockMode.Locked;
		Cursor.visible = false;
	}

	// Update is called once per frame
	void Update()
	{
		float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
		float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;
		float lookX = Input.GetAxis("Keyboard Turn X") * lookSensitivity * Time.deltaTime;
		float lookY = Input.GetAxis("Keyboard Turn Y") * lookSensitivity * Time.deltaTime;

		mouseX += lookX;
		mouseY += lookY;

		xRotation -= mouseY;
		xRotation = Mathf.Clamp(xRotation, -90f, 90f);

		transform.localRotation = Quaternion.Euler(xRotation, 0, 0);
		playerBody.Rotate(Vector3.up * mouseX);
	}
}
