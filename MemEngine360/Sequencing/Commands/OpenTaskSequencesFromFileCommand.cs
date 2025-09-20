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

using System.Xml;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.FilePicking;
using PFXToolKitUI.Tasks;
using PFXToolKitUI.Utils;

namespace MemEngine360.Sequencing.Commands;
public class OpenTaskSequencesFromFileCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        return TaskSequenceManager.DataKey.IsPresent(e.ContextData) ? Executability.Valid : Executability.Invalid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!TaskSequenceManager.DataKey.TryGetContext(e.ContextData, out TaskSequenceManager? manager)) {
            return;
        }

        string? filePath = await IFilePickDialogService.Instance.OpenFile("Open a file containing task sequences", Filters.XmlAndAll);
        if (filePath == null) {
            return;
        }

        Result<XmlDocument> result = await ActivityManager.Instance.RunTask(() => {
            IActivityProgress progress = ActivityManager.Instance.CurrentTask.Progress;
            progress.IsIndeterminate = true;
            progress.Text = "Reading document...";
            XmlDocument document = new XmlDocument();
            document.Load(filePath);
            return Task.FromResult(document);
        });
        
        if (result.Exception != null) {
            await LogExceptionHelper.ShowMessageAndPrintToLogs("Error reading document", result.Exception.Message, result.Exception);
            return;
        }

        Result<List<TaskSequence>> listResult = result.Map(XmlTaskSequenceSerialization.DeserializeDocument);
        if (listResult.Exception != null) {
            await LogExceptionHelper.ShowMessageAndPrintToLogs("Error parsing task sequences", listResult.Exception.Message, listResult.Exception);
            return;
        }
        
        manager.Sequences.AddRange(listResult.Value);
    }
}