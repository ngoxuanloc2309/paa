using SimplePLC.Protocol.Codec;
using SimplePLC.Protocol.Constants;

namespace SimplePLC.Infrastructure.Simulator;

/// <summary>
/// Bộ sinh tín hiệu giá trị Tag thời gian thực cho MCU Reference Simulator.
/// Chỉ tương tác với vùng nhớ thanh ghi Tag (0x0900..0x09FF).
/// Tuyệt đối KHÔNG chứa Rule Engine / logic PLC trigger-action nội bộ.
/// </summary>
public sealed class RuntimeTagGenerator
{
    private readonly SimulatorRegisterMemory _memory;

    public RuntimeTagGenerator(SimulatorRegisterMemory memory)
    {
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
    }

    /// <summary>
    /// Đặt giá trị Tag (int32) vào 2 thanh ghi tương ứng tại 0x0900 + tagIndex * 2.
    /// </summary>
    public void SetTagValue(ushort tagIndex, int value)
    {
        if (tagIndex >= ModbusRegisterMap.MaxRuntimeTags)
        {
            throw new ArgumentOutOfRangeException(nameof(tagIndex), $"Tag index {tagIndex} exceeds maximum capacity {ModbusRegisterMap.MaxRuntimeTags}.");
        }

        ushort baseAddr = (ushort)(ModbusRegisterMap.RuntimeTagValuesBaseAddress + (tagIndex * ModbusRegisterMap.RegistersPerRuntimeTag));
        Span<ushort> regSpan = stackalloc ushort[2];
        RegisterCodec.EncodeRuntimeTagValue(value, regSpan);
        _memory.InternalWrite(baseAddr, regSpan[0]);
        _memory.InternalWrite((ushort)(baseAddr + 1), regSpan[1]);
    }

    /// <summary>
    /// Đọc giá trị hiện tại của Tag (int32).
    /// </summary>
    public int GetTagValue(ushort tagIndex)
    {
        if (tagIndex >= ModbusRegisterMap.MaxRuntimeTags)
        {
            throw new ArgumentOutOfRangeException(nameof(tagIndex), $"Tag index {tagIndex} exceeds maximum capacity {ModbusRegisterMap.MaxRuntimeTags}.");
        }

        ushort baseAddr = (ushort)(ModbusRegisterMap.RuntimeTagValuesBaseAddress + (tagIndex * ModbusRegisterMap.RegistersPerRuntimeTag));
        ReadOnlySpan<ushort> regSpan = _memory.InternalSpan(baseAddr, 2);
        return RegisterCodec.DecodeRuntimeTagValue(regSpan);
    }

    /// <summary>
    /// Đảo trạng thái Tag kiểu Boolean (0 <-> 1).
    /// </summary>
    public void ToggleTag(ushort tagIndex)
    {
        int current = GetTagValue(tagIndex);
        SetTagValue(tagIndex, current == 0 ? 1 : 0);
    }

    /// <summary>
    /// Sinh tín hiệu mô phỏng ngõ vào phục vụ hiển thị Watch Table và Canvas Debugging (Demo mode).
    /// </summary>
    public void GenerateDemoPattern(int step)
    {
        // DI0: Đổi trạng thái mỗi 5 bước
        SetTagValue(0, (step / 5) % 2);

        // DI1: Đổi trạng thái mỗi 3 bước
        SetTagValue(1, (step / 3) % 2);

        // AI0 (Áp suất): Dao động sóng sin giả lập 40..85 bar
        int pressure = (int)(62 + 20 * Math.Sin(step * 0.2));
        SetTagValue(16, pressure);

        // AI1 (Nhiệt độ): Dao động 35..65°C
        int temp = (int)(50 + 15 * Math.Cos(step * 0.15));
        SetTagValue(17, temp);
    }
}
