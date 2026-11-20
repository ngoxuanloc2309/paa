using System;
using System.Collections.Generic;
using System.Linq;
using SimplePLC.Domain.Enums;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.ViewModels;

namespace SimplePLC.Studio.Services.Modbus;

/// <summary>
/// Unified Modbus execution schedule containing de-duplicated Canvas nodes and coalesced System-only channels.
/// </summary>
public sealed class UnifiedModbusSchedule
{
    public IReadOnlyList<ModbusReadNodeViewModel> CanvasReads { get; init; } = Array.Empty<ModbusReadNodeViewModel>();
    public IReadOnlyList<ModbusWriteNodeViewModel> CanvasWrites { get; init; } = Array.Empty<ModbusWriteNodeViewModel>();
    public IReadOnlyList<ModbusMasterReadConfigDto> SystemCoalescedReads { get; init; } = Array.Empty<ModbusMasterReadConfigDto>();
    public IReadOnlyList<ModbusMasterWriteConfigDto> SystemCoalescedWrites { get; init; } = Array.Empty<ModbusMasterWriteConfigDto>();
}

/// <summary>
/// Domain service coordinating two-way synchronization, address translation,
/// and compile-time coalescing/de-duplication between System Tab Master Channels and Canvas FBD nodes.
/// </summary>
public static class ModbusChannelSyncService
{
    private const int MaxPinsPerBlock = 4;

    /// <summary>
    /// Converts a Modicon 1-based address (e.g. 40001, 30001, 10001, 1) or raw 0-based offset into a wire-level 0-based offset.
    /// Follows the industrial standard CalculateOffset rule.
    /// </summary>
    public static int ToWireOffset(int address, int functionCode) => address switch
    {
        >= 40001 and <= 49999 => address - 40001,
        >= 30001 and <= 39999 => address - 30001,
        >= 10001 and <= 19999 => address - 10001,
        >= 1 and <= 9999 => (functionCode is 1 or 5 or 15 ? address - 1 : address),
        _ => address >= 0 ? address : 0
    };

    /// <summary>
    /// Converts a wire-level 0-based offset into a Modicon 1-based address (e.g. 0 -> 40001 for FC03).
    /// </summary>
    public static int ToModiconAddress(int wireOffset, int functionCode)
    {
        if (wireOffset < 0) wireOffset = 0;

        return functionCode switch
        {
            1 or 5 or 15 => 1 + wireOffset,
            2 => 10001 + wireOffset,
            4 => 30001 + wireOffset,
            3 or 6 or 16 => 40001 + wireOffset,
            _ => 40001 + wireOffset
        };
    }

    /// <summary>
    /// Synchronizes pins from an MB_READ node back to the corresponding System Port MasterChannels table.
    /// </summary>
    public static void SyncReadNodeToSystemPort(ModbusReadNodeViewModel node, ModbusPortItemViewModel port)
    {
        if (node == null || port == null) return;

        int baseWire = node.StartAddress;
        byte fc = (byte)node.FunctionCode;

        foreach (var pin in node.Pins)
        {
            string tagName = pin.TargetTag?.Name ?? string.Empty;
            if (string.IsNullOrWhiteSpace(tagName)) continue;

            int pinWireOffset = baseWire + pin.RegOffset;
            int modiconAddr = ToModiconAddress(pinWireOffset, fc);

            var existing = port.MasterChannels.FirstOrDefault(c =>
                c.SlaveId == node.SlaveId &&
                (c.Address == modiconAddr || string.Equals(c.TargetTagName, tagName, StringComparison.OrdinalIgnoreCase)));

            if (existing != null)
            {
                existing.SlaveId = node.SlaveId;
                existing.FunctionCode = fc;
                existing.Address = (ushort)modiconAddr;
                existing.DataType = pin.DataType;
                existing.Endianness = pin.Endianness;
                existing.Scale = pin.Multiplier;
                existing.TargetTagName = tagName;
                existing.TargetTag = pin.TargetTag;
            }
            else
            {
                var newCh = new ModbusMasterChannelItemViewModel
                {
                    SlaveId = node.SlaveId,
                    FunctionCode = fc,
                    Address = (ushort)modiconAddr,
                    DataType = pin.DataType,
                    Endianness = pin.Endianness,
                    Scale = pin.Multiplier,
                    TargetTagName = tagName,
                    TargetTag = pin.TargetTag
                };
                port.MasterChannels.Add(newCh);
            }
        }
    }

    /// <summary>
    /// Synchronizes pins from an MB_WRITE node back to the corresponding System Port MasterChannels table.
    /// </summary>
    public static void SyncWriteNodeToSystemPort(ModbusWriteNodeViewModel node, ModbusPortItemViewModel port)
    {
        if (node == null || port == null) return;

        int baseWire = node.StartAddress;
        byte fc = (byte)node.FunctionCode;

        foreach (var pin in node.Pins)
        {
            string tagName = pin.TargetTag?.Name ?? string.Empty;
            if (string.IsNullOrWhiteSpace(tagName)) continue;

            int pinWireOffset = baseWire + pin.RegOffset;
            int modiconAddr = ToModiconAddress(pinWireOffset, fc);

            var existing = port.MasterChannels.FirstOrDefault(c =>
                c.SlaveId == node.SlaveId &&
                (c.Address == modiconAddr || string.Equals(c.TargetTagName, tagName, StringComparison.OrdinalIgnoreCase)));

            if (existing != null)
            {
                existing.SlaveId = node.SlaveId;
                existing.FunctionCode = fc;
                existing.Address = (ushort)modiconAddr;
                existing.DataType = pin.DataType;
                existing.Endianness = pin.Endianness;
                existing.Scale = pin.Multiplier;
                existing.TargetTagName = tagName;
                existing.TargetTag = pin.TargetTag;
            }
            else
            {
                var newCh = new ModbusMasterChannelItemViewModel
                {
                    SlaveId = node.SlaveId,
                    FunctionCode = fc,
                    Address = (ushort)modiconAddr,
                    DataType = pin.DataType,
                    Endianness = pin.Endianness,
                    Scale = pin.Multiplier,
                    TargetTagName = tagName,
                    TargetTag = pin.TargetTag
                };
                port.MasterChannels.Add(newCh);
            }
        }
    }

    /// <summary>
    /// Propagates port-level parameters (Timeout, Baud) from System Ports to existing Canvas nodes.
    /// </summary>
    public static void SyncSystemPortsToCanvasNodes(
        IEnumerable<ModbusPortItemViewModel> ports,
        IEnumerable<ModbusReadNodeViewModel> readNodes,
        IEnumerable<ModbusWriteNodeViewModel> writeNodes)
    {
        if (ports == null) return;
        var portMap = ports.ToDictionary(p => (int)p.PortIndex);

        if (readNodes != null)
        {
            foreach (var r in readNodes)
            {
                if (portMap.TryGetValue(r.PortIndex, out var p) && p.ResponseTimeoutMs > 0)
                {
                    r.TimeoutMs = p.ResponseTimeoutMs;
                }
            }
        }

        if (writeNodes != null)
        {
            foreach (var w in writeNodes)
            {
                if (portMap.TryGetValue(w.PortIndex, out var p) && p.ResponseTimeoutMs > 0)
                {
                    w.TimeoutMs = p.ResponseTimeoutMs;
                }
            }
        }
    }

    /// <summary>
    /// Builds a unified execution schedule.
    /// Channels claimed by Canvas nodes are de-duplicated.
    /// Standalone System Tab channels are coalesced into multi-pin blocks (up to 4 contiguous registers per block).
    /// </summary>
    public static UnifiedModbusSchedule BuildUnifiedModbusSchedule(
        IEnumerable<ModbusPortItemViewModel> ports,
        IEnumerable<ModbusReadNodeViewModel> canvasReads,
        IEnumerable<ModbusWriteNodeViewModel> canvasWrites)
    {
        var readList = canvasReads?.ToList() ?? new List<ModbusReadNodeViewModel>();
        var writeList = canvasWrites?.ToList() ?? new List<ModbusWriteNodeViewModel>();

        var claimedReadTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in readList)
        {
            foreach (var pin in r.Pins)
            {
                if (pin.TargetTag != null && !string.IsNullOrWhiteSpace(pin.TargetTag.Name))
                    claimedReadTags.Add(pin.TargetTag.Name);
            }
        }

        var claimedWriteTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var w in writeList)
        {
            foreach (var pin in w.Pins)
            {
                if (pin.TargetTag != null && !string.IsNullOrWhiteSpace(pin.TargetTag.Name))
                    claimedWriteTags.Add(pin.TargetTag.Name);
            }
        }

        var coalescedReads = new List<ModbusMasterReadConfigDto>();
        var coalescedWrites = new List<ModbusMasterWriteConfigDto>();

        if (ports != null)
        {
            foreach (var port in ports.Where(p => p.Enabled))
            {
                var unlinkedReads = port.MasterChannels
                    .Where(ch => ch.FunctionCode >= 1 && ch.FunctionCode <= 4)
                    .Where(ch => !string.IsNullOrWhiteSpace(ch.TargetTagName) && !claimedReadTags.Contains(ch.TargetTagName))
                    .OrderBy(ch => ch.SlaveId)
                    .ThenBy(ch => ch.FunctionCode)
                    .ThenBy(ch => ch.Address)
                    .ToList();

                coalescedReads.AddRange(CoalesceReadChannels(port.PortIndex, port.ResponseTimeoutMs, unlinkedReads));

                var unlinkedWrites = port.MasterChannels
                    .Where(ch => ch.FunctionCode == 5 || ch.FunctionCode == 6 || ch.FunctionCode == 15 || ch.FunctionCode == 16)
                    .Where(ch => !string.IsNullOrWhiteSpace(ch.TargetTagName) && !claimedWriteTags.Contains(ch.TargetTagName))
                    .OrderBy(ch => ch.SlaveId)
                    .ThenBy(ch => ch.FunctionCode)
                    .ThenBy(ch => ch.Address)
                    .ToList();

                coalescedWrites.AddRange(CoalesceWriteChannels(port.PortIndex, port.ResponseTimeoutMs, unlinkedWrites));
            }
        }

        return new UnifiedModbusSchedule
        {
            CanvasReads = readList,
            CanvasWrites = writeList,
            SystemCoalescedReads = coalescedReads,
            SystemCoalescedWrites = coalescedWrites
        };
    }

    /// <summary>
    /// Overload accepting ProjectMetadata.ModbusPorts directly (for headless / compiler usage).
    /// </summary>
    public static (List<ModbusMasterReadConfigDto> reads, List<ModbusMasterWriteConfigDto> writes) CoalesceStandalonePortChannels(
        IEnumerable<ModbusPortConfigModel>? ports,
        IEnumerable<string> existingClaimedReadTags,
        IEnumerable<string> existingClaimedWriteTags,
        Func<string, ushort?>? tagIndexResolver)
    {
        var reads = new List<ModbusMasterReadConfigDto>();
        var writes = new List<ModbusMasterWriteConfigDto>();
        if (ports == null) return (reads, writes);

        var claimedRead = new HashSet<string>(existingClaimedReadTags ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        var claimedWrite = new HashSet<string>(existingClaimedWriteTags ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);

        int blockIdx = 100;
        foreach (var port in ports.Where(p => p.Enabled && string.Equals(p.Role, "Master", StringComparison.OrdinalIgnoreCase)))
        {
            if (port.MasterChannels == null || port.MasterChannels.Count == 0) continue;

            // Reads
            var unlinkedReads = port.MasterChannels
                .Where(ch => ch.FunctionCode >= 1 && ch.FunctionCode <= 4)
                .Where(ch => string.IsNullOrEmpty(ch.LinkedNodeId))
                .Where(ch => !string.IsNullOrWhiteSpace(ch.TargetTagName) && !claimedRead.Contains(ch.TargetTagName))
                .OrderBy(ch => ch.SlaveId)
                .ThenBy(ch => ch.FunctionCode)
                .ThenBy(ch => ch.Address)
                .ToList();

            var readGroups = unlinkedReads.GroupBy(c => new { c.SlaveId, c.FunctionCode });
            foreach (var grp in readGroups)
            {
                var sorted = grp.OrderBy(c => c.Address).ToList();
                var currentBatch = new List<ModbusMasterChannelModel>();
                int currentBatchStartWire = -1;

                foreach (var ch in sorted)
                {
                    int wireOffset = ToWireOffset(ch.Address, ch.FunctionCode);
                    if (currentBatch.Count == 0)
                    {
                        currentBatch.Add(ch);
                        currentBatchStartWire = wireOffset;
                    }
                    else
                    {
                        int lastWire = ToWireOffset(currentBatch.Last().Address, ch.FunctionCode);
                        if (wireOffset == lastWire + 1 && currentBatch.Count < MaxPinsPerBlock)
                        {
                            currentBatch.Add(ch);
                        }
                        else
                        {
                            reads.Add(CreateReadModelDto(port.PortIndex, port.ResponseTimeoutMs, grp.Key.SlaveId, grp.Key.FunctionCode, (ushort)currentBatchStartWire, currentBatch, blockIdx++, tagIndexResolver));
                            currentBatch.Clear();
                            currentBatch.Add(ch);
                            currentBatchStartWire = wireOffset;
                        }
                    }
                }
                if (currentBatch.Count > 0)
                {
                    reads.Add(CreateReadModelDto(port.PortIndex, port.ResponseTimeoutMs, grp.Key.SlaveId, grp.Key.FunctionCode, (ushort)currentBatchStartWire, currentBatch, blockIdx++, tagIndexResolver));
                }
            }

            // Writes
            var unlinkedWrites = port.MasterChannels
                .Where(ch => ch.FunctionCode is 5 or 6 or 15 or 16)
                .Where(ch => string.IsNullOrEmpty(ch.LinkedNodeId))
                .Where(ch => !string.IsNullOrWhiteSpace(ch.TargetTagName) && !claimedWrite.Contains(ch.TargetTagName))
                .OrderBy(ch => ch.SlaveId)
                .ThenBy(ch => ch.FunctionCode)
                .ThenBy(ch => ch.Address)
                .ToList();

            var writeGroups = unlinkedWrites.GroupBy(c => new { c.SlaveId, c.FunctionCode });
            foreach (var grp in writeGroups)
            {
                var sorted = grp.OrderBy(c => c.Address).ToList();
                var currentBatch = new List<ModbusMasterChannelModel>();
                int currentBatchStartWire = -1;

                foreach (var ch in sorted)
                {
                    int wireOffset = ToWireOffset(ch.Address, ch.FunctionCode);
                    if (currentBatch.Count == 0)
                    {
                        currentBatch.Add(ch);
                        currentBatchStartWire = wireOffset;
                    }
                    else
                    {
                        int lastWire = ToWireOffset(currentBatch.Last().Address, ch.FunctionCode);
                        if (wireOffset == lastWire + 1 && currentBatch.Count < MaxPinsPerBlock)
                        {
                            currentBatch.Add(ch);
                        }
                        else
                        {
                            writes.Add(CreateWriteModelDto(port.PortIndex, port.ResponseTimeoutMs, grp.Key.SlaveId, grp.Key.FunctionCode, (ushort)currentBatchStartWire, currentBatch, blockIdx++, tagIndexResolver));
                            currentBatch.Clear();
                            currentBatch.Add(ch);
                            currentBatchStartWire = wireOffset;
                        }
                    }
                }
                if (currentBatch.Count > 0)
                {
                    writes.Add(CreateWriteModelDto(port.PortIndex, port.ResponseTimeoutMs, grp.Key.SlaveId, grp.Key.FunctionCode, (ushort)currentBatchStartWire, currentBatch, blockIdx++, tagIndexResolver));
                }
            }
        }

        return (reads, writes);
    }

    private static ModbusMasterReadConfigDto CreateReadModelDto(
        byte portIndex,
        ushort timeoutMs,
        byte slaveId,
        byte functionCode,
        ushort startWireOffset,
        List<ModbusMasterChannelModel> channels,
        int blockIdx,
        Func<string, ushort?>? tagIndexResolver)
    {
        var pins = channels.Select((ch, idx) => new ModbusMasterPinBindingDto
        {
            Id = $"Val_{idx + 1}",
            Label = string.IsNullOrWhiteSpace(ch.TargetTagName) ? $"Val_{idx + 1}" : ch.TargetTagName,
            RegOffset = (ushort)(ToWireOffset(ch.Address, functionCode) - startWireOffset),
            DataType = ch.DataType,
            Endianness = ch.Endianness,
            Multiplier = (float)(ch.Scale == 0 ? 1.0 : ch.Scale),
            TargetTagIndex = tagIndexResolver != null ? tagIndexResolver(ch.TargetTagName) : null
        }).ToList();

        var fcEnum = functionCode switch
        {
            1 => ModbusMasterFunctionCode.ReadCoils,
            2 => ModbusMasterFunctionCode.ReadDiscreteInputs,
            4 => ModbusMasterFunctionCode.ReadInputRegisters,
            _ => ModbusMasterFunctionCode.ReadHoldingRegisters
        };

        return new ModbusMasterReadConfigDto
        {
            NodeId = $"PORT_{portIndex}_COALESCED_READ_{startWireOffset}",
            BlockIndex = blockIdx,
            PortIndex = portIndex,
            Enabled = true,
            SlaveId = slaveId,
            FunctionCode = fcEnum,
            StartAddress = startWireOffset,
            RegCount = (ushort)pins.Count,
            ReadMode = ModbusReadMode.Cyclic,
            PollIntervalMs = 500,
            TimeoutMs = timeoutMs > 0 ? timeoutMs : (uint)200,
            Pins = pins
        };
    }

    private static ModbusMasterWriteConfigDto CreateWriteModelDto(
        byte portIndex,
        ushort timeoutMs,
        byte slaveId,
        byte functionCode,
        ushort startWireOffset,
        List<ModbusMasterChannelModel> channels,
        int blockIdx,
        Func<string, ushort?>? tagIndexResolver)
    {
        var fcEnum = functionCode switch
        {
            5 => ModbusMasterWriteFunctionCode.WriteSingleCoil,
            15 => ModbusMasterWriteFunctionCode.WriteMultipleCoils,
            16 => ModbusMasterWriteFunctionCode.WriteMultipleRegisters,
            _ => ModbusMasterWriteFunctionCode.WriteSingleRegister
        };

        var firstCh = channels.FirstOrDefault();
        ushort? targetTagIndex = firstCh != null && tagIndexResolver != null ? tagIndexResolver(firstCh.TargetTagName) : null;

        return new ModbusMasterWriteConfigDto
        {
            NodeId = $"PORT_{portIndex}_COALESCED_WRITE_{startWireOffset}",
            BlockIndex = blockIdx,
            PortIndex = portIndex,
            Enabled = true,
            SlaveId = slaveId,
            FunctionCode = fcEnum,
            StartAddress = startWireOffset,
            PointCount = (ushort)channels.Count,
            WriteMode = ModbusWriteMode.DirectRegisters,
            TriggerMode = ModbusWriteTriggerMode.OnDataChange,
            CyclicIntervalMs = 500,
            TimeoutMs = timeoutMs > 0 ? timeoutMs : (uint)200,
            Deadband = 0.5f,
            CommandTagIndex = targetTagIndex
        };
    }

    private static List<ModbusMasterReadConfigDto> CoalesceReadChannels(
        byte portIndex,
        ushort timeoutMs,
        List<ModbusMasterChannelItemViewModel> channels)
    {
        var result = new List<ModbusMasterReadConfigDto>();
        if (channels.Count == 0) return result;

        var groups = channels.GroupBy(c => new { c.SlaveId, c.FunctionCode });

        foreach (var grp in groups)
        {
            var sorted = grp.OrderBy(c => c.Address).ToList();
            var currentBatch = new List<ModbusMasterChannelItemViewModel>();
            int currentBatchStartWire = -1;

            foreach (var ch in sorted)
            {
                int wireOffset = ToWireOffset(ch.Address, ch.FunctionCode);

                if (currentBatch.Count == 0)
                {
                    currentBatch.Add(ch);
                    currentBatchStartWire = wireOffset;
                }
                else
                {
                    int lastWire = ToWireOffset(currentBatch.Last().Address, ch.FunctionCode);
                    if (wireOffset == lastWire + 1 && currentBatch.Count < MaxPinsPerBlock)
                    {
                        currentBatch.Add(ch);
                    }
                    else
                    {
                        result.Add(CreateReadDto(portIndex, timeoutMs, grp.Key.SlaveId, grp.Key.FunctionCode, (ushort)currentBatchStartWire, currentBatch));
                        currentBatch.Clear();
                        currentBatch.Add(ch);
                        currentBatchStartWire = wireOffset;
                    }
                }
            }

            if (currentBatch.Count > 0)
            {
                result.Add(CreateReadDto(portIndex, timeoutMs, grp.Key.SlaveId, grp.Key.FunctionCode, (ushort)currentBatchStartWire, currentBatch));
            }
        }

        return result;
    }

    private static ModbusMasterReadConfigDto CreateReadDto(
        byte portIndex,
        ushort timeoutMs,
        byte slaveId,
        byte functionCode,
        ushort startWireOffset,
        List<ModbusMasterChannelItemViewModel> channels)
    {
        var pins = channels.Select((ch, idx) => new ModbusMasterPinBindingDto
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Label = $"Ch_{idx + 1}",
            RegOffset = (ushort)(ToWireOffset(ch.Address, functionCode) - startWireOffset),
            DataType = ch.DataType ?? ModbusDataType.Int16,
            Endianness = ch.Endianness,
            Multiplier = (float)ch.Scale,
            TargetTagIndex = ch.TargetTag != null ? (ushort)ch.TargetTag.Index : null
        }).ToList();

        var fcEnum = functionCode switch
        {
            1 => ModbusMasterFunctionCode.ReadCoils,
            2 => ModbusMasterFunctionCode.ReadDiscreteInputs,
            4 => ModbusMasterFunctionCode.ReadInputRegisters,
            _ => ModbusMasterFunctionCode.ReadHoldingRegisters
        };

        return new ModbusMasterReadConfigDto
        {
            PortIndex = portIndex,
            SlaveId = slaveId,
            FunctionCode = fcEnum,
            StartAddress = startWireOffset,
            RegCount = (ushort)pins.Count,
            ReadMode = ModbusReadMode.Cyclic,
            PollIntervalMs = 500,
            TimeoutMs = timeoutMs > 0 ? timeoutMs : (uint)300,
            Pins = pins
        };
    }

    private static List<ModbusMasterWriteConfigDto> CoalesceWriteChannels(
        byte portIndex,
        ushort timeoutMs,
        List<ModbusMasterChannelItemViewModel> channels)
    {
        var result = new List<ModbusMasterWriteConfigDto>();
        if (channels.Count == 0) return result;

        var groups = channels.GroupBy(c => new { c.SlaveId, c.FunctionCode });

        foreach (var grp in groups)
        {
            var sorted = grp.OrderBy(c => c.Address).ToList();
            var currentBatch = new List<ModbusMasterChannelItemViewModel>();
            int currentBatchStartWire = -1;

            foreach (var ch in sorted)
            {
                int wireOffset = ToWireOffset(ch.Address, ch.FunctionCode);

                if (currentBatch.Count == 0)
                {
                    currentBatch.Add(ch);
                    currentBatchStartWire = wireOffset;
                }
                else
                {
                    int lastWire = ToWireOffset(currentBatch.Last().Address, ch.FunctionCode);
                    if (wireOffset == lastWire + 1 && currentBatch.Count < MaxPinsPerBlock)
                    {
                        currentBatch.Add(ch);
                    }
                    else
                    {
                        result.Add(CreateWriteDto(portIndex, timeoutMs, grp.Key.SlaveId, grp.Key.FunctionCode, (ushort)currentBatchStartWire, currentBatch));
                        currentBatch.Clear();
                        currentBatch.Add(ch);
                        currentBatchStartWire = wireOffset;
                    }
                }
            }

            if (currentBatch.Count > 0)
            {
                result.Add(CreateWriteDto(portIndex, timeoutMs, grp.Key.SlaveId, grp.Key.FunctionCode, (ushort)currentBatchStartWire, currentBatch));
            }
        }

        return result;
    }

    private static ModbusMasterWriteConfigDto CreateWriteDto(
        byte portIndex,
        ushort timeoutMs,
        byte slaveId,
        byte functionCode,
        ushort startWireOffset,
        List<ModbusMasterChannelItemViewModel> channels)
    {
        var fcEnum = functionCode switch
        {
            5 => ModbusMasterWriteFunctionCode.WriteSingleCoil,
            15 => ModbusMasterWriteFunctionCode.WriteMultipleCoils,
            16 => ModbusMasterWriteFunctionCode.WriteMultipleRegisters,
            _ => ModbusMasterWriteFunctionCode.WriteSingleRegister
        };

        return new ModbusMasterWriteConfigDto
        {
            PortIndex = portIndex,
            SlaveId = slaveId,
            FunctionCode = fcEnum,
            StartAddress = startWireOffset,
            PointCount = (ushort)channels.Count,
            WriteMode = ModbusWriteMode.DirectRegisters,
            TriggerMode = ModbusWriteTriggerMode.OnDataChange,
            TimeoutMs = timeoutMs > 0 ? timeoutMs : (uint)300
        };
    }
}
