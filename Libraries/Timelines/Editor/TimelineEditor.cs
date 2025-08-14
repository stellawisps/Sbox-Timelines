using Editor;
using Sandbox;

namespace Timeline;


[CustomEditor(typeof(Timeline))]
public class TimelineControlWidget : ControlObjectWidget
{
	// Whether or not this control supports multi-editing (if you have multiple GameObjects selected)
	public override bool SupportsMultiEdit => false;

	public TimelineControlWidget(SerializedProperty property) : base(property, true)
	{
		Layout = Layout.Column();
		Layout.Spacing = 2;

		// Get the EventTracks property from the serialized object
		SerializedObject.TryGetProperty(nameof(Timeline.EventTracks), out var events);

		// Add the timeline event control
		
		Layout.Add(Create(events));
		
	}

	protected override void OnPaint()
	{
		// Overriding and doing nothing here will prevent the default background from being painted
	}
}
