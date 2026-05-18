using CoreFoundation;
using Foundation;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using UIKit;

namespace SuggestingBox.Maui;

internal class FormattedEditorHandler : EditorHandler
{
    private NSObject _editingStartedObserver;
    private UIView _defaultInputAccessoryView;
    private bool _hasDefaultInputAccessoryView;

    protected override MauiTextView CreatePlatformView()
        => new PasteAwareTextView();

    protected override void ConnectHandler(MauiTextView platformView)
    {
        base.ConnectHandler(platformView);
        UpdateInputAccessoryView(platformView);

        SubscribeFormattedEditorPropertyChanged();

        void HandleTextDidBeginEditing(NSNotification notification)
        {
            if (notification.Object != platformView) return;

            UpdateInputAccessoryView(platformView);
            DispatchQueue.MainQueue.DispatchAsync(() => UpdateInputAccessoryView(platformView));
        }

        _editingStartedObserver = NSNotificationCenter.DefaultCenter.AddObserver(UITextView.TextDidBeginEditingNotification, HandleTextDidBeginEditing, platformView);
    }

    protected override void DisconnectHandler(MauiTextView platformView)
    {
        UnsubscribeFormattedEditorPropertyChanged();

        if (_editingStartedObserver is not null)
        {
            NSNotificationCenter.DefaultCenter.RemoveObserver(_editingStartedObserver);
            _editingStartedObserver = null;
        }
        _defaultInputAccessoryView = null;
        _hasDefaultInputAccessoryView = false;

        base.DisconnectHandler(platformView);
    }

    private void SubscribeFormattedEditorPropertyChanged()
    {
        if (VirtualView is not FormattedEditor formattedEditor) return;
        formattedEditor.PropertyChanged += OnFormattedEditorPropertyChanged;
    }

    private void UnsubscribeFormattedEditorPropertyChanged()
    {
        if (VirtualView is not FormattedEditor formattedEditor) return;
        formattedEditor.PropertyChanged -= OnFormattedEditorPropertyChanged;
    }

    private void OnFormattedEditorPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(FormattedEditor.DisableInputAccessoryView)) return;
        if (PlatformView is null) return;

        UpdateInputAccessoryView(PlatformView);
    }

    private void UpdateInputAccessoryView(UITextView platformTextView)
    {
        if (VirtualView is not FormattedEditor formattedEditor) return;

        if (formattedEditor.DisableInputAccessoryView)
        {
            if (platformTextView.InputAccessoryView is not null)
            {
                _defaultInputAccessoryView = platformTextView.InputAccessoryView;
                _hasDefaultInputAccessoryView = true;
            }

            platformTextView.InputAccessoryView = null;
            platformTextView.ReloadInputViews();
            return;
        }

        if (!_hasDefaultInputAccessoryView) return;

        platformTextView.InputAccessoryView = _defaultInputAccessoryView;
        platformTextView.ReloadInputViews();
    }
}

internal class PasteAwareTextView : MauiTextView
{
    internal Action<byte[], int> OnImagePasted { get; set; }

    public override void Paste(NSObject sender)
    {
        if (TryHandleImagePaste()) return;
        base.Paste(sender);
    }

    public override void Paste(NSItemProvider[] itemProviders)
    {
        if (TryHandleImagePaste()) return;
        base.Paste(itemProviders);
    }

    private bool TryHandleImagePaste()
    {
        if (OnImagePasted is null) return false;

        int cursorPosition = Math.Max(0, (int)SelectedRange.Location);
        var pasteboard = UIPasteboard.General;
        if (!pasteboard.HasImages) return false;

        var image = pasteboard.Image;
        if (image is null) return false;

        using var pngData = image.AsPNG();
        if (pngData is null || pngData.Length == 0) return false;

        byte[] imageData = new byte[pngData.Length];
        System.Runtime.InteropServices.Marshal.Copy(pngData.Bytes, imageData, 0, (int)pngData.Length);
        OnImagePasted(imageData, cursorPosition);
        return true;
    }
}
