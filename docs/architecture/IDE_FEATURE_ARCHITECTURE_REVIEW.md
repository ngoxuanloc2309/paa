# ĐÁNH GIÁ CHUYÊN SÂU KIẾN TRÚC & TÍNH NĂNG IDE CÔNG NGHIỆP
## SIMPLEPLC STUDIO / SYNAPTIX IDE v2.2.4
### Báo Cáo Thẩm Định Kỹ Thuật, Đối Sánh Chuẩn Công Nghiệp & Lộ Trình Nâng Cấp

---

**Mã tài liệu**: `SPLC-ENG-REV-2026-IDE-001`  
**Phiên bản hệ thống**: SynaptiX IDE v2.2.4 (SimplePLC Studio Desktop Suite)  
**Tác giả**: Ban Kiến Trúc Phần Mềm & Thẩm Định Hệ Thống (Teamwork Engineering Review)  
**Ngày phát hành**: 2026-10-07  
**Phạm vi thẩm định**: Toàn bộ kiến trúc Desktop IDE (`SimplePLC.Studio`), các tầng phụ thuộc (`SimplePLC.Application`, `SimplePLC.Domain`, `SimplePLC.Protocol`, `SimplePLC.Infrastructure`), và bộ công cụ kiểm thử phần cứng (`tools/SimplePLC.HardwareTest`).  

---

## MỤC LỤC
1. [TỔNG QUAN ĐIỀU HÀNH & BẢNG ĐIỂM ĐÁNH GIÁ KIẾN TRÚC](#1-tổng-quan-điều-hành--bảng-điểm-đánh-giá-kiến-trúc)
2. [ĐÁNH GIÁ CHUYÊN SÂU 6 PHÂN HỆ CHỨC NĂNG CỐT LÕI](#2-đánh-giá-chuyên-sâu-6-phân-hệ-chức-năng-cốt-lõi)
   - 2.1 [FBD Canvas & Hạ Tầng Đồ Họa Nodify](#21-fbd-canvas--hạ-tầng-đồ-họa-nodify)
   - 2.2 [Bảng Quy Tắc Rules Table & Đồng Bộ Đồ Thị Hai Chiều](#22-bảng-quy-tắc-rules-table--đồng-bộ-đồ-thị-hai-chiều)
   - 2.3 [Bảng Giám Sát Thời Gian Thực Live Watch Table](#23-bảng-giám-sát-thời-gian-thực-live-watch-table)
   - 2.4 [Danh Mục Biến Tag Catalog & Ánh Xạ Bộ Nhớ Modbus](#24-danh-mục-biến-tag-catalog--ánh-xạ-bộ-nhớ-modbus)
   - 2.5 [Cổng Nạp An Toàn Deploy Safety Gate & Cơ Chế Fail-Safe](#25-cổng-nạp-an-toàn-deploy-safety-gate--cơ-chế-fail-safe)
   - 2.6 [Bộ Giả Lập MCU Hardware Simulator & Kiểm Thử Phần Cứng Vòng Lặp COM](#26-bộ-giả-lập-mcu-hardware-simulator--kiểm-thử-phần-cứng-vòng-lặp-com)
3. [ĐỐI SÁNH KHOẢNG CÁCH TÍNH NĂNG VỚI CÁC NỀN TẢNG ĐẦU NGÀNH](#3-đối-sánh-khoảng-cách-tính-năng-với-các-nền-tảng-đầu-ngành)
   - 3.1 [Khối Chức Năng Tự Định Nghĩa (Custom UDFB)](#31-khối-chức-năng-tự-định-nghĩa-custom-udfb)
   - 3.2 [Kiểm Tra Lỗi Cú Pháp & Cảnh Báo Trực Quan Trên Canvas (Real-time Linter Squiggles)](#32-kiểm-tra-lỗi-cú-pháp--cảnh-báo-trực-quan-trên-canvas-real-time-linter-squiggles)
   - 3.3 [So Sánh Sai Khác Đồ Họa & Quản Lý Phiên Bản (Visual Graph Diff & VCS)](#33-so-sánh-sai-khác-đồ-họa--quản-lý-phiên-bản-visual-graph-diff--vcs)
   - 3.4 [Hệ Thống Quản Lý Cảnh Báo & Sự Kiện (Alarms & Events Banner ISA-18.2 / IEC 62682)](#34-hệ-thống-quản-lý-cảnh-báo--sự-kiện-alarms--events-banner-isa-182--iec-62682)
4. [LỘ TRÌNH NÂNG CẤP HỆ THỐNG 3 GIAI ĐOẠN (ACTIONABLE ROADMAP)](#4-lộ-trình-nâng-cấp-hệ-thống-3-giai-đoạn-actionable-roadmap)
   - 4.1 [Giai Đoạn 1: Quick Wins (Triển khai ngay: 1 - 2 tuần)](#41-giai-đoạn-1-quick-wins-triển-khai-ngay-1---2-tuần)
   - 4.2 [Giai Đoạn 2: Mid-Term (Trung hạn: 1 - 3 tháng)](#42-giai-đoạn-2-mid-term-trung-hạn-1---3-tháng)
   - 4.3 [Giai Đoạn 3: Strategic (Chiến lược: 3 - 6 tháng)](#43-giai-đoạn-3-strategic-chiến-lược-3---6-tháng)
5. [KẾT LUẬN & KIẾN NGHỊ HÀNH ĐỘNG](#5-kết-luận--kiến-nghị-hành-động)

---

## 1. TỔNG QUAN ĐIỀU HÀNH & BẢNG ĐIỂM ĐÁNH GIÁ KIẾN TRÚC

### 1.1 Tổng quan Đánh giá
SynaptiX IDE (SimplePLC Studio v2.2.4) là môi trường phát triển tích hợp công nghiệp thế hệ mới được xây dựng trên nền tảng .NET 8 WPF, thư viện đồ họa Nodify 6.2.0, và kiến trúc Clean Architecture. Hệ thống hướng tới việc cung cấp một công cụ lập trình logic gọn nhẹ, trực quan, loại bỏ sự cồng kềnh của các bộ phần mềm tự động hóa truyền thống nhưng vẫn đảm bảo tính an toàn vận hành ở cấp độ công nghiệp nặng.

Các đặc tính kiến trúc nổi bật:
1. **Tuân thủ Clean Architecture**: Tách biệt rõ ràng ranh giới giữa Domain Logic, Protocol Modbus, Application Services, và UI Presentation (WPF MVVM).
2. **Tính tất định thời gian thực (Real-time Determinism)**: Mô hình thực thi luật ECA (Event-Condition-Action) tuần tự với chu kỳ quét mô phỏng 10-20ms, loại bỏ hoàn toàn cấp phát bộ nhớ động (Zero Allocations trên hot-path scan pass).
3. **Cơ chế nạp an toàn 4 bước (Zero-Downtime Atomic Deployment)**: Sử dụng kiến trúc Double-Buffering (Staging `0x9010` -> CRC-16 `0x9003` -> Atomic Commit `0xA000 = 0xA5A5` -> Active `0x0100`), đảm bảo MCU không bao giờ rơi vào trạng thái nạp nửa chừng (half-state execution).
4. **Cơ chế bảo vệ bằng Diagnostic Lease Watchdog (3000ms)**: Thuê quyền can thiệp cưỡng bức ngõ ra có thời hạn; tự động đưa toàn bộ ngõ ra số về mức 0 (Fail-Safe Off) khi ngắt kết nối.

### 1.2 Bảng Điểm Đánh Giá Kiến Trúc (Architecture Evaluation Scorecard)

| Trụ Cột Đánh Giá | Điểm (Thang 10) | Xếp Hạng | Tóm Tắt Hiện Trạng & Cơ Sở Chấm Điểm |
| :--- | :---: | :---: | :--- |
| **Kiến Trúc & Tính Module Hóa (Architecture & Modularity)** | **9.0 / 10** | Xuất Sắc | Tách tầng Clean Architecture nghiêm ngặt; Domain hoàn toàn không rò rỉ I/O hay UI; Application use case phân lập tốt. Điểm trừ: Chưa có khái niệm đóng gói khối con (UDFB Sub-graph) ở mức Domain. |
| **Độ Tin Cậy Thời Gian Thực (Real-Time Reliability & Determinism)** | **8.8 / 10** | Tốt | Chu kỳ quét Scan Pass 10-20ms ổn định; thuật toán phát hiện chu trình phụ thuộc (`RuleDependencyGraph`) và xung đột ghi (`WriteConflictValidator`) chặt chẽ. Điểm trừ: Chưa có tính năng đo jitter thực trên phần cứng vật lý qua oscilloscope. |
| **Công Thái Học & Giao Diện Người Dùng (UI/UX Ergonomics)** | **7.5 / 10** | Khá | Canvas Nodify mượt mà, màu sắc chuẩn công nghiệp (`#006487`, `#107C41`), phím tắt căn lề (`Alt+Mũi tên`, `Alt+G`) rất tiện lợi, i18n Vi/En tức thì. Điểm trừ nghiêm trọng: Lỗi cú pháp và xung đột ghi chỉ báo chữ ở bảng Inspector bên phải, thiếu hoàn toàn Squiggles/Adorners trực tiếp trên canvas; chưa có Visual Diff. |
| **Giao Thức Modbus & Cơ Chế An Toàn (Safety & Protocol Compliance)** | **9.2 / 10** | Xuất Sắc | Cơ chế Staging-Commit 4 bước với mã khóa `0xA5A5` xuất sắc; Watchdog Lease 3000ms ngắt an toàn ngõ ra khi đứt cáp; cơ chế khóa đường truyền (`IDeviceOperationCoordinator`) ngăn chặn triệt để xung đột bus RS485. |
| **Bộ Giả Lập & Kiểm Thử Phần Cứng (Simulation & Test Suite)** | **9.0 / 10** | Xuất Sắc | Bộ giả lập C# chuẩn xác 1:1 với firmware STM32; kịch bản tự động hóa loopback COM5 <-> COM10 7 bước bao phủ toàn diện từ Handshake, Telemetry, Function Block đến Fail-Safe Watchdog. |
| **ĐIỂM TỔNG HỢP (COMPOSITE)** | **8.7 / 10** | **Vững Chắc (Production Ready cho MVP)** | **Hệ thống có nền tảng cốt lõi rất mạnh, sẵn sàng mở rộng các tính năng cấp cao (UDFB, Canvas Linter, Git VCS, ISA-18.2 Alarms).** |

---

## 2. ĐÁNH GIÁ CHUYÊN SÂU 6 PHÂN HỆ CHỨC NĂNG CỐT LÕI

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                                   SIMPLEPLC STUDIO                                     │
│ ┌──────────────────────┬────────────────────────┬────────────────────────────────────┐ │
│ │ 1. FBD CANVAS        │ 2. RULES TABLE         │ 3. LIVE WATCH TABLE                │ │
│ │ - Nodify v6.2.0      │ - 32-Byte Big-Endian   │ - 200ms / 5Hz Polling              │ │
│ │ - Orthogonal Routing │ - 2-Way Sync Engine    │ - Polling Lease Coordinator        │ │
│ │ - Macro Expansion    │ - Inline Cell Editing  │ - Interlocked UI Throttling        │ │
│ ├──────────────────────┼────────────────────────┼────────────────────────────────────┤ │
│ │ 4. TAG CATALOG       │ 5. DEPLOY SAFETY GATE  │ 6. MCU HARDWARE SIMULATOR          │ │
│ │ - 124+ Unified Tags  │ - 4-Step Atomic Commit │ - 10-20ms Scan Cycle Pass          │ │
│ │ - 0x0900..0x09F7 Map │ - 0xA5A5 Magic Word    │ - Virtual COM Loopback (COM5/10)   │ │
│ │ - BOOL / INT32 / REAL│ - 3000ms Watchdog      │ - 7-Step Auto Test Integration     │ │
│ └──────────────────────┴────────────────────────┴────────────────────────────────────┘ │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

### 2.1 FBD Canvas & Hạ Tầng Đồ Họa Nodify
Phân hệ biên tập sơ đồ khối Function Block Diagram tọa lạc tại `SimplePLC.Studio/Views/LogicEditorView.xaml` và `SimplePLC.Studio/ViewModels/LogicEditorViewModel.cs`.

#### 1. Thành phần Thư viện & Cấu hình Điều hướng
- **Thư viện nền tảng**: Thư viện mã nguồn mở chuyên dụng cho sơ đồ khối **Nodify v6.2.0** (`nodify:NodifyEditor`).
- **Liên kết Dữ liệu (Bindings)**:
  - `ItemsSource="{Binding Nodes}"`: Tập hợp các phần tử kế thừa từ `GraphNodeViewModel`.
  - `Connections="{Binding Connections}"`: Tập hợp các liên kết dây tín hiệu kế thừa từ `ConnectionViewModel`.
  - `PendingConnectionTemplate`: Template quản lý trạng thái tương tác chuột khi người dùng đang kéo đường dây mới (`LogicEditorView.xaml:73`).
- **Định tuyến Đường dây Trực giao (Orthogonal Wire Routing)**:
  - Sử dụng thành phần `nodify:StepConnection` cho phép dây tự động bẻ góc vuông công nghiệp thay vì dây cong Bezier tùy tiện:
    - Đường nét trực quan chính: Độ dày nét `StrokeThickness="2"`, bán kính bo góc `Spacing="15"`.
    - Vỏ bọc cảm ứng tương tác vô hình (Hit-Testing Envelope): `StrokeThickness="12"` với `Stroke="Transparent"` bao bọc dây chính. Kỹ thuật này giúp kỹ sư dễ dàng click chọn hoặc nhấn `Delete` để xóa dây mà không đòi hỏi độ chính xác tuyệt đối tới từng pixel chuột.
    - Mã màu trạng thái dây: Trạng thái tĩnh/chờ mang màu xám đá `#64748B` (Slate Grey); khi dây dẫn tín hiệu mức cao (High / True), dây chuyển sang màu xanh ngọc công nghiệp `#107C41` kèm hiệu ứng mũi tên di chuyển chỉ báo chiều dòng dữ liệu; khi được click chọn, dây phát sáng viền xanh SynaptiX Petrol `#006487`.
- **Không gian Làm việc & Điều hướng**:
  - Hỗ trợ không gian vô hạn (Infinite Canvas), thu phóng mượt mà từ $20\%$ (`MinViewportZoom="0.2"`) đến $200\%$ (`MaxViewportZoom="2.0"`).
  - Điều hướng lia chuột (Pan) bằng nút chuột giữa (Middle Mouse Button) hoặc kéo giữ chuột phải (Right Mouse Drag).
  - Lưới tọa độ tự động co giãn thích ứng theo mức zoom của người dùng.

#### 2. Phân Cấp Lớp Đối Tượng Node (GraphNodeViewModel Hierarchy)
Toàn bộ logic đồ thị kế thừa từ lớp cơ sở trừu tượng tại `SimplePLC.Studio/ViewModels/GraphNodeViewModel.cs`:
- **`GraphNodeViewModel` (Lớp Cơ Sở)**:
  - Kế thừa từ `ObservableObject` (CommunityToolkit.Mvvm).
  - Thuộc tính hình học: `Location` (`Point`), `Width`, `Height`.
  - Thuộc tính định danh: `Id` (`Guid`), `Title`, `Subtitle`, `HeaderBrush`, `NodeType` (`GraphNodeType`).
  - Cổng kết nối: `InputConnectors` (`ObservableCollection<ConnectorViewModel>`), `OutputConnectors`.
  - Trạng thái giám sát trực tiếp: `IsExecuting` (`bool`), `LiveValue` (`object`), `HasError` (`bool`), `ErrorMessage` (`string`).
- **Các Lớp Dẫn Xuất Chuyên Biệt**:
  1. `InputNodeViewModel`: Nguồn cấp dữ liệu ngõ vào vật lý/nội bộ (`DI0..DI7`, `AI0..AI3`, `VREG0..VREG31`).
  2. `TriggerNodeViewModel`: Phát hiện sự kiện chuyển tiếp trạng thái (`OnRise`, `OnFall`, `OnChange`, `Interval`, `TimeWindow`), tích hợp tham số lọc nhiễu dội phím (`DebounceMs` / `for_ms`).
  3. `GuardNodeViewModel`: Khối điều kiện cho phép (Boolean Guard: so sánh toán tử `==`, `!=`, `>`, `<`, `>=`, `<=`; cờ đảo `IsNegated` đại diện cho tiếp điểm thường đóng NC).
  4. `ActionNodeViewModel`: Tác vụ điều khiển chấp hành (`SetTag`, `ToggleTag`, `IncCounter`, `ScaleTag`, `AddTag`).
  5. `TimerNodeViewModel`: Khối vĩ mô định thời (Timer Macro) chuẩn IEC 61131-3: hỗ trợ 3 chế độ `TON` (On-Delay), `TOF` (Off-Delay), `TP` (Pulse Timer); tham số `PresetTimeMs`, `ElapsedTimeMs`.
  6. `CounterNodeViewModel`: Khối vĩ mô bộ đếm (Counter Macro): hỗ trợ `CTU` (Count Up), `CTD` (Count Down); tham số `PresetValue`, `CurrentCount`, ngõ vào xóa `ResetInput`.
  7. `ScaleNodeViewModel`: Khối chuẩn hóa tuyến tính tín hiệu Analog ($y = k \cdot x + b$): tham số `InMin`, `InMax`, `OutMin`, `OutMax`, cờ tự động kẹp biên an toàn `ClampOutput`.

#### 3. Chân Kết Nối & Cơ Chế Tự Đảo Chiều Thông Minh (Self-Orienting Pins)
Tại `ConnectorViewModel.cs` và `LogicEditorViewModel.cs`:
- Mỗi cổng được gán `FlowDirection` (`Input` hoặc `Output`) và `ConnectorType` (`Flow`, `DataBool`, `DataInt32`, `DataReal`).
- **Cơ chế Tự Đảo Chiều (Self-Orienting)**: Khi kỹ sư vô tình kéo dây ngược từ cổng Input sang cổng Output, phương thức `CompleteConnection` (`LogicEditorViewModel.cs:1099`) tự động phát hiện `source.IsInput && target.IsOutput` và lập tức hoán đổi vị trí:
  $$\text{Source}_{\text{final}} = \text{Target}_{\text{raw}}, \quad \text{Target}_{\text{final}} = \text{Source}_{\text{raw}}$$
  đảm bảo nguyên tắc dòng dữ liệu công nghiệp luôn truyền từ Trái sang Phải (Left-to-Right Dataflow).
- **Quy tắc Kiểm tra Tính Tương Thích Kết Nối (Connection Validation)**:
  - Cấm kết nối giữa 2 cổng cùng chiều (Input-Input hoặc Output-Output).
  - Cấm tự nối vòng về chính khối hiện tại (Self-loop).
  - Cấm tạo trùng lặp kết nối giữa cùng một cặp chân.
  - Bắt buộc tuân thủ đơn cổng vào: Mỗi chân Input chỉ được nhận tối đa 1 dây dẫn (`SPLC-GRAPH-010`).

#### 4. Ngữ Pháp Đồ Thị ECA & Mở Rộng Macro (Graph Grammar V1 & Macro Expansion)
SimplePLC Studio không áp dụng mô hình Rung Network kiểu Ladder truyền thống mà vận hành trên mô hình **ECA (Event - Condition - Action)** tuân thủ `src/SimplePLC.Application/Logic/Graph/GraphGrammarV1.cs`:
- **Biểu diễn Cổng AND**: Tạo ra bởi liên kết tuần tự `TriggerNode` (Event) $\rightarrow$ `GuardNode` (Condition). Nếu sự kiện nổ và điều kiện Guard thỏa mãn, Action mới được kích hoạt.
  *Ràng buộc Ngữ pháp*: `GraphGrammarV1` cấm ngặt việc xâu chuỗi liên tiếp nhiều khối Guard (`SPLC-GRAPH-007: ErrChainedGuards`). Để AND nhiều điều kiện, kỹ sư phải sử dụng cờ nhớ trung gian `VFLAG`.
- **Biểu diễn Cổng NOT**: Thực hiện qua cờ `IsNegated = true` trên `GuardNodeViewModel` hoặc sườn xuống `OnFall` trên `TriggerNodeViewModel`.
- **Biểu diễn Cổng OR**: Thực hiện bằng cách tạo nhiều nhánh đường ống độc lập cùng trỏ vào một đích (`TargetTag`).
- **Cơ chế Phân Rã Macro (Macro Expansion in `RuleCompiler.cs`)**:
  Các khối chức năng cao cấp được biên dịch thành các luật nguyên thủy 32-byte:
  - **TON (2 Rules)**:
    1. Khi Input có sườn lên (`OnRise`), kích hoạt Timer nội bộ với `Interval = PresetTimeMs`.
    2. Khi Timer chạm hạn (`OnRise` của cờ timer), thực hiện `SetTag Q = 1`. Khi Input mất (`OnFall`), thực hiện `SetTag Q = 0`.
  - **TOF (2 Rules)**:
    1. Khi Input có sườn lên (`OnRise`), lập tức `SetTag Q = 1`.
    2. Khi Input có sườn xuống (`OnFall`), kích hoạt trễ ngắt `PT`. Hết trễ thực hiện `SetTag Q = 0`.
  - **TP (2 Rules)**:
    1. Khi Input có sườn lên và $Q = 0$, `SetTag Q = 1` và khởi động bộ định thời.
    2. Hết thời gian xung $PT$, tự động ngắt `SetTag Q = 0`.
  - **CTU / CTD (3-4 Rules)**:
    1. Bắt sườn xung đếm $\rightarrow$ `IncCounter`.
    2. So sánh `CurrentCount >= PV` $\rightarrow$ Đặt ngõ ra $Q = 1$.
    3. Bắt sườn Reset $\rightarrow$ Xóa `CurrentCount = 0`, $Q = 0$.
  - **SCALE (1 Rule)**:
    Khi giá trị ngõ vào thay đổi (`OnChange`), thực hiện tác vụ `ScaleTag` với hệ số góc $k$ và độ lệch $b$.

#### 5. Truyền Dẫn Giá Trị Thời Gian Thực (Real-Time Value Propagation)
- `RuntimeMonitorService` đọc dữ liệu telemetry qua Modbus và nạp vào `IRuntimeStateStore`.
- `LogicEditorViewModel` đồng bộ giá trị vào các Node.
- `NodeLiveStateConverters.cs` ánh xạ trạng thái sang giao diện trực quan:
  - Viền khối đổi màu xanh lá `#107C41` phát sáng khi logic đang tích cực (`IsExecuting = true`).
  - Giá trị trực tiếp (`LiveValue`) hiển thị ngay dưới chân pin của từng connector.
  - Dây dẫn `StepConnection` phát sáng xanh lá và xuất hiện mũi tên chuyển động khi mức tín hiệu là High (True / 1).

---

### 2.2 Bảng Quy Tắc Rules Table & Đồng Bộ Đồ Thị Hai Chiều
Tọa lạc tại `SimplePLC.Studio/Views/RuleTableView.xaml` và `SimplePLC.Studio/ViewModels/RuleTableViewModel.cs`.

#### 1. Cấu Trúc RuleItemModel
Bảng quy tắc hiển thị danh sách các luật thực thi (`ObservableCollection<RuleItemModel>`). Mỗi luật trong `RuleItemModel.cs` đại diện cho một lệnh điều khiển ECA hoàn chỉnh:

| Thuộc Tính | Kiểu Dữ Liệu | Ý Nghĩa Kỹ Thuật |
| :--- | :--- | :--- |
| `Id` | `int` | Thứ tự thực thi của luật trong bảng (1-based index) |
| `TriggerTag` | `string` | Tên biến kích hoạt sự kiện (ví dụ: `DI0`, `VFLAG1`) |
| `TriggerType` | `TriggerType` | Kiểu kích hoạt (`OnRise`, `OnFall`, `OnChange`, `Interval`, `TimeWindow`) |
| `GuardTag` | `string` | Tên biến kiểm tra điều kiện (tùy chọn) |
| `GuardOperator` | `GuardOp` | Phép toán so sánh điều kiện (`==`, `!=`, `>`, `<`, `>=`, `<=`, `None`) |
| `GuardValue` | `int` | Hằng số so sánh của Guard |
| `ActionTag` | `string` | Tên biến chịu tác động thực thi (ví dụ: `DO0`, `COUNTER0`) |
| `ActionType` | `ActionType` | Tác vụ thực hiện (`SetTag`, `ToggleTag`, `IncCounter`, `ScaleTag`, `AddTag`) |
| `ActionParam` | `int` | Tham số hành động (giá trị gán, bước nhảy hoặc id scale) |
| `SourceNodes` | `List<ProjectNodeData>` | Danh sách Node nguồn trên FBD Canvas tạo ra Rule này |
| `SourceConnections` | `List<ProjectConnectionData>` | Danh sách Dây nối nguồn trên FBD Canvas tạo ra Rule này |

#### 2. Định Dạng Nhị Phân 32-Byte V1.7 Big-Endian (Binary Packing)
Được hiện thực đồng bộ tại `SimplePLC.Studio/Services/RuleBinaryEncoder.cs:140-188` và `src/SimplePLC.Protocol/Codec/RegisterCodec.cs:24-64`:
Mỗi luật điều khiển được đóng gói thành một cấu trúc nhị phân chuẩn định dạng Big-Endian chiếm đúng **32 bytes** (tương đương 16 thanh ghi Modbus Holding Registers 16-bit):

```text
CẤU TRÚC NHỊ PHÂN 32-BYTE / RULE (CHUẨN DATA CONTRACT V1.7 BIG-ENDIAN)
┌─────────────────────────────────┬─────────────────────────────────┐
│ Byte 00 - 03 (Thanh ghi 0 - 1)  │ Byte 04 - 07 (Thanh ghi 2 - 3)  │
│ threshold_lo (int32_t, BE)      │ threshold_hi (int32_t, BE)      │
├─────────────────────────────────┼─────────────────────────────────┤
│ Byte 08 - 11 (Thanh ghi 4 - 5)  │ Byte 12 - 15 (Thanh ghi 6 - 7)  │
│ for_ms (uint32_t, BE)           │ action_param (int32_t, BE)      │
├─────────────────────────────────┼─────────────────────────────────┤
│ Byte 16 - 17 (Thanh ghi 8)      │ Byte 18 - 19 (Thanh ghi 9)      │
│ trigger_tag (uint16_t, BE)      │ action_tag (uint16_t, BE)       │
├─────────────────────────────────┼────────────────┬────────────────┤
│ Byte 20 - 21 (Thanh ghi 10)     │ Byte 22 (Hi 11)│ Byte 23 (Lo 11)│
│ guard_tag (uint16_t, BE)        │ enabled        │ trigger_type   │
│ (bit15 NEG, bit 0..14 TagIdx)   │ (uint8_t: 0/1) │ (uint8_t: Enum)│
├────────────────┬────────────────┴────────────────┴────────────────┤
│ Byte 24 (Hi 12)│ Byte 25 (Lo 12)│ Byte 26 - 31 (Thanh ghi 13 - 15)│
│ compare_op     │ action_type    │ reserved[6]                     │
│ (uint8_t: Enum)│ (uint8_t: Enum)│ (6 x uint8_t = 0x00, R8 Padding)│
└────────────────┴────────────────┴─────────────────────────────────┘
```

**Bảng chi tiết 12 trường dữ liệu nhị phân chuẩn xác**:
| Offset Byte | Thanh Ghi Modbus | Tên Trường | Kiểu Dữ Liệu | Ý Nghĩa Kỹ Thuật & Giới Hạn |
|:---:|:---:|---|:---:|---|
| **00 - 03** | `+0 .. +1` | `threshold_lo` | `int32_t` (BE) | Ngưỡng dưới cho so sánh trigger (hoặc giá trị so sánh đơn). High Word ghi ở Reg +0, Low Word ghi ở Reg +1. |
| **04 - 07** | `+2 .. +3` | `threshold_hi` | `int32_t` (BE) | Ngưỡng trên (dùng cho phép so sánh khoảng `BETWEEN`). |
| **08 - 11** | `+4 .. +5` | `for_ms` | `uint32_t` (BE) | Thời gian duy trì điều kiện trước khi kích hoạt (Debounce ms / Dwell ms). |
| **12 - 15** | `+6 .. +7` | `action_param` | `int32_t` (BE) | Tham số thực thi: Giá trị gán cho `SET_TAG`, bước tăng cho `INC_COUNTER`, hoặc tỷ lệ Scale. |
| **16 - 17** | `+8` | `trigger_tag` | `uint16_t` (BE) | Chỉ số Tag kích hoạt (Tag Index trong `ProductDefinition`, dải 0..65534). |
| **18 - 19** | `+9` | `action_tag` | `uint16_t` (BE) | Chỉ số Tag đối tượng chịu tác động thực thi. |
| **20 - 21** | `+10` | `guard_tag` | `uint16_t` (BE) | Chỉ số Tag điều kiện bảo vệ: Bit 0..14 là Tag Index, Bit 15 là cờ đảo `NEGATE` (`0x8000`). Nếu không có Guard, đặt `0x7FFF`. |
| **22** | `+11` (High Byte) | `enabled` | `uint8_t` | Cờ kích hoạt luật: `1` = Có hiệu lực, `0` = Vô hiệu hóa. |
| **23** | `+11` (Low Byte) | `trigger_type` | `uint8_t` | Kiểu sự kiện: `0:NONE`, `1:ON_CHANGE`, `2:ON_RISE`, `3:ON_FALL`, `4:INTERVAL`, `5:TIME_WINDOW`. |
| **24** | `+12` (High Byte) | `compare_op` | `uint8_t` | Phép toán so sánh: `0:NONE`, `1:EQ`, `2:NEQ`, `3:GT`, `4:LT`, `5:GTE`, `6:LTE`, `7:BETWEEN`. |
| **25** | `+12` (Low Byte) | `action_type` | `uint8_t` | Hành vi thực thi: `0:NONE`, `1:SET_TAG`, `2:TOGGLE_TAG`, `3:INC_COUNTER`, `4:WRITE_REMOTE`, `5:SEND_ALARM`, `6:LOG_EVENT`, `7:SCALE_TAG`. |
| **26 - 31** | `+13 .. +15` | `reserved[6]` | `uint8_t[6]` | 6 bytes đệm dự phòng (luôn điền `0x00 0x00 0x00 0x00 0x00 0x00`), tương đương 3 thanh ghi Modbus. |

**Khẳng định tính toàn vẹn Checksum**: Trong chuẩn giao thức SimplePLC V1.7, mã kiểm tra toàn vẹn CRC-16 Modbus được tính toán trên **toàn bộ Payload danh sách rule** (tại thanh ghi `0x9003` - `ExpectedCrc16Address`), **hoàn toàn KHÔNG có trường CRC-32 hay checksum riêng lẻ cho từng rule đơn lẻ**. Mọi trường giả mạo (như `RuleId`, `AuxParam`, `Rule CRC-32`) đều không tồn tại trong firmware và protocol encoder của hệ thống.

#### 3. Cơ Chế Đồng Bộ Hai Chiều (2-Way Graph-Table Synchronization)
- **Từ Graph sang Rules (`RuleCompiler.cs`)**: Khi biên dịch đồ thị, `RuleCompiler` duyệt các pipeline node, phân tách các Macro thành các luật phẳng, đồng thời lưu trữ toàn bộ ID của các Node và Connection tham gia vào thuộc tính `SourceNodes` và `SourceConnections` của `RuleItemModel`.
- **Từ Rules ngược lại Graph (`RuleMapper.cs` & `LogicEditorViewModel.LoadFromRules`)**: Khi mở lại dự án từ tệp lưu trữ, hệ thống trích xuất dữ liệu hình học trong `SourceNodes` và `SourceConnections` để tái tạo lại đúng vị trí các khối và dây nối lên canvas Nodify.
- **Chỉnh sửa Trực tiếp Trên Bảng (Inline Cell Editing)**: Người dùng có thể chỉnh sửa trực tiếp giá trị trên DataGrid của `RuleTableView`. Khi thay đổi thông số, hệ thống kích hoạt sự kiện cập nhật ngược trở lại thuộc tính tương ứng trên Node đồ họa.

---

### 2.3 Bảng Giám Sát Thời Gian Thực Live Watch Table
Tọa lạc tại `SimplePLC.Studio/Views/LiveWatchView.xaml` và `SimplePLC.Studio/ViewModels/LiveWatchViewModel.cs`.

#### 1. Cấu Trúc Dữ Liệu WatchTagItemModel
Quản lý danh sách các thẻ theo dõi `WatchTags` (`ObservableCollection<WatchTagItemModel>`):
- Tên Tag, Địa chỉ Modbus hiển thị (`DisplayAddress`).
- Kiểu dữ liệu (`TagDataType`: `BOOL`, `INT32`, `REAL`).
- Giá trị thời gian thực (`CurrentValue`), Giá trị cưỡng bức (`ForcedValue`).
- Cờ cưỡng bức (`IsForced`), Huy hiệu chất lượng tín hiệu (`QualityBadge`: `GOOD`, `STALE`, `BAD_COMM`).
- Nhãn thời gian cập nhật cuối (`LastUpdated`).

#### 2. Vòng Lặp Polling 200ms / 5Hz & Điều Phối An Toàn (Safe Leasing)
Tại `src/SimplePLC.Application/Services/RuntimeMonitorService.cs`:
- **Tầng truyền thông hiện hành**: Toàn bộ luồng polling telemetry và giám sát runtime tags hiện đang vận hành trên nền **Modbus RTU qua cổng nối tiếp Serial COM (USB UART / USB-CDC Virtual COM)** thông qua `src/SimplePLC.Infrastructure/Transport/ModbusRtuClient.cs`. Giao thức Modbus TCP qua Ethernet / Wi-Fi là mục tiêu thuộc lộ trình chiến lược (Phase 3).
- **Tần số lấy mẫu**: Mặc định **200ms (5 Hz)** cho chu kỳ đọc telemetry và trạng thái runtime tags (`0x0900..0x09F7`). Chu kỳ kiểm tra nhịp tim MCU diễn ra mỗi 1000ms (1 Hz) thông qua bộ chia 5 (`telemetryDivisor = 5`).
- **Cơ chế Thu hồi Quyền Polling (Polling Lease Coordination)**:
  - Do giao thức RS-485 / Modbus RTU là đơn luồng bán song công (Half-Duplex), việc nhiều dịch vụ cùng gửi frame yêu cầu sẽ gây va chạm xung đột và timeout.
  - Khi người dùng chuẩn bị nạp chương trình (Deploy) hoặc can thiệp điều khiển cưỡng bức (Diagnostic Force), `IDeviceOperationCoordinator` phát yêu cầu `TryAcquirePollingLeaseAsync`.
  - `RuntimeMonitorService` lập tức tạm dừng luồng polling ngầm để nhường độc quyền đường truyền cho `DeployRulesUseCase` hoặc `DiagnosticControlService`.

#### 3. Kỹ Thuật Điều Phối Luồng Giao Diện (UI Batching & Throttling)
Để giao diện WPF không bị đơ giật khi nhận khối lượng lớn bản tin cập nhật liên tục ở tần số 5 Hz:
- Dữ liệu thô từ cổng COM được lưu vào `ConcurrentDictionary` trong `RuntimeStateStore`.
- Sử dụng biến cờ nguyên tử:
  ```csharp
  if (Interlocked.CompareExchange(ref _isUiScheduled, 1, 0) == 0)
  {
      _dispatcher.BeginInvoke(DispatcherPriority.DataBind, new Action(() =>
      {
          Interlocked.Exchange(ref _isUiScheduled, 0);
          UpdateVisibleRowsBatch();
      }));
  }
  ```
- Lệnh vẽ lại chỉ được đưa vào hàng đợi Dispatcher với độ ưu tiên `DispatcherPriority.DataBind` khi luồng UI đang rảnh, thực hiện cập nhật đồng loạt (Batch Update) cho các dòng hiển thị.

#### 4. Định Dạng Giá Trị Chuyên Biệt (Industrial Value Formatters)
- **BOOL**: Hiển thị chấm tròn công nghiệp kèm text: `● BẬT (1)` (xanh ngọc) hoặc `○ TẮT (0)` (xám mờ).
- **INT32**: Định dạng số có dấu phân cách hàng nghìn (`N0`), hỗ trợ chuyển đổi nhanh sang biểu diễn Hexadecimal (`0xXXXXXXXX`).
- **REAL / ANALOG**: Hiển thị 2 chữ số thập phân (`F2`), tự động quy đổi theo thang đo kỹ thuật (Engineering Units: mA, V, bar, °C).

---

### 2.4 Danh Mục Biến Tag Catalog & Ánh Xạ Bộ Nhớ Modbus
Tọa lạc tại `SimplePLC.Studio/ViewModels/TagCatalogViewModel.cs` và `src/SimplePLC.Protocol/Constants/ModbusRegisterMap.cs`.

#### 1. Phân Bổ Không Gian 124+ Tags Mặc Định

| Dải Tag ID | Tên Tag | Kiểu Dữ Liệu | Chức Năng Kỹ Thuật |
| :--- | :--- | :--- | :--- |
| `0 .. 7` | `DI0 .. DI7` | `BOOL` | 8 ngõ vào số vật lý cách ly quang (Optocoupler Digital Inputs) |
| `8 .. 15` | `DO0 .. DO7` | `BOOL` | 8 ngõ ra số vật lý rơ-le / transistor (Relay/Transistor Outputs) |
| `16 .. 19` | `AI0 .. AI3` | `INT32` (12-bit ADC) | 4 ngõ vào tương tự công nghiệp (0-10V hoặc 4-20mA) |
| `20 .. 51` | `VFLAG0 .. VFLAG31` | `BOOL` | 32 cờ nhớ ảo nội suy logic (Internal Logic Flags) |
| `52 .. 83` | `VREG0 .. VREG31` | `INT32` | 32 thanh ghi biến RAM tạm thời (Mất dữ liệu khi mất nguồn) |
| `84 .. 115` | `VREG_RETAIN0 .. VREG_RETAIN31` | `INT32` | 32 thanh ghi lưu trữ Flash Non-Volatile (Lưu giữ bền vững) |
| `116 .. 123` | `COUNTER0 .. COUNTER7` | `INT32` | 8 thanh ghi bộ đếm xung sự kiện phần cứng |

#### 2. Bản Đồ Thanh Ghi Modbus Chuẩn Hóa (Modbus Holding Registers Map)

```text
BẢN ĐỒ BỘ NHỚ THANH GHI MODBUS (HOLDING REGISTERS - 4XXXX)
┌───────────────────┬─────────────────────┬────────────────────────────────────────────────────────┐
│ Vùng Địa Chỉ      │ Tên Định Danh       │ Công Năng & Ý Nghĩa Chi Tiết                           │
├───────────────────┼─────────────────────┼────────────────────────────────────────────────────────┤
│ 0x0000 .. 0x007F  │ TAG_CONFIG          │ Cấu hình thuộc tính Tag, Scaling Analog, Invert Logic  │
│ 0x0100 .. 0x073F  │ ACTIVE_RULE_TABLE   │ Bảng luật thực thi đang chạy trong RAM MCU (100 rules) │
│ 0x0800 .. 0x081F  │ SYSTEM_TELEMETRY    │ Nhịp tim MCU, chu kỳ scan (us), CPU load, Uptime (s)   │
│ 0x0900 .. 0x09F7  │ RUNTIME_TAG_VALUES  │ Giá trị 32-bit thực của 124 Tag (2 thanh ghi / 1 Tag)  │
│ 0x0A20 .. 0x0A24  │ DIAG_CONTROL        │ Cổng cưỡng bức & Thuê quyền chẩn đoán Diagnostic Lease │
│ 0x9000            │ CONFIG_STATUS       │ Trạng thái nạp: 0:IDLE, 1:RECEIVING, 2:VERIFYING,      │
│                   │                     │                 3:READY, 4:ERROR                       │
│ 0x9001            │ CONFIG_ERROR_CODE   │ Mã lỗi nạp (0x0000: Không lỗi, 0x0006: CRC Mismatch...)│
│ 0x9002            │ RULE_COUNT_STAGED   │ Số lượng luật ghi trong vùng đệm Staging               │
│ 0x9003            │ EXPECTED_CRC16      │ Checksum CRC-16 Modbus của toàn bộ Payload Staging     │
│ 0x9004            │ ACTIVE_RULE_COUNT   │ Số lượng luật hiện hành đang chạy                      │
│ 0x9005            │ ACTIVE_RULE_CRC16   │ Checksum CRC-16 của bảng luật hiện hành                │
│ 0x9010 .. 0x964F  │ STAGING_RULE_TABLE  │ Vùng nhớ đệm tạm thời cho bảng luật nạp mới (Max 100r) │
│ 0xA000            │ COMMIT_COMMAND      │ Thanh ghi nhận lệnh cam kết nguyên tử (Ghi: 0xA5A5)    │
│ 0xA001            │ ACTIVE_VERSION      │ Phiên bản cấu hình logic hiện hành trên vi điều khiển  │
└───────────────────┴─────────────────────┴────────────────────────────────────────────────────────┘
```

- **Công Thức Tính Địa Chỉ Modbus Cho Tag ID $k$**:
  $$\text{Modbus Address} = 0x0900 + (k \times 2)$$
  MCU trả về 2 thanh ghi liên tiếp (High Word và Low Word) theo định dạng Big-Endian 32-bit signed integer.

---

### 2.5 Cổng Nạp An Toàn Deploy Safety Gate & Cơ Chế Fail-Safe
Tọa lạc tại `SimplePLC.Studio/ViewModels/DeploymentGateViewModel.cs` và `src/SimplePLC.Infrastructure/Devices/RuleTableWriter.cs`.

- **Tầng truyền thông hiện hành**: Toàn bộ quy trình nạp cấu hình Staging và lệnh cam kết nguyên tử hiện tại được thực thi qua **Modbus RTU trên cổng nối tiếp Serial COM (USB UART / USB-CDC Virtual COM)** thông qua `src/SimplePLC.Infrastructure/Transport/ModbusRtuClient.cs`. Giao thức Modbus TCP (đóng gói MBAP Header 7 bytes qua Ethernet / Wi-Fi) là mục tiêu phát triển thuộc lộ trình chiến lược (Phase 3).

```text
QUY TRÌNH NẠP LUẬT 4 BƯỚC KHÔNG GIÁN ĐOẠN (ZERO-DOWNTIME ATOMIC DEPLOYMENT)
[Host Studio IDE]                                                [Target MCU STM32]
       │                                                                  │
       ├─ [BƯỚC 1: PRE-FLIGHT VALIDATION] ────────────────────────────────┤
       │   - Kiểm tra cú pháp GraphGrammarV1                              │
       │   - Kiểm tra xung đột ghi WriteConflictValidator                │
       │   - Tính toán mã CRC-16 trên mảng nhị phân                       │
       │                                                                  │
       ├─ [BƯỚC 2: STREAMING STAGING BUFFER] ────────────────────────────►│
       │   - Ghi RULE_COUNT_STAGED vào 0x9002                             │ MCU lưu dữ liệu vào
       │   - Ghi EXPECTED_CRC16 vào 0x9003                                │ RAM đệm STAGING_TABLE
       │   - Stream từng gói 32-byte rules vào 0x9010..                   │ (0x9010..0x964F)
       │                                                                  │
       ├─ [BƯỚC 3: ATOMIC COMMIT COMMAND] ───────────────────────────────►│
       │   - Ghi mã khóa bảo vệ 0xA5A5 vào thanh ghi 0xA000               │ MCU khóa ngắt logic ~1ms.
       │                                                                  │ Tự tính CRC16 vùng đệm.
       │                                                                  │ So khớp EXPECTED_CRC16:
       │                                                                  │ ├─ NẾU HỢP LỆ:
       │                                                                  │ │  Pointer Swap Staging->Active
       │                                                                  │ │  Tăng ACTIVE_VERSION (0xA001)
       │                                                                  │ │  CONFIG_STATUS (0x9000) = 3 (READY)
       │                                                                  │ │  CONFIG_ERROR_CODE (0x9001) = 0x0000
       │                                                                  │ └─ NẾU SAI CRC16:
       │                                                                  │    Từ chối commit! Giữ Active cũ.
       │                                                                  │    CONFIG_STATUS (0x9000) = 4 (ERROR)
       │                                                                  │    CONFIG_ERROR_CODE (0x9001) = 0x0006
       │                                                                  │
       ├─ [BƯỚC 4: VERIFICATION & AUDIT CONFIRM] ◄────────────────────────┤
       │   - Đọc 2 thanh ghi từ 0x9000: CONFIG_STATUS, CONFIG_ERROR_CODE  │
       │   - Xác nhận CONFIG_STATUS == 3 (READY), ERROR_CODE == 0x0000    │
       │   - Đọc ACTIVE_VERSION (0xA001) tăng lên 1 đơn vị                │
       │   - Hoàn tất nạp an toàn, ghi nhận Audit Log.                    │
```

#### 1. Cam Kết Nguyên Tử (Atomic Commit `0xA000 = 0xA5A5`)
- Tuyệt đối không bao giờ ghi trực tiếp bảng luật mới vào vùng `ACTIVE_RULE_TABLE` (`0x0100`).
- Toàn bộ dữ liệu được stream an toàn vào vùng đệm `STAGING_RULE_TABLE` (`0x9010`).
- Sau khi stream xong, Host ghi mã kiểm tra CRC-16 Modbus vào `0x9003` và phát lệnh cam kết nguyên tử: ghi giá trị bảo vệ `0xA5A5` vào thanh ghi `0xA000` (`CommitCommandAddress`).
- **Hành vi trên MCU & Quản lý Mã Trạng Thái**:
  - MCU tạm khóa đánh giá logic trong đúng 1 chu kỳ scan (~1ms), tự tính toán lại CRC-16 trên toàn bộ vùng đệm Staging `0x9010`.
  - **Mã Trạng Thái Nạp (`CONFIG_STATUS` tại `0x9000` - `ConfigStatusAddress`)**:
    * `0`: `IDLE` (Hệ thống sẵn sàng ở trạng thái rỗi)
    * `1`: `RECEIVING` (Đang tiếp nhận các khối luật nhị phân vào Staging)
    * `2`: `VERIFYING` (Đang tính toán kiểm tra mã toàn vẹn CRC-16)
    * `3`: `READY` (Nạp thành công, cấu hình đã cam kết an toàn sang Active)
    * `4`: `ERROR` (Quá trình nạp thất bại, từ chối commit)
  - **Mã Lỗi Nạp (`CONFIG_ERROR_CODE` tại `0x9001` - `ConfigErrorCodeAddress`)**:
    * `0x0000`: `NONE` (Không có lỗi)
    * `0x0006`: `CRC_MISMATCH` (Mã CRC-16 tính toán không khớp với `ExpectedCrc16Address` 0x9003)
  - Nếu khớp $100\%$, MCU thực hiện hoán đổi con trỏ bộ nhớ (pointer swap), tăng `ACTIVE_VERSION` (`0xA001`) thêm 1 đơn vị, ghi nhận `CONFIG_STATUS (0x9000) = 3 (READY)` và `CONFIG_ERROR_CODE (0x9001) = 0x0000`.
  - Nếu sai lệch dù chỉ 1 bit, lệnh nạp bị hủy bỏ ngay lập tức, MCU giữ nguyên trạng bảng luật cũ đang chạy và báo lỗi `CONFIG_STATUS (0x9000) = 4 (ERROR)` kèm `CONFIG_ERROR_CODE (0x9001) = 0x0006 (CRC_MISMATCH)`. Host đọc 2 thanh ghi liên tiếp từ `0x9000` (`RuleTableWriter.cs:132-158`) để xác thực an toàn tuyệt đối.

#### 2. Cơ Chế Thuê Quyền Chẩn Đoán & Ngắt Fail-Safe (Diagnostic Watchdog Lease)
Tại `src/SimplePLC.Application/Services/DiagnosticControlService.cs`:
- Để can thiệp cưỡng bức ngõ ra (Force I/O), kỹ sư phải kích hoạt phiên `DIAG_CONTROL` với thời hạn thuê (Lease time):
  $$\text{DiagDefaultLeaseMs} = 3000\text{ ms (Watchdog 3 giây)}$$
- Host IDE duy trì việc gửi gói tin nhịp tim (heartbeat) định kỳ mỗi $1000\text{ ms}$.
- **Khóa An Toàn Phần Mềm (Software Interlock)**: Khi ở trạng thái `ENGINE_RUNNING`, lệnh ghi cưỡng bức ngõ ra bị firmware MCU chặn cứng. Khi kích hoạt `DIAG_CONTROL`, Rule Engine trên MCU tạm dừng đánh giá logic để tránh xung đột quyền ghi.
- **Tự Động Phục Hồi An Toàn (Auto-Revert Fail-Safe)**: Nếu cáp nối RS-485 bị đứt hoặc Host IDE bị treo/tắt đột ngột quá $3000\text{ ms}$, MCU tự động kích hoạt ngắt Watchdog:
  1. Lập tức reset toàn bộ ngõ ra số `DO0..DO7 = 0` (Fail-Safe Off) để bảo vệ động cơ và van nhà máy.
  2. Tự động thu hồi quyền `DIAG_CONTROL`, phục hồi MCU về trạng thái `ENGINE_RUNNING` với bảng luật Active ban đầu.

#### 3. Quản Lý Biến Bền Vững (Retentive Dirty Tracking)
Khi sửa đổi các thanh ghi `VREG_RETAIN`:
- MCU đánh dấu cờ `RETAIN_DIRTY = 1`.
- Dữ liệu chưa ghi ngay vào Flash để tránh làm chai bộ nhớ Flash (Wear-Leveling).
- Người dùng nhấn nút xác nhận trên giao diện Deploy Gate, Host mới phát lệnh `COMMIT_RETAIN`, MCU mới thực hiện ghi Flash Sector nguyên khối kèm mã kiểm tra tính toàn vẹn.

---

### 2.6 Bộ Giả Lập MCU Hardware Simulator & Kiểm Thử Phần Cứng Vòng Lặp COM
Tọa lạc tại `src/SimplePLC.Infrastructure/Simulator/McuReferenceSimulator.cs` và `tools/SimplePLC.HardwareTest/Program.cs`.

#### 1. Kiến Trúc Bộ Giả Lập Tham Chiếu C# (`McuReferenceSimulator.cs`)
Đóng vai trò là bản sao tham chiếu (Reference Implementation) chuẩn xác $100\%$ về mặt hành vi của firmware nhúng STM32. Vòng lặp quét chu kỳ 10-20ms (`ExecuteScanPass`) diễn ra qua 5 bước nghiêm ngặt:
1. **Input Scan**: Đọc trạng thái chân vào từ mảng DI/AI mô phỏng.
2. **Logic Evaluation**: Duyệt tuần tự các luật trong `_activeRuleTable`, đánh giá Trigger sườn xung và biểu thức Guard.
3. **Action Execution**: Cập nhật trạng thái ngõ ra nội bộ và cập nhật các bộ đếm `COUNTER`.
4. **Output Update**: Đẩy trạng thái ra mảng DO vật lý mô phỏng.
5. **Telemetry Update**: Cập nhật số chu kỳ scan (`ScanCount`), thời gian thực thi scan thực tế (`ScanTimeUs`), bộ đếm thời gian hoạt động (`UptimeSeconds`).

#### 2. Cổng Nối Tiếp Ảo & Cầu Ghép Nối Loopback
- `McuModbusRtuServer.cs` bọc `McuReferenceSimulator` và mở một listener Modbus RTU Slave (địa chỉ Slave ID = 1) lắng nghe trên cổng Serial vật lý hoặc cổng ảo.
- Hỗ trợ mô hình loopback ghép cặp thông qua phần mềm cổng ảo (như com0com): ví dụ ghép cặp **COM5 $\longleftrightarrow$ COM10**.

#### 3. Bộ Kịch Bản Kiểm Thử Phần Cứng Tự Động 7 Bước (`SimplePLC.HardwareTest`)
Trong `tools/SimplePLC.HardwareTest/Program.cs`, bộ kiểm thử chạy tự động qua 7 bước:
- **Bước 1 - Handshake**: Đọc thanh ghi telemetry `0x0800`, xác thực chuỗi nhận dạng firmware và phiên bản giao thức.
- **Bước 2 - Resource Profile**: Đọc bản đồ kích thước bộ nhớ (số lượng DI/DO/AI/VFLAG/VREG hỗ trợ).
- **Bước 3 - Telemetry Loop**: Đọc liên tục 10 chu kỳ telemetry, xác thực chu kỳ scan dao động chuẩn trong ngưỡng 10ms - 20ms.
- **Bước 4 - Function Blocks V2**: Kiểm thử tính toán của các khối Timer (TON/TOF/TP) và Counter (CTU/CTD) trực tiếp trên MCU.
- **Bước 5 - Atomic Deployment**: Tạo bảng luật giả lập gồm 5 rules $\rightarrow$ Nạp vào Staging `0x9010` $\rightarrow$ Ghi CRC16 $\rightarrow$ Phát lệnh Commit `0xA5A5` $\rightarrow$ Kiểm tra phiên bản `0xA001` tăng lên $\rightarrow$ Đọc kiểm tra nội dung bảng Active `0x0100`.
- **Bước 6 - Diagnostic Interlock**: Bật cờ `DIAG_CONTROL` với Lease 3000ms $\rightarrow$ Cưỡng bức `DO0 = 1` $\rightarrow$ Kiểm tra `DO0` thực sự bật $\rightarrow$ Chờ 3500ms không gửi nhịp tim $\rightarrow$ Xác thực Watchdog đã tự động thu hồi quyền và reset `DO0 = 0`.
- **Bước 7 - RTC Clock Sync**: Đồng bộ thời gian thực từ Host xuống thanh ghi đồng hồ RTC của MCU.

---

## 3. ĐỐI SÁNH KHOẢNG CÁCH TÍNH NĂNG VỚI CÁC NỀN TẢNG ĐẦU NGÀNH

Phần này phân tích chi tiết khoảng cách tính năng kỹ thuật giữa SimplePLC Studio và ba nền tảng tự động hóa công nghiệp hàng đầu thế giới: **Siemens TIA Portal V18/V19**, **CODESYS V3.5 SP19/SP20**, và **Beckhoff TwinCAT 3.1**.

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                      MA TRẬN ĐỐI SÁNH KHOẢNG CÁCH CÔNG NGHỆ                            │
├──────────────────────┬──────────────────────┬──────────────────────┬───────────────────┤
│ Tiêu Chí             │ Siemens TIA Portal   │ CODESYS / TwinCAT 3  │ SimplePLC Hiện Tại│
├──────────────────────┼──────────────────────┼──────────────────────┼───────────────────┤
│ 1. Khối Chức Năng    │ UDFB + Instance DB   │ OOP Function Blocks  │ Flat Hardcoded    │
│    (Custom UDFB)     │ Multi-Instance Call  │ Methods & Properties │ Macros (Chưa UDFB)│
├──────────────────────┼──────────────────────┼──────────────────────┼───────────────────┤
│ 2. Linter Trực Quan  │ Squiggles trực tiếp  │ Squiggles + Inline   │ Chỉ báo Text ở    │
│    (Canvas Linter)   │ Cảnh báo chân trống  │ Type Check Adorners  │ Inspector bên phải│
├──────────────────────┼──────────────────────┼──────────────────────┼───────────────────┤
│ 3. So Sánh Đồ Họa    │ Project Compare      │ Native Git Plugin    │ Snapshot .splc    │
│    (Graph Diff VCS)  │ Side-by-Side Merge   │ Graphical CFC Diff   │ Chưa có Git / Diff│
├──────────────────────┼──────────────────────┼──────────────────────┼───────────────────┤
│ 4. Quản Lý Cảnh Báo  │ ProDiag / WinCC      │ Alarm Manager        │ Chưa có hệ thống  │
│    (Alarms ISA-18.2) │ Banner đỉnh màn hình │ Alarm Groups / State │ Cảnh báo chuẩn hóa│
└──────────────────────┴──────────────────────┴──────────────────────┴───────────────────┘
```

### 3.1 Khối Chức Năng Tự Định Nghĩa (Custom UDFB)

#### 1. Chuẩn mực Ngành (Authoritative Benchmarks)
- **Tiêu chuẩn IEC 61131-3 (3rd Edition)**: Phân định rạch ròi giữa:
  - **Function (FC)**: Khối hàm thuần túy không lưu trạng thái (Stateless). Cùng tham số đầu vào luôn cho cùng kết quả. Bộ nhớ cấp phát trên Stack (`VAR_TEMP`) và tự giải phóng khi kết thúc lời gọi.
  - **Function Block (FB)**: Khối chức năng có trạng thái (Stateful). Đòi hỏi bộ nhớ tĩnh liên kết để duy trì trạng thái nội bộ (`VAR`), cờ chốt, thời gian trôi qua giữa các chu kỳ quét (Scan-to-Scan memory persistence).
- **Siemens TIA Portal**: Khối FB gắn liền với **Instance Data Block (Instance DB)** hoặc **Multi-Instance DB** (khai báo lồng nhau trong Instance DB của khối cha). Hỗ trợ phân định rõ ràng các phạm vi biến: `VAR_INPUT`, `VAR_OUTPUT`, `VAR_IN_OUT`, `VAR_STATIC`, `VAR_TEMP`. Hỗ trợ đóng gói thư viện toàn cục (Global Library) và khóa bảo vệ bản quyền (Know-How Protection).
- **CODESYS & TwinCAT 3**: Hỗ trợ lập trình hướng đối tượng OOP công nghiệp (`FUNCTION_BLOCK ... IMPLEMENTS ...`), cấp phát thực thể linh hoạt trong bộ nhớ, hỗ trợ phương thức (`METHOD`), thuộc tính (`PROPERTY`) và giao diện (`INTERFACE`).

#### 2. Khoảng Cách Kiến Trúc Của SimplePLC Studio
- SimplePLC hiện **chưa có** khái niệm Function Block tự định nghĩa.
- Toàn bộ logic hiện tại là **đồ thị phẳng (Flat Graph)**. Các khối phức hợp duy nhất chỉ là 3 Macro cứng cố định: `TimerNodeViewModel`, `CounterNodeViewModel`, `ScaleNodeViewModel`.
- Khi biên dịch, các Macro này bị phân rã cưỡng bức thành các luật 32-byte ghi thẳng vào các tag toàn cục phẳng (`VFLAG`, `VREG`).
- Kỹ sư không thể đóng gói một cụm 10-20 khối thành một Sub-graph tái sử dụng; khi sơ đồ có nhiều cụm động cơ/bơm giống nhau, số lượng dây nối trên canvas tăng vọt gây hiện tượng "Spaghetti Wiring".

#### 3. Thiết Kế Giải Pháp Chuẩn Hóa Kiến Trúc Cho SimplePLC
- **Mô hình Sub-graph Encapsulation**: Xây dựng đối tượng `UdfbDefinitionModel` chứa một đồ thị FBD con hoàn chỉnh độc lập.
- **Giao diện Chân Cắm (Pin Scoping)**: Cho phép định nghĩa danh mục chân cắm ngõ vào (`InPins`), ngõ ra (`OutPins`), và chân tham chiếu (`InOutPins`).
- **Bộ Quản Lý Vùng Nhớ Thực Thể (Instance Memory Offset Table)**: Khi kỹ sư kéo thả một UDFB lên Canvas chính, hệ thống tự động cấp phát một dải bộ nhớ nội bộ riêng biệt (ví dụ: gán từ dải `VREG_INSTANCE_OFFSET` và `VFLAG_INSTANCE_OFFSET`), cách ly hoàn toàn trạng thái giữa các thực thể động cơ khác nhau mà không làm xung đột biến.

---

### 3.2 Kiểm Tra Lỗi Cú Pháp & Cảnh Báo Trực Quan Trên Canvas (Real-time Linter Squiggles)

#### 1. Chuẩn mực Ngành (Authoritative Benchmarks)
- **Nguyên lý Cognitive Proximity (Cận thị Nhận thức)**: Trong công thái học IDE hiện đại, thông báo lỗi phải xuất hiện ngay tại vị trí trỏ chuột và chân linh kiện gặp sự cố; thời gian phản hồi cú pháp (Diagnostic Latency) phải dưới $150\text{ ms}$ thông qua luồng chạy nền (Background AST Linting) không gây đơ UI.
- **TIA Portal & TwinCAT 3**:
  - Chân bắt buộc chưa nối dây hiển thị dấu hỏi đỏ `???` hoặc viền đỏ nhấp nháy.
  - Nối dây sai kiểu dữ liệu (ví dụ nối tín hiệu Analog mV vào chân Boolean) hiển thị gạch sóng màu cam cảnh báo lệch kiểu dữ liệu (Type Mismatch Squiggle).
  - Cảnh báo xung đột ghi (Write Conflict / Double Coil) phát hiện 2 cuộn dây cùng ghi một ngõ ra trong cùng chu kỳ OB1.
  - Chu trình dữ liệu kín (Cyclic Dependency) làm đường dây nối phát sáng màu đỏ cảnh báo vòng lặp đệ quy.

#### 2. Khoảng Cách Kiến Trúc Của SimplePLC Studio
- **Nghịch lý hiện tại**: SimplePLC đã sở hữu bộ phân tích tĩnh cực mạnh trong tầng Application (`GraphGrammarV1`, `WriteConflictValidator`, `RuleDependencyGraph`), nhưng **trải nghiệm hiển thị chẩn đoán (Diagnostic UI/UX) lại bị đứt gãy hoàn toàn**:
  - Danh sách chẩn đoán `Diagnostics` chỉ hiển thị dưới dạng văn bản Text thuần túy bên trong Drawer Inspector ở cạnh phải (`<ItemsControl ItemsSource="{Binding Diagnostics}" Margin="0,6,0,0">`).
  - Trên Nodify Canvas: Hoàn toàn **không có gạch sóng (Squiggles)** màu đỏ/vàng dưới chân node; **không có viền cảnh báo lỗi (Error Border)** trên khối Node; **không có biểu tượng chấm than (!) hoặc Tooltip giải thích lỗi** khi di chuột vào chân cắm.
  - Kỹ sư chỉ biết sơ đồ bị lỗi khi nhìn sang bảng thuộc tính bên phải thấy dòng chữ đỏ thông báo lỗi, rất khó định vị node nào đang bị sai trong đồ thị lớn.

#### 3. Thiết Kế Giải Pháp Chuẩn Hóa Kiến Trúc Cho SimplePLC
- Bổ sung thuộc tính `HasError`, `HasWarning`, `ErrorTooltip` vào `GraphNodeViewModel` và `ConnectorViewModel`.
- Tùy biến DataTemplate của Nodify:
  - Khi node bị lỗi cú pháp hoặc xung đột ghi: Viền khối đổi sang màu đỏ rực `#DC2626` kèm hiệu ứng viền phát sáng nhẹ.
  - Khi chân cắm bắt buộc chưa có dây nối: Hiển thị chấm tròn cảnh báo nhấp nháy và gạch chân lượn sóng màu cam dưới tên chân.
  - Khi đường dây tham gia vào chu trình lặp kín (`RuleDependencyGraph` cycle): Đổi màu nét vẽ dây thành màu đỏ tươi kèm tooltip: *"SPLC-COMP-DEP-001: Phát hiện vòng lặp phụ thuộc chu kỳ quét"*.

---

### 3.3 So Sánh Sai Khác Đồ Họa & Quản Lý Phiên Bản (Visual Graph Diff & VCS)

#### 1. Chuẩn mực Ngành (Authoritative Benchmarks)
- **Siemens TIA Portal Project Compare & VCI**: Hỗ trợ so sánh trực quan 2 dự án (Online/Offline hoặc Offline/Offline) ở cấp độ mạng đồ họa (Network Comparison). Giao diện 2 cửa sổ song song (Side-by-Side Split View) với bảng màu quy chuẩn:
  - Màu xanh lá cây (Green): Phần tử mới thêm vào.
  - Màu đỏ (Red): Phần tử bị xóa bỏ.
  - Màu vàng/xanh dương (Yellow/Blue): Phần tử bị thay đổi thuộc tính hoặc đấu lại dây.
  - Cho phép chọn chấp nhận từng Network ("Take from Left" / "Take from Right").
- **CODESYS Git & TwinCAT TcXaeCompare**: Tích hợp Git native vào cây dự án; công cụ so sánh đồ họa nhận diện semantic diff của sơ đồ (không so sánh XML thô mà so sánh cấu trúc đồ thị: thêm khối, xóa khối, đổi tham số, định tuyến lại dây).

#### 2. Khoảng Cách Kiến Trúc Của SimplePLC Studio
- Dự án SimplePLC hiện được tuần tự hóa thành **một tệp JSON duy nhất (`.splc`)** ghi đè toàn bộ dự án.
- Hệ thống chỉ có bộ nhớ Undo/Redo tạm thời trong phiên làm việc (`GraphHistoryService`) lưu trong RAM, mất toàn bộ khi đóng phần mềm.
- Hoàn toàn **chưa có tích hợp Git VCS**: Không có tính năng tạo commit, chuyển nhánh, hoặc theo dõi lịch sử.
- Hoàn toàn **chưa có Visual Graph Diff**: Không có thuật toán Semantic Graph Diffing để đối chiếu sự thay đổi giữa các phiên bản đồ thị, không có giao diện xem trước sự sai khác giữa dự án offline và chương trình đang chạy trên PLC.

#### 3. Thiết Kế Giải Pháp Chuẩn Hóa Kiến Trúc Cho SimplePLC
- Xây dựng thuật toán so sánh đồ thị ngữ nghĩa `GraphDiffEngine`: So sánh đồ thị theo 4 chiều:
  $$\Delta_{\text{Graph}} = \{\text{Nodes}_{\text{Added}}, \text{Nodes}_{\text{Removed}}, \text{Nodes}_{\text{Modified}}, \text{Edges}_{\text{Rewired}}\}$$
- Hỗ trợ chế độ hiển thị Ghost Overlay Diff hoặc Side-by-Side View trên Nodify Canvas.
- Tích hợp Git native (thông qua LibGit2Sharp) với các lệnh Commit, Branch, Merge trực tiếp từ thanh trạng thái IDE.

---

### 3.4 Hệ Thống Quản Lý Cảnh Báo & Sự Kiện (Alarms & Events Banner ISA-18.2 / IEC 62682)

#### 1. Chuẩn mực Ngành (Authoritative Benchmarks)
- **Tiêu chuẩn ANSI/ISA-18.2-2016 & IEC 62682**:
  - **Máy trạng thái Vòng đời Cảnh báo (Alarm State Machine)**:
    - `Normal`: Bình thường, không có sự cố.
    - `UNACK` (Unacknowledged): Cảnh báo xuất hiện, đèn/còi nhấp nháy, người vận hành chưa bấm xác nhận.
    - `ACK` (Acknowledged): Cảnh báo còn tồn tại, nhưng người vận hành đã bấm xác nhận; còi tắt, đèn chuyển sáng liên tục.
    - `RTN_UNACK` (Return to Normal Unacknowledged): Sự cố đã tự hết trước khi kịp bấm xác nhận; đèn nhấp nháy chậm báo hiệu đã từng có sự cố thoáng qua.
    - `Shelved / Suppressed`: Cảnh báo bị tạm gác trong khoảng thời gian có hạn (Shelving timer) để bảo trì, chống tràn chuông báo (Alarm Flooding).
  - **Phân cấp Mức độ Ưu tiên (Priority Levels)**:
    - *Priority 1: Emergency / Critical* (`#DC2626` Đỏ nhấp nháy): Đe dọa an toàn tính mạng, nổ vỡ bồn áp suất, E-Stop kích hoạt.
    - *Priority 2: High* (`#EA580C` Cam): Nguy cơ dừng dây chuyền, quá nhiệt động cơ chính.
    - *Priority 3: Medium* (`#CA8A04` Vàng): Sai lệch quy trình, cảm biến chạm ngưỡng cảnh báo sớm.
    - *Priority 4: Low* (`#2563EB` Xanh dương): Thông báo hoàn thành mẻ, nhắc nhở bảo trì định kỳ.
  - **Banner Cảnh báo Đỉnh Màn hình (High-Priority Alarm Banner)**: Nằm cố định ở dải trên cùng của IDE, không bao giờ bị che khuất; luôn hiển thị cảnh báo chưa xác nhận có mức độ ưu tiên cao nhất, kèm nút **[Xác nhận / ACK]** và **[Tắt chuông / Silence]**.
  - **Nhật ký Sự kiện & Audit Trail (SOE - Sequence of Events)**: Lưu vết sự kiện có nhãn thời gian độ phân giải mili-giây phục vụ điều tra sự cố.

#### 2. Khoảng Cách Kiến Trúc Của SimplePLC Studio
- SimplePLC hiện **hoàn toàn thiếu** hệ thống Alarms & Events Management.
- Bảng giám sát `LiveWatchView` chỉ hiển thị danh sách biến đo lường thô, không có định nghĩa ngưỡng cảnh báo (High-High, High, Low, Low-Low, Deadband).
- Không có Banner cảnh báo toàn cục cố định trên giao diện; kỹ sư chuyển sang tab khác sẽ không biết hệ thống đang gặp sự cố.
- Không có cơ chế lưu trữ nhật ký biến cố cảnh báo (Alarm Log / SOE) lưu vào tệp hoặc cơ sở dữ liệu.

#### 3. Thiết Kế Giải Pháp Chuẩn Hóa Kiến Trúc Cho SimplePLC
- Xây dựng dịch vụ `AlarmEngineService` quản lý máy trạng thái ISA-18.2 cho toàn bộ các Tag.
- Thêm thành phần `GlobalAlarmBannerControl` gắn cố định tại đỉnh `MainWindow.xaml`, hiển thị dòng thông báo sự cố khẩn cấp nhất kèm nút [Xác nhận / ACK].
- Xây dựng cơ sở dữ liệu nhật ký sự kiện `AlarmAuditLogger` (bộ đệm vòng SQLite lưu trữ 10,000 sự kiện có nhãn thời gian mili-giây).

---

## 4. LỘ TRÌNH NÂNG CẤP HỆ THỐNG 3 GIAI ĐOẠN (ACTIONABLE ROADMAP)

Để đưa SimplePLC Studio đạt đẳng cấp công thái học tương đương Siemens TIA Portal và CODESYS, lộ trình kỹ thuật được phân kỳ thành 3 giai đoạn rõ ràng:

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                        LỘ TRÌNH NÂNG CẤP HỆ THỐNG SIMPLEPLC STUDIO                     │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ GIAI ĐOẠN 1: QUICK WINS (1 - 2 TUẦN)                                                   │
│ [1] Canvas Diagnostic Squiggles & Node Error Adorners (Viền đỏ/vàng trên Canvas)       │
│ [2] Global Alarm Banner ISA-18.2 cố định trên đỉnh MainWindow với nút ACK & Mute       │
│ [3] Nâng cấp System Prompt AI với đầy đủ tri thức an toàn công nghiệp (Fail-Safe)      │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ GIAI ĐOẠN 2: MID-TERM (1 - 3 THÁNG)                                                    │
│ [1] Gemini / OpenAI Tool Calling Engine (Bộ 8 Function Calling JSON Schemas chuẩn)     │
│ [2] Staging Draft Transaction & Ghost Nodes/Wires Preview trên Canvas FBD              │
│ [3] Khối User-Defined Function Blocks (UDFB) sơ cấp: Đóng gói Sub-graph V1             │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ GIAI ĐOẠN 3: STRATEGIC (3 - 6 THÁNG)                                                   │
│ [1] Semantic Visual Graph Diff & Tích hợp Git VCS (So sánh & Merge 2 nhánh trực quan)  │
│ [2] UDFB hoàn chỉnh với Instance Data Blocks & Thư viện đóng gói Package Manager       │
│ [3] Hệ thống Quản lý Cảnh báo Nâng cao (Alarm Shelving, SOE SQLite Database)           │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

### 4.1 Giai Đoạn 1: Quick Wins (Triển khai ngay: 1 - 2 tuần)
*Mục tiêu: Xử lý các điểm nghẽn trải nghiệm hiển thị và an toàn tức thì với chi phí kỹ thuật thấp.*

1. **Trực Quan Hóa Lỗi Biên Dịch Trên Canvas (Canvas Diagnostic Visuals)**:
   - Thêm thuộc tính `HasError`, `HasWarning`, `ErrorTooltip` vào `GraphNodeViewModel` và `ConnectorViewModel`.
   - Cập nhật DataTemplate của Nodify trong `NodeTemplates.xaml`: Hiển thị viền đỏ phát sáng `#DC2626` quanh các node bị vi phạm cú pháp hoặc xung đột ghi; hiển thị gạch sóng đỏ nhấp nháy dưới các chân cắm bắt buộc chưa nối dây.
   - Hiển thị Tooltip giải thích chi tiết mã lỗi (`SPLC-GRAPH-007`, `SPLC-COMP-DEP-002`) ngay khi di chuột vào khối.
2. **Dải Banner Cảnh Báo Toàn Cục ISA-18.2 (Global High-Priority Alarm Banner)**:
   - Thêm component `AlarmBannerControl` tại hàng trên cùng của `MainWindow.xaml`.
   - Kết nối trực tiếp với trạng thái mất kết nối COM/Modbus hoặc cảnh báo vượt ngưỡng khẩn cấp, tích hợp nút **[ACK]** và nút **[Mute]**.
3. **Nâng Cấp Tri Thức An Toàn Trong System Prompt Cho AI**:
   - Cập nhật `AiPromptBuilder.BuildSystemInstruction()` với bộ quy tắc an toàn công nghiệp: Tiếp điểm nút Stop/E-Stop bắt buộc dùng thường đóng NC (logic ngắt sườn xuống hoặc mức 0), mạch tự giữ động cơ bắt buộc ưu tiên Reset (Dominant-Reset), và tự động chèn thời gian lọc dội phím cơ khí $30 - 50\text{ ms}$.

### 4.2 Giai Đoạn 2: Mid-Term (Trung hạn: 1 - 3 tháng)
*Mục tiêu: Đưa trợ lý AI lên chuẩn Tool Calling và nâng cấp tính năng module hóa logic.*

1. **Chuyển Đổi AI Sang Cơ Chế Tool Calling Hai Chiều (Bidirectional Tool Calling)**:
   - Nâng cấp `GeminiApiClient` hỗ trợ `tools: [{ functionDeclarations: [...] }]` chuẩn Google Gemini & OpenAI Strict Mode.
   - Tích hợp bộ 8 JSON Schemas chuẩn (`add_node`, `remove_node`, `connect_wires`, `disconnect_wire`, `configure_timer_counter`, `rename_tag`, `auto_layout_graph`, `diagnose_logic_bugs`).
2. **Cơ Chế Xem Trước Thay Đổi Dưới Dạng Ghost Elements (Visual Diff Preview)**:
   - Xây dựng `CanvasGhostRenderer` vẽ các node đề xuất với viền nét đứt màu xanh ngọc `#00A389` và độ mờ `Opacity = 0.8`.
   - Bổ sung thanh điều khiển nổi trên Canvas: `[✓ Chấp nhận (Accept)]` và `[✕ Hủy bỏ (Reject)]`, tích hợp cơ chế khôi phục qua `GraphHistoryService` (Undo snapshot).
3. **Đóng Gói Khối Chức Năng Tự Định Nghĩa (UDFB Sub-graph V1)**:
   - Cho phép người dùng quét chọn một nhóm node và bấm `Ctrl + G` để nén thành một "Composite Function Block".
   - Tự động sinh giao diện chân cắm vào/ra và quản lý bảng nhớ nội bộ.

### 4.3 Giai Đoạn 3: Strategic (Chiến lược: 3 - 6 tháng)
*Mục tiêu: Hoàn thiện hệ sinh thái công nghiệp cấp cao ngang tầm TIA Portal và TwinCAT.*

1. **So Sánh Sai Khác Đồ Họa & Tích Hợp Git (Semantic Graph Diff & VCS)**:
   - Tách file lưu trữ `.splc` thành cấu trúc thư mục mở cho từng POU/FBD.
   - Xây dựng thuật toán so sánh đồ thị ngữ nghĩa `GraphDiffEngine` và giao diện Side-by-Side Visual Merge.
   - Tích hợp Git native vào IDE: Quản lý nhánh (Branching), Commit, và Push/Pull tới máy chủ Git công nghiệp.
2. **Hệ Thống UDFB Hoàn Chỉnh Với Instance Data Blocks & Library Packaging**:
   - Hỗ trợ Multi-Instance DB lồng nhau nhiều cấp.
   - Cơ chế đóng gói thư viện số (Package Manager) với tính năng bảo vệ bản quyền mã nguồn (Know-How Protection).
3. **Hệ Thống Quản Lý Cảnh Báo Chuẩn ISA-18.2 Toàn Diện**:
   - Bảng cấu hình Alarm Groups, tính toán ngưỡng High-High, High, Low, Low-Low kèm Deadband đo lường.
   - Cơ sở dữ liệu SQLite lưu trữ nhật ký biến cố Sequence of Events (SOE) có nhãn thời gian mili-giây và phân quyền kiểm toán người dùng (Audit Trail).

---

## 5. KẾT LUẬN & KIẾN NGHỊ HÀNH ĐỘNG

SimplePLC Studio (SynaptiX IDE v2.2.4) là một thành tựu kỹ thuật đáng ghi nhận trong lĩnh vực phần mềm điều khiển công nghiệp hiện đại:
- **Kiến trúc lõi rất vững chắc**: Clean Architecture, phân tách rành mạch các tầng Domain, Protocol, Application, và Presentation.
- **Tính an toàn tuyệt đối**: Quy trình nạp 4 bước với mã khóa nguyên tử `0xA5A5` và cơ chế Watchdog Lease 3000ms đạt chuẩn fail-safe công nghiệp cao nhất.
- **Nền tảng kiểm thử hoàn hảo**: Simulator tham chiếu C# tương thích 1:1 với firmware STM32 kết hợp kịch bản kiểm thử tự động loopback COM5 <-> COM10 7 bước đảm bảo độ tin cậy tuyệt đối trước khi triển khai thực tế.

Việc thực thi lộ trình nâng cấp 3 giai đoạn nêu trên sẽ đưa SimplePLC Studio từ một công cụ lập trình PLC tinh gọn vươn lên thành một nền tảng tự động hóa toàn diện, xóa nhòa khoảng cách công thái học với Siemens TIA Portal và CODESYS, đồng thời mở đường cho thế hệ AI Copilot tự hành đích thực trong công nghiệp sản xuất.

---
**Tài liệu tham chiếu liên quan**:
- `SPLC-SPEC-AI-NEXTGEN-001`: Bản đặc tả kỹ thuật Next-Gen Industrial AI Copilot.
- `SPLC-SPEC-MODBUS-MAP-001`: Đặc tả chi tiết bản đồ thanh ghi Modbus RTU/TCP SimplePLC.
- `SPLC-SPEC-DIAG-001`: Quy chuẩn điều phối chẩn đoán và quyền truy cập phần cứng an toàn.
