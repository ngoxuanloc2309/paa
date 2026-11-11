using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;

namespace SimplePLC.Studio.ViewModels;

/// <summary>
/// Đại diện cho một lựa chọn kênh tần số Zigbee 2.4 GHz kèm nhãn hiển thị trực quan.
/// </summary>
/// <param name="Channel">Số hiệu kênh RF (11 .. 26).</param>
/// <param name="DisplayName">Tên hiển thị kèm nhãn khuyến nghị nếu là kênh sạch.</param>
public sealed record ZigbeeChannelOption(ushort Channel, string DisplayName)
{
    /// <inheritdoc/>
    public override string ToString() => DisplayName;
}

/// <summary>
/// ViewModel quản lý toàn bộ trạng thái, giao tiếp và máy trạng thái nhập liệu (Input FSM)
/// của phân hệ truyền thông không dây Zigbee Modbus (SPLC-SPEC-ZIGBEE-006).
/// Tuân thủ học thuyết First Principles (Axiom 1, 2, 3, 4) và các tiêu chuẩn IEC 61508-3 / IEC 62443.
/// </summary>
public partial class ZigbeeConfigViewModel : ObservableObject
{
    private byte _committedStationId = 1;
    private ushort _committedPanId = 0x1A2B;

    /// <summary>
    /// Khởi tạo ViewModel cấu hình Zigbee với giá trị an toàn mặc định.
    /// </summary>
    public ZigbeeConfigViewModel()
    {
        _stationIdText = _committedStationId.ToString(CultureInfo.InvariantCulture);
        _panIdHex = _committedPanId.ToString("X4", CultureInfo.InvariantCulture);
        InitializeChannels();

        LocalizationService.Instance.LanguageChanged += () =>
        {
            InitializeChannels();
            OnPropertyChanged(nameof(RoleDisplayText));
            OnPropertyChanged(nameof(ChannelHintText));
            OnPropertyChanged(nameof(RssiDisplayText));
            OnPropertyChanged(nameof(LinkStateDisplayText));
            OnPropertyChanged(nameof(StatusSummaryBadge));
        };
    }

    // ==========================================
    // 1. CẤU HÌNH TỔNG QUAN & BẬT/TẮT
    // ==========================================

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusSummaryBadge))]
    private bool _isEnabled = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRoleMaster))]
    [NotifyPropertyChangedFor(nameof(IsRoleSlave))]
    [NotifyPropertyChangedFor(nameof(RoleDisplayText))]
    private ZigbeeRole _deviceRole = ZigbeeRole.Slave;

    /// <summary>
    /// Cho biết thiết bị có đang hoạt động ở vai trò Master (Coordinator) hay không.
    /// </summary>
    public bool IsRoleMaster
    {
        get => DeviceRole == ZigbeeRole.Master;
        set
        {
            if (value) DeviceRole = ZigbeeRole.Master;
        }
    }

    /// <summary>
    /// Cho biết thiết bị có đang hoạt động ở vai trò Slave (Router) hay không.
    /// </summary>
    public bool IsRoleSlave
    {
        get => DeviceRole == ZigbeeRole.Slave;
        set
        {
            if (value) DeviceRole = ZigbeeRole.Slave;
        }
    }

    /// <summary>
    /// Chuỗi văn bản hiển thị vai trò hiện tại.
    /// </summary>
    public string RoleDisplayText => DeviceRole == ZigbeeRole.Master
        ? (LocalizationService.Instance.IsVietnamese ? "Master (Coordinator)" : "Master (Coordinator)")
        : (LocalizationService.Instance.IsVietnamese ? "Slave (Router)" : "Slave (Router)");

    // ==========================================
    // 2. MÁY TRẠNG THÁI NHẬP LIỆU: ĐỊA CHỈ TRẠM (STATION ID)
    // ==========================================

    [ObservableProperty]
    private string _stationIdText = "1";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StationIdBadge))]
    private byte _stationId = 1;

    [ObservableProperty]
    private bool _hasStationIdError = false;

    [ObservableProperty]
    private string _stationIdErrorMessage = string.Empty;

    /// <summary>
    /// Huy hiệu hiển thị địa chỉ trạm thu gọn.
    /// </summary>
    public string StationIdBadge => $"ID {StationId}";

    partial void OnStationIdTextChanged(string value)
    {
        ValidateStationIdDrafting(value);
    }

    private void ValidateStationIdDrafting(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            HasStationIdError = false;
            StationIdErrorMessage = string.Empty;
            return;
        }

        if (byte.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out byte val))
        {
            if (val == 236 || val == 237)
            {
                HasStationIdError = true;
                StationIdErrorMessage = LocalizationService.Instance.IsVietnamese
                    ? "Cấm sử dụng ID 236 (0xEC) và 237 (0xED) vì trùng mã lệnh cấu hình module RF."
                    : "Station IDs 236 (0xEC) and 237 (0xED) are restricted by RF module firmware.";
                return;
            }

            if (val < 1 || val > 247)
            {
                HasStationIdError = true;
                StationIdErrorMessage = LocalizationService.Instance.IsVietnamese
                    ? "Địa chỉ trạm hợp lệ trong khoảng 1 .. 247."
                    : "Station ID must be between 1 and 247.";
                return;
            }

            HasStationIdError = false;
            StationIdErrorMessage = string.Empty;
        }
        else
        {
            HasStationIdError = true;
            StationIdErrorMessage = LocalizationService.Instance.IsVietnamese
                ? "Giá trị phải là số nguyên từ 1 đến 247."
                : "Value must be an integer between 1 and 247.";
        }
    }

    /// <summary>
    /// Giải quyết dứt điểm trạng thái nhập liệu khi mất focus (LostFocus / Enter Commit).
    /// </summary>
    [RelayCommand]
    public void CommitStationId()
    {
        string raw = StationIdText.Trim();
        if (byte.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out byte val) &&
            val >= 1 && val <= 247 && val != 236 && val != 237)
        {
            _committedStationId = val;
            StationId = val;
            StationIdText = val.ToString(CultureInfo.InvariantCulture);
            HasStationIdError = false;
            StationIdErrorMessage = string.Empty;
        }
        else
        {
            // Auto-Revert về giá trị cam kết hợp lệ gần nhất
            StationIdText = _committedStationId.ToString(CultureInfo.InvariantCulture);
            StationId = _committedStationId;
            HasStationIdError = false;
            StationIdErrorMessage = string.Empty;
        }
    }

    // ==========================================
    // 3. MÃ MẠNG PAN ID & KÊNH TẦN SỐ (CHANNEL)
    // ==========================================

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PanIdDisplay))]
    private string _panIdHex = "1A2B";

    [ObservableProperty]
    private ushort _panId = 0x1A2B;

    /// <summary>
    /// Chuỗi hiển thị PAN ID kèm tiền tố 0x.
    /// </summary>
    public string PanIdDisplay => $"0x{PanIdHex.ToUpperInvariant()}";

    /// <summary>
    /// Giải quyết dứt điểm và chuẩn hóa chuỗi Hex của PAN ID khi mất focus.
    /// </summary>
    [RelayCommand]
    public void CommitPanId()
    {
        string raw = PanIdHex.Trim();
        if (raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            raw = raw.Substring(2);
        }

        if (ushort.TryParse(raw, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ushort val) &&
            val >= 0x0001 && val <= 0xFFFE)
        {
            _committedPanId = val;
            PanId = val;
            PanIdHex = val.ToString("X4", CultureInfo.InvariantCulture);
        }
        else
        {
            // Auto-Revert
            PanIdHex = _committedPanId.ToString("X4", CultureInfo.InvariantCulture);
            PanId = _committedPanId;
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCleanChannel))]
    [NotifyPropertyChangedFor(nameof(ChannelHintText))]
    private ushort _channel = 15;

    /// <summary>
    /// Danh sách các kênh tần số 2.4 GHz IEEE 802.15.4 (11 .. 26).
    /// </summary>
    public ObservableCollection<ZigbeeChannelOption> AvailableChannels { get; } = new();

    private void InitializeChannels()
    {
        AvailableChannels.Clear();
        bool isVi = LocalizationService.Instance.IsVietnamese;
        for (ushort ch = 11; ch <= 26; ch++)
        {
            bool isClean = ch == 15 || ch == 20 || ch == 25 || ch == 26;
            string label = isClean
                ? $"{ch} {(isVi ? "(Kênh sạch - Khuyên dùng)" : "(Clean Channel - Recommended)")}"
                : ch.ToString(CultureInfo.InvariantCulture);

            AvailableChannels.Add(new ZigbeeChannelOption(ch, label));
        }
    }

    /// <summary>
    /// Cho biết kênh sóng hiện tại có phải là kênh sạch (Clean Channel) không bị chồng lấn sóng Wi-Fi 2.4 GHz.
    /// </summary>
    public bool IsCleanChannel => Channel == 15 || Channel == 20 || Channel == 25 || Channel == 26;

    /// <summary>
    /// Văn bản gợi ý kênh sạch hoặc cảnh báo nhiễu sóng Wi-Fi.
    /// </summary>
    public string ChannelHintText => IsCleanChannel
        ? (LocalizationService.Instance.IsVietnamese
            ? "★ Kênh sạch (Khuyên dùng): Không bị chồng lấn dải tần với Wi-Fi Ch 1, 6, 11."
            : "★ Clean Channel (Recommended): Clear of Wi-Fi 1, 6, 11 channel overlap.")
        : (LocalizationService.Instance.IsVietnamese
            ? "⚠️ Cảnh báo nhiễu: Kênh này có thể bị ảnh hưởng nếu ở gần trạm phát Wi-Fi 2.4 GHz."
            : "⚠️ Warning: This channel may experience interference near 2.4 GHz Wi-Fi APs.");

    // ==========================================
    // 4. LỰA CHỌN ĂNG-TEN VÔ TUYẾN
    // ==========================================

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAntennaPcb))]
    [NotifyPropertyChangedFor(nameof(IsAntennaExternal))]
    private ZigbeeAntenna _antennaMode = ZigbeeAntenna.Pcb;

    /// <summary>
    /// Cho biết có chọn ăng-ten tích hợp sẵn trên bo mạch (PCB) hay không.
    /// </summary>
    public bool IsAntennaPcb
    {
        get => AntennaMode == ZigbeeAntenna.Pcb;
        set
        {
            if (value) AntennaMode = ZigbeeAntenna.Pcb;
        }
    }

    /// <summary>
    /// Cho biết có chọn ăng-ten ngoài gắn qua đầu nối SMA hay không.
    /// </summary>
    public bool IsAntennaExternal
    {
        get => AntennaMode == ZigbeeAntenna.External;
        set
        {
            if (value) AntennaMode = ZigbeeAntenna.External;
        }
    }

    // ==========================================
    // 5. AN NINH MẠNG AES-128 (IEC 62443 WRITE-ONLY)
    // ==========================================

    [ObservableProperty]
    private bool _isSecurityEnabled = false;

    [ObservableProperty]
    private string _networkKey = string.Empty;

    [ObservableProperty]
    private bool _isKeyVisible = false;

    [ObservableProperty]
    private bool _preserveKeyOnDeploy = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KeySetBadgeText))]
    private bool _isKeySetInFlash = false;

    /// <summary>
    /// Chuyển đổi trạng thái ẩn/hiện mật khẩu trong ô nhập liệu.
    /// </summary>
    [RelayCommand]
    public void ToggleKeyVisibility()
    {
        IsKeyVisible = !IsKeyVisible;
    }

    /// <summary>
    /// Huy hiệu thông báo trạng thái khóa mạng đã được lưu trong Flash phần cứng hay chưa.
    /// </summary>
    public string KeySetBadgeText => IsKeySetInFlash
        ? (LocalizationService.Instance.IsVietnamese ? "KEY_SET: Đã lưu khóa trong Flash" : "KEY_SET: Key Stored in Flash")
        : (LocalizationService.Instance.IsVietnamese ? "Chưa có khóa trong Flash" : "No Key Set in Flash");

    [ObservableProperty]
    private string _networkName = "Zigbee_Node_1";

    // ==========================================
    // 6. VIỄN TRẮC THỜI GIAN THỰC (READ-ONLY TELEMETRY)
    // ==========================================

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LinkStateDisplayText))]
    [NotifyPropertyChangedFor(nameof(IsLinkUp))]
    private ZigbeeLinkState _linkState = ZigbeeLinkState.Off;

    /// <summary>
    /// Cho biết sóng mạng Zigbee đã sẵn sàng truyền thông (LINK_UP).
    /// </summary>
    public bool IsLinkUp => LinkState == ZigbeeLinkState.Up;

    /// <summary>
    /// Chuỗi hiển thị trạng thái liên kết vô tuyến.
    /// </summary>
    public string LinkStateDisplayText => LinkState switch
    {
        ZigbeeLinkState.Off => LocalizationService.Instance.IsVietnamese ? "ĐÃ TẮT" : "OFF",
        ZigbeeLinkState.Joining => LocalizationService.Instance.IsVietnamese ? "ĐANG GIA NHẬP..." : "JOINING...",
        ZigbeeLinkState.Up => LocalizationService.Instance.IsVietnamese ? "KẾT NỐI (LINK UP)" : "CONNECTED (LINK UP)",
        ZigbeeLinkState.Error => LocalizationService.Instance.IsVietnamese ? "LỖI KẾT NỐI" : "ERROR",
        _ => "UNKNOWN"
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RssiDisplayText))]
    [NotifyPropertyChangedFor(nameof(RssiBarPercent))]
    private short _rssiDbm = 0;

    /// <summary>
    /// Chuỗi hiển thị cường độ tín hiệu kèm đơn vị dBm.
    /// </summary>
    public string RssiDisplayText => RssiDbm != 0 ? $"{RssiDbm} dBm" : "-- dBm";

    /// <summary>
    /// Tỉ lệ phần trăm cường độ tín hiệu sóng phục vụ thanh đo trực quan (0..100%).
    /// </summary>
    public double RssiBarPercent
    {
        get
        {
            if (RssiDbm == 0 || LinkState != ZigbeeLinkState.Up) return 0;
            // Dải đo công nghiệp từ -100 dBm (0%) đến -40 dBm (100%)
            int val = Math.Clamp((int)RssiDbm, -100, -40);
            return (val + 100) * (100.0 / 60.0);
        }
    }

    [ObservableProperty]
    private ushort _lqi = 0;

    [ObservableProperty]
    private ushort _nodeCount = 0;

    [ObservableProperty]
    private ushort _txPackets = 0;

    [ObservableProperty]
    private ushort _rxPackets = 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ErrorDisplayText))]
    private ZigbeeErrorCode _errorCode = ZigbeeErrorCode.NoError;

    /// <summary>
    /// Chuỗi hiển thị mã lỗi phần cứng chuẩn hóa.
    /// </summary>
    public string ErrorDisplayText => ErrorCode switch
    {
        ZigbeeErrorCode.NoError => LocalizationService.Instance.IsVietnamese ? "Bình thường (Không lỗi)" : "Normal (No Error)",
        ZigbeeErrorCode.ErrModuleTimeout => LocalizationService.Instance.IsVietnamese ? "Lỗi: Module RF không phản hồi" : "Error: RF Module Timeout",
        ZigbeeErrorCode.ErrInvalidRfParams => LocalizationService.Instance.IsVietnamese ? "Lỗi: Tham số RF ngoài dải" : "Error: Invalid RF Parameters",
        ZigbeeErrorCode.ErrRestrictedSlaveId => LocalizationService.Instance.IsVietnamese ? "Lỗi: Trùng ID 236/237 bị cấm" : "Error: Restricted Slave ID",
        _ => $"Error Code {ErrorCode}"
    };

    /// <summary>
    /// Huy hiệu tóm tắt trạng thái hiển thị trên thanh Sidebar.
    /// </summary>
    public string StatusSummaryBadge => IsEnabled
        ? (LocalizationService.Instance.IsVietnamese ? "Hoạt động" : "Active")
        : (LocalizationService.Instance.IsVietnamese ? "Đã tắt" : "Disabled");

    // ==========================================
    // 7. PHƯƠNG THỨC NẠP, XUẤT VÀ KIỂM ĐỊNH (PRE-FLIGHT)
    // ==========================================

    /// <summary>
    /// Nạp dữ liệu từ đối tượng truyền thông nhị phân ZigbeeConfigDto (Holding Regs 0x1700..0x173F).
    /// </summary>
    /// <param name="dto">Đối tượng DTO nguồn.</param>
    public void LoadFromDto(ZigbeeConfigDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        IsEnabled = dto.Enabled;
        DeviceRole = dto.Role;
        StationId = dto.SlaveId;
        _committedStationId = dto.SlaveId;
        StationIdText = dto.SlaveId.ToString(CultureInfo.InvariantCulture);

        PanId = dto.PanId;
        _committedPanId = dto.PanId;
        PanIdHex = dto.PanId.ToString("X4", CultureInfo.InvariantCulture);

        Channel = dto.Channel;
        AntennaMode = dto.Antenna;
        IsSecurityEnabled = dto.SecurityEnabled;
        PreserveKeyOnDeploy = dto.PreserveKey;

        // Telemetry
        LinkState = dto.LinkState;
        RssiDbm = dto.Rssi;
        NodeCount = dto.NodeCount;
        IsKeySetInFlash = dto.IsKeySet;
        ErrorCode = dto.ErrorCode;

        // Khóa mạng là Write-Only, không ghi đè NetworkKey bằng byte 0 trả về từ thiết bị
        if (IsKeySetInFlash && string.IsNullOrWhiteSpace(NetworkKey))
        {
            PreserveKeyOnDeploy = true;
        }
    }

    /// <summary>
    /// Xuất dữ liệu cấu hình thành đối tượng nhị phân ZigbeeConfigDto chuẩn bị mã hóa xuống Flash.
    /// </summary>
    /// <returns>Đối tượng ZigbeeConfigDto chứa tham số đã xác thực.</returns>
    public ZigbeeConfigDto ExportToDto()
    {
        CommitStationId();
        CommitPanId();

        var dto = new ZigbeeConfigDto
        {
            Enabled = IsEnabled,
            Role = DeviceRole,
            Framing = ZigbeeFraming.Rtu,
            SecurityEnabled = IsSecurityEnabled,
            Antenna = AntennaMode,
            PreserveKey = PreserveKeyOnDeploy,
            SlaveId = StationId,
            PanId = PanId,
            Channel = Channel
        };

        if (IsSecurityEnabled && !string.IsNullOrWhiteSpace(NetworkKey))
        {
            byte[] keyBytes = ParseNetworkKeyBytes(NetworkKey);
            dto.NetKey = keyBytes;
        }

        return dto;
    }

    private static byte[] ParseNetworkKeyBytes(string raw)
    {
        byte[] key = new byte[ModbusRegisterMap.ZigbeeKeyBytes];
        string clean = raw.Replace("-", "").Replace(" ", "").Replace("0x", "").Trim();

        if (clean.Length == 32)
        {
            for (int i = 0; i < 16; i++)
            {
                if (byte.TryParse(clean.AsSpan(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
                {
                    key[i] = b;
                }
            }
        }
        else
        {
            byte[] asciiBytes = System.Text.Encoding.ASCII.GetBytes(raw);
            Array.Copy(asciiBytes, key, Math.Min(asciiBytes.Length, key.Length));
        }

        return key;
    }

    /// <summary>
    /// Đồng bộ cấu hình từ mô hình lưu trữ dự án ZigbeeConfigModel.
    /// </summary>
    /// <param name="model">Dữ liệu cấu hình đọc từ file dự án .synx.</param>
    public void LoadFromModel(ZigbeeConfigModel model)
    {
        if (model == null) return;

        IsEnabled = model.Enabled;
        DeviceRole = string.Equals(model.Role, "Master", StringComparison.OrdinalIgnoreCase)
            ? ZigbeeRole.Master
            : ZigbeeRole.Slave;

        StationId = model.SlaveId;
        _committedStationId = model.SlaveId;
        StationIdText = model.SlaveId.ToString(CultureInfo.InvariantCulture);

        PanIdHex = model.PanIdHex;
        CommitPanId();

        Channel = model.Channel;
        AntennaMode = string.Equals(model.Antenna, "External", StringComparison.OrdinalIgnoreCase)
            ? ZigbeeAntenna.External
            : ZigbeeAntenna.Pcb;

        IsSecurityEnabled = model.SecurityEnabled;
        PreserveKeyOnDeploy = model.PreserveKeyOnDeploy;
        NetworkName = model.NetworkName;
    }

    /// <summary>
    /// Lưu cấu hình hiện tại vào mô hình dự án ZigbeeConfigModel để tuần tự hóa ra file .synx.
    /// </summary>
    /// <param name="model">Mô hình nhận dữ liệu lưu trữ.</param>
    public void SaveToModel(ZigbeeConfigModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        CommitStationId();
        CommitPanId();

        model.Enabled = IsEnabled;
        model.Role = DeviceRole == ZigbeeRole.Master ? "Master" : "Slave";
        model.SlaveId = StationId;
        model.PanIdHex = PanIdHex;
        model.Channel = Channel;
        model.Antenna = AntennaMode == ZigbeeAntenna.External ? "External" : "Pcb";
        model.SecurityEnabled = IsSecurityEnabled;
        model.PreserveKeyOnDeploy = PreserveKeyOnDeploy;
        model.NetworkName = NetworkName;
    }

    /// <summary>
    /// Kiểm tra tính hợp lệ toàn vẹn (Pre-flight validation) trước khi lưu hoặc nạp xuống thiết bị.
    /// </summary>
    /// <param name="errorMessage">Thông điệp lỗi nếu vi phạm.</param>
    /// <returns>True nếu hợp lệ an toàn, False nếu vi phạm hợp đồng.</returns>
    public bool PreflightValidate(out string? errorMessage)
    {
        if (HasStationIdError)
        {
            errorMessage = StationIdErrorMessage;
            return false;
        }

        CommitStationId();
        CommitPanId();

        if (StationId == 236 || StationId == 237)
        {
            errorMessage = LocalizationService.Instance.IsVietnamese
                ? "Không thể áp dụng: Trùng ID 236 hoặc 237 bị cấm trên module RF."
                : "Cannot apply: Station ID 236 or 237 is restricted by RF module.";
            return false;
        }

        if (StationId < 1 || StationId > 247)
        {
            errorMessage = LocalizationService.Instance.IsVietnamese
                ? "Không thể áp dụng: Địa chỉ trạm phải từ 1 đến 247."
                : "Cannot apply: Station ID must be between 1 and 247.";
            return false;
        }

        if (Channel < 11 || Channel > 26)
        {
            errorMessage = LocalizationService.Instance.IsVietnamese
                ? "Không thể áp dụng: Kênh sóng phải trong khoảng 11 .. 26."
                : "Cannot apply: Channel must be between 11 and 26.";
            return false;
        }

        if (IsSecurityEnabled && !PreserveKeyOnDeploy && string.IsNullOrWhiteSpace(NetworkKey))
        {
            errorMessage = LocalizationService.Instance.IsVietnamese
                ? "Bảo mật AES-128 được bật nhưng chưa nhập khóa mạng và không kích hoạt giữ khóa cũ."
                : "AES-128 security is enabled but no key entered and PreserveKey is disabled.";
            return false;
        }

        errorMessage = null;
        return true;
    }
}
