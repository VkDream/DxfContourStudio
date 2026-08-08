#nullable enable

using CommunityToolkit.Mvvm.ComponentModel;

namespace DxfContourStudio.Wpf.ViewModels;

/// <summary>
/// A single row of the layers sidebar: name, entity count and the visibility
/// checkbox. Writes layer visibility directly through the view model so the
/// document view state stays the single source of truth.
/// </summary>
public sealed partial class LayerRowViewModel : ObservableObject
{
    private readonly MainViewModel _owner;

    /// <summary>Layer name as declared by the source.</summary>
    public string Name { get; }

    /// <summary>Number of entities on this layer.</summary>
    public int EntityCount { get; }

    [ObservableProperty]
    private bool _isVisible;

    public LayerRowViewModel(MainViewModel owner, string name, bool isVisible, int entityCount)
    {
        _owner = owner;
        Name = name;
        EntityCount = entityCount;
        _isVisible = isVisible;
    }

    partial void OnIsVisibleChanged(bool value) => _owner.SetLayerVisibility(Name, value);
}