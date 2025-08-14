using System;
using System.Collections.Generic;
using System.Linq;
using Editor;
using Editor.GraphicsItems;
using Sandbox;

namespace Timeline;

public partial class TimelineEventEditorWidget : Widget
{
	public Action<EventTracks> ValueChanged { get; set; }

	EventStopWidget eventBar;
	TimelineAreaWidget timelineArea;

	public EventStopWidget.Point selectedPoint;

	[Range( 0.0f, 100.0f, slider: false ), Step( 0.1f ), Title( "Time (seconds)" )]
	public float TimeValue
	{
		get => selectedPoint?.AbsoluteTime ?? 0.0f;
		set
		{
			if ( selectedPoint is null )
				return;

			selectedPoint.AbsoluteTime = value;
			// Convert back to normalized time for display
			selectedPoint.Time = Value.Duration > 0 ? value / Value.Duration : 0;
			UpdateFromPoints();
		}
	}
	
	[Range( 0.1f, 10.0f, slider: false ), Step( 0.1f ), Title( "Track Duration (seconds)" )]
	public float DurationValue
	{
		get => Value?.Duration ?? 10.0f;
		set
		{
			if ( Value != null )
			{
				Value.Duration = Math.Max(0.1f, value);
				UpdatePoints(); // Recalculate normalized positions
				OnEdited();
			}
		}
	}
	
	public string NameValue
	{
		get => Value?.EventId ?? "";
		set
		{
			if ( Value != null )
			{
				Value.EventId = value;
				OnEdited();
			}
		}
	}

	Label labelMultiple;
	ControlWidget editTime;
	ControlWidget editDuration;
	ControlWidget editName;

	public EventTracks _value;
	
	/// <summary>
	/// The current event tracks value
	/// </summary>
	public EventTracks Value
	{
		get => _value;
		set
		{
			_value = value;
			Update();
			ValueChanged?.Invoke( _value );
			UpdatePoints();
		}
	}

	public SerializedProperty SerializedProperty { get; set; }

	public TimelineEventEditorWidget( Widget parent = null ) : base( parent )
	{
		_value = new EventTracks();

		Layout = Layout.Column();
		FocusMode = FocusMode.Click;

		labelMultiple = Layout.Add( new Label( this ) );
		labelMultiple.Text = "Multiple Values Selected. Making changes will modify all.";
		labelMultiple.SetStyles( $"color: {Theme.MultipleValues.Hex};" );
		labelMultiple.Visible = false;

		// Timeline area for visualization
		timelineArea = Layout.Add( new TimelineAreaWidget( this ) );

		// Event stops bar
		eventBar = Layout.Add( new EventStopWidget( this ) );
		eventBar.OnAddPoint = ( normalizedTime ) =>
		{
			// Convert normalized time to absolute time
			float absoluteTime = normalizedTime * Value.Duration;
			_value.AddEvent( new TimelineEvent { Time = absoluteTime } );
			UpdatePoints();
			OnEdited();
		};

		Layout.AddSpacingCell( 6 );

		var so = this.GetSerialized();
		so.OnPropertyChanged = OnEventEdited;

		var row = Layout.AddRow();
		var controls = Layout.Grid();
		controls.Margin = 8;
		controls.Spacing = 8;
		row.AddLayout( controls );
		row.AddStretchCell( 1 );

		{
			editTime = controls.AddCell( 0, 0, new FloatControlWidget( so.GetProperty( "TimeValue" ) ) { Label = "Time", Icon = "timeline" } );
			editTime.Enabled = false;
			editTime.MaximumWidth = 200;
			editTime.MaximumHeight = 25;

			controls.AddCell( 1, 0, new Label( "Duration" ) );

			editDuration = controls.AddCell( 2, 0, new FloatControlWidget( so.GetProperty( "DurationValue" ) ) {  } );
			editDuration.MaximumWidth = 200;
			editDuration.MaximumHeight = 25;
			editDuration.Enabled = false;

			editName = controls.AddCell( 0, 1, new StringControlWidget( so.GetProperty( "NameValue" ) ) );
			editName.MaximumWidth = 300;
			editName.Enabled = false;
			
			var options = controls.AddCell( 1, 1, Layout.Row(), alignment: TextFlag.Left );
			options.Spacing = 8;

			var delete = options.Add( new IconButton( "delete", DeletePoint ) );
			delete.ToolTip = "Remove Event";
			delete.Bind( "Enabled" ).ReadOnly().From( () => selectedPoint is not null, null );

			options.AddStretchCell( 1 );

			var selectNext = options.Add( new IconButton( "chevron_left", () => SelectNext( false ) ) { ToolTip = "Select previous" } );
			selectNext.Bind( "Enabled" ).ReadOnly().From( () => selectedPoint is not null, null );

			var selectPrev = options.Add( new IconButton( "chevron_right", () => SelectNext( true ) ) { ToolTip = "Select next" } );
			selectPrev.Bind( "Enabled" ).ReadOnly().From( () => selectedPoint is not null, null );

			options.Add( new IconButton( "more_horiz", DoMoreOptionsMenu ) );
		}
	}

	private void SelectNext( bool forward )
	{
		if ( selectedPoint == null || eventBar.Points.Count == 0 )
			return;

		int currentIdx = selectedPoint.Index;
		int nextIdx = (currentIdx + (forward ? 1 : -1) + eventBar.Points.Count) % eventBar.Points.Count;
		UpdateSelection( eventBar.Points[nextIdx] );
	}

	private void DoMoreOptionsMenu()
	{
		var menu = new ContextMenu( this );

		menu.AddOption( new Option( "Distribute Evenly", "balance", () =>
		{
			for ( int i = 0; i < eventBar.Points.Count; i++ )
			{
				float normalizedTime = (float)i / Math.Max( 1, eventBar.Points.Count - 1 );
				eventBar.Points[i].Time = normalizedTime;
				eventBar.Points[i].AbsoluteTime = normalizedTime * Value.Duration;
			}
			UpdateFromPoints();
		} ) );

		menu.AddOption( new Option( "Clear All Events", "delete_sweep", () =>
		{
			Value.Events = new List<TimelineEvent>();
			eventBar.Points.Clear();
			Update();
		} ) );

		menu.OpenAtCursor();
	}

	protected override void OnPaint()
	{
		labelMultiple.Visible = SerializedProperty?.IsMultipleDifferentValues ?? false;
		editDuration.Enabled = true;
		editName.Enabled = true;
	}

	public void OnEdited()
	{
		Update();
	}

	[Shortcut( "editor.delete", "DEL" )]
	void DeletePoint()
	{
		if ( selectedPoint == null )
			return;

		eventBar.Points.Remove( selectedPoint );
		UpdateSelection( null );
		UpdateFromPoints();
	}

	private void OnEventEdited( SerializedProperty property )
	{
		UpdateFromPoints();
	}

	bool skipUpdatePoints;

	public void UpdatePoints()
	{
		if ( skipUpdatePoints ) return;

		eventBar.Points.Clear();

		if ( Value.Events != null )
		{
			for ( int i = 0; i < Value.Events.Count; i++ )
			{
				var evt = Value.Events[i];
				var p = new EventStopWidget.Point
				{
					Index = i,
					Time = Value.Duration > 0 ? evt.Time / Value.Duration : 0, // Normalized for display
					AbsoluteTime = evt.Time, // Store absolute time
					Paint = PaintEvent,
					Moved = ( p ) => UpdateFromPoints(),
					Pressed = p => UpdateSelection( p )
				};

				eventBar.Points.Add( p );
			}
		}

		eventBar.Update();
		timelineArea.Update();
		UpdateSelection( selectedPoint ); // Maintain selection if possible
	}

	private void UpdateFromPoints()
	{
		var val = Value ?? new EventTracks();
		val.Events = new List<TimelineEvent>();

		foreach ( var p in eventBar.Points )
		{
			if ( p.Disabled ) continue;
			// Convert normalized time back to absolute time
			float absoluteTime = p.Time * val.Duration;
			p.AbsoluteTime = absoluteTime; // Update the stored absolute time
			val.AddEvent( new TimelineEvent { Time = absoluteTime } );
		}

		skipUpdatePoints = true;
		Value = val;
		skipUpdatePoints = false;
	}

	void PaintEvent( EventStopWidget.Point p )
	{
		var box = p.Rect.Shrink( 2, 2, 2, 2 );

		// Selection highlight
		if ( selectedPoint == p )
		{
			Paint.SetPen( Theme.Blue, 3 );
			Paint.ClearBrush();
			Paint.DrawRect( box.Grow( 2 ), 3 );
		}

		// Event marker
		Paint.SetPen( Theme.Primary, 2 );
		Paint.SetBrush( Theme.ControlBackground );
		Paint.DrawRect( box, 2 );

		// Event type indicator (small colored dot)
		var dotRect = new Rect( box.Center.x - 3, box.Center.y - 3, 6, 6 );
		Paint.SetBrush( Color.Blue );
		Paint.ClearPen();
		Paint.DrawCircle( dotRect );
	}

	void UpdateSelection( EventStopWidget.Point p )
	{
		selectedPoint = p;
		editTime.Enabled = p is not null;
	}
}

// Timeline visualization area
class TimelineAreaWidget : Widget
{
	private TimelineEventEditorWidget _eventEditor;

	public TimelineAreaWidget( TimelineEventEditorWidget eventEditor )
	{
		_eventEditor = eventEditor;
		FixedHeight = 64;
	}

	protected override void OnPaint()
	{
		base.OnPaint();
		var rect = LocalRect.Shrink( 8, 4 );
		
		// Timeline background
		Paint.SetBrush( Theme.ControlBackground.Darken( 0.5f ) );
		Paint.ClearPen();
		Paint.DrawRect( rect, 4 );

		// Draw duration info
		var duration = _eventEditor.Value?.Duration ?? 10.0f;
		Paint.SetFont( "Roboto", 9, 400 );
		Paint.SetPen( Theme.TextControl.WithAlpha( 0.7f ) );
		Paint.DrawText( rect.Shrink( 4, 2 ), $"Duration: {duration:F1}s", TextFlag.Left | TextFlag.Top );

		// Timeline ruler with time markers
		Paint.SetPen( Theme.TextControl.WithAlpha( 0.3f ), 1 );
		for ( int i = 0; i <= 10; i++ )
		{
			float x = rect.Left + (rect.Width * i / 10f);
			float tickHeight = i % 5 == 0 ? 8 : 4;
			Paint.DrawLine( new Vector2( x, rect.Top + 12 ), new Vector2( x, rect.Top + 12 + tickHeight ) );
			Paint.DrawLine( new Vector2( x, rect.Bottom ), new Vector2( x, rect.Bottom - tickHeight ) );
			
			// Draw time labels on major ticks
			if ( i % 5 == 0 )
			{
				float timeAtTick = (i / 10f) * duration;
				Paint.SetFont( "Roboto", 8, 400 );
				Paint.DrawText( new Rect( x - 15, rect.Bottom - 15, 30, 10 ), $"{timeAtTick:F1}s", TextFlag.Center );
			}
		}

		// Events
		if ( _eventEditor.Value?.Events != null )
		{
			foreach ( var evt in _eventEditor.Value.Events )
			{
				// Calculate position based on absolute time
				float normalizedTime = duration > 0 ? evt.Time / duration : 0;
				float x = rect.Left + normalizedTime * rect.Width;
				
				// Clamp to visible area
				if ( x >= rect.Left && x <= rect.Right )
				{
					Paint.SetPen( Color.Blue, 3 );
					Paint.DrawLine( new Vector2( x, rect.Top + 20 ), new Vector2( x, rect.Bottom - 20 ) );
					
					// Draw time label
					Paint.SetFont( "Roboto", 8, 600 );
					Paint.SetPen( Color.Blue );
					Paint.DrawText( new Rect( x - 20, rect.Top + 22, 40, 10 ), $"{evt.Time:F1}s", TextFlag.Center );
				}
			}
		}
	}

	protected override void OnMouseClick( MouseEvent e )
	{
		base.OnMouseClick( e );

		var rect = LocalRect.Shrink( 8, 4 );
		var normalizedTime = (e.LocalPosition.x - rect.Left) / rect.Width;
		normalizedTime = normalizedTime.Clamp( 0, 1 );

		// Convert to absolute time
		var absoluteTime = normalizedTime * _eventEditor.Value.Duration;

		// Add event at clicked position
		_eventEditor._value.AddEvent( new TimelineEvent { Time = absoluteTime } );
		_eventEditor.UpdatePoints();
		_eventEditor.OnEdited();
	}
}

// Event stops widget for dragging events
public class EventStopWidget : Widget
{
	public Action<float> OnAddPoint;

	Point Pressed;
	Point Hovered;

	public class Point
	{
		public int Index { get; set; }
		public float Time { get; set; } // Normalized time (0-1) for display positioning
		public float AbsoluteTime { get; set; } // Absolute time in seconds
		public Action<Point> Paint { get; set; }
		public Action<Point> Pressed { get; set; }
		public Action<Point> Moved { get; set; }
		public Rect Rect { get; set; }
		public bool Disabled { get; set; }
	}

	public List<Point> Points = new();

	public EventStopWidget( Widget parent ) : base( parent )
	{
		FixedHeight = 24;
		MouseTracking = true;
	}

	protected override void OnPaint()
	{
		var w = 16;

		Paint.Antialiasing = true;

		foreach ( var p in Points )
		{
			if ( p.Disabled ) continue;

			var x = 8 + p.Time * (LocalRect.Width - 16.0f);
			p.Rect = new Rect( x - (w / 2), 2, w, Height - 4 );

			Paint.SetFlags( false, Hovered == p, Pressed == p, false, true );
			p.Paint?.Invoke( p );
		}
	}

	float LocalToTime( float local ) => (local - 8) / (Width - 16.0f);

	protected override void OnMouseMove( MouseEvent e )
	{
		if ( Pressed is not null )
		{
			Pressed.Disabled = !LocalRect.Grow( 256, 16 ).IsInside( e.LocalPosition );
			Pressed.Time = LocalToTime( e.LocalPosition.x ).Clamp( 0, 1 );
			Pressed.Moved?.Invoke( Pressed );
			Cursor = CursorShape.Finger;
			Update();
			return;
		}

		Hovered = null;

		foreach ( var p in Points )
		{
			var x = 8 + p.Time * (LocalRect.Width - 16.0f);
			if ( MathF.Abs( x - e.LocalPosition.x ) < 8.0f )
			{
				Hovered = p;
			}
		}

		Cursor = Hovered == null ? CursorShape.Arrow : CursorShape.Finger;
		Update();
	}

	protected override void OnMousePress( MouseEvent e )
	{
		base.OnMousePress( e );

		var delta = LocalToTime( e.LocalPosition.x ).Clamp( 0, 1 );

		if ( Hovered is not null )
		{
			Pressed = Hovered;
			Pressed?.Pressed?.Invoke( Pressed );
			return;
		}

		OnAddPoint?.Invoke( delta );

		Pressed = Points.FirstOrDefault( x => MathF.Abs( x.Time - delta ) < 0.01f );
		Pressed?.Pressed?.Invoke( Pressed );
		Update();
	}

	protected override void OnMouseReleased( MouseEvent e )
	{
		if ( Pressed is not null && Pressed.Disabled )
		{
			Points.Remove( Pressed );
		}

		Pressed = null;
		Update();
	}

	protected override void OnMouseLeave()
	{
		Hovered = null;
		Update();
	}
}
