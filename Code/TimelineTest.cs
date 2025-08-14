using System;
using Timeline;

namespace Sandbox;

public class ShakingCubeTimelineTest : TimelineEventDispatcher
{
	[RequireComponent] private Timeline.Timeline Timeline { get; set; }
	protected override void RegisterEvents()
	{
		BindEvent( "jump", OnJump );
		BindFloatCurve( "yaw",RotateYaw );
		BindFloatCurve( "roll",RotateRoll );
		BindFloatCurve( "pitch",RotatePitch );
	}

	private Angles CubeRotation = Angles.Zero;

	protected override void OnStart()
	{
		Timeline.Play();
	}

	protected override void OnUpdate()
	{
		WorldRotation = CubeRotation;
	}
	
	public void RotateYaw(float rotation)
	{
		var ee = CubeRotation;
		ee.yaw = rotation;
		CubeRotation = ee;
	}
	public void RotateRoll(float rotation)
	{
		var ee = CubeRotation;
		ee.roll = rotation;
		CubeRotation = ee;
	}

	public void RotatePitch( float rotation )
	{
		var ee = CubeRotation;
		ee.pitch = rotation;
		CubeRotation = ee;
	}
	

	public void OnJump()
	{
		WorldPosition += new Vector3( 0, 0, 20 );
	}
	
}
