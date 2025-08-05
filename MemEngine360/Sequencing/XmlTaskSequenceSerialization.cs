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
using MemEngine360.Engine.Modes;
using MemEngine360.Sequencing.Operations;
using MemEngine360.ValueAbstraction;
using PFXToolKitUI.Utils;

namespace MemEngine360.Sequencing;

public class XmlTaskSequenceSerialization {
    private readonly struct ConditionEntry(string name, Action<XmlDocument, XmlElement, BaseSequenceCondition> serializer, Action<XmlElement, BaseSequenceCondition> deserializer) {
        public readonly string Name = name;
        public readonly Action<XmlDocument, XmlElement, BaseSequenceCondition> Serializer = serializer;
        public readonly Action<XmlElement, BaseSequenceCondition> Deserializer = deserializer;
    }

    private readonly struct OperationEntry(string name, Action<XmlDocument, XmlElement, BaseSequenceOperation> serializer, Action<XmlElement, BaseSequenceOperation> deserializer) {
        public readonly string Name = name;
        public readonly Action<XmlDocument, XmlElement, BaseSequenceOperation> Serializer = serializer;
        public readonly Action<XmlElement, BaseSequenceOperation> Deserializer = deserializer;
    }

    private static readonly Dictionary<Type, ConditionEntry> conditionTypeToRegistrationInfo;
    private static readonly Dictionary<string, Type> conditionNameToTypeInfo;

    private static readonly Dictionary<Type, OperationEntry> operationTypeToRegistrationInfo;
    private static readonly Dictionary<string, Type> operationNameToTypeInfo;

    static XmlTaskSequenceSerialization() {
        conditionTypeToRegistrationInfo = new Dictionary<Type, ConditionEntry>();
        conditionNameToTypeInfo = new Dictionary<string, Type>();

        operationTypeToRegistrationInfo = new Dictionary<Type, OperationEntry>();
        operationNameToTypeInfo = new Dictionary<string, Type>();
    }

    public static void RegisterCondition(string uniqueName, Type typeOfCondition, Action<XmlDocument, XmlElement, BaseSequenceCondition> serializer, Action<XmlElement, BaseSequenceCondition> deserializer) {
        ArgumentNullException.ThrowIfNull(typeOfCondition);
        ArgumentNullException.ThrowIfNull(serializer);
        ArgumentNullException.ThrowIfNull(deserializer);
        if (!typeof(BaseSequenceCondition).IsAssignableFrom(typeOfCondition))
            throw new ArgumentException($"Condition type ({typeOfCondition.Name}) is not assignable to " + nameof(BaseSequenceCondition));

        conditionNameToTypeInfo.Add(uniqueName, typeOfCondition);
        conditionTypeToRegistrationInfo.Add(typeOfCondition, new ConditionEntry(uniqueName, serializer, deserializer));
    }

    public static void RegisterOperation(string uniqueName, Type typeOfOperation, Action<XmlDocument, XmlElement, BaseSequenceOperation> serializer, Action<XmlElement, BaseSequenceOperation> deserializer) {
        ArgumentNullException.ThrowIfNull(typeOfOperation);
        ArgumentNullException.ThrowIfNull(serializer);
        ArgumentNullException.ThrowIfNull(deserializer);
        if (!typeof(BaseSequenceOperation).IsAssignableFrom(typeOfOperation))
            throw new ArgumentException($"Operation type ({typeOfOperation.Name}) is not assignable to " + nameof(BaseSequenceOperation));

        operationNameToTypeInfo.Add(uniqueName, typeOfOperation);
        operationTypeToRegistrationInfo.Add(typeOfOperation, new OperationEntry(uniqueName, serializer, deserializer));
    }

    public static void SaveToDocument(XmlDocument document, IList<TaskSequence> sequences) {
        XmlElement xmlSequenceList = (XmlElement) document.AppendChild(document.CreateElement("TaskSequenceList"))!;
        foreach (TaskSequence sequence in sequences) {
            SaveSequence(document, xmlSequenceList, sequence);
        }
    }

    public static List<TaskSequence> DeserializeDocument(XmlDocument document) {
        if (!(document.GetElementsByTagName("TaskSequenceList").OfType<XmlElement>().FirstOrDefault() is XmlElement listElement))
            throw new Exception("Missing TaskSequenceList in document");
        return DeserializeSequenceList(listElement);
    }

    public static List<TaskSequence> DeserializeSequenceList(XmlElement xmlSequenceList) {
        List<TaskSequence> list = new List<TaskSequence>();
        foreach (XmlElement xmlSequence in xmlSequenceList.GetElementsByTagName("Sequence").OfType<XmlElement>()) {
            list.Add(DeserializeSequence(xmlSequence));
        }

        return list;
    }

    public static void SaveSequence(XmlDocument document, XmlElement sequenceElement, TaskSequence sequence) {
        XmlElement element = (XmlElement) sequenceElement.AppendChild(document.CreateElement("Sequence"))!;
        element.SetAttribute("DisplayName", sequence.DisplayName);
        element.SetAttribute("RunCount", sequence.RunCount < 0 ? "Infinity" : sequence.RunCount.ToString());
        element.SetAttribute("BusyLockPriority", sequence.HasBusyLockPriority ? "true" : "false");
        if (sequence.Conditions.Count > 0) {
            XmlElement xmlConditionList = (XmlElement) element.AppendChild(document.CreateElement("ConditionList"))!;
            foreach (BaseSequenceCondition condition in sequence.Conditions.ToList()) {
                SaveCondition(document, xmlConditionList, condition);
            }
        }

        if (sequence.Operations.Count > 0) {
            XmlElement xmlOperationList = (XmlElement) element.AppendChild(document.CreateElement("OperationList"))!;
            foreach (BaseSequenceOperation operation in sequence.Operations.ToList()) {
                SaveOperation(document, xmlOperationList, operation);
            }
        }
    }

    public static TaskSequence DeserializeSequence(XmlElement element) {
        int runCount;
        string runCountText = element.GetAttribute("RunCount");
        if (runCountText.StartsWith("inf", StringComparison.OrdinalIgnoreCase))
            runCount = -1;
        else if (!int.TryParse(runCountText, out runCount))
            throw new Exception("Invalid run count text: " + runCountText);

        if (!bool.TryParse(element.GetAttribute("BusyLockPriority"), out bool busyLock))
            throw new Exception("Invalid bool for BusyLockPriority: " + runCountText);

        TaskSequence sequence = new TaskSequence() {
            DisplayName = element.GetAttribute("DisplayName"),
            RunCount = runCount,
            HasBusyLockPriority = busyLock
        };

        if (element.GetElementsByTagName("ConditionList").OfType<XmlElement>().FirstOrDefault() is XmlElement conditionListElement) {
            foreach (XmlElement conditionElement in conditionListElement.GetElementsByTagName("Condition").OfType<XmlElement>()) {
                sequence.Conditions.Add(DeserializeCondition(conditionElement));
            }
        }

        if (element.GetElementsByTagName("OperationList").OfType<XmlElement>().FirstOrDefault() is XmlElement operationListElement) {
            foreach (XmlElement operationElement in operationListElement.GetElementsByTagName("Operation").OfType<XmlElement>()) {
                sequence.Operations.Add(DeserializeOperation(operationElement));
            }
        }

        foreach (JumpToLabelOperation op in sequence.Operations.OfType<JumpToLabelOperation>()) {
            op.UpdateTargetLabelForName();
        }
        
        return sequence;
    }

    public static void SaveCondition(XmlDocument document, XmlElement parentElement, BaseSequenceCondition condition) {
        if (!conditionTypeToRegistrationInfo.TryGetValue(condition.GetType(), out ConditionEntry entry)) {
            throw new Exception("Missing registration for condition type: " + condition.GetType());
        }

        XmlElement element = (XmlElement) parentElement.AppendChild(document.CreateElement("Condition"))!;
        element.SetAttribute("TypeName", entry.Name);
        element.SetAttribute("IsEnabled", condition.IsEnabled ? "true" : "false");
        element.SetAttribute("OutputMode", condition.OutputMode.ToString());

        for (Type? type = condition.GetType(); type != null; type = type.BaseType) {
            if (conditionTypeToRegistrationInfo.TryGetValue(type, out ConditionEntry info)) {
                info.Serializer(document, element, condition);
            }
        }
    }

    public static BaseSequenceCondition DeserializeCondition(XmlElement element) {
        if (!conditionNameToTypeInfo.TryGetValue(element.GetAttribute("TypeName"), out Type? objType))
            throw new Exception($"No such condition type: '{element.GetAttribute("TypeName")}'");
        if (!bool.TryParse(element.GetAttribute("IsEnabled"), out bool isEnabled))
            throw new Exception("Invalid bool for IsEnabled: " + element.GetAttribute("IsEnabled"));
        if (!Enum.TryParse(element.GetAttribute("OutputMode"), out ConditionOutputMode outputMode))
            throw new Exception("Invalid output mode: " + element.GetAttribute("OutputMode"));

        BaseSequenceCondition condition = (BaseSequenceCondition) Activator.CreateInstance(objType)!;
        condition.IsEnabled = isEnabled;
        condition.OutputMode = outputMode;

        for (Type? type = condition.GetType(); type != null; type = type.BaseType) {
            if (conditionTypeToRegistrationInfo.TryGetValue(type, out ConditionEntry info)) {
                info.Deserializer(element, condition);
            }
        }

        return condition;
    }

    public static void SaveOperation(XmlDocument document, XmlElement parentElement, BaseSequenceOperation operation) {
        if (!operationTypeToRegistrationInfo.TryGetValue(operation.GetType(), out OperationEntry entry)) {
            throw new Exception("Missing registration for condition type: " + operation.GetType());
        }

        XmlElement element = (XmlElement) parentElement.AppendChild(document.CreateElement("Operation"))!;
        element.SetAttribute("TypeName", entry.Name);
        element.SetAttribute("IsEnabled", operation.IsEnabled ? "true" : "false");
        XmlElement xmlRandomTriggerInfo = (XmlElement) element.AppendChild(document.CreateElement("RandomTriggerInfo"))!;
        {
            TimeSpan? w4t = operation.RandomTriggerHelper.WaitForTriggerInterval;
            if (w4t.HasValue)
                xmlRandomTriggerInfo.SetAttribute("WaitForTriggerInterval", TimeSpanUtils.ConvertToString(w4t.Value));
            xmlRandomTriggerInfo.SetAttribute("Chance", operation.RandomTriggerHelper.Chance.ToString());
            xmlRandomTriggerInfo.SetAttribute("MinimumTriesToTrigger", operation.RandomTriggerHelper.MinimumTriesToTrigger.ToString());
        }
        
        if (operation.Conditions.Count > 0) {
            XmlElement xmlConditionList = (XmlElement) element.AppendChild(document.CreateElement("ConditionList"))!;
            foreach (BaseSequenceCondition condition in operation.Conditions.ToList()) {
                SaveCondition(document, xmlConditionList, condition);
            }
        }

        for (Type? type = operation.GetType(); type != null; type = type.BaseType) {
            if (operationTypeToRegistrationInfo.TryGetValue(type, out OperationEntry info)) {
                info.Serializer(document, element, operation);
            }
        }
    }

    public static BaseSequenceOperation DeserializeOperation(XmlElement element) {
        if (!operationNameToTypeInfo.TryGetValue(element.GetAttribute("TypeName"), out Type? objType))
            throw new Exception($"No such condition type: '{element.GetAttribute("TypeName")}'");
        if (!bool.TryParse(element.GetAttribute("IsEnabled"), out bool isEnabled))
            throw new Exception("Invalid bool for IsEnabled: " + element.GetAttribute("IsEnabled"));
        if (!(element.GetElementsByTagName("RandomTriggerInfo").OfType<XmlElement>().FirstOrDefault() is XmlElement randomTriggerElement))
            throw new Exception("Missing 'RandomTriggerInfo'");

        TimeSpan? theWaitTime = null;
        string w4tText = randomTriggerElement.GetAttribute("WaitForTriggerInterval");
        if (!string.IsNullOrWhiteSpace(w4tText)) {
            if (!TimeSpanUtils.TryParseTime(w4tText, out TimeSpan waitTime, out string? errorMessage))
                throw new Exception($"Invalid time for WaitForTriggerInterval '{randomTriggerElement.GetAttribute("WaitForTriggerInterval")}'. {errorMessage}");
            theWaitTime = waitTime;
        }

        if (!uint.TryParse(randomTriggerElement.GetAttribute("Chance"), out uint chance))
            throw new Exception("Invalid uint for Chance: " + randomTriggerElement.GetAttribute("Chance"));
        if (!uint.TryParse(randomTriggerElement.GetAttribute("MinimumTriesToTrigger"), out uint minTries))
            throw new Exception("Invalid uint for MinimumTriesToTrigger: " + randomTriggerElement.GetAttribute("MinimumTriesToTrigger"));

        BaseSequenceOperation operation = (BaseSequenceOperation) Activator.CreateInstance(objType)!;
        operation.IsEnabled = isEnabled;
        operation.RandomTriggerHelper.WaitForTriggerInterval = theWaitTime;
        operation.RandomTriggerHelper.Chance = chance;
        operation.RandomTriggerHelper.MinimumTriesToTrigger = minTries;

        for (Type? type = operation.GetType(); type != null; type = type.BaseType) {
            if (operationTypeToRegistrationInfo.TryGetValue(type, out OperationEntry info)) {
                info.Deserializer(element, operation);
            }
        }
        
        if (element.GetElementsByTagName("ConditionList").OfType<XmlElement>().FirstOrDefault() is XmlElement conditionListElement) {
            foreach (XmlElement conditionElement in conditionListElement.GetElementsByTagName("Condition").OfType<XmlElement>()) {
                operation.Conditions.Add(DeserializeCondition(conditionElement));
            }
        }

        return operation;
    }

    public static void SerializeDataValue(XmlElement element, IDataValue value) {
        element.SetAttribute("Type", value.DataType.ToString());
        switch (value.DataType) {
            case DataType.Byte:
            case DataType.Int16:
            case DataType.Int32:
            case DataType.Int64:
            case DataType.Float:
            case DataType.Double:
            case DataType.String:
                element.SetAttribute("Value", value.BoxedValue.ToString());
                break;
            case DataType.ByteArray: element.SetAttribute("Value", NumberUtils.BytesToHexAscii(((DataValueByteArray) value).Value, ' ')); break;
            default:                 throw new ArgumentOutOfRangeException();
        }
    }

    public static IDataValue DeserializeDataValue(XmlElement element) {
        string typeAttribute = element.GetAttribute("Type");
        if (string.IsNullOrWhiteSpace(typeAttribute) || !Enum.TryParse(typeAttribute, true, out DataType type)) {
            throw new Exception($"Invalid data type for {nameof(IDataValue)}: {typeAttribute}");
        }

        string valueAttribute = element.GetAttribute("Value");
        switch (type) {
            case DataType.Byte:   return new DataValueByte(byte.Parse(valueAttribute));
            case DataType.Int16:  return new DataValueInt16(short.Parse(valueAttribute));
            case DataType.Int32:  return new DataValueInt32(int.Parse(valueAttribute));
            case DataType.Int64:  return new DataValueInt64(long.Parse(valueAttribute));
            case DataType.Float:  return new DataValueFloat(float.Parse(valueAttribute));
            case DataType.Double: return new DataValueDouble(double.Parse(valueAttribute));
            case DataType.String:
                string stringTypeAttribute = element.GetAttribute("StringType");
                if (string.IsNullOrWhiteSpace(stringTypeAttribute) || !Enum.TryParse(stringTypeAttribute, true, out StringType stringType)) {
                    throw new Exception($"Invalid string type: {stringTypeAttribute}");
                }

                return new DataValueString(valueAttribute, stringType);
            case DataType.ByteArray:
                if (!NumberUtils.TryParseHexAsciiToBytes(valueAttribute, out byte[]? array, ' ')) {
                    throw new Exception($"Invalid byte array: {valueAttribute}");
                }

                return new DataValueByteArray(array);
            default: throw new ArgumentOutOfRangeException();
        }
    }
}