using System;
using System.Collections.ObjectModel;
using System.Linq;
using MemEngine360.Engine.Modes;

namespace MemEngine360.Avalonia;

public static class CommonCollections {
    public static ReadOnlyCollection<DataType> DataTypes { get; } = Enum.GetValues(typeof(DataType)).Cast<DataType>().ToList().AsReadOnly();
}