# Command System

This system is inspired by IntelliJ IDEA's action system, where you have a CommandManager which contains all registered commands.
You access commands via a string key, and then execute the command by passing in contextual data (stored in a `IContextData`). The context data gets a
value from data keys, and the `ContextData` implementation stores entries in a map which is how UI components store their context data.
The `DataManager` class manages the context data for all UI components (scroll down for more info)

This means that when you for example press F2 while focused on the root window, there won't be much contextual information. 

But if you click it when focused on say a saved address table entry, there's a lot of context information there (window, memory engine, address table manager, address table entry); 
It's context-sensitive, duh

# Shortcut System

An application typically has a global ShortcutManager. Each window/shell that has a value for `UIInputManager.FocusPath` set has a ShortcutProcessor (which is created by the manager) and that handles the input for that specific window; 
essentially it processes the inputs for that target and activates shortcuts. The manager stores a root ShortcutGroup, forming a hierarchy of groups and shortcuts.

### Chained shortcuts
A shortcut could be activated with a single keystroke (e.g. S or CTRL+X), or by a long chain or sequential input strokes (LMB click, CTRL+SHIFT+X, B, Q, WheelUp, ALT+E to finally activate a shortcut)

### Structure
Each group and shortcut have their own identifier Name, and is used by the FullPath, where the groups are separated by a '/' character

The application would create focus groups, which is what would allow advanced context-specific shortcuts to work (e.g. clicking F2 changes what happens based on what part of the app is focused)

ShortcutGroup, GroupedShortcut and GroupedInputState all have a name associated with them, unique relative to their parent (basically like a file system).

### Final stuff
Long story short, the shortcut system figures out a list of shortcuts to "activate" based on the current global focus path, and activates all of them until
one is activated successfully.

Keymap.xml contains the shortcuts (and some unused ones)

We use this system instead of the built in shortcut system so that the user can adjust shortcuts and have them be saved. Although... saving shortcuts isn't fully implemented yet.

## Advanced Context Menu system

So far, all context menus use the `AdvancedContextMenu` class. The menu items have a model-view connection and all the model items are stored in a `ContextRegistry`
which, when linked to a control, will find the `AdvancedContextMenu` instance associated with the registry, or it creates one and then the UI components are generated,
and it sets it as the control's ContextMenu so that standard context behaviour works. There's one menu per registry, to help with performance and memory usage.

Context registries contain a list of group objects, which are name. This gives some control order the ordering of menu items
in cae a plugin wants to access known groups and insert their commands into a suitable location. A better API to simply this process
is being worked on

Entries are the actual menu item models, which are represented as the `IContextObject` interface.
`IContextEntry` is for actual menu items, but things like separators and captions just use IContextGroup

Currently, there's only 2 types of groups:

- Static groups, which contain a list of entry instances.
- Dynamic groups, which contains a generator callback which can create entries based on the available context, and they're injected into the context menu
  at the location of the dynamic group entry (which is a placeholder entry so that the generator system knows where to put the items).

There's also the `ContextCapturingMenu`, which is used as a window's top level menu. This menu captures the fully-inherited IContextData of the control
that was focused just before a menu item was opened in said menu (see below for more info about context data and this fully-inherited behaviour)

# Data Manager

The data manager is used to store local context data in a control, and implement context data inheritance containing the merged context data for all of a control's visual parents and itself.

It has two primary properties: `ContextData`, and `InheritedContextData`. Despite the name, the inherited version does not use Avalonia's built-in property inheritance feature, but instead
uses my own inheritance implementation by using `ContextDataProperty` changes and the `VisualAncestorChanged` event (which I add/remove reflectively when the `ContextDataProperty` changes,
and it fires when an object's visual parent changes). By doing this, an event can be fired (`InheritedContextChangedEvent`) for every single child of an element in its visual tree when its
local context data is changed. The `ContextUsage` class depends on this feature in order to do things like re-query the executability state of a command when a control's full inherited context changes
