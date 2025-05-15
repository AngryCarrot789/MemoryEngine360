# Models and Binding
> Some dogshit philosophy I suppose, skip if you want. It just explains why I don't use MVVM in this program, probably in too much detail

MemEngine360 uses very little MVVM. I don't enjoy writing apps with MVVM primarily because it strips away so much freedom to do cool things. 

All you really have control over is the final value from a converter, and also string format too. And I suppose you could just make a delegate property, e.g. MemoryAddressAsText instead of using `StringFormat=X8` with MemoryAddress

Say for example you want to "Bind" a slider's value to a VM property. But in changing the VM's property updates some text block and triggers expensive operations (maybe later using the dispatcher), so now the app is lagging like crazy when you drag it. 

To solve this, you'd disable the expensive operation when a slider is clicked (while still allowing the text block to update), and re-enable the operation when the click is released and maybe trigger it at the same time (if the value changed).

To achieve this with MVVM means either using code-behind and directly changing the VM property, or, create a custom slider class (or an extension of it) that contains two properties "ValueDuringDrag" and "ValueAfterDragCompleted" and bind them to two properties in your VM (e.g. MemoryAddressForDrag, MemoryAddressForDragComplete), and you'll probably need a 3rd property such as MemoryAddress to unify the first two. TLDR; mvvm makes this a nightmare. But maybe you don't use sliders?

Another example: A text box that allows parsing the text as either a normal integer or as hex, based on if the text is prefixed with "0x". Or maybe just a text box for showing and changing an integer value.

In both of these cases, when the value is malformed (e.g. not hex nor integer, or not prefixed with "0x" but using hex digits) and you click the enter key, you want to show a dialog specifically containing the error. And clicking Escape resets the values.  There isn't really a way to do this with binding at all (I forgot if avalonia has the binding update on lost focus?). The most you can do is show a validation error when binding to an integer property. And I suppose you could use a converter to convert back from text to integer based on the prefix.

TLDR; doing fun things requires code-behind sometimes, and in some cases, isn't possible without custom controls. So why not just design everything to force code behind?  

By forcing code behind, we don't waste 5 hours wondering how to do this in MVVM when it takes 5 minutes in code behind. Yeah it takes longer for simple things, e.g. a TextBlock's text bound to some string property in a model, but we have full control over that text block now, whereas we would have spent 10 minutes writing the converter classes and multi-bindings for the text styles, weights, etc. 

# Actually Models and Binding
MemEngine360 doesn't really use any particular architecture. The closest would be MVC, except the views themselves are controllers. So perhaps the Code-Behind Architecture?

Models contain the properties and fire events when a property changes. When an event fires, it may mean different similar properties change (e.g. TaskState changes, so the result of IsStateCompleted, IsStateCancelled, etc. is different)

The views add/remove event handlers on the models, and update the appropriate controls.

## Binding helpers
I created the `IBinder` framework, found in the namespace `PFXToolKitUI.Avalonia.Bindings`.

This framework aims to massively simplify binding model values to controls, and there's a lot of different binders for different scenarios.
Here are some of the common ones

### AvaloniaPropertyToEventPropertyBinder
The most common binder used. Binds an avalonia property to a CLR property. The name of an event is specified in the constructor, and `AutoEventHelper` is used to generate an event handler for it, regardless of the event signature

Uses two `Action<IBinder<TModel>>` called `updateControl` (required) and `updateModel` (optional; maybe you want the control to be read only so it can't change the model)

### EventPropertyBinder
A lightweight version of `AvaloniaPropertyToEventPropertyBinder` that does not listen to avalonia property changes. Uses the same updateControl/updateModel mechanism

### TextBoxToEventPropertyBinder and TextBoxToDataParameterBinder
TextBox specific binders use a `Func<IBinder<TModel>, string>` to get the text from a model, and an async function `Func<IBinder<TModel>, string, Task>` to try and update the model based on the text in the text box, or do something like show the user a dialog containing the problem with the text.

We use this for the Start and Length fields in the Memory Scanning Options panel.

### EventPropertyEnumBinder and DataParameterEnumBinder
These are special radio button binders used to "assign" an enum value to a radio button so that, when that radio button becomes checked,
the model's enum property (or data parameter value if using that version) is set to whatever value was assigned to that radio button.

### AvaloniaPropertyToDataParameterBinder 
Binds an avalonia property to a data parameter. Supports two optional `Func<object?, object?>` to convert to/from the avalonia and data parameter values

### AvaloniaPropertyToEventPropertyGetSetBinder
Similar to `AvaloniaPropertyToEventPropertyBinder` but uses getter/setter functions to get/set the model value  


