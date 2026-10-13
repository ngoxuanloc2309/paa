using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimplePLC.Application.Abstractions;
using SimplePLC.Protocol.Dto;
using SimplePLC.Studio.Services;

namespace SimplePLC.Studio.ViewModels;

public record TimezoneOption(string DisplayName, TimeZoneInfo TimeZoneInfo, bool IsLocalPc = false)
{
    public override string ToString() => DisplayName;
}

public partial class RtcSyncViewModel : ObservableObject
{
    private readonly IRtcClockClient _rtcClient;
    private readonly byte _slaveId;
    private readonly DispatcherTimer _previewTimer;

    public Action<bool>? RequestClose { get; set; }

    [ObservableProperty]
    private string _currentMcuTimeText = "--:--:--";

    [ObservableProperty]
    private bool _hasHardwareRtc;

    [ObservableProperty]
    private string _hwRtcText = "Đang kiểm tra...";

    [ObservableProperty]
    private string _hwRtcBadgeText = string.Empty;

    [ObservableProperty]
    private string _hwRtcDescriptionText = string.Empty;

    [ObservableProperty]
    private bool _isBatteryLow;

    [ObservableProperty]
    private string _batteryWarningText = string.Empty;

    [ObservableProperty]
    private bool _isMcuSynced;

    [ObservableProperty]
    private ObservableCollection<TimezoneOption> _availableTimezones = new();

    [ObservableProperty]
    private TimezoneOption? _selectedTimezone;

    [ObservableProperty]
    private string _targetPreviewTimeText = string.Empty;

    [ObservableProperty]
    private string _targetModbusSummaryText = string.Empty;

    [ObservableProperty]
    private bool _isManualTimeEnabled;

    [ObservableProperty]
    private DateTime _manualDate = DateTime.Today;

    [ObservableProperty]
    private int _manualHour = DateTime.Now.Hour;

    [ObservableProperty]
    private int _manualMinute = DateTime.Now.Minute;

    [ObservableProperty]
    private int _manualSecond = DateTime.Now.Second;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public RtcClockDto? SyncedResult { get; private set; }

    public RtcSyncViewModel(IRtcClockClient rtcClient, byte slaveId = 1)
    {
        _rtcClient = rtcClient;
        _slaveId = slaveId;

        PopulateTimezones();

        _previewTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _previewTimer.Tick += (s, e) => UpdatePreview();
        _previewTimer.Start();

        UpdatePreview();
    }

    public async Task InitializeAsync()
    {
        try
        {
            IsBusy = true;
            var current = await _rtcClient.ReadRtcClockAsync(_slaveId).ConfigureAwait(false);

            if (System.Windows.Application.Current?.Dispatcher is { } dispatcher)
            {
                dispatcher.Invoke(() =>
                {
                    ApplyCurrentMcuRtc(current);
                });
            }
            else
            {
                ApplyCurrentMcuRtc(current);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = (LocalizationService.Instance.IsVietnamese
                ? "Không thể đọc thời gian từ bộ điều khiển: "
                : "Failed to read clock from controller: ") + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyCurrentMcuRtc(RtcClockDto rtc)
    {
        IsMcuSynced = rtc.IsSynced;
        HasHardwareRtc = rtc.HasHardwareRtc;
        IsBatteryLow = rtc.IsBatteryLow;

        int tzHours = rtc.TimezoneOffsetMinutes / 60;
        string tzSign = rtc.TimezoneOffsetMinutes >= 0 ? "+" : "";
        CurrentMcuTimeText = $"{rtc.LocalDateTime:HH:mm:ss dd/MM/yyyy} (UTC{tzSign}{tzHours})";

        HwRtcBadgeText = rtc.HasHardwareRtc
            ? (LocalizationService.Instance.IsVietnamese ? "IC RTC PHẦN CỨNG ĐỘC LẬP" : "HARDWARE RTC IC")
            : (LocalizationService.Instance.IsVietnamese ? "ĐỒNG HỒ PHẦN MỀM (SYSTICK)" : "SOFTWARE CLOCK (SYSTICK)");

        HwRtcDescriptionText = rtc.HasHardwareRtc
            ? (LocalizationService.Instance.IsVietnamese ? "Có nguồn pin dự phòng, duy trì chính xác thời gian khi ngắt nguồn điện chính." : "Battery-backed real-time clock, retains accurate time across main power loss.")
            : (LocalizationService.Instance.IsVietnamese ? "Đếm nhịp vi điều khiển (SysTick), thời gian sẽ bị đặt lại khi ngắt nguồn điện." : "Internal system timer (SysTick), resets to initial state upon power loss.");

        HwRtcText = rtc.HasHardwareRtc
            ? (LocalizationService.Instance.IsVietnamese ? "Phần cứng RTC (Duy trì giờ khi tắt nguồn)" : "Hardware RTC (Retains time on power-off)")
            : (LocalizationService.Instance.IsVietnamese ? "Đồng hồ phần mềm (Giờ reset khi tắt nguồn)" : "Software clock (Resets time on power-off)");

        BatteryWarningText = rtc.IsBatteryLow
            ? (LocalizationService.Instance.IsVietnamese ? "⚠ Cảnh báo: Đồng hồ bộ điều khiển vừa bị gián đoạn nguồn nuôi (vừa thiết lập lại)" : "⚠ Warning: Controller clock was halted due to power interruption")
            : string.Empty;
    }

    private void PopulateTimezones()
    {
        AvailableTimezones.Clear();

        var localTz = TimeZoneInfo.Local;
        string localLabel = LocalizationService.Instance.IsVietnamese
            ? $"[Mặc định] Giờ máy tính hiện tại — {localTz.DisplayName}"
            : $"[Default] Current PC Time — {localTz.DisplayName}";

        var localOption = new TimezoneOption(localLabel, localTz, IsLocalPc: true);
        AvailableTimezones.Add(localOption);
        SelectedTimezone = localOption;

        // Bổ sung toàn bộ múi giờ quốc tế được sắp xếp chuẩn xác theo BaseUtcOffset (từ UTC-12:00 đến UTC+14:00)
        var allSystem = TimeZoneInfo.GetSystemTimeZones()
            .OrderBy(tz => tz.BaseUtcOffset)
            .ThenBy(tz => tz.DisplayName);

        foreach (var tz in allSystem)
        {
            AvailableTimezones.Add(new TimezoneOption(tz.DisplayName, tz));
        }
    }

    partial void OnSelectedTimezoneChanged(TimezoneOption? value)
    {
        UpdatePreview();
    }

    partial void OnIsManualTimeEnabledChanged(bool value)
    {
        UpdatePreview();
    }

    partial void OnManualDateChanged(DateTime value)
    {
        UpdatePreview();
    }

    partial void OnManualHourChanged(int value)
    {
        UpdatePreview();
    }

    partial void OnManualMinuteChanged(int value)
    {
        UpdatePreview();
    }

    partial void OnManualSecondChanged(int value)
    {
        UpdatePreview();
    }

    public void UpdatePreview()
    {
        var tz = SelectedTimezone?.TimeZoneInfo ?? TimeZoneInfo.Local;

        DateTimeOffset targetTime;
        if (IsManualTimeEnabled)
        {
            var customDt = new DateTime(
                ManualDate.Year, ManualDate.Month, ManualDate.Day,
                Math.Clamp(ManualHour, 0, 23),
                Math.Clamp(ManualMinute, 0, 59),
                Math.Clamp(ManualSecond, 0, 59));
            var offset = tz.GetUtcOffset(customDt);
            targetTime = new DateTimeOffset(customDt, offset);
        }
        else
        {
            var utcNow = DateTimeOffset.UtcNow;
            targetTime = TimeZoneInfo.ConvertTime(utcNow, tz);
        }

        string dayOfWeek = LocalizationService.Instance.IsVietnamese
            ? targetTime.ToString("dddd, dd/MM/yyyy", new System.Globalization.CultureInfo("vi-VN"))
            : targetTime.ToString("dddd, yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

        int tzHours = (int)targetTime.Offset.TotalMinutes / 60;
        string tzSign = targetTime.Offset.TotalMinutes >= 0 ? "+" : "";
        TargetPreviewTimeText = $"{targetTime:HH:mm:ss} — {dayOfWeek} (UTC{tzSign}{tzHours})";

        string unitMinute = LocalizationService.Instance.IsVietnamese ? "phút" : "min";
        TargetModbusSummaryText = $"Modbus 0x0810: Epoch UTC = {targetTime.ToUnixTimeSeconds()}, TzOffset = {(short)targetTime.Offset.TotalMinutes} {unitMinute}";
    }

    [RelayCommand]
    public async Task SyncAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;

            var tz = SelectedTimezone?.TimeZoneInfo ?? TimeZoneInfo.Local;
            DateTimeOffset? manualTime = null;

            if (IsManualTimeEnabled)
            {
                var customDt = new DateTime(
                    ManualDate.Year, ManualDate.Month, ManualDate.Day,
                    Math.Clamp(ManualHour, 0, 23),
                    Math.Clamp(ManualMinute, 0, 59),
                    Math.Clamp(ManualSecond, 0, 59));
                var offset = tz.GetUtcOffset(customDt);
                manualTime = new DateTimeOffset(customDt, offset);
            }

            SyncedResult = await _rtcClient.SyncToCustomTimezoneAsync(tz, manualTime, _slaveId).ConfigureAwait(false);

            if (System.Windows.Application.Current?.Dispatcher is { } dispatcher)
            {
                dispatcher.Invoke(() =>
                {
                    _previewTimer.Stop();
                    RequestClose?.Invoke(true);
                });
            }
            else
            {
                _previewTimer.Stop();
                RequestClose?.Invoke(true);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = (LocalizationService.Instance.IsVietnamese
                ? "Lỗi đồng bộ RTC: "
                : "Failed to sync RTC: ") + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public void Cancel()
    {
        _previewTimer.Stop();
        RequestClose?.Invoke(false);
    }
}
