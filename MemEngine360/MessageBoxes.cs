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

using MemEngine360.Commands;
using PFXToolKitUI.Services.Messaging;

namespace MemEngine360;

/// <summary>
/// A class that stores reusable message box templates
/// </summary>
public static class MessageBoxes {
    public static readonly MessageBoxTemplate Dummy = new MessageBoxTemplate("Caption", "Header here", "Message body here", MessageBoxButtons.OKCancel, MessageBoxResult.OK, icon: MessageBoxIcons.WarningIcon, persistentDialogName: "dialog.dummytemplated");
    public static readonly MessageBoxTemplate AlreadyConnectedToConsole = new MessageBoxTemplate("Already Connected", "Already connected to a console. Close existing connection first?", MessageBoxButtons.OKCancel, MessageBoxResult.OK, persistentDialogName: OpenConsoleConnectionDialogCommand.AlreadyOpenDialogName);
}