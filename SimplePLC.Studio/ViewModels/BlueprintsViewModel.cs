using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows;
using SimplePLC.Domain.Enums;
using SimplePLC.Studio.Models;

namespace SimplePLC.Studio.ViewModels;

public partial class BlueprintsViewModel : ObservableObject
{
    private readonly LogicEditorViewModel _logicEditor;
    private readonly Action _navigateToEditor;

    public ObservableCollection<BlueprintModel> Blueprints { get; } = new();

    public BlueprintsViewModel(LogicEditorViewModel logicEditor, Action navigateToEditor)
    {
        _logicEditor = logicEditor;
        _navigateToEditor = navigateToEditor;

        // =========================================================================
        // NHÓM 1: ĐIỀU KHIỂN LOGIC SỐ & LIÊN KHÓA AN TOÀN (DISCRETE & SAFETY)
        // =========================================================================

        // 1. Andon Alert
        Blueprints.Add(new BlueprintModel
        {
            Id = "andon",
            TitleVi = "Hệ Thống Báo Động Andon",
            TitleEn = "Andon Alert System",
            SubtitleVi = "Máy dừng > 15 giây trong ca sản xuất",
            SubtitleEn = "Machine stopped > 15s during production shift",
            DescriptionVi = "Tự động kích hoạt đèn đỏ cảnh báo DO0 khi tín hiệu máy chạy DI0 mất sườn xuống quá 15,000ms trong khung giờ ca.",
            DescriptionEn = "Automatically triggers red warning light DO0 when machine run signal DI0 loses falling edge for over 15,000ms during active shift.",
            BadgeVi = "Andon / OEE",
            BadgeEn = "Andon / OEE",
            BadgeColor = "#B91C1C",
            Icon = "🚨",
            RuleCount = 1,
            TagsSummaryVi = "DI0 (Máy chạy), VFLAG0 (Trong ca), DO0 (Đèn đỏ)",
            TagsSummaryEn = "DI0 (Machine Run), VFLAG0 (In Shift), DO0 (Red Light)"
        });

        // 2. Production Counter (Retain)
        Blueprints.Add(new BlueprintModel
        {
            Id = "counter",
            TitleVi = "Đếm Sản Lượng Dây Chuyền (Retain)",
            TitleEn = "Production Line Counter (Retain)",
            SubtitleVi = "Cộng dồn 2 nguồn vào bộ đếm tổng",
            SubtitleEn = "Accumulate line count into total counter",
            DescriptionVi = "Mỗi xung sườn lên từ cảm biến DI2 (Line 1) sẽ cộng dồn vào bộ đếm riêng VREG_RETAIN0 và bộ đếm tổng VREG_RETAIN2. Lưu an toàn qua mất điện bằng Flash Retain.",
            DescriptionEn = "Each rising edge pulse from DI2 (Line 1) accumulates into local counter VREG_RETAIN0 and total counter VREG_RETAIN2. Preserved across power loss via Flash Retain.",
            BadgeVi = "Sản lượng",
            BadgeEn = "Production",
            BadgeColor = "#107C41",
            Icon = "📊",
            RuleCount = 2,
            TagsSummaryVi = "DI2, VREG_RETAIN0 (Chuyền 1), VREG_RETAIN2 (Tổng)",
            TagsSummaryEn = "DI2, VREG_RETAIN0 (Line 1), VREG_RETAIN2 (Total)"
        });

        // 3. Feed Calling & Toggle
        Blueprints.Add(new BlueprintModel
        {
            Id = "feed",
            TitleVi = "Gọi Cấp Liệu & Đảo Trạng Thái",
            TitleEn = "Material Call & State Toggle",
            SubtitleVi = "Bấm nút đảo trạng thái đèn/van cấp liệu",
            SubtitleEn = "Push button toggles call light / feed valve",
            DescriptionVi = "Công nhân bấm nút DI1 (sườn lên) để bật/tắt (Toggle) đèn báo gọi cấp liệu DO1 để nhân viên tiếp liệu trong kho phát hiện.",
            DescriptionEn = "Operator presses button DI1 (rising edge) to toggle call light DO1 so warehouse logistics staff can respond.",
            BadgeVi = "Logistics",
            BadgeEn = "Logistics",
            BadgeColor = "#C05621",
            Icon = "📦",
            RuleCount = 1,
            TagsSummaryVi = "DI1 (Nút gọi), DO1 (Đèn vàng báo cấp liệu)",
            TagsSummaryEn = "DI1 (Call Button), DO1 (Yellow Feed Light)"
        });

        // 4. Automatic Sump Pump
        Blueprints.Add(new BlueprintModel
        {
            Id = "sump_pump",
            TitleVi = "Tự Động Bơm Thoát Nước (Sump Pump)",
            TitleEn = "Automatic Sump Pump Control",
            SubtitleVi = "Phao mức cao kích hoạt bơm xả buồng máy",
            SubtitleEn = "High-level float switch triggers drainage pump",
            DescriptionVi = "Khi cảm biến phao mức cao DI5 đóng mạch (sườn lên), hệ thống lập tức kích hoạt bơm thoát nước DO4 để ngăn ngập buồng máy.",
            DescriptionEn = "When high float switch DI5 closes (rising edge), system immediately turns on drainage pump DO4 to prevent basement flooding.",
            BadgeVi = "Xử lý nước",
            BadgeEn = "Water Drainage",
            BadgeColor = "#006487",
            Icon = "💧",
            RuleCount = 1,
            TagsSummaryVi = "DI5 (Cảm biến an toàn/phao), DO4 (Bơm xả)",
            TagsSummaryEn = "DI5 (Float Switch), DO4 (Sump Pump)"
        });

        // 5. Safety Door Guard (Guard đảo - Negated)
        Blueprints.Add(new BlueprintModel
        {
            Id = "safety_door",
            TitleVi = "Liên Khóa Cửa An Toàn (Safety Door)",
            TitleEn = "Safety Door Interlock",
            SubtitleVi = "Chỉ cho phép chạy băng tải khi Cửa Đang ĐÓNG",
            SubtitleEn = "Conveyor can only start when safety door is CLOSED",
            DescriptionVi = "Khi công nhân bấm nút khởi động DI7, hệ thống kiểm tra cảm biến an toàn DI6. Nhờ Guard Đảo (Pass khi DI6 = 0, Cửa Đóng), relay băng tải DO5 mới được kích hoạt.",
            DescriptionEn = "When operator presses start button DI7, system verifies safety sensor DI6. With Negated Guard (Pass when DI6 = 0, Door Closed), conveyor relay DO5 is allowed to start.",
            BadgeVi = "An toàn / Guard Đảo",
            BadgeEn = "Safety / Inverted",
            BadgeColor = "#B91C1C",
            Icon = "🚪",
            RuleCount = 1,
            TagsSummaryVi = "DI7 (Nút chạy), DI6 (Cửa an toàn, Guard đảo), DO5 (Băng tải)",
            TagsSummaryEn = "DI7 (Start Button), DI6 (Door Sensor, Inverted), DO5 (Conveyor)"
        });

        // 6. Safety Interlock
        Blueprints.Add(new BlueprintModel
        {
            Id = "interlock",
            TitleVi = "Liên Khóa Cắt Nguồn Cửa (Safety Interlock)",
            TitleEn = "Power Cutoff Interlock",
            SubtitleVi = "Mở cửa bảo vệ ngắt ngay lập tức relay nguồn",
            SubtitleEn = "Opening guard door immediately drops power relay",
            DescriptionVi = "Khi cảm biến cửa an toàn DI6 bị hở (sườn xuống 1→0), lập tức ngắt relay cấp nguồn DO7 để dừng toàn bộ cơ cấu chấp hành nguy hiểm.",
            DescriptionEn = "When safety door sensor DI6 opens (falling edge 1→0), system instantly cuts main power relay DO7 to halt all hazardous actuators.",
            BadgeVi = "Interlock",
            BadgeEn = "Interlock",
            BadgeColor = "#B91C1C",
            Icon = "🛑",
            RuleCount = 1,
            TagsSummaryVi = "DI6 (Cửa bảo vệ), DO7 (Relay nguồn)",
            TagsSummaryEn = "DI6 (Guard Door), DO7 (Main Power Relay)"
        });

        // 7. Status Tower Light
        Blueprints.Add(new BlueprintModel
        {
            Id = "tower_light",
            TitleVi = "Đèn Tháp Trạng Thái Máy (Tower Light)",
            TitleEn = "Machine Status Tower Light",
            SubtitleVi = "Bật đèn xanh khi máy chạy, tắt đèn đỏ",
            SubtitleEn = "Turn on green light when running, turn off red",
            DescriptionVi = "Khi máy chuyển sang trạng thái chạy (DI0 chuyển 0→1), tự động kích hoạt đèn xanh DO2 và tắt đèn đỏ cảnh báo DO0.",
            DescriptionEn = "When machine enters running state (DI0 rises 0→1), automatically enables green light DO2 and clears red alert light DO0.",
            BadgeVi = "Hiển thị",
            BadgeEn = "Indication",
            BadgeColor = "#107C41",
            Icon = "🚥",
            RuleCount = 2,
            TagsSummaryVi = "DI0 (Máy chạy), DO2 (Đèn xanh), DO0 (Đèn đỏ)",
            TagsSummaryEn = "DI0 (Running), DO2 (Green Light), DO0 (Red Light)"
        });

        // 8. Emergency Stop (E-Stop)
        Blueprints.Add(new BlueprintModel
        {
            Id = "e_stop",
            TitleVi = "Mạch Cắt Khẩn Cấp E-Stop (Emergency Stop)",
            TitleEn = "Emergency Stop Circuit (E-Stop)",
            SubtitleVi = "Nhấn nút E-Stop dừng băng tải và bật còi",
            SubtitleEn = "Pressing E-Stop stops conveyor and sounds horn",
            DescriptionVi = "Khi công nhân nhấn nút E-Stop DI4 (sườn lên), lập tức cắt lệnh chạy băng tải DO4 và đồng thời phát còi cảnh báo DO3.",
            DescriptionEn = "When worker presses E-Stop button DI4 (rising edge), immediately disengages conveyor DO4 and triggers alarm siren DO3.",
            BadgeVi = "E-Stop Khẩn",
            BadgeEn = "Emergency E-Stop",
            BadgeColor = "#B91C1C",
            Icon = "⚠️",
            RuleCount = 2,
            TagsSummaryVi = "DI4 (Nút dừng khẩn), DO4 (Băng tải 1), DO3 (Còi báo động)",
            TagsSummaryEn = "DI4 (E-Stop Button), DO4 (Conveyor 1), DO3 (Alarm Siren)"
        });

        // =========================================================================
        // NHÓM 2: ĐO LƯỜNG & XỬ LÝ TƯƠNG TỰ (ANALOG & INSTRUMENTATION)
        // =========================================================================

        // 9. Thermal Overheat Protection
        Blueprints.Add(new BlueprintModel
        {
            Id = "overheat",
            TitleVi = "Bảo Vệ Quá Nhiệt Động Cơ (Thermal Guard)",
            TitleEn = "Thermal Overheat Protection",
            SubtitleVi = "Nhiệt độ bể > 85°C kích hoạt quạt làm mát",
            SubtitleEn = "Tank temperature > 85°C engages cooling fan",
            DescriptionVi = "Giám sát kênh analog AI1 (Nhiệt độ bể). Khi nhiệt độ vượt ngưỡng an toàn (> 85°C), tự động đóng relay bật quạt tản nhiệt DO2.",
            DescriptionEn = "Monitors analog channel AI1 (Tank temp). When temperature exceeds threshold (> 85°C), automatically energizes cooling fan DO2.",
            BadgeVi = "An toàn nhiệt",
            BadgeEn = "Thermal Safety",
            BadgeColor = "#B91C1C",
            Icon = "🔥",
            RuleCount = 1,
            TagsSummaryVi = "AI1 (Nhiệt độ bể), DO2 (Quạt làm mát)",
            TagsSummaryEn = "AI1 (Tank Temp), DO2 (Cooling Fan)"
        });

        // 10. Compressed Air Pressure Monitoring
        Blueprints.Add(new BlueprintModel
        {
            Id = "air_press",
            TitleVi = "Giám Sát Áp Suất Khí Nén & Thủy Lực",
            TitleEn = "Pneumatic & Hydraulic Pressure Monitor",
            SubtitleVi = "Áp suất dầu/khí dưới 40 bar cảnh báo còi",
            SubtitleEn = "Pressure below 40 bar sounds alarm horn",
            DescriptionVi = "Giám sát áp suất từ cảm biến AI0. Nếu áp suất tụt xuống dưới ngưỡng 40 bar, kích hoạt còi báo động DO3 để kỹ thuật viên can thiệp kịp thời.",
            DescriptionEn = "Monitors line pressure from sensor AI0. If pressure drops below 40 bar, triggers alarm horn DO3 for timely technician intervention.",
            BadgeVi = "Khí nén / Dầu",
            BadgeEn = "Pneumatics / Oil",
            BadgeColor = "#475569",
            Icon = "⏱️",
            RuleCount = 1,
            TagsSummaryVi = "AI0 (Áp suất dầu), DO3 (Còi báo động)",
            TagsSummaryEn = "AI0 (Line Pressure), DO3 (Alarm Horn)"
        });

        // 11. Pressure Working Window (ON_CHANGE BETWEEN)
        Blueprints.Add(new BlueprintModel
        {
            Id = "press_window",
            TitleVi = "Giám Sát Dải Áp Suất Tiêu Chuẩn (Pressure Window)",
            TitleEn = "Standard Pressure Range Window",
            SubtitleVi = "Duy trì van cấp DO6 khi áp suất trong dải 40 - 80 bar",
            SubtitleEn = "Maintain feed valve DO6 while pressure is within 40 - 80 bar",
            DescriptionVi = "Sử dụng phép so sánh khoảng BETWEEN [40..80] bar trên kênh tương tự AI0. Khi áp suất đạt chuẩn, van nạp DO6 được đóng mạch duy trì.",
            DescriptionEn = "Uses BETWEEN comparison window [40..80] bar on analog input AI0. When pressure is within nominal range, maintains supply valve DO6.",
            BadgeVi = "Analog Window",
            BadgeEn = "Analog Window",
            BadgeColor = "#0284C7",
            Icon = "🎛️",
            RuleCount = 1,
            TagsSummaryVi = "AI0 (Áp suất [40..80]), DO6 (Van cấp dầu)",
            TagsSummaryEn = "AI0 (Pressure [40..80]), DO6 (Feed Valve)"
        });

        // 12. Linear Scaling Pressure 0-10V to Bar (SCALE Block)
        Blueprints.Add(new BlueprintModel
        {
            Id = "scale_pressure",
            TitleVi = "Quy Đổi Áp Suất Khí Nén 0-10V Sang Bar (Khối SCALE)",
            TitleEn = "Pneumatic Pressure Scaler 0-10V to Bar (SCALE)",
            SubtitleVi = "Quy đổi điện áp AI0 0..10,000 mV sang 0..16.0 bar",
            SubtitleEn = "Scale AI0 voltage 0..10,000 mV to 0..16.0 bar",
            DescriptionVi = "Sử dụng khối Function Block SCALE công nghiệp. Tín hiệu điện áp AI0 (0..10V) được nhân với Gain k = 0.0016, chặn an toàn (Clamping) 0..16 bar và xuất qua cổng OUT ghi vào thanh ghi VREG0.",
            DescriptionEn = "Uses industrial Function Block SCALE. AI0 voltage (0..10V) is scaled with Gain k = 0.0016, safety clamped to 0..16 bar, and routed from OUT port into register VREG0.",
            BadgeVi = "SCALE / Áp suất",
            BadgeEn = "SCALE / Pressure",
            BadgeColor = "#0284C7",
            Icon = "📐",
            RuleCount = 1,
            TagsSummaryVi = "AI0 (0..10V), Khối SCALE (0..16 bar), VREG0",
            TagsSummaryEn = "AI0 (0..10V), SCALE Block (0..16 bar), VREG0"
        });

        // 13. Linear Scaling Temperature 4-20mA to °C (SCALE Block)
        Blueprints.Add(new BlueprintModel
        {
            Id = "scale_temp",
            TitleVi = "Hiệu Chuẩn Cảm Biến Nhiệt Độ 4-20mA Sang °C (Khối SCALE)",
            TitleEn = "Temperature Transmitter Scaler 4-20mA to °C (SCALE)",
            SubtitleVi = "Calib 2-điểm: 4mA (-20°C) đến 20mA (100°C) đưa vào VREG1",
            SubtitleEn = "2-point calib: 4mA (-20°C) to 20mA (100°C) into VREG1",
            DescriptionVi = "Quy đổi tuyến tính cảm biến nhiệt độ AI1 (4..20mA qua trở 500Ω = 2000..10000 mV) với hệ số Gain k = 0.015, Offset b = -50.0, chặn an toàn -20..100°C đưa ra thanh ghi VREG1.",
            DescriptionEn = "Linearly scales temperature sensor AI1 (4..20mA over 500Ω = 2000..10000 mV) with Gain k = 0.015, Offset b = -50.0, clamped to -20..100°C into VREG1.",
            BadgeVi = "SCALE / Nhiệt độ",
            BadgeEn = "SCALE / Temp",
            BadgeColor = "#D97706",
            Icon = "🌡️",
            RuleCount = 1,
            TagsSummaryVi = "AI1 (4..20mA), Khối SCALE (-20..100°C), VREG1",
            TagsSummaryEn = "AI1 (4..20mA), SCALE Block (-20..100°C), VREG1"
        });

        // 14. Tank Liquid Level Scaler 0-10V to Meters (SCALE Block)
        Blueprints.Add(new BlueprintModel
        {
            Id = "scale_tank_level",
            TitleVi = "Đo Mức Nước Bồn Chứa 0-10V Sang Mét (Khối SCALE)",
            TitleEn = "Tank Liquid Level Scaler 0-10V to Meters (SCALE)",
            SubtitleVi = "Quy đổi cảm biến mức siêu âm AI0 sang 0..5.00 mét",
            SubtitleEn = "Scale ultrasonic level sensor AI0 to 0..5.00 meters",
            DescriptionVi = "Cảm biến mức nước AI0 (0..10,000 mV) đi qua khối SCALE với Gain k = 0.0005, hiển thị 2 chữ số thập phân, chặn ngưỡng an toàn 0..5.0m và ghi kết quả vào VREG0.",
            DescriptionEn = "Level sensor AI0 (0..10,000 mV) passes through SCALE block with Gain k = 0.0005, 2 decimal places, clamped 0..5.0m, outputting to VREG0.",
            BadgeVi = "SCALE / Mức nước",
            BadgeEn = "SCALE / Level",
            BadgeColor = "#059669",
            Icon = "🌊",
            RuleCount = 1,
            TagsSummaryVi = "AI0 (0..10V), Khối SCALE (0..5m), VREG0",
            TagsSummaryEn = "AI0 (0..10V), SCALE Block (0..5m), VREG0"
        });

        // 13. Critical Pressure Alarm (SEND_ALARM)
        Blueprints.Add(new BlueprintModel
        {
            Id = "alarm_press",
            TitleVi = "Cảnh Báo Sự Cố Áp Suất Khẩn (Send Alarm)",
            TitleEn = "Emergency Pressure Failure Alarm",
            SubtitleVi = "Phát mã cảnh báo #101 khi áp suất sụt dưới 20 bar",
            SubtitleEn = "Broadcast Alarm Code #101 when pressure drops < 20 bar",
            DescriptionVi = "Khi áp suất buồng khí AI0 tụt xuống mức cực kỳ nguy hiểm (< 20 bar), kích hoạt còi cảnh báo DO3 với mã lỗi khẩn cấp Alarm #101 lên mạng giám sát.",
            DescriptionEn = "When chamber pressure AI0 drops to critical level (< 20 bar), activates buzzer DO3 and broadcasts emergency Alarm #101 over supervisory network.",
            BadgeVi = "Cảnh báo / Alarm",
            BadgeEn = "Alarm / Alert",
            BadgeColor = "#DC2626",
            Icon = "🚨",
            RuleCount = 1,
            TagsSummaryVi = "AI0 (Áp suất < 20 bar), DO3 (Còi báo động, Mã 101)",
            TagsSummaryEn = "AI0 (Pressure < 20 bar), DO3 (Alarm Horn, Code 101)"
        });

        // =========================================================================
        // NHÓM 3: ĐỊNH THỜI TUẦN HOÀN & MACRO TIMERS (IEC 61131-3)
        // =========================================================================

        // 14. Periodic Lubrication Cycle
        Blueprints.Add(new BlueprintModel
        {
            Id = "lube_cycle",
            TitleVi = "Bơm Tra Dầu Bôi Trơn Định Kỳ (Lube Cycle)",
            TitleEn = "Periodic Lubrication Cycle",
            SubtitleVi = "Kích hoạt chu kỳ bơm dầu ray trượt mỗi 60s",
            SubtitleEn = "Trigger guideway lubrication pump every 60s",
            DescriptionVi = "Sử dụng Trigger định thời INTERVAL 60,000ms để kích hoạt van xả dầu bôi trơn DO6, giúp các ổ bi và ray trượt hoạt động trơn tru.",
            DescriptionEn = "Uses INTERVAL 60,000ms periodic timer to pulse lubrication valve DO6, maintaining smooth motion for bearings and linear guides.",
            BadgeVi = "Bảo dưỡng",
            BadgeEn = "Maintenance",
            BadgeColor = "#C05621",
            Icon = "⚙️",
            RuleCount = 1,
            TagsSummaryVi = "VREG0 (Timer), DO6 (Van cấp dầu)",
            TagsSummaryEn = "VREG0 (Timer), DO6 (Lube Valve)"
        });

        // 15. TON On-Delay
        Blueprints.Add(new BlueprintModel
        {
            Id = "ton_delay",
            TitleVi = "Trễ Bật Bơm Bôi Trơn (TON On-Delay)",
            TitleEn = "Lube Pump On-Delay Timer (TON)",
            SubtitleVi = "Nhấn giữ nút chạy 5s mới kích hoạt bơm",
            SubtitleEn = "Hold run button for 5s continuously to start pump",
            DescriptionVi = "Khối TON định thời trễ bật: Cần tín hiệu chạy DI0 duy trì liên tục trong 5,000ms thì bơm DO0 mới bật. Nếu nhả trước 5s thì timer tự reset.",
            DescriptionEn = "IEC 61131-3 TON On-Delay: Requires DI0 run command held high for 5,000ms before pump DO0 starts. Releasing early resets the timer.",
            BadgeVi = "Timer / TON",
            BadgeEn = "Timer / TON",
            BadgeColor = "#4C1D95",
            Icon = "⏱️",
            RuleCount = 2,
            TagsSummaryVi = "DI0 (Tín hiệu chạy) ➔ TON (PT=5000ms, Q=DO0)",
            TagsSummaryEn = "DI0 (Run Signal) ➔ TON (PT=5000ms, Q=DO0)"
        });

        // 16. TOF Off-Delay
        Blueprints.Add(new BlueprintModel
        {
            Id = "tof_cooling",
            TitleVi = "Trễ Ngắt Quạt Tản Nhiệt (TOF Off-Delay)",
            TitleEn = "Cooling Fan Off-Delay Timer (TOF)",
            SubtitleVi = "Máy tắt, quạt duy trì làm mát thêm 10s",
            SubtitleEn = "When machine stops, fan continues cooling for 10s",
            DescriptionVi = "Khối TOF định thời trễ ngắt: Khi máy DI1 chạy thì quạt DO1 bật ngay. Khi máy tắt (DI1 ngắt), quạt tiếp tục duy trì làm mát thêm 10,000ms rồi mới tự động tắt.",
            DescriptionEn = "IEC 61131-3 TOF Off-Delay: Fan DO1 turns on instantly when DI1 starts. When DI1 stops, fan continues running for 10,000ms before shutting off.",
            BadgeVi = "Timer / TOF",
            BadgeEn = "Timer / TOF",
            BadgeColor = "#4C1D95",
            Icon = "⏳",
            RuleCount = 2,
            TagsSummaryVi = "DI1 (Máy chạy) ➔ TOF (PT=10000ms, Q=DO1)",
            TagsSummaryEn = "DI1 (Machine Run) ➔ TOF (PT=10000ms, Q=DO1)"
        });

        // 17. TP Pulse Timer
        Blueprints.Add(new BlueprintModel
        {
            Id = "tp_air_blow",
            TitleVi = "Xung Khí Nén Thổi Phôi (TP Pulse)",
            TitleEn = "Part Ejection Air Pulse Timer (TP)",
            SubtitleVi = "Phát xung van khí đúng 1.5s khi phát hiện phôi",
            SubtitleEn = "Fires air solenoid for exactly 1.5s upon part detection",
            DescriptionVi = "Khối TP phát xung độ rộng chuẩn: Cảm biến quang DI3 phát hiện phôi (sườn lên) sẽ mở van khí nén DO3 trong đúng 1,500ms. Miễn nhiễm dội hoặc phôi dừng chắn cảm biến.",
            DescriptionEn = "IEC 61131-3 TP Pulse Timer: Photoelectric sensor DI3 detects part (rising edge) and pulses air valve DO3 for exactly 1,500ms regardless of sensor blockage.",
            BadgeVi = "Timer / TP",
            BadgeEn = "Timer / TP",
            BadgeColor = "#4C1D95",
            Icon = "⚡",
            RuleCount = 2,
            TagsSummaryVi = "DI3 (Cảm biến phôi) ➔ TP (PT=1500ms, Q=DO3)",
            TagsSummaryEn = "DI3 (Part Sensor) ➔ TP (PT=1500ms, Q=DO3)"
        });

        // =========================================================================
        // NHÓM 4: BỘ ĐẾM SẢN LƯỢNG & MACRO COUNTERS (IEC 61131-3)
        // =========================================================================

        // 18. CTU Batch Counter
        Blueprints.Add(new BlueprintModel
        {
            Id = "ctu_batch",
            TitleVi = "Đóng Thùng Tự Động (CTU Batch Counter)",
            TitleEn = "Automatic Boxing Batch Counter (CTU)",
            SubtitleVi = "Đếm đủ 10 sản phẩm bật đèn báo thùng đầy",
            SubtitleEn = "Counts 10 units then illuminates box-full beacon",
            DescriptionVi = "Khối CTU đếm tiến: Mỗi xung cảm biến DI2 tăng 1 vào VREG_RETAIN0. Khi CV >= 10, ngõ ra DO2 bật báo thùng đầy. Nhấn nút DI3 để reset về 0 đóng thùng mới.",
            DescriptionEn = "IEC 61131-3 CTU Count-Up: Each DI2 pulse increments retentive register VREG_RETAIN0. When CV >= 10, DO2 turns on. Push button DI3 resets count to 0.",
            BadgeVi = "Counter / CTU",
            BadgeEn = "Counter / CTU",
            BadgeColor = "#059669",
            Icon = "🔢",
            RuleCount = 4,
            TagsSummaryVi = "DI2 (Cảm biến) ➔ CTU (PV=10, CV=VREG_RETAIN0, Q=DO2, Reset=DI3)",
            TagsSummaryEn = "DI2 (Sensor) ➔ CTU (PV=10, CV=VREG_RETAIN0, Q=DO2, Reset=DI3)"
        });

        // 19. CTD Countdown
        Blueprints.Add(new BlueprintModel
        {
            Id = "ctd_batch",
            TitleVi = "Đếm Lùi Cấp Phôi (CTD Countdown)",
            TitleEn = "Feeder Blank Countdown Counter (CTD)",
            SubtitleVi = "Cảnh báo còi khi khay nạp còn 0 phôi",
            SubtitleEn = "Sounds replenishment horn when tray reaches 0 blanks",
            DescriptionVi = "Khối CTD đếm lùi: Mỗi chu kỳ dập DI1 làm giảm 1 phôi trong VREG_RETAIN1 (từ 5 về 0). Khi hết phôi (CV <= 0), còi DO5 kêu báo tiếp liệu. Bấm nút DI0 để nạp lại 5 phôi.",
            DescriptionEn = "IEC 61131-3 CTD Count-Down: Each stamping cycle DI1 decrements blank count in VREG_RETAIN1 (from 5 to 0). When empty (CV <= 0), siren DO5 sounds. DI0 reloads 5.",
            BadgeVi = "Counter / CTD",
            BadgeEn = "Counter / CTD",
            BadgeColor = "#059669",
            Icon = "📉",
            RuleCount = 4,
            TagsSummaryVi = "DI1 (Chu kỳ) ➔ CTD (PV=5, CV=VREG_RETAIN1, Q=DO5, Reset=DI0)",
            TagsSummaryEn = "DI1 (Cycle) ➔ CTD (PV=5, CV=VREG_RETAIN1, Q=DO5, Reset=DI0)"
        });

        // =========================================================================
        // NHÓM 5: THỜI GIAN THỰC & LỊCH TRÌNH CA LÀM VIỆC (REAL-TIME RTC)
        // =========================================================================

        // 20. RTC Morning Shift
        Blueprints.Add(new BlueprintModel
        {
            Id = "rtc_morning_shift",
            TitleVi = "Chiếu Sáng Vào Ca Sáng (RTC Morning Shift)",
            TitleEn = "Morning Shift Illumination (RTC)",
            SubtitleVi = "Bật đèn xưởng DO1 đúng 07:00 sáng mỗi ngày",
            SubtitleEn = "Turn on shop floor lighting DO1 promptly at 07:00 daily",
            DescriptionVi = "Sử dụng đồng hồ thời gian thực RTC trên MCU. Đúng 07:00 (EQ 700), hệ thống tự động bật đèn chiếu sáng xưởng DO1 chuẩn bị cho ca làm việc.",
            DescriptionEn = "Uses real-time clock RTC on MCU. Exactly at 07:00 (EQ 700), automatically turns on factory lighting DO1 for the morning shift.",
            BadgeVi = "RTC / Lịch Trình",
            BadgeEn = "RTC / Schedule",
            BadgeColor = "#0D9488",
            Icon = "⏰",
            RuleCount = 1,
            TagsSummaryVi = "DI0, RTC Clock (07:00), DO1 (Đèn chiếu sáng xưởng)",
            TagsSummaryEn = "DI0, RTC Clock (07:00), DO1 (Shop Floor Lighting)"
        });

        // 21. RTC Night Security
        Blueprints.Add(new BlueprintModel
        {
            Id = "rtc_night_security",
            TitleVi = "Chiếu Sáng An Ninh Ca Đêm (RTC Night Security)",
            TitleEn = "Night Security Illumination (RTC)",
            SubtitleVi = "Bật đèn an ninh DO7 trong khung giờ 18:00 - 06:00 khi có người",
            SubtitleEn = "Turn on security floodlight DO7 from 18:00 to 06:00 upon motion",
            DescriptionVi = "Sử dụng khung giờ thực xuyên đêm [18:00..06:00] (BETWEEN 1800..600). Khi có tín hiệu phát hiện chuyển động DI4 kết hợp Guard kiểm tra chế độ bảo vệ VFLAG0, đèn pha an ninh DO7 lập tức bật sáng.",
            DescriptionEn = "Uses cross-midnight time window [18:00..06:00] (BETWEEN 1800..600). When motion detector DI4 triggers while security guard VFLAG0 is active, floodlight DO7 illuminates.",
            BadgeVi = "RTC / Ca Đêm",
            BadgeEn = "RTC / Night Shift",
            BadgeColor = "#4F46E5",
            Icon = "🌙",
            RuleCount = 1,
            TagsSummaryVi = "DI4 (Cảm biến), RTC (18:00-06:00), VFLAG0 (Chế độ), DO7 (Đèn)",
            TagsSummaryEn = "DI4 (Motion), RTC (18:00-06:00), VFLAG0 (Arm Mode), DO7 (Floodlight)"
        });
    }

    [RelayCommand]
    public void ApplyBlueprint(BlueprintModel bp)
    {
        _logicEditor.NewRuleCanvas();
        var tags = _logicEditor.TagCatalog.AllTags;

        // Chuẩn hóa tọa độ lưới trực giao:
        // Col 0 = 80, Col 1 = 340, Col 2 = 600, Col 3 = 860
        // Trục tâm ngang thẳng tắp: Y = 120
        // Rẽ nhánh đối xứng: Y = 50 (-70px) và Y = 190 (+70px)

        if (bp.Id == "andon")
        {
            var di0 = tags.FirstOrDefault(t => t.Name == "DI0") ?? tags[1];
            var do0 = tags.FirstOrDefault(t => t.Name == "DO0") ?? tags[9];
            var vflag0 = tags.FirstOrDefault(t => t.Name == "VFLAG0") ?? tags[33];

            var inp = new InputNodeViewModel(di0) { Location = new Point(80, 120) };
            var trig = new TriggerNodeViewModel { TriggerType = TriggerType.ON_FALL, ForMs = 15000, Location = new Point(340, 120) };
            var grd = new GuardNodeViewModel(vflag0) { Location = new Point(600, 120) };
            var act = new ActionNodeViewModel(do0) { ActionType = ActionType.SET_TAG, ActionParam = 1, Location = new Point(860, 120) };

            _logicEditor.Nodes.Add(inp);
            _logicEditor.Nodes.Add(trig);
            _logicEditor.Nodes.Add(grd);
            _logicEditor.Nodes.Add(act);

            _logicEditor.Connect(inp.OutputConnectors[0], trig.InputConnectors[0]);
            _logicEditor.Connect(trig.OutputConnectors[0], grd.InputConnectors[0]);
            _logicEditor.Connect(grd.OutputConnectors[0], act.InputConnectors[0]);
        }
        else if (bp.Id == "counter")
        {
            var di2 = tags.FirstOrDefault(t => t.Name == "DI2") ?? tags[3];
            var r0 = tags.FirstOrDefault(t => t.Name == "VREG_RETAIN0")
                     ?? tags.FirstOrDefault(t => t.Kind == TagKind.VirtualRegisterRetain && t.Index == 84)
                     ?? tags.FirstOrDefault(t => t.Kind == TagKind.VirtualRegisterRetain)
                     ?? tags[53];
            var r2 = tags.FirstOrDefault(t => t.Name == "VREG_RETAIN2")
                     ?? tags.FirstOrDefault(t => t.Kind == TagKind.VirtualRegisterRetain && t.Index == 86)
                     ?? tags[55];

            var inp1 = new InputNodeViewModel(di2) { Location = new Point(80, 120) };
            var trig1 = new TriggerNodeViewModel { TriggerType = TriggerType.ON_RISE, Location = new Point(340, 120) };
            var act1 = new ActionNodeViewModel(r0) { ActionType = ActionType.INC_COUNTER, ActionParam = 1, Location = new Point(600, 50) };
            var act2 = new ActionNodeViewModel(r2) { ActionType = ActionType.INC_COUNTER, ActionParam = 1, Location = new Point(600, 190) };

            _logicEditor.Nodes.Add(inp1);
            _logicEditor.Nodes.Add(trig1);
            _logicEditor.Nodes.Add(act1);
            _logicEditor.Nodes.Add(act2);

            _logicEditor.Connect(inp1.OutputConnectors[0], trig1.InputConnectors[0]);
            _logicEditor.Connect(trig1.OutputConnectors[0], act1.InputConnectors[0]);
            _logicEditor.Connect(trig1.OutputConnectors[0], act2.InputConnectors[0]);
        }
        else if (bp.Id == "feed")
        {
            var di1 = tags.FirstOrDefault(t => t.Name == "DI1") ?? tags[2];
            var do1 = tags.FirstOrDefault(t => t.Name == "DO1") ?? tags[10];

            var inp = new InputNodeViewModel(di1) { Location = new Point(80, 120) };
            var trig = new TriggerNodeViewModel { TriggerType = TriggerType.ON_RISE, Location = new Point(340, 120) };
            var act = new ActionNodeViewModel(do1) { ActionType = ActionType.TOGGLE_TAG, ActionParam = 0, Location = new Point(600, 120) };

            _logicEditor.Nodes.Add(inp);
            _logicEditor.Nodes.Add(trig);
            _logicEditor.Nodes.Add(act);

            _logicEditor.Connect(inp.OutputConnectors[0], trig.InputConnectors[0]);
            _logicEditor.Connect(trig.OutputConnectors[0], act.InputConnectors[0]);
        }
        else if (bp.Id == "sump_pump")
        {
            var di5 = tags.FirstOrDefault(t => t.Name == "DI5") ?? tags[6];
            var do4 = tags.FirstOrDefault(t => t.Name == "DO4") ?? tags[13];

            var inp = new InputNodeViewModel(di5) { Location = new Point(80, 120) };
            var trig = new TriggerNodeViewModel { TriggerType = TriggerType.ON_RISE, Location = new Point(340, 120) };
            var act = new ActionNodeViewModel(do4) { ActionType = ActionType.SET_TAG, ActionParam = 1, Location = new Point(600, 120) };

            _logicEditor.Nodes.Add(inp);
            _logicEditor.Nodes.Add(trig);
            _logicEditor.Nodes.Add(act);

            _logicEditor.Connect(inp.OutputConnectors[0], trig.InputConnectors[0]);
            _logicEditor.Connect(trig.OutputConnectors[0], act.InputConnectors[0]);
        }
        else if (bp.Id == "safety_door")
        {
            var di7 = tags.FirstOrDefault(t => t.Name == "DI7") ?? tags[8];
            var di6 = tags.FirstOrDefault(t => t.Name == "DI6") ?? tags[7];
            var do5 = tags.FirstOrDefault(t => t.Name == "DO5") ?? tags[14];

            var inp = new InputNodeViewModel(di7) { Location = new Point(80, 120) };
            var trig = new TriggerNodeViewModel { TriggerType = TriggerType.ON_RISE, Location = new Point(340, 120) };
            var grd = new GuardNodeViewModel(di6) { Negate = true, Location = new Point(600, 120) };
            var act = new ActionNodeViewModel(do5) { ActionType = ActionType.SET_TAG, ActionParam = 1, Location = new Point(860, 120) };

            _logicEditor.Nodes.Add(inp);
            _logicEditor.Nodes.Add(trig);
            _logicEditor.Nodes.Add(grd);
            _logicEditor.Nodes.Add(act);

            _logicEditor.Connect(inp.OutputConnectors[0], trig.InputConnectors[0]);
            _logicEditor.Connect(trig.OutputConnectors[0], grd.InputConnectors[0]);
            _logicEditor.Connect(grd.OutputConnectors[0], act.InputConnectors[0]);
        }
        else if (bp.Id == "interlock")
        {
            var di6 = tags.FirstOrDefault(t => t.Name == "DI6") ?? tags[7];
            var do7 = tags.FirstOrDefault(t => t.Name == "DO7") ?? tags[16];

            var inp = new InputNodeViewModel(di6) { Location = new Point(80, 120) };
            var trig = new TriggerNodeViewModel { TriggerType = TriggerType.ON_FALL, Location = new Point(340, 120) };
            var act = new ActionNodeViewModel(do7) { ActionType = ActionType.SET_TAG, ActionParam = 0, Location = new Point(600, 120) };

            _logicEditor.Nodes.Add(inp);
            _logicEditor.Nodes.Add(trig);
            _logicEditor.Nodes.Add(act);

            _logicEditor.Connect(inp.OutputConnectors[0], trig.InputConnectors[0]);
            _logicEditor.Connect(trig.OutputConnectors[0], act.InputConnectors[0]);
        }
        else if (bp.Id == "tower_light")
        {
            var di0 = tags.FirstOrDefault(t => t.Name == "DI0") ?? tags[1];
            var do0 = tags.FirstOrDefault(t => t.Name == "DO0") ?? tags[9];
            var do2 = tags.FirstOrDefault(t => t.Name == "DO2") ?? tags[11];

            var inp = new InputNodeViewModel(di0) { Location = new Point(80, 120) };
            var trig = new TriggerNodeViewModel { TriggerType = TriggerType.ON_RISE, Location = new Point(340, 120) };
            var act1 = new ActionNodeViewModel(do2) { ActionType = ActionType.SET_TAG, ActionParam = 1, Location = new Point(600, 50) };
            var act2 = new ActionNodeViewModel(do0) { ActionType = ActionType.SET_TAG, ActionParam = 0, Location = new Point(600, 190) };

            _logicEditor.Nodes.Add(inp);
            _logicEditor.Nodes.Add(trig);
            _logicEditor.Nodes.Add(act1);
            _logicEditor.Nodes.Add(act2);

            _logicEditor.Connect(inp.OutputConnectors[0], trig.InputConnectors[0]);
            _logicEditor.Connect(trig.OutputConnectors[0], act1.InputConnectors[0]);
            _logicEditor.Connect(trig.OutputConnectors[0], act2.InputConnectors[0]);
        }
        else if (bp.Id == "e_stop")
        {
            var di4 = tags.FirstOrDefault(t => t.Name == "DI4") ?? tags[5];
            var do4 = tags.FirstOrDefault(t => t.Name == "DO4") ?? tags[13];
            var do3 = tags.FirstOrDefault(t => t.Name == "DO3") ?? tags[12];

            var inp = new InputNodeViewModel(di4) { Location = new Point(80, 120) };
            var trig = new TriggerNodeViewModel { TriggerType = TriggerType.ON_RISE, Location = new Point(340, 120) };
            var act1 = new ActionNodeViewModel(do4) { ActionType = ActionType.SET_TAG, ActionParam = 0, Location = new Point(600, 50) };
            var act2 = new ActionNodeViewModel(do3) { ActionType = ActionType.SET_TAG, ActionParam = 1, Location = new Point(600, 190) };

            _logicEditor.Nodes.Add(inp);
            _logicEditor.Nodes.Add(trig);
            _logicEditor.Nodes.Add(act1);
            _logicEditor.Nodes.Add(act2);

            _logicEditor.Connect(inp.OutputConnectors[0], trig.InputConnectors[0]);
            _logicEditor.Connect(trig.OutputConnectors[0], act1.InputConnectors[0]);
            _logicEditor.Connect(trig.OutputConnectors[0], act2.InputConnectors[0]);
        }
        else if (bp.Id == "overheat")
        {
            var ai1 = tags.FirstOrDefault(t => t.Name == "AI1") ?? tags[18];
            var do2 = tags.FirstOrDefault(t => t.Name == "DO2") ?? tags[11];

            var inp = new InputNodeViewModel(ai1) { Location = new Point(80, 120) };
            var trig = new TriggerNodeViewModel { TriggerType = TriggerType.ON_CHANGE, CompareOp = CompareOp.GT, ThresholdLo = 85, Location = new Point(340, 120) };
            var act = new ActionNodeViewModel(do2) { ActionType = ActionType.SET_TAG, ActionParam = 1, Location = new Point(600, 120) };

            _logicEditor.Nodes.Add(inp);
            _logicEditor.Nodes.Add(trig);
            _logicEditor.Nodes.Add(act);

            _logicEditor.Connect(inp.OutputConnectors[0], trig.InputConnectors[0]);
            _logicEditor.Connect(trig.OutputConnectors[0], act.InputConnectors[0]);
        }
        else if (bp.Id == "air_press")
        {
            var ai0 = tags.FirstOrDefault(t => t.Name == "AI0") ?? tags[17];
            var do3 = tags.FirstOrDefault(t => t.Name == "DO3") ?? tags[12];

            var inp = new InputNodeViewModel(ai0) { Location = new Point(80, 120) };
            var trig = new TriggerNodeViewModel { TriggerType = TriggerType.ON_CHANGE, CompareOp = CompareOp.LT, ThresholdLo = 40, Location = new Point(340, 120) };
            var act = new ActionNodeViewModel(do3) { ActionType = ActionType.SET_TAG, ActionParam = 1, Location = new Point(600, 120) };

            _logicEditor.Nodes.Add(inp);
            _logicEditor.Nodes.Add(trig);
            _logicEditor.Nodes.Add(act);

            _logicEditor.Connect(inp.OutputConnectors[0], trig.InputConnectors[0]);
            _logicEditor.Connect(trig.OutputConnectors[0], act.InputConnectors[0]);
        }
        else if (bp.Id == "press_window")
        {
            var ai0 = tags.FirstOrDefault(t => t.Name == "AI0") ?? tags[17];
            var do6 = tags.FirstOrDefault(t => t.Name == "DO6") ?? tags[15];

            var inp = new InputNodeViewModel(ai0) { Location = new Point(80, 120) };
            var trig = new TriggerNodeViewModel { TriggerType = TriggerType.ON_CHANGE, CompareOp = CompareOp.BETWEEN, ThresholdLo = 40, ThresholdHi = 80, Location = new Point(340, 120) };
            var act = new ActionNodeViewModel(do6) { ActionType = ActionType.SET_TAG, ActionParam = 1, Location = new Point(600, 120) };

            _logicEditor.Nodes.Add(inp);
            _logicEditor.Nodes.Add(trig);
            _logicEditor.Nodes.Add(act);

            _logicEditor.Connect(inp.OutputConnectors[0], trig.InputConnectors[0]);
            _logicEditor.Connect(trig.OutputConnectors[0], act.InputConnectors[0]);
        }
        else if (bp.Id == "scale_pressure")
        {
            var ai0 = tags.FirstOrDefault(t => t.Name == "AI0") ?? tags[17];
            var vreg0 = tags.FirstOrDefault(t => t.Name == "VREG0")
                        ?? tags.FirstOrDefault(t => t.Kind == TagKind.VirtualRegister && t.Index == 37)
                        ?? tags.FirstOrDefault(t => t.Kind == TagKind.VirtualRegister)
                        ?? tags[37];

            var inp = new InputNodeViewModel(ai0) { Location = new Point(80, 120) };
            var scale = new ScaleNodeViewModel(ai0, vreg0)
            {
                Gain = 0.0016,
                Offset = 0.0,
                Unit = "bar",
                DecimalPlaces = 1,
                IsClamped = true,
                ClampMin = 0.0,
                ClampMax = 16.0,
                CustomLabel = "Quy đổi Áp suất",
                Location = new Point(340, 120)
            };
            var act = new ActionNodeViewModel(vreg0) { ActionType = ActionType.SCALE_TAG, ActionParam = 1, Location = new Point(600, 120) };

            _logicEditor.Nodes.Add(inp);
            _logicEditor.Nodes.Add(scale);
            _logicEditor.Nodes.Add(act);

            _logicEditor.Connect(inp.OutputConnectors[0], scale.InputConnectors[0]);
            _logicEditor.Connect(scale.OutputConnectors[0], act.InputConnectors[0]);
        }
        else if (bp.Id == "scale_temp")
        {
            var ai1 = tags.FirstOrDefault(t => t.Name == "AI1") ?? tags[18];
            var vreg1 = tags.FirstOrDefault(t => t.Name == "VREG1")
                        ?? tags.FirstOrDefault(t => t.Kind == TagKind.VirtualRegister && t.Index == 38)
                        ?? tags.FirstOrDefault(t => t.Kind == TagKind.VirtualRegister)
                        ?? tags[38];

            var inp = new InputNodeViewModel(ai1) { Location = new Point(80, 120) };
            var scale = new ScaleNodeViewModel(ai1, vreg1)
            {
                Gain = 0.015,
                Offset = -50.0,
                Unit = "°C",
                DecimalPlaces = 1,
                IsClamped = true,
                ClampMin = -20.0,
                ClampMax = 100.0,
                CustomLabel = "Quy đổi Nhiệt độ",
                Location = new Point(340, 120)
            };
            var act = new ActionNodeViewModel(vreg1) { ActionType = ActionType.SCALE_TAG, ActionParam = 1, Location = new Point(600, 120) };

            _logicEditor.Nodes.Add(inp);
            _logicEditor.Nodes.Add(scale);
            _logicEditor.Nodes.Add(act);

            _logicEditor.Connect(inp.OutputConnectors[0], scale.InputConnectors[0]);
            _logicEditor.Connect(scale.OutputConnectors[0], act.InputConnectors[0]);
        }
        else if (bp.Id == "scale_tank_level")
        {
            var ai0 = tags.FirstOrDefault(t => t.Name == "AI0") ?? tags[17];
            var vreg0 = tags.FirstOrDefault(t => t.Name == "VREG0")
                        ?? tags.FirstOrDefault(t => t.Kind == TagKind.VirtualRegister && t.Index == 37)
                        ?? tags.FirstOrDefault(t => t.Kind == TagKind.VirtualRegister)
                        ?? tags[37];

            var inp = new InputNodeViewModel(ai0) { Location = new Point(80, 120) };
            var scale = new ScaleNodeViewModel(ai0, vreg0)
            {
                Gain = 0.0005,
                Offset = 0.0,
                Unit = "m",
                DecimalPlaces = 2,
                IsClamped = true,
                ClampMin = 0.0,
                ClampMax = 5.0,
                CustomLabel = "Mức nước Bồn",
                Location = new Point(340, 120)
            };
            var act = new ActionNodeViewModel(vreg0) { ActionType = ActionType.SCALE_TAG, ActionParam = 1, Location = new Point(600, 120) };

            _logicEditor.Nodes.Add(inp);
            _logicEditor.Nodes.Add(scale);
            _logicEditor.Nodes.Add(act);

            _logicEditor.Connect(inp.OutputConnectors[0], scale.InputConnectors[0]);
            _logicEditor.Connect(scale.OutputConnectors[0], act.InputConnectors[0]);
        }
        else if (bp.Id == "alarm_press")
        {
            var ai0 = tags.FirstOrDefault(t => t.Name == "AI0") ?? tags[17];
            var do3 = tags.FirstOrDefault(t => t.Name == "DO3") ?? tags[12];

            var inp = new InputNodeViewModel(ai0) { Location = new Point(80, 120) };
            var trig = new TriggerNodeViewModel { TriggerType = TriggerType.ON_CHANGE, CompareOp = CompareOp.LT, ThresholdLo = 20, Location = new Point(340, 120) };
            var act = new ActionNodeViewModel(do3) { ActionType = ActionType.SEND_ALARM, ActionParam = 101, Location = new Point(600, 120) };

            _logicEditor.Nodes.Add(inp);
            _logicEditor.Nodes.Add(trig);
            _logicEditor.Nodes.Add(act);

            _logicEditor.Connect(inp.OutputConnectors[0], trig.InputConnectors[0]);
            _logicEditor.Connect(trig.OutputConnectors[0], act.InputConnectors[0]);
        }
        else if (bp.Id == "lube_cycle")
        {
            var vreg0 = tags.FirstOrDefault(t => t.Name == "VREG0") ?? tags[37];
            var do6 = tags.FirstOrDefault(t => t.Name == "DO6") ?? tags[15];

            var inp = new InputNodeViewModel(vreg0) { Location = new Point(80, 120) };
            var trig = new TriggerNodeViewModel { TriggerType = TriggerType.INTERVAL, ForMs = 60000, Location = new Point(340, 120) };
            var act = new ActionNodeViewModel(do6) { ActionType = ActionType.TOGGLE_TAG, ActionParam = 0, Location = new Point(600, 120) };

            _logicEditor.Nodes.Add(inp);
            _logicEditor.Nodes.Add(trig);
            _logicEditor.Nodes.Add(act);

            _logicEditor.Connect(inp.OutputConnectors[0], trig.InputConnectors[0]);
            _logicEditor.Connect(trig.OutputConnectors[0], act.InputConnectors[0]);
        }
        else if (bp.Id == "ton_delay")
        {
            var di0 = tags.FirstOrDefault(t => t.Name == "DI0") ?? tags[1];
            var do0 = tags.FirstOrDefault(t => t.Name == "DO0") ?? tags[9];

            var inp = new InputNodeViewModel(di0) { Location = new Point(80, 120) };
            var timer = new TimerNodeViewModel("TON", di0, do0, 5000) { Location = new Point(340, 120) };
            var act = new ActionNodeViewModel(do0) { Location = new Point(600, 120) };

            _logicEditor.Nodes.Add(inp);
            _logicEditor.Nodes.Add(timer);
            _logicEditor.Nodes.Add(act);

            _logicEditor.Connect(inp.OutputConnectors[0], timer.InputConnectors[0]);
            _logicEditor.Connect(timer.OutputConnectors[0], act.InputConnectors[0]);
        }
        else if (bp.Id == "tof_cooling")
        {
            var di1 = tags.FirstOrDefault(t => t.Name == "DI1") ?? tags[2];
            var do1 = tags.FirstOrDefault(t => t.Name == "DO1") ?? tags[10];

            var inp = new InputNodeViewModel(di1) { Location = new Point(80, 120) };
            var timer = new TimerNodeViewModel("TOF", di1, do1, 10000) { Location = new Point(340, 120) };
            var act = new ActionNodeViewModel(do1) { Location = new Point(600, 120) };

            _logicEditor.Nodes.Add(inp);
            _logicEditor.Nodes.Add(timer);
            _logicEditor.Nodes.Add(act);

            _logicEditor.Connect(inp.OutputConnectors[0], timer.InputConnectors[0]);
            _logicEditor.Connect(timer.OutputConnectors[0], act.InputConnectors[0]);
        }
        else if (bp.Id == "tp_air_blow")
        {
            var di3 = tags.FirstOrDefault(t => t.Name == "DI3") ?? tags[4];
            var do3 = tags.FirstOrDefault(t => t.Name == "DO3") ?? tags[12];

            var inp = new InputNodeViewModel(di3) { Location = new Point(80, 120) };
            var timer = new TimerNodeViewModel("TP", di3, do3, 1500) { Location = new Point(340, 120) };
            var act = new ActionNodeViewModel(do3) { Location = new Point(600, 120) };

            _logicEditor.Nodes.Add(inp);
            _logicEditor.Nodes.Add(timer);
            _logicEditor.Nodes.Add(act);

            _logicEditor.Connect(inp.OutputConnectors[0], timer.InputConnectors[0]);
            _logicEditor.Connect(timer.OutputConnectors[0], act.InputConnectors[0]);
        }
        else if (bp.Id == "ctu_batch")
        {
            var di2 = tags.FirstOrDefault(t => t.Name == "DI2") ?? tags[3];
            var di3 = tags.FirstOrDefault(t => t.Name == "DI3") ?? tags[4];
            var do2 = tags.FirstOrDefault(t => t.Name == "DO2") ?? tags[11];
            var cv = tags.FirstOrDefault(t => t.Name == "VREG_RETAIN0") ?? tags.FirstOrDefault(t => t.Kind == TagKind.VirtualRegisterRetain);

            var inpCount = new InputNodeViewModel(di2) { Location = new Point(80, 50) };
            var inpReset = new InputNodeViewModel(di3) { Location = new Point(80, 190) };
            var counter = new CounterNodeViewModel("CTU", di2, cv, do2, di3, 10) { Location = new Point(340, 120) };
            var actOut = new ActionNodeViewModel(do2) { Location = new Point(600, 120) };

            _logicEditor.Nodes.Add(inpCount);
            _logicEditor.Nodes.Add(inpReset);
            _logicEditor.Nodes.Add(counter);
            _logicEditor.Nodes.Add(actOut);

            _logicEditor.Connect(inpCount.OutputConnectors[0], counter.InputConnectors[0]);
            _logicEditor.Connect(inpReset.OutputConnectors[0], counter.InputConnectors[1]);
            _logicEditor.Connect(counter.OutputConnectors[0], actOut.InputConnectors[0]);
        }
        else if (bp.Id == "ctd_batch")
        {
            var di1 = tags.FirstOrDefault(t => t.Name == "DI1") ?? tags[2];
            var di0 = tags.FirstOrDefault(t => t.Name == "DI0") ?? tags[1];
            var do5 = tags.FirstOrDefault(t => t.Name == "DO5") ?? tags[14];
            var cv = tags.FirstOrDefault(t => t.Name == "VREG_RETAIN1") ?? tags.FirstOrDefault(t => t.Kind == TagKind.VirtualRegisterRetain);

            var inpCount = new InputNodeViewModel(di1) { Location = new Point(80, 50) };
            var inpReset = new InputNodeViewModel(di0) { Location = new Point(80, 190) };
            var counter = new CounterNodeViewModel("CTD", di1, cv, do5, di0, 5) { Location = new Point(340, 120) };
            var actOut = new ActionNodeViewModel(do5) { Location = new Point(600, 120) };

            _logicEditor.Nodes.Add(inpCount);
            _logicEditor.Nodes.Add(inpReset);
            _logicEditor.Nodes.Add(counter);
            _logicEditor.Nodes.Add(actOut);

            _logicEditor.Connect(inpCount.OutputConnectors[0], counter.InputConnectors[0]);
            _logicEditor.Connect(inpReset.OutputConnectors[0], counter.InputConnectors[1]);
            _logicEditor.Connect(counter.OutputConnectors[0], actOut.InputConnectors[0]);
        }
        else if (bp.Id == "rtc_morning_shift")
        {
            var di0 = tags.FirstOrDefault(t => t.Name == "DI0") ?? tags[1];
            var do1 = tags.FirstOrDefault(t => t.Name == "DO1") ?? tags[10];

            var inp = new InputNodeViewModel(di0) { Location = new Point(80, 120) };
            var trig = new TriggerNodeViewModel { TriggerType = TriggerType.TIME_WINDOW, CompareOp = CompareOp.EQ, ThresholdLo = 700, ThresholdHi = 700, Location = new Point(340, 120) };
            var act = new ActionNodeViewModel(do1) { ActionType = ActionType.SET_TAG, ActionParam = 1, Location = new Point(600, 120) };

            _logicEditor.Nodes.Add(inp);
            _logicEditor.Nodes.Add(trig);
            _logicEditor.Nodes.Add(act);

            _logicEditor.Connect(inp.OutputConnectors[0], trig.InputConnectors[0]);
            _logicEditor.Connect(trig.OutputConnectors[0], act.InputConnectors[0]);
        }
        else if (bp.Id == "rtc_night_security")
        {
            var di4 = tags.FirstOrDefault(t => t.Name == "DI4") ?? tags[5];
            var vflag0 = tags.FirstOrDefault(t => t.Name == "VFLAG0") ?? tags[33];
            var do7 = tags.FirstOrDefault(t => t.Name == "DO7") ?? tags[16];

            var inp = new InputNodeViewModel(di4) { Location = new Point(80, 120) };
            var trig = new TriggerNodeViewModel { TriggerType = TriggerType.TIME_WINDOW, CompareOp = CompareOp.BETWEEN, ThresholdLo = 1800, ThresholdHi = 600, Location = new Point(340, 120) };
            var grd = new GuardNodeViewModel(vflag0) { Location = new Point(600, 120) };
            var act = new ActionNodeViewModel(do7) { ActionType = ActionType.SET_TAG, ActionParam = 1, Location = new Point(860, 120) };

            _logicEditor.Nodes.Add(inp);
            _logicEditor.Nodes.Add(trig);
            _logicEditor.Nodes.Add(grd);
            _logicEditor.Nodes.Add(act);

            _logicEditor.Connect(inp.OutputConnectors[0], trig.InputConnectors[0]);
            _logicEditor.Connect(trig.OutputConnectors[0], grd.InputConnectors[0]);
            _logicEditor.Connect(grd.OutputConnectors[0], act.InputConnectors[0]);
        }

        // Tự động biên dịch ngay lập tức để đồng bộ sang bảng Rule
        _logicEditor.CompileNow();
        _navigateToEditor();
    }
}
