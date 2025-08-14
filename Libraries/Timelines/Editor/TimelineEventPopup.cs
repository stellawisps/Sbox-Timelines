using System;
using Editor;
using Sandbox;

namespace Timeline;

public class TimelineEventPopup : Widget
{
    TimelineEventEditorWidget Editor;
    Label labelMultiple;

    public TimelineEventPopup(Widget parent) : base(parent)
    {
        WindowFlags = WindowFlags.Dialog | WindowFlags.Customized | WindowFlags.WindowTitle | WindowFlags.CloseButton | WindowFlags.WindowSystemMenuHint;
        WindowTitle = "Timeline Event Editor";
        DeleteOnClose = true;

        MinimumSize = new(800, 200);
        Size = new(800, 200);

        Editor = new TimelineEventEditorWidget(this);

        Layout = Layout.Column();
        Layout.Margin = 16;
        Layout.Spacing = 8;

        labelMultiple = Layout.Add(new Label(this));
        labelMultiple.Text = "Multiple Values Selected. Making changes will modify all.";
        labelMultiple.SetStyles($"color: {Theme.MultipleValues.Hex};");
        labelMultiple.Visible = false;

        Layout.Add(Editor, 1);
    }

    public void AddEventTracks(SerializedProperty serializedProperty, Action onChanged)
    {
        labelMultiple.Visible = serializedProperty.IsMultipleDifferentValues;

        AddEventTracks(
            () => serializedProperty.GetValue<EventTracks>(), 
            v =>
            {
                serializedProperty.SetValue(v);
                onChanged?.Invoke();
                labelMultiple.Visible = serializedProperty.IsMultipleDifferentValues;
            }
        );
    }

    public void AddEventTracks(Func<EventTracks> get, Action<EventTracks> set)
    {
        var eventTracks = get() ?? new EventTracks();
        
        Editor.SerializedProperty = null; // We're not using serialized property directly in this case
        Editor.Value = eventTracks;
        Editor.ValueChanged = (newValue) =>
        {
            set?.Invoke(newValue);
        };
    }

    protected override void OnPaint()
    {
        base.OnPaint();
        
        // Update multiple values visibility
        if (Editor.SerializedProperty != null)
        {
            labelMultiple.Visible = Editor.SerializedProperty.IsMultipleDifferentValues;
        }
    }
}
