using SimplePLC.Protocol.Constants;

namespace SimplePLC.Infrastructure.Simulator;

/// <summary>
/// Bộ nhớ thanh ghi Modbus mô phỏng (65.536 thanh ghi), kiểm soát nghiêm ngặt tính hợp lệ và quyền truy cập
/// theo đúng Data Contract V1.7 như firmware STM32 NanoModbus thật.
/// </summary>
public sealed class SimulatorRegisterMemory : IDisposable
{
    private readonly ushort[] _rawRegisters = new ushort[65536];
    private readonly ReaderWriterLockSlim _syncLock = new();

    /// <summary>
    /// Kiểm tra xem toàn bộ dải địa chỉ [startAddress .. startAddress + count - 1] có thuộc phân vùng được phép ĐỌC hay không.
    /// </summary>
    public static bool IsReadableRange(ushort startAddress, ushort count)
    {
        if (count == 0 || startAddress + count > 65536) return false;
        int endAddress = startAddress + count - 1;

        // Kiểm tra xem dải có nằm trọn vẹn trong một phân vùng readable hợp lệ không
        return IsWithin(startAddress, endAddress, ModbusRegisterMap.DeviceDescriptorAddress, ModbusRegisterMap.DeviceDescriptorLength) ||
               IsWithin(startAddress, endAddress, ModbusRegisterMap.DeviceResourceInfoAddress, ModbusRegisterMap.DeviceResourceInfoLength) ||
               IsWithin(startAddress, endAddress, ModbusRegisterMap.RuleTableInfoAddress, ModbusRegisterMap.RuleTableInfoLength) ||
               IsWithin(startAddress, endAddress, ModbusRegisterMap.ActiveRuleTableBaseAddress, ModbusRegisterMap.ActiveRuleTableMaxLength) ||
               IsWithin(startAddress, endAddress, ModbusRegisterMap.DeviceHealthAddress, ModbusRegisterMap.DeviceHealthLength) ||
               IsWithin(startAddress, endAddress, ModbusRegisterMap.RtcClockAddress, ModbusRegisterMap.RtcClockLength) ||
               IsWithin(startAddress, endAddress, ModbusRegisterMap.RuntimeTagValuesBaseAddress, ModbusRegisterMap.RuntimeTagValuesMaxLength) ||
               IsWithin(startAddress, endAddress, ModbusRegisterMap.SystemCommandResultAddress, ModbusRegisterMap.SystemCommandResultLength) ||
               IsWithin(startAddress, endAddress, ModbusRegisterMap.ConfigStatusAddress, 1) ||
               IsWithin(startAddress, endAddress, ModbusRegisterMap.ConfigErrorCodeAddress, 1) ||
               IsWithin(startAddress, endAddress, ModbusRegisterMap.RuleCountStagedAddress, 1) ||
               IsWithin(startAddress, endAddress, ModbusRegisterMap.ExpectedCrc16Address, 1) ||
               IsWithin(startAddress, endAddress, ModbusRegisterMap.ActiveRuleCountAddress, 1) ||
               IsWithin(startAddress, endAddress, ModbusRegisterMap.ActiveRuleCrc16Address, 1) ||
               IsWithin(startAddress, endAddress, ModbusRegisterMap.ConfigStatusAddress, 6) || // 0x9000..0x9005
               IsWithin(startAddress, endAddress, ModbusRegisterMap.StagingRuleTableBaseAddress, ModbusRegisterMap.StagingRuleTableMaxLength) ||
               IsWithin(startAddress, endAddress, ModbusRegisterMap.ActiveRuleVersionAddress, 1) ||
               IsWithin(startAddress, endAddress, ModbusRegisterMap.DiagBlockBaseAddress, ModbusRegisterMap.DiagBlockLength) ||
               IsWithin(startAddress, endAddress, ModbusRegisterMap.FbTimerTableBaseAddress, ModbusRegisterMap.FbTimerTableLength) ||
               IsWithin(startAddress, endAddress, ModbusRegisterMap.FbCounterTableBaseAddress, ModbusRegisterMap.FbCounterTableLength) ||
               IsWithin(startAddress, endAddress, ModbusRegisterMap.FbTimerTableBaseAddress, ModbusRegisterMap.FbTableTotalLength);
    }

    /// <summary>
    /// Kiểm tra xem toàn bộ dải địa chỉ [startAddress .. startAddress + count - 1] có thuộc phân vùng được phép GHI hay không.
    /// </summary>
    public static bool IsWritableRange(ushort startAddress, ushort count)
    {
        if (count == 0 || startAddress + count > 65536) return false;
        int endAddress = startAddress + count - 1;

        return IsWithin(startAddress, endAddress, ModbusRegisterMap.SystemCommandAddress, 1) ||
               IsWithin(startAddress, endAddress, ModbusRegisterMap.DiagCommandAddress, 1) ||
               IsWithin(startAddress, endAddress, ModbusRegisterMap.RtcClockAddress, ModbusRegisterMap.RtcClockLength) ||
               IsWithin(startAddress, endAddress, ModbusRegisterMap.RuleCountStagedAddress, 1) ||
               IsWithin(startAddress, endAddress, ModbusRegisterMap.ExpectedCrc16Address, 1) ||
               IsWithin(startAddress, endAddress, ModbusRegisterMap.RuleCountStagedAddress, 2) || // 0x9002..0x9003
               IsWithin(startAddress, endAddress, ModbusRegisterMap.StagingRuleTableBaseAddress, ModbusRegisterMap.StagingRuleTableMaxLength) ||
               IsWithin(startAddress, endAddress, ModbusRegisterMap.CommitCommandAddress, 1) ||
               IsWithin(startAddress, endAddress, ModbusRegisterMap.FbTimerTableBaseAddress, ModbusRegisterMap.FbTimerTableLength) ||
               IsWithin(startAddress, endAddress, ModbusRegisterMap.FbCounterTableBaseAddress, ModbusRegisterMap.FbCounterTableLength) ||
               IsWithin(startAddress, endAddress, ModbusRegisterMap.FbTimerTableBaseAddress, ModbusRegisterMap.FbTableTotalLength);
    }

    private static bool IsWithin(int start, int end, int rangeStart, int rangeLength)
    {
        int rangeEnd = rangeStart + rangeLength - 1;
        return start >= rangeStart && end <= rangeEnd;
    }

    /// <summary>
    /// Đọc mảng thanh ghi theo chuẩn Modbus FC03 (kiểm tra chặt chẽ tính hợp lệ của địa chỉ).
    /// </summary>
    public ushort[] ReadRegisters(ushort startAddress, ushort count)
    {
        if (!IsReadableRange(startAddress, count))
        {
            throw new InvalidOperationException($"Modbus Exception 0x02 (ILLEGAL_DATA_ADDRESS): Address range 0x{startAddress:X4} (count: {count}) is undefined or not readable per Contract V1.7.");
        }

        _syncLock.EnterReadLock();
        try
        {
            var destination = new ushort[count];
            Array.Copy(_rawRegisters, startAddress, destination, 0, count);
            return destination;
        }
        finally
        {
            _syncLock.ExitReadLock();
        }
    }

    /// <summary>
    /// Ghi mảng thanh ghi theo chuẩn Modbus FC16 (kiểm tra quyền truy cập và ranh giới cho phép).
    /// </summary>
    public void WriteRegisters(ushort startAddress, ReadOnlySpan<ushort> values)
    {
        ushort count = (ushort)values.Length;
        if (!IsWritableRange(startAddress, count))
        {
            throw new InvalidOperationException($"Modbus Exception 0x02 (ILLEGAL_DATA_ADDRESS): Address range 0x{startAddress:X4} (count: {count}) is read-only or outside allowed writable registers per Contract V1.7.");
        }

        _syncLock.EnterWriteLock();
        try
        {
            values.CopyTo(_rawRegisters.AsSpan(startAddress, count));
        }
        finally
        {
            _syncLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Ghi đơn thanh ghi theo chuẩn Modbus FC06.
    /// </summary>
    public void WriteSingleRegister(ushort address, ushort value)
    {
        if (!IsWritableRange(address, 1))
        {
            throw new InvalidOperationException($"Modbus Exception 0x02 (ILLEGAL_DATA_ADDRESS): Address 0x{address:X4} is read-only or outside allowed writable registers per Contract V1.7.");
        }

        _syncLock.EnterWriteLock();
        try
        {
            _rawRegisters[address] = value;
        }
        finally
        {
            _syncLock.ExitWriteLock();
        }
    }

    // =========================================================
    // Direct Internal Access (dành riêng cho Simulator Core)
    // =========================================================

    public ushort InternalRead(ushort address) => _rawRegisters[address];

    public void InternalWrite(ushort address, ushort value) => _rawRegisters[address] = value;

    public ReadOnlySpan<ushort> InternalSpan(ushort startAddress, int count) =>
        _rawRegisters.AsSpan(startAddress, count);

    public Span<ushort> InternalMutableSpan(ushort startAddress, int count) =>
        _rawRegisters.AsSpan(startAddress, count);

    public void InternalClear(ushort startAddress, int count)
    {
        _syncLock.EnterWriteLock();
        try
        {
            Array.Clear(_rawRegisters, startAddress, count);
        }
        finally
        {
            _syncLock.ExitWriteLock();
        }
    }

    public void Dispose()
    {
        _syncLock.Dispose();
    }
}
