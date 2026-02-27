using System.Collections;
using Microsoft.Maui.Controls.Shapes;

namespace SuggestingBox.Maui;

public class SuggestingBox : ContentView
{
    private readonly FormattedEditor editor;
    private readonly CollectionView suggestionListView;
    private readonly Border suggestionPopup;
    private readonly List<SuggestionToken> tokens = [];
    private string currentPrefix = string.Empty;
    private string currentQueryText = string.Empty;
    private int prefixStartIndex = -1;
    private bool isUpdatingText;
    private int textChangeGeneration;
    private bool hasPendingTokenDeletion;
    private double measuredItemHeight;
    private int lastKnownCursorPosition = -1;

    public static readonly BindableProperty PrefixesProperty =
        BindableProperty.Create(nameof(Prefixes), typeof(string), typeof(SuggestingBox), string.Empty);

    public static readonly BindableProperty ItemsSourceProperty =
        BindableProperty.Create(nameof(ItemsSource), typeof(IEnumerable), typeof(SuggestingBox), null,
            propertyChanged: OnItemsSourceChanged);

    public static readonly BindableProperty ItemTemplateProperty =
        BindableProperty.Create(nameof(ItemTemplate), typeof(DataTemplate), typeof(SuggestingBox), null,
            propertyChanged: OnItemTemplateChanged);

    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(SuggestingBox), string.Empty,
            BindingMode.TwoWay, propertyChanged: OnTextPropertyChanged);

    public static readonly BindableProperty PlaceholderProperty =
        BindableProperty.Create(nameof(Placeholder), typeof(string), typeof(SuggestingBox), string.Empty,
            propertyChanged: OnPlaceholderPropertyChanged);

    public static readonly BindableProperty MaxSuggestionHeightProperty =
        BindableProperty.Create(nameof(MaxSuggestionHeight), typeof(double), typeof(SuggestingBox), 200.0,
            propertyChanged: OnSuggestionHeightPropertyChanged);

    public string Prefixes
    {
        get => (string)GetValue(PrefixesProperty);
        set => SetValue(PrefixesProperty, value);
    }

    public IEnumerable ItemsSource
    {
        get => (IEnumerable)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public DataTemplate ItemTemplate
    {
        get => (DataTemplate)GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public double MaxSuggestionHeight
    {
        get => (double)GetValue(MaxSuggestionHeightProperty);
        set => SetValue(MaxSuggestionHeightProperty, value);
    }

    public event SuggestingBoxEventHandler<SuggestionChosenEventArgs> SuggestionChosen;
    public event SuggestingBoxEventHandler<SuggestionRequestedEventArgs> SuggestionRequested;
    public event SuggestingBoxEventHandler<ImageInsertedEventArgs> ImageInserted;

    public SuggestingBox()
    {
        editor = new FormattedEditor
        {
            AutoSize = EditorAutoSizeOption.TextChanges,
            VerticalOptions = LayoutOptions.Start
        };
        editor.TextChanged += OnEditorTextChanged;
        editor.PropertyChanged += OnEditorPropertyChanged;
        editor.HandlerChanged += OnEditorHandlerChanged;

        suggestionListView = new CollectionView
        {
            SelectionMode = SelectionMode.Single,
            VerticalOptions = LayoutOptions.Start
        };
        suggestionListView.SelectionChanged += OnSuggestionSelected;

        suggestionPopup = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = 4 },
            Padding = new Thickness(4),
            IsVisible = false,
            Content = suggestionListView,
            VerticalOptions = LayoutOptions.Start
        };

        Content = new VerticalStackLayout
        {
            Children = { editor, suggestionPopup }
        };

        UpdateThemeColors();
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (Application.Current is Application application)
            application.RequestedThemeChanged += OnThemeChanged;
    }

    private void OnEditorHandlerChanged(object sender, EventArgs e)
    {
        if (editor.Handler is not null)
            TextFormatter.SubscribeCursorChanged(editor, OnNativeCursorChanged);
        else
            TextFormatter.UnsubscribeCursorChanged(editor);
    }

    private void OnNativeCursorChanged(int previousPosition, int newPosition)
    {
        if (isUpdatingText || hasPendingTokenDeletion || tokens.Count == 0) return;

        string text = editor.Text ?? string.Empty;
        var tokenAtCursor = tokens.FirstOrDefault(
            token => newPosition > token.StartIndex && newPosition < token.EndIndex);

        if (tokenAtCursor is null) return;

        bool movingLeft = previousPosition > newPosition || previousPosition < 0;
        int targetCursor = movingLeft
            ? tokenAtCursor.StartIndex
            : tokenAtCursor.EndIndex;

        if (!movingLeft && targetCursor < text.Length && text[targetCursor] == ' ')
            targetCursor++;

        int finalCursor = Math.Min(targetCursor, text.Length);

        // WinUI ignores Selection.SetRange calls made synchronously inside a SelectionChanged handler,
        // so defer to the next frame.
        isUpdatingText = true;
        Dispatcher.Dispatch(() =>
        {
            TextFormatter.ResetNativeText(editor, text, finalCursor);
            isUpdatingText = false;
        });
    }

    public IEnumerable<object> GetSuggestion()
    {
        if (ItemsSource is null)
            return [];

        return ItemsSource.Cast<object>();
    }

    public void RaiseImageInserted(byte[] imageData) =>
        ImageInserted?.Invoke(this, new ImageInsertedEventArgs(imageData));

    private void OnThemeChanged(object sender, AppThemeChangedEventArgs args) => UpdateThemeColors();

    private void UpdateThemeColors()
    {
        bool isDark = Application.Current?.RequestedTheme == AppTheme.Dark;

        suggestionPopup.BackgroundColor = isDark
            ? Color.FromArgb("#2D2D2D")
            : Colors.White;

        suggestionPopup.Stroke = new SolidColorBrush(isDark
            ? Color.FromArgb("#555555")
            : Colors.LightGray);

        suggestionPopup.Shadow = new Shadow
        {
            Brush = isDark ? Brush.White : Brush.Black,
            Offset = new Point(0, 2),
            Radius = 4,
            Opacity = isDark ? 0.1f : 0.2f
        };
    }

    private void OnEditorTextChanged(object sender, TextChangedEventArgs args)
    {
        if (isUpdatingText) return;

        // Swallow text changes while a deferred token deletion is pending.
        // The MAUI handler's MapText reverts the native text to the pre-deletion
        // value after HandleTokenDeletion; rapid key repeats would then see
        // token positions that don't match the actual text.
        if (hasPendingTokenDeletion) return;

        string oldText = args.OldTextValue ?? string.Empty;
        string newText = args.NewTextValue ?? string.Empty;

        // Handle atomic token deletion: when any part of a token is deleted, remove the entire token
        if (HandleTokenDeletion(oldText, newText)) return;

        // Adjust token positions for edits that don't affect tokens
        AdjustTokenPositions(oldText, newText);

        isUpdatingText = true;
        Text = newText;
        isUpdatingText = false;

        // Infer cursor position from the edit region because editor.CursorPosition
        // may not be synced yet on Android/iOS when TextChanged fires.
        var (editPosition, _, insertedLength) = FindEditRegion(oldText, newText);
        int inferredCursor = editPosition + insertedLength;

        DetectSuggestionTrigger(newText, inferredCursor);

        // Re-apply formatting after every text change so that text typed at a token
        // boundary (where iOS TypingAttributes inherits the token's style) is
        // corrected to default formatting on the next frame.
        if (tokens.Count > 0)
            ScheduleFormatting();
    }

    private void OnEditorPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(Editor.CursorPosition)) return;
        if (isUpdatingText || hasPendingTokenDeletion || tokens.Count == 0) return;

        int previousCursor = lastKnownCursorPosition;
        lastKnownCursorPosition = editor.CursorPosition;

        // Defer to the next frame so any concurrent text change (e.g. backspace) can set
        // hasPendingTokenDeletion first. Without this, on iOS the cursor push fires before
        // HandleTokenDeletion, which corrupts the cursor state and causes it to get stuck.
        Dispatcher.Dispatch(() =>
        {
            if (isUpdatingText || hasPendingTokenDeletion || tokens.Count == 0) return;

            int cursorPosition = editor.CursorPosition;
            string text = editor.Text ?? string.Empty;

            var tokenAtCursor = tokens.FirstOrDefault(
                token => cursorPosition > token.StartIndex && cursorPosition < token.EndIndex);

            if (tokenAtCursor is null) return;

            // Detect direction: moving left pushes to token start, moving right pushes past token end
            bool movingLeft = previousCursor > cursorPosition;
            int targetCursor = movingLeft
                ? tokenAtCursor.StartIndex
                : tokenAtCursor.EndIndex;

            if (!movingLeft && targetCursor < text.Length && text[targetCursor] == ' ')
                targetCursor++;

            isUpdatingText = true;
            editor.CursorPosition = Math.Min(targetCursor, text.Length);
            lastKnownCursorPosition = editor.CursorPosition;
            isUpdatingText = false;
        });
    }


    private bool HandleTokenDeletion(string oldText, string newText)
    {
        if (tokens.Count == 0) return false;

        // move the cursor past the token boundary (tokens are immutable).
        if (newText.Length > oldText.Length)
        {
            var (insertPosition, _, insertedLength) = FindEditRegion(oldText, newText);
            var targetToken = tokens.FirstOrDefault(
                token => insertPosition > token.StartIndex && insertPosition <= token.EndIndex);

            if (targetToken is null) return false;

            // Allow whitespace inserted exactly at the token end boundary — it falls outside
            // the visible token and does not inherit the token's styling after ScheduleFormatting.
            if (insertPosition == targetToken.EndIndex
                && insertedLength > 0
                && newText.Substring(insertPosition, insertedLength).All(char.IsWhiteSpace))
                return false;

            int cursorAfterToken = targetToken.EndIndex;
            if (cursorAfterToken < oldText.Length && oldText[cursorAfterToken] == ' ')
                cursorAfterToken++;

            ApplyTextAndScheduleSync(oldText, Math.Min(cursorAfterToken, oldText.Length));
            return true;
        }

        if (newText.Length >= oldText.Length) return false;

        var (editPosition, deletedCount) = FindDeletionRegion(oldText, newText);

        // Find tokens overlapping with the deleted region in old text coordinates
        var affectedTokens = tokens
            .Where(token => token.StartIndex < editPosition + deletedCount && token.EndIndex > editPosition)
            .OrderByDescending(token => token.StartIndex)
            .ToList();

        if (affectedTokens.Count == 0) return false;

        // Reconstruct from the old text with entire tokens removed
        string result = oldText;
        int cursorTarget = editPosition;

        foreach (var token in affectedTokens)
        {
            int removeLength = token.Length;

            // Also remove the trailing space that was inserted after the token
            if (token.EndIndex < result.Length && result[token.EndIndex] == ' ')
                removeLength++;

            cursorTarget = Math.Min(cursorTarget, token.StartIndex);
            result = result.Remove(token.StartIndex, removeLength);
            tokens.Remove(token);
        }

        RecalculateTokenPositions(result);
        ApplyTextAndScheduleSync(result, Math.Min(cursorTarget, result.Length));
        return true;
    }

    private void ApplyTextAndScheduleSync(string resultText, int cursorPosition)
    {
        hasPendingTokenDeletion = true;

        isUpdatingText = true;
        Text = resultText;
        TextFormatter.ResetNativeText(editor, resultText, cursorPosition);
        isUpdatingText = false;

        int generation = ++textChangeGeneration;
        Dispatcher.Dispatch(() =>
        {
            hasPendingTokenDeletion = false;
            if (generation != textChangeGeneration) return;

            isUpdatingText = true;
            editor.Text = resultText;
            editor.CursorPosition = cursorPosition;
            Text = resultText;
            TextFormatter.ResetNativeText(editor, resultText, cursorPosition);
            lastKnownCursorPosition = cursorPosition;
            isUpdatingText = false;
            ScheduleFormatting();
        });
    }

    private void AdjustTokenPositions(string oldText, string newText)
    {
        if (tokens.Count == 0 || oldText.Length == newText.Length) return;

        var (editPosition, oldLength, newLength) = FindEditRegion(oldText, newText);
        int shift = newLength - oldLength;
        if (shift == 0) return;

        foreach (var token in tokens.Where(token => token.StartIndex >= editPosition + oldLength))
            token.StartIndex += shift;
    }

    private void DetectSuggestionTrigger(string text, int cursorPosition)
    {
        string prefixes = Prefixes ?? string.Empty;

        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(prefixes))
        {
            HideSuggestions();
            return;
        }

        // Use the inferred cursor position so prefixes in the middle of text work
        cursorPosition = Math.Min(cursorPosition, text.Length);

        int lastPrefixIndex = -1;
        char foundPrefix = '\0';

        for (int index = cursorPosition - 1; index >= 0; index--)
        {
            // Skip positions that fall inside existing tokens
            if (tokens.Any(token => index >= token.StartIndex && index < token.EndIndex)) continue;

            // Stop scanning at whitespace boundaries (except the prefix char itself)
            if (char.IsWhiteSpace(text[index]) && index != cursorPosition - 1) break;

            if (!prefixes.Contains(text[index])) continue;
            if (index == 0 || char.IsWhiteSpace(text[index - 1]))
            {
                lastPrefixIndex = index;
                foundPrefix = text[index];
                break;
            }
        }

        if (lastPrefixIndex < 0)
        {
            HideSuggestions();
            return;
        }

        // Query text spans from after the prefix character to the cursor position
        string queryText = text[(lastPrefixIndex + 1)..cursorPosition];

        if (queryText.Contains('\n') || queryText.Contains('\r'))
        {
            HideSuggestions();
            return;
        }

        currentPrefix = foundPrefix.ToString();
        currentQueryText = queryText;
        prefixStartIndex = lastPrefixIndex;

        SuggestionRequested?.Invoke(this, new SuggestionRequestedEventArgs(currentPrefix, queryText));

        if (ItemsSource is not null && ItemsSource.Cast<object>().Any())
            suggestionPopup.IsVisible = true;
        else
            HideSuggestions();
    }

    private void OnSuggestionSelected(object sender, SelectionChangedEventArgs args)
    {
        if (args.CurrentSelection.Count == 0) return;

        object selectedItem = args.CurrentSelection[0];
        var chosenArgs = new SuggestionChosenEventArgs(currentPrefix, selectedItem);
        SuggestionChosen?.Invoke(this, chosenArgs);

        if (!string.IsNullOrEmpty(chosenArgs.DisplayText))
        {
            isUpdatingText = true;

            string text = editor.Text ?? string.Empty;
            string before = text[..prefixStartIndex];
            int queryEnd = prefixStartIndex + currentPrefix.Length + currentQueryText.Length;
            string after = queryEnd < text.Length ? text[queryEnd..] : string.Empty;

            string tokenText = currentPrefix + chosenArgs.DisplayText;

            var token = new SuggestionToken(
                prefixStartIndex,
                currentPrefix,
                chosenArgs.DisplayText,
                new SuggestionFormat
                {
                    BackgroundColor = chosenArgs.Format.BackgroundColor,
                    ForegroundColor = chosenArgs.Format.ForegroundColor,
                    Bold = chosenArgs.Format.Bold
                });

            // Calculate the text length change for adjusting existing tokens
            int lengthDelta = tokenText.Length + 1 - (currentPrefix.Length + currentQueryText.Length);

            foreach (var existingToken in tokens.Where(t => t.StartIndex > prefixStartIndex))
                existingToken.StartIndex += lengthDelta;

            tokens.Add(token);

            string newText = before + tokenText + " " + after;

            // Place cursor after the token and trailing space
            int cursorAfterToken = prefixStartIndex + tokenText.Length + 1;
            editor.Text = newText;
            Text = newText;
            editor.CursorPosition = Math.Min(cursorAfterToken, newText.Length);
            TextFormatter.ResetNativeText(editor, newText, Math.Min(cursorAfterToken, newText.Length));
            isUpdatingText = false;

            ScheduleFormatting();
        }

        HideSuggestions();
        suggestionListView.SelectedItem = null;
    }

    private void ScheduleFormatting()
    {
        Dispatcher.Dispatch(() =>
        {
            isUpdatingText = true;
            TextFormatter.ApplyFormatting(editor, tokens);
            isUpdatingText = false;
        });
    }

    private void RecalculateTokenPositions(string text)
    {
        int searchStart = 0;
        var validTokens = new List<SuggestionToken>();

        foreach (var token in tokens.OrderBy(token => token.StartIndex))
        {
            int index = text.IndexOf(token.FullText, searchStart, StringComparison.Ordinal);
            if (index >= 0)
            {
                token.StartIndex = index;
                searchStart = index + token.Length;
                validTokens.Add(token);
            }
        }

        tokens.Clear();
        tokens.AddRange(validTokens);
    }

    private void HideSuggestions()
    {
        suggestionPopup.IsVisible = false;
        currentPrefix = string.Empty;
        currentQueryText = string.Empty;
        prefixStartIndex = -1;
    }

    private static (int position, int count) FindDeletionRegion(string oldText, string newText)
    {
        int prefixLength = 0;
        int minLength = Math.Min(oldText.Length, newText.Length);

        while (prefixLength < minLength && oldText[prefixLength] == newText[prefixLength])
            prefixLength++;

        int suffixLength = 0;
        while (suffixLength < minLength - prefixLength
            && oldText[oldText.Length - 1 - suffixLength] == newText[newText.Length - 1 - suffixLength])
            suffixLength++;

        return (prefixLength, oldText.Length - prefixLength - suffixLength);
    }

    private static (int position, int oldLength, int newLength) FindEditRegion(string oldText, string newText)
    {
        int prefixLength = 0;
        int minLength = Math.Min(oldText.Length, newText.Length);

        while (prefixLength < minLength && oldText[prefixLength] == newText[prefixLength])
            prefixLength++;

        int suffixLength = 0;
        while (suffixLength < minLength - prefixLength
            && oldText[oldText.Length - 1 - suffixLength] == newText[newText.Length - 1 - suffixLength])
            suffixLength++;

        return (prefixLength, oldText.Length - prefixLength - suffixLength, newText.Length - prefixLength - suffixLength);
    }

    private static void OnItemsSourceChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not SuggestingBox suggestingBox) return;

        IEnumerable itemsSource = newValue as IEnumerable;
        if (itemsSource is not null and not IList)
            itemsSource = itemsSource.Cast<object>().ToList();

        suggestingBox.suggestionListView.ItemsSource = itemsSource;
        suggestingBox.UpdateSuggestionHeight(itemsSource);
    }

    private void UpdateSuggestionHeight(IEnumerable itemsSource)
    {
        int itemCount = itemsSource is ICollection collection ? collection.Count : 0;
        if (itemCount == 0)
        {
            suggestionListView.HeightRequest = 0;
            return;
        }

        if (measuredItemHeight > 0)
        {
            suggestionListView.HeightRequest = Math.Min(itemCount * measuredItemHeight, MaxSuggestionHeight);
            return;
        }

        // First measurement: set to max height so all items are rendered, then measure actual content height
        suggestionListView.HeightRequest = MaxSuggestionHeight;

        // Subscribe to SizeChanged — fires after the CollectionView is fully rendered (more reliable than Dispatch)
        suggestionListView.SizeChanged += OnSuggestionListSizeChangedForMeasurement;
    }

    private void OnSuggestionListSizeChangedForMeasurement(object sender, EventArgs e)
    {
        suggestionListView.SizeChanged -= OnSuggestionListSizeChangedForMeasurement;

        IEnumerable source = suggestionListView.ItemsSource;
        int itemCount = source is ICollection collection ? collection.Count : 0;
        if (itemCount == 0) return;

        double contentHeight = TextFormatter.GetNativeContentHeight(suggestionListView);
        if (contentHeight > 0)
        {
            measuredItemHeight = contentHeight / itemCount;
            suggestionListView.HeightRequest = Math.Min(contentHeight, MaxSuggestionHeight);
        }
    }

    private static void OnItemTemplateChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not SuggestingBox suggestingBox) return;
        suggestingBox.suggestionListView.ItemTemplate = newValue as DataTemplate;
        suggestingBox.measuredItemHeight = 0;
    }

    private static void OnTextPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not SuggestingBox suggestingBox || suggestingBox.isUpdatingText) return;

        suggestingBox.isUpdatingText = true;
        suggestingBox.editor.Text = newValue as string ?? string.Empty;
        suggestingBox.isUpdatingText = false;
    }

    private static void OnPlaceholderPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not SuggestingBox suggestingBox) return;
        suggestingBox.editor.Placeholder = newValue as string ?? string.Empty;
    }

    private static void OnSuggestionHeightPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not SuggestingBox suggestingBox) return;
        suggestingBox.UpdateSuggestionHeight(suggestingBox.suggestionListView.ItemsSource);
    }
}
