namespace SuggestingBox.Maui;

internal static partial class TextFormatter
{
    internal static partial void ApplyFormatting(Editor editor, IReadOnlyList<SuggestionToken> tokens);
    internal static partial void ResetNativeText(Editor editor, string text, int cursorPosition);
    internal static partial double GetNativeContentHeight(CollectionView collectionView);
    // onCursorMoved(previousPosition, newPosition)
    internal static partial void SubscribeCursorChanged(Editor editor, Action<int, int> onCursorMoved);
    internal static partial void UnsubscribeCursorChanged(Editor editor);
    // Returns the Y coordinate of the bottom of the cursor line, in MAUI DIPs, relative to the editor's top.
    internal static partial double GetCursorBottomY(Editor editor);
    // Intercept image paste on platforms that embed images into the editor (e.g. Windows RichEditBox)
    internal static partial void SubscribePasteHandler(Editor editor, Action<byte[]> onImagePasted);
    internal static partial void UnsubscribePasteHandler(Editor editor);
}
