using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;

namespace SimplePLC.Studio.ViewModels;

public partial class SimulatorViewModel : ObservableObject
{
    private readonly TagCatalogViewModel _tagCatalog;
    private readonly RuleTableViewModel _ruleTable;
    private readonly LogicEditorViewModel? _logicEditor;
    private readonly DispatcherTimer _scanTimer;
    private readonly SimulatedClock _clock = new();
    private readonly RuntimeEngine _engine;

    public TagCatalogViewModel TagCatalog => _tagCatalog;
    public RuleTableViewModel RuleTable => _ruleTable;

    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _simClock = "08:30";
    [ObservableProperty] private int _simSpeed = 1;
    [ObservableProperty] private long _scanTick;
    [ObservableProperty] private long _scanNumber;
    [ObservableProperty] private string _gateWarning = string.Empty;
    [ObservableProperty] private bool _filterActiveOnly = false;
    [ObservableProperty] private RuntimeSnapshot? _latestSnapshot;

    public ObservableCollection<string> EventStream { get; } = new();
    public ObservableCollection<RuleEvaluationRecord> RuleStatusList { get; } = new();
    public ObservableCollection<string> ExecutionTraceFeed { get; } = new();

    public SimulatorViewModel(
        TagCatalogViewModel tagCatalog, 
        RuleTableViewModel? ruleTable = null,
        LogicEditorViewModel? logicEditor = null)
    {
        _tagCatalog = tagCatalog;
        _ruleTable = ruleTable ?? new RuleTableViewModel(tagCatalog);
        _logicEditor = logicEditor;
        _engine = new RuntimeEngine(tagCatalog.AllTags.ToList());

        if (_logicEditor != null)
        {
            _logicEditor.PropertyChanged += OnLogicEditorPropertyChanged;
        }

        _scanTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _scanTimer.Tick += OnScanCycle;

        // Chỉ chạy scan timer khi logic đã được biên dịch hợp lệ
        bool canAutoStart = _logicEditor == null || (_logicEditor.CompileState == CompileState.Valid && _logicEditor.CurrentProgram != null);
        _isRunning = canAutoStart;
        if (canAutoStart)
        {
            _scanTimer.Start();
        }

        AddEvent("Simulator sẵn sàng. RuntimeEngine chạy theo CompiledProgram.");
    }

    private void OnLogicEditorPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LogicEditorViewModel.CompileState) or nameof(LogicEditorViewModel.CurrentProgram))
        {
            if (_logicEditor == null) return;

            if (_logicEditor.CompileState is CompileState.Stale or CompileState.Invalid or CompileState.NotCompiled)
            {
                if (IsRunning)
                {
                    IsRunning = false;
                    _scanTimer.Stop();
                    GateWarning = "Tạm dừng mô phỏng: Logic đã thay đổi hoặc có lỗi. Vui lòng biên dịch thành công để tiếp tục.";
                    AddEvent(GateWarning);
                }
            }
            else if (_logicEditor.CompileState == CompileState.Valid && _logicEditor.CurrentProgram != null)
            {
                GateWarning = string.Empty;
                AddEvent($"Đã tải chương trình logic mới ({_logicEditor.CurrentProgram.RuleCount} rules).");
            }
        }
    }

    private void AddEvent(string msg)
    {
        EventStream.Insert(0, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}");
        if (EventStream.Count > 60)
            EventStream.RemoveAt(EventStream.Count - 1);
    }

    [RelayCommand]
    public void ToggleDI(TagModel diTag)
    {
        diTag.Value = diTag.Value == 0 ? 1 : 0;
        string stateStr = diTag.Value == 1 ? "BẬT (HIGH)" : "TẮT (LOW)";
        AddEvent($"Ngõ vào vật lý {diTag.Name} ({diTag.Alias}) chuyển sang {stateStr}");
        if (IsRunning)
        {
            RunScan();
        }
    }

    [RelayCommand]
    public void ToggleEngine()
    {
        if (!IsRunning)
        {
            if (_logicEditor != null && (_logicEditor.CompileState != CompileState.Valid || _logicEditor.CurrentProgram == null))
            {
                GateWarning = "Không thể chạy mô phỏng: Logic chưa biên dịch (Stale) hoặc có lỗi (Invalid).";
                AddEvent(GateWarning);
                return;
            }

            IsRunning = true;
            _scanTimer.Start();
            AddEvent("Tiếp tục chạy chu kỳ quét.");
        }
        else
        {
            IsRunning = false;
            _scanTimer.Stop();
            AddEvent("Tạm dừng mô phỏng.");
        }
    }

    [RelayCommand]
    public void StepScan()
    {
        if (IsRunning)
        {
            IsRunning = false;
            _scanTimer.Stop();
        }

        if (_logicEditor != null && (_logicEditor.CompileState != CompileState.Valid || _logicEditor.CurrentProgram == null))
        {
            GateWarning = "Không thể chạy mô phỏng: Logic chưa biên dịch (Stale) hoặc có lỗi (Invalid).";
            AddEvent(GateWarning);
            return;
        }

        _clock.Advance(100);
        RunScan();
        AddEvent($"[Scan #{ScanNumber}] Đã thực hiện bước quét đơn (Step Scan).");
    }

    private void OnScanCycle(object? sender, EventArgs e)
    {
        _clock.Advance(100);
        RunScan();
    }

    private void RunScan()
    {
        ScanTick = _clock.NowMs;
        RuntimeSnapshot snapshot;
        if (_logicEditor?.CurrentProgram != null)
        {
            snapshot = _engine.Scan(_logicEditor.CurrentProgram, _clock.NowMs);
        }
        else
        {
            snapshot = _engine.Scan(_ruleTable.Rules.ToList(), _clock.NowMs);
        }

        ScanNumber = snapshot.ScanNumber;
        LatestSnapshot = snapshot;

        // S1: Cập nhật đồng hồ mô phỏng sau mỗi scan
        var ts = TimeSpan.FromMilliseconds(_clock.NowMs);
        SimClock = $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";

        // Cập nhật bảng trạng thái từng Rule (in-place update để giảm thiểu UI churn)
        if (RuleStatusList.Count == snapshot.Evaluations.Count)
        {
            for (int i = 0; i < snapshot.Evaluations.Count; i++)
            {
                RuleStatusList[i] = snapshot.Evaluations[i];
            }
        }
        else
        {
            RuleStatusList.Clear();
            foreach (var eval in snapshot.Evaluations)
            {
                RuleStatusList.Add(eval);
            }
        }

        // Cập nhật Execution Trace Feed
        bool hasActiveRules = snapshot.Evaluations.Any(e => e.Status is RuleEvaluationStatus.Pass or RuleEvaluationStatus.WaitingDwell or RuleEvaluationStatus.BlockedByGuard);
        if (!FilterActiveOnly || hasActiveRules)
        {
            foreach (var line in snapshot.FormattedTrace)
            {
                ExecutionTraceFeed.Insert(0, $"[{DateTime.Now:HH:mm:ss.fff}] {line}");
            }
            while (ExecutionTraceFeed.Count > 150)
            {
                ExecutionTraceFeed.RemoveAt(ExecutionTraceFeed.Count - 1);
            }
        }

        foreach (var evt in snapshot.Events)
            AddEvent($"[R{evt.RuleIndex + 1}] {evt.Message}");
    }

    [RelayCommand]
    public void AdvanceClock()
    {
        _clock.Advance(1000);
        RunScan();
    }

    [RelayCommand]
    public void ResetRuntime()
    {
        _clock.Reset();
        _engine.Reset();
        ScanNumber = 0;
        ExecutionTraceFeed.Clear();
        RuleStatusList.Clear();
        foreach (var tag in _tagCatalog.AllTags.Where(t => t.Kind != TagKind.DiscreteInput))
            if (tag.Kind is TagKind.DiscreteOutput or TagKind.VirtualFlag 
                         or TagKind.VirtualRegister or TagKind.Counter)
                tag.Value = 0;
        // VirtualRegisterRetain giữ nguyên (by design — mô phỏng bộ nhớ non-volatile)
        RunScan();
        AddEvent("Đã reset runtime simulator.");
    }
}
