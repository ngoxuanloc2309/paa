using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimplePLC.Application.Mapping;
using SimplePLC.Domain.Builders;
using SimplePLC.Domain.Models;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;

namespace SimplePLC.Studio.ViewModels;

public partial class NewProjectViewModel : ObservableObject
{
    [ObservableProperty]
    private string _projectName = "Project_1";

    [ObservableProperty]
    private string _projectLocation = string.Empty;

    [ObservableProperty]
    private string _author = "SimplePLC Engineer";

    [ObservableProperty]
    private string _description = string.Empty;

    public IReadOnlyList<HardwareDevicePreset> Presets { get; } = HardwareDevicePreset.BuiltInPresets;

    [ObservableProperty]
    private HardwareDevicePreset _selectedPreset;

    [ObservableProperty]
    private bool _isCustomMode;

    [ObservableProperty]
    private ushort _customDi = 8;

    [ObservableProperty]
    private ushort _customDo = 8;

    [ObservableProperty]
    private ushort _customAi = 4;

    [ObservableProperty]
    private ushort _customVflag = 32;

    [ObservableProperty]
    private ushort _customVreg = 32;

    [ObservableProperty]
    private ushort _customRetain = 32;

    [ObservableProperty]
    private ushort _customCounters = 8;

    [ObservableProperty]
    private ushort _customMaxRules = 100;

    [ObservableProperty]
    private bool _hasConnectedDevice;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isStatusSuccess;

    // Phục vụ nhận diện từ MCU đang kết nối
    private ProductDefinition? _detectedProduct;
    private string? _detectedMcuName;

    public NewProjectViewModel()
    {
        _selectedPreset = Presets[0];
        _projectLocation = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "SimplePLC",
            "Projects");

        CheckConnectedDevice();
        UpdatePreviewProperties();
    }

    private void CheckConnectedDevice()
    {
        var session = AppServices.Instance.SessionManager.CurrentSession;
        HasConnectedDevice = session is { IsActive: true, Descriptor: not null, ResourceInfo: not null };
    }

    partial void OnSelectedPresetChanged(HardwareDevicePreset value)
    {
        if (value != null && !IsCustomMode)
        {
            UpdatePreviewProperties();
        }
    }

    partial void OnIsCustomModeChanged(bool value)
    {
        UpdatePreviewProperties();
    }

    partial void OnCustomDiChanged(ushort value) => UpdatePreviewProperties();
    partial void OnCustomDoChanged(ushort value) => UpdatePreviewProperties();
    partial void OnCustomAiChanged(ushort value) => UpdatePreviewProperties();
    partial void OnCustomVflagChanged(ushort value) => UpdatePreviewProperties();
    partial void OnCustomVregChanged(ushort value) => UpdatePreviewProperties();
    partial void OnCustomRetainChanged(ushort value) => UpdatePreviewProperties();
    partial void OnCustomCountersChanged(ushort value) => UpdatePreviewProperties();
    partial void OnCustomMaxRulesChanged(ushort value) => UpdatePreviewProperties();

    // ==========================================
    // PREVIEW PROPERTIES
    // ==========================================
    public string PreviewDeviceName
    {
        get
        {
            if (_detectedProduct != null) return _detectedProduct.ProductName;
            if (IsCustomMode) return "Custom Hardware Device";
            return SelectedPreset?.DisplayName ?? "SimplePLC Device";
        }
    }

    public string PreviewMcuTarget
    {
        get
        {
            if (!string.IsNullOrEmpty(_detectedMcuName)) return _detectedMcuName;
            if (IsCustomMode) return "Custom Microcontroller";
            return SelectedPreset?.McuTarget ?? "STM32F401CCU6";
        }
    }

    public int PreviewDigitalInputs => _detectedProduct != null 
        ? _detectedProduct.Resources.DigitalInputs 
        : (IsCustomMode ? CustomDi : SelectedPreset?.DigitalInputs ?? 8);

    public int PreviewDigitalOutputs => _detectedProduct != null 
        ? _detectedProduct.Resources.DigitalOutputs 
        : (IsCustomMode ? CustomDo : SelectedPreset?.DigitalOutputs ?? 8);

    public int PreviewAnalogInputs => _detectedProduct != null 
        ? _detectedProduct.Resources.AnalogInputs 
        : (IsCustomMode ? CustomAi : SelectedPreset?.AnalogInputs ?? 4);

    public int PreviewVirtualFlags => _detectedProduct != null 
        ? _detectedProduct.Resources.VirtualFlags 
        : (IsCustomMode ? CustomVflag : SelectedPreset?.VirtualFlags ?? 32);

    public int PreviewVirtualRegs => _detectedProduct != null 
        ? _detectedProduct.Resources.VirtualRegisters 
        : (IsCustomMode ? CustomVreg : SelectedPreset?.VirtualRegisters ?? 32);

    public int PreviewRetainRegs => _detectedProduct != null 
        ? _detectedProduct.Resources.RetentiveRegisters 
        : (IsCustomMode ? CustomRetain : SelectedPreset?.RetentiveRegisters ?? 32);

    public int PreviewCounters => _detectedProduct != null 
        ? _detectedProduct.Resources.Counters 
        : (IsCustomMode ? CustomCounters : SelectedPreset?.Counters ?? 8);

    public int PreviewMaxRules => _detectedProduct != null 
        ? _detectedProduct.MaxRules 
        : (IsCustomMode ? CustomMaxRules : SelectedPreset?.MaxRules ?? 100);

    public string PreviewDiText => PreviewDigitalInputs > 0 
        ? $"{PreviewDigitalInputs} (DI0 – DI{PreviewDigitalInputs - 1})" 
        : "0";

    public string PreviewDoText => PreviewDigitalOutputs > 0 
        ? $"{PreviewDigitalOutputs} (DO0 – DO{PreviewDigitalOutputs - 1})" 
        : "0";

    public string PreviewAiText => PreviewAnalogInputs > 0 
        ? $"{PreviewAnalogInputs} (AI0 – AI{PreviewAnalogInputs - 1})" 
        : "0";

    public string PreviewVflagText => PreviewVirtualFlags > 0 
        ? $"{PreviewVirtualFlags} (VFLAG0 – VFLAG{PreviewVirtualFlags - 1})" 
        : "0";

    public string PreviewVregText => PreviewVirtualRegs > 0 
        ? $"{PreviewVirtualRegs} (VREG0 – VREG{PreviewVirtualRegs - 1})" 
        : "0";

    public string PreviewRetainText => PreviewRetainRegs > 0 
        ? $"{PreviewRetainRegs} (RETAIN0 – RETAIN{PreviewRetainRegs - 1})" 
        : "0";

    public string PreviewCountersText => PreviewCounters > 0 
        ? $"{PreviewCounters} (COUNTER0 – COUNTER{PreviewCounters - 1})" 
        : "0";

    public int PreviewTotalTags => PreviewDigitalInputs + PreviewDigitalOutputs + PreviewAnalogInputs +
                                   PreviewVirtualFlags + PreviewVirtualRegs + PreviewRetainRegs + PreviewCounters;

    public void UpdatePreviewProperties()
    {
        OnPropertyChanged(nameof(PreviewDeviceName));
        OnPropertyChanged(nameof(PreviewMcuTarget));
        OnPropertyChanged(nameof(PreviewDigitalInputs));
        OnPropertyChanged(nameof(PreviewDigitalOutputs));
        OnPropertyChanged(nameof(PreviewAnalogInputs));
        OnPropertyChanged(nameof(PreviewVirtualFlags));
        OnPropertyChanged(nameof(PreviewVirtualRegs));
        OnPropertyChanged(nameof(PreviewRetainRegs));
        OnPropertyChanged(nameof(PreviewCounters));
        OnPropertyChanged(nameof(PreviewMaxRules));
        OnPropertyChanged(nameof(PreviewDiText));
        OnPropertyChanged(nameof(PreviewDoText));
        OnPropertyChanged(nameof(PreviewAiText));
        OnPropertyChanged(nameof(PreviewVflagText));
        OnPropertyChanged(nameof(PreviewVregText));
        OnPropertyChanged(nameof(PreviewRetainText));
        OnPropertyChanged(nameof(PreviewCountersText));
        OnPropertyChanged(nameof(PreviewTotalTags));
    }

    [RelayCommand]
    public void BrowseLocation()
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog
        {
            Title = LocalizationService.Tr("NewProjectLocation"),
            InitialDirectory = Directory.Exists(ProjectLocation) 
                ? ProjectLocation 
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.FolderName))
        {
            ProjectLocation = dlg.FolderName;
        }
    }

    [RelayCommand]
    public void DetectFromConnectedDevice()
    {
        var session = AppServices.Instance.SessionManager.CurrentSession;
        if (session == null || !session.IsActive || session.Descriptor == null || session.ResourceInfo == null)
        {
            StatusMessage = LocalizationService.Tr("NewProjectNoDeviceConnected");
            IsStatusSuccess = false;
            return;
        }

        if (DeviceProfileMapper.TryBuildProductDefinition(session.Descriptor, session.ResourceInfo, out var product, out var error))
        {
            _detectedProduct = product;
            _detectedMcuName = $"MCU Connected (HW {session.Descriptor.HwVersionMajor}.{session.Descriptor.HwVersionMinor}, FW {session.Descriptor.FwVersionMajor}.{session.Descriptor.FwVersionMinor})";
            IsCustomMode = false;
            StatusMessage = LocalizationService.Tr("NewProjectDeviceDetectedSuccess");
            IsStatusSuccess = true;
            UpdatePreviewProperties();
        }
        else
        {
            StatusMessage = error ?? "Failed to read device hardware profile.";
            IsStatusSuccess = false;
        }
    }

    [RelayCommand]
    public void SelectStandardPreset(HardwareDevicePreset preset)
    {
        if (preset == null) return;
        _detectedProduct = null;
        _detectedMcuName = null;
        IsCustomMode = false;
        SelectedPreset = preset;
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    public void EnableCustomMode()
    {
        _detectedProduct = null;
        _detectedMcuName = null;
        IsCustomMode = true;
        StatusMessage = string.Empty;
    }

    public ProductDefinition BuildProductDefinition()
    {
        if (_detectedProduct != null)
        {
            return _detectedProduct;
        }

        if (IsCustomMode)
        {
            var res = new ProductResourceProfile(
                CustomDi,
                CustomDo,
                CustomAi,
                CustomVflag,
                CustomVreg,
                CustomRetain,
                CustomCounters);

            return DeviceProfileBuilder.Build(
                deviceClass: 1, // REMOTE_IO
                productVariant: 255, // CUSTOM
                protocolVersion: DeviceProfileBuilder.ProtocolVersionV1,
                wireProfile: DeviceProfileBuilder.WireProfileV1,
                maxRules: CustomMaxRules,
                runtimeTagCount: (ushort)res.TotalTags,
                resources: res);
        }

        return SelectedPreset.BuildProductDefinition();
    }

    public bool Validate(out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(ProjectName))
        {
            error = LocalizationService.Tr("NewProjectInvalidName");
            return false;
        }

        char[] invalidChars = Path.GetInvalidFileNameChars();
        if (ProjectName.IndexOfAny(invalidChars) >= 0)
        {
            error = LocalizationService.Tr("NewProjectInvalidName");
            return false;
        }

        if (string.IsNullOrWhiteSpace(ProjectLocation))
        {
            ProjectLocation = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "SimplePLC",
                "Projects");
        }

        return true;
    }
}
