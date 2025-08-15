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
       
       // Draw Event ID and duration info
       var topInfoHeight = 14;


       // Draw duration info
       var duration = value?.Duration ?? 10.0f;
       Paint.SetFont( "Roboto", 8, 400 );
       Paint.SetPen( Theme.TextControl.WithAlpha( 0.7f ) );
       var durationRect = new Rect(inner.Right - 80, inner.Top + 2, 76, 12);
       Paint.DrawText( durationRect, $"{duration:F1}s", TextFlag.Right | TextFlag.Top );

       // Timeline ruler (below the info text)
       var rulerTop = inner.Top + topInfoHeight + 2;
       Paint.SetPen( Theme.TextControl.WithAlpha( 0.2f ), 1 );
       for ( int i = 0; i <= 10; i++ )
       {
          float x = inner.Left + (inner.Width * i / 10f);
          float tickHeight = i % 5 == 0 ? 6 : 3;
          Paint.DrawLine( new Vector2( x, rulerTop ), new Vector2( x, rulerTop + tickHeight ) );
          Paint.DrawLine( new Vector2( x, inner.Bottom - 4 ), new Vector2( x, inner.Bottom - 4 - tickHeight ) );
          
          // Draw time labels on major ticks
          if ( i % 5 == 0 )
          {
             float timeAtTick = (i / 10f) * duration;
             Paint.SetFont( "Roboto", 7, 400 );
             Paint.SetPen( Theme.TextControl.WithAlpha( 0.5f ) );
             Paint.DrawText( new Rect( x - 15, inner.Bottom - 15, 30, 8 ), $"{timeAtTick:F1}s", TextFlag.Center );
          }
       }

       // Draw events
       if ( value?.Events != null && value.Events.Count > 0 )
       {
          foreach ( var evt in value.Events )
          {
             // Calculate position based on absolute time and track duration
             float normalizedTime = duration > 0 ? evt.Time / duration : 0;
             float x = inner.Left + normalizedTime * inner.Width;
             
             // Only draw if within visible bounds
             if ( x >= inner.Left && x <= inner.Right )
             {
                var eventColor = GetEventIdColor(value.EventId);
                
                // Draw event marker line
                Paint.SetPen( eventColor, 2 );
                Paint.DrawLine( new Vector2( x, rulerTop + 8 ), new Vector2( x, inner.Bottom - 18 ) );
                
                // Draw event dot
                var dotRect = new Rect( x - 3, inner.Center.y + 2, 6, 6 );
                Paint.SetBrush( eventColor );
                Paint.ClearPen();
                Paint.DrawCircle( dotRect );
             }
          }

          // Draw event count
          Paint.SetFont( "Roboto", 8, 400 );
          Paint.SetPen( Theme.TextControl.WithAlpha( 0.7f ) );
          Paint.DrawText( inner.Shrink( 4, 2 ), $"{value.Events.Count} events", TextFlag.Left | TextFlag.Bottom );
       }
       else
       {
          // Draw "no events" text
          Paint.SetFont( "Roboto", 10, 400 );
          Paint.SetPen( Theme.TextControl.WithAlpha( 0.5f ) );
          var messageRect = new Rect(inner.Left, rulerTop + 8, inner.Width, inner.Bottom - rulerTop - 24);
          Paint.DrawText( messageRect, "Click to add events", TextFlag.Center );
       }

       // Draw border around timeline area
       Paint.SetBrushAndPen( Color.Transparent, col, 2 );
       Paint.DrawRect( inner, 3 );
       
       if (!string.IsNullOrEmpty(value?.EventId))
       {
	       Paint.SetFont( "Roboto", 9, 600 );
	       Paint.SetPen( Theme.Primary );
	       var labelRect = new Rect(inner.Left + 4, inner.Top + 2, inner.Width - 8, 12);
	       Paint.DrawText( labelRect, $"'{value.EventId}'", TextFlag.Left | TextFlag.Top );
       }
    }

    Color GetEventIdColor( string eventId )
    {
        // Generate a consistent color based on the event ID
        if (string.IsNullOrEmpty(eventId))
            return Theme.Primary;

        return eventId.ToLower() switch
        {
            "jump" => Color.Orange,
            "footstep" => Color.Green,
            "sound" => Color.Cyan,
            "explosion" => Color.Red,
            "effect" => Color.Blue,
            "animation" => Color.Yellow,
            "trigger" => Color.Yellow,
            "camera" => Color.Magenta,
            _ => new ColorHsv(eventId.GetHashCode() % 360, 0.7f, 0.8f) // Generate color from hash
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
