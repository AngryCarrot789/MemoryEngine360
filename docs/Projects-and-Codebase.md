# MemoryEngine360 Project Structure
MemEngine360 contains 5 main projects:
- `PFXToolKitUI`: The base core project for the PFX toolkit. This project contains lots of useful utilities and systems for a basic application (e.g. persistent configurations, command system and more)
- `PFXToolKitUI.Avalonia`: The base UI project for the PFX toolkit. This and the above project contain nothing related to MemEngine360 and are basically just an extension of Avalonia with my own utilities.
- `MemEngine360`: This is the 'core' project or the backend of MemEngine360, and contains the entire MemEngine360 API
- `MemEngine360.BaseFrontEnd`: This project references avalonia libraries, and contains some of the MemEngine360 UI
  components (such as binding utilities, model->control bi-dictionaries, and lots more).
  The reason for the base front end project is so that plugins may access and interact directly with avalonia,
  while also supporting the 'core plugins' feature
- `MemEngine360.Avalonia`: The UI entry point project. This implements some of the core UI things and actually runs the application.

  As time goes on, this project should ideally get smaller to a point, since we want to move as much over to the base front end to maximize
  what plugins can do. Core plugins cannot reference this project due to cyclic dependency problems.

## Control model Connection and Disconnection

A lot of controls contain methods along the lines of `OnAdding`, `OnAdded`, `OnRemoving`, `OnRemoved`, and
for 'content' of those controls, they might just have `OnConnected` and `OnDisconnected`
(as in, connect to and from the model). This gives a simpler way to add/remove event handlers,
query data from the models, etc.

The order is something along the line of this *(Copied from FramePFX docs)*:

- A model object containing a list (e.g. Track) adds a new item, (e.g. new Clip)
- View receives some sort of ItemAdded event, creates a new control (or retrieves a recycled item)
- `OnAdding` is called on the control, passing in the model and sometimes the owner control.
- The new control is added to the parent's internal items list
- The control's styling and template may be explicitly applied (because avalonia does not apply them until they are measured, so doing this lets us access template controls in OnAdded)
- `OnAdded` is called on the control. It might 'connect' (via binding utilities CLR event handlers) data between the model and UI. The data parameter system makes this process easier with built-in helpers.

And then later on maybe,

- *Clip is removed from the track*
- View receives some sort of ItemRemoved event
- `OnRemoving` is invoked on the clip control, it might unregister events handlers.
- Clip control is removed from the owner track control
- `OnRemoved` is invoked. The clip control clears references to the model and owner track control.
  The control may then be appended to a list of recycled controls, since creating new control instances can be expensive

## Core plugins

These are more like 'modules' of the application. They are projects referenced by the main project and are loaded before dynamic plugins.

## Dynamic/Assembly plugins

These are plugins that are completely dynamically loaded. This system is still WIP, but they can do everything a core plugin can do.   
Assembly loading doesn't use an `AssemblyLoadContext`, so there's versioning issues to deal with

## Supported and unsupported features overview

So far, plugins are able to do some simply things, such as:

- Custom configuration pages and files (using built in persistent storage system)
- Custom data parameters and property editor slots (aka rows)
- Custom commands
- Custom console types
- Custom dialogs and windows
- Adding context menu entries to UIs

... And a few others. There's still lots that cannot be done yet. A few of the main things are:

- Custom panels in the UI

# Plugin lifetime

This is the order of API methods that are called into a plugin:

- `OnCreated`: Invoked just after the constructor is invoked and the properties (Descriptor, PluginLoader and folder) are set.
- `RegisterCommands`: Register your commands here.
- `RegisterServices`: Register your services here
- `RegisterConfigurations`: Register your persistent configs
- `GetXamlResources`: Add the path of your .axaml files (relative to your plugin project) that should be loaded and added to the application's `MergedDictionaries` list
- `OnApplicationLoaded`: Invoked once all application states are ready. No editor window will be open at this point.
- `OnApplicationExiting`: Invoked once the application is about to exit. This is called before persistent configs are saved, so here is where you can synchronize them with the application if you're not already doing it dynamically

Remarks on `OnApplicationLoaded`: This is where you can register application event handlers, your UI controls, custom clips, custom resources, exporters, context menu entries, property editor slot controls, and lots more.
This method is async so there is no rush to do things quickly here.

# MemEngine360 API

This section describes the main APIs of MemEngine360. This is still being updated

## Property Editor

The property editor system is a highly customisable framework, which unfortunately also makes it somewhat complicated to use.

The main power of the custom property editor system is supporting multiple objects being modified at once. We call these "Handlers".

Say for example you want to edit a `double` property in multiple objects, and:

- The property has a fixed value range (e.g. 0.0 to 1.0)
- You want to post-process the displayed value string (e.g. display 0.0 to 1.0 as a percentage: 0% to 100.0%)
- You also want to support manual value entry and support converting, say, 95% back to a raw value of 0.95 for the property
- Change the value increments (aka step profile) of the NumberDragger based on if shift, ctrl or both are pressed.

You can do all of this on a single line... sort of! There's lots of built-in classes which make this doable on a single line.

The property editor consists of rows that we call "Slots". A slot typically consists of a left, spacing, and right column; left for the display name, and right for the slot content (e.g. a slider, NumberDragger or text box). The column sizes are bound to the `PropertyEditorControl.Column[0,1,2]Width` properties.

For example, this is the property editor slot used to modify the `Line Spacing` of a text clip, which is also animatable:

```csharp  
new DataParameterNumberPropertyEditorSlot<float>(// Built-in slot which manages float parameters  
    MyObject.MyFloatParameter,                  // The specific parameter we want to modify
    typeof(MyObject),                           // The type of object the slot is applicable to, typically the Owner of the parameter 
    "My awesome float",                         // A readable display name string, on the left column 
    DragStepProfile.Pixels)                     // The NumberDragger's increment behaviour. Pixels is fine tuned
{   
    // Converts, say, "25.3" into "25.3 px", and supports converting back too "25.3"  
    ValueFormatter = SuffixValueFormatter.StandardPixels 
});
```  

You can register your own custom property editor slot UI controls via the registry object `BasePropertyEditorSlotControl.Registry`, like so:

```csharp  
// The model/control naming convention isn't strict, it's just good practice
Registry.RegisterType<MyCustomPropertyEditorSlot>(  
    // ModelControlRegistry<TModel, TControl> uses factory methods 
    () => new MyCustomPropertyEditorSlotControl()
);  
```

Slot controls may be recycled, so it's important to override the `OnConnected` and `OnDisconnected` methods and add/remove event handlers or bindings accordingly to and from the page model.

A lot of the system is documented in code, so for specific things like slot selection and multiple   
handlers with differing values, I recommend reading the source code to get a better understanding

To specify the handlers that a slot can modify, you call `SetHandlers(IReadOnlyList<object>)` on the slot, and, if the slot supports the number of
handlers and also the underlying types of the handler object(s), then it sets `IsCurrentlyApplicable` to true, and the slot can work as normal,
and the `Handlers` property is updated with the objects that can be modified.  
`ClearHandlers` will obviously clear the handlers and makes the slot no longer applicable.

`SetHandlers` is a recursive method, so it only needs to get called on the root object that you want to set the handlers of. `PropertyEditor` contains the 'root' slot which is what you can call `SetHandlers` on.
However, the main property editor in the UI has 2 sub-root slots, one for clips and one for tracks. Therefore, `SetHandlers` should not be invoked on the root of the `VideoEditorPropertyEditor` object.

Should you wish to add your own slot to the main UI, maybe your plugin adds a 2nd window and you want selected objects in that window to be reflected
in the main UI's property editor, you should add that slot to the `VideoEditorPropertyEditor`'s root, and managed the handlers of the slot you added.

## Data Parameters

MemEngine360 has a data parameter system. The base class is `DataParameter`, but you shouldn't override this class directly, instead override `DataParameter<T>` if you need your own type. These parameters are used to make interfacing with a `PropertyEditor` or binding to the UI in code-behind generally easier; it saves having to use reflection everywhere by instead going through a `ValueAccessor<T>`.

## Configuration pages

Configuration pages are the standard way of modifying application or project properties.

Page models are implemented via the `ConfigurationPage` class. This base class contains the active `ConfigurationContext` which is an object created whenever the settings dialog is opened and is used to track modified pages and the active page (currently being viewed).

The methods available are:

- `ValueTask OnContextCreated(ConfigurationContext)`: This is invoked recursively for every page in a configuration manager when a settings dialog is opened.
  This is where you can load data from the application or project into the state of the page, and also register event handlers for data changes, if you need to.
- `ValueTask OnContextDestroyed(ConfigurationContext)`: This is invoked recursively for every page in a configuration manager when a settings dialog is closed.
  You should unregister event handlers in this method

- `ValueTask Apply(List<ApplyChangesFailureEntry>)`: Apply changes back into the application or project.
  The provided list is not fully implemented yet, however, it should be used instead of showing message dialogs, since it might annoy the user if there's 100s of errors that occur. So instead, all errors will be shown at once in a custom dialog using the `ApplyChangesFailureEntry` objects as the models.

- `void OnActiveContextChanged(ConfigurationContext, ConfigurationContext)`: This is invoked when the viewed configuration page changes. `newContext` is null when this page is no longer visible, and is non-null when this page is now being viewed. You may wish to implement the loading data behaviour in this method instead of `OnContextCreated` to help with performance.
  But beware, this method isn't async, since it is invoked during a UI input event (the tree node being clicked), so don't do anything too slow here

### Application configuration pages

There's a singleton configuration page for the entire application stored in `ApplicationConfigurationManager.Instance`. You can add your own configuration entries and pages in your plugin's `OnApplicationLoading` method

### Page controls

The simplest way to create a configuration page would be to derive from `PropertyEditorConfigurationPage` and use its property editor

But if you wish to implement a completely custom configuration page control (either XAML or declarative if you so please), you can register a mapping via the `ConfigurationPageRegistry.Registry`, like so:

```csharp
Registry.RegisterType<MyConfigurationPage>(
    () => new MyConfigurationPageControl()
);
```

By doing this, you allow the UI to create your control when it tries to present your page. Page controls may be recycled, so it's important to override the `OnConnected` and `OnDisconnected` methods and add/remove event handlers or bindings accordingly to and from the page model.

## Persistent configurations (aka config files)

MemEngine360 has a built-in system for loading and saving configurations (PSP, or persistent storage system), saving you from having to manage a file path and file IO yourself.

You create a type that derives from `PersistentConfiguration`. Then you can register persistent properties via the `PersistentProperty` class.

For example, say you want to save the location of the editor window:

```csharp
public sealed class EditorWindowConfigurationOptions : PersistentConfiguration {
    // This lets us get/set the instance of this configuration, so that we can update
    // the PosX/PosY properties when the window moves.
    public static EditorWindowConfigurationOptions Instance => Application.Instance.PersistentStorageManager.GetConfiguration<EditorWindowConfigurationOptions>();
    
    // Register the persistent properties.
    public static readonly PersistentProperty<int> PosXProperty = PersistentProperty.RegisterParsable<int, EditorWindowConfigurationOptions>(nameof(PosX), 0, o => o.posX, (o, val) => o.posX = val, false);
    public static readonly PersistentProperty<int> PosYProperty = PersistentProperty.RegisterParsable<int, EditorWindowConfigurationOptions>(nameof(PosY), 0, o => o.posY, (o, val) => o.posY = val, false);
    
    // Value backing fields
    private int posX, posY;
    
    // Get/Set helpers
    public int PosX {
        get => PosXProperty.GetValue(this);
        set => PosXProperty.SetValue(this, value);
    }
    public int PosY {
        get => PosYProperty.GetValue(this);
        set => PosYProperty.SetValue(this, value);
    }
    
    // Value change helpers. There are other ways of adding value change handlers too
    public event PersistentPropertyInstanceValueChangeEventHandler<int>? PosXChanged {
        add => PosXProperty.AddValueChangeHandler(this, value);
        remove => PosXProperty.RemoveValueChangeHandler(this, value);
    }
    
    public event PersistentPropertyInstanceValueChangeEventHandler<int>? PosYChanged {
        add => PosYProperty.AddValueChangeHandler(this, value);
        remove => PosYProperty.RemoveValueChangeHandler(this, value);
    }
    
    public EditorWindowConfigurationOptions() {
        IVideoEditorService.Instance.VideoEditorCreatedOrShown += OnVideoEditorCreatedOrShown;
    }
    
    private void OnVideoEditorCreatedOrShown(IVideoEditorWindow window, bool isbeforeshow) {
        if (!isbeforeshow) { // when false, the window is actually visible
            window.WindowPosition = new SKPointI(this.PosX, this.PosY);
        }
    }
}
```

Then to register the config, override `RegisterConfigurations` in your plugin class.

```csharp
// The manager is the application's PSM, 
// accessible directly via Application.Instance.PersistentStorageManager
public override void RegisterConfigurations(PersistentStorageManager manager) {
    // Register our config.
    //   'editor' is the area. Areas are just files. There can be multiple configs per area.
    //   'windowinfo' is the config name in the area.
    manager.Register(new EditorWindowConfigurationOptions(), "editor", "windowinfo");
}
```

## Custom Commands

You can register custom commands in your plugin's `RegisterCommands` method, like so:

```csharp
public override void RegisterCommands(CommandManager manager) {  
    manager.Register(
        "myplugin.commands.editor.ShowCompTimlineName", 
        new ShowCompTimlineNameCommand()
    );  
}

private class ShowCompTimlineNameCommand : Command {  
    protected override Executability CanExecuteCore(CommandEventArgs e) 
    {  
        if (!DataKeys.TimelineKey.TryGetContext(e.ContextData, out Timeline? timeline)) {  
            // Normally, MenuItem will be invisible, or button will be disabled
            return Executability.Invalid;
        }  
    
        return timeline is CompositionTimeline 
            ? Executability.Valid 				   // Control clickable
            : Executability.ValidButCannotExecute; // Control disabled
    }
    
    protected override async Task ExecuteAsync(CommandEventArgs e) 
    {  
        if (!DataKeys.TimelineKey.TryGetContext(e.ContextData, out Timeline timeline)) 
            return;
        if (!(timeline is CompositionTimeline composition)) 
            return;
    
        await IMessageDialogService.Instance.ShowMessage(
            "hello", $"My resource = '{composition.Resource.DisplayName}'"
        );
    }
}
```

## Brush and Icon API

This section describes the colour brush and icon API. This provides a way for plugins to create icons and use them in different parts of the application without having to ever interact with avalonia bitmaps or images directly.

### Brush Manager

This service provides a mechanism for creating abstract delegates around Avalonia brushes.

- `CreateConstant`: creates the equivalent of `ImmutableSolidColorBrush`
- `CreateConstantLinearGradient`: creates the equivalent of `ImmutableLinearGradientBrush`
- `CreateConstantRadialGradient`: creates the equivalent of `ImmutableRadialGradientBrush`
- `GetStaticThemeBrush`: creates the equivalent of `IImmutableBrush`
- `GetDynamicThemeBrush` is more complicated. It's a subscription based object where the front end subscribes to dynamic changes
  of a brush via the application's `ResourcesChanged` and `ActualThemeVariantChanged` events.
  This allows, for example, an icon to use the standard glyph colour (which is white within dark themes and black within light themes, adjustable of course)

### Icon Manager

Icons are managed via the `IconManager`. This provides a way to creating different types of icons, such as images from the disk, bitmaps, custom geometry (SVG) and so on.
When creating an icon, you provide brushes created by the `BrushManager`.  
and they take brushes created by the .

Icon can be passed to context menu entries and used in toolbar buttons

> Accessing underlying icon pixel data is not currently implemented but is certainly possible; SVG icons for example would have to be rendered first using `RenderTargetBitmap`.

## End

This documentation is still being updated, there's a few things missing.