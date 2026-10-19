using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimplePLC.Protocol.Cryptography;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.Services;

namespace SimplePLC.Studio.ViewModels;

/// <summary>
/// Phần tử đại diện cho một kênh chấp hành trong bảng ánh xạ địa chỉ Modbus (Sort-to-Light / Cụm van / Tháp đèn).
/// </summary>
public class ActuatorChannelMappingItem
{
    public ushort ChannelIndex { get; set; }
    public string ChannelDisplay => $"CH {ChannelIndex:D2}";
    public string CommandRegHex { get; set; } = string.Empty;
    public string ParamRegHex { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Mục phân tích một trường trong khung tin Modbus RTU (Slave ID, FC, Addr, Data, CRC).
/// </summary>
public class ModbusFrameFieldBreakdown
{
    public string FieldName { get; set; } = string.Empty;
    public string RawHex { get; set; } = string.Empty;
    public string Interpretation { get; set; } = string.Empty;
}

/// <summary>
/// ViewModel phục vụ hộp thoại cấu hình khối MB_WRITE (Modbus Master Write).
/// Hỗ trợ 2 chế độ:
/// 1. Channel-Indexed Target (Sort-to-Light, Cụm van, Tháp đèn) với bảng ánh xạ địa chỉ và tùy chọn Exclusive.
/// 2. Direct Registers (Ghi trực tiếp thanh ghi Modbus truyền thống FC 05, 06, 15, 16).
/// Cung cấp giao diện kỹ thuật công nghiệp chuẩn Slate Gray, phân tích khung tin Modbus RTU rõ ràng.
/// </summary>
public partial class ModbusWriteConfigDialogViewModel : ObservableObject
{
    public ModbusWriteNodeViewModel Node { get; }
    public TagCatalogViewModel TagCatalog { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryBadgeText))]
    private byte _slaveId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryBadgeText))]
    [NotifyPropertyChangedFor(nameof(IsChannelIndexedMode))]
    [NotifyPropertyChangedFor(nameof(IsDirectRegistersMode))]
    private ModbusWriteMode _writeMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryBadgeText))]
    private ModbusMasterWriteFunctionCode _functionCode;

    // Direct mode settings
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryBadgeText))]
    private ushort _startAddress;

    [ObservableProperty]
    private ushort _pointCount;

    // Channel-Indexed mode settings
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryBadgeText))]
    private ushort _baseAddressCommand;

    [ObservableProperty]
    private ushort _baseAddressParam;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryBadgeText))]
    private ushort _channelCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryBadgeText))]
    private bool _isExclusiveChannel;

    // Trigger & Timing
    [ObservableProperty]
    private ModbusWriteTriggerMode _triggerMode;

    [ObservableProperty]
    private uint _cyclicIntervalMs;

    [ObservableProperty]
    private uint _timeoutMs;

    public bool IsChannelIndexedMode => WriteMode == ModbusWriteMode.ChannelIndexed;
    public bool IsDirectRegistersMode => WriteMode == ModbusWriteMode.DirectRegisters;

    public string SummaryBadgeText => WriteMode == ModbusWriteMode.ChannelIndexed
        ? $"Slave {SlaveId} | {ChannelCount} Channels | BaseCmd 0x{BaseAddressCommand:X4}{(IsExclusiveChannel ? " [Exclusive]" : "")}"
        : $"Slave {SlaveId} | FC{(byte)FunctionCode:D2} | Addr {StartAddress} ({Node.Pins.Count} Pins)";

    public bool HasPins => Node.Pins.Count > 0;
    public bool HasNoPins => Node.Pins.Count == 0;

    // Actuator Channel Mappings Table (Channel-Indexed mode)
    public ObservableCollection<ActuatorChannelMappingItem> ChannelMappings { get; } = new();

    // Frame Preview & Simulator Fields
    [ObservableProperty]
    private ushort _simChannel = 1;

    [ObservableProperty]
    private ushort _simCommand = 1;

    [ObservableProperty]
    private string _simParamHex = "00FF00"; // Mặc định: Green (Xanh lá)

    [ObservableProperty]
    private ushort _simDirectValue = 100;

    [ObservableProperty]
    private string _simStatusMessage = string.Empty;

    [ObservableProperty]
    private string _simTxFrameHex = string.Empty;

    [ObservableProperty]
    private string _simParamTxFrameHex = string.Empty;

    [ObservableProperty]
    private bool _hasParamFrame = false;

    [ObservableProperty]
    private string _simGeneratedFrames = string.Empty;

    [ObservableProperty]
    private string _simExplanation = string.Empty;

    public ObservableCollection<ModbusFrameFieldBreakdown> FrameBreakdownItems { get; } = new();

    public IReadOnlyList<OptionItem<ModbusWriteMode>> AvailableWriteModes { get; } = new List<OptionItem<ModbusWriteMode>>
    {
        new(ModbusWriteMode.ChannelIndexed, LocalizationService.Tr("ModbusWriteModeIndexed") ?? "Channel-Indexed Target"),
        new(ModbusWriteMode.DirectRegisters, LocalizationService.Tr("ModbusWriteModeDirect") ?? "Direct Registers")
    };

    public IReadOnlyList<OptionItem<ModbusMasterWriteFunctionCode>> AvailableFunctionCodes { get; } = new List<OptionItem<ModbusMasterWriteFunctionCode>>
    {
        new(ModbusMasterWriteFunctionCode.WriteSingleRegister, LocalizationService.Tr("ModbusWriteFc06") ?? "FC 06: Write Register (4x)"),
        new(ModbusMasterWriteFunctionCode.WriteMultipleRegisters, LocalizationService.Tr("ModbusWriteFc16") ?? "FC 16: Write Multiple (4x)"),
        new(ModbusMasterWriteFunctionCode.WriteSingleCoil, LocalizationService.Tr("ModbusWriteFc05") ?? "FC 05: Write Coil (0x)"),
        new(ModbusMasterWriteFunctionCode.WriteMultipleCoils, LocalizationService.Tr("ModbusWriteFc15") ?? "FC 15: Write Multiple Coils (0x)")
    };

    public IReadOnlyList<OptionItem<ModbusWriteTriggerMode>> AvailableTriggerModes { get; } = new List<OptionItem<ModbusWriteTriggerMode>>
    {
        new(ModbusWriteTriggerMode.OnTriggerRisingEdge, LocalizationService.Tr("ModbusWriteTrigEdge") ?? "Rising Edge (Trig)"),
        new(ModbusWriteTriggerMode.OnDataChange, LocalizationService.Tr("ModbusWriteTrigChange") ?? "On Data Change"),
        new(ModbusWriteTriggerMode.Cyclic, LocalizationService.Tr("ModbusWriteTrigCyclic") ?? "Cyclic (Periodic)")
    };

    public IReadOnlyList<OptionItem<ModbusDataType>> AvailableDataTypes { get; } = new List<OptionItem<ModbusDataType>>
    {
        new(ModbusDataType.Int16, "INT16 (Signed)"),
        new(ModbusDataType.UInt16, "UINT16 (Unsigned)"),
        new(ModbusDataType.Real, "REAL (Float32)"),
        new(ModbusDataType.Int32, "INT32 (Signed)"),
        new(ModbusDataType.UInt32, "UINT32 (Unsigned)"),
        new(ModbusDataType.Bool, "BOOL (0 / 1)")
    };

    public IReadOnlyList<OptionItem<ModbusEndianness>> AvailableEndianness { get; } = new List<OptionItem<ModbusEndianness>>
    {
        new(ModbusEndianness.BigEndian_ABCD, "Big-Endian (ABCD)"),
        new(ModbusEndianness.MidBig_CDAB, "Word-Swap (CDAB)"),
        new(ModbusEndianness.ByteSwap_BADC, "Byte-Swap (BADC)"),
        new(ModbusEndianness.LittleEndian_DCBA, "Little-Endian (DCBA)")
    };

    public ModbusWriteConfigDialogViewModel(ModbusWriteNodeViewModel node, TagCatalogViewModel tagCatalog)
    {
        Node = node ?? throw new ArgumentNullException(nameof(node));
        TagCatalog = tagCatalog;

        _slaveId = node.SlaveId;
        _writeMode = node.WriteMode;
        _functionCode = node.FunctionCode;
        _startAddress = node.StartAddress;
        _pointCount = Math.Max((ushort)1, node.PointCount);
        _baseAddressCommand = node.BaseAddressCommand;
        _baseAddressParam = node.BaseAddressParam;
        _channelCount = Math.Max((ushort)1, node.ChannelCount);
        _isExclusiveChannel = node.IsExclusiveChannel;
        _triggerMode = node.TriggerMode;
        _cyclicIntervalMs = node.CyclicIntervalMs;
        _timeoutMs = node.TimeoutMs;

        foreach (var pin in node.Pins)
        {
            pin.PropertyChanged += OnPinPropertyChanged;
        }

        if (_writeMode == ModbusWriteMode.DirectRegisters && node.Pins.Count == 0)
        {
            AddPin();
        }

        UpdateChannelMappings();
        GenerateFramePreview();
    }

    private void OnPinPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(SummaryBadgeText));
        GenerateFramePreview();
    }

    partial void OnSimDirectValueChanged(ushort value)
    {
        GenerateFramePreview();
    }

    partial void OnWriteModeChanged(ModbusWriteMode value)
    {
        if (value == ModbusWriteMode.DirectRegisters && Node.Pins.Count == 0)
        {
            AddPin();
        }
        OnPropertyChanged(nameof(IsChannelIndexedMode));
        OnPropertyChanged(nameof(IsDirectRegistersMode));
        OnPropertyChanged(nameof(SummaryBadgeText));
        UpdateChannelMappings();
        GenerateFramePreview();
    }

    partial void OnChannelCountChanged(ushort value)
    {
        UpdateChannelMappings();
        GenerateFramePreview();
    }

    partial void OnBaseAddressCommandChanged(ushort value)
    {
        UpdateChannelMappings();
        GenerateFramePreview();
    }

    partial void OnBaseAddressParamChanged(ushort value)
    {
        UpdateChannelMappings();
        GenerateFramePreview();
    }

    partial void OnSlaveIdChanged(byte value)
    {
        GenerateFramePreview();
    }

    partial void OnFunctionCodeChanged(ModbusMasterWriteFunctionCode value)
    {
        GenerateFramePreview();
    }

    partial void OnStartAddressChanged(ushort value)
    {
        GenerateFramePreview();
    }

    partial void OnPointCountChanged(ushort value)
    {
        GenerateFramePreview();
    }

    public void UpdateChannelMappings()
    {
        ChannelMappings.Clear();
        int count = Math.Min((int)ChannelCount, 32);
        bool isVi = LocalizationService.Instance.IsVietnamese;
        for (int i = 1; i <= count; i++)
        {
            ushort cmdAddr = (ushort)(BaseAddressCommand + i - 1);
            ushort paramAddr = (ushort)(BaseAddressParam + (i - 1) * 2);
            ChannelMappings.Add(new ActuatorChannelMappingItem
            {
                ChannelIndex = (ushort)i,
                CommandRegHex = $"0x{cmdAddr:X4} ({cmdAddr})",
                ParamRegHex = $"0x{paramAddr:X4} ({paramAddr})",
                Description = isVi ? $"Kênh điều khiển {i}" : $"Actuator Channel {i}"
            });
        }
    }

    [RelayCommand]
    public void AddPin()
    {
        if (Node.Pins.Count >= 8) return;

        ushort nextOffset = 0;
        if (Node.Pins.Count > 0)
        {
            var last = Node.Pins.Last();
            int span = (last.DataType is ModbusDataType.Real or ModbusDataType.Int32 or ModbusDataType.UInt32) ? 2 : 1;
            nextOffset = (ushort)(last.RegOffset + span);
        }

        var newPin = new ModbusPinBindingViewModel
        {
            Label = $"In_{Node.Pins.Count + 1}",
            RegOffset = nextOffset,
            DataType = ModbusDataType.Int16,
            Endianness = ModbusEndianness.BigEndian_ABCD,
            Multiplier = 1.0f
        };
        newPin.PropertyChanged += OnPinPropertyChanged;
        Node.Pins.Add(newPin);
        OnPropertyChanged(nameof(SummaryBadgeText));
        OnPropertyChanged(nameof(HasPins));
        OnPropertyChanged(nameof(HasNoPins));
        GenerateFramePreview();
    }

    [RelayCommand]
    public void RemovePin(ModbusPinBindingViewModel pin)
    {
        if (pin != null && Node.Pins.Contains(pin))
        {
            pin.PropertyChanged -= OnPinPropertyChanged;
            Node.Pins.Remove(pin);
            OnPropertyChanged(nameof(SummaryBadgeText));
            OnPropertyChanged(nameof(HasPins));
            OnPropertyChanged(nameof(HasNoPins));
            GenerateFramePreview();
        }
    }

    [RelayCommand]
    public void GenerateFramePreview()
    {
        bool isVi = LocalizationService.Instance.IsVietnamese;
        FrameBreakdownItems.Clear();

        if (WriteMode == ModbusWriteMode.ChannelIndexed)
        {
            ushort ch = Math.Clamp(SimChannel, (ushort)1, Math.Max((ushort)1, ChannelCount));
            ushort cmd = SimCommand;
            
            // Clean simParamHex
            string paramHex = (SimParamHex ?? string.Empty).Replace(" ", "").Replace("0x", "").Trim();
            uint paramVal = 0;
            if (!uint.TryParse(paramHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out paramVal))
            {
                paramVal = 0;
            }

            ushort cmdRegAddr = (ushort)(BaseAddressCommand + ch - 1);
            ushort paramRegAddr = (ushort)(BaseAddressParam + ((ch - 1) * 2));

            var frames = new List<string>();

            // Frame 1: Write Single Register FC06 for Command
            byte[] cmdFrame = CreateFc06Frame(SlaveId, cmdRegAddr, cmd);
            string cmdFrameHex = FormatHex(cmdFrame);
            frames.Add(isVi 
                ? $"1. Gói lệnh điều khiển FC06 (FC 06) -> Thanh ghi 0x{cmdRegAddr:X4} (Giá trị = {cmd}):" 
                : $"1. Command Telegram FC06 (FC 06) -> Register 0x{cmdRegAddr:X4} (Value = {cmd}):");
            frames.Add(isVi ? $"   Khung truyền: {cmdFrameHex}" : $"   TX Frame: {cmdFrameHex}");
            SimTxFrameHex = cmdFrameHex;

            // Frame 2: Write Multiple Registers FC16 for Param (RGB 24-bit / 32-bit: 2 registers)
            ushort regHigh = (ushort)((paramVal >> 16) & 0xFFFF);
            ushort regLow = (ushort)(paramVal & 0xFFFF);
            byte[] paramFrame = CreateFc16Frame(SlaveId, paramRegAddr, new[] { regHigh, regLow });
            string paramFrameHex = FormatHex(paramFrame);
            frames.Add(isVi 
                ? $"2. Gói tham số/màu FC16 (FC 16) -> Thanh ghi 0x{paramRegAddr:X4} (Giá trị = 0x{paramVal:X6}):" 
                : $"2. Parameter Telegram FC16 (FC 16) -> Register 0x{paramRegAddr:X4} (Value = 0x{paramVal:X6}):");
            frames.Add(isVi ? $"   Khung truyền: {paramFrameHex}" : $"   TX Frame: {paramFrameHex}");
            SimParamTxFrameHex = paramFrameHex;
            HasParamFrame = true;

            SimGeneratedFrames = string.Join(Environment.NewLine, frames);
            SimStatusMessage = LocalizationService.Tr("ModbusWriteStatusSuccess") ?? "✔ Valid Modbus RTU frame (CRC-16 verified)";

            // Breakdown
            ushort crcCmd = (ushort)(cmdFrame[6] | (cmdFrame[7] << 8));
            FrameBreakdownItems.Add(new ModbusFrameFieldBreakdown
            {
                FieldName = isVi ? "Địa chỉ thiết bị" : "Device Address",
                RawHex = $"{SlaveId:X2}",
                Interpretation = isVi ? $"Thiết bị số {SlaveId}" : $"Device {SlaveId}"
            });
            FrameBreakdownItems.Add(new ModbusFrameFieldBreakdown
            {
                FieldName = isVi ? "Mã hàm" : "Function Code",
                RawHex = "06",
                Interpretation = isVi ? "FC 06: Ghi một thanh ghi" : "FC 06: Write Single Register"
            });
            FrameBreakdownItems.Add(new ModbusFrameFieldBreakdown
            {
                FieldName = isVi ? "Thanh ghi lệnh" : "Command Register",
                RawHex = $"{cmdFrame[2]:X2} {cmdFrame[3]:X2}",
                Interpretation = isVi ? $"0x{cmdRegAddr:X4} (Kênh {ch})" : $"0x{cmdRegAddr:X4} (Channel {ch})"
            });
            FrameBreakdownItems.Add(new ModbusFrameFieldBreakdown
            {
                FieldName = isVi ? "Dữ liệu lệnh" : "Command Value",
                RawHex = $"{cmdFrame[4]:X2} {cmdFrame[5]:X2}",
                Interpretation = isVi ? $"Giá trị = {cmd}" : $"Value = {cmd}"
            });
            FrameBreakdownItems.Add(new ModbusFrameFieldBreakdown
            {
                FieldName = isVi ? "Mã kiểm tra" : "CRC Checksum",
                RawHex = $"{cmdFrame[6]:X2} {cmdFrame[7]:X2}",
                Interpretation = isVi ? $"0x{crcCmd:X4} (Chính xác)" : $"0x{crcCmd:X4} (Valid)"
            });

            SimExplanation = isVi
                ? $"✔ Chế độ điều khiển theo chỉ số kênh (Kênh {ch}/{ChannelCount}):" + Environment.NewLine +
                  $"• Lệnh Command={cmd} ghi vào thanh ghi 0x{cmdRegAddr:X4}." + Environment.NewLine +
                  $"• Tham số Param=0x{paramVal:X6} ghi vào thanh ghi 0x{paramRegAddr:X4} (2 thanh ghi RGB/DWord)." + Environment.NewLine +
                  (IsExclusiveChannel 
                      ? $"• Chế độ chọn một (Exclusive): Tự động xóa các kênh khác (1..{ch - 1}, {ch + 1}..{ChannelCount}) về 0." 
                      : "• Chế độ đa kênh độc lập (Independent): Các kênh hoạt động riêng biệt, không tự ngắt lẫn nhau.")
                : $"✔ Channel-Indexed Target Mode (Channel {ch}/{ChannelCount}):" + Environment.NewLine +
                  $"• Command={cmd} written to register 0x{cmdRegAddr:X4}." + Environment.NewLine +
                  $"• Param=0x{paramVal:X6} written to register 0x{paramRegAddr:X4} (2 registers RGB/DWord)." + Environment.NewLine +
                  (IsExclusiveChannel 
                      ? $"• Exclusive Mode: Automatically clears other channels (1..{ch - 1}, {ch + 1}..{ChannelCount}) to 0." 
                      : "• Independent Mode: Channels operate independently without clearing other channels.");
        }
        else
        {
            // Direct Registers Mode
            var frames = new List<string>();
            byte[] frame;

            switch (FunctionCode)
            {
                case ModbusMasterWriteFunctionCode.WriteSingleRegister:
                    frame = CreateFc06Frame(SlaveId, StartAddress, 100);
                    frames.Add(isVi 
                        ? $"Gói ghi một thanh ghi FC06 (FC 06) -> Addr 0x{StartAddress:X4} (Giá trị mẫu = 100):" 
                        : $"FC06 Write Single Register (FC 06) -> Addr 0x{StartAddress:X4} (Sample Value = 100):");
                    frames.Add(isVi ? $"Khung truyền: {FormatHex(frame)}" : $"TX Frame: {FormatHex(frame)}");
                    SimTxFrameHex = FormatHex(frame);
                    break;

                case ModbusMasterWriteFunctionCode.WriteMultipleRegisters:
                    ushort count = Math.Max((ushort)1, PointCount);
                    var sampleVals = Enumerable.Range(0, count).Select(i => (ushort)(100 + i * 10)).ToArray();
                    frame = CreateFc16Frame(SlaveId, StartAddress, sampleVals);
                    frames.Add(isVi 
                        ? $"Gói ghi nhiều thanh ghi FC16 (FC 16) -> Addr 0x{StartAddress:X4}, Count={count}:" 
                        : $"FC16 Write Multiple Registers (FC 16) -> Addr 0x{StartAddress:X4}, Count={count}:");
                    frames.Add(isVi ? $"Khung truyền: {FormatHex(frame)}" : $"TX Frame: {FormatHex(frame)}");
                    SimTxFrameHex = FormatHex(frame);
                    break;

                case ModbusMasterWriteFunctionCode.WriteSingleCoil:
                    frame = CreateFc05Frame(SlaveId, StartAddress, true);
                    frames.Add(isVi 
                        ? $"Gói ghi một cuộn dây FC05 (FC 05) -> Addr 0x{StartAddress:X4} (Trạng thái = BẬT):" 
                        : $"FC05 Write Single Coil (FC 05) -> Addr 0x{StartAddress:X4} (State = ON):");
                    frames.Add(isVi ? $"Khung truyền: {FormatHex(frame)}" : $"TX Frame: {FormatHex(frame)}");
                    SimTxFrameHex = FormatHex(frame);
                    break;

                case ModbusMasterWriteFunctionCode.WriteMultipleCoils:
                    frame = CreateFc15Frame(SlaveId, StartAddress, PointCount > 0 ? PointCount : (ushort)1, new byte[] { 0x01 });
                    frames.Add(isVi 
                        ? $"Gói ghi nhiều cuộn dây FC15 (FC 15) -> Addr 0x{StartAddress:X4}, Count={PointCount}:" 
                        : $"FC15 Write Multiple Coils (FC 15) -> Addr 0x{StartAddress:X4}, Count={PointCount}:");
                    frames.Add(isVi ? $"Khung truyền: {FormatHex(frame)}" : $"TX Frame: {FormatHex(frame)}");
                    SimTxFrameHex = FormatHex(frame);
                    break;

                default:
                    frame = Array.Empty<byte>();
                    break;
            }

            HasParamFrame = false;
            SimParamTxFrameHex = string.Empty;
            SimGeneratedFrames = string.Join(Environment.NewLine, frames);
            SimStatusMessage = LocalizationService.Tr("ModbusWriteStatusSuccess") ?? "✔ Valid Modbus RTU frame (CRC-16 verified)";

            if (frame.Length >= 6)
            {
                int crcIdx = frame.Length - 2;
                ushort crc = (ushort)(frame[crcIdx] | (frame[crcIdx + 1] << 8));
                FrameBreakdownItems.Add(new ModbusFrameFieldBreakdown
                {
                    FieldName = isVi ? "Địa chỉ thiết bị" : "Device Address",
                    RawHex = $"{SlaveId:X2}",
                    Interpretation = isVi ? $"Thiết bị số {SlaveId}" : $"Device {SlaveId}"
                });
                FrameBreakdownItems.Add(new ModbusFrameFieldBreakdown
                {
                    FieldName = isVi ? "Mã hàm" : "Function Code",
                    RawHex = $"{frame[1]:X2}",
                    Interpretation = $"FC {(byte)FunctionCode:D2}"
                });
                FrameBreakdownItems.Add(new ModbusFrameFieldBreakdown
                {
                    FieldName = isVi ? "Địa chỉ bắt đầu" : "Start Address",
                    RawHex = $"{frame[2]:X2} {frame[3]:X2}",
                    Interpretation = $"0x{StartAddress:X4} ({StartAddress})"
                });
                FrameBreakdownItems.Add(new ModbusFrameFieldBreakdown
                {
                    FieldName = isVi ? "Mã kiểm tra" : "CRC Checksum",
                    RawHex = $"{frame[crcIdx]:X2} {frame[crcIdx + 1]:X2}",
                    Interpretation = isVi ? $"0x{crc:X4} (Chính xác)" : $"0x{crc:X4} (Valid)"
                });
            }

            SimExplanation = isVi
                ? $"✔ Chế độ ghi trực tiếp thanh ghi: Khối ghi dữ liệu của {Node.Pins.Count} chân vào thiết bị {SlaveId} bằng hàm FC {(byte)FunctionCode:D2}."
                : $"✔ Direct Registers Mode: The block writes data from {Node.Pins.Count} data pins directly to Device {SlaveId} via FC {(byte)FunctionCode:D2}.";
        }
    }

    private static byte[] CreateFc06Frame(byte slaveId, ushort address, ushort value)
    {
        byte[] frame = new byte[8];
        frame[0] = slaveId;
        frame[1] = 0x06;
        frame[2] = (byte)((address >> 8) & 0xFF);
        frame[3] = (byte)(address & 0xFF);
        frame[4] = (byte)((value >> 8) & 0xFF);
        frame[5] = (byte)(value & 0xFF);

        ushort crc = Crc16Modbus.Compute(frame.AsSpan(0, 6));
        frame[6] = (byte)(crc & 0xFF);
        frame[7] = (byte)((crc >> 8) & 0xFF);
        return frame;
    }

    private static byte[] CreateFc16Frame(byte slaveId, ushort address, ushort[] values)
    {
        byte byteCount = (byte)(values.Length * 2);
        byte[] frame = new byte[7 + byteCount + 2];
        frame[0] = slaveId;
        frame[1] = 0x10; // FC 16
        frame[2] = (byte)((address >> 8) & 0xFF);
        frame[3] = (byte)(address & 0xFF);
        frame[4] = (byte)((values.Length >> 8) & 0xFF);
        frame[5] = (byte)(values.Length & 0xFF);
        frame[6] = byteCount;

        for (int i = 0; i < values.Length; i++)
        {
            frame[7 + i * 2] = (byte)((values[i] >> 8) & 0xFF);
            frame[7 + i * 2 + 1] = (byte)(values[i] & 0xFF);
        }

        int payloadLen = 7 + byteCount;
        ushort crc = Crc16Modbus.Compute(frame.AsSpan(0, payloadLen));
        frame[payloadLen] = (byte)(crc & 0xFF);
        frame[payloadLen + 1] = (byte)((crc >> 8) & 0xFF);
        return frame;
    }

    private static byte[] CreateFc05Frame(byte slaveId, ushort address, bool state)
    {
        byte[] frame = new byte[8];
        frame[0] = slaveId;
        frame[1] = 0x05;
        frame[2] = (byte)((address >> 8) & 0xFF);
        frame[3] = (byte)(address & 0xFF);
        frame[4] = state ? (byte)0xFF : (byte)0x00;
        frame[5] = 0x00;

        ushort crc = Crc16Modbus.Compute(frame.AsSpan(0, 6));
        frame[6] = (byte)(crc & 0xFF);
        frame[7] = (byte)((crc >> 8) & 0xFF);
        return frame;
    }

    private static byte[] CreateFc15Frame(byte slaveId, ushort address, ushort coilCount, byte[] coilBytes)
    {
        byte byteCount = (byte)coilBytes.Length;
        byte[] frame = new byte[7 + byteCount + 2];
        frame[0] = slaveId;
        frame[1] = 0x0F; // FC 15
        frame[2] = (byte)((address >> 8) & 0xFF);
        frame[3] = (byte)(address & 0xFF);
        frame[4] = (byte)((coilCount >> 8) & 0xFF);
        frame[5] = (byte)(coilCount & 0xFF);
        frame[6] = byteCount;

        Array.Copy(coilBytes, 0, frame, 7, byteCount);

        int payloadLen = 7 + byteCount;
        ushort crc = Crc16Modbus.Compute(frame.AsSpan(0, payloadLen));
        frame[payloadLen] = (byte)(crc & 0xFF);
        frame[payloadLen + 1] = (byte)((crc >> 8) & 0xFF);
        return frame;
    }

    private static string FormatHex(byte[] bytes)
    {
        return string.Join(" ", bytes.Select(b => b.ToString("X2")));
    }

    [RelayCommand]
    public void Apply()
    {
        Node.SlaveId = Math.Clamp(SlaveId, (byte)1, (byte)247);
        Node.WriteMode = WriteMode;
        Node.FunctionCode = FunctionCode;
        Node.StartAddress = StartAddress;
        Node.PointCount = Math.Max((ushort)1, PointCount);
        Node.BaseAddressCommand = BaseAddressCommand;
        Node.BaseAddressParam = BaseAddressParam;
        Node.ChannelCount = Math.Max((ushort)1, ChannelCount);
        Node.IsExclusiveChannel = IsExclusiveChannel;
        Node.TriggerMode = TriggerMode;
        Node.CyclicIntervalMs = Math.Max(50u, CyclicIntervalMs);
        Node.TimeoutMs = Math.Max(50u, TimeoutMs);
        Node.SyncConnectors();
    }
}
