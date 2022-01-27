﻿using System;
using System.Collections;
using System.Collections.Generic;
using log4net;
using UnityEngine;
using static Utilities;

// This script controls the telescope according to the inputs from
// the control room as received by the MCUCommand updated by SimServer.
public class TelescopeControllerSim : MonoBehaviour
{
	// log4net logger.
	private static readonly ILog Log = LogManager.GetLogger(typeof(TelescopeControllerSim));
	
	// The game objects that get rotated by a movement command.
	public GameObject azimuth;
	public GameObject elevation;
	
	// The command that determines the telescope's movement.
	public MCUCommand command;
	
	// The object that updates the UI with the state of variables.
	public UIHandler ui;
	
	// The current values of the azimuth and elevation in degrees.
	private float simTelescopeAzimuthDegrees;
	private float simTelescopeElevationDegrees;
	
	// The current azimuth and elevation speeds in degrees per second.
	private float azSpeed = 0.0f;
	private float elSpeed = 0.0f;
	
	// Whether the azimuth or elevation motors are moving,
	// the direction of travel (true = positive, false = negative),
	// and the acceleration direction.
	// Always check the moving bool before checking the motion or accelerating
	// bools.
	private bool azimuthMoving = false;
	private bool azimuthPosMotion = false;
	private bool azimuthAccelerating = false;
	private bool azimuthDecelerating = false;
	private bool azimuthHomed = false;
	
	private bool elevationMoving = false;
	private bool elevationPosMotion = false;
	private bool elevationAccelerating = false;
	private bool elevationDecelerating = false;
	private bool elevationHomed = false;
	
	private bool executingRelativeMove = false;
	
	// If the angle and target are within this distance, consider them equal.
	private float epsilon = 0.001f;
	
	// The max and min allowed angles for the elevation, expressed as the 
	// actual angle plus 15 degrees to convert the actual angle to the 
	// angle range that Unity uses.
	private float maxEl = 92.0f + 15.0f;
	private float minEl = -8.0f + 15.0f;
	
	/// <summary>
	/// Start is called before the first frame.
	/// </summary>
	void Start()
	{
		Log.Debug("NEW SIMULATION INSTANCE");
		Log.Debug("Initializing simulator.");
		
		// Set the current azimuth and elevation degrees to the rotation
		// of the game objects.
		simTelescopeAzimuthDegrees = azimuth.transform.eulerAngles.y;
		simTelescopeElevationDegrees = elevation.transform.eulerAngles.z;
		
		// Initialize the MCUCommand.
		command.InitSim();
	}
	
	/// <summary>
	/// Update is called once per frame.
	/// </summary>
	void Update()
	{
		if(command.ignoreCommand)
			return;
		
		// Determine what the current command is.
		HandleCommand();
		
		// Update the azimuth and elevation positions.
		UpdateAzimuth();
		UpdateElevation();
		
		// Determine if any errors have occurred.
		HandleErrors();
		
		// Update any non-error output.
		HandleOutput();
	}
	
	/// <summary>
	/// Update is called when the scene ends.
	/// </summary>
	void OnDestroy()
	{
		Log.Debug("END SIMULATION INSTANCE\n\n\n");
	}
	
	/// <summary>
	/// Determine what the current command is and update necessary variables.
	/// </summary>
	public void HandleCommand() 
	{
		if(command.jog) 
			HandleJog();
		
		// Update the UI with the input azimuth and elevation.
		ui.InputAzimuth(command.azimuthData);
		ui.InputElevation(command.elevationData);
	}
	
	/// <summary>
	/// Determine if any errors have occurred and update the necessary boolean values
	/// so that the SimServer can set the correct error bits.
	/// </summary>
	public void HandleErrors()
	{
		command.invalidInput = LimitSwitchHit();
		if(command.invalidInput)
			Log.Warn("A limit switch has been hit.");
	}
	
	/// <summary>
	/// Determine if any special output needs tracked and update the necessary boolean
	/// values so that the SimServer can set the correct error bits.
	/// </summary>
	public void HandleOutput()
	{
		// If the current command is a relative move, record that so that the
		// move complete bit can be set.
		if(command.relativeMove)
			executingRelativeMove = true;
		// If the current command is not a relative move or a stop command, then
		// the move complete bit shouldn't be set.
		else if(!command.stop)
			executingRelativeMove = false;
		
		// If the current command is a home command and the axis has stopped moving,
		// then this axis is homed.
		if(command.home && !AzimuthMoving())
		{
			command.invalidAzimuthPosition = false;
			azimuthHomed = true;
		}
		// If the current command is not a home command and it moves this axis, 
		// then this axis is not homed.
		else if(!command.home && AzimuthMoving())
			azimuthHomed = false;
		
		if(command.home && !ElevationMoving())
		{
			command.invalidElevationPosition = false;
			elevationHomed = true;
		}
		else if(!command.home && ElevationMoving())
			elevationHomed = false;
		
	}
	
	/// <summary>
	/// Return true if the telescope elevation has hit a limit switch. This is true if the
	/// current elevation is beyond a limit value or at a limit value and it has a target
	/// to go even further beyond that.
	/// </summary>
	public bool LimitSwitchHit()
	{
		float current = simTelescopeElevationDegrees;
		float target = current + command.elevationData;
		return (current > maxEl || (current == maxEl && target > maxEl))
			|| (current < minEl || (current == minEl && target < minEl));
	}
	
	/// <summary>
	/// Return true if a relative move was received.
	/// </summary>
	public bool RelativeMove()
	{
		return executingRelativeMove;
	}
	
	/// <summary>
	/// Return the current azimuth angle.
	/// </summary>
	public float Azimuth()
	{
		return simTelescopeAzimuthDegrees;
	}
	
	/// <summary>
	/// Return true if the azimuth motor is moving.
	/// </summary>
	public bool AzimuthMoving()
	{
		return azimuthMoving;
	}
	
	/// <summary>
	/// Return true if the azimuth motor is moving in the positive direction
	/// and false if it is moving in the negative direction.
	/// Always check AzimuthMoving before checking this function.
	/// </summary>
	public bool AzimuthPosMotion()
	{
		return azimuthPosMotion;
	}
	
	/// <summary>
	/// Return true if the azimuth motor has positive acceleration and
	/// false if it has negative acceleration (i.e. it is decelerating).
	/// Always check AzimuthMoving before checking this function.
	/// </summary>
	public bool AzimuthAccelerating()
	{
		return azimuthAccelerating;
	}
	
	/// <summary>
	/// Return true if the azimuth motor has negative acceleration (i.e. deceleration).
	/// Always check AzimuthMoving before checking this function.
	/// </summary>
	public bool AzimuthDecelerating()
	{
		return azimuthDecelerating;
	}
	
	/// <summary>
	/// Return true if the azimuth orientation is at the homed position.
	/// </summary>
	public bool AzimuthHomed()
	{
		return azimuthHomed;
	}
	
	/// <summary>
	/// Return the current elevation angle where negative values are below the horizon.
	/// </summary>
	public float Elevation()
	{
		return simTelescopeElevationDegrees;
	}
	
	/// <summary>
	/// Return true if the elevation motor is moving.
	/// </summary>
	public bool ElevationMoving()
	{
		return elevationMoving;
	}
	
	/// <summary>
	/// Return true if the elevation motor is moving in the positive direction
	/// and false if it is moving in the negative direction.
	/// Always check ElevationMoving before checking this function.
	/// </summary>
	public bool ElevationPosMotion()
	{
		return elevationPosMotion;
	}
	
	/// <summary>
	/// Return true if the elevation motor has positive acceleration.
	/// Always check ElevationMoving before checking this function.
	/// </summary>
	public bool ElevationAccelerating()
	{
		return elevationAccelerating;
	}
	
	/// <summary>
	/// Return true if the elevation motor has negative acceleration (i.e. deceleration).
	/// Always check ElevationMoving before checking this function.
	/// </summary>
	public bool ElevationDecelerating()
	{
		return elevationDecelerating;
	}
	
	/// <summary>
	/// Return true if the elevation orientation is at the homed position.
	/// </summary>
	public bool ElevationHomed()
	{
		return elevationHomed;
	}
	
	/// <summary>
	/// Return the angle of the azimuth object truncated to a single decimal place.
	/// For use in the UI.
	/// </summary>
	public double UnityAzimuth()
	{
		return System.Math.Round(azimuth.transform.eulerAngles.y, 1);
	}
	
	/// <summary>
	/// Return the angle of the elevation object truncated to a single decimal place.
	/// For use in the UI.
	/// </summary>
	public double UnityElevation()
	{
		return System.Math.Round(elevation.transform.eulerAngles.z, 1);
	}
	
	/// <summary>
	/// Return the current azimuth angle truncated to a single decimal place.
	/// For use in the UI.
	/// </summary>
	public double SimAzimuth()
	{
		return System.Math.Round(simTelescopeAzimuthDegrees, 1);
	}
	
	/// <summary>
	/// Return the current elevation angle truncated to a single decimal place.
	/// For use in the UI.
	/// </summary>
	public double SimElevation()
	{
		return System.Math.Round((simTelescopeElevationDegrees - 15.0f), 1);
	}
	
	/// <summary>
	/// Return the current azimuth target angle truncated to a single decimal place.
	/// For use in the UI.
	/// </summary>
	public double TargetAzimuth()
	{
		return System.Math.Round(simTelescopeAzimuthDegrees + command.azimuthData, 1);
	}
	
	/// <summary>
	/// Return the current elevation target angle truncated to a single decimal place.
	/// For use in the UI.
	/// </summary>
	public double TargetElevation()
	{
		return System.Math.Round(simTelescopeElevationDegrees + command.elevationData - 15.0f, 1);
	}
	
	/// <summary>
	/// Return the current azimuth speed truncated to a single decimal place.
	/// For use in the UI.
	/// </summary>
	public double AzimuthSpeed()
	{
		return System.Math.Round(azSpeed, 2);
	}
	
	/// <summary>
	/// Return the current elevation speed truncated to a single decimal place.
	/// For use in the UI.
	/// </summary>
	public double ElevationSpeed()
	{
		return System.Math.Round(elSpeed, 2);
	}
	
	/// <summary>
	/// Handle a jog command by setting the target orientation 1 degree ahead of the current orientation,
	/// relative to the direction of the jog. This causes the telescope to continually move in the direction
	/// of the jog, since HandleJog is called every frame during a jog.
	/// </summary>
	private void HandleJog()
	{
		float azJog = command.azJog ? 1.0f : 0.0f;
		float elJog = command.azJog ? 0.0f : 1.0f;
		float target = command.posJog ? 1.0f : -1.0f;
		
		command.azimuthData = target * azJog;
		command.elevationData = target * elJog;
	}
	
	/// <summary>
	/// Update the telescope azimuth.
	/// <summary>
	private void UpdateAzimuth()
	{
		ref float moveBy = ref command.azimuthData;
		
		// If the amount of azimuth degrees to move by is non-zero, the azimuth must move.
		if(moveBy != 0.0f || azSpeed != 0.0f)
			ShiftAzimuthSpeed(moveBy);
		
		if(azSpeed != 0.0f)
		{
			// Get the current orientation and movement speed
			ref float current = ref simTelescopeAzimuthDegrees;
			float old = current;
			
			// Move the azimuth.
			current = MoveAzimuth(current, azSpeed);
			
			// Update the MCUCommand by subtracting the angle moved from the remaining degrees to move by.
			moveBy -= AngleDistance(current, old);
			
			// If the total degrees remaining to move by is less than the epsilon, consider it on target.
			if(moveBy != 0.0f && WithinEpsilon(moveBy, epsilon))
			{
				Log.Debug("Threw out the remaining " + moveBy + " degree azimuth movement because it was smaller than the accepted epsilon value of " + epsilon + ".");
				moveBy = 0.0f;
			}
		}
		
		azimuthMoving = (azSpeed != 0.0f);
		azimuthPosMotion = (azSpeed > 0.0f);
	}
	
	/// <summary>
	/// Update the telescope elevation.
	/// <summary>
	private void UpdateElevation()
	{
		ref float moveBy = ref command.elevationData;
		
		// If the amount of elevation degrees to move by is non-zero, the elevation must move.
		if(moveBy != 0.0f || elSpeed != 0.0f)
			ShiftElevationSpeed(moveBy);
		
		if(elSpeed != 0.0f)
		{
			// Get the current orientation and movement speed
			ref float current = ref simTelescopeElevationDegrees;
			float old = current;
			
			// Move the elevation.
			current = MoveElevation(current, elSpeed);
			
			// Update the MCUCommand by subtracting the angle moved from the remaining degrees to move by.
			float moved = AngleDistance(current, old);
			moveBy -= moved;
			// If the elevation didn't move despite elSpeed being non-zero, that means we've hit
			// one of the limit switches and therefore should drop the speed to 0.
			if(moved == 0.0f && (
				(WithinEpsilon(AngleDistance(current, maxEl), epsilon) && elSpeed > 0.0f) ||
				(WithinEpsilon(AngleDistance(current, minEl), epsilon) && elSpeed < 0.0f)))
				elSpeed = 0.0f;
			
			// If the total degrees remaining to move by is less than the epsilon, consider it on target.
			if(moveBy != 0.0f && WithinEpsilon(moveBy, epsilon))
			{
				Log.Debug("Threw out the remaining " + moveBy + " degree elevation movement because it was smaller than the accepted epsilon value of " + epsilon + ".");
				moveBy = 0.0f;
			}
		}
		
		elevationMoving = (elSpeed != 0.0f);
		elevationPosMotion = (elSpeed > 0.0f);
	}
	
	/// <summary>
	/// Shift the current azimuth speed up or down according to acceleration and deceleration.
	/// </summary>
	/// <param name="remaining">The remaining number of degrees to move by for the current movement.</param>
	private void ShiftAzimuthSpeed(float remaining)
	{
		float sign = (remaining > 0.0f) ? 1.0f : -1.0f;
		remaining = Mathf.Abs(remaining);
		float progress = (remaining > 0.0f && command.cachedAzData != 0.0f) ? (1.0f - remaining / Mathf.Abs(command.cachedAzData)) : 1.0f;
		if(command.jog)
			progress = 0.0f;
		float maxSpeed = command.azimuthSpeed;
		float accel = command.azimuthAcceleration;
		float decel = command.azimuthDeceleration;
		if(command.stop)
		{
			progress = 1.0f;
			sign = (azSpeed > 0.0f) ? 1.0f : -1.0f;
			decel = 0.9f;
		}
		
		// Accelerate if we're in the first 50% of a movement.
		if(progress <= 0.5f)
		{
			azSpeed += sign * accel * Time.deltaTime;
			if(Mathf.Abs(azSpeed) >= Mathf.Abs(maxSpeed))
				azSpeed = sign * maxSpeed;
			
			azimuthAccelerating = (azSpeed != maxSpeed);
			azimuthDecelerating = false;
		}
		// Don't accelerate if we're in the last 50% of a movement.
		else if(progress > 0.5f)
		{
			// Decelerate if the remaining movement is smaller than the stopping distance.
			if(progress == 1.0f || StoppingDistance(azSpeed, decel) >= remaining)
			{
				azSpeed -= sign * decel * Time.deltaTime;
				if((sign == 1.0f && azSpeed <= 0.0f) ||
						(sign == -1.0f && azSpeed >= 0.0f))
					azSpeed = 0.0f;
				
				azimuthDecelerating = (azSpeed != 0.0f);
			}
			azimuthAccelerating = false;
		}
	}
	
	/// <summary>
	/// Shift the current elevation speed up or down according to acceleration and deceleration.
	/// </summary>
	/// <param name="remaining">The remaining number of degrees to move by for the current movement.</param>
	private void ShiftElevationSpeed(float remaining)
	{
		float sign = (remaining > 0.0f) ? 1.0f : -1.0f;
		remaining = Mathf.Abs(remaining);
		float progress = (remaining > 0.0f && command.cachedElData != 0.0f) ? (1.0f - remaining / Mathf.Abs(command.cachedElData)) : 1.0f;
		if(command.jog)
			progress = 0.0f;
		float maxSpeed = command.elevationSpeed;
		float accel = command.elevationAcceleration;
		float decel = command.elevationDeceleration;
		if(command.stop)
		{
			progress = 1.0f;
			sign = (elSpeed > 0.0f) ? 1.0f : -1.0f;
			decel = 0.9f;
		}
		
		// Accelerate if we're in the first 50% of a movement.
		if(progress <= 0.5f)
		{
			elSpeed += sign * accel * Time.deltaTime;
			if(Mathf.Abs(elSpeed) >= Mathf.Abs(maxSpeed))
				elSpeed = sign * maxSpeed;
			
			elevationAccelerating = (elSpeed != maxSpeed);
			elevationDecelerating = false;
		}
		// Don't accelerate if we're in the last 50% of a movement.
		else if(progress > 0.5f)
		{
			// Decelerate if the remaining movement is smaller than the stopping distance.
			if(progress == 1.0f || StoppingDistance(elSpeed, decel) >= remaining)
			{
				elSpeed -= sign * decel * Time.deltaTime;
				if((sign == 1.0f && elSpeed <= 0.0f) ||
						(sign == -1.0f && elSpeed >= 0.0f))
					elSpeed = 0.0f;
				
				elevationDecelerating = (elSpeed != 0.0f);
			}
			elevationAccelerating = false;
		}
	}
	
	/// <summary>
	/// Rotate the azimuth object.
	/// </summary>
	/// <param name="current">The current azimuth angle.</param>
	/// <param name="moveBy">The total degrees to move by before reaching the target azimuth.</param>
	/// <param name="speed">The speed at which to rotate in degrees per second.</param>
	/// <returns>The new azimuth angle.</returns>
	private float MoveAzimuth(float current, float speed)
	{
		// Alter the movement speed by the time since the last frame. This ensures
		// a smooth movement regardless of the framerate.
		speed *= Time.deltaTime;
		
		// Rotate the azimuth game object by the final speed.
		azimuth.transform.Rotate(0, speed, 0);
		
		// Return the new azimuth orientation, bounded within the range [0, 360).
		return BoundAzimuth(current + speed);
	}
	
	/// <summary>
	/// Rotate the elevation object.
	/// </summary>
	/// <param name="current">The current elevation angle.</param>
	/// <param name="moveBy">The total degrees to move by before reaching the target elevation.</param>
	/// <param name="speed">The speed at which to rotate in degrees per second.</param>
	/// <returns>The new elevation angle.</returns>
	private float MoveElevation(float current, float speed)
	{
		// Alter the movement speed by the time since the last frame. This ensures
		// a smooth movement regardless of the framerate.
		speed *= Time.deltaTime;
		
		// If we're closer to the target than the allowed bounds, lower the movement
		// speed so that we don't go out of bounds.
		float bounded = BoundElevation(elevation.transform.eulerAngles.z + speed, minEl, maxEl);
		if(bounded == minEl || bounded == maxEl)
			speed = bounded - current;
		
		// Rotate the elevation game object by the final speed.
		elevation.transform.Rotate(0, 0, speed);
		
		// Return the new elevation orientation, bounded within the range [minEl, maxEl].
		return BoundElevation(current + speed, minEl, maxEl);
	}
}
