using MemEngine360.Engine;
using MemEngine360.Engine.View;
using PFXToolKitUI.Interactivity.Contexts;

namespace MemEngine360;

/// <summary>
/// Common data keys for memory engine
/// </summary>
public static class CommonKeys {
    /// <summary>
    /// The data key for the memory engine ui's view state
    /// </summary>
    public static readonly DataKey<MemoryEngineViewState> MemoryEngineViewStateDataKey = DataKeys.Create<MemoryEngineViewState>("MemoryEngineViewState");
    
    /// <summary>
    /// The data key for the contextual memory engine
    /// </summary>
    public static readonly DataKey<MemoryEngine> MemoryEngineDataKey = DataKeys.Create<MemoryEngine>("MemoryEngine");
}