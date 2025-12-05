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

using MemEngine360.Engine.StructViewing.ClassBuilding;
using PFXToolKitUI.Interactivity;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Utils.Collections.Observable;

namespace MemEngine360.Engine.StructViewing;

public class StructViewerManager : IUserLocalContext {
    public static StructViewerManager Instance { get; } = new StructViewerManager();

    /// <summary>
    /// Gets the root class.
    /// </summary>
    public ClassType RootClass { get; }

    /// <summary>
    /// Gets the list of registered class types
    /// </summary>
    public ObservableList<ClassType> Classes { get; } = new ObservableList<ClassType>();

    public IMutableContextData UserContext { get; } = new ContextData();
    
    public StructViewerManager() {
        // This system won't work as well as it needs to be. The layout is hard coded, whereas
        // we want to be able to try and resolve the layout dynamically based on an expected structure.
        // For example, An array of items sized based on another address
        
        this.RootClass = new ClassType("_root_");
        this.Classes.Add(new ClassType("BO1 MP Player1") {
            Fields = {
                new FieldElement(0x28, "Pos X", TypeDescriptor.Primitive(PrimitiveTypeDescriptor.Type.Float)),
                new FieldElement(0x2C, "Pos Y", TypeDescriptor.Primitive(PrimitiveTypeDescriptor.Type.Float)),
                new FieldElement(0x30, "Pos Z", TypeDescriptor.Primitive(PrimitiveTypeDescriptor.Type.Float)),
            }
        });
        
        this.RootClass.Fields.Add(new FieldElement(0x8303A610, "Player 1", TypeDescriptor.OfType(this.Classes[0])));
    }
}