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

using MemEngine360.Sequencing;
using PFXToolKitUI.Avalonia.Bindings.TextBoxes;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Utils;

namespace MemEngine360.BaseFrontEnd.TaskSequencing.Operations.EditorContent;

public partial class RandomTriggerEditorContent : BaseOperationEditorContent {
    private readonly TextBoxToEventPropertyBinder<RandomTriggerHelper> chanceBinder = new TextBoxToEventPropertyBinder<RandomTriggerHelper>(nameof(RandomTriggerHelper.ChanceChanged), (b) => b.Model.Chance.ToString(), async (b, str) => {
        if (!NumberUtils.TryParseHexOrRegular(str, out uint value)) {
            await IMessageDialogService.Instance.ShowMessage("Invalid value", $"The chance must be an integer. {Environment.NewLine}E.g. a 5% chance means this value would be 20 (because 1/20 is 0.05, and 100*(1/5) is 20)", defaultButton: MessageBoxResult.OK, icon: MessageBoxIcons.ErrorIcon);
            return false;
        }

        b.Model.Chance = value;
        return true;
    });
    
    private readonly TextBoxToEventPropertyBinder<RandomTriggerHelper> wait4trigBinder = new TextBoxToEventPropertyBinder<RandomTriggerHelper>(nameof(RandomTriggerHelper.WaitForTriggerIntervalChanged), (b) => b.Model.WaitForTriggerInterval is TimeSpan span ? TimeSpanUtils.ConvertToString(span) : "", async (b, str) => {
        if (string.IsNullOrEmpty(str)) {
            b.Model.WaitForTriggerInterval = null;
            return true;
        }

        if (!TimeSpanUtils.TryParseTime(str, out TimeSpan span, out string? errMsg)) {
            await IMessageDialogService.Instance.ShowMessage("Invalid time", errMsg, defaultButton: MessageBoxResult.OK, icon: MessageBoxIcons.ErrorIcon);
            return false;
        }
        
        if (TimeSpanUtils.IsOutOfRangeForDelay(span, out errMsg)) {
            await IMessageDialogService.Instance.ShowMessage("Delay out of range", errMsg, defaultButton: MessageBoxResult.OK, icon: MessageBoxIcons.ErrorIcon);
            return false;
        }
        
        b.Model.WaitForTriggerInterval = span;
        return true;
    });
    
    private readonly TextBoxToEventPropertyBinder<RandomTriggerHelper> minTriesBinder = new TextBoxToEventPropertyBinder<RandomTriggerHelper>(nameof(RandomTriggerHelper.MinimumTriesToTriggerChanged), (b) => b.Model.MinimumTriesToTrigger.ToString(), async (b, str) => {
        if (!uint.TryParse(str, out uint value)) {
            await IMessageDialogService.Instance.ShowMessage("Invalid value", $"The value must be an integer between {uint.MinValue} and {uint.MaxValue}.", defaultButton: MessageBoxResult.OK, icon: MessageBoxIcons.ErrorIcon);
            return false;
        }

        b.Model.MinimumTriesToTrigger = value;
        return true;
    });

    public override string Caption => "Random Trigger";

    public RandomTriggerEditorContent() {
        this.InitializeComponent();
        
        this.chanceBinder.AttachControl(this.PART_ChanceTextBox);
        this.wait4trigBinder.AttachControl(this.PART_WaitTextBox);
        this.minTriesBinder.AttachControl(this.PART_MinimumTriesTextBox);
    }

    protected override void OnOperationChanged(BaseSequenceOperation? oldOperation, BaseSequenceOperation? newOperation) {
        base.OnOperationChanged(oldOperation, newOperation);
        
        this.chanceBinder.SwitchModel(newOperation?.RandomTriggerHelper);
        this.wait4trigBinder.SwitchModel(newOperation?.RandomTriggerHelper);
        this.minTriesBinder.SwitchModel(newOperation?.RandomTriggerHelper);
    }
}