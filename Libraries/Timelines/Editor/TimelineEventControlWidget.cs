using System.Linq;
using Editor;
using Sandbox;

namespace Timeline;

[CustomEditor( typeof( EventTracks ) )]
public class TimelineEventControlWidget : ControlWidget
{
	public Color HighlightColor = Theme.Blue;

	public override bool SupportsMultiEdit => true;

	public TimelineEventControlWidget( SerializedProperty property ) : base( property )
	{
		Cursor = CursorShape.Finger;
		FixedHeight = 64; // Set a fixed height for the timeline preview
	}

	protected override void PaintOver()
	{
		var value = SerializedProperty.GetValue<EventTracks>();
		var col = HighlightColor.WithAlpha( Paint.HasMouseOver ? 1 : 0.5f );
		var inner = LocalRect.Shrink( 4.0f );

		// Draw timeline background
		Paint.SetBrush( Theme.ControlBackground );
		Paint.ClearPen();
		Paint.DrawRect( inner, 3 );

		// Draw timeline ruler
		Paint.SetPen( Theme.TextControl.WithAlpha( 0.2f ), 1 );
		for ( int i = 0; i <= 10; i++ )
		{
			float x = inner.Left + (inner.Width * i / 10f);
			float tickHeight = i % 5 == 0 ? 6 : 3;
			Paint.DrawLine( new Vector2( x, inner.Top ), new Vector2( x, inner.Top + tickHeight ) );
			Paint.DrawLine( new Vector2( x, inner.Bottom ), new Vector2( x, inner.Bottom - tickHeight ) );
		}

		// Draw events
		if ( value?.Events != null && value.Events.Count > 0 )
		{
			foreach ( var evt in value.Events )
			{
				float x = inner.Left + evt.Time * inner.Width;
				var eventColor = GetEventTypeColor( evt.EventType );
				
				// Draw event marker
				Paint.SetPen( eventColor, 2 );
				Paint.DrawLine( new Vector2( x, inner.Top + 8 ), new Vector2( x, inner.Bottom - 8 ) );
				
				// Draw event dot
				var dotRect = new Rect( x - 3, inner.Center.y - 3, 6, 6 );
				Paint.SetBrush( eventColor );
				Paint.ClearPen();
				Paint.DrawCircle( dotRect );
			}

			// Draw event count
			Paint.SetFont( "Roboto", 10, 400 );
			Paint.SetPen( Theme.TextControl.WithAlpha( 0.7f ) );
			Paint.DrawText( inner.Shrink( 4, 2 ), $"{value.Events.Count} events", TextFlag.Right | TextFlag.Bottom );
		}
		else
		{
			// Draw "no events" text
			Paint.SetFont( "Roboto", 10, 400 );
			Paint.SetPen( Theme.TextControl.WithAlpha( 0.5f ) );
			Paint.DrawText( inner, "Click to add events", TextFlag.Center );
		}

		// Draw border
		Paint.SetBrushAndPen( Color.Transparent, col, 2 );
		Paint.DrawRect( LocalRect.Shrink( 1 ), 3 );
	}

	Color GetEventTypeColor( string eventType )
	{
		return eventType?.ToLower() switch
		{
			"sound" => Color.Green,
			"effect" => Color.Blue,
			"animation" => Color.Orange,
			"trigger" => Color.Red,
			"camera" => Color.Yellow,
			_ => Theme.Primary
		};
	}

	protected override void OnMousePress( MouseEvent e )
	{
		base.OnMousePress( e );

		if ( e.LeftMouseButton )
		{
			// Open timeline event editor popup
			var popup = new TimelineEventPopup( this );

			popup.Position = e.ScreenPosition - popup.Size * new Vector2( 0.5f, 0.5f );
			popup.Visible = true;

			// Constrain to screen bounds
			popup.ConstrainToScreen();

			popup.AddEventTracks( SerializedProperty, Update );
		}
	}
}
