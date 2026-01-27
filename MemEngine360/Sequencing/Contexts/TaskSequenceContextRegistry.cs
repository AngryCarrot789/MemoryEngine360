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

using MemEngine360.Sequencing.View;
using PFXToolKitUI;
using PFXToolKitUI.AdvancedMenuService;
using PFXToolKitUI.Utils;

namespace MemEngine360.Sequencing.Contexts;

public static class TaskSequenceContextRegistry {
    public static readonly ContextRegistry Registry = new ContextRegistry("Task Sequence");

    private static BaseMenuEntry CmdRunSequenceCommand {
        get {
            return field ??= new CommandMenuEntry("commands.sequencer.RunSequenceCommand", "Run") {
                Icon = StandardIcons.SmallContinueActivityIconColourful, DisabledIcon = StandardIcons.SmallContinueActivityIconDisabled
            }.AddCanExecuteChangeUpdaterForEvent(TaskSequence.DataKey, nameof(TaskSequence.IsRunningChanged));
        }
    }

    private static BaseMenuEntry CmdStopSpecificSequenceCommand {
        get {
            return field ??= new CommandMenuEntry("commands.sequencer.StopSpecificSequenceCommand", "Stop") {
                Icon = StandardIcons.StopIconColourful,
                DisabledIcon = StandardIcons.StopIconDisabled
            }.AddCanExecuteChangeUpdaterForEvent(TaskSequence.DataKey, nameof(TaskSequence.IsRunningChanged));
        }
    }

    private static BaseMenuEntry StopSelectedSequencesCommand {
        get {
            if (field != null)
                return field;

            BaseMenuEntry entry = new CommandMenuEntry("commands.sequencer.StopSelectedSequencesCommand", "Stop All") {
                Icon = StandardIcons.StopIconColourful,
                DisabledIcon = StandardIcons.StopIconDisabled
            };

            // NotifyCollectionChangedEventHandler handler = (o, args) => entry.RaiseCanExecuteChanged();
            // entry.AddContextChangedHandler(TaskSequenceManagerViewState.DataKey, (sender, e) => {
            //     if (e.OldValue != null)
            //         e.OldValue.ActiveSequences.CollectionChanged -= handler;
            //     if (e.NewValue != null)
            //         e.NewValue.ActiveSequences.CollectionChanged += handler;
            //     entry.RaiseCanExecuteChanged();
            // });

            return field = entry;
        }
    }

    static TaskSequenceContextRegistry() {
        Registry.Opened += static (_, context) => {
            if (TaskSequenceManagerViewState.DataKey.TryGetContext(context, out TaskSequenceManagerViewState? manager)) {
                if (manager.SelectedSequences.Count > 0) {
                    if (manager.SelectedSequences.Count == 1) {
                        string first = manager.SelectedSequences.First.DisplayName;
                        Registry.ObjectName = string.IsNullOrWhiteSpace(first) ? "(unnamed sequence)" : first;
                    }
                    else {
                        Registry.ObjectName = $"{manager.SelectedSequences.Count} sequence{Lang.S(manager.SelectedSequences.Count)}";
                    }

                    return;
                }
            }

            Registry.ObjectName = null;
        };

        FixedWeightedMenuEntryGroup edit = Registry.GetFixedGroup("general");
        edit.AddHeader("Edit");
        edit.AddDynamicSubGroup((group, ctx, items) => {
            if (TaskSequenceManagerViewState.DataKey.TryGetContext(ctx, out TaskSequenceManagerViewState? ui)) {
                if (ui.PrimarySelectedSequence != null)
                    items.Add(new CommandMenuEntry("commands.sequencer.RenameSequenceCommand", "Rename", icon: StandardIcons.ABCTextIcon));
            }
        });

        edit.AddCommand("commands.sequencer.DuplicateSequenceCommand", "Duplicate");

        FixedWeightedMenuEntryGroup actions = Registry.GetFixedGroup("operations");
        actions.AddHeader("General");
        actions.AddEntry(CmdRunSequenceCommand!);
        actions.AddDynamicSubGroup((group, ctx, items) => {
            if (TaskSequenceManagerViewState.DataKey.TryGetContext(ctx, out TaskSequenceManagerViewState? ui)) {
                items.Add(CmdStopSpecificSequenceCommand);
                if (ui.SelectedSequences.Count > 1) {
                    items.Add(new SeparatorEntry());
                    items.Add(StopSelectedSequencesCommand);
                }
            }
        });

        actions.AddCommand("commands.sequencer.ConnectToDedicatedConsoleCommand", "Connect to console...", "Connect using a dedicated connection instead of using the engine's connection", icon: SimpleIcons.ConnectToConsoleDedicatedIcon);

        FixedWeightedMenuEntryGroup destruction = Registry.GetFixedGroup("destruction");

        // Hook onto context changed instead of using dynamic context entries, because they're slightly expensive and
        // also sometimes buggy with no hope of being truly fixed because i'm not smart enough to figure it out.
        // Check out the method AdvancedMenuService.RemoveItemNodesWithDynamicSupport for nightmare fuel
        destruction.AddCommand("commands.sequencer.DeleteSequenceSelectionCommand", "Delete Sequence(s)").
                    AddContextUpdateHandler(TaskSequenceManagerViewState.DataKey, (sender, e) => {
                        sender.DisplayName =
                            e.Value != null && e.Value.SelectedSequences.Count == 1
                                ? "Delete Sequence"
                                : "Delete Sequences";
                    });
    }
}