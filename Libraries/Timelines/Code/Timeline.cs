using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox;

namespace Timeline;

/// <summary>
/// Simple timeline event - uses absolute time in seconds
/// </summary>
[System.Serializable]
public class TimelineEvent
{
    /// <summary>
    /// Time position in absolute seconds
    /// </summary>
    [Property]
    public float Time { get; set; } = 0.0f;
    
    public string EventId { get; set; }

    public TimelineEvent() { }

    public TimelineEvent(float time)
    {
        Time = time;
    }
}

/// <summary>
/// Container for timeline events - single track with an event ID and its own duration
/// </summary>
[System.Serializable]
public class EventTracks
{
    /// <summary>
    /// The event ID that will be triggered for all events on this track
    /// </summary>
    [Property]
    public string EventId { get; set; } = "event";

    /// <summary>
    /// All events on this track
    /// </summary>
    [Property]
    public List<TimelineEvent> Events { get; set; } = new List<TimelineEvent>();

    /// <summary>
    /// Duration of this specific track in seconds (independent of main timeline)
    /// </summary>
    [Property]
    public float Duration { get; set; } = 10.0f;

    public EventTracks()
    {
        Events = new List<TimelineEvent>();
    }

    public EventTracks(params TimelineEvent[] events)
    {
        Events = new List<TimelineEvent>(events);
        SortEvents();
    }

    /// <summary>
    /// Add an event to the timeline
    /// </summary>
    public void AddEvent(TimelineEvent timelineEvent)
    {
        if (timelineEvent == null)
            return;

        Events ??= new List<TimelineEvent>();
        Events.Add(timelineEvent);
        SortEvents();
    }

    /// <summary>
    /// Add an event at a specific time (in seconds)
    /// </summary>
    public void AddEvent(float timeInSeconds)
    {
        AddEvent(new TimelineEvent(timeInSeconds));
    }

    /// <summary>
    /// Remove an event from the timeline
    /// </summary>
    public bool RemoveEvent(TimelineEvent timelineEvent)
    {
        Events ??= new List<TimelineEvent>();
        return Events.Remove(timelineEvent);
    }

    /// <summary>
    /// Clear all events
    /// </summary>
    public void Clear()
    {
        Events ??= new List<TimelineEvent>();
        Events.Clear();
    }

    /// <summary>
    /// Get events at a specific time (with tolerance)
    /// </summary>
    public IEnumerable<TimelineEvent> GetEventsAtTime(float timeInSeconds, float tolerance = 0.001f)
    {
        Events ??= new List<TimelineEvent>();
        return Events.Where(e => Math.Abs(e.Time - timeInSeconds) <= tolerance);
    }

    /// <summary>
    /// Get events within a time range (in seconds)
    /// </summary>
    public IEnumerable<TimelineEvent> GetEventsInRange(float startTimeInSeconds, float endTimeInSeconds)
    {
        Events ??= new List<TimelineEvent>();
        return Events.Where(e => e.Time >= startTimeInSeconds && e.Time <= endTimeInSeconds);
    }

    /// <summary>
    /// Get the next event after a given time
    /// </summary>
    public TimelineEvent GetNextEvent(float timeInSeconds)
    {
        Events ??= new List<TimelineEvent>();
        return Events.FirstOrDefault(e => e.Time > timeInSeconds);
    }

    /// <summary>
    /// Get the previous event before a given time
    /// </summary>
    public TimelineEvent GetPreviousEvent(float timeInSeconds)
    {
        Events ??= new List<TimelineEvent>();
        return Events.LastOrDefault(e => e.Time < timeInSeconds);
    }

    /// <summary>
    /// Sort events by time
    /// </summary>
    private void SortEvents()
    {
        Events ??= new List<TimelineEvent>();
        Events.Sort((a, b) => a.Time.CompareTo(b.Time));
    }

    /// <summary>
    /// Get event count
    /// </summary>
    public int Count => Events?.Count ?? 0;

    public override string ToString()
    {
        return $"EventTracks '{EventId}' ({Count} events)";
    }
}

/// <summary>
/// Wrapper for a float curve with metadata
/// </summary>
[System.Serializable]
public class TimelineFloatCurve
{
    /// <summary>
    /// The name/ID of this float curve
    /// </summary>
    [Property]
    public string CurveId { get; set; } = "curve";

    /// <summary>
    /// The actual curve data
    /// </summary>
    [Property]
    public Curve Curve { get; set; } = new Curve();

    /// <summary>
    /// Whether this curve is enabled
    /// </summary>
    [Property]
    public bool Enabled { get; set; } = true;
    [Property]
    public bool Loop { get; set; } = false;

    public TimelineFloatCurve()
    {
        Curve = new Curve();
    }

    public TimelineFloatCurve(string curveId)
    {
        CurveId = curveId;
        Curve = new Curve();
    }

    /// <summary>
    /// Evaluate the curve at a specific time
    /// </summary>
    public float Evaluate(float timeInSeconds)
    {
	    if ( Loop )
	    {
		    return Curve.Evaluate( timeInSeconds.UnsignedMod( Curve.TimeRange.y) );
	    }
	    
        return Curve.Evaluate(timeInSeconds);
    }

    public override string ToString()
    {
        return $"FloatCurve '{CurveId}'";
    }
}

/// <summary>
/// Component that listens for timeline events and float curve updates
/// </summary>
public class TimelineEventDispatcher : Component
{
    // Dictionary to store event bindings
    private Dictionary<string, Action> _eventBindings = new();
    private Dictionary<string, Action<TimelineEvent>> _eventBindingsWithData = new();
    private Dictionary<string, Action<float>> _floatCurveBindings = new();

    protected override void OnAwake()
    {
        base.OnAwake();
        
        // Subscribe to global timeline events
        Timeline.OnEventTriggered += OnTimelineEvent;
        Timeline.OnFloatCurveUpdated += OnFloatCurveUpdated;
        
        // Register events manually (no reflection needed)
        RegisterEvents();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        Timeline.OnEventTriggered -= OnTimelineEvent;
        Timeline.OnFloatCurveUpdated -= OnFloatCurveUpdated;
    }

    private void OnTimelineEvent(GameObject gameObject, string eventId, TimelineEvent evt)
    {
	    if ( !gameObject.IsValid() || !GameObject.IsValid() ) return;
        // Only handle events for our GameObject
        if (gameObject.Root != GameObject.Root)
            return;

        // Try simple action first
        if (_eventBindings.TryGetValue(eventId, out var action))
        {
            action?.Invoke();
        }

        // Try action with event data
        if (_eventBindingsWithData.TryGetValue(eventId, out var actionWithData))
        {
            actionWithData?.Invoke(evt);
        }
    }

    private void OnFloatCurveUpdated(GameObject gameObject, string curveId, float value)
    {
	    if ( !gameObject.IsValid() || !GameObject.IsValid() ) return;
        // Only handle curves for our GameObject
        if (gameObject.Root != GameObject.Root)
            return;

        // Try float curve binding
        if (_floatCurveBindings.TryGetValue(curveId, out var action))
        {
            action?.Invoke(value);
        }
    }

    /// <summary>
    /// Bind an event ID to a simple action
    /// </summary>
    public void BindEvent(string eventId, Action action)
    {
        _eventBindings[eventId] = action;
    }

    /// <summary>
    /// Bind an event ID to an action that receives event data
    /// </summary>
    public void BindEvent(string eventId, Action<TimelineEvent> action)
    {
        _eventBindingsWithData[eventId] = action;
    }

    /// <summary>
    /// Bind a float curve ID to an action that receives the curve value
    /// </summary>
    public void BindFloatCurve(string curveId, Action<float> action)
    {
        _floatCurveBindings[curveId] = action;
    }

    /// <summary>
    /// Unbind an event
    /// </summary>
    public void UnbindEvent(string eventId)
    {
        _eventBindings.Remove(eventId);
        _eventBindingsWithData.Remove(eventId);
    }

    /// <summary>
    /// Unbind a float curve
    /// </summary>
    public void UnbindFloatCurve(string curveId)
    {
        _floatCurveBindings.Remove(curveId);
    }

    /// <summary>
    /// Override this method to register your timeline events and curves
    /// </summary>
    protected virtual void RegisterEvents()
    {
        // Override this in derived classes to register events and curves
        // Example:
        // BindEvent("jump", OnJump);
        // BindFloatCurve("volume", OnVolumeChanged);
    }
}

/// <summary>
/// Main timeline component with reverse play support
/// </summary>
public class Timeline : Component
{
    [Property]
    public List<EventTracks> EventTracks { get; set; } = new List<EventTracks>();
    
    [Property, InlineEditor]
    public List<TimelineFloatCurve> FloatCurves { get; set; } = new List<TimelineFloatCurve>();

    [Property]
    public bool AutoPlay { get; set; } = false;

    [Property]
    public bool Loop { get; set; } = false;

    [Property]
    public float Duration { get; set; } = 5.0f;

    /// <summary>
    /// Playback speed multiplier. Negative values play in reverse.
    /// </summary>
    [Property]
    public float PlaybackSpeed { get; set; } = 1.0f;

    public float CurrentTime { get; private set; }
    public bool IsPlaying { get; private set; }
    public bool IsReversePlaying => PlaybackSpeed < 0;

    /// <summary>
    /// Event fired when a timeline event is triggered
    /// </summary>
    public static event Action<GameObject, string, TimelineEvent> OnEventTriggered;

    /// <summary>
    /// Event fired when a float curve value is updated
    /// </summary>
    public static event Action<GameObject, string, float> OnFloatCurveUpdated;

    private float _lastTime = -1;
    private List<TimelineEvent> _triggeredEvents = new();

    protected override void OnAwake()
    {
        base.OnAwake();
        
        if (EventTracks == null)
            EventTracks = new List<EventTracks>();

        if (FloatCurves == null)
            FloatCurves = new List<TimelineFloatCurve>();

        if (AutoPlay)
            Play();
    }

    protected override void OnUpdate()
    {
        if (!IsPlaying)
            return;

        CurrentTime += Time.Delta * PlaybackSpeed;
        
        // Clamp current time to bounds
        CurrentTime = Math.Clamp(CurrentTime, 0, Duration);
        
        CheckForEvents();
        UpdateFloatCurves();

        // Handle end of timeline
        if ((PlaybackSpeed > 0 && CurrentTime >= Duration) || 
            (PlaybackSpeed < 0 && CurrentTime <= 0))
        {
            if (Loop)
            {
                CurrentTime = PlaybackSpeed > 0 ? 0 : Duration;
                _triggeredEvents.Clear();
            }
            else
            {
                Stop();
            }
        }
    }

    /// <summary>
    /// Start playing forward
    /// </summary>
    public void Play()
    {
        PlaybackSpeed = Math.Abs(PlaybackSpeed); // Ensure positive
        IsPlaying = true;
        _triggeredEvents.Clear();
    }

    /// <summary>
    /// Start playing in reverse
    /// </summary>
    public void PlayReverse()
    {
        PlaybackSpeed = -Math.Abs(PlaybackSpeed); // Ensure negative
        IsPlaying = true;
        _triggeredEvents.Clear();
    }

    /// <summary>
    /// Toggle between forward and reverse play
    /// </summary>
    public void ToggleDirection()
    {
        PlaybackSpeed = -PlaybackSpeed;
        _triggeredEvents.Clear(); // Clear to allow re-triggering events
    }

    public void Pause()
    {
        IsPlaying = false;
    }

    public void Stop()
    {
        IsPlaying = false;
        CurrentTime = PlaybackSpeed < 0 ? Duration : 0;
        _triggeredEvents.Clear();
    }

    public void Seek(float timeInSeconds)
    {
        CurrentTime = Math.Clamp(timeInSeconds, 0, Duration);
        _triggeredEvents.Clear();

        // Re-trigger events that should have happened by now
        // This depends on playback direction
        foreach (var eventTrack in EventTracks)
        {
            IEnumerable<TimelineEvent> eventsToTrigger;
            
            if (PlaybackSpeed >= 0)
            {
                // Forward: trigger events from 0 to current time
                eventsToTrigger = eventTrack.GetEventsInRange(0, CurrentTime);
            }
            else
            {
                // Reverse: trigger events from duration to current time
                eventsToTrigger = eventTrack.GetEventsInRange(CurrentTime, Duration);
            }
            
            foreach (var evt in eventsToTrigger)
            {
                TriggerEvent(evt, eventTrack.EventId);
            }
        }

        // Update float curves immediately
        UpdateFloatCurves();
        _lastTime = CurrentTime;
    }

    private void CheckForEvents()
    {
        foreach (var eventTrack in EventTracks)
        {
            IEnumerable<TimelineEvent> eventsToTrigger;

            if (PlaybackSpeed >= 0)
            {
                // Forward playback: trigger events between last time and current time
                eventsToTrigger = eventTrack.Events
                    .Where(e => e.Time > _lastTime && e.Time <= CurrentTime)
                    .Where(e => !_triggeredEvents.Contains(e));
            }
            else
            {
                // Reverse playback: trigger events between current time and last time
                eventsToTrigger = eventTrack.Events
                    .Where(e => e.Time >= CurrentTime && e.Time < _lastTime)
                    .Where(e => !_triggeredEvents.Contains(e))
                    .OrderByDescending(e => e.Time); // Trigger in reverse order
            }

            foreach (var evt in eventsToTrigger)
            {
                TriggerEvent(evt, eventTrack.EventId);
            }
        }

        _lastTime = CurrentTime;
    }

    private void UpdateFloatCurves()
    {
        foreach (var floatCurve in FloatCurves.Where(c => c.Enabled))
        {
            var value = floatCurve.Evaluate(CurrentTime);
            OnFloatCurveUpdated?.Invoke(GameObject, floatCurve.CurveId, value);
        }
    }

    private void TriggerEvent(TimelineEvent evt, string eventId)
    {
        if (_triggeredEvents.Contains(evt))
            return;

        _triggeredEvents.Add(evt);
        OnEventTriggered?.Invoke(GameObject, eventId, evt);

    }

    /// <summary>
    /// Add a new event track
    /// </summary>
    public EventTracks AddEventTrack(string eventId)
    {
        EventTracks ??= new List<EventTracks>();
        var track = new EventTracks { EventId = eventId };
        EventTracks.Add(track);
        return track;
    }

    /// <summary>
    /// Add a new float curve
    /// </summary>
    public TimelineFloatCurve AddFloatCurve(string curveId)
    {
        FloatCurves ??= new List<TimelineFloatCurve>();
        var curve = new TimelineFloatCurve(curveId);
        FloatCurves.Add(curve);
        return curve;
    }

    /// <summary>
    /// Get an event track by ID
    /// </summary>
    public EventTracks GetEventTrack(string eventId)
    {
        EventTracks ??= new List<EventTracks>();
        return EventTracks.FirstOrDefault(t => t.EventId == eventId);
    }

    /// <summary>
    /// Get a float curve by ID
    /// </summary>
    public TimelineFloatCurve GetFloatCurve(string curveId)
    {
        FloatCurves ??= new List<TimelineFloatCurve>();
        return FloatCurves.FirstOrDefault(c => c.CurveId == curveId);
    }
}

/// <summary>
/// Extended example with reverse play handling
/// </summary>
public class ExampleTimelineListener : TimelineEventDispatcher
{
    protected override void RegisterEvents()
    {
        // Register your timeline events here
        BindEvent("jump", OnJump);
        BindEvent("footstep", OnFootstep);
        BindEvent("explosion", OnExplosion);
        BindEvent("door_open", OnDoorOpen);
        BindEvent("door_close", OnDoorClose);

        // Register float curve handlers
        BindFloatCurve("volume", OnVolumeChanged);
        BindFloatCurve("speed", OnSpeedChanged);
    }

    public void OnJump()
    {
        var timeline = GameObject.Components.Get<Timeline>();
        var direction = timeline?.IsReversePlaying == true ? "landed" : "jumped";
        Log.Info($"Player {direction}!");
        
        // You can handle reverse differently if needed
        if (timeline?.IsReversePlaying == true)
        {
            // Handle reverse jump (landing)
            Sound.Play("land.sound");
        }
        else
        {
            // Handle forward jump
            Sound.Play("jump.sound");
        }
    }

    public void OnFootstep(TimelineEvent evt)
    {
        var timeline = GameObject.Components.Get<Timeline>();
        var direction = timeline?.IsReversePlaying == true ? "←" : "→";
        Log.Info($"Footstep {direction} at {evt.Time:F1}s");
        Sound.Play("footstep.sound");
    }

    public void OnDoorOpen()
    {
        var timeline = GameObject.Components.Get<Timeline>();
        if (timeline?.IsReversePlaying == true)
        {
            // When playing reverse, "door_open" event should close the door
            Log.Info("Door closing (reverse)");
            // Close door animation/sound
        }
        else
        {
            Log.Info("Door opening");
            // Open door animation/sound
        }
    }

    public void OnDoorClose()
    {
        var timeline = GameObject.Components.Get<Timeline>();
        if (timeline?.IsReversePlaying == true)
        {
            // When playing reverse, "door_close" event should open the door
            Log.Info("Door opening (reverse)");
            // Open door animation/sound
        }
        else
        {
            Log.Info("Door closing");
            // Close door animation/sound
        }
    }

    public void OnExplosion()
    {
        var timeline = GameObject.Components.Get<Timeline>();
        if (timeline?.IsReversePlaying == true)
        {
            Log.Info("Explosion reversing (implosion?)");
            // Maybe play reverse explosion effect
        }
        else
        {
            Log.Info("Explosion!");
            Sound.Play("explosion.sound");
        }
    }

    public void OnVolumeChanged(float volume)
    {
        // Float curves work the same in both directions
        //Sound.SetVolume(volume);
    }

    public void OnSpeedChanged(float speed)
    {
        // You might want to handle negative speeds for reverse
        var timeline = GameObject.Components.Get<Timeline>();
        var actualSpeed = timeline?.IsReversePlaying == true ? -speed : speed;
        Log.Info($"Speed changed to: {actualSpeed}");
    }
}
