namespace SuggestingBox.Maui;

internal class FormattedEditor : Editor
{
    internal static readonly BindableProperty DisableInputAccessoryViewProperty =
        BindableProperty.Create(nameof(DisableInputAccessoryView), typeof(bool), typeof(FormattedEditor), true);

    internal bool DisableInputAccessoryView
    {
        get => (bool)GetValue(DisableInputAccessoryViewProperty);
        set => SetValue(DisableInputAccessoryViewProperty, value);
    }
}
