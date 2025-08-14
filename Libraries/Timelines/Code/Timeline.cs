using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox;

namespace Timeline;

/// <summary>
/// Represents a single event on the timeline
/// </summary>
[System.Serializable]
public class TimelineEvent
{
	/// <summary>
	/// Time position on the timeline (0.0 to 1.0)
	/// </summary>
	[Property]
	public float Time { get; set; } = 0.0f;

	/// <summary>
	/// Type of event (e.g., "Sound", "Effect", "Animation")
	/// </summary>
	[Property]
	public string EventType { get; set; } = "Event";

	/// <summary>
	/// Optional data payload for the event
	/// </summary>
	[Property]
	public string EventData { get; set; } = "";

	public TimelineEvent()
	{
	}

	public TimelineEvent( float time, string eventType = "Event", string eventData = "" )
	{
		Time = time;
		EventType = eventType;
		EventData = eventData;
	}

	public override string ToString()
	{
		return $"{EventType} @ {Time:F2}";
	}
}

/// <summary>
/// Container for timeline events
/// </summary>
[System.Serializable]
public class EventTracks
{
	/// <summary>
	/// All events on the timeline, sorted by time
	/// </summary>
	[Property]
	public List<TimelineEvent> Events { get; set; } = new List<TimelineEvent>();

	/// <summary>
	/// Duration of the timeline in seconds (for conversion from normalized time)
	/// </summary>
	[Property]
	public float Duration { get; set; } = 10.0f;

	public EventTracks()
	{
		Events = new List<TimelineEvent>();
	}

	public EventTracks( params TimelineEvent[] events )
	{
		Events = new List<TimelineEvent>( events );
		SortEvents();
	}

	/// <summary>
	/// Add an event to the timeline
	/// </summary>
	public void AddEvent( TimelineEvent timelineEvent )
	{
		if ( timelineEvent == null )
			return;

		Events.Add( timelineEvent );
		SortEvents();
	}

	/// <summary>
	/// Add an event at a specific time
	/// </summary>
	public void AddEvent( float time, string eventType = "Event", string eventData = "" )
	{
		AddEvent( new TimelineEvent( time, eventType, eventData ) );
	}

	/// <summary>
	/// Remove an event from the timeline
	/// </summary>
	public bool RemoveEvent( TimelineEvent timelineEvent )
	{
		return Events.Remove( timelineEvent );
	}

	/// <summary>
	/// Remove all events of a specific type
	/// </summary>
	public int RemoveEventsOfType( string eventType )
	{
		return Events.RemoveAll( e => e.EventType == eventType );
	}

	/// <summary>
	/// Clear all events
	/// </summary>
	public void Clear()
	{
		Events.Clear();
	}

	/// <summary>
	/// Get events at a specific time (with tolerance)
	/// </summary>
	public IEnumerable<TimelineEvent> GetEventsAtTime( float time, float tolerance = 0.001f )
	{
		return Events.Where( e => Math.Abs( e.Time - time ) <= tolerance );
	}

	/// <summary>
	/// Get events within a time range
	/// </summary>
	public IEnumerable<TimelineEvent> GetEventsInRange( float startTime, float endTime )
	{
		return Events.Where( e => e.Time >= startTime && e.Time <= endTime );
	}

	/// <summary>
	/// Get events of a specific type
	/// </summary>
	public IEnumerable<TimelineEvent> GetEventsOfType( string eventType )
	{
		return Events.Where( e => e.EventType == eventType );
	}

	/// <summary>
	/// Get the next event after a given time
	/// </summary>
	public TimelineEvent GetNextEvent( float time )
	{
		return Events.FirstOrDefault( e => e.Time > time );
	}

	/// <summary>
	/// Get the previous event before a given time
	/// </summary>
	public TimelineEvent GetPreviousEvent( float time )
	{
		return Events.LastOrDefault( e => e.Time < time );
	}

	/// <summary>
	/// Convert normalized time (0-1) to absolute time in seconds
	/// </summary>
	public float NormalizedToAbsolute( float normalizedTime )
	{
		return normalizedTime * Duration;
	}

	/// <summary>
	/// Convert absolute time in seconds to normalized time (0-1)
	/// </summary>
	public float AbsoluteToNormalized( float absoluteTime )
	{
		return Duration > 0 ? absoluteTime / Duration : 0;
	}

	/// <summary>
	/// Sort events by time
	/// </summary>
	private void SortEvents()
	{
		Events.Sort( ( a, b ) => a.Time.CompareTo( b.Time ) );
	}

	/// <summary>
	/// Get all unique event types
	/// </summary>
	public IEnumerable<string> GetEventTypes()
	{
		return Events.Select( e => e.EventType ).Distinct();
	}

	/// <summary>
	/// Get event count
	/// </summary>
	public int Count => Events.Count;

	public override string ToString()
	{
		return $"EventTracks ({Count} events)";
	}
}

/// <summary>
/// Main timeline component that can be added to GameObjects
/// </summary>
[System.Serializable]
public class Timeline : Component
{
	/// <summary>
	/// The event tracks for this timeline
	/// </summary>
	[Property]
	public EventTracks EventTracks { get; set; } = new EventTracks();

	/// <summary>
	/// Whether the timeline should play automatically on start
	/// </summary>
	[Property]
	public bool AutoPlay { get; set; } = false;

	/// <summary>
	/// Whether the timeline should loop
	/// </summary>
	[Property]
	public bool Loop { get; set; } = false;

	/// <summary>
	/// Duration of the timeline in seconds
	/// </summary>
	[Property]
	public float Duration
	{
		get => EventTracks?.Duration ?? 10.0f;
		set
		{
			if ( EventTracks != null )
				EventTracks.Duration = value;
		}
	}

	/// <summary>
	/// Current playback time (0 to Duration)
	/// </summary>
	public float CurrentTime { get; private set; }

	/// <summary>
	/// Whether the timeline is currently playing
	/// </summary>
	public bool IsPlaying { get; private set; }

	/// <summary>
	/// Event fired when a timeline event is triggered
	/// </summary>
	public static event Action<GameObject, TimelineEvent> OnEventTriggered;

	private float _lastTime = -1;
	private List<TimelineEvent> _triggeredEvents = new List<TimelineEvent>();

	protected override void OnAwake()
	{
		base.OnAwake();
		
		if ( EventTracks == null )
			EventTracks = new EventTracks();

		if ( AutoPlay )
			Play();
	}

	protected override void OnUpdate()
	{
		if ( !IsPlaying )
			return;

		CurrentTime += Time.Delta;

		// Check for events to trigger
		CheckForEvents();

		// Handle looping or stopping
		if ( CurrentTime >= Duration )
		{
			if ( Loop )
			{
				CurrentTime = 0;
				_triggeredEvents.Clear();
			}
			else
			{
				Stop();
			}
		}
	}

	/// <summary>
	/// Start playing the timeline
	/// </summary>
	public void Play()
	{
		IsPlaying = true;
		_triggeredEvents.Clear();
	}

	/// <summary>
	/// Pause the timeline
	/// </summary>
	public void Pause()
	{
		IsPlaying = false;
	}

	/// <summary>
	/// Stop the timeline and reset to beginning
	/// </summary>
	public void Stop()
	{
		IsPlaying = false;
		CurrentTime = 0;
		_triggeredEvents.Clear();
	}

	/// <summary>
	/// Seek to a specific time
	/// </summary>
	public void Seek( float time )
	{
		CurrentTime = Math.Clamp( time, 0, Duration );
		_triggeredEvents.Clear();

		// Re-trigger any events that should have happened by now
		var normalizedTime = CurrentTime / Duration;
		var eventsToTrigger = EventTracks.GetEventsInRange( 0, normalizedTime );
		
		foreach ( var evt in eventsToTrigger )
		{
			TriggerEvent( evt );
		}
	}

	private void CheckForEvents()
	{
		var normalizedTime = CurrentTime / Duration;
		var normalizedLastTime = _lastTime / Duration;

		var eventsToTrigger = EventTracks.Events
			.Where( e => e.Time > normalizedLastTime && e.Time <= normalizedTime )
			.Where( e => !_triggeredEvents.Contains( e ) );

		foreach ( var evt in eventsToTrigger )
		{
			TriggerEvent( evt );
		}

		_lastTime = CurrentTime;
	}

	private void TriggerEvent( TimelineEvent evt )
	{
		if ( _triggeredEvents.Contains( evt ) )
			return;

		_triggeredEvents.Add( evt );
		OnEventTriggered?.Invoke( GameObject, evt );

		Log.Info( $"Timeline Event: {evt}" );
	}
}
