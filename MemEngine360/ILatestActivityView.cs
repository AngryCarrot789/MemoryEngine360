using PFXToolKitUI.Interactivity.Contexts;

namespace MemEngine360;

/// <summary>
/// An interface for a view that presents "activity" text, typically in a status bar in the bottom left
/// </summary>
public interface ILatestActivityView {
    public static readonly DataKey<ILatestActivityView> DataKey = DataKey<ILatestActivityView>.Create("LatestActivityView");
    
    /// <summary>
    /// Gets or sets the latest activity
    /// </summary>
    string Activity { get; set; }
}