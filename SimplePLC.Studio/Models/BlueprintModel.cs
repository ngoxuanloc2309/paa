using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using SimplePLC.Domain.Models;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.Services;

namespace SimplePLC.Studio.Models;

public partial class BlueprintModel : ObservableObject, IDisposable
{
    [ObservableProperty]
    private string _id = string.Empty;

    public ushort MinDigitalInputs { get; init; } = 0;
    public ushort MinDigitalOutputs { get; init; } = 0;
    public ushort MinAnalogInputs { get; init; } = 0;
    public ushort MinRetentiveRegisters { get; init; } = 0;
    public ushort MinPhysicalRs485Ports { get; init; } = 0;
    public SPLC_HardwareCapabilities RequiredCapabilities { get; init; } = SPLC_HardwareCapabilities.NONE;

    [ObservableProperty]
    private bool _isCompatible = true;

    [ObservableProperty]
    private string _incompatibleReasonVi = string.Empty;

    [ObservableProperty]
    private string _incompatibleReasonEn = string.Empty;

    public string IncompatibleReason => LocalizationService.Instance.IsVietnamese ? IncompatibleReasonVi : IncompatibleReasonEn;

    [ObservableProperty]
    private string _titleVi = string.Empty;

    [ObservableProperty]
    private string _titleEn = string.Empty;

    [ObservableProperty]
    private string _subtitleVi = string.Empty;

    [ObservableProperty]
    private string _subtitleEn = string.Empty;

    [ObservableProperty]
    private string _descriptionVi = string.Empty;

    [ObservableProperty]
    private string _descriptionEn = string.Empty;

    [ObservableProperty]
    private string _badgeVi = string.Empty;

    [ObservableProperty]
    private string _badgeEn = string.Empty;

    [ObservableProperty]
    private string _badgeColor = "#107C41";

    [ObservableProperty]
    private string _icon = "⚡";

    [ObservableProperty]
    private int _ruleCount;

    [ObservableProperty]
    private string _tagsSummaryVi = string.Empty;

    [ObservableProperty]
    private string _tagsSummaryEn = string.Empty;

    public string Title
    {
        get => LocalizationService.Instance.IsVietnamese 
            ? (!string.IsNullOrWhiteSpace(TitleVi) ? TitleVi : TitleEn) 
            : (!string.IsNullOrWhiteSpace(TitleEn) ? TitleEn : TitleVi);
        set
        {
            TitleVi = value;
            OnPropertyChanged(nameof(Title));
        }
    }

    public string Subtitle
    {
        get => LocalizationService.Instance.IsVietnamese 
            ? (!string.IsNullOrWhiteSpace(SubtitleVi) ? SubtitleVi : SubtitleEn) 
            : (!string.IsNullOrWhiteSpace(SubtitleEn) ? SubtitleEn : SubtitleVi);
        set
        {
            SubtitleVi = value;
            OnPropertyChanged(nameof(Subtitle));
        }
    }

    public string Description
    {
        get => LocalizationService.Instance.IsVietnamese 
            ? (!string.IsNullOrWhiteSpace(DescriptionVi) ? DescriptionVi : DescriptionEn) 
            : (!string.IsNullOrWhiteSpace(DescriptionEn) ? DescriptionEn : DescriptionVi);
        set
        {
            DescriptionVi = value;
            OnPropertyChanged(nameof(Description));
        }
    }

    public string Badge
    {
        get => LocalizationService.Instance.IsVietnamese 
            ? (!string.IsNullOrWhiteSpace(BadgeVi) ? BadgeVi : BadgeEn) 
            : (!string.IsNullOrWhiteSpace(BadgeEn) ? BadgeEn : BadgeVi);
        set
        {
            BadgeVi = value;
            OnPropertyChanged(nameof(Badge));
        }
    }

    public string TagsSummary
    {
        get => LocalizationService.Instance.IsVietnamese 
            ? (!string.IsNullOrWhiteSpace(TagsSummaryVi) ? TagsSummaryVi : TagsSummaryEn) 
            : (!string.IsNullOrWhiteSpace(TagsSummaryEn) ? TagsSummaryEn : TagsSummaryVi);
        set
        {
            TagsSummaryVi = value;
            OnPropertyChanged(nameof(TagsSummary));
        }
    }

    public BlueprintModel()
    {
        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(Badge));
        OnPropertyChanged(nameof(TagsSummary));
        OnPropertyChanged(nameof(IncompatibleReason));
    }

    /// <summary>
    /// Thẩm định tính tương thích của mẫu Blueprint với hồ sơ phần cứng mục tiêu.
    /// </summary>
    public bool CheckCompatibility(ProductDefinition? product, out string reasonVi, out string reasonEn)
    {
        if (product == null)
        {
            reasonVi = string.Empty;
            reasonEn = string.Empty;
            return true;
        }

        var res = product.Resources;
        var missingVi = new List<string>();
        var missingEn = new List<string>();

        if (MinDigitalInputs > 0 && res.DigitalInputs < MinDigitalInputs)
        {
            missingVi.Add($"{MinDigitalInputs} DI (hiện có: {res.DigitalInputs})");
            missingEn.Add($"{MinDigitalInputs} DI (current: {res.DigitalInputs})");
        }

        if (MinDigitalOutputs > 0 && res.DigitalOutputs < MinDigitalOutputs)
        {
            missingVi.Add($"{MinDigitalOutputs} DO (hiện có: {res.DigitalOutputs})");
            missingEn.Add($"{MinDigitalOutputs} DO (current: {res.DigitalOutputs})");
        }

        if (MinAnalogInputs > 0 && res.AnalogInputs < MinAnalogInputs)
        {
            missingVi.Add($"{MinAnalogInputs} AI (hiện có: {res.AnalogInputs})");
            missingEn.Add($"{MinAnalogInputs} AI (current: {res.AnalogInputs})");
        }

        if (MinRetentiveRegisters > 0 && res.RetentiveRegisters < MinRetentiveRegisters)
        {
            missingVi.Add($"{MinRetentiveRegisters} VREG Retain (hiện có: {res.RetentiveRegisters})");
            missingEn.Add($"{MinRetentiveRegisters} VREG Retain (current: {res.RetentiveRegisters})");
        }

        if (MinPhysicalRs485Ports > 0 && product.PhysicalRs485PortCount < MinPhysicalRs485Ports)
        {
            missingVi.Add($"{MinPhysicalRs485Ports} Cổng RS-485 (hiện có: {product.PhysicalRs485PortCount})");
            missingEn.Add($"{MinPhysicalRs485Ports} RS-485 Port(s) (current: {product.PhysicalRs485PortCount})");
        }

        if (RequiredCapabilities != SPLC_HardwareCapabilities.NONE)
        {
            var caps = product.HardwareCapabilities;
            if (RequiredCapabilities.HasFlag(SPLC_HardwareCapabilities.MODBUS_MASTER) && !caps.HasFlag(SPLC_HardwareCapabilities.MODBUS_MASTER))
            {
                missingVi.Add("Modbus Master");
                missingEn.Add("Modbus Master");
            }
            if (RequiredCapabilities.HasFlag(SPLC_HardwareCapabilities.MQTT_PUB) && !caps.HasFlag(SPLC_HardwareCapabilities.MQTT_PUB))
            {
                missingVi.Add("MQTT Publish");
                missingEn.Add("MQTT Publish");
            }
            if (RequiredCapabilities.HasFlag(SPLC_HardwareCapabilities.MQTT_SUB) && !caps.HasFlag(SPLC_HardwareCapabilities.MQTT_SUB))
            {
                missingVi.Add("MQTT Subscribe");
                missingEn.Add("MQTT Subscribe");
            }
            if (RequiredCapabilities.HasFlag(SPLC_HardwareCapabilities.RTC) && !product.SupportsRtcClock)
            {
                missingVi.Add("Đồng hồ RTC");
                missingEn.Add("RTC Clock");
            }
            if (RequiredCapabilities.HasFlag(SPLC_HardwareCapabilities.WIFI) && !caps.HasFlag(SPLC_HardwareCapabilities.WIFI))
            {
                missingVi.Add("Wi-Fi");
                missingEn.Add("Wi-Fi");
            }
            if (RequiredCapabilities.HasFlag(SPLC_HardwareCapabilities.HTTP_CLIENT) && !caps.HasFlag(SPLC_HardwareCapabilities.HTTP_CLIENT))
            {
                missingVi.Add("HTTP Client");
                missingEn.Add("HTTP Client");
            }
            if (RequiredCapabilities.HasFlag(SPLC_HardwareCapabilities.TRANSPARENT_STREAM) && !caps.HasFlag(SPLC_HardwareCapabilities.TRANSPARENT_STREAM))
            {
                missingVi.Add("TCP Stream");
                missingEn.Add("TCP Stream");
            }
            if (RequiredCapabilities.HasFlag(SPLC_HardwareCapabilities.ZIGBEE) && !caps.HasFlag(SPLC_HardwareCapabilities.ZIGBEE))
            {
                missingVi.Add("Zigbee");
                missingEn.Add("Zigbee");
            }
        }

        if (missingVi.Count > 0)
        {
            reasonVi = $"Thiết bị thiếu: {string.Join(", ", missingVi)}";
            reasonEn = $"Device lacks: {string.Join(", ", missingEn)}";
            return false;
        }

        reasonVi = string.Empty;
        reasonEn = string.Empty;
        return true;
    }

    public void Dispose()
    {
        LocalizationService.Instance.LanguageChanged -= OnLanguageChanged;
        GC.SuppressFinalize(this);
    }
}
