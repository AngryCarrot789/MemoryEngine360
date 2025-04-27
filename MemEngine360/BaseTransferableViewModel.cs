// 
// Copyright (c) 2024-2025 REghZy
// 
// This file is part of MemEngine360.
// 
// MemEngine360 is free software; you can redistribute it and/or
// modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; either
// version 3.0 of the License, or (at your option) any later version.
// 
// MemEngine360 is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with MemEngine360. If not, see <https://www.gnu.org/licenses/>.
// 

using System.ComponentModel;
using System.Runtime.CompilerServices;
using PFXToolKitUI.DataTransfer;

namespace MemEngine360;

/// <summary>
/// A helper class for a view model that also uses the data parameter system, because fuck raw dog MVVM ;)
/// </summary>
public abstract class BaseTransferableViewModel : ITransferableData, INotifyPropertyChanged {
    public TransferableData TransferableData { get; }
    
    public event PropertyChangedEventHandler? PropertyChanged;
    
    public BaseTransferableViewModel() {
        this.TransferableData = new TransferableData(this);
    }

    /// <summary>
    /// Registers a callback on the parameters that fires the <see cref="PropertyChanged"/> event on the owner instance
    /// </summary>
    /// <param name="parameters"></param>
    protected static void RegisterParametersAsObservable(params DataParameter[] parameters) {
        DataParameter.AddMultipleHandlers(RaiseObservablePropertyChanged, parameters);
    }

    /// <summary>
    /// Raises the <see cref="PropertyChanged"/> event using the parameter's name as a property name
    /// </summary>
    /// <param name="parameter">The parameter</param>
    /// <param name="owner">The owner</param>
    protected static void RaiseObservablePropertyChanged(DataParameter parameter, ITransferableData owner) {
        ((BaseTransferableViewModel) owner).RaisePropertyChanged(parameter.Name);
    }
    
    /// <summary>
    /// Raises the <see cref="PropertyChanged"/> event
    /// </summary>
    /// <param name="propertyName">
    /// The property name, or null (listeners may do special things
    /// when this is null, or they might ignore the event)
    /// </param>
    protected void RaisePropertyChanged([CallerMemberName] string? propertyName = null) {
        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}