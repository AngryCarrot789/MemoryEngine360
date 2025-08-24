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
using PFXToolKitUI.Logging;
using PFXToolKitUI.Services.FilePicking;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Tasks;
using PFXToolKitUI.Utils;

namespace MemEngine360.Sequencing.Commands;

public class LoadTaskSequencesCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        return ITaskSequencerUI.DataKey.GetExecutabilityForPresence(e.ContextData);
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!ITaskSequencerUI.DataKey.TryGetContext(e.ContextData, out ITaskSequencerUI? ui)) {
            return;
        }

        string? filePath = await IFilePickDialogService.Instance.OpenFile("Open a file containing task sequences", Filters.XmlAndAll);
        if (filePath == null) {
            return;
        }
        
        ActivityTask<XmlDocument> task = ActivityManager.Instance.RunTask(() => {
            ActivityManager.Instance.GetCurrentProgressOrEmpty().IsIndeterminate = true;
            XmlDocument document = new XmlDocument();
            document.Load(filePath);
            return Task.FromResult(document);
        });

        XmlDocument? doc = await task;
        if (doc == null) {
            if (task.Exception != null) {
                AppLogger.Instance.WriteLine("Error deserializing XML document");
                AppLogger.Instance.WriteLine(task.Exception.GetToString());
                await IMessageDialogService.Instance.ShowMessage("Error deserializing XML document", task.Exception.Message);
            }
            
            return;
        }

        List<TaskSequence> list;
        try {
            list = XmlTaskSequenceSerialization.DeserializeDocument(doc);
        }
        catch (Exception ex) {
            AppLogger.Instance.WriteLine("Error deserializing XML document");
            AppLogger.Instance.WriteLine(ex.GetToString());
            await IMessageDialogService.Instance.ShowMessage("Error deserializing XML document", ex.Message);
            return;
        }

        foreach (TaskSequence seq in list) {
            ui.Manager.Sequences.Add(seq);
        }
    }
}