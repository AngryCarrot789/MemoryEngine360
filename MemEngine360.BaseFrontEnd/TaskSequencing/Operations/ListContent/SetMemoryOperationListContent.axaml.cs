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

using Avalonia.Interactivity;
using MemEngine360.BaseFrontEnd.TaskSequencing.DataHandlers;
using MemEngine360.BaseFrontEnd.Utils;
using MemEngine360.Engine.Modes;
using MemEngine360.Sequencing;
using MemEngine360.Sequencing.DataProviders;
using MemEngine360.Sequencing.Operations;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Bindings.ComboBoxes;
using PFXToolKitUI.Services.Messaging;

namespace MemEngine360.BaseFrontEnd.TaskSequencing.Operations.ListContent;

public partial class SetMemoryOperationListContent : BaseOperationListContent {
    private readonly ConstantDataValueHandler constDataValueHandler;

    private readonly IBinder<SetMemoryOperation> addressBinder = new TextBoxToEventPropertyBinder<SetMemoryOperation>(nameof(SetMemoryOperation.AddressChanged), (b) => b.Model.Address.ToString()!, (b, text) => BinderParsingUtils.TryParseAddressEx(text, b, (a, v) => a.Model.Address = v));
    private readonly ComboBoxToEventPropertyEnumBinder<DataType> dataTypeBinder = new ComboBoxToEventPropertyEnumBinder<DataType>(typeof(DataProviderHandler), nameof(DataProviderHandler.DataTypeChanged), (x) => ((DataProviderHandler) x).DataType, (x, y) => ((DataProviderHandler) x).DataType = y);

    private readonly IBinder<SetMemoryOperation> iterateBinder = new TextBoxToEventPropertyBinder<SetMemoryOperation>(nameof(SetMemoryOperation.IterateCountChanged), (b) => b.Model.IterateCount.ToString(), async (b, text) => {
        if (uint.TryParse(text, out uint value)) {
            b.Model.IterateCount = value;
            return true;
        }
        else {
            await IMessageDialogService.Instance.ShowMessage("Invalid value", $"Iterate count is invalid. It must be between 0 and {uint.MaxValue}", defaultButton: MessageBoxResult.OK);
            return false;
        }
    });

    public new SetMemoryOperation? Operation => (SetMemoryOperation?) base.Operation;

    public SetMemoryOperationListContent() {
        this.InitializeComponent();
        this.addressBinder.AttachControl(this.PART_AddressTextBox);
        this.iterateBinder.AttachControl(this.PART_IterateCountTextBox);
        this.constDataValueHandler = new ConstantDataValueHandler(this.PART_ValueTextBox);
    }

    protected override void OnLoaded(RoutedEventArgs e) {
        this.dataTypeBinder.Attach(this.PART_DataTypeComboBox, this.constDataValueHandler);
        base.OnLoaded(e);
    }

    protected override void OnUnloaded(RoutedEventArgs e) {
        this.dataTypeBinder.Detach();
        base.OnUnloaded(e);
    }

    protected override void OnOperationChanged(BaseSequenceOperation? oldOperation, BaseSequenceOperation? newOperation) {
        base.OnOperationChanged(oldOperation, newOperation);
        this.addressBinder.SwitchModel((SetMemoryOperation?) newOperation);
        this.iterateBinder.SwitchModel((SetMemoryOperation?) newOperation);
        if (oldOperation is SetMemoryOperation oldOp) {
            oldOp.DataValueProviderChanged -= this.OnDataValueProviderChanged;
            if (oldOp.DataValueProvider != null)
                this.OnDataValueProviderChanged(oldOp, oldOp.DataValueProvider, null);
        }

        if (newOperation is SetMemoryOperation newOp) {
            newOp.DataValueProviderChanged += this.OnDataValueProviderChanged;
            if (newOp.DataValueProvider != null)
                this.OnDataValueProviderChanged(newOp, null, newOp.DataValueProvider);
        }
    }

    private void OnDataValueProviderChanged(SetMemoryOperation sender, DataValueProvider? oldProvider, DataValueProvider? newProvider) {
        this.PART_ToValueStackPanel.IsVisible = newProvider == null || newProvider is ConstantDataProvider;
        if (oldProvider is ConstantDataProvider) {
            this.constDataValueHandler.Disconnect();
        }

        if (newProvider is ConstantDataProvider const2) {
            this.constDataValueHandler.Connect(const2);
        }
    }
}