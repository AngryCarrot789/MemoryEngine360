// 
// Copyright (c) 2024-2025 REghZy
// 
// This file is part of MemoryEngine360.
// 
// MemoryEngine360 is free software; you can redistribute it and/or
// modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; either
// version 3.0 of the License, or (at your option) any later version.
// 
// MemoryEngine360 is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with MemoryEngine360. If not, see <https://www.gnu.org/licenses/>.
// 

using PFXToolKitUI.Icons;
using PFXToolKitUI.Utils.Collections.Observable;

namespace MemEngine360.Engine;

/// <summary>
/// Allows for registering custom tools with memory engine.
/// </summary>
public sealed class ToolManager {
    public static ToolManager Instance { get; } = new ToolManager();

    private readonly ObservableList<ToolDefinition> myTools;
    
    /// <summary>
    /// Gets a list of tools currently registered.
    /// </summary>
    public ReadOnlyObservableList<ToolDefinition> Tools { get; }

    private ToolManager() {
        this.myTools = new ObservableList<ToolDefinition>();
        this.Tools = new ReadOnlyObservableList<ToolDefinition>(this.myTools);
    }

    /// <summary>
    /// Registers a command tool
    /// </summary>
    /// <param name="commandId">The ID of the command to be executed</param>
    /// <param name="displayName">The readable name of this tool</param>
    /// <param name="description">A description of what this tool is used for, typically used as the tooltip</param>
    /// <param name="icon">An icon to present</param>
    /// <param name="groupPath">
    /// Basically a folder path, except for menu items, where this tool should be added,
    /// separated with the forward slash ('/') character. Each 'folder' is the display name
    /// for another menu item (case-insensitive)
    /// </param>
    /// <returns>The tool definition</returns>
    public CommandToolDefinition RegisterCommandTool(string commandId, string displayName, string? description = null, Icon? icon = null, string? groupPath = null) {
        CommandToolDefinition tool = new CommandToolDefinition(commandId, displayName, description, groupPath, icon);
        this.myTools.Add(tool);
        return tool;
    }
}



public abstract class ToolDefinition {
    /// <summary>
    /// Gets the tool's readable name. Will not be an empty or whitespace-only string
    /// </summary>
    public string DisplayName { get; }
    
    /// <summary>
    /// Gets a readable description of what the tool is used for. May be null, empty, whitespaces or a valid string.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// Gets the group that this tool is defined in
    /// </summary>
    public string? GroupPath { get; }

    /// <summary>
    /// Gets the tool's icon
    /// </summary>
    public Icon? Icon { get; }

    protected internal ToolDefinition(string displayName, string? description, string? groupPath, Icon? icon) {
        this.DisplayName = displayName;
        this.Description = description;
        this.GroupPath = groupPath;
        this.Icon = icon;
    }
}

public sealed class CommandToolDefinition : ToolDefinition {
    public string CommandId { get; }
    
    internal CommandToolDefinition(string commandId, string displayName, string? description, string? groupPath, Icon? icon) : base(displayName, description, groupPath, icon) {
        this.CommandId = commandId;
    }
}