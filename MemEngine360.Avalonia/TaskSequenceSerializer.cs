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

using System;
using System.Linq;
using System.Xml;
using MemEngine360.Engine.Addressing;
using MemEngine360.Engine.Modes;
using MemEngine360.Sequencing;
using MemEngine360.Sequencing.Conditions;
using MemEngine360.Sequencing.DataProviders;
using MemEngine360.Sequencing.Operations;
using MemEngine360.ValueAbstraction;
using PFXToolKitUI.Utils;

namespace MemEngine360.Avalonia;

public static class TaskSequenceSerializer {
    public static void RegisterTaskSequencerSerialization() {
        XmlTaskSequenceSerialization.RegisterCondition("CompareMemory", typeof(CompareMemoryCondition), (document, element, _cond) => {
            CompareMemoryCondition condition = (CompareMemoryCondition) _cond;
            element.SetAttribute("Address", condition.Address.ToString());
            element.SetAttribute("CompareType", condition.CompareType.ToString());
            element.SetAttribute("ParseIntInputAsHex", condition.ParseIntAsHex ? "true" : "false");
            if (condition.CompareTo != null) {
                XmlTaskSequenceSerialization.SerializeDataValue((XmlElement) element.AppendChild(document.CreateElement("CompareTo"))!, condition.CompareTo);
            }
        }, (element, _cond) => {
            CompareMemoryCondition condition = (CompareMemoryCondition) _cond;
            if (!MemoryAddressUtils.TryParse(element.GetAttribute("Address"), out IMemoryAddress? address, out string? error))
                throw new Exception("Invalid address' " + element.GetAttribute("Address") + "': " + error!);
            if (!Enum.TryParse(element.GetAttribute("CompareType"), true, out CompareType compareType))
                throw new Exception("Invalid CompareType: " + element.GetAttribute("CompareType"));
            if (!bool.TryParse(element.GetAttribute("ParseIntInputAsHex"), out bool parseIntAsHex))
                throw new Exception("Invalid bool for ParseIntInputAsHex: " + element.GetAttribute("ParseIntInputAsHex"));

            condition.Address = address;
            condition.CompareType = compareType;
            condition.ParseIntAsHex = parseIntAsHex;
            if (element.GetElementsByTagName("CompareTo").OfType<XmlElement>().FirstOrDefault() is XmlElement dataTypeElement)
                condition.CompareTo = XmlTaskSequenceSerialization.DeserializeDataValue(dataTypeElement);
        });

        XmlTaskSequenceSerialization.RegisterOperation("Delay", typeof(DelayOperation), (document, element, _op) => {
            DelayOperation op = (DelayOperation) _op;
            element.SetAttribute("Delay", TimeSpanUtils.ConvertToString(op.Delay));
        }, (element, _op) => {
            DelayOperation op = (DelayOperation) _op;
            if (!TimeSpanUtils.TryParseTime(element.GetAttribute("Delay"), out TimeSpan delay, out string? errorMessage))
                throw new Exception($"Invalid delay value '{element.GetAttribute("Delay")}'. " + errorMessage);
            op.Delay = delay;
        });

        XmlTaskSequenceSerialization.RegisterOperation("JumpTo", typeof(JumpToLabelOperation), (document, element, _op) => {
            JumpToLabelOperation op = (JumpToLabelOperation) _op;
            element.SetAttribute("TargetName", op.CurrentTarget?.LabelName);
        }, (element, _op) => {
            JumpToLabelOperation op = (JumpToLabelOperation) _op;
            string name = element.GetAttribute("TargetName");
            // targets will be updated once all operations are fully deserialized
            op.SetTarget(string.IsNullOrWhiteSpace(name) ? null : name, null);
        });

        XmlTaskSequenceSerialization.RegisterOperation("Label", typeof(LabelOperation), (document, element, _op) => {
            LabelOperation op = (LabelOperation) _op;
            element.SetAttribute("Name", op.LabelName);
        }, (element, _op) => {
            LabelOperation op = (LabelOperation) _op;
            string name = element.GetAttribute("Name");
            op.LabelName = string.IsNullOrWhiteSpace(name) ? null : name;
        });

        XmlTaskSequenceSerialization.RegisterOperation("StopSequence", typeof(StopSequenceOperation), (document, element, _op) => {
        }, (element, _op) => {
        });

        XmlTaskSequenceSerialization.RegisterOperation("SetMemory", typeof(SetMemoryOperation), (document, element, _op) => {
            SetMemoryOperation op = (SetMemoryOperation) _op;
            element.SetAttribute("Address", op.Address.ToString());
            element.SetAttribute("IterateCount", op.IterateCount.ToString());
            element.SetAttribute("WriteMode", op.WriteMode.ToString());
            if (op.DataValueProvider is ConstantDataProvider constProvider) {
                XmlElement providerElement = (XmlElement) element.AppendChild(document.CreateElement("ConstantProvider"))!;
                if (constProvider.DataValue is IDataValue value)
                    XmlTaskSequenceSerialization.SerializeDataValue((XmlElement) providerElement.AppendChild(document.CreateElement("Value"))!, value);
                providerElement.SetAttribute("DataType", constProvider.DataType.ToString());
                providerElement.SetAttribute("ParseIntInputAsHex", constProvider.ParseIntAsHex ? "true" : "false");
                providerElement.SetAttribute("AppendNullCharToString", constProvider.AppendNullCharToString ? "true" : "false");
            }
            else if (op.DataValueProvider is RandomNumberDataProvider randomProvider) {
                XmlElement providerElement = (XmlElement) element.AppendChild(document.CreateElement("RandomProvider"))!;
                if (randomProvider.Minimum is IDataValue minVal)
                    XmlTaskSequenceSerialization.SerializeDataValue((XmlElement) providerElement.AppendChild(document.CreateElement("MinNumber"))!, minVal);
                if (randomProvider.Maximum is IDataValue maxVal)
                    XmlTaskSequenceSerialization.SerializeDataValue((XmlElement) providerElement.AppendChild(document.CreateElement("MaxNumber"))!, maxVal);
                providerElement.SetAttribute("DataType", randomProvider.DataType.ToString());
                providerElement.SetAttribute("ParseIntInputAsHex", randomProvider.ParseIntAsHex ? "true" : "false");
                providerElement.SetAttribute("AppendNullCharToString", randomProvider.AppendNullCharToString ? "true" : "false");
            }
        }, (element, _op) => {
            SetMemoryOperation op = (SetMemoryOperation) _op;
            if (!MemoryAddressUtils.TryParse(GetRequiredAttribute(element, "Address", false), out IMemoryAddress? addr, out string? errMsg))
                throw new Exception("Invalid memory address. " + errMsg);

            op.Address = addr;
            op.IterateCount = GetOptionalAttribute(element, "IterateCount", uint.Parse, op.IterateCount);
            op.WriteMode = GetOptionalAttribute(element, "WriteMode", s => Enum.Parse<SetMemoryWriteMode>(s, true), op.WriteMode);

            if (element.GetElementsByTagName("ConstantProvider").OfType<XmlElement>().FirstOrDefault() is XmlElement constElement) {
                ConstantDataProvider constProvider = (ConstantDataProvider) (op.DataValueProvider = new ConstantDataProvider());
                constProvider.DataType = GetRequiredAttribute(constElement, "DataType", s => Enum.Parse<DataType>(s, true));
                constProvider.AppendNullCharToString = GetOptionalAttribute(constElement, "AppendNullCharToString", bool.Parse, constProvider.AppendNullCharToString);
                constProvider.ParseIntAsHex = GetOptionalAttribute(constElement, "ParseIntInputAsHex", bool.Parse, constProvider.ParseIntAsHex);
                if (constElement.GetElementsByTagName("Value").OfType<XmlElement>().FirstOrDefault() is XmlElement valueElement)
                    constProvider.DataValue = XmlTaskSequenceSerialization.DeserializeDataValue(valueElement);
            }
            else if (element.GetElementsByTagName("RandomProvider").OfType<XmlElement>().FirstOrDefault() is XmlElement randElement) {
                RandomNumberDataProvider provider = (RandomNumberDataProvider) (op.DataValueProvider = new RandomNumberDataProvider());
                provider.DataType = GetRequiredAttribute(randElement, "DataType", s => Enum.Parse<DataType>(s, true));
                provider.AppendNullCharToString = GetOptionalAttribute(randElement, "AppendNullCharToString", bool.Parse, provider.AppendNullCharToString);
                provider.ParseIntAsHex = GetOptionalAttribute(randElement, "ParseIntInputAsHex", bool.Parse, provider.ParseIntAsHex);
                if (randElement.GetElementsByTagName("MinNumber").OfType<XmlElement>().FirstOrDefault() is XmlElement minElement)
                    provider.Minimum = (DataValueNumeric) XmlTaskSequenceSerialization.DeserializeDataValue(minElement);
                if (randElement.GetElementsByTagName("MaxNumber").OfType<XmlElement>().FirstOrDefault() is XmlElement maxElement)
                    provider.Maximum = (DataValueNumeric) XmlTaskSequenceSerialization.DeserializeDataValue(maxElement);
            }
        });
    }

    private static T GetOptionalAttribute<T>(XmlElement srcElement, string attributeName, Func<string, T> converter, T notPresentValue) {
        XmlAttribute? node = srcElement.GetAttributeNode(attributeName);
        if (node == null)
            return notPresentValue;

        try {
            return converter(node.Value);
        }
        catch (Exception e) {
            throw new Exception($"Failed to parse attribute '{attributeName}' as {typeof(T).Name}", e);
        }
    }

    private static T GetRequiredAttribute<T>(XmlElement srcElement, string attributeName, Func<string, T> converter) {
        XmlAttribute? node = srcElement.GetAttributeNode(attributeName);
        if (node == null)
            throw new Exception($"Missing required attribute '{attributeName}'");

        try {
            return converter(node.Value);
        }
        catch (Exception e) {
            throw new Exception($"Failed to parse attribute '{attributeName}' as {typeof(T).Name}", e);
        }
    }

    private static string GetRequiredAttribute(XmlElement srcElement, string attributeName, bool canBeWhitespaces) {
        XmlAttribute? node = srcElement.GetAttributeNode(attributeName);
        if (node == null)
            throw new Exception($"Missing required attribute '{attributeName}'");

        if (!canBeWhitespaces && string.IsNullOrWhiteSpace(node.Value))
            throw new Exception($"Attribute '{attributeName}' cannot be an empty string or consist of only whitespaces");

        try {
            return node.Value;
        }
        catch (Exception e) {
            throw new Exception($"Failed to parse attribute '{attributeName}' as {nameof(String)}", e);
        }
    }
}