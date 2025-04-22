using PFXToolKitUI.Interactivity;

namespace MemEngine360.Engine;

public interface IResultListSelectionManager<TModel> : ISelectionManager<TModel>, ILightSelectionManager<TModel>, ISelectAllManager, IListBasedSelectionManager<TModel> {
    
}