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
    internal static partial void SubscribePasteHandler(Editor editor, Action<byte[], int> onImagePasteRequested);
    internal static partial void UnsubscribePasteHandler(Editor editor);
    // Intercept platform undo when SuggestingBox must keep text and inline tokens in sync.
    internal static partial void SubscribeUndoHandler(Editor editor, Func<bool> onUndoRequested);
    internal static partial void UnsubscribeUndoHandler(Editor editor);
    // Returns the height of the software keyboard in MAUI DIPs, or 0 when no keyboard is visible.
    internal static partial double GetSoftKeyboardHeight();
    // Returns the position of source's top-left corner relative to target's top-left corner in MAUI DIPs.
    // Returns (NaN, NaN) when native views are unavailable.
    internal static partial Point GetPositionRelativeToView(VisualElement source, VisualElement target);
}
