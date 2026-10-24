using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using SimplePLC.Application.Logic.Compilation;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;

namespace SimplePLC.Studio.ViewModels;

public partial class RuleTableViewModel : ObservableObject
{
    private readonly TagCatalogViewModel _tagCatalog;
    private readonly Action<int>? _navigateToTab;

    public ObservableCollection<RuleItemModel> Rules { get; } = new();
    public ObservableCollection<RuleItemModel> FilteredRules { get; } = new();

    public ObservableCollection<ModbusCommandItemViewModel> ModbusCommands { get; } = new();
    public ObservableCollection<MqttServiceItemViewModel> MqttServices { get; } = new();
    public ObservableCollection<FunctionBlockItemViewModel> FunctionBlocks { get; } = new();

    [ObservableProperty]
    private int _selectedSubTabIndex = 0; // 0: Rules, 1: Modbus, 2: MQTT, 3: Function Blocks

    public int ModbusCommandCount => ModbusCommands.Count;
    public int MqttServiceCount => MqttServices.Count;
    public int FunctionBlockCount => FunctionBlocks.Count;

    [RelayCommand]
    public void SelectSubTab(object? param)
    {
        if (param != null && int.TryParse(param.ToString(), out int tabIndex))
        {
            SelectedSubTabIndex = tabIndex;
        }
    }

    [ObservableProperty]
    private RuleItemModel? _selectedRule;

    [ObservableProperty]
    private bool _isDevPayloadVisible;

    [ObservableProperty]
    private bool _isFlowInspectorExpanded = true;

    public string FlowInspectorToggleText => IsFlowInspectorExpanded
        ? LocalizationService.Tr("RuleFlowInspectorCollapse")
        : LocalizationService.Tr("RuleFlowInspectorExpand");

    public string FlowInspectorToggleIcon => IsFlowInspectorExpanded ? "▲" : "▼";

    [ObservableProperty]
    private string _searchFilter = string.Empty;

    [ObservableProperty]
    private string _statusFilter = "ALL"; // "ALL", "ACTIVE", "DISABLED"

    [ObservableProperty]
    private int _activeRuleCount;

    public int TotalRuleCount => Rules.Count;
    public int DisabledRuleCount => Rules.Count(r => !r.Enabled);

    public int MemoryUsageBytes => Rules.Count * 32;
    public int MemoryUsagePercent => (int)Math.Round((double)TotalRuleCount / 100.0 * 100.0);
    public bool HasRules => Rules.Count > 0;
    public bool HasFilteredRules => FilteredRules.Count > 0;

    public string ActiveCountText => string.Format(LocalizationService.Tr("RuleTableActiveCountFormat"), TotalRuleCount);
    public string MemoryStatusText => string.Format(LocalizationService.Tr("RuleTableMemoryUsageFormat"), TotalRuleCount, MemoryUsagePercent);

    public string FilterAllLabel => string.Format(LocalizationService.Tr("RuleFilterAll"), TotalRuleCount);
    public string FilterActiveLabel => string.Format(LocalizationService.Tr("RuleFilterActive"), ActiveRuleCount);
    public string FilterDisabledLabel => string.Format(LocalizationService.Tr("RuleFilterDisabled"), DisabledRuleCount);

    public bool IsFilterAll => StatusFilter == "ALL";
    public bool IsFilterActive => StatusFilter == "ACTIVE";
    public bool IsFilterDisabled => StatusFilter == "DISABLED";

    public RuleTableViewModel(TagCatalogViewModel tagCatalog, Action<int>? navigateToTab = null)
    {
        _tagCatalog = tagCatalog;
        _navigateToTab = navigateToTab;

        LocalizationService.Instance.LanguageChanged += () =>
        {
            RefreshAllProperties();
            foreach (var r in Rules)
            {
                r.UpdateNarrative();
            }
        };

        // Note: New projects start clean with 0 rules per user requirements.
        ApplyFilter();
    }

    partial void OnSearchFilterChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnStatusFilterChanged(string value)
    {
        OnPropertyChanged(nameof(IsFilterAll));
        OnPropertyChanged(nameof(IsFilterActive));
        OnPropertyChanged(nameof(IsFilterDisabled));
        ApplyFilter();
    }

    [RelayCommand]
    public void SetStatusFilter(string filter)
    {
        if (StatusFilter != filter)
        {
            StatusFilter = filter;
        }
    }

    [RelayCommand]
    public void ClearSearch()
    {
        SearchFilter = string.Empty;
    }

    public Action<RuleItemModel>? OnEditRuleRequested { get; set; }
    public Action? OnNewRuleRequested { get; set; }
    public Func<Task>? OnUploadFromDeviceRequested { get; set; }
    public Action? OnOpenModbusPortSettingsRequested { get; set; }
    public Action? OnOpenMqttBrokerSettingsRequested { get; set; }
    public Action<ModbusCommandItemViewModel>? OnDeleteModbusCommandRequested { get; set; }
    public Action<MqttServiceItemViewModel>? OnDeleteMqttServiceRequested { get; set; }
    public Action<FunctionBlockItemViewModel>? OnDeleteFunctionBlockRequested { get; set; }

    [RelayCommand]
    public void OpenModbusPortSettings()
    {
        OnOpenModbusPortSettingsRequested?.Invoke();
    }

    [RelayCommand]
    public void OpenMqttBrokerSettings()
    {
        OnOpenMqttBrokerSettingsRequested?.Invoke();
    }

    public string SubTabLogicLabel => $"{LocalizationService.Tr("RuleSubTabLogic")} ({TotalRuleCount})";
    public string SubTabModbusLabel => $"{LocalizationService.Tr("RuleSubTabModbus")} ({ModbusCommandCount})";
    public string SubTabMqttLabel => $"{LocalizationService.Tr("RuleSubTabMqtt")} ({MqttServiceCount})";
    public string SubTabFunctionBlocksLabel => $"{LocalizationService.Tr("RuleSubTabFunctionBlocks")} ({FunctionBlockCount})";

    [RelayCommand]
    public async Task UploadFromDeviceAsync()
    {
        if (OnUploadFromDeviceRequested != null)
        {
            await OnUploadFromDeviceRequested.Invoke();
        }
    }

    [RelayCommand]
    public void EditInLogicGraph(RuleItemModel? rule = null)
    {
        var target = rule ?? SelectedRule;
        if (target != null)
        {
            OnEditRuleRequested?.Invoke(target);
        }
        _navigateToTab?.Invoke(0);
    }

    [RelayCommand]
    public void CreateNewRule()
    {
        OnNewRuleRequested?.Invoke();
        _navigateToTab?.Invoke(0);
    }

    [RelayCommand]
    public void DeleteRule(RuleItemModel? rule = null)
    {
        var target = rule ?? SelectedRule;
        if (target == null) return;

        Rules.Remove(target);
        for (int i = 0; i < Rules.Count; i++)
        {
            Rules[i].Index = i;
            Rules[i].Id = $"R{i + 1}";
        }
        ApplyFilter();
    }

    [RelayCommand]
    public void DeleteModbusCommand(ModbusCommandItemViewModel? cmd)
    {
        if (cmd == null) return;
        ModbusCommands.Remove(cmd);
        OnPropertyChanged(nameof(ModbusCommandCount));
        OnPropertyChanged(nameof(SubTabModbusLabel));
        OnDeleteModbusCommandRequested?.Invoke(cmd);
    }

    [RelayCommand]
    public void DeleteMqttService(MqttServiceItemViewModel? svc)
    {
        if (svc == null) return;
        MqttServices.Remove(svc);
        OnPropertyChanged(nameof(MqttServiceCount));
        OnPropertyChanged(nameof(SubTabMqttLabel));
        OnDeleteMqttServiceRequested?.Invoke(svc);
    }

    [RelayCommand]
    public void DeleteFunctionBlock(FunctionBlockItemViewModel? fb)
    {
        if (fb == null) return;
        FunctionBlocks.Remove(fb);
        OnPropertyChanged(nameof(FunctionBlockCount));
        OnPropertyChanged(nameof(SubTabFunctionBlocksLabel));
        OnDeleteFunctionBlockRequested?.Invoke(fb);
    }

    [RelayCommand]
    public void DeduplicateRules()
    {
        int removed = DeduplicateRulesCore();
        try
        {
            if (removed > 0)
            {
                MessageBox.Show(
                    LocalizationService.Instance.IsVietnamese
                        ? $"Đã tự động loại bỏ {removed} quy tắc trùng lặp thành công!\nBảng Rule hiện có {Rules.Count} quy tắc duy nhất."
                        : $"Successfully removed {removed} duplicate rule(s)!\nRule table now has {Rules.Count} unique rules.",
                    "SynaptiX IDE",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(
                    LocalizationService.Instance.IsVietnamese
                        ? "Không phát hiện quy tắc nào bị trùng lặp trong Bảng Rule."
                        : "No duplicate rules found in Rule Table.",
                    "SynaptiX IDE",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        catch
        {
            // Headless / non-interactive fallback
        }
    }

    public int DeduplicateRulesCore()
    {
        var seen = new HashSet<string>();
        var duplicates = new List<RuleItemModel>();

        foreach (var r in Rules)
        {
            string sig = $"{r.TriggerType}|{r.TriggerTag?.Index}|{r.ForMs}|{r.CompareOp}|{r.ThresholdLo}|{r.ThresholdHi}|{r.GuardTag?.Index}|{r.GuardNegated}|{r.ActionType}|{r.ActionTag?.Index}|{r.ActionParam}";
            if (!seen.Add(sig))
            {
                duplicates.Add(r);
            }
        }

        if (duplicates.Count > 0)
        {
            foreach (var d in duplicates)
            {
                Rules.Remove(d);
            }

            for (int i = 0; i < Rules.Count; i++)
            {
                Rules[i].Index = i;
                Rules[i].Id = $"R{i + 1}";
            }

            ApplyFilter();
        }

        return duplicates.Count;
    }

    [RelayCommand]
    public void NavigateToBlueprints()
    {
        _navigateToTab?.Invoke(3);
    }

    [RelayCommand]
    public void ToggleDevPayload()
    {
        IsDevPayloadVisible = !IsDevPayloadVisible;
    }

    [RelayCommand]
    public void ToggleFlowInspector()
    {
        IsFlowInspectorExpanded = !IsFlowInspectorExpanded;
        OnPropertyChanged(nameof(FlowInspectorToggleText));
        OnPropertyChanged(nameof(FlowInspectorToggleIcon));
        OnPropertyChanged(nameof(SubTabLogicLabel));
        OnPropertyChanged(nameof(SubTabModbusLabel));
        OnPropertyChanged(nameof(SubTabMqttLabel));
        OnPropertyChanged(nameof(SubTabFunctionBlocksLabel));
    }

    [RelayCommand]
    public void CopyHex(RuleItemModel? rule = null)
    {
        var target = rule ?? SelectedRule;
        if (target != null && !string.IsNullOrEmpty(target.RawHex))
        {
            try
            {
                Clipboard.SetText(target.RawHex);
            }
            catch
            {
                // Headless test or non-interactive safe fallback
            }
        }
    }

    [RelayCommand]
    public void ExportCsv()
    {
        if (Rules.Count == 0) return;

        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "CSV File (*.csv)|*.csv",
            FileName = $"SynaptiX_Rules_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("ID,Enabled,Trigger,Guard,Action,Narrative,RawHex32B");
                foreach (var r in Rules)
                {
                    sb.AppendLine($"\"{r.Id}\",{r.Enabled},\"{r.TriggerSummary}\",\"{r.GuardSummary}\",\"{r.ActionSummary}\",\"{r.Narrative.Replace("\"", "\"\"")}\",\"{r.RawHex}\"");
                }
                System.IO.File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);

                MessageBox.Show(
                    string.Format(LocalizationService.Tr("RuleExportSuccess"), Rules.Count, sfd.FileName),
                    "SynaptiX IDE",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "SynaptiX IDE", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void WireRule(RuleItemModel rule)
    {
        rule.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(RuleItemModel.Enabled))
            {
                RefreshAllProperties();
                if (StatusFilter != "ALL")
                {
                    ApplyFilter();
                }
            }
        };
    }

    public void ApplyFilter()
    {
        var query = Rules.AsEnumerable();

        if (StatusFilter == "ACTIVE")
        {
            query = query.Where(r => r.Enabled);
        }
        else if (StatusFilter == "DISABLED")
        {
            query = query.Where(r => !r.Enabled);
        }

        if (!string.IsNullOrWhiteSpace(SearchFilter))
        {
            string term = SearchFilter.Trim();
            query = query.Where(r =>
                (r.Id != null && r.Id.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (r.TriggerSummary != null && r.TriggerSummary.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (r.GuardSummary != null && r.GuardSummary.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (r.ActionSummary != null && r.ActionSummary.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (r.Narrative != null && r.Narrative.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (r.RawHex != null && r.RawHex.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (r.TriggerTag != null && r.TriggerTag.Name.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (r.GuardTag != null && r.GuardTag.Name.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (r.ActionTag != null && r.ActionTag.Name.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                r.ActionType.ToString().Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.TriggerType.ToString().Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        var list = query.ToList();
        FilteredRules.Clear();
        foreach (var r in list)
        {
            FilteredRules.Add(r);
        }

        if (SelectedRule == null || !FilteredRules.Contains(SelectedRule))
        {
            SelectedRule = FilteredRules.FirstOrDefault();
        }

        RefreshAllProperties();
    }

    private void RefreshAllProperties()
    {
        ActiveRuleCount = Rules.Count(r => r.Enabled);
        OnPropertyChanged(nameof(TotalRuleCount));
        OnPropertyChanged(nameof(DisabledRuleCount));
        OnPropertyChanged(nameof(MemoryUsageBytes));
        OnPropertyChanged(nameof(MemoryUsagePercent));
        OnPropertyChanged(nameof(HasRules));
        OnPropertyChanged(nameof(HasFilteredRules));
        OnPropertyChanged(nameof(ActiveCountText));
        OnPropertyChanged(nameof(MemoryStatusText));
        OnPropertyChanged(nameof(FilterAllLabel));
        OnPropertyChanged(nameof(FilterActiveLabel));
        OnPropertyChanged(nameof(FilterDisabledLabel));
        OnPropertyChanged(nameof(FlowInspectorToggleText));
        OnPropertyChanged(nameof(FlowInspectorToggleIcon));
    }

    /// <summary>
    /// Chiếu toàn bộ CompiledProgram thành các dòng RuleItemModel chỉ đọc để hiển thị và phân tích byte disassembly.
    /// </summary>
    public void ProjectProgram(SimplePLC.Application.Logic.Compilation.CompiledProgram? program)
    {
        Rules.Clear();
        if (program != null && program.Rules.Count > 0)
        {
            foreach (var domainRule in program.Rules)
            {
                var r = RuleItemModel.FromDomainRule(domainRule, _tagCatalog.AllTags);
                WireRule(r);
                Rules.Add(r);
            }
        }
        ApplyFilter();
    }

    public void ReplaceRules(IEnumerable<RuleItemModel> rules)
    {
        Rules.Clear();
        foreach (var rule in rules)
        {
            WireRule(rule);
            Rules.Add(rule);
        }
        ApplyFilter();
    }

    /// <summary>
    /// Thêm mới hoặc cập nhật một/nhiều Rule được biên dịch từ bản vẽ Canvas vào Bảng Rule.
    /// Quản lý theo DiagramId để cập nhật đúng các rule của cùng một bản vẽ mà không sinh trùng lặp.
    /// </summary>
    public List<RuleItemModel> AddOrUpdateCompiledRules(
        IEnumerable<SimplePLC.Domain.Models.Rule> domainRules,
        List<ProjectNodeData>? sourceNodes,
        List<ProjectConnectionData>? sourceConnections,
        string? targetRuleId,
        string? diagramId = null)
    {
        var domainList = domainRules.ToList();
        if (domainList.Count == 0) return new List<RuleItemModel>();

        var result = new List<RuleItemModel>();

        // 1. Tìm các rule đã có trong bảng thuộc về bản vẽ này
        List<RuleItemModel> existingDiagramRules = new();
        if (!string.IsNullOrEmpty(diagramId))
        {
            existingDiagramRules = Rules.Where(r => r.DiagramId == diagramId).ToList();
        }

        if (existingDiagramRules.Count == 0 && !string.IsNullOrEmpty(targetRuleId))
        {
            var target = Rules.FirstOrDefault(r => r.Id == targetRuleId);
            if (target != null)
            {
                if (!string.IsNullOrEmpty(target.DiagramId))
                {
                    existingDiagramRules = Rules.Where(r => r.DiagramId == target.DiagramId).ToList();
                }
                else
                {
                    existingDiagramRules.Add(target);
                }
            }
        }

        var effectiveDiagramId = !string.IsNullOrEmpty(diagramId)
            ? diagramId
            : (existingDiagramRules.FirstOrDefault()?.DiagramId ?? Guid.NewGuid().ToString("N"));

        int updateCount = Math.Min(domainList.Count, existingDiagramRules.Count);

        // 2. Cập nhật in-place các rule đã có của bản vẽ này
        for (int i = 0; i < updateCount; i++)
        {
            var existing = existingDiagramRules[i];
            var updated = RuleItemModel.FromDomainRule(domainList[i], _tagCatalog.AllTags);
            existing.TriggerType = updated.TriggerType;
            existing.TriggerTag = updated.TriggerTag;
            existing.ForMs = updated.ForMs;
            existing.CompareOp = updated.CompareOp;
            existing.GuardTag = updated.GuardTag;
            existing.ThresholdLo = updated.ThresholdLo;
            existing.ThresholdHi = updated.ThresholdHi;
            existing.GuardNegated = updated.GuardNegated;
            existing.ActionType = updated.ActionType;
            existing.ActionTag = updated.ActionTag;
            existing.ActionParam = updated.ActionParam;
            existing.RawHex = updated.RawHex;
            existing.RawHexBreakdown = updated.RawHexBreakdown;
            existing.DiagramId = effectiveDiagramId;
            existing.SourceNodes = sourceNodes != null ? new List<ProjectNodeData>(sourceNodes) : null;
            existing.SourceConnections = sourceConnections != null ? new List<ProjectConnectionData>(sourceConnections) : null;
            existing.UpdateNarrative();
            result.Add(existing);
        }

        // 3. Thêm mới nếu bản vẽ có thêm nhánh logic (domainList nhiều hơn existingDiagramRules)
        for (int i = updateCount; i < domainList.Count; i++)
        {
            var item = RuleItemModel.FromDomainRule(domainList[i], _tagCatalog.AllTags);
            int nextNum = 1;
            while (Rules.Any(r => r.Id == $"R{nextNum}"))
            {
                nextNum++;
            }
            item.Id = $"R{nextNum}";
            item.Index = Rules.Count;
            item.DiagramId = effectiveDiagramId;
            item.SourceNodes = sourceNodes != null ? new List<ProjectNodeData>(sourceNodes) : null;
            item.SourceConnections = sourceConnections != null ? new List<ProjectConnectionData>(sourceConnections) : null;
            WireRule(item);
            Rules.Add(item);
            result.Add(item);
        }

        // 4. Nếu trên bản vẽ đã xóa bớt nhánh logic, loại bỏ các rule thừa của bản vẽ này
        for (int i = updateCount; i < existingDiagramRules.Count; i++)
        {
            var excess = existingDiagramRules[i];
            Rules.Remove(excess);
        }

        ApplyFilter();
        SelectedRule = result.LastOrDefault() ?? SelectedRule;
        return result;
    }

    public List<ProjectRuleData> ExportToProjectData()
    {
        var list = new List<ProjectRuleData>();
        foreach (var r in Rules)
        {
            list.Add(new ProjectRuleData
            {
                Id = r.Id,
                Index = r.Index,
                Enabled = r.Enabled,
                TriggerType = r.TriggerType,
                TriggerTagName = r.TriggerTag?.Name ?? string.Empty,
                ForMs = r.ForMs,
                CompareOp = r.CompareOp,
                GuardTagName = r.GuardTag?.Name ?? string.Empty,
                ThresholdLo = r.ThresholdLo,
                ThresholdHi = r.ThresholdHi,
                GuardNegated = r.GuardNegated,
                ActionType = r.ActionType,
                ActionTagName = r.ActionTag?.Name ?? string.Empty,
                ActionParam = r.ActionParam,
                Narrative = r.Narrative,
                RawHex = r.RawHex,
                DiagramId = r.DiagramId,
                SourceNodes = r.SourceNodes,
                SourceConnections = r.SourceConnections
            });
        }
        return list;
    }

    public void LoadFromProjectData(List<ProjectRuleData>? ruleDataList)
    {
        Rules.Clear();
        if (ruleDataList == null || ruleDataList.Count == 0)
        {
            ApplyFilter();
            return;
        }

        var allTags = _tagCatalog.AllTags;
        foreach (var data in ruleDataList)
        {
            var r = new RuleItemModel
            {
                Id = data.Id,
                Index = data.Index,
                Enabled = data.Enabled,
                TriggerType = data.TriggerType,
                TriggerTag = allTags.FirstOrDefault(t => string.Equals(t.Name, data.TriggerTagName, StringComparison.OrdinalIgnoreCase)),
                ForMs = data.ForMs,
                CompareOp = data.CompareOp,
                GuardTag = allTags.FirstOrDefault(t => string.Equals(t.Name, data.GuardTagName, StringComparison.OrdinalIgnoreCase)),
                ThresholdLo = data.ThresholdLo,
                ThresholdHi = data.ThresholdHi,
                GuardNegated = data.GuardNegated,
                ActionType = data.ActionType,
                ActionTag = allTags.FirstOrDefault(t => string.Equals(t.Name, data.ActionTagName, StringComparison.OrdinalIgnoreCase)),
                ActionParam = data.ActionParam,
                RawHex = data.RawHex,
                DiagramId = data.DiagramId,
                SourceNodes = data.SourceNodes,
                SourceConnections = data.SourceConnections
            };
            r.UpdateNarrative();
            WireRule(r);
            Rules.Add(r);
        }
        ApplyFilter();
    }

    public void LoadDefaultRules()
    {
        Rules.Clear();
        var tags = _tagCatalog.AllTags;

        // R1: Andon - Máy dừng liên tục >15s trong ca -> Bật đèn đỏ DO0
        var r1 = new RuleItemModel
        {
            Id = "R1", Index = 0, Enabled = true,
            TriggerType = TriggerType.ON_FALL, TriggerTag = tags.FirstOrDefault(t => t.Name == "DI0"),
            ForMs = 15000,
            GuardTag = tags.FirstOrDefault(t => t.Name == "VFLAG0"),
            ActionType = ActionType.SET_TAG, ActionTag = tags.FirstOrDefault(t => t.Name == "DO0"), ActionParam = 1
        };
        r1.UpdateNarrative();
        WireRule(r1);
        Rules.Add(r1);

        // R2: Bấm nút gọi cấp liệu DI1 -> Đảo trạng thái đèn vàng DO1
        var r2 = new RuleItemModel
        {
            Id = "R2", Index = 1, Enabled = true,
            TriggerType = TriggerType.ON_RISE, TriggerTag = tags.FirstOrDefault(t => t.Name == "DI1"),
            GuardTag = tags.FirstOrDefault(t => t.Name == "DI0"),
            ActionType = ActionType.TOGGLE_TAG, ActionTag = tags.FirstOrDefault(t => t.Name == "DO1"), ActionParam = 0
        };
        r2.UpdateNarrative();
        WireRule(r2);
        Rules.Add(r2);

        // R3: Đếm xung Line 1 (DI2) -> Cộng bộ đếm riêng Counter1 (VREG_RETAIN0)
        var r3 = new RuleItemModel
        {
            Id = "R3", Index = 2, Enabled = true,
            TriggerType = TriggerType.ON_RISE, TriggerTag = tags.FirstOrDefault(t => t.Name == "DI2"),
            ActionType = ActionType.INC_COUNTER, ActionTag = tags.FirstOrDefault(t => t.Name == "VREG_RETAIN0"), ActionParam = 1
        };
        r3.UpdateNarrative();
        WireRule(r3);
        Rules.Add(r3);

        // R4: Đếm xung Line 1 (DI2) -> Cộng bộ đếm tổng Total (VREG_RETAIN2)
        var r4 = new RuleItemModel
        {
            Id = "R4", Index = 3, Enabled = true,
            TriggerType = TriggerType.ON_RISE, TriggerTag = tags.FirstOrDefault(t => t.Name == "DI2"),
            ActionType = ActionType.INC_COUNTER, ActionTag = tags.FirstOrDefault(t => t.Name == "VREG_RETAIN2"), ActionParam = 1
        };
        r4.UpdateNarrative();
        WireRule(r4);
        Rules.Add(r4);

        // R5: Đếm xung Line 2 (DI3) -> Cộng bộ đếm riêng Counter2 (VREG_RETAIN1)
        var r5 = new RuleItemModel
        {
            Id = "R5", Index = 4, Enabled = true,
            TriggerType = TriggerType.ON_RISE, TriggerTag = tags.FirstOrDefault(t => t.Name == "DI3"),
            ActionType = ActionType.INC_COUNTER, ActionTag = tags.FirstOrDefault(t => t.Name == "VREG_RETAIN1"), ActionParam = 1
        };
        r5.UpdateNarrative();
        WireRule(r5);
        Rules.Add(r5);

        // R6: Đếm xung Line 2 (DI3) -> Cộng bộ đếm tổng Total (VREG_RETAIN2)
        var r6 = new RuleItemModel
        {
            Id = "R6", Index = 5, Enabled = true,
            TriggerType = TriggerType.ON_RISE, TriggerTag = tags.FirstOrDefault(t => t.Name == "DI3"),
            ActionType = ActionType.INC_COUNTER, ActionTag = tags.FirstOrDefault(t => t.Name == "VREG_RETAIN2"), ActionParam = 1
        };
        r6.UpdateNarrative();
        WireRule(r6);
        Rules.Add(r6);

        // R7: Áp suất dầu (AI0) vượt ngưỡng >80 bar -> Bật còi cảnh báo DO3
        var r7 = new RuleItemModel
        {
            Id = "R7", Index = 6, Enabled = true,
            TriggerType = TriggerType.ON_CHANGE, TriggerTag = tags.FirstOrDefault(t => t.Name == "AI0"),
            CompareOp = CompareOp.GT, ThresholdLo = 80,
            ActionType = ActionType.SET_TAG, ActionTag = tags.FirstOrDefault(t => t.Name == "DO3"), ActionParam = 1
        };
        r7.UpdateNarrative();
        WireRule(r7);
        Rules.Add(r7);

        // R8: Nhiệt độ bể (AI1) ngoài khoảng an toàn 40..75°C -> Kích hoạt cảnh báo (Send Alarm code 102)
        var r8 = new RuleItemModel
        {
            Id = "R8", Index = 7, Enabled = true,
            TriggerType = TriggerType.ON_CHANGE, TriggerTag = tags.FirstOrDefault(t => t.Name == "AI1"),
            CompareOp = CompareOp.BETWEEN, ThresholdLo = 40, ThresholdHi = 75,
            GuardNegated = true,
            ActionType = ActionType.SEND_ALARM, ActionTag = tags.FirstOrDefault(t => t.Name == "VFLAG1"), ActionParam = 102
        };
        r8.UpdateNarrative();
        WireRule(r8);
        Rules.Add(r8);

        // R9: Tự động vào ca lúc 07:00 sáng -> Bật cờ Trong ca (VFLAG0 = 1)
        var r9 = new RuleItemModel
        {
            Id = "R9", Index = 8, Enabled = true,
            TriggerType = TriggerType.TIME_POINT, TriggerTag = tags.FirstOrDefault(t => t.Name == "NONE"),
            ThresholdLo = 700,
            ActionType = ActionType.SET_TAG, ActionTag = tags.FirstOrDefault(t => t.Name == "VFLAG0"), ActionParam = 1
        };
        r9.UpdateNarrative();
        WireRule(r9);
        Rules.Add(r9);

        // R10: Nhịp tim hệ thống định kỳ mỗi 1000ms -> Đảo đèn báo trạng thái DO2
        var r10 = new RuleItemModel
        {
            Id = "R10", Index = 9, Enabled = true,
            TriggerType = TriggerType.INTERVAL, TriggerTag = tags.FirstOrDefault(t => t.Name == "NONE"),
            ForMs = 1000,
            ActionType = ActionType.TOGGLE_TAG, ActionTag = tags.FirstOrDefault(t => t.Name == "DO2"), ActionParam = 0
        };
        r10.UpdateNarrative();
        WireRule(r10);
        Rules.Add(r10);

        ApplyFilter();
    }

    /// <summary>
    /// Chuyển đổi toàn bộ danh sách quy tắc hiện tại trên bảng sang Domain RuleTable chuẩn.
    /// </summary>
    public SimplePLC.Domain.Models.RuleTable ToDomainRuleTable(SimplePLC.Domain.Models.ProductDefinition product)
    {
        var table = new SimplePLC.Domain.Models.RuleTable();
        foreach (var r in Rules)
        {
            table.AddRule(r.ToDomainRule(product));
        }
        return table;
    }

    /// <summary>
    /// Nạp và hiển thị danh sách quy tắc từ Domain RuleTable lên giao diện.
    /// </summary>
    public void LoadFromDomainRuleTable(SimplePLC.Domain.Models.RuleTable domainTable)
    {
        Rules.Clear();
        foreach (var domainRule in domainTable.Rules)
        {
            var r = RuleItemModel.FromDomainRule(domainRule, _tagCatalog.AllTags);
            WireRule(r);
            Rules.Add(r);
        }
        ApplyFilter();
    }

    /// <summary>
    /// Cập nhật các bảng phân hệ (Modbus Master, MQTT Services, Function Blocks) từ hình ảnh thiết bị đọc ngược từ MCU.
    /// </summary>
    public void UpdateSubsystemsFromDevice(FullDeviceConfigurationDto fullConfig)
    {
        ModbusCommands.Clear();
        if (fullConfig.ModbusMasterReads != null)
        {
            for (int i = 0; i < fullConfig.ModbusMasterReads.Count; i++)
            {
                var r = fullConfig.ModbusMasterReads[i];
                ModbusCommands.Add(new ModbusCommandItemViewModel
                {
                    Id = $"MB{i + 1}",
                    BlockType = LocalizationService.Instance.IsVietnamese ? "Đọc Modbus (Poller)" : "Modbus Read (Poller)",
                    PortDisplay = $"{(LocalizationService.Instance.IsVietnamese ? "Cổng" : "Port")} {r.PortIndex + 1}",
                    SlaveId = r.SlaveId,
                    FunctionCodeDisplay = $"FC{(byte)r.FunctionCode:D2} ({r.FunctionCode})",
                    AddressDisplay = $"0x{r.StartAddress:X4} ({r.StartAddress})",
                    LengthDisplay = $"{r.RegCount} regs",
                    ModeDisplay = r.ReadMode == ModbusReadMode.Cyclic 
                        ? $"Cyclic ({r.PollIntervalMs}ms)" 
                        : (LocalizationService.Instance.IsVietnamese ? "Xung sườn lên" : "Rising Edge"),
                    PinsOrTargetDisplay = r.Pins.Count > 0 
                        ? $"{r.Pins.Count} Pins ({string.Join(", ", r.Pins.Select(p => p.Label))})" 
                        : "0 Pins",
                    Status = r.Enabled ? "Active" : "Disabled",
                    IsActive = r.Enabled
                });
            }
        }

        if (fullConfig.ModbusMasterWrites != null)
        {
            for (int i = 0; i < fullConfig.ModbusMasterWrites.Count; i++)
            {
                var w = fullConfig.ModbusMasterWrites[i];
                ModbusCommands.Add(new ModbusCommandItemViewModel
                {
                    Id = $"MW{i + 1}",
                    BlockType = LocalizationService.Instance.IsVietnamese ? "Ghi Modbus (Writer)" : "Modbus Write (Writer)",
                    PortDisplay = $"{(LocalizationService.Instance.IsVietnamese ? "Cổng" : "Port")} {w.PortIndex + 1}",
                    SlaveId = w.SlaveId,
                    FunctionCodeDisplay = $"FC{(byte)w.FunctionCode:D2} ({w.FunctionCode})",
                    AddressDisplay = $"0x{w.StartAddress:X4} ({w.StartAddress})",
                    LengthDisplay = $"{w.PointCount} pts",
                    ModeDisplay = w.TriggerMode == ModbusWriteTriggerMode.Cyclic 
                        ? $"Cyclic ({w.CyclicIntervalMs}ms)" 
                        : (LocalizationService.Instance.IsVietnamese ? "Xung kích hoạt" : "Trigger"),
                    PinsOrTargetDisplay = $"{w.WriteMode}",
                    Status = w.Enabled ? "Active" : "Disabled",
                    IsActive = w.Enabled
                });
            }
        }

        if (fullConfig.DirectWrites != null)
        {
            for (int i = 0; i < fullConfig.DirectWrites.Count; i++)
            {
                var dw = fullConfig.DirectWrites[i];
                ModbusCommands.Add(new ModbusCommandItemViewModel
                {
                    Id = $"DW{i + 1}",
                    BlockType = LocalizationService.Instance.IsVietnamese ? "Ghi trực tiếp (Direct)" : "Direct Write",
                    PortDisplay = LocalizationService.Instance.IsVietnamese ? "Nội bộ" : "Internal",
                    SlaveId = 0,
                    FunctionCodeDisplay = $"{dw.Mode}",
                    AddressDisplay = $"{dw.TargetPin}",
                    LengthDisplay = $"{dw.ChannelCount} ch",
                    ModeDisplay = LocalizationService.Instance.IsVietnamese ? "Xung kích hoạt" : "Action Trigger",
                    PinsOrTargetDisplay = $"{dw.TargetPin}",
                    Status = "Active",
                    IsActive = true
                });
            }
        }

        MqttServices.Clear();
        if (fullConfig.MqttTelemetries != null)
        {
            for (int i = 0; i < fullConfig.MqttTelemetries.Count; i++)
            {
                var pub = fullConfig.MqttTelemetries[i];
                MqttServices.Add(new MqttServiceItemViewModel
                {
                    Id = $"PUB{i + 1}",
                    ServiceType = LocalizationService.Instance.IsVietnamese ? "Phát dữ liệu (Telemetry)" : "Telemetry Publish",
                    Topic = pub.Topic,
                    Qos = (byte)pub.Qos,
                    ModeDisplay = pub.PublishMode == "Periodic" 
                        ? $"Cyclic ({pub.IntervalMs}ms)" 
                        : pub.PublishMode,
                    PayloadSummary = $"{pub.Fields.Count} JSON fields",
                    Status = "Active",
                    IsActive = true
                });
            }
        }

        if (fullConfig.MqttSubscriptions != null)
        {
            for (int i = 0; i < fullConfig.MqttSubscriptions.Count; i++)
            {
                var sub = fullConfig.MqttSubscriptions[i];
                MqttServices.Add(new MqttServiceItemViewModel
                {
                    Id = $"SUB{i + 1}",
                    ServiceType = LocalizationService.Instance.IsVietnamese ? "Nhận lệnh (Inbound)" : "Inbound Subscribe",
                    Topic = sub.Topic,
                    Qos = (byte)sub.Qos,
                    ModeDisplay = LocalizationService.Instance.IsVietnamese ? "Bóc tách lệnh" : "Message Ingestion",
                    PayloadSummary = $"{sub.Pins.Count} output pins",
                    Status = "Active",
                    IsActive = true
                });
            }
        }

        FunctionBlocks.Clear();
        if (fullConfig.Timers != null)
        {
            for (int i = 0; i < fullConfig.Timers.Count; i++)
            {
                var t = fullConfig.Timers[i];
                if (t.Mode != SPLC_TimerMode.DISABLED)
                {
                    FunctionBlocks.Add(new FunctionBlockItemViewModel
                    {
                        Id = $"T{i + 1}",
                        BlockType = $"{t.Mode} Timer",
                        PresetDisplay = $"{t.PresetMs} ms",
                        InputSignal = $"RuleRef #{t.RuleRef}",
                        OutputSignal = $"Elapsed {t.ElapsedMs}ms",
                        Status = t.Running ? "Running" : (t.Q ? "Done" : "Idle"),
                        IsActive = true
                    });
                }
            }
        }

        if (fullConfig.Counters != null)
        {
            for (int i = 0; i < fullConfig.Counters.Count; i++)
            {
                var c = fullConfig.Counters[i];
                if (c.Mode != SPLC_CounterMode.DISABLED)
                {
                    FunctionBlocks.Add(new FunctionBlockItemViewModel
                    {
                        Id = $"C{i + 1}",
                        BlockType = $"{c.Mode} Counter",
                        PresetDisplay = $"{c.PresetValue} pulses",
                        InputSignal = $"Current {c.CurrentValue}",
                        OutputSignal = c.Q ? "Done (TRUE)" : "Counting",
                        Status = c.Q ? "Done" : "Counting",
                        IsActive = true
                    });
                }
            }
        }

        OnPropertyChanged(nameof(ModbusCommandCount));
        OnPropertyChanged(nameof(MqttServiceCount));
        OnPropertyChanged(nameof(FunctionBlockCount));
        OnPropertyChanged(nameof(SubTabModbusLabel));
        OnPropertyChanged(nameof(SubTabMqttLabel));
        OnPropertyChanged(nameof(SubTabFunctionBlocksLabel));
    }

    /// <summary>
    /// Cập nhật các bảng phân hệ (Modbus Master, MQTT Services, Function Blocks) từ chương trình đã biên dịch.
    /// </summary>
    public void UpdateSubsystemsFromProgram(CompiledProgram program)
    {
        ModbusCommands.Clear();
        if (program.ModbusMasterConfigs != null)
        {
            for (int i = 0; i < program.ModbusMasterConfigs.Count; i++)
            {
                var r = program.ModbusMasterConfigs[i];
                ModbusCommands.Add(new ModbusCommandItemViewModel
                {
                    Id = $"MB{i + 1}",
                    BlockType = LocalizationService.Instance.IsVietnamese ? "Đọc Modbus (Poller)" : "Modbus Read (Poller)",
                    PortDisplay = $"{(LocalizationService.Instance.IsVietnamese ? "Cổng" : "Port")} {r.PortIndex + 1}",
                    SlaveId = r.SlaveId,
                    FunctionCodeDisplay = $"FC{(byte)r.FunctionCode:D2} ({r.FunctionCode})",
                    AddressDisplay = $"0x{r.StartAddress:X4} ({r.StartAddress})",
                    LengthDisplay = $"{r.RegCount} regs",
                    ModeDisplay = r.ReadMode == ModbusReadMode.Cyclic 
                        ? $"Cyclic ({r.PollIntervalMs}ms)" 
                        : (LocalizationService.Instance.IsVietnamese ? "Xung sườn lên" : "Rising Edge"),
                    PinsOrTargetDisplay = r.Pins.Count > 0 
                        ? $"{r.Pins.Count} Pins ({string.Join(", ", r.Pins.Select(p => p.Label))})" 
                        : "0 Pins",
                    Status = r.Enabled ? "Active" : "Disabled",
                    IsActive = r.Enabled
                });
            }
        }

        if (program.ModbusMasterWriteConfigs != null)
        {
            for (int i = 0; i < program.ModbusMasterWriteConfigs.Count; i++)
            {
                var w = program.ModbusMasterWriteConfigs[i];
                ModbusCommands.Add(new ModbusCommandItemViewModel
                {
                    Id = $"MW{i + 1}",
                    BlockType = LocalizationService.Instance.IsVietnamese ? "Ghi Modbus (Writer)" : "Modbus Write (Writer)",
                    PortDisplay = $"{(LocalizationService.Instance.IsVietnamese ? "Cổng" : "Port")} {w.PortIndex + 1}",
                    SlaveId = w.SlaveId,
                    FunctionCodeDisplay = $"FC{(byte)w.FunctionCode:D2} ({w.FunctionCode})",
                    AddressDisplay = $"0x{w.StartAddress:X4} ({w.StartAddress})",
                    LengthDisplay = $"{w.PointCount} pts",
                    ModeDisplay = w.TriggerMode == ModbusWriteTriggerMode.Cyclic 
                        ? $"Cyclic ({w.CyclicIntervalMs}ms)" 
                        : (LocalizationService.Instance.IsVietnamese ? "Xung kích hoạt" : "Trigger"),
                    PinsOrTargetDisplay = $"{w.WriteMode}",
                    Status = w.Enabled ? "Active" : "Disabled",
                    IsActive = w.Enabled
                });
            }
        }

        if (program.DirectWriteConfigs != null)
        {
            for (int i = 0; i < program.DirectWriteConfigs.Count; i++)
            {
                var dw = program.DirectWriteConfigs[i];
                ModbusCommands.Add(new ModbusCommandItemViewModel
                {
                    Id = $"DW{i + 1}",
                    BlockType = LocalizationService.Instance.IsVietnamese ? "Ghi trực tiếp (Direct)" : "Direct Write",
                    PortDisplay = LocalizationService.Instance.IsVietnamese ? "Nội bộ" : "Internal",
                    SlaveId = 0,
                    FunctionCodeDisplay = $"{dw.Mode}",
                    AddressDisplay = $"{dw.TargetPin}",
                    LengthDisplay = $"{dw.ChannelCount} ch",
                    ModeDisplay = LocalizationService.Instance.IsVietnamese ? "Xung kích hoạt" : "Action Trigger",
                    PinsOrTargetDisplay = $"{dw.TargetPin}",
                    Status = "Active",
                    IsActive = true
                });
            }
        }

        MqttServices.Clear();
        if (program.MqttTelemetryConfigs != null)
        {
            for (int i = 0; i < program.MqttTelemetryConfigs.Count; i++)
            {
                var pub = program.MqttTelemetryConfigs[i];
                MqttServices.Add(new MqttServiceItemViewModel
                {
                    Id = $"PUB{i + 1}",
                    ServiceType = LocalizationService.Instance.IsVietnamese ? "Phát dữ liệu (Telemetry)" : "Telemetry Publish",
                    Topic = pub.Topic,
                    Qos = (byte)pub.Qos,
                    ModeDisplay = pub.PublishMode == "Periodic" 
                        ? $"Cyclic ({pub.IntervalMs}ms)" 
                        : pub.PublishMode,
                    PayloadSummary = $"{pub.Fields.Count} JSON fields",
                    Status = "Active",
                    IsActive = true
                });
            }
        }

        if (program.MqttSubscriptionConfigs != null)
        {
            for (int i = 0; i < program.MqttSubscriptionConfigs.Count; i++)
            {
                var sub = program.MqttSubscriptionConfigs[i];
                MqttServices.Add(new MqttServiceItemViewModel
                {
                    Id = $"SUB{i + 1}",
                    ServiceType = LocalizationService.Instance.IsVietnamese ? "Nhận lệnh (Inbound)" : "Inbound Subscribe",
                    Topic = sub.Topic,
                    Qos = (byte)sub.Qos,
                    ModeDisplay = LocalizationService.Instance.IsVietnamese ? "Bóc tách lệnh" : "Message Ingestion",
                    PayloadSummary = $"{sub.Pins.Count} output pins",
                    Status = "Active",
                    IsActive = true
                });
            }
        }

        FunctionBlocks.Clear();
        if (program.FunctionBlockTimers != null)
        {
            for (int i = 0; i < program.FunctionBlockTimers.Count; i++)
            {
                var t = program.FunctionBlockTimers[i];
                if (t.Mode != SPLC_TimerMode.DISABLED)
                {
                    FunctionBlocks.Add(new FunctionBlockItemViewModel
                    {
                        Id = $"T{i + 1}",
                        BlockType = $"{t.Mode} Timer",
                        PresetDisplay = $"{t.PresetMs} ms",
                        InputSignal = $"RuleRef #{t.RuleRef}",
                        OutputSignal = $"Elapsed {t.ElapsedMs}ms",
                        Status = t.Running ? "Running" : (t.Q ? "Done" : "Idle"),
                        IsActive = true
                    });
                }
            }
        }

        if (program.FunctionBlockCounters != null)
        {
            for (int i = 0; i < program.FunctionBlockCounters.Count; i++)
            {
                var c = program.FunctionBlockCounters[i];
                if (c.Mode != SPLC_CounterMode.DISABLED)
                {
                    FunctionBlocks.Add(new FunctionBlockItemViewModel
                    {
                        Id = $"C{i + 1}",
                        BlockType = $"{c.Mode} Counter",
                        PresetDisplay = $"{c.PresetValue} pulses",
                        InputSignal = $"Current {c.CurrentValue}",
                        OutputSignal = c.Q ? "Done (TRUE)" : "Counting",
                        Status = c.Q ? "Done" : "Counting",
                        IsActive = true
                    });
                }
            }
        }

        OnPropertyChanged(nameof(ModbusCommandCount));
        OnPropertyChanged(nameof(MqttServiceCount));
        OnPropertyChanged(nameof(FunctionBlockCount));
        OnPropertyChanged(nameof(SubTabModbusLabel));
        OnPropertyChanged(nameof(SubTabMqttLabel));
        OnPropertyChanged(nameof(SubTabFunctionBlocksLabel));
    }
}
