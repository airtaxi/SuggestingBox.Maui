namespace SuggestingBox.Maui;

internal static partial class TextFormatter
{
    internal static partial void ApplyFormatting(Editor editor, IReadOnlyList<SuggestionToken> tokens);
    internal static partial void ResetNativeText(Editor editor, string text, int cursorPosition);
    internal static partial double GetNativeContentHeight(CollectionView collectionView);
    // onCursorMoved(previousPosition, newPosition)
    internal static partial void SubscribeCursorChanged(Editor editor, Action<int, int> onCursorMoved);
    internal static partial void UnsubscribeCursorChanged(Editor editor);
}
