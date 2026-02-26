namespace SuggestingBox.Maui;

public delegate void SuggestingBoxEventHandler<in TArgs>(SuggestingBox sender, TArgs args) where TArgs : EventArgs;
