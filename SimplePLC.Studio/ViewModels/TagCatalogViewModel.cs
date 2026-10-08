using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using SimplePLC.Application.Models;
using SimplePLC.Protocol.Models;

namespace SimplePLC.Studio.ViewModels;

public partial class TagCatalogViewModel : ObservableObject
{
    private ProductResourceProfile? _currentResources;
    public ProductResourceProfile? CurrentResources => _currentResources;

    public ObservableCollection<TagModel> AllTags { get; } = new();
    public ObservableCollection<TagModel> DigitalTags { get; } = new();
    public ObservableCollection<TagModel> OutputDigitalTags { get; } = new();
    public ObservableCollection<TagModel> RegisterTags { get; } = new();
    public ObservableCollection<TagModel> FilteredTags { get; } = new();

    public ObservableCollection<GroupFilterItem> GroupButtons { get; } = new();

    [ObservableProperty]
    private string _searchFilter = string.Empty;

    [ObservableProperty]
    private string _selectedGroup = "ALL";

    [ObservableProperty]
    private TagGroupInfo _currentGroupInfo = null!;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BannerToggleIcon))]
    [NotifyPropertyChangedFor(nameof(BannerToggleText))]
    private bool _isBannerExpanded = false;

    public string BannerToggleIcon => IsBannerExpanded ? "▲" : "▼";
    public string BannerToggleText => IsBannerExpanded
        ? LocalizationService.Tr("TagBannerCollapse")
        : LocalizationService.Tr("TagBannerExpand");

    public int TotalTagCount => AllTags.Count(t => t.Kind != TagKind.None);
    public string TitleText => string.Format(LocalizationService.Tr("TagCatalogTitleDynamic"), TotalTagCount);

    public TagCatalogViewModel()
    {
        InitializeTagsFromLayout(TagLayoutMap.Default);
        UpdateGroupButtons();
        _currentGroupInfo = GetGroupInfo("ALL");
        ApplyFilter();

        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged()
    {
        OnPropertyChanged(nameof(TitleText));
        OnPropertyChanged(nameof(BannerToggleText));
        UpdateGroupButtons();
        CurrentGroupInfo = GetGroupInfo(SelectedGroup);
    }

    [RelayCommand]
    public void ToggleBanner()
    {
        IsBannerExpanded = !IsBannerExpanded;
    }

    [RelayCommand]
    public void ClearSearch()
    {
        SearchFilter = string.Empty;
    }

    [RelayCommand]
    public void ToggleTagValue(TagModel? tag)
    {
        if (tag == null || !tag.IsDigital) return;
        tag.Value = tag.Value == 0 ? 1 : 0;
    }

    [RelayCommand]
    public void ResetDefaultAliases()
    {
        var confirm = MessageBox.Show(
            LocalizationService.Tr("TagResetConfirm"),
            LocalizationService.Tr("TagResetTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        ResetAllAliases();
    }

    [RelayCommand]
    public void ExportCsv()
    {
        var dlg = new SaveFileDialog
        {
            Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
            FileName = $"SimplePLC_Tags_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
            DefaultExt = ".csv"
        };

        if (dlg.ShowDialog() == true)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("Index,Name,Group,ModbusAddress,DataType,Access,Alias,Value");
                foreach (var tag in AllTags.Where(t => t.Kind != TagKind.None))
                {
                    string safeAlias = $"\"{tag.Alias.Replace("\"", "\"\"")}\"";
                    sb.AppendLine($"{tag.Index},{tag.Name},{tag.Group},{tag.ModbusAddressText},{tag.DataTypeText},{tag.AccessModeText},{safeAlias},{tag.Value}");
                }
                File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);

                MessageBox.Show(
                    string.Format(LocalizationService.Tr("TagExportSuccess"), TotalTagCount, dlg.FileName),
                    "Export CSV",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void InitializeTags() => InitializeTagsFromLayout(TagLayoutMap.Default);

    private void InitializeTagsFromLayout(TagLayoutMap layout)
    {
        AllTags.Clear();
        AllTags.Add(new TagModel { Index = 65535, Kind = TagKind.None, Name = "NONE", Alias = "— Không chọn —", Group = "SYSTEM" });

        for (int i = 0; i < layout.DiCount; i++)
            AllTags.Add(new TagModel { Index = (ushort)(layout.DiBase + i), Kind = TagKind.DiscreteInput, Name = $"DI{i}", Channel = i + 1, Group = "DI", Value = 0 });

        for (int i = 0; i < layout.DoCount; i++)
            AllTags.Add(new TagModel { Index = (ushort)(layout.DoBase + i), Kind = TagKind.DiscreteOutput, Name = $"DO{i}", Channel = i + 1, Group = "DO", Value = 0 });

        for (int i = 0; i < layout.AiCount; i++)
            AllTags.Add(new TagModel { Index = (ushort)(layout.AiBase + i), Kind = TagKind.AnalogInput, Name = $"AI{i}", Channel = i + 1, Group = "AI", Value = 0 });

        for (int i = 0; i < layout.VflagCount; i++)
            AllTags.Add(new TagModel { Index = (ushort)(layout.VflagBase + i), Kind = TagKind.VirtualFlag, Name = $"VFLAG{i}", Channel = i + 1, Group = "VFLAG", Value = 0 });

        for (int i = 0; i < layout.VregCount; i++)
            AllTags.Add(new TagModel { Index = (ushort)(layout.VregBase + i), Kind = TagKind.VirtualRegister, Name = $"VREG{i}", Channel = i + 1, Group = "VREG", Value = 0 });

        for (int i = 0; i < layout.VregRetainCount; i++)
            AllTags.Add(new TagModel { Index = (ushort)(layout.VregRetainBase + i), Kind = TagKind.VirtualRegisterRetain, Name = $"VREG_RETAIN{i}", Channel = i + 1, Group = "VREG_R", Value = 0 });

        for (int i = 0; i < layout.CounterCount; i++)
            AllTags.Add(new TagModel { Index = (ushort)(layout.CounterBase + i), Kind = TagKind.Counter, Name = $"COUNTER{i}", Channel = i + 1, Group = "COUNTER", Value = 0 });

        RebuildDerivedCollections();
    }

    /// <summary>
    /// Gọi khi MCU kết nối và gửi DeviceResourceInfo thực.
    /// Rebuild toàn bộ Tag Catalog theo layout của MCU.
    /// </summary>
    public void RebuildFromLayout(TagLayoutMap layout)
    {
        InitializeTagsFromLayout(layout);
        UpdateGroupButtons();
        CurrentGroupInfo = GetGroupInfo(SelectedGroup);
        ApplyFilter();
        OnPropertyChanged(nameof(TotalTagCount));
        OnPropertyChanged(nameof(TitleText));
    }

    private void RebuildDerivedCollections()
    {
        DigitalTags.Clear();
        foreach (var tag in AllTags.Where(t => t.Kind == TagKind.None || t.IsDigital))
            DigitalTags.Add(tag);

        OutputDigitalTags.Clear();
        foreach (var tag in AllTags.Where(t => t.Kind == TagKind.DiscreteOutput || t.Kind == TagKind.VirtualFlag))
            OutputDigitalTags.Add(tag);

        RegisterTags.Clear();
        foreach (var tag in AllTags.Where(t => t.Kind is TagKind.VirtualRegisterRetain or TagKind.VirtualRegister or TagKind.Counter))
            RegisterTags.Add(tag);
    }

    public void ResetAllAliases()
    {
        foreach (var tag in AllTags)
        {
            if (tag.Kind == TagKind.None)
            {
                tag.Alias = "— Không chọn —";
            }
            else
            {
                tag.Alias = string.Empty;
            }
        }
        ApplyFilter();
    }

    [RelayCommand]
    public void FilterByGroup(string group)
    {
        SelectedGroup = group;
        foreach (var btn in GroupButtons)
        {
            btn.IsSelected = (btn.Key == group || (btn.Key is "ALL" or "TẤT CẢ" && group is "ALL" or "TẤT CẢ"));
        }
        CurrentGroupInfo = GetGroupInfo(group);
        ApplyFilter();
    }

    partial void OnSearchFilterChanged(string value)
    {
        ApplyFilter();
    }

    public void ApplyFilter()
    {
        FilteredTags.Clear();
        foreach (var tag in AllTags)
        {
            if (tag.Kind == TagKind.None) continue;

            bool matchesGroup = SelectedGroup is "ALL" or "TẤT CẢ" || tag.Group == SelectedGroup;
            bool matchesSearch = string.IsNullOrWhiteSpace(SearchFilter) ||
                                 tag.Name.Contains(SearchFilter, StringComparison.OrdinalIgnoreCase) ||
                                 tag.Alias.Contains(SearchFilter, StringComparison.OrdinalIgnoreCase) ||
                                 tag.ModbusAddressText.Contains(SearchFilter, StringComparison.OrdinalIgnoreCase) ||
                                 tag.DataTypeText.Contains(SearchFilter, StringComparison.OrdinalIgnoreCase);

            if (matchesGroup && matchesSearch)
            {
                FilteredTags.Add(tag);
            }
        }
    }

    public void UpdateGroupButtons(ProductResourceProfile? resources = null)
    {
        resources ??= _currentResources;
        bool isVi = LocalizationService.Instance.IsVietnamese;

        int diCount = resources?.DigitalInputs ?? AllTags.Count(t => t.Kind == TagKind.DiscreteInput);
        int doCount = resources?.DigitalOutputs ?? AllTags.Count(t => t.Kind == TagKind.DiscreteOutput);
        int aiCount = resources?.AnalogInputs ?? AllTags.Count(t => t.Kind == TagKind.AnalogInput);
        int vflagCount = resources?.VirtualFlags ?? AllTags.Count(t => t.Kind == TagKind.VirtualFlag);
        int vregCount = resources?.VirtualRegisters ?? AllTags.Count(t => t.Kind == TagKind.VirtualRegister);
        int vregRetainCount = resources?.RetentiveRegisters ?? AllTags.Count(t => t.Kind == TagKind.VirtualRegisterRetain);
        int counterCount = resources?.Counters ?? AllTags.Count(t => t.Kind == TagKind.Counter);

        var list = new List<GroupFilterItem>
        {
            new() { Key = "ALL", Label = isVi ? $"TẤT CẢ ({TotalTagCount})" : $"ALL ({TotalTagCount})" }
        };

        if (diCount > 0) list.Add(new() { Key = "DI", Label = $"DI ({diCount})" });
        if (doCount > 0) list.Add(new() { Key = "DO", Label = $"DO ({doCount})" });
        if (aiCount > 0) list.Add(new() { Key = "AI", Label = $"AI ({aiCount})" });
        if (vflagCount > 0) list.Add(new() { Key = "VFLAG", Label = $"VFLAG ({vflagCount})" });
        if (vregCount > 0) list.Add(new() { Key = "VREG", Label = $"VREG ({vregCount})" });
        if (vregRetainCount > 0) list.Add(new() { Key = "VREG_R", Label = $"VREG_R ({vregRetainCount})" });
        if (counterCount > 0) list.Add(new() { Key = "COUNTER", Label = $"COUNTER ({counterCount})" });

        // Nếu nhóm đang chọn không còn tồn tại trên phần cứng (ví dụ AI = 0), tự động reset về "ALL"
        if (SelectedGroup != "ALL" && SelectedGroup != "TẤT CẢ" && !list.Any(g => g.Key == SelectedGroup))
        {
            SelectedGroup = "ALL";
        }

        foreach (var item in list)
        {
            item.IsSelected = (item.Key == SelectedGroup || (item.Key is "ALL" or "TẤT CẢ" && SelectedGroup is "ALL" or "TẤT CẢ"));
        }

        GroupButtons.Clear();
        foreach (var item in list)
        {
            GroupButtons.Add(item);
        }
    }

    /// <summary>
    /// Đồng bộ và co giãn danh mục Tag theo ProductDefinition nhận từ thiết bị thực tế (Wire Contract V2.0).
    /// Giữ nguyên các TagModel cũ (bảo toàn Alias và Value), chỉ loại bỏ Tag không tồn tại trên phần cứng và thêm Tag mới.
    /// </summary>
    public void SyncWithProductDefinition(ProductDefinition? product)
    {
        if (product == null) return;

        _currentResources = product.Resources;

        var existingMap = AllTags.ToDictionary(t => t.Index);
        var syncedTags = new List<TagModel>();

        // 1. Tag NONE (Index 65535) luôn luôn được giữ lại cho các ComboBox chọn Tag
        if (!existingMap.TryGetValue(65535, out var noneTag))
        {
            noneTag = new TagModel
            {
                Index = 65535,
                Kind = TagKind.None,
                Name = "NONE",
                Alias = "— Không chọn —",
                Group = "SYSTEM"
            };
        }
        syncedTags.Add(noneTag);

        var layout = new TagLayoutMap(
            product.Resources.DigitalInputs,
            product.Resources.DigitalOutputs,
            product.Resources.AnalogInputs,
            product.Resources.VirtualFlags,
            product.Resources.VirtualRegisters,
            product.Resources.RetentiveRegisters,
            product.Resources.Counters);

        // 2. Duyệt qua từng TagDefinition được sinh từ phần cứng MCU
        foreach (var def in product.Tags)
        {
            string group = MapGroup(def.Kind);
            int channel = CalculateChannel(def.Kind, def.TagIndex, layout);

            if (existingMap.TryGetValue(def.TagIndex, out var existing))
            {
                // Giữ nguyên instance cũ để bảo toàn Alias đã đặt và liên kết của Rule
                existing.Name = def.Name;
                existing.Kind = def.Kind;
                existing.Group = group;
                existing.Channel = channel;
                syncedTags.Add(existing);
            }
            else
            {
                syncedTags.Add(new TagModel
                {
                    Index = def.TagIndex,
                    Name = def.Name,
                    Kind = def.Kind,
                    Group = group,
                    Channel = channel,
                    Alias = string.Empty,
                    Value = 0
                });
            }
        }

        // 3. Cập nhật AllTags
        AllTags.Clear();
        foreach (var tag in syncedTags)
        {
            AllTags.Add(tag);
        }

        // 4. Đồng bộ các collection phụ thuộc
        RebuildDerivedCollections();

        // 5. Cập nhật thanh nút lọc nhóm GroupButtons
        UpdateGroupButtons(product.Resources);

        // 6. Cập nhật bộ lọc và thông tin UI
        ApplyFilter();
        OnPropertyChanged(nameof(TotalTagCount));
        OnPropertyChanged(nameof(TitleText));
        CurrentGroupInfo = GetGroupInfo(SelectedGroup);
    }

    /// <summary>
    /// Đồng bộ giá trị sống (Live runtime values) nhận từ MonitorService.TagsUpdated.
    /// </summary>
    public void UpdateTagValues(IReadOnlyList<RuntimeTagValue> values)
    {
        if (values == null || values.Count == 0) return;

        void Apply()
        {
            foreach (var val in values)
            {
                var tag = AllTags.FirstOrDefault(t => t.Index == val.TagIndex);
                if (tag != null && tag.Value != val.Value)
                {
                    tag.Value = val.Value;
                }
            }
        }

        if (System.Windows.Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(new Action(Apply));
        }
        else
        {
            Apply();
        }
    }

    private static string MapGroup(TagKind kind) => kind switch
    {
        TagKind.DiscreteInput => "DI",
        TagKind.DiscreteOutput => "DO",
        TagKind.AnalogInput => "AI",
        TagKind.VirtualFlag => "VFLAG",
        TagKind.VirtualRegister => "VREG",
        TagKind.VirtualRegisterRetain => "VREG_R",
        TagKind.Counter => "COUNTER",
        _ => "SYSTEM"
    };

    private static int CalculateChannel(TagKind kind, int index, TagLayoutMap layout) => kind switch
    {
        TagKind.DiscreteInput => (index - layout.DiBase) + 1,
        TagKind.DiscreteOutput => (index - layout.DoBase) + 1,
        TagKind.AnalogInput => (index - layout.AiBase) + 1,
        TagKind.VirtualFlag => (index - layout.VflagBase) + 1,
        TagKind.VirtualRegister => (index - layout.VregBase) + 1,
        TagKind.VirtualRegisterRetain => (index - layout.VregRetainBase) + 1,
        TagKind.Counter => (index - layout.CounterBase) + 1,
        _ => 1
    };

    private static int CalculateChannel(TagKind kind, int index) => CalculateChannel(kind, index, TagLayoutMap.Default);

    public TagGroupInfo GetGroupInfo(string group)
    {
        bool isVi = LocalizationService.Instance.IsVietnamese;
        int diCount = _currentResources?.DigitalInputs ?? AllTags.Count(t => t.Kind == TagKind.DiscreteInput);
        int doCount = _currentResources?.DigitalOutputs ?? AllTags.Count(t => t.Kind == TagKind.DiscreteOutput);
        int aiCount = _currentResources?.AnalogInputs ?? AllTags.Count(t => t.Kind == TagKind.AnalogInput);
        int vflagCount = _currentResources?.VirtualFlags ?? AllTags.Count(t => t.Kind == TagKind.VirtualFlag);
        int vregCount = _currentResources?.VirtualRegisters ?? AllTags.Count(t => t.Kind == TagKind.VirtualRegister);
        int vregRetainCount = _currentResources?.RetentiveRegisters ?? AllTags.Count(t => t.Kind == TagKind.VirtualRegisterRetain);
        int counterCount = _currentResources?.Counters ?? AllTags.Count(t => t.Kind == TagKind.Counter);
        int totalTags = _currentResources?.TotalTags ?? TotalTagCount;

        return group switch
        {
            "DI" => new TagGroupInfo
            {
                GroupCode = "DI",
                GroupName = isVi ? "Digital Input — Ngõ Vào Số 24VDC (Cách Ly Quang)" : "Digital Input — 24VDC Opto-Isolated Inputs",
                Description = isVi 
                    ? "Thu nhận tín hiệu đóng/ngắt nhị phân (0 hoặc 1, 24VDC) từ thiết bị trường như nút ấn, cảm biến tiệm cận, công tắc hành trình, nút dừng khẩn cấp E-Stop."
                    : "Receives binary ON/OFF signals (0 or 1, 24VDC) from field devices such as pushbuttons, proximity sensors, limit switches, and emergency stops.",
                RoleInPLC = isVi
                    ? "Làm tín hiệu kích hoạt cho khối TRIGGER (bắt sườn xung Rising Edge 0→1, Falling Edge 1→0) hoặc làm tiếp điểm kiểm tra an toàn trong khối GUARD."
                    : "Acts as activation signal for TRIGGER blocks (rising edge 0→1, falling edge 1→0) or interlock condition in GUARD blocks.",
                HardwareSpec = isVi
                    ? $"{diCount} Kênh vật lý (DI0 – DI{Math.Max(0, diCount - 1)}) · Chu kỳ quét: 10ms · 24VDC Optocoupler · Kiểu dữ liệu: Bit (0/1)"
                    : $"{diCount} Physical Channels (DI0 – DI{Math.Max(0, diCount - 1)}) · 10ms Scan Cycle · 24VDC Optocoupler · Type: Bit (0/1)",
                Icon = "📥",
                BadgeColor = "#006487"
            },
            "DO" => new TagGroupInfo
            {
                GroupCode = "DO",
                GroupName = isVi ? "Digital Output — Ngõ Ra Số Điều Khiển Cơ Cấu Chấp Hành" : "Digital Output — Actuator Control Outputs",
                Description = isVi
                    ? "Điều khiển đóng/cắt tải thực tế trong tủ điện như cuộn hút Contactor động cơ, van điện từ khí nén (Solenoid), đèn tháp 3 màu báo trạng thái, còi cảnh báo."
                    : "Controls physical field loads such as motor contactors, pneumatic solenoid valves, 3-color tower lights, and audible alarms.",
                RoleInPLC = isVi
                    ? "Đối tượng thực thi đích trong khối ACTION (thực hiện lệnh Set Tag Value = 1/0, Toggle Tag State On/Off khi chuỗi điều kiện logic thỏa mãn)."
                    : "Execution target in ACTION blocks (Set Tag Value = 1/0, Toggle Tag State On/Off when logical conditions evaluate true).",
                HardwareSpec = isVi
                    ? $"{doCount} Kênh vật lý (DO0 – DO{Math.Max(0, doCount - 1)}) · Relay/Transistor 24VDC 0.5A · Đọc/Ghi 2 chiều · Kiểu: Bit (0/1)"
                    : $"{doCount} Physical Channels (DO0 – DO{Math.Max(0, doCount - 1)}) · Relay/Transistor 24VDC 0.5A · Read/Write · Type: Bit (0/1)",
                Icon = "📤",
                BadgeColor = "#2563EB"
            },
            "AI" => new TagGroupInfo
            {
                GroupCode = "AI",
                GroupName = isVi ? "Analog Input — Ngõ Vào Tương Tự Đo Lường Cảm Biến Liên Tục" : "Analog Input — Continuous Sensor Inputs",
                Description = isVi
                    ? "Thu nhận và chuyển đổi tín hiệu đo lường liên tục (chuẩn 4–20mA hoặc 0–10V) từ cảm biến áp suất dầu/khí, cảm biến nhiệt độ buồng đốt/sấy, cảm biến đo mức bồn chứa, biến dòng CT."
                    : "Converts continuous instrumentation signals (4–20mA or 0–10V) from pressure transmitters, RTD/thermocouples, level sensors, and current transducers.",
                RoleInPLC = isVi
                    ? "Cung cấp giá trị tức thời cho khối GUARD để so sánh ngưỡng bảo vệ (Greater Than, Less Than, Between Range) hoặc khối ACTION để Scale đơn vị đo."
                    : "Supplies real-time values for GUARD blocks to compare thresholds (Greater, Less, Between Range) or for math scaling.",
                HardwareSpec = isVi
                    ? $"{aiCount} Kênh vật lý (AI0 – AI{Math.Max(0, aiCount - 1)}) · 12-bit ADC (0 – 4095 hoặc scale 0–100%) · Tần số lấy mẫu: 100Hz"
                    : $"{aiCount} Physical Channels (AI0 – AI{Math.Max(0, aiCount - 1)}) · 12-bit ADC (0 – 4095 or 0–100%) · 100Hz Sampling Rate",
                Icon = "📊",
                BadgeColor = "#D97706"
            },
            "VFLAG" => new TagGroupInfo
            {
                GroupCode = "VFLAG",
                GroupName = isVi ? "Virtual Flag — Cờ Nhớ Logic Nội Bộ (Internal Relay M-Coil)" : "Virtual Flag — Internal Logic Flags (M-Relays)",
                Description = isVi
                    ? "Đóng vai trò như các tiếp điểm rơ-le trung gian ảo trong PLC (tương đương vùng nhớ bit M trong Siemens, B trong Rockwell). Dùng ghi nhớ trạng thái logic chuyển tiếp giữa các bước quy trình."
                    : "Acts as virtual intermediate relay coils/contacts (equivalent to M bits in Siemens, B bits in Rockwell). Stores sequence state between rules.",
                RoleInPLC = isVi
                    ? "Làm biến trung gian truyền trạng thái giữa các Rule độc lập (ví dụ cờ 'in_shift' báo trong ca làm việc, cờ 'machine_ready' máy sẵn sàng, cờ lỗi dừng chuyền)."
                    : "Transfers logic state between independent rules (e.g., 'in_shift' shift flag, 'machine_ready' ready flag, line trip flag).",
                HardwareSpec = isVi
                    ? $"{vflagCount} Cờ nội bộ (VFLAG0 – VFLAG{Math.Max(0, vflagCount - 1)}) · Lưu trong RAM vi điều khiển (Tự động xóa về 0 khi mất nguồn điện)"
                    : $"{vflagCount} Internal Flags (VFLAG0 – VFLAG{Math.Max(0, vflagCount - 1)}) · Stored in MCU RAM (Cleared on power loss)",
                Icon = "🚩",
                BadgeColor = "#4C1D95"
            },
            "VREG" => new TagGroupInfo
            {
                GroupCode = "VREG",
                GroupName = isVi ? "Virtual Register — Thanh Ghi Số Nguyên Tính Toán & Định Thời (RAM)" : "Virtual Register — Math & Timing Registers (RAM)",
                Description = isVi
                    ? "Vùng nhớ số nguyên 16-bit (-32768 đến +32767) dùng làm biến số học, lưu giá trị đặt tạm thời (Setpoint), giá trị đếm thời gian của Timer, đếm số lần cảnh báo tạm trong ca."
                    : "16-bit signed integer memory (-32768 to +32767) for calculations, dynamic setpoints, timer elapsed times, and runtime counters.",
                RoleInPLC = isVi
                    ? "Lưu giá trị ngưỡng động, tham số tính toán số học cho các phép so sánh trong khối GUARD hoặc làm đối số tăng/giảm trong khối ACTION."
                    : "Holds setpoints for GUARD comparisons or increment/decrement operands in ACTION blocks.",
                HardwareSpec = isVi
                    ? $"{vregCount} Thanh ghi (VREG0 – VREG{Math.Max(0, vregCount - 1)}) · Kiểu Int16 Signed · Lưu trong RAM vi điều khiển"
                    : $"{vregCount} Registers (VREG0 – VREG{Math.Max(0, vregCount - 1)}) · Signed Int16 · Stored in MCU RAM",
                Icon = "🔢",
                BadgeColor = "#0D9488"
            },
            "VREG_R" => new TagGroupInfo
            {
                GroupCode = "VREG_R",
                GroupName = isVi ? "Virtual Register Retain — Thanh Ghi Lưu Bộ Nhớ Vĩnh Viễn (Non-Volatile)" : "Virtual Register Retain — Non-Volatile Retentive Registers (Flash)",
                Description = isVi
                    ? "Vùng nhớ vĩnh viễn lưu vào chip Flash/EEPROM, KHÔNG BỊ MẤT DỮ LIỆU khi tủ điện mất nguồn đột ngột. Lưu trữ các chỉ số sống còn: Tổng sản lượng ca/ngày, bộ đếm chu kỳ máy dập, số giờ vận hành bảo trì."
                    : "Retentive memory preserved in Flash/EEPROM across power cycles. Retains production counts, machine cycles, maintenance hours, and recipes.",
                RoleInPLC = isVi
                    ? "Lưu trữ bộ đếm cộng dồn trong khối ACTION (Increment Counter) hoặc công thức sản xuất (Recipe) cần bảo toàn vĩnh viễn khi tắt bật máy."
                    : "Retains cumulative totals in ACTION blocks or persistent configuration parameters.",
                HardwareSpec = isVi
                    ? $"{vregRetainCount} Thanh ghi lưu vĩnh viễn (VREG_RETAIN0 – VREG_RETAIN{Math.Max(0, vregRetainCount - 1)}) · Bộ nhớ Flash Non-Volatile (>100k lần ghi) · Kiểu: Int32"
                    : $"{vregRetainCount} Retentive Registers (VREG_RETAIN0 – VREG_RETAIN{Math.Max(0, vregRetainCount - 1)}) · Non-Volatile Flash (>100k writes) · Type: Int32",
                Icon = "💾",
                BadgeColor = "#059669"
            },
            "COUNTER" => new TagGroupInfo
            {
                GroupCode = "COUNTER",
                GroupName = isVi ? "Counter — Bộ Đếm Nội Bộ (Internal Counter)" : "Counter — Internal High-Speed Counters",
                Description = isVi
                    ? "Bộ đếm số học phục vụ các thao tác đếm sản phẩm, chu kỳ làm việc của cơ cấu chấp hành trong hệ thống."
                    : "Arithmetic counters for counting products, batches, and actuator stroke cycles.",
                RoleInPLC = isVi
                    ? "Lưu trữ giá trị đếm trong khối GUARD để so sánh ngưỡng hoặc làm đối số tăng/giảm trong khối ACTION."
                    : "Stores count values for GUARD threshold comparisons or target for ACTION counter increments.",
                HardwareSpec = isVi
                    ? $"{counterCount} Bộ đếm (COUNTER0 – COUNTER{Math.Max(0, counterCount - 1)}) · Kiểu Int32 · Lưu trong RAM vi điều khiển"
                    : $"{counterCount} Counters (COUNTER0 – COUNTER{Math.Max(0, counterCount - 1)}) · Type Int32 · Stored in MCU RAM",
                Icon = "⏱️",
                BadgeColor = "#E11D48"
            },
            _ => new TagGroupInfo
            {
                GroupCode = "ALL",
                GroupName = isVi ? $"Toàn Bộ Không Gian Tag Hệ Thống ({totalTags} Tags)" : $"All System Tag Space ({totalTags} Tags)",
                Description = isVi
                    ? $"Quản lý tập trung toàn bộ {totalTags} địa chỉ Tag trên SimplePLC bao gồm I/O vật lý (DI, DO, AI) và các biến nhớ logic/số học nội bộ."
                    : $"Centralized management of all {totalTags} tag addresses on SimplePLC including physical I/O (DI, DO, AI) and internal memory registers.",
                RoleInPLC = isVi
                    ? "Mọi Tag đều có thể gán tên gợi nhớ (Alias), đọc trạng thái và liên kết trực tiếp làm đầu vào cho Trigger, Guard hoặc đầu ra cho Action."
                    : "All tags can have descriptive aliases assigned, live monitored, and bound directly to Trigger, Guard, or Action nodes.",
                HardwareSpec = isVi
                    ? $"{totalTags} Tags · Ánh xạ trực tiếp sang Modbus FC03/FC16 và RAM/Flash vi điều khiển"
                    : $"{totalTags} Tags · Direct mapping to Modbus FC03/FC16 and MCU memory",
                Icon = "📋",
                BadgeColor = "#475569"
            }
        };
    }

    public List<ProjectTagData> ExportToProjectData()
    {
        return AllTags.Select(t => new ProjectTagData
        {
            Index = t.Index,
            Name = t.Name,
            Alias = t.Alias,
            Kind = t.Kind,
            Channel = t.Channel,
            Group = t.Group,
            Value = t.Value
        }).ToList();
    }

    public void LoadFromProjectData(IEnumerable<ProjectTagData> tagsData)
    {
        if (tagsData == null) return;
        foreach (var pTag in tagsData)
        {
            var existing = AllTags.FirstOrDefault(t => t.Index == pTag.Index || string.Equals(t.Name, pTag.Name, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.Alias = pTag.Alias;
                existing.Group = pTag.Group;
                existing.Channel = pTag.Channel;
                existing.Value = pTag.Value;
                if (pTag.Kind != TagKind.None) existing.Kind = pTag.Kind;
            }
            else
            {
                AllTags.Add(new TagModel
                {
                    Index = pTag.Index,
                    Name = pTag.Name,
                    Alias = pTag.Alias,
                    Kind = pTag.Kind,
                    Channel = pTag.Channel,
                    Group = pTag.Group,
                    Value = pTag.Value
                });
            }
        }
        ApplyFilter();
        OnPropertyChanged(nameof(TotalTagCount));
        OnPropertyChanged(nameof(TitleText));
        UpdateGroupButtons();
    }
}

public partial class GroupFilterItem : ObservableObject
{
    public string Key { get; set; } = string.Empty;

    [ObservableProperty]
    private string _label = string.Empty;

    [ObservableProperty]
    private bool _isSelected;
}

public class TagGroupInfo
{
    public string GroupCode { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RoleInPLC { get; set; } = string.Empty;
    public string HardwareSpec { get; set; } = string.Empty;
    public string Icon { get; set; } = "📋";
    public string BadgeColor { get; set; } = "#006487";
}

