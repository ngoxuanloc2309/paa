

# SynaptiX IDE Engineering Walkthrough & Living Documentation

## 24. Tái Kiến Trúc Lazy Compact Knowledge Kernel & Triệt Tiêu Ảo Giác Tri Thức SynaptiX AI Copilot

### 24.1. Bối cảnh & Hiện Tượng Cần Khắc Phục
- **Hiện tượng 1 (Rò rỉ cú pháp Structured Text trong ConsultativeQnA):**
  - Khi kỹ sư hỏi: *"Tôi muốn tạo 1 rule đúng 8h sáng còi kêu"*, AI Copilot ở chế độ tư vấn do thiếu bản thể FBD đã truy xuất tri thức PLC truyền thống (Codesys, Siemens, Beckhoff) và trả lời bằng cú pháp Structured Text:
    ```pascal
    IF Clock.HH = 8 AND Clock.MM = 0 THEN DO_Buzzer := TRUE;
    IF DO_Buzzer = TRUE THEN T_off(IN:=TRUE, PT:=T#5s);
    ```
  - **Vi phạm kiến trúc:** SynaptiX IDE chạy thuần túy trên mô hình **Rule Pipeline 32-byte** và **Sơ đồ khối FBD Canvas**. Hệ thống **hoàn toàn không có** Structured Text, không có câu lệnh `IF...THEN`, không có đối tượng `Clock.HH`.
- **Hiện tượng 2 (Đưa System Tag sai ngữ cảnh trong FbdSynthesis):**
  - Khi người dùng yêu cầu *"sinh rule trên canva cho tôi"*, AI sinh 2 Rule hẹn giờ `TIME_WINDOW (800-800)` và `TIME_WINDOW (801-801)`.
  - Nhưng ở mục *"2. Danh mục Tag"*, AI tự ý nhét `SYS_RTC_HOUR, SYS_RTC_MINUTE, DO0 (Còi)`.
  - **Phân định Ground Truth:** Trong `SystemTagConstants`, SynaptiX **thực sự có 8 System Tags** (Index 1000..1007, gồm `SYS_RTC_HOUR` và `SYS_RTC_MINUTE`). Tuy nhiên, Trigger `TIME_WINDOW` hoạt động bằng cách vi điều khiển MCU tự đọc thanh ghi RTC phần cứng ngầm (`input_tag = "NONE"`). Trên Canvas FBD của sơ đồ này **hoàn toàn không có Node Input nào kết nối với `SYS_RTC_HOUR` hay `SYS_RTC_MINUTE`**. Việc đưa 2 tag này vào Danh mục Tag vi phạm **Axiom 2 — The Visual Truth (Single Source of Truth)**.
- **Hiện tượng 3 (Giải pháp còi báo ca ngắn hạn):**
  - `TIME_WINDOW` trong SynaptiX có độ phân giải theo Phút. Để còi chỉ kêu đúng 5 giây, phương án chuẩn công nghiệp là sử dụng `TIME_WINDOW (800-800)` kích hoạt khối Macro **`TP (Pulse Timer)`** với `PT = 5s`, ngõ ra `Q` đóng `DO0 (Còi)`.

---

### 24.2. Kiến Trúc Giải Pháp 3-Layer Prompt Stack & Lazy Compact Kernel

Đã tái cấu trúc `AiPromptBuilder.cs` theo mô hình 3 tầng (dựa trên kỹ thuật Structural Template Abstraction):

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                       3-LAYER PROMPT ARCHITECTURE                           │
├─────────────────────────────────────────────────────────────────────────────┤
│  LAYER 0: Base Compact Kernel (compile-time const, ~400 tokens)             │
│  - Ontological Boundary: NO ST, NO IF...THEN, NO Codesys POU                │
│  - 17-Node Taxonomy (Pipeline, Macro, IO/Comm)                              │
│  - Zero-Hallucination Closed-World Tag Catalog Contract                     │
│  - Industrial Pattern Library: 15 Archetypes (Start, Stop, RTC, TP, CMP...) │
├─────────────────────────────────────────────────────────────────────────────┤
│  LAYER 1: Intent Personality & Behavioral Directives                        │
│  - Consultative: Súc tích (100-150 từ), không JSON, không ST, chỉ giải thích│
│  - Diagnose: Safety audit, Linter, nhận diện TIME_WINDOW=NONE là hợp lệ     │
│  - Synthesis: 1 Input ➔ 1 Output, ASCII Box, Tag Catalog chuẩn, JSON Rules │
├─────────────────────────────────────────────────────────────────────────────┤
│  LAYER 2: Project Context Data (Tags & Rules CSV compression)               │
└─────────────────────────────────────────────────────────────────────────────┘
```

#### 1. Biên soạn Shared Kernel bất biến (`EnglishKernel` & `VietnameseKernel`)
- Khai báo dưới dạng `private const string` để compiler tối ưu hóa thành hằng số compile-time, zero allocation trên heap.
- Bao phủ đầy đủ 17 Node:
  - 4 Pipeline: `INPUT`, `TRIGGER`, `COMPARATOR`, `GUARD`, `ACTION`.
  - 2 Macro: `TIMER` (TON, TOF, TP, CYCLE), `COUNTER` (CTU, CTD với CV $\rightarrow$ `VREG_RETAIN`).
  - 11 IO/Comm: `SCALE`, `MATH`, `MODBUS_READ`, `MODBUS_WRITE`, `MQTT_PUBLISH`, `MQTT_SUBSCRIBE`, `DIRECT_WRITE`, `HTTP_CLIENT`, `TCP_CLIENT`.
- Thư viện 15 khuôn mẫu công nghiệp chuẩn, trong đó có mẫu Còi báo ca ngắn hạn dùng `TP (Pulse Timer, PT=5s)` và `TIME_WINDOW`.

#### 2. Khóa chặt quy chuẩn Danh mục Tag (Closed-World Assumption)
$$\text{DanhMụcTag} = (\text{InputTag} \cup \text{GuardTag} \cup \text{ActionTag} \cup \text{MacroTag}) \setminus \{\text{"NONE"}\}$$
- Quy định rõ: Chỉ liệt kê các Tag (gồm Physical, Internal, hoặc System) **thực sự xuất hiện trên các Node của sơ đồ**.
- Khi sơ đồ dùng `TIME_WINDOW` (`input_tag = "NONE"`), cấm tuyệt đối đưa `SYS_RTC_HOUR` hay `SYS_RTC_MINUTE` vào danh mục tag.
- Chỉ khi kỹ sư thực sự kết nối Node Input vào `SYS_RTC_HOUR` (ví dụ nối vào Comparator `[CMP: EQ 8]`), `SYS_CLOCK_1S` (nhấp nháy đèn), hay `SYS_FIRST_SCAN` thì tag hệ thống đó mới được liệt kê.

#### 3. Tối ưu hóa Context JSON sang Compact CSV
- Rút gọn định dạng từng Tag từ:
  `- DI0 (Bí danh: Nút Start): Loại=DiscreteInput, Địa chỉ=0x0000`
- Sang định dạng CSV tinh gọn:
  `- DI0 (Nút Start, DiscreteInput, 0x0000)`
- Tiết kiệm ~400 tokens trên mỗi request có 40 tags, giúp tốc độ phản hồi qua Groq Free Tier đạt $\ge 5.5$ requests/phút.

---

### 24.3. Bằng Chứng Kiểm Định & Kết Quả Thử Nghiệm

1. **Bộ kiểm thử AI chuyên sâu (`AiUniversalKnowledgeTests`):**
   - Đã bổ sung 10 nhóm bài test đơn vị hermetic mới (tương ứng 40 test cases qua `[Theory]`):
     - `AllModes_ContainOntologicalBoundary`: Cả 3 chế độ ở 2 ngôn ngữ đều có ranh giới cấm ST/IF...THEN.
     - `AllModes_ContainTimerAndCounterKnowledge`: Hiểu rõ TON, TOF, TP, CTU, VREG_RETAIN.
     - `AllModes_ContainZeroHallucinationContract`: Cấm đưa SYS_RTC_* vào sơ đồ TIME_WINDOW.
     - `AllModes_Contain15Archetypes`: Chứa đủ MQTT, MODBUS, SCALE, COMPARATOR.
     - `DiagnoseMode_KnowsTimeWindowWithoutInputIsNormal`: Nhận diện TIME_WINDOW không có input là bình thường.
     - `ConsultativeMode_KnowsPulseTimerForShortAlarms`: Tư vấn khối TP Pulse Timer cho còi ngắn hạn.
     - `AllModes_PromptLengthUnderSafeTokenBudget`: Đảm bảo prompt < 10,000 chars (< 2,500 tokens).
     - `BuildSynthesisInstruction_LengthConstraint_SatisfiesRange`: Nằm trong dải [6,000, 25,000] chars.
     - `BuildContextPrompt_TagFormatting_RetainsAliasAndAddress`: Định dạng CSV giữ trọn vẹn thông tin.
     - `TagCatalogRule_ExplicitlyForbidsSysRtcTagsWhenInputIsNone`: Chặn đứng việc đưa SYS_RTC_* khi input=NONE.
   - Kết quả: **132/132 AI tests PASS 100%**.

2. **Kiểm tra hồi quy toàn Solution:**
   - Biên dịch: `0 Warning(s), 0 Error(s)`.
   - Chạy toàn bộ 5 test projects:
     - `SimplePLC.Domain.Tests`: 55/55 passed
     - `SimplePLC.Protocol.Tests`: 243/243 passed
     - `SimplePLC.Infrastructure.Tests`: 210/210 passed
     - `SimplePLC.Application.Tests`: 283/283 passed
     - `SimplePLC.Studio.Tests`: 1,551/1,551 passed
   - **Tổng cộng: 2,342 / 2,342 tests PASS 100%**.

3. **Kiểm tra quy chuẩn Clean Architecture & TDD:**
   - `validate-backend-arch.ps1`: **PASS 0 architecture violations**.
   - `validate-tdd-docs.ps1`: **PASS 0 QA/TDD violations**.

---

## 22. Tinh Chỉnh Công Thái Học Giao Diện Wi-Fi, RS-485 Dialog & Deploy Pipeline Auto-Reset

### 22.1. Loại Bỏ Trùng Lặp RSSI Wi-Fi & Bổ Sung Nút Bật/Tắt Trong Popup Wi-Fi
- **Bối cảnh:** Thẻ `Network SSID & Authentication` trong `WifiConfigControl.xaml` hiển thị telemetry pill RSSI trùng lặp với tab Telemetry/Inspector bên phải. Ngoài ra, popup `WifiConfigDialogView` thiếu công tắc Kích hoạt kết nối Wi-Fi trên thiết bị (`IsEnabled`).
- **Triển khai:**
  - Loại bỏ telemetry pill RSSI góc phải header Card 1 trong `WifiConfigControl.xaml`.
  - Bổ sung `CheckBox` kích hoạt Wi-Fi vào header của `WifiConfigDialogView.xaml`, tích hợp cảnh báo màu cam (`NoticeWifiDisabled`) và hiệu ứng làm mờ / vô hiệu hoá form (`Opacity=0.45`, `IsEnabled=False`) khi Wi-Fi bị tắt.
  - Đồng bộ hai chiều với `ProjectMetadata.WifiEnabled` và `DtuGatewayStudioViewModel.ReloadFromMetadata()`.
  - Bổ sung unit tests cho luồng commit metadata Wi-Fi: `41/41 tests passed`.

### 22.2. Chuyển Cấu Hình Cổng RS-485 Modbus Thành Popup Modal Trực Tiếp
- **Bối cảnh:** Lựa chọn `Device -> RS-485 Modbus Ports Configuration... (Ctrl+Shift+B)` trước đây chuyển tab sang System thay vì mở popup như MQTT hay block Modbus.
- **Triển khai:**
  - Cập nhật `OpenModbusPortConfig` trong `MainViewModel.cs` mở trực tiếp `ModbusPortConfigDialogView` với owner là active window.
  - Tự động đồng bộ `ProjectMetadata`, `DtuGatewayStudioVM` và `LogicEditorVM` sau khi Apply.
  - Đồng bộ 100% trải nghiệm người dùng với nút bánh răng ⚙ trong các khối FBD `MB_READ` và `MB_WRITE`.

### 22.3. Sửa Lỗi Log Lơ Lửng & Chuẩn Hóa Cơ Chế Tự Động Hoàn Nguyên (Auto-Reset) Cho Tab Deploy
- **Bối cảnh:**
  - Trong `DeployView.xaml`, `TextBox` hiển thị `ConsoleLog` bị căn giữa theo chiều dọc do thiếu `VerticalContentAlignment="Top"` và `AcceptsReturn="True"`, gây cảm giác dòng text trôi lơ lửng ở giữa card.
  - Sau khi nạp thành công, 5 bước bị đóng băng ở `Done 100%` và nút chuyển sang màu xanh lá vĩnh viễn, khiến giao diện thiếu trực quan cho lần nạp tiếp theo.
- **Triển khai:**
  - Đã thêm `AcceptsReturn="True"` và `VerticalContentAlignment="Top"` cho `TextBox` log trong `DeployView.xaml`.
  - Bổ sung cơ chế `ScheduleAutoResetSuccess(3000)` trong `DeployViewModel.cs`: Sau khi nạp thành công, giữ hiển thị 3 giây để kỹ sư theo dõi kết quả, sau đó tự động hoàn nguyên Stepper về trạng thái `Sẵn sàng (Ready / Pending)`, ẩn thanh tiến độ, chuyển nút bấm về kiểu Primary tiêu chuẩn sẵn sàng nạp tiếp, đồng thời giữ nguyên 100% lịch sử `ConsoleLog`.
  - Bổ sung unit test `ResetToReadyState_RestoresStepsAndProgress_WhilePreservingConsoleLog`: toàn bộ `46/46` unit test Deploy vượt qua.

---

## 21. Xây Dựng Bộ Cài Đặt Chính Thức SynaptiX IDE (Microsoft VS Code Standard Inno Setup 6)

### 21.1. Bối cảnh & Nguyên Lý Thiết Kế
- **Mục tiêu:** Đóng gói phần mềm desktop SynaptiX IDE v2.15.0 (.NET 8 WPF 64-bit self-contained) thành một bộ cài đặt Windows duy nhất (`SynaptiX_Setup_v2.15.0.exe`), đạt chuẩn phân phối quốc tế tương tự Microsoft Visual Studio Code và Git for Windows.
- **Tiêu chuẩn chất lượng:**
  1. **Dung lượng siêu tinh gọn:** Ứng dụng gốc self-contained nặng ~241 MB được nén bằng thuật toán **LZMA2 Ultra64 Solid**, đạt kích thước phân phối chỉ **72.18 MB** (giảm 70% băng thông tải về).
  2. **Giao diện chuẩn Segoe UI & Modern Wizard:** Khắc phục triệt để các lỗi layout của phiên bản thử nghiệm:
     - Không có dòng text bị cắt cụt (clipping).
     - Không bị chèn đè hoặc mất nút radio "I accept / I do not accept" ở chân trang EULA.
     - Không bị lặp dòng "Click Next to continue, or Cancel to exit Setup".
     - Loại bỏ icon thư mục vàng (clipart) lỗi thời ở trang chọn thư mục.
     - Đồng bộ 100% typography `Segoe UI` trên toàn bộ wizard và văn bản giấy phép.
  3. **Bộ nhận diện thương hiệu độ phân giải cao:**
     - Banner chào mừng/hoàn tất (`wizard_large.bmp` 164x314 px): Logo SynaptiX sắc nét trên nền slate trung tính `#F8FAFC` với đường phân cách viền phải 1px `#E2E8F0`.
     - Logo tiêu đề trang (`wizard_small.bmp` 55x55 px): Logo SynaptiX trên nền trắng `#FFFFFF` đồng nhất tuyệt đối với thanh header Inno Setup.
  4. **Tích hợp hệ điều hành Windows đầy đủ:**
     - Shortcut Desktop: `SynaptiX IDE` với icon độ phân giải cao đa tầng `app.ico`.
     - Start Menu Program Group: `SynaptiX Corporation` chứa `SynaptiX IDE`, công cụ giả lập `SynaptiX MCU Emulator`, và trình gỡ cài đặt.
     - File Association: Tự động liên kết đuôi tệp dự án `.synx` với icon SynaptiX và lệnh mở trực tiếp `SynaptixIDE.exe "%1"`.
     - Windows Apps & Features: Đăng ký gỡ cài đặt sạch sẽ, hỗ trợ cả tài khoản người dùng chuẩn và quyền quản trị.

### 21.2. Thông Số Kỹ Thuật Bản Phát Hành
- **Tên tệp cài đặt:** `installer\dist\SynaptiX_IDE_Setup_v2.15.0.exe`
- **Kích thước tệp:** `72.17 MB` (75,677,968 bytes)
- **Mã băm SHA-256:** `D979D80EC8173B795C49FFCE5B1B983DF74B071E741A8AB50CA87C02E8E4C67B`
- **Bộ nén & phiên bản:** Inno Setup 6.7.4 (LZMA2 Ultra64 Solid Compression)

---



## 20. Tái Cấu Trúc Toàn Diện Phân Hệ MQTT SynaptiX IDE (Canvas-Centric MQTT & Broker Infrastructure Decoupling)

### 20.1. Bối cảnh & Nguyên Lý Thiết Kế (Architectural Doctrine)
- **Vấn Đề Trước Tái Cấu Trúc:**
  - Hệ thống tồn tại song song hai nơi cấu hình MQTT: Màn hình System Tab (DTU Gateway) chứa các tab phụ cấu hình Telemetry Pub và Command Sub cố định, trong khi Logic Editor Canvas lại hỗ trợ các khối FBD `MQTT_PUB` và `MQTT_SUB`.
  - Sự trùng lặp này vi phạm **Axiom 2 (The Visual Truth / Single Source of Truth)**: Người dùng bị phân tâm giữa hai nơi cấu hình, không rõ luồng nào thực sự nạp xuống PLC, và System Tab bị giới hạn vào 1 Topic duy nhất thay vì kiến trúc đa Topic / đa Payload của IoT công nghiệp.
  - Khối `MQTT_SUB` trên Canvas gặp hiện tượng dôi connector mồ côi khi gọi `Pins.Clear()`, thiếu cơ chế khóa chống chọn trùng Tag nội bộ khối (Intra-Block Exclusion), và chưa có cơ chế đồng bộ kiểu dữ liệu hai chiều chuẩn xác giữa kiểu pin MQTT và kiểu thanh ghi PLC (`TagModel.DataType`).
- **Học Thuyết Thiết Kế Sau Tái Cấu Trúc:**
  - **System Tab (DTU Gateway):** Tập trung **100% vào Hạ Tầng Broker** (Host, Port, Client ID, Username, Password, KeepAlive, Last Will LWT, TLS/mTLS, Ping test kết nối). Toàn bộ mã legacy về Pub/Sub tại System Tab được dọn sạch hoàn toàn.
  - **Logic Canvas (FBD Blocks):** Đóng vai trò là **Single Source of Truth** duy nhất cho cấu hình đa Topic / đa Payload thông qua các khối `MQTT_PUB` và `MQTT_SUB`.
  - **Axiom 1 (Hardware Ground Truth):** Các thanh ghi 32-bit (`VREG`, `AI`, `ModbusHolding`) chỉ tồn tại ở dạng `Int32` hoặc `Float32 (REAL)`. Chúng không thể bị ép sang `Boolean` (1-bit coil). Khi người dùng chuyển chân MQTT sang `Bool`, các thanh ghi số sẽ bị loại khỏi danh sách tương thích và chân tự động reset về `None`.
  - **Axiom 4 (End-to-End Invariant):** Mắt xích đồng bộ toàn vẹn từ giao diện UI $\rightarrow$ ViewModel $\rightarrow$ RuleCompiler $\rightarrow$ Deploy Tag Registry (phân vùng MCU `0x0080` mã kiểu 3 REAL) $\rightarrow$ Runtime/Debug.

---

### 20.2. Các Thành Phần Kỹ Thuật Được Triển Khai

#### 1. Dọn Sạch Mã Legacy & Tối Giản Hóa Giao Diện System Tab
- **`DtuGatewayStudioView.xaml`:**
  - Loại bỏ hoàn toàn thanh chọn 3 tab phụ (`SubSections`) và 2 UserControl con (`MqttTelemetryPublishControl`, `MqttCommandSubscribeControl`).
  - Nhúng trực tiếp `MqttBrokerConfigControl` làm nội dung chính của Section MQTT.
  - Tinh chỉnh Inspector Card 1 (`Card 1: Broker & Network Profile`) để tóm tắt thông số Broker, Client ID, KeepAlive, LWT và hiển thị hướng dẫn trực quan: *"Cấu hình đa Topic & Payload: Sử dụng các khối FBD MQTT_PUB / MQTT_SUB trên Logic Canvas"*.
  - Kiểm định XAML: Tuân thủ 100% Fluent 2 và Token Semantic (`validate-xaml-tokens.ps1` $\rightarrow$ `[PASS] 0 violations`).
- **`DtuGatewayStudioViewModel.cs`:**
  - Xóa bỏ triệt để các thuộc tính, phương thức, event listener thừa: `MqttTelemetryVM`, `MqttCommandVM`, `MqttPublishTopic`, `MqttSubscribeTopic`, `MqttPublishModeDisplay`, `MqttQueueModeDisplay`, `_mqttActiveSubSection`, v.v.
  - Hệ thống chỉ lưu trữ và biên dịch cấu hình Broker & LWT chuẩn.
- **`LocalizationService.cs`:**
  - Bổ sung các khóa song ngữ EN/VI: `InspectorMqttBrokerSummaryTitle`, `InspectorClientIdLabel`, `InspectorKeepAliveLabel`, `InspectorMqttCanvasGuidance`, `MqttPinConflictTooltip`.

#### 2. Khắc Phục Lỗi Dôi Connector Mồ Côi Trên Canvas
- **`MqttSubscribeNodeViewModel.cs`:**
  - Trong sự kiện `OnPinsCollectionChanged`, bổ sung nhánh xử lý sự kiện `NotifyCollectionChangedAction.Reset` (khi gọi `Pins.Clear()`).
  - Tự động lọc và gỡ bỏ toàn bộ dynamic connectors cũ của các pin dữ liệu khỏi danh sách `OutputConnectors`, chỉ bảo lưu 2 connector hệ thống cố định là `Valid` và `Err`.

#### 3. Cơ Chế Khóa Chống Trùng Tag Nội Bộ Khối (Intra-Block Exclusion)
- **`MqttPinBindingViewModel.cs` & `MqttSubConfigDialogViewModel.cs`:**
  - Khởi tạo delegate `PeerPinsProvider = () => Node.Pins` cho từng pin.
  - Trong `UpdateTagOptionConflict(TagOptionItem opt)`: Kiểm tra nếu bất kỳ pin anh em nào trong cùng khối đã chọn `opt.Tag`, tùy chọn đó sẽ lập tức bị khóa (`IsSelectable = false`), gắn nhãn cảnh báo trực quan `[⛔ {peer.Label}]` và tooltip giải thích chi tiết.
  - Khi một pin thay đổi `TargetTag`, sự kiện tự động kích hoạt `RefreshTagOptions()` trên toàn bộ các pin trong khối, đảm bảo cập nhật trạng thái khóa/mở khóa tức thì theo thời gian thực.

#### 4. Đồng Bộ Kiểu Dữ Liệu Hai Chiều Chuẩn Mực (Two-Way DataType Sync)
- **Xử lý `OnDataTypeChanged`:**
  - Khi pin chuyển sang `Float`: Nếu `TargetTag.IsDataTypeEditable == true`, tự động cập nhật `TargetTag.DataType = Float32`.
  - Khi pin chuyển sang `Int16` / `Int32`: Nếu `TargetTag.IsDataTypeEditable == true`, tự động cập nhật `TargetTag.DataType = Int32`.
  - Khi pin chuyển sang `Bool`: Tuyệt đối không thay đổi kiểu của `VREG` thành `Boolean` (tuân thủ Axiom 1). Thay vào đó, `VREG` không còn nằm trong `AvailableTags` và `TargetTag` tự động reset về `None`.
- **Xử lý `OnTargetTagChanged`:**
  - Khi chọn Tag kỹ thuật số (`IsDigital || DataType == Boolean`): Pin tự động đổi `DataType = MqttDataType.Bool`.
  - Khi chọn Tag kiểu số thực (`DataType == Float32`): Pin tự động đổi `DataType = MqttDataType.Float`.
  - Khi chọn Tag số nguyên (`DataType == Int32`): Nếu pin đang là `Float` và Tag có `IsDataTypeEditable`, Tag được tự động đồng bộ sang `Float32`; nếu không, pin thích ứng sang `Int32`.

#### 5. Đồng Bộ Tag Registry Phân Vùng MCU `0x0080` (Runtime / Deploy)
- **`DeployViewModel.cs`:**
  - Trong quá trình biên dịch nạp (`CompileAndStagePayload`), hệ thống quét toàn bộ các khối `MqttSubscribeNodeViewModel` trên Logic Canvas.
  - Đối với các pin có `DataType == MqttDataType.Float` và `TargetTag` trỏ vào phân vùng thanh ghi thực `0x0080..0x00BF`, hệ thống tự động gán `tagDataTypes[tag.Index] = 3` (mã kiểu `SPLC_TAG_TYPE_REAL` trong firmware MCU).
  - Điều này đảm bảo khi thiết bị nạp vào Flash và khởi động runtime, MCU giải mã giá trị IEEE 754 32-bit Float chính xác tuyệt đối, tránh hiện tượng đọc nhầm thành số nguyên thô.

---

### 20.3. Bộ Kiểm Thử Độc Lập End-to-End (`MqttEndToEndArchitectureTests.cs`)
Khởi tạo bộ test case kỹ thuật độc lập với 7 bài kiểm thử chuyên sâu theo Axiom 4:
1. **`MqttSubscribeNode_WhenPinsCleared_RemovesDynamicConnectorsAndPreservesSystemPins`:** Xác thực việc gọi `Pins.Clear()` gỡ sạch dynamic connectors và bảo toàn 2 system pins (`Valid`, `Err`).
2. **`MqttPinBinding_IntraBlockExclusion_PreventsDuplicateTagSelectionInSameBlock`:** Xác thực khóa chống chọn trùng Tag nội bộ khối và mở khóa tức thì khi Tag được giải phóng.
3. **`MqttPinBinding_TwoWayDataTypeSync_UpdatesTargetTagDataType`:** Xác thực chu trình đồng bộ kiểu dữ liệu hai chiều và bảo vệ kiểu vật lý của `VREG`.
4. **`MqttPinBinding_WhenFloatPinSelectsInt32Vreg_SynchronizesVregToFloat32`:** Xác thực tự động nâng cấp VREG từ Int32 lên Float32 khi gán vào chân Float.
5. **`MqttSubscribeNode_RuleCompiler_GeneratesCorrectRuleAndPayloadMapping`:** Xác thực `RuleCompiler` dịch chuẩn xác cấu trúc AST của khối MQTT Subscribe từ Canvas.
6. **`MqttSubscribeNode_Deploy_SynchronizesTagRegistry_FloatTypesForMcu`:** Xác thực `DeployViewModel` đồng bộ mã kiểu 3 (`SPLC_TAG_TYPE_REAL`) vào mảng `tagDataTypes` phân vùng `0x0080`.
7. **`MqttBrokerConfig_FullRoundtrip_SerializationAndDeserialization`:** Xác thực serialization/deserialization trọn vẹn thông số Broker & LWT qua `MqttBrokerConfigCodec`.

---

### 20.4. Kết Quả Xác Thực Kỹ Thuật (Zero Violations & 100% Pass)
- **UI/UX XAML Token Validator:** `[PASS]` 0 violations.
- **Backend Clean Architecture & IEC 61508-3 Validator:** `[PASS]` 0 architecture violations.
- **QA, TDD & Living Documentation Validator:** `[PASS]` 0 violations.
- **Toàn bộ Unit & Integration Test Suite:** **2,176 / 2,176 tests PASSED (100%)**
  - `SimplePLC.Domain.Tests`: 55 / 55 PASS
  - `SimplePLC.Protocol.Tests`: 243 / 243 PASS
  - `SimplePLC.Infrastructure.Tests`: 199 / 199 PASS
  - `SimplePLC.Application.Tests`: 265 / 265 PASS
  - `SimplePLC.Studio.Tests`: 1,414 / 1,414 PASS

---

## 19. Tối Ưu Hóa & Tăng Tốc Toàn Bộ Test Suite (Zero-Wait Test Optimization)

### 19.1. Bối cảnh & Hiện trạng Kiểm toán Hiệu năng
- Khi chạy toàn bộ test suite, thời gian thực thi của `SimplePLC.Studio.Tests` kéo dài gần 1 phút 30 giây (85–90s) dù chỉ chạy trên máy phát triển cao cấp.
- Trích xuất tệp XML Profiling (`test_results.trx`) chỉ ra các điểm nghẽn hiệu năng chính:
  1. **`DeployViewModel.cs` Chứa Các Bước Trễ Giả Lập UI (`Task.Delay(60..70ms)`):**
     - Tổng cộng 6 lệnh `Task.Delay` tương đương **400ms** thời gian trễ nhân tạo cho mỗi lần gọi `StartDeployAsync()`.
     - Trong test suite có hơn 65 lần thực thi Deploy (trong đó test `Test_01_AllFactoryBlueprints` lặp qua 30 Blueprint liên tiếp), gây lãng phí tới **26.0 giây** hoàn toàn vô ích cho việc chờ đợi hiệu ứng chuyển động tiến trình không tồn tại trong môi trường Headless.
     - Riêng test case `Test_01_AllFactoryBlueprints` chiếm tới **18.12 giây**!
  2. **`WifiConfigViewModel.cs` Chờ Thăm Dò Polling Cố Định:**
     - Vòng lặp kết nối Wi-Fi thăm dò 10 lần với `Task.Delay(300ms) = 3.0 giây`.
     - Test case `ConnectAsync_WithValidSsid_ShouldCallWriteConfigAndTriggerConnect` tốn **3.088 giây** chỉ để chờ timeout trạng thái `Connecting`.
  3. **Truy Cập Mutex OS Clipboard (`System.Windows.Clipboard`) Trong STA Thread:**
     - Khi chạy `Duplicate()` hoặc `CopyPaste()`, thao tác đồng bộ với OS Clipboard của Windows bị chặn mutex lên tới 1.0 giây mỗi test.

### 19.2. Giải Pháp Kỹ Thuật Triệt Để (Zero-Wait Architecture)
1. **Thuộc Tính `StepDelayMs` và Cơ Chế Tự Động Hóa `EffectiveDelayMs` Trong `DeployViewModel`:**
   - Bổ sung thuộc tính `StepDelayMs` (mặc định 60ms cho UI ứng dụng thực tế).
   - Tự động chuyển `EffectiveDelayMs = 0` khi làm việc với `FakeModbusClient` trong môi trường kiểm thử.
   - Kết quả: `Test_01_AllFactoryBlueprints` giảm từ **18,124 ms xuống còn 308 ms (tăng tốc 58.8 lần)**! Các test Deploy khác giảm từ ~450ms xuống còn **6–20ms**.
2. **Cơ Chế Dynamic Polling Interval Trong `WifiConfigViewModel`:**
   - Bổ sung `ConnectPollIntervalMs` và `ScanPollIntervalMs`.
   - Tự động bỏ qua trễ thăm dò (0ms) khi client là `FakeWifiClient`.
   - Kết quả: `ConnectAsync_WithValidSsid` giảm từ **3,088 ms xuống còn 2 ms (tăng tốc 1,500 lần)**! Toàn bộ 21 test Wi-Fi hoàn thành trong **0.92 giây**.
3. **Bỏ Qua Đồng Bộ OS Clipboard Ngoài UI Application (`LogicEditorViewModel`):**
   - Chỉ ghi dữ liệu ra `System.Windows.Clipboard` khi `System.Windows.Application.Current != null` (chạy trên ứng dụng Desktop thực tế). Trong môi trường headless test, sử dụng trực tiếp clipboard bộ nhớ `_inMemoryClipboard`.
   - Kết quả: `Clipboard_CopyPaste` giảm từ **976 ms xuống còn 4 ms**!

### 19.3. Kết Quả Sau Tối Ưu Hóa
- Toàn bộ thời gian chạy `SimplePLC.Studio.Tests` giảm hơn **20–25 giây**, xuống còn **~1 phút 4 giây** cho 1,404 tests.
- Tổng số lượng test suite toàn hệ thống: **2,166 / 2,166 tests PASS (100%)** qua cả 5 dự án.

---

## 18. Kiểm Thử Tự Động Toàn Diện Hệ Thống & Cơ Chế Độc Quyền Ghi Tag (Exclusive Ingestion Ownership)

### 18.1. Bối cảnh & Mục tiêu Kỹ thuật
- Nhằm đảm bảo tính toàn vẹn 1:1 theo **Học thuyết Kỹ thuật First Principles (Axiom 1 & Axiom 2)**:
  - Phân vùng nhớ PLC / MCU không được phép xảy ra xung đột tranh chấp ghi (Race Condition / Data Thrashing) khi nhiều luồng truyền thông ngoại vi (Modbus Master Read FC01..FC04 và MQTT Subscribe Dynamic Pins) cùng trỏ vào một địa chỉ Tag PLC.
  - Giao diện người dùng SynaptiX IDE phải hiển thị Visual Truth: Tự động vô hiệu hóa các Tag đã bị chiếm dụng trong Dropdown của kênh khác kèm Huy hiệu cảnh báo (`[⛔ Modbus Ch.X]` hoặc `[⛔ MQTT: Pin]`) và Tooltip giải thích chi tiết, đồng thời áp dụng cơ chế tự miễn trừ (Self-Exemption) cho chính kênh đang sở hữu Tag.
  - Cổng an toàn tiền kiểm nạp (Deploy Preflight Safety Gate) trong `DeployViewModel.cs` phải tự động phát hiện xung đột và từ chối phát lệnh nạp (`[SAFETY ABORT]`) trước khi gửi bất kỳ byte dữ liệu nào xuống cổng vật lý, bảo vệ an toàn cho dây chuyền công nghiệp.

### 18.2. Các Thành Phần Được Triển Khai & Bổ Sung Test
1. **Dịch vụ Quản lý Sở hữu Ghi Độc quyền (`TagWriteOwnershipService.cs`):**
   - Đăng ký Provider động từ `ModbusPortConfigViewModel` và `MqttSubscribeNodeViewModel`.
   - Cung cấp hàm `GetClaimant(TagModel tag, object currentCaller)` với cơ chế tự miễn trừ.
   - Hàm chuẩn hóa `IsSameTag(TagModel t1, TagModel t2)` xử lý an toàn `TagKind.None` và so khớp case-insensitive.
2. **Cổng An Toàn Deploy (`DeployViewModel.cs`):**
   - Tích hợp `ValidateIngestionOwnership(modbusReads, mqttSubs)` trong bước tiền kiểm (Preflight Step 0).
   - Tự động đồng bộ trạng thái thất bại vào `DeployState = Failed`, `StatusTitle = "Nạp Thất Bại"`, và hiển thị lý do xung đột trên giao diện người dùng.
3. **Bộ Kiểm Thử Độc Quyền Ghi Tag (`ExclusiveTagOwnershipTests.cs` - 15/15 tests PASS):**
   - `CrossProtocol_ModbusReadAndMqttSub_EnforcesExclusiveOwnership`: Kiểm tra chặn xung đột chéo giữa Modbus Master và MQTT Inbound.
   - `CrossProtocol_TwoMqttSubscribePins_EnforcesExclusiveOwnership`: Kiểm tra chặn 2 pin MQTT cùng ghi vào một Tag.
   - `ModbusReadChannel_WhenTagClaimed_DisablesTagInOtherChannelsDropdown`: Kiểm tra vô hiệu hóa Tag trong Dropdown.
   - `ModbusChannels_CrossSelection_PreservesBothChannelsTags`: Kiểm tra chuyển đổi Tag không bị mất dữ liệu.
   - `ModbusReadChannel_WhenChannelRemoved_ReleasesTagOwnership`: Kiểm tra giải phóng quyền khi xóa kênh.
   - `ModbusPortItemViewModel_WhenPortDisabled_ReleasesAllClaims`: Kiểm tra giải phóng toàn bộ khi tắt cổng.
   - `ModbusReadChannel_SelfExemption_AllowsCurrentChannelToKeepTag`: Kiểm tra cơ chế tự miễn trừ của Modbus.
   - `MqttPinBindingViewModel_SelfExemption_AllowsCurrentPinToKeepTag`: Kiểm tra cơ chế tự miễn trừ của MQTT.
   - `ModbusWriteChannel_DoesNotClaimTag_AllowsOtherWriters`: Đảm bảo kênh ghi (Modbus Write) không chiếm độc quyền vì nó chỉ đọc dữ liệu từ PLC.
   - `TagWriteOwnershipService_IsSameTag_BoundaryAndEdgeCases`: Kiểm thử biên và trường hợp `TagKind.None`.
   - `DeploySafetyGate_WhenMultipleIngestionWritersTargetSameTag_RejectsDeployment`: Kiểm thử từ chối nạp cấp độ DTO.
   - `DeploySafetyGate_WhenConflictExists_AbortsPreflightInDeployViewModel`: Kiểm thử từ chối nạp cấp độ ViewModel UI.
   - `DeploySafetyGate_WhenLogicEditorRulesTargetSameTag_AllowsDeployment`: Cho phép các rule logic nội bộ cùng ghi theo logic PLC.
   - `ModbusPortConfigView_WhenAddMasterChannelExecuted_DoesNotCrash`: Kiểm tra độ ổn định UI khi thêm kênh.
   - `MqttCommandSubscribeControl_WhenAddPinExecuted_DoesNotCrash`: Kiểm tra độ ổn định UI khi thêm pin MQTT.

### 18.3. Mở Rộng Kiểm Thử Tự Động Phần Cứng HIL Qua COM7 ↔ COM9 (27/27 tests PASS)
- Bộ kiểm thử phần cứng mô phỏng thực tế `RealCom7Com9HwHiltTests.cs` được nâng cấp từ 24 lên **27 test cases toàn diện**:
  - **Test 25 (`Section2_MqttWill_WhenConfigured_StagedAndVerifiedOverWire`):** Kiểm chứng ghi 32 thanh ghi tại `0x0E60` qua mã hàm FC16 trên đường truyền nối tiếp COM9 $\rightarrow$ COM7, đọc lại bằng FC03 và giải mã chính xác các trường `WILL_CONTROL`, Topic và Payload.
  - **Test 26 (`Section2_MqttTls_WhenCertificateStaged_CommitsAndStoresCrcOverWire`):** Kiểm chứng phân mảnh chứng chỉ TLS thành các chunk, nạp qua `0x0F00`, đọc xác nhận tại `0x0F40`, gửi lệnh Commit `0xA5A5` tại `0x0F42` và kiểm tra Active Mask tại `0x0F43` cùng Root CA CRC tại `0x0F44`.
  - **Test 27 (`Section8_ExclusiveOwnership_DeployGateSafetyRejection_OverWire`):** Kiểm chứng cổng an toàn ngăn chặn phát tán dữ liệu xuống dây cáp nối tiếp khi phát hiện xung đột ghi Tag, sau đó khắc phục xung đột và hoàn thành nạp hợp lệ.

### 18.4. Tổng Hợp Kết Quả Toàn Bộ Hệ Thống
- **XAML Design Token Compliance:** 100% tuân thủ, **0 vi phạm**.
- **Backend Clean Architecture & IEC 61508-3:** 100% tuân thủ, **0 vi phạm**.
- **QA / TDD & Living Documentation:** 100% tuân thủ, **0 vi phạm**.
- **Tổng số Unit / Integration / HIL Tests:** **2,111 / 2,111 tests PASSED (100%)**
  - `SimplePLC.Domain.Tests`: 55 / 55 PASS
  - `SimplePLC.Protocol.Tests`: 241 / 241 PASS
  - `SimplePLC.Infrastructure.Tests`: 189 / 189 PASS
  - `SimplePLC.Application.Tests`: 253 / 253 PASS
  - `SimplePLC.Studio.Tests`: 1,373 / 1,373 PASS

---

## 16. Chuẩn Hóa Đặc Tả Kỹ Thuật Firmware ICD Cho Phân Hệ MQTT Last Will & Testament (LWT, SPLC-SPEC-MQTT-004)

### 16.1. Bối cảnh & Mục tiêu
- Phía Firmware MCU bắt đầu triển khai tính năng MQTT Last Will & Testament (LWT) và cần tài liệu Interface Control Document (ICD) chính thức xác định rõ:
  - Bản đồ thanh ghi Modbus, phân bổ bộ nhớ (`0x0E60..0x0E7F`, 32 Holding Registers = 64 Bytes).
  - Cấu trúc C nhị phân chuẩn (`SPLC_MqttWillConfig_t`), các bitmask điều khiển (`WILL_CONTROL`).
  - Chu trình nạp Deploy hai pha (Two-Phase Staging & Atomic Commit qua `0xA000 = 0xA5A5`).
  - Xử lý các điều kiện biên: từ chối commit khi `Enabled = 1` nhưng Topic rỗng, vô hiệu hóa an toàn khi `Enabled = 0`, phân giải token `{device}` và phòng vệ cú pháp lệnh AT module Wi-Fi (loại bỏ dấu `"` và `\`).

### 16.2. Các Văn Bản & Tài Liệu Được Cập Nhật / Khởi Tạo
1. **Khởi tạo ICD Chính Thức:**
   - [`docs/firmware/SPLC_SPEC_MQTT_004_LAST_WILL_ICD.md`](file:///g:/HoaNV/Projects/SimplePLC/docs/firmware/SPLC_SPEC_MQTT_004_LAST_WILL_ICD.md):
     - Định nghĩa phân vùng Modbus 32 thanh ghi: `0x0E60` (`WILL_CONTROL`), `0x0E61..0x0E70` (`WILL_TOPIC[32]`), `0x0E71..0x0E7F` (`WILL_PAYLOAD[30]`).
     - Quy chuẩn Two-Phase Deploy: Nạp vào Shadow RAM buffer $\rightarrow$ Kiểm tra CRC $\rightarrow$ Commit Flash tại `0xA000` $\rightarrow$ Tái cấu hình MQTT stack.
     - Quy tắc phòng vệ lệnh AT: Chặn ký tự `"`, `\` và ký tự không in được; chuẩn hóa payload mặc định sang text thuần `"offline"`; App tự động phân giải token `{device}` trước khi gửi xuống thiết bị.
     - Ma trận kiểm thử & Checklist nghiệm thu cho Firmware MCU (LWT-TC01 .. LWT-TC06).
2. **Cập nhật Master Conformance Spec:**
   - [`docs/firmware/MCU_CONFORMANCE_SPECIFICATION_V2_0.md`](file:///g:/HoaNV/Projects/SimplePLC/docs/firmware/MCU_CONFORMANCE_SPECIFICATION_V2_0.md):
     - Bổ sung phân vùng `0x0E60..0x0E7F` vào Bảng 1 (Master Holding Registers Memory Map).
     - Bổ sung Mục 13.5.1: *Quy Chuẩn Bản Tin Di Chúc MQTT Last Will & Testament LWT (`0x0E60..0x0E7F`)*.
     - Cập nhật sơ đồ tuần tự Deploy (Sequence Diagram) bổ sung bước ghi `0x0E60`.
     - Bổ sung tiêu chí nghiệm thu **CHKL-25**: *MQTT Last Will & Testament LWT Conformance*.
3. **Cập nhật Mục lục & Web Portal Living Docs:**
   - [`docs/README.md`](file:///g:/HoaNV/Projects/SimplePLC/docs/README.md): Thêm liên kết tài liệu `SPLC_SPEC_MQTT_004_LAST_WILL_ICD.md`.
   - [`docs-portal/vi/tools/mqtt-and-wifi-config.md`](file:///g:/HoaNV/Projects/SimplePLC/docs-portal/vi/tools/mqtt-and-wifi-config.md) & [`docs-portal/en/tools/mqtt-and-wifi-config.md`](file:///g:/HoaNV/Projects/SimplePLC/docs-portal/en/tools/mqtt-and-wifi-config.md): Chuẩn hóa giá trị mặc định của `WillPayload` sang text thuần `"offline"` (<= 30 ký tự) kèm khuyến nghị tránh dùng dấu `"` để tương thích module Wi-Fi.

### 16.3. Kết quả Thẩm Định (Docs Validation)
- Chạy công cụ kiểm định tự động `validate-docs.ps1` thuộc skill `synaptix-docs-techwriter`:
  - Quét 89 tệp Markdown trên docs-portal: **0 vi phạm**.
  - Kiểm tra 14 thông số kỹ thuật Firmware ICD: **14/14 PASS**.
  - Biên dịch cổng thông tin VitePress 1.6 (`npm run docs:build`): **0 broken links, clean build**.

---

## 17. Hoàn Thiện Triển Khai Phân Hệ MQTT Last Will & Testament (LWT) Trên SynaptiX IDE (Full Pipeline)

### 17.1. Bối cảnh & Yêu cầu Kỹ thuật
- Sau khi chuẩn hóa ICD `SPLC-SPEC-MQTT-004`, toàn bộ chuỗi mắt xích từ Presentation Layer (WPF UI, ViewModels) đến Core Logic (Deploy UseCase, RuleTableWriter) và Hardware Protocol Codec được hoàn thiện nhằm đảm bảo tính toàn vẹn 1:1 giữa Studio và Firmware MCU.
- Các yêu cầu kỹ thuật then chốt:
  1. **Quy chuẩn Two-Phase Deploy & Safe Disable:** Khi người dùng tắt tính năng LWT (`Enabled = false`), hệ thống nạp bắt buộc phải ghi đè 32 thanh ghi giá trị 0 (`WILL_CONTROL = 0`) vào địa chỉ `0x0E60` trong Shadow RAM trước khi gửi lệnh COMMIT `0xA000 = 0xA5A5`, đảm bảo MCU xóa sạch cấu hình LWT cũ trong Flash và ngắt cờ Will khi gửi gói `CONNECT`.
  2. **Tự động phân giải Token `{device}`:** App tự động thay thế token `{device}` bằng `MqttClientId` hợp lệ trước khi mã hóa wire packet, giải phóng MCU khỏi việc xử lý chuỗi ký tự động.
  3. **Phòng vệ cú pháp Lệnh AT Wi-Fi:** Tự động thanh lọc (sanitize) các ký tự dấu ngoặc kép `"` và dấu gạch chéo ngược `\` trong ô nhập Topic và Payload (cả khi nhập trực tiếp lẫn khi nạp Deploy), giới hạn độ dài Payload tối đa 30 ký tự, và đặt giá trị mặc định là chuỗi text thuần `"offline"`.
  4. **Chặn lỗi UI & Trực quan hóa lỗi:** Khi bật LWT nhưng `WillTopic` rỗng hoặc chỉ chứa khoảng trắng, trường nhập hiển thị viền cảnh báo màu đỏ (`DangerBrush`), thông báo lỗi tiếng Việt/tiếng Anh, và vô hiệu hóa nút Lưu (`CanSave = false`).

### 17.2. Các Thành Phần Mã Nguồn Được Triển Khai

1. **Protocol Layer (`SimplePLC.Protocol`):**
   - [`src/SimplePLC.Protocol/Dto/MqttWillConfigDto.cs`](file:///g:/HoaNV/Projects/SimplePLC/src/SimplePLC.Protocol/Dto/MqttWillConfigDto.cs): Chuẩn hóa giá trị mặc định của `Payload` sang `"offline"`, bổ sung đầy đủ tài liệu XML doc theo chuẩn Living Docs.
   - [`src/SimplePLC.Protocol/Codec/MqttWillConfigCodec.cs`](file:///g:/HoaNV/Projects/SimplePLC/src/SimplePLC.Protocol/Codec/MqttWillConfigCodec.cs): Mã hóa/giải mã 32 holding register (`0x0E60..0x0E7F`) với bitmask `WILL_CONTROL` (Bit 0: Enable, Bit 1: Retain, Bit 2..3: QoS) và Packed ASCII Big-Endian (Topic 32 bytes, Payload 30 bytes).
   - [`tests/SimplePLC.Protocol.Tests/MqttWillConfigCodecTests.cs`](file:///g:/HoaNV/Projects/SimplePLC/tests/SimplePLC.Protocol.Tests/MqttWillConfigCodecTests.cs): Kiểm thử Triad (Happy Path, Boundary Truncation, Fault Injection). 8/8 tests PASS.

2. **Infrastructure Layer (`SimplePLC.Infrastructure`):**
   - [`src/SimplePLC.Infrastructure/Devices/RuleTableWriter.cs`](file:///g:/HoaNV/Projects/SimplePLC/src/SimplePLC.Infrastructure/Devices/RuleTableWriter.cs): Bổ sung khối lệnh xử lý an toàn:
     - Khi `effectiveWill != null && effectiveWill.Enabled`: Mã hóa và ghi 32 registers vào `0x0E60`.
     - Khi `effectiveWill != null && !effectiveWill.Enabled`: Ghi đè 32 registers 0 vào `0x0E60` và ghi log `[MQTT LWT] Disabling LWT: clearing 32 registers at 0x0E60`.
   - [`tests/SimplePLC.Infrastructure.Tests/RuleTableWriterLwtTests.cs`](file:///g:/HoaNV/Projects/SimplePLC/tests/SimplePLC.Infrastructure.Tests/RuleTableWriterLwtTests.cs): Kiểm thử kiểm chứng ghi 32 registers kích hoạt và xóa an toàn khi vô hiệu hóa. 2/2 tests PASS.

3. **Application Layer (`SimplePLC.Application`):**
   - [`src/SimplePLC.Application/UseCases/DeployRulesUseCase.cs`](file:///g:/HoaNV/Projects/SimplePLC/src/SimplePLC.Application/UseCases/DeployRulesUseCase.cs): Tiếp nhận `MqttWillConfigDto? mqttWillConfig` và chuyển tiếp đến `_writer.DeployRulesAsync(...)`.
   - [`tests/SimplePLC.Application.Tests/DeployRulesUseCaseTests.cs`](file:///g:/HoaNV/Projects/SimplePLC/tests/SimplePLC.Application.Tests/DeployRulesUseCaseTests.cs): Bổ sung test `ExecuteAsync_WithMqttWillConfig_ForwardsLwtToWriterAndLogs0x0E60`. 6/6 tests PASS.

4. **Presentation Layer & UI/UX (`SimplePLC.Studio`):**
   - [`SimplePLC.Studio/ViewModels/DeployViewModel.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/ViewModels/DeployViewModel.cs): Trích xuất `MqttWillConfig` từ `ProjectMetadata`, giải quyết token `{device}`, thanh lọc ký tự `"` và `\`, cắt ngắn payload tối đa 30 ký tự, và truyền xuống `DeployRulesUseCase`.
   - [`SimplePLC.Studio/ViewModels/MqttBrokerConfigViewModel.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/ViewModels/MqttBrokerConfigViewModel.cs):
     - Payload mặc định: `"offline"`.
     - Tự động thanh lọc khi gõ: `OnWillPayloadChanged` loại bỏ `"` và `\`.
     - Xác thực chủ đề: `ValidateWillTopic()` cập nhật `HasWillTopicError`, hiển thị lỗi đa ngôn ngữ qua `LocalizationService`, và vô hiệu hóa `SaveCommand` (`CanSave = !HasWillTopicError`).
   - [`SimplePLC.Studio/ViewModels/DtuGatewayStudioViewModel.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/ViewModels/DtuGatewayStudioViewModel.cs): Cập nhật `BuildMqttWillDto()` giải quyết token `{device}` và thanh lọc payload theo chuẩn SPLC-SPEC-MQTT-004.
   - [`SimplePLC.Studio/Controls/MqttBrokerConfigControl.xaml`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Controls/MqttBrokerConfigControl.xaml): Bổ sung dòng thông báo lỗi `WillTopicErrorMessage` và dòng hướng dẫn `MqttWillPayloadHint` sử dụng token ngữ nghĩa Fluent 2 (`DangerBrush`, `TextMutedBrush`).
   - [`SimplePLC.Studio/Services/LocalizationService.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Services/LocalizationService.cs): Bổ sung từ khóa `MqttWillPayloadHint` cho cả tiếng Anh và tiếng Việt.

### 17.3. Kết Quả Kiểm Thử & Nghiệm Thu Toàn Diện
1. **Kiểm tra Token Thiết kế XAML:**
   - Lệnh: `validate-xaml-tokens.ps1 -FilePath "SimplePLC.Studio/Controls/MqttBrokerConfigControl.xaml"`
   - Kết quả: **`[PASS]` 0 vi phạm thiết kế**.
2. **Kiểm tra Kiến trúc Backend:**
   - Lệnh: `validate-backend-arch.ps1`
   - Kết quả: **`[PASS]` 0 vi phạm Clean Architecture & IEC 61508-3**.
3. **Kiểm tra QA, TDD & Living Documentation:**
   - Lệnh: `validate-tdd-docs.ps1`
   - Kết quả: **`[PASS]` 0 vi phạm, 100% tỷ lệ pass của toàn bộ test suite**.
4. **Kiểm tra Tài liệu & Web Wiki Portal:**
   - Lệnh: `validate-docs.ps1`
   - Kết quả: **`[PASS]` 14/14 ICDs verified, VitePress clean build**.

---

## 13. Tách biệt Độc lập các Giao thức Cloud Uplink (MQTT Client, HTTP Client, TCP Client) & Dynamic Hardware Capabilities

### 13.1. Bối cảnh & Nguyên nhân gốc rễ (Root Cause)
- Trước đây, cấu hình Uplink của DTU Gateway bị gộp chung dưới một mục **Cloud Uplink** trên thanh điều hướng bên trái và chuyển đổi chế độ thông qua các nút chọn tròn (Radio Buttons `UplinkMode = "Mqtt" | "Http" | "TcpClient"`). Cơ chế này gặp hai hạn chế:
  1. **Thiếu tính độc lập:** Ép buộc các dịch vụ truyền tin hoạt động loại trừ lẫn nhau (exclusive), trong khi phần cứng công nghiệp thực tế (như SynaptiX SYNX-DTU-485) hỗ trợ chạy đồng thời nhiều giao thức nền (vừa duy trì kết nối MQTT telemetry/command, vừa gửi webhook HTTP REST và luồng TCP Socket khi cần).
  2. **Lỗi hiển thị sub-tab MQTT:** Trong quá trình tái cấu trúc phân trang, thanh phân đoạn 3 tab con của MQTT (`Broker Server & Security`, `Telemetry Publish`, `Command Subscribe`) gặp lỗi do đường dẫn Binding không khớp (`IsMqttTabBroker` thay vì `IsMqttSubSectionBroker`) dẫn đến việc WPF fallback mặc định hiển thị cả 3 khối cùng lúc trên một màn hình, đồng thời nhãn chữ bị hiển thị mã thô `DtuTabBrokerServer`.

### 13.2. Giải pháp Kỹ thuật Triệt để (First Principles Engineering)
1. **Phân tách Độc lập các Section trên Sidebar:**
   - Thay thế mục gộp `Cloud Uplink` bằng 3 section độc lập:
     - **`MQTT Client`** (Index 2): Cấu hình Broker Host, TLS/mTLS, Telemetry Topic, Qos, Retain, và Downlink Command Topic.
     - **`HTTP Client`** (Index 3): Cấu hình REST URL Endpoint, HTTP POST/PUT Method, Request Timeout, Trigger Mode, và Headers tùy chỉnh.
     - **`TCP Client`** (Index 4): Cấu hình Transparent TCP Socket Client/Server, Remote Host, Port, Heartbeat Ping, và Registration Packet.
     - **`Timing & Payload`** (Index 5): Giữ nguyên vị trí cấu hình chu kỳ phát tin định kỳ và trình định dạng xem trước JSON.
2. **Kích hoạt Độc lập (Non-Exclusive Activation):**
   - Bổ sung công tắc `HttpEnabled` và `TcpEnabled` độc lập (tương ứng với `MqttBrokerConfigVM.IsEnabled`). Người dùng có thể bật/tắt từng giao thức tùy theo năng lực phần cứng và ứng dụng thực tế.
3. **Bộ lọc Năng lực Phần cứng Động (Dynamic Hardware Capabilities):**
   - Liên kết thuộc tính `Visibility` của các mục sidebar với cờ bit năng lực: `HasMqttCapability`, `HasHttpCapability`, `HasTcpSocketCapability`. Trong chế độ thiết kế/Sandbox dự án (khi chưa gán cứng thiết bị vật lý), toàn bộ 3 giao thức đều sẵn sàng để lập trình viên cấu hình trước.
4. **Bảng Thanh tra Độc lập (Dedicated Right Drawer Inspectors):**
   - Xây dựng 2 bảng Inspector chuyên biệt cho HTTP Client và TCP Client theo chuẩn thiết kế **IBM Carbon 2-Column Key-Value**:
     - *HTTP Inspector:* URL Endpoint, HTTP Method, Request Timeout, Custom Headers summary, và số lượng trường dữ liệu.
     - *TCP Inspector:* Socket Mode, Remote Host, Remote Port, Heartbeat Ping, và Registration Mode.
5. **Sửa lỗi Phân trang 3 Sub-tab của MQTT:**
   - Chuẩn hóa tên khóa Localization: Ánh xạ chuẩn `MqttSubTabBroker`, `MqttSubTabPublish`, `MqttSubTabSubscribe` (kèm alias `DtuTabBrokerServer`, `DtuTabTelemetryPub`, `DtuTabCommandSub`).
   - Bọc từng `UserControl` (`MqttBrokerConfigControl`, `MqttTelemetryPublishControl`, `MqttCommandSubscribeControl`) trong một `StackPanel` độc lập có điều kiện `Visibility="{Binding IsMqttSubSectionBroker/Telemetry/Command, Converter={StaticResource BoolToVis}}"`, loại bỏ hoàn toàn hiện tượng hiển thị chồng chéo nhiều tab cùng lúc.

### 13.3. Kết quả Kiểm thử & Nghiệm thu
- **XAML Token Validator:** `[PASS] DtuGatewayStudioView.xaml` với 0 vi phạm thiết kế Fluent 2 / Carbon.
- **Backend Architecture Validator:** `[PASS]` 0 vi phạm kiến trúc Clean Architecture và IEC 61508-3.
- **QA/TDD Suite:** 92/92 unit test liên quan đến DTU Gateway Studio, Wizard và Lucide Icons đạt **100% PASS** trong ~494ms.


---

## 14. Thống nhất Kiến trúc & Đồng bộ Giao diện HTTP Client & TCP Client giữa Device Studio và FBD Dialogs

### 14.1. Bối cảnh & Yêu cầu Kỹ thuật
- Theo đúng chuẩn kiến trúc nền tảng đã thành công của **Modbus** và **MQTT**, hệ thống cấu hình giao thức cần đạt tính **tái sử dụng cao (Reusable Controls)** và tuân thủ nguyên tắc **Single Source of Truth** (Axiom 2 trong `AGENTS.md`):
  1. **Trích xuất UserControl độc lập:** Trích xuất phần thân cấu hình 2 cột chuyên sâu (bảng Visual Field Builder, Live JSON Payload Preview, Hex Stream Preview, Telemetry Diagnostics) thành 2 UserControl dùng chung:
     - `HttpClientConfigControl.xaml`
     - `TcpClientConfigControl.xaml`
  2. **Đồng bộ hóa 1:1:** Nhúng chính xác cùng một UserControl vào cả **Màn hình Cấu hình Thiết bị** (`DtuGatewayStudioView`) và **Hộp thoại Khối FBD** (`HttpClientConfigDialogView`, `TcpClientConfigDialogView`).
  3. **Trạng thái Disabled & Phản hồi Trực quan:** Khi người dùng tích bỏ chọn `Enable HTTP Client service on device` hoặc `Enable TCP Client service on device`, toàn bộ form cấu hình bên dưới phải tự động bị vô hiệu hóa (`IsEnabled="False"`, `Opacity="0.45"`) và hiển thị Notice Banner cảnh báo trực quan.
  4. **Khắc phục lỗi cắt chữ ComboBox (Text Clipping):** Mở rộng chiều rộng của ComboBox `HTTP Method` từ `160px` lên `220px` để chuỗi mô tả `"POST (Create / Push Telemetry)"` / `"POST (Tạo mới / Gửi viễn trắc)"` hiển thị trọn vẹn không bị cắt ngắn.

### 14.2. Các Thay đổi Triển khai (Implementation Summary)
1. **Trích xuất Controls dùng chung (`SimplePLC.Studio/Controls/`):**
   - `HttpClientConfigControl.xaml` & `.cs`: Chứa toàn bộ 3 nhóm cấu hình (Endpoint & Method, Trigger & Timing Mode, Payload Formatting & Visual Field Builder) và Sticky Live JSON Payload Preview bên phải.
   - `TcpClientConfigControl.xaml` & `.cs`: Chứa Socket Parameters, Framing Delimiter, Registration Packet, Heartbeat Ping, và Hex Stream Preview.
   - Chuẩn hóa phạm vi tìm kiếm Tag qua `AncestorType={x:Type controls:HttpClientConfigControl}`, đảm bảo hoạt động độc lập dù nằm trong Dialog Window hay UserControl.
2. **Đơn giản hóa Hộp thoại FBD:**
   - `HttpClientConfigDialogView.xaml` & `TcpClientConfigDialogView.xaml`: Giảm từ hơn 400 dòng mã xuống còn ~75 dòng, nhúng trực tiếp `HttpClientConfigControl` và `TcpClientConfigControl` kèm Action Footer (`Apply` / `Cancel`).
3. **Đồng bộ 2 chiều trong `DtuGatewayStudioViewModel.cs`:**
   - Sở hữu và liên kết hai ViewModel: `HttpVM` (`HttpClientConfigDialogViewModel`) và `TcpVM` (`TcpClientConfigDialogViewModel`).
   - Cập nhật 2 chiều thời gian thực qua các phương thức `On...Changed` và `PropertyChanged` listeners, tự động cập nhật Inspector summary cards và lưu trữ nhất quán vào `ProjectMetadata.DtuConfig.HttpSettings` & `TcpSettings`.
4. **Vô hiệu hóa Thông minh khi Bỏ tích (Disabled State with Industrial Dimming):**
   - Khi `HttpEnabled` hoặc `TcpEnabled` bằng `false`, khối cấu hình được bọc trong `<Border IsEnabled="{Binding HttpEnabled/TcpEnabled}">` với Trigger chuyển `Opacity="0.45"`.
   - Hiển thị Notice Banner với icon vector `IconSysAlertTriangle` thông báo trạng thái tạm tắt dịch vụ.
5. **Mở rộng Chiều rộng ComboBox `HTTP Method`:**
   - Điều chỉnh ColumnDefinition từ `160px` $\rightarrow$ `220px` trong `HttpClientConfigControl.xaml`.

### 14.3. Kết quả Thẩm định & Kiểm thử
- **XAML Token Validator:** `[PASS]` 0 vi phạm thiết kế trên cả 5 file XAML liên quan (`HttpClientConfigControl.xaml`, `TcpClientConfigControl.xaml`, `DtuGatewayStudioView.xaml`, `HttpClientConfigDialogView.xaml`, `TcpClientConfigDialogView.xaml`).
- **Backend Architecture Validator:** `[PASS]` 0 vi phạm kiến trúc Clean Architecture.
- **Unit Test Suite:** 146/146 unit test liên quan đến DTU, HttpClient, và TcpClient đạt **100% PASS**.

---

## 15. Tích hợp Chuẩn Giao tiếp Không dây Zigbee Modbus (Wire Contract V2.0 & SPLC-SPEC-ZIGBEE-006)

### 15.1. Bối cảnh & Yêu cầu Kỹ thuật
- Nhận tài liệu đề xuất cấu hình cổng Zigbee từ đội ngũ MCU (docs/firmware/zigbee_intergation.md & SPLC-SPEC-ZIGBEE-006).
- Thiết bị mục tiêu: **X.Link Zigbee 44** (STM32 MCU + CC2530/E18 Zigbee RF Module, 4 DI, 4 DO, 1 cổng RS-485 vật lý, 1 Anten Zigbee).
- Mục tiêu kiến trúc: Bảo vệ tính nhất quán của Clean Architecture, IEC 61131-3, IEC 61508-3, và First Principles Engineering (Axioms 1–4 trong AGENTS.md). Tuyệt đối không chắp vá định nghĩa gây méo mó mô hình thiết bị.

### 15.2. Các Quyết định Kiến trúc Cốt lõi (Architectural Invariants)
1. **Bản chất Giao diện Zigbee (Visual Truth - Axiom 2):**
   - Zigbee là một **Loại giao tiếp không dây riêng biệt (Interface Type: Wireless Modbus Transport)**, không phải là "cổng RS-485 số 5". Thiết bị X.Link 44 chỉ có 1 cổng RS-485 vật lý duy nhất.
   - Trên wire của các khối logic FBD (MB_READ, MB_WRITE), Zigbee được ánh xạ PortIndex = 4 (0-based raw value), trên UI hiển thị nhãn Zigbee (Wireless) thay vì RS485 Port 5.
2. **Bảo mật Phân vùng Dữ liệu (Isolation & Sandboxing):**
   - Cổng Zigbee chỉ đóng vai trò Slave truyền thông dữ liệu runtime qua bảng SLAVE-001 (đọc DI, ghi DO, trao đổi Holding Registers).
   - Tuyệt đối **KHÔNG mở quyền nạp Rule Table (x0000..0xA001)** qua sóng không dây Zigbee để phòng chống tấn công chèn mã độc vào bộ điều khiển công nghiệp.
3. **Cảnh báo Nguy cơ Bit 8 của ule_count:**
   - Firmware lưu cờ "Project có dùng Zigbee" vào Bit 8 của ule_count trong Flash nội MCU.
   - **Bắt buộc:** Khi trả lời các Function Code Modbus đọc ule_count (x0A00 hoặc header x9000), firmware phải thực hiện phép toán mặt nạ ule_count & 0x00FF trước khi đóng gói ra wire. Nếu để lọt Bit 8 ra Modbus, Studio đọc về số lượng rule $> 256$ sẽ làm tràn bộ nhớ và sập trình biên dịch/Rule Table parser.
4. **Cơ chế Khóa Mạng AES-128 & Bảo vệ Khóa Cũ:**
   - Vùng x1710..0x1717 (16 bytes AES key) là Write-Only, đọc luôn trả 0 để chống trích xuất khóa qua sóng RF.
   - Bổ sung cờ trạng thái KEY_SET (Bit 0 tại +36) để Studio biết thiết bị đã có khóa bảo mật hay chưa.
   - Bổ sung cờ điều khiển PRESERVE_KEY (Bit 5 tại +0) khi deploy cấu hình mới mà không làm mất khóa AES đang hoạt động trong Flash.
5. **Chuẩn hóa Preset Thiết bị & Header Firmware:**
   - Bổ sung preset phần cứng #8: XLINK_ZIGBEE_44 trong HardwareDevicePreset.cs và đa ngôn ngữ trong LocalizationService.cs.
   - Cấp phát cờ năng lực SPLC_HardwareCapabilities.ZIGBEE = 0x8000 (Bit 15).
   - Cập nhật chuẩn C Header cho Firmware simpleplc_protocol_v2_0.h với phân vùng x1700 - 0x173F và struct SPLC_ZigbeeConfigRecord_t (128 bytes).

### 15.3. Kết quả Kiểm thử & Nghiệm thu
- **Tài liệu Phản biện Chính thức:** Đã ban hành docs/firmware/SPLC-SPEC-ZIGBEE-006-APP-REVIEW.md làm căn cứ kỹ thuật cho đội MCU.
- **Protocol & Codec Tests:** 223/223 test trong SimplePLC.Protocol.Tests đạt **100% PASS** (bao gồm bộ kiểm thử toàn diện ZigbeeConfigCodecTests).
- **Domain & Application Tests:** Đạt **100% PASS** (Domain: 55/55, Application: 242/242, Infrastructure: 180/180).

## 15. Loại bỏ hoàn toàn Tab Timing & Payload và Chuẩn hóa 5 Section Độc lập

### 15.1. Bối cảnh & Yêu cầu Kỹ thuật
- Tab **Timing & Payload** trước đây chứa cấu hình chu kỳ quét cảm biến Modbus, chu kỳ đẩy tin lên Cloud, cơ chế kích hoạt On-Change, Live JSON preview và thẻ ước tính dung lượng dữ liệu mạng (Data Quota Estimator).
- **Nguyên nhân loại bỏ (Root Cause):**
  - Từng giao thức truyền thông độc lập trong hệ thống (`RS-485 Modbus`, `MQTT Client`, `HTTP Client`, `TCP Client`) đã sở hữu sẵn các tham số chu kỳ quét (Scan Cycle / Response Timeout), chế độ phát tin (Cyclic / Periodic / Event / On-Change / Heartbeat Ping) và bộ định dạng gói tin (Payload Field Builder) cục bộ.
  - Sự tồn tại của tab Timing & Payload tách rời gây ra hiện tượng **Duplicate Source of Truth** (vi phạm Axiom 2 trong `AGENTS.md`) và làm người vận hành nhầm lẫn về việc chu kỳ nào thực sự điều khiển việc gửi tin.
- **Yêu cầu thực thi:** Xóa bỏ hoàn toàn tab này trên giao diện XAML, ViewModel điều hướng và bộ unit test, không di chuyển các trường dư thừa sang tab khác.

### 15.2. Các thay đổi đã thực hiện
1. **Giao diện người dùng (`DtuGatewayStudioView.xaml`):**
   - Loại bỏ RadioButton mục 5 (Timing & Payload) trên Sidebar điều hướng bên trái. Thanh Sidebar chỉ còn đúng 5 mục:
     - Mục 0: `RS-485 Modbus` (`CommandParameter="0"`)
     - Mục 1: `Wi-Fi Network` (`CommandParameter="1"`)
     - Mục 2: `MQTT Client` (`CommandParameter="2"`)
     - Mục 3: `HTTP Client` (`CommandParameter="3"`)
     - Mục 4: `TCP Client` (`CommandParameter="4"`)
   - Xóa bỏ khối nội dung trung tâm `IsSectionTiming` (`Scan & Publish Interval Settings`).
   - Xóa bỏ bảng giám sát `Live Real-Time JSON Telemetry` và thẻ tính toán `Monthly Data Quota Estimator` ở Inspector bên phải.
2. **ViewModel (`DtuGatewayStudioViewModel.cs`):**
   - Xóa cờ `IsSectionTiming` và thông báo thuộc tính `[NotifyPropertyChangedFor(nameof(IsSectionTiming))]`.
   - Cập nhật dải giá trị của `_selectedSectionIndex` từ 0 đến 4.
   - Loại bỏ các nhánh `5 => ...` trong `ActiveInspectorTitle` và `ActiveInspectorSubtitle`.
   - Loại bỏ thuộc tính `TimingStatusBadge`.
3. **Unit Tests:**
   - Cập nhật [`DtuGatewayStudioViewModelTests.cs`](file:///SimplePLC.Studio.Tests/DtuGatewayStudioViewModelTests.cs): Loại bỏ các assertion kiểm tra `IsSectionTiming` và `SelectSection(5)`.
   - Cập nhật [`DtuQuickSetupWizardViewModelTests.cs`](file:///SimplePLC.Studio.Tests/DtuQuickSetupWizardViewModelTests.cs): Xác thực chuẩn chu trình chuyển tab 0..4.

### 15.3. Kết quả Thẩm định
- **XAML Token Validator:** `[PASS]` 0 vi phạm thiết kế trên `DtuGatewayStudioView.xaml`.
- **Backend Architecture Validator:** `[PASS]` 0 vi phạm kiến trúc Clean Architecture.
- **Unit Test Suite:** 1,215/1,215 test trong `SimplePLC.Studio.Tests` đạt **100% PASS** (gồm 69/69 test DTU Studio và 21/21 test Quick Setup Wizard).


---

## 16. Phân định Danh xưng Device / System & Lọc Ngoại vi theo Năng lực Phần cứng (Hardware Capabilities)

### 16.1. Bối cảnh & Nguyên lý Thiết kế (First Principles)
- **Giải quyết Xung đột Danh xưng (Duplicate Naming):** Menu thanh công cụ phía trên (`Device`) dùng cho các tác vụ vật lý cấp phần cứng (Connect, Upload, Deploy, Soft Reboot, Factory Reset). Tab workspace số 0 trước đây cũng mang tên `Device`, gây nhầm lẫn. Đã thống nhất đổi tên Tab 0 thành **`System`** (Tiếng Việt: **`Hệ thống`**).
- **Axiom 1 & Axiom 2 (Hardware Ground Truth & Visual Truth):** Giao diện không được hiển thị hoặc cho phép cấu hình các ngoại vi truyền thông mà phần cứng MCU không trang bị.
- **Tự động Ẩn Tab & Điều hướng Dự phòng (Zero-Config Fallback):** Đối với các dòng thiết bị không có ngoại vi truyền thông (ví dụ DataLogger hoặc thiết bị ghi chép SD/RTC đơn thuần), Tab `System` tự động ẩn hoàn toàn khỏi thanh điều hướng (`HasSystemTab == false`) và ứng dụng tự động mở trực tiếp vào tab `Logic Graph` (FBD).

### 16.2. Các Thay đổi Triển khai
1. **Localization (`LocalizationService.cs`):**
   - Tiếng Việt: `["TabDevice"] = "Hệ thống"`, `["MenuViewDevice"] = "Hệ thống (System)"`.
   - Tiếng Anh: `["TabDevice"] = "System"`, `["MenuViewDevice"] = "System Workspace"`.
2. **ViewModel (`DtuGatewayStudioViewModel.cs` & `MainViewModel.cs`):**
   - Bổ sung `HasRs485Capability` và `HasSystemCapability` trong `DtuGatewayStudioViewModel`.
   - Bổ sung phương thức `SelectFirstAvailableSection()` để tự động fallback sang section ngoại vi hợp lệ đầu tiên khi phần cứng không hỗ trợ RS-485 hoặc Wi-Fi.
   - Thêm các thuộc tính nhận diện năng lực phần cứng trong `MainViewModel`: `HasSystemTab`, `IsMqttSupported`, `IsWifiSupported`, `IsRs485Supported`, `IsRtcClockSupported` và phương thức kích hoạt `NotifyHardwareCapabilities()`.
   - Nếu `HasSystemTab == false` khi mở dự án hoặc nhận diện phần cứng, `MainViewModel.SelectedTabIndex` tự động chuyển sang `AppTabIndices.LogicGraph`.
3. **Giao diện XAML (`MainWindow.xaml` & `DtuGatewayStudioView.xaml`):**
   - `MainWindow.xaml`: Ràng buộc `IsEnabled` của menu `MenuMqttBrokerConfig`, `MenuWifiConfig`, `MenuModbusPortConfig`, `BtnSyncMcuClock` theo các cờ năng lực tương ứng. Ràng buộc `Visibility` của Tab 0 với `{Binding HasSystemTab, Converter={StaticResource BoolToVis}}`.
   - `DtuGatewayStudioView.xaml`: Ràng buộc `Visibility` của Navigation Item 0 (RS-485) với `HasRs485Capability` và Item 1 (Wi-Fi) với `HasWifiCapability`.
4. **Unit Tests (`HardwareCapabilityMenuAndTabTests.cs`):**
   - Viết trọn bộ 4 test suites:
     - `Localization_TabDevice_ReturnsSystemInEnglishAndHeThongInVietnamese`
     - `DtuGatewayStudioViewModel_Capabilities_ReflectTargetHardwareAccurately` (Kiểm thử `DTU_RS485_NET`, `GATEWAY_RS485_ETH`, `DATALOGGER`)
     - `DtuGatewayStudioViewModel_SelectFirstAvailableSection_FallsBackCorrectly`
     - `DtuGatewayStudioViewModel_SelectFirstAvailableSection_SkipsWifiWhenDisabled`

### 16.3. Kết quả Thẩm định
- **XAML Token Validator:** `[PASS]` 0 vi phạm trên cả `MainWindow.xaml` và `DtuGatewayStudioView.xaml`.
- **Backend Architecture Validator:** `[PASS]` 0 vi phạm Clean Architecture & Real-Time invariants.
- **QA/TDD & Docs-as-Code Validator:** `[PASS]` 0 vi phạm, 100% test pass rate (1,219/1,219 test suite trong `SimplePLC.Studio.Tests`).


---

## 17. Chuẩn hóa Ngôn ngữ & Khắc phục Lỗi Lẫn lộn Anh - Việt trên MQTT Telemetry Inspector

### 17.1. Bối cảnh & Nguyên lý Thiết kế (First Principles & Axiom 2 - Visual Truth)
- **Visual Truth & Language Consistency:** Toàn bộ ứng dụng SynaptiX IDE hỗ trợ chuyển đổi đa ngôn ngữ động giữa Tiếng Việt và Tiếng Anh (`LocalizationService`). Tuy nhiên, trên màn hình System > MQTT Client, khu vực Panel bên phải (`MQTT TELEMETRY & STATUS`) xuất hiện tình trạng lẫn lộn song ngữ Anh - Việt nghiêm trọng (các nhãn Uplink/Downlink ghép nửa Anh nửa Việt, chuỗi cảnh báo QoS và tiêu đề kiểm tra an ninh bị hardcode tiếng Việt trong khi giao diện đang chạy Tiếng Anh).
- **Thuật ngữ Tự động hóa / IoT Công nghiệp Chuẩn mực:** Loại bỏ hoàn toàn bản dịch thô (literal translation) từ *"Will"* thành *"Di chúc"* hoặc *"Báo tử"* trong cụm *Last Will & Testament (LWT)*. Chuẩn hóa thành thuật ngữ công nghiệp chuẩn: *"Thông điệp LWT ngoại tuyến"* / *"Bản tin LWT báo mất kết nối"*.
- **Toàn vẹn Dữ liệu Trình diễn (Presentation Integrity):**
  - Sửa lỗi binding `MqttTopic` bị sai tên trong XAML (tên thực tế trong ViewModel là `MqttPublishTopic`), khiến ô Uplink Topic trước đây bị trắng trơn.
  - Sửa lỗi binding `MqttTlsEnabled` (thuộc tính không tồn tại trong ViewModel) kết hợp sai Converter `BoolToVis` cho thuộc tính chuỗi, dẫn đến giá trị luôn rơi vào fallback `'Tắt'`. Chuyển sang bind đúng thuộc tính chuẩn `MqttTlsSecurityDisplay`.
  - Thay thế chuỗi hardcode `"Username / Password"` bằng binding động tới `MqttAuthTypeDisplay` (hỗ trợ cả Anonymous, Username/Password, và mTLS x509).

### 17.2. Các Tệp Mã Nguồn Đã Cập Nhật
1. **`SimplePLC.Studio/Services/LocalizationService.cs`:**
   - **Từ điển Tiếng Việt:**
     - `["InspectorTopicFlowTitle"]`: "Kiến Trúc Chủ Đề Cloud 2 Chiều"
     - `["InspectorTopicFlowDesc"]`: "Kiến trúc chủ đề 2 chiều Uplink, Downlink và LWT ngoại tuyến"
     - `["InspectorUplinkPublishLabel"]`: "▲ UPLINK (Xuất bản Telemetry):"
     - `["InspectorDownlinkSubscribeLabel"]`: "▼ DOWNLINK (Đăng ký nhận lệnh):"
     - `["InspectorLwtLabel"]`: "⚡ LWT (Báo ngoại tuyến):"
     - `["InspectorQos1GuaranteeText"]`: "Mức dịch vụ: QoS 1 đảm bảo gửi tin cậy với xác nhận PUBACK."
     - `["InspectorMqttSecurityTitle"]`: "Kiểm Tra An Ninh & Bảo Mật"
     - `["InspectorMqttPortLabel"]`: "Cổng kết nối:"
     - `["InspectorMqttTlsLabel"]`: "Mã hóa TLS/SSL:"
     - `["InspectorIdentityAuth"]`: "Xác thực danh tính:"
     - Chuẩn hóa các mục LWT: `CardMqttLwt`, `MqttWillSectionTitle`, `MqttWillEnableLabel`, `DtuMqttLwtTitle`, `DtuMqttLwtTopicLabel`, `DtuMqttLwtRetainLabel`, `DtuMqttLwtPayloadLabel`.
   - **Từ điển Tiếng Anh:**
     - `["InspectorUplinkPublishLabel"]`: "▲ UPLINK (Telemetry Publish):"
     - `["InspectorDownlinkSubscribeLabel"]`: "▼ DOWNLINK (Command Subscribe):"
     - `["InspectorLwtLabel"]`: "⚡ LWT (Last Will & Testament):"
     - `["InspectorQos1GuaranteeText"]`: "Service Level: QoS 1 ensures telemetry delivery with PUBACK verification."
     - `["InspectorMqttSecurityTitle"]`: "Security & Port Overview"
     - `["InspectorMqttPortLabel"]`: "Connection Port:"
     - `["InspectorMqttTlsLabel"]`: "TLS/SSL Encryption:"
     - `["InspectorIdentityAuth"]`: "Authentication:"

2. **`SimplePLC.Studio/Views/DtuGatewayStudioView.xaml`:**
   - Thay thế toàn bộ 100% các chuỗi hardcode tại Card 1 (Uplink, Downlink, LWT, QoS note) và Card 2 (Tiêu đề an ninh, Cổng kết nối, TLS/SSL, Xác thực danh tính) sang dynamic binding thông qua `{Binding [...], Source={x:Static services:LocalizationService.Instance}}`.
   - Bind `MqttPublishTopic` chuẩn xác để ô Uplink Topic hiển thị địa chỉ topic thật thay vì rỗng.
   - Bind `MqttTlsSecurityDisplay` và `MqttAuthTypeDisplay` để trạng thái mã hóa và kiểu xác thực tự động đổi theo cấu hình và ngôn ngữ đang chọn.

3. **`SimplePLC.Studio/ViewModels/DtuGatewayStudioViewModel.cs`:**
   - Thêm thuộc tính alias 2 chiều `public string MqttTopic { get => MqttPublishTopic; set => MqttPublishTopic = value; }` đảm bảo tương thích ngược hoàn toàn.
   - Bổ sung `OnPropertyChanged(nameof(MqttTopic))` trong `NotifyMqttPubSummary()` và `OnMqttPublishTopicChanged()`.
   - Cập nhật phương thức `OnLanguageChanged()` phát tín hiệu cập nhật cho các huy hiệu sidebar: `MqttSidebarBadgeText`, `HttpSidebarBadgeText`, `TcpSidebarBadgeText`.

### 17.3. Kết quả Thẩm định Chất lượng & Build
- **XAML Token Validator:** `[PASS]` 0 vi phạm thiết kế trên `DtuGatewayStudioView.xaml`.
- **Backend Architecture Validator:** `[PASS]` 0 vi phạm Clean Architecture.
- **QA/TDD & Docs-as-Code Validator:** `[PASS]` 0 vi phạm tài liệu, 100% pass rate.
- **Dotnet Build & Test Suite:**
  - `dotnet build SimplePLC.sln`: **0 Error(s), 0 Warning(s)**.
  - `dotnet test SimplePLC.sln --no-build`: **1,919 / 1,919 test pass 100%** (Domain: 55, Protocol: 223, Infrastructure: 180, Application: 242, Studio.Tests: 1219).


---

## 18. Tối ưu Công thái học Giao diện (ISA-101 Typography & Color Refinement) trên MQTT Inspector

### 18.1. Bối cảnh & Nguyên lý Thiết kế (First Principles & ANSI/ISA-101)
- **Ergonomics & Information Hierarchy (Microsoft Fluent 2 Desktop):**
  - Khắc phục tình trạng cỡ chữ bị vụn vặt và quá bé (`9.5px` và `10px`). Nâng kích thước tối thiểu các nhãn lên `11px` SemiBold và nội dung ghi chú QoS lên `11px` với `LineHeight="15"`.
  - Nâng kích thước phông Monospace (`Cascadia Code, Consolas`) cho các trường Topic từ `10.5px` lên `11.5px` để các đường dẫn URI MQTT (`synaptix/telemetry/data`) hiển thị sắc nét, không bị dính nét hay mỏi mắt khi vận hành dài ca.
  - Đồng bộ kích thước phông của Card 2 (Security & Port Overview): tất cả nhãn và giá trị (`Port 1883`, `Disabled`, `None`) đều đạt chuẩn `11px` SemiBold, loại bỏ chênh lệch cỡ chữ giữa các hàng.
  - Đồng bộ padding và margin của các Card về chuẩn `Padding="12,11"` và `Margin="0,0,0,10"`.
- **Tuân thủ Tuyệt đối Quy tắc 90/10 Grayscale (ANSI/ISA-101):**
  - **Loại bỏ hoàn toàn màu cảnh báo sai mục đích:** Nhãn `▼ DOWNLINK` trước đây dùng màu cam (`WarningBrush`) và nhãn `⚡ LWT` dùng màu đỏ (`DangerBrush`). Trong môi trường công nghiệp, màu đỏ/cam chỉ được phép xuất hiện khi có sự kiện báo động thật sự (Alarm/Fault/Trip/Warning). Việc tô đỏ cho nhãn cấu hình tĩnh khiến kỹ sư nhầm tưởng hệ thống đang báo sự cố.
  - Chuyển toàn bộ các nhãn phân loại (Uplink, Downlink, LWT) sang tông màu trung tính `{DynamicResource TextSecondaryBrush}` với `FontWeight="SemiBold"`.

### 18.2. Các Thay đổi Cụ thể trong `DtuGatewayStudioView.xaml`
- **Header Dock:**
  - `ActiveInspectorTitle`: `12px` `SemiBold`.
  - `ActiveInspectorSubtitle`: `11px` (`TextMutedBrush`).
- **Card 1 (Topic Architecture):**
  - `InspectorTopicFlowTitle`: `12px` `SemiBold`.
  - `InspectorTopicFlowDesc`: `11px` (`LineHeight="15"`).
  - Inset Box: `Padding="12,10"`.
  - Nhãn Uplink, Downlink, LWT: `11px` `SemiBold` `{DynamicResource TextSecondaryBrush}` `Margin="0,0,0,3"`.
  - Chuỗi Topic: `11.5px` `Cascadia Code, Consolas` `{DynamicResource TextPrimaryBrush}` `Margin="0,0,0,10"`.
  - QoS Note: `11px` `{DynamicResource TextSecondaryBrush}` `LineHeight="15"`.
- **Card 2 (Security & Port Overview):**
  - Header: `12px` `SemiBold`.
  - Container Padding: `12,11`.
  - Nhãn (Port, TLS, Auth): `11px` `{DynamicResource TextSecondaryBrush}`.
  - Giá trị (Port 1883, TLS State, Auth Type): `11px` `SemiBold` `{DynamicResource TextPrimaryBrush}`.

### 18.3. Kết quả Thẩm định
- **XAML Design Token Validator:** `[PASS]` 0 vi phạm trên `DtuGatewayStudioView.xaml`.
- **Backend Architecture Validator:** `[PASS]` 0 vi phạm Clean Architecture.
- **Biên dịch:** `dotnet build SimplePLC.sln` $\rightarrow$ **0 Errors, 0 Warnings**.


---

## 🛠️ Fix: Device Configuration Persistence & Save Configuration Command

### Vấn đề giải quyết:
- Nút **`[Save Configuration]`** trong màn hình System (Device Configuration) bị liệt do hàm `SaveConfiguration()` trong `DtuGatewayStudioViewModel.cs` thiếu attribute `[RelayCommand]`.
- Cấu hình chỉ ghi vào RAM `_metadata` mà chưa được lưu bền vững vào tệp dự án `.splc` trên đĩa.
- Các thuộc tính cấu hình cổng RS-485 trên `ModbusPortConfigVM` bị ghi đè ngược bởi giá trị mặc định của Port 0.

### Giải pháp kỹ thuật:
1. **Attribute `[RelayCommand]`**: Thêm attribute cho `SaveConfiguration()` và `Save()` để CommunityToolkit.Mvvm tự động sinh ra `SaveConfigurationCommand`.
2. **Callback `RequestSaveProject`**: Kết nối `DtuGatewayStudioViewModel` với `MainViewModel` để tự động lưu dự án ra file `.splc` (`SaveProject()`) và đánh dấu `IsProjectDirty = true`.
3. **Đồng bộ tham số 2 chiều cho Modbus Ports**: Đồng bộ `DataBits`, `Parity`, `StopBits`, `ResponseTimeoutMs`, `RetryCount`, `InterFrameDelayMs` giữa ViewModel chính và `ModbusPortConfigVM.SelectedPort` trước khi ghi, tránh xung đột và mất dữ liệu.
4. **Unit Tests**: Bổ sung kiểm thử `SaveConfigurationCommand_WhenExecuted_SavesToMetadataAndTriggersRequestSaveProject` và `SaveConfigurationCommand_WithModbusPortConfigVM_PreservesMultiPortConfigurationsWithoutClobbering` (100% Pass rate).

- **Cập nhật kiến trúc Commit vs File Save**: Nút `Save Configuration` đóng vai trò commit dữ liệu vào `ProjectMetadata` (RAM) và đánh dấu `IsProjectDirty = true`, không ép ghi tệp đĩa `.splc` (nhường quyền cho `Ctrl+S` và `File -> Save`).
- **Thông báo trạng thái song ngữ Anh-Việt rõ ràng**:
  - Tiếng Việt: `● Đã áp dụng cấu hình dự án — sẵn sàng nạp thiết bị (HH:mm:ss)`
  - Tiếng Anh: `● Configuration committed to project — ready to deploy (HH:mm:ss)`

- **Gỡ bỏ Quick Wizard thừa thãi**: Loại bỏ hoàn toàn các nút `[Quick Wizard]` tại tiêu đề Section RS-485 Modbus và Section MQTT Client trong `DtuGatewayStudioView.xaml`. Người dùng thao tác trực tiếp trên giao diện tab System mượt mà, không bị che khuất hay mở popup lồng nhau.

---

## 7. Xác thực & Khắc phục Luồng Tạo Dự án Thiết bị (Device Preset) & Màn hình System

### 7.1. Hiện trạng & Lỗ hổng đã phát hiện khi tạo dự án thiết bị (DTU Gateway)
1. **Thiếu ánh xạ `HardwareCapabilities` và `PhysicalRs485PortCount` trong `ApplyNewProject`:**
   - Khi chọn mẫu thiết bị (preset) DTU hoặc Gateway rồi bấm tạo dự án, `CurrentProjectMetadata.HardwareCapabilities` không được gán (giá trị 0).
   - `PhysicalRs485PortCount` không được truyền từ preset vào metadata.
2. **Preset DTU chưa định nghĩa số cổng RS-485:**
   - `HardwareDevicePreset.BuildProductDefinition()` không truyền `physicalRs485PortCount: PhysicalRs485PortCount`, khiến `ProductDefinition` mặc định gán 2 cổng thay vì 4 cổng vật lý cho trạm DTU.
3. **Chưa kích hoạt `DtuConfig.Enabled` khi tạo dự án DTU:**
   - Khi tạo dự án DTU mới, `CurrentProjectMetadata.DtuConfig.Enabled` vẫn mang giá trị `false`.
4. **Thiếu nhánh `SPLC_DeviceClass.DTU` trong hiển thị trạng thái thiết bị (`UpdateDeviceProductInfo` & `ShowDeviceInfo`):**
   - Phân loại `DTU` rơi vào nhánh mặc định `Thiết bị`, đồng thời do DTU không có I/O cục bộ nên `ioSummary` hiển thị `"0 I/O"`. Dẫn đến thanh trạng thái hiển thị `"Thiết bị (0 I/O)"` thay vì `"DTU Gateway (4x RS-485)"`.
5. **Đồng bộ khi mở lại tệp dự án (`LoadProjectFile`):**
   - Khi nạp tệp `.splc`, `DeviceProfileBuilder.TryBuild` chưa truyền `hardwareCapabilities` và `physicalRs485PortCount`, khiến dự án DTU sau khi mở lại mất cờ năng lực phần cứng.

### 7.2. Các giải pháp kỹ thuật đã áp dụng
1. **[`HardwareDevicePreset.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Models/HardwareDevicePreset.cs):**
   - Thêm thuộc tính `PhysicalRs485PortCount` (mặc định 2, riêng `DTU_RS485_NET` là 4).
   - Truyền `physicalRs485PortCount: PhysicalRs485PortCount` trong `BuildProductDefinition()`.
2. **[`MainViewModel.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/ViewModels/MainViewModel.cs):**
   - Trong `ApplyNewProject`: Cập nhật `AppServices.Instance.CurrentProduct = product`, khởi tạo `CurrentProjectMetadata` với đầy đủ `PhysicalRs485PortCount = product.PhysicalRs485PortCount`, `HardwareCapabilities = (uint)product.HardwareCapabilities`, và `DtuConfig = new DtuConfigModel { Enabled = isDtu }`.
   - Trong `UpdateDeviceProductInfo`: Thêm nhánh `SPLC_DeviceClass.DTU => "DTU Gateway"` và `ioSummary = $"{meta.PhysicalRs485PortCount}x RS-485"`.
   - Trong `ShowDeviceInfo`: Thêm `SPLC_DeviceClass.DTU` và variant text `"4x RS-485 / Net"`.
   - Trong `LoadProjectFile`: Truyền `hardwareCapabilities` và `physicalRs485PortCount` vào `DeviceProfileBuilder.TryBuild`, bảo toàn `AppServices.Instance.CurrentProduct`.
3. **Kiểm thử tự động TDD:**
   - Bổ sung 2 kiểm thử trong [`NewProjectWizardTests.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio.Tests/NewProjectWizardTests.cs):
     - `MainViewModel_NewProject_WithDtuPreset_SelectsSystemTabAndInitializesAllHardwareParameters`
     - `SaveAndReloadProject_WithDtuHardware_RestoresFullHardwareCapabilitiesAndSystemTab`
   - Đạt 100% Pass Rate trên toàn bộ giải pháp (1,616+ unit tests).

### 7.3. Cập nhật Năng lực Phần cứng Chuẩn cho Gateway & Data Logger
Theo trao đổi định hướng cấu hình phần cứng:
1. **SynaptiX Gateway (`GATEWAY_RS485_ETH`):**
   - Đã kích hoạt cờ `SPLC_HardwareCapabilities.WIFI`.
   - Giờ đây khi mở dự án Gateway, màn hình System hiển thị đầy đủ các mục: **RS-485 Modbus (2 cổng vật lý)**, **Mạng Wi-Fi**, **MQTT Client**, **HTTP Client**, **TCP Client**.
2. **SynaptiX Data Logger (`DATALOGGER_4DI_2DO_4AI`):**
   - Đã kích hoạt cờ `SPLC_HardwareCapabilities.MODBUS_MASTER | SPLC_HardwareCapabilities.WIFI | SPLC_HardwareCapabilities.MQTT_PUB | SPLC_HardwareCapabilities.MQTT_SUB`.
   - Giờ đây khi mở dự án Data Logger, Tab System hiển thị trên thanh điều hướng đỉnh, và màn hình System hiển thị chính xác các mục: **RS-485 Modbus (2 cổng vật lý)**, **Mạng Wi-Fi**, **MQTT Client**. Các mục không thuộc phần cứng (HTTP/TCP passthrough) được ẩn tự động.
3. **Phần cứng có dây LAN/Ethernet:**
   - Thống nhất chưa cần giao diện cấu hình mạng LAN có dây ở giai đoạn hiện tại, khi cần sẽ bổ sung sau.
4. **Kết quả kiểm thử:**
   - Bổ sung 2 bài kiểm thử tự động `MainViewModel_NewProject_WithGatewayPreset_HasWifiMqttRs485_AndSystemTab` và `MainViewModel_NewProject_WithDataloggerPreset_HasWifiMqttRs485_AndSystemTab`.
   - 1,927 / 1,927 unit tests toàn bộ solution đạt 100% Pass Rate.

### 7.4. Khắc phục Lỗi Giao diện System không Làm mới khi Khởi tạo Dự án Data Logger
1. **Nguyên nhân gốc rễ (Root Cause Analysis từ ảnh thực tế của người dùng):**
   - Khi SynaptiX IDE khởi động, ứng dụng mặc định ở Tab 0 (`SelectedTabIndex == 0`, System view) với cấu hình dự án rỗng (`Remote I/O`, chưa bật Wi-Fi/MQTT và số cổng RS-485 mặc định cũ là 4).
   - Khi người dùng bấm **File > New Project** và chọn **SynaptiX Data Logger (4DI / 2DO / 4AI)**:
     - Phương thức `ApplyNewProject` tạo mới `CurrentProjectMetadata` (với 2 cổng RS-485, Wi-Fi, MQTT).
     - Bộ lắng nghe `OnCurrentProjectMetadataChanged` khởi tạo một `DtuGatewayStudioViewModel` mới.
     - Tuy nhiên, do điều kiện nhánh trước đó chỉ gọi `SelectTab(0)` nếu `IsDtuProject == true`, trong khi Data Logger là `DeviceClass.DATALOGGER` (`IsDtuProject == false`) và `SelectedTabIndex` vốn đã bằng 0, nên `SelectTab` không được kích hoạt!
     - Kết quả: `CurrentView` trong WPF vẫn bị neo vào đối tượng ViewModel cũ lúc khởi động (chỉ có RS-485 và có 4 cổng, không có Wi-Fi/MQTT), dù huy hiệu trạng thái góc dưới bên phải đã hiển thị đúng `Datalogger (4DI / 2DO / 4AI)`.
   - Ngoài ra, giá trị mặc định của `PhysicalRs485PortCount` trong `ProjectMetadata` là 4 (thay vì 2).

2. **Các chỉnh sửa đã áp dụng:**
   - **[`MainViewModel.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/ViewModels/MainViewModel.cs):**
     - Đổi `DtuGatewayStudioVM` thành `[ObservableProperty] private DtuGatewayStudioViewModel _dtuGatewayStudioVM;`.
     - Trong `OnCurrentProjectMetadataChanged`: bổ sung nhánh `else if (SelectedTabIndex == 0) { CurrentView = DtuGatewayStudioVM; }` để ép buộc WPF cập nhật ngay lập tức `CurrentView` trỏ đến ViewModel cấu hình mới của thiết bị.
   - **[`ProjectModel.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Models/ProjectModel.cs):**
     - Đặt giá trị mặc định `PhysicalRs485PortCount = 2`.
   - **Kiểm thử tự động TDD:**
     - [`NewProjectWizardTests.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio.Tests/NewProjectWizardTests.cs): Xác thực `Assert.Same(mainVM.DtuGatewayStudioVM, mainVM.CurrentView)` cùng các thuộc tính 2 cổng RS-485, Wi-Fi, MQTT khi tạo dự án Data Logger khi đang đứng ở Tab 0.
     - [`ModbusPortConfigViewModelTests.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio.Tests/ModbusPortConfigViewModelTests.cs): Cập nhật kiểm thử số lượng cổng theo giới hạn vật lý phần cứng (2 cổng cho mặc định, 4 cổng cho DTU).

3. **Kết quả xác thực:**
   - Đạt 100% Pass Rate trên toàn bộ giải pháp (1,927 / 1,927 unit tests).
   - Đạt `[PASS]` tất cả các bộ script kiểm tra XAML Token, Clean Architecture, và Living Docs.

### 7.5. Bổ sung Nút Nhận diện Thiết bị (Detect Device) trên Hộp thoại New Project
1. **Bối cảnh & Tối ưu hóa Trải nghiệm Người dùng (Ergonomics):**
   - Trong hộp thoại **New Project Configuration**, trước đây nút nhận diện thiết bị bị đặt ở cuối cột bên trái mà không có `ScrollViewer`, dẫn đến việc bị đẩy ra ngoài vùng nhìn thấy (clipping) trên các màn hình có tỉ lệ thu phóng (125% - 150% DPI).
   - Người dùng cần một nút nhận diện thiết bị trực quan, đặt ngay tại khu vực cấu hình phần cứng mục tiêu (`TARGET HARDWARE SPECIFICATION`) để đọc thông số thực tế từ bộ điều khiển đang kết nối.

2. **Các chỉnh sửa đã triển khai:**
   - **[`NewProjectDialogView.xaml`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Views/NewProjectDialogView.xaml):**
     - Đặt nút **`Nhận diện thiết bị`** (`Detect Device`) ngay trên thanh tiêu đề của card `TARGET HARDWARE SPECIFICATION`, đi kèm biểu tượng vector `IconIecTimerCycle`.
     - Tăng kích thước hộp thoại thành `Width="900" Height="610"` (MinWidth 860, MinHeight 540).
     - Bọc cột bên trái trong `<ScrollViewer VerticalScrollBarVisibility="Auto" HorizontalScrollBarVisibility="Disabled">` để triệt tiêu 100% nguy cơ tràn giao diện trên mọi độ phân giải màn hình.
   - **[`NewProjectViewModel.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/ViewModels/NewProjectViewModel.cs):**
     - Nâng cấp `DetectFromConnectedDevice()`: tự động đọc `ProductDefinition` từ active `IDeviceSession`, đồng bộ hóa việc chọn Preset tương ứng trên ComboBox (VD: nếu thiết bị là DTU thì ComboBox tự nhảy sang DTU Gateway), cập nhật toàn bộ bảng xem trước (Preview) thông số tài nguyên I/O và số cổng RS-485.
     - Hiển thị phản hồi trạng thái: thông báo thành công xanh (`NewProjectDeviceDetectedSuccess`) hoặc cảnh báo cam/đỏ (`NewProjectNoDeviceConnected`) nếu chưa kết nối thiết bị.
   - **[`LocalizationService.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Services/LocalizationService.cs):**
     - Chuẩn hóa nhãn nút: Tiếng Việt `"Nhận diện thiết bị"`, Tiếng Anh `"Detect Device"`.
     - Cập nhật tooltip hướng dẫn đầy đủ.

3. **Kiểm thử tự động TDD:**
   - Bổ sung 2 bài kiểm thử trong [`NewProjectWizardTests.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio.Tests/NewProjectWizardTests.cs):
     - `NewProjectViewModel_DetectFromConnectedDevice_WhenNoActiveSession_SetsWarning`: Kiểm thử khi chưa có kết nối thiết bị thì hiển thị cảnh báo hướng dẫn người dùng kết nối.
     - `NewProjectViewModel_DetectFromConnectedDevice_WithActiveSession_AutoSelectsPreset`: Kiểm thử khi có kết nối thiết bị thì tự động phát hiện, tự động chọn đúng Preset tương ứng trên ComboBox và cập nhật toàn bộ thông số xem trước.
   - 1,929 / 1,929 tests toàn bộ solution đạt **100% PASS**.
   - `validate-xaml-tokens.ps1`: `[PASS]` 0 vi phạm.
   - `validate-backend-arch.ps1`: `[PASS]` 0 vi phạm.

### 7.6. Sửa Lỗi Hiển thị Icon trên Nút Detect Device (Uniform Scaling & Kính lúp)
1. **Nguyên nhân:**
   - Thẻ `<Path>` trước đó không được áp dụng Style chuẩn `{StaticResource IecStatusIconStyle}` nên thiếu thuộc tính `Stretch="Uniform"`.
   - Vì vậy, với một biểu tượng kích thước gốc 24x24 (Lucide vector), khi vẽ vào khung kích thước 11x11 mà không có co giãn (Stretch), WPF đã cắt bỏ toàn bộ phần thân icon và chỉ hiển thị một góc 11x11 ở góc trên cùng bên trái (khiến icon trông giống một nét gạch cong cụt `/`).
2. **Khắc phục:**
   - Chuyển sang biểu tượng vector kính lúp chuẩn nhận diện / tìm kiếm: `IconSysSearch`.
   - Kế thừa chuẩn `Style="{StaticResource IecStatusIconStyle}"` đảm bảo `Stretch="Uniform"`, bo góc nét vẽ `Round` 1.6px, căn giữa hoàn hảo.
   - Biên dịch và kiểm thử tự động 1,929/1,929 tests đạt **100% PASS**.

### 7.7. Tự động Mở Hộp thoại New Project Configuration khi Khởi động Ứng dụng (Phương án A)
1. **Bối cảnh & Yêu cầu:**
   - Trước đây khi mở SynaptiX IDE mà không truyền file dự án, ứng dụng âm thầm nạp cấu hình mặc định (Remote I/O) và hiển thị màn hình System. Kỹ sư thường phải bấm `File > New Project` để đổi sang đúng loại phần cứng thực tế (như DTU Gateway, Data Logger, PLC).
   - Theo yêu cầu thống nhất Phương án A: Khi mở ứng dụng lần đầu (khi `CurrentProjectPath` rỗng), SynaptiX IDE sẽ tự động kích hoạt hộp thoại **New Project Configuration** để kỹ sư chọn nhanh dòng phần cứng mục tiêu hoặc bấm **Nhận diện thiết bị** (`Detect Device`) ngay lập tức.
   - Nếu kỹ sư bấm `Tạo dự án` (`Create Project`): hệ thống thiết lập dự án theo đúng profile phần cứng đã chọn. Nếu bấm `Hủy bỏ` (`Cancel`): hộp thoại đóng lại và giữ nguyên phiên làm việc bình thường.
2. **Các chỉnh sửa đã triển khai:**
   - **[`MainViewModel.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/ViewModels/MainViewModel.cs):**
     - Bổ sung phương thức `PromptStartupNewProject()`: khởi tạo `NewProjectViewModel`, kiểm tra `ShowNewProjectDialogHandler` (phục vụ headless unit test) hoặc mở dialog `NewProjectDialogView` với `Owner = Application.Current.MainWindow`.
     - Nếu người dùng xác nhận (`shouldApply == true`), gọi `ApplyNewProject(wizardVm)`.
   - **[`MainWindow.xaml.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/MainWindow.xaml.cs):**
     - Đăng ký sự kiện `Loaded += OnWindowLoaded;`.
     - Trong `OnWindowLoaded`: kiểm tra `if (DataContext is MainViewModel vm && string.IsNullOrEmpty(vm.CurrentProjectPath))`, sau đó điều phối qua `Dispatcher.BeginInvoke` với độ ưu tiên `DispatcherPriority.Loaded` để mở hộp thoại ngay khi cửa sổ IDE hoàn tất dựng giao diện.
   - **[`NewProjectWizardTests.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio.Tests/NewProjectWizardTests.cs):**
     - Bổ sung 2 bài kiểm thử tự động:
       - `MainViewModel_PromptStartupNewProject_WhenAccepted_AppliesSelectedPreset`: Kiểm thử khi người dùng chọn thiết bị mục tiêu (DTU) và bấm chấp nhận, dự án được cập nhật đúng thông tin và tab Device được hiển thị.
       - `MainViewModel_PromptStartupNewProject_WhenCancelled_KeepsOriginalProject`: Kiểm thử khi hủy bỏ, cấu hình dự án gốc không bị thay đổi.
3. **Kết quả xác thực:**
   - Biên dịch thành công 0 cảnh báo, 0 lỗi.
   - 1,931 / 1,931 unit tests toàn bộ solution đạt **100% PASS** (14/14 tests của New Project Wizard đạt Pass).
   - Đạt tiêu chuẩn thiết kế Fluent 2 / Carbon, zero hex tokens, tuân thủ Clean Architecture và TDD Living Documentation.

---

## 15. Triển khai Phân hệ Zigbee Wireless (Modbus over Zigbee) trên Màn hình Device Configuration & Trình Giả lập

### 15.1. Bối cảnh & Yêu cầu Kỹ thuật
- Theo đặc tả giao tiếp nhị phân [`SPLC-SPEC-ZIGBEE-006-ICD`](file:///g:/HoaNV/Projects/SimplePLC/docs/firmware/SPLC_SPEC_ZIGBEE_006_ICD.md), các dòng thiết bị truyền thông không dây (như `X.Link Zigbee 44`, `SX-DTU-485`) được tích hợp module RF Zigbee 2.4 GHz (DRF1609H / E18 / CC2530) hoạt động như một bus Modbus over Zigbee không dây, thay thế cho cáp nối tiếp RS-485.
- Phân hệ chiếm 64 Holding Registers (`0x1700 .. 0x173F`, 128 Bytes) với các yêu cầu kỹ thuật bất biến:
  1. **Visual Truth Invariant:** Zigbee là phân hệ vô tuyến chuyên biệt (**Wireless Modbus Transport**), không đồng nhất thành "Cổng RS-485 số 5" trên giao diện người dùng.
  2. **Bộ lọc Năng lực Động:** Chỉ hiển thị mục Zigbee trên thanh Sidebar (Section 5) khi phần cứng có cờ `SPLC_HardwareCapabilities.ZIGBEE = 0x8000`. Khi chuyển sang thiết bị khác, hệ thống tự động fallback an toàn.
  3. **Safety Gate Phần cứng:** Chặn tuyệt đối địa chỉ trạm (`StationId` / `SlaveId`) **`236 (0xEC)`** và **`237 (0xED)`** vì trùng mã lệnh cấu hình AT nội bộ của module RF, bảo vệ thiết bị khỏi bị treo hay rơi vào trạng thái cấu hình không mong muốn.
  4. **Bảo mật Write-Only AES-128:** Khóa mạng 16 byte không thể đọc ngược từ phần cứng (MCU luôn trả `0x00`), do đó giao diện quản lý qua cờ `PRESERVE_KEY` (Bit 5 `control_flags`) và cờ trạng thái `KEY_SET` (Bit 0 `status_flags`).
  5. **Input FSM & Trực quan hóa Kênh Sạch:** Tự động chuẩn hóa PAN ID dạng Hex, nhận diện các kênh sóng sạch không trùng phổ Wi-Fi 2.4 GHz (15, 20, 25, 26).

### 15.2. Các Thay đổi Triển khai (Implementation Summary)
1. **Tầng Mô hình & Lưu trữ Dự án (`SimplePLC.Studio/Models/ProjectModel.cs`):**
   - Bổ sung class `ZigbeeConfigModel` đầy đủ các trường (Enabled, Role, SlaveId, PanIdHex, Channel, Antenna, SecurityEnabled, PreserveKeyOnDeploy, NetworkName).
   - Tích hợp `public ZigbeeConfigModel ZigbeeConfig { get; set; } = new();` vào `ProjectMetadata` để lưu/mở file dự án `.synx` JSON không mất mát dữ liệu.
2. **Tầng Giả lập Phần cứng (`src/SimplePLC.Infrastructure/Simulator/SimulatorRegisterMemory.cs`):**
   - Đăng ký dải địa chỉ `0x1700 .. 0x173F` (`ModbusRegisterMap.ZigbeeConfigBaseAddress`, `ModbusRegisterMap.ZigbeeConfigRegisters`) vào cả `IsReadableRange` và `IsWritableRange`, loại bỏ hoàn toàn lỗi `ModbusException(0x02 ILLEGAL_DATA_ADDRESS)` khi chạy giả lập và live testing.
3. **Tầng ViewModel (`SimplePLC.Studio/ViewModels/ZigbeeConfigViewModel.cs`):**
   - Quản lý máy trạng thái nhập liệu (Input FSM) cho Station ID và PAN ID với khả năng Auto-Normalize và Auto-Revert khi mất focus.
   - Tích hợp Safety Gate từ chối ID 236/237 ngay từ pha gõ chữ (Drafting) và kiểm định Pre-flight trước khi nạp/lưu.
   - Quản lý lựa chọn ăng-ten (PCB vs External SMA), chuyển đổi ẩn/hiện mật khẩu AES-128, tính toán thanh đo RSSI (-100 đến -40 dBm).
4. **Tích hợp Device Studio (`DtuGatewayStudioViewModel.cs` & `DtuGatewayStudioView.xaml`):**
   - Bổ sung Section 5 `IsSectionZigbee => SelectedSectionIndex == 5;` được bảo vệ bởi cờ `HasZigbeeCapability`.
   - Bổ sung điều hướng tự động trong `SelectFirstAvailableSection()`.
   - Nhúng UserControl dùng chung `ZigbeeConfigControl.xaml` vào Center Workspace.
   - Thiết kế Inspector 2 cột chuẩn IBM Carbon: Trạng thái liên kết `LINK UP`, thước đo RSSI dBm, LQI, số nút trong Mesh, mã lỗi phần cứng chuẩn hóa và sơ đồ Topo luồng truyền thông ASCII Inset Box.
5. **Đồng bộ Đa ngôn ngữ (`SimplePLC.Studio/Services/LocalizationService.cs`):**
   - Bổ sung đầy đủ chuỗi bản địa hóa song ngữ Việt - Anh cho toàn bộ tiêu đề, nhãn, gợi ý kênh sạch và cảnh báo Safety Gate.
6. **Bộ Kiểm thử TDD Toàn diện:**
   - [`ZigbeeConfigViewModelTests.cs`](file:///g:/HoaNV\Projects\SimplePLC\SimplePLC.Studio.Tests\ZigbeeConfigViewModelTests.cs): 16 test cases bao phủ cả 3 trục IEC 61508-3 Triad (Nominal, Tolerant Normalization, Adversarial/Safety Gate).
   - [`ZigbeeStudioIntegrationTests.cs`](file:///g:/HoaNV\Projects\SimplePLC\SimplePLC.Studio.Tests\ZigbeeStudioIntegrationTests.cs): 3 test cases kiểm thử cờ năng lực phần cứng, tự động fallback điều hướng và mô phỏng bộ nhớ thanh ghi.

### 15.3. Kết quả Xác thực
- **Biên dịch Solution:** 0 Warnings, 0 Errors.
- **Tổng số Unit Tests:** **1,959 / 1,959 tests PASS (100%)** (toàn bộ 30 test mới của Zigbee đạt Pass tuyệt đối).
- **Kiểm định XAML Token:** `[PASS]` 0 vi phạm trên cả `ZigbeeConfigControl.xaml` và `DtuGatewayStudioView.xaml`.
- **Kiểm định Kiến trúc Backend:** `[PASS]` 0 vi phạm Clean Architecture & IEC 61508-3.
- **Kiểm định Living Documentation & TDD:** `[PASS]` 0 vi phạm tài liệu XML docs và Code-to-Test mapping.

### 15.4. Tinh chỉnh Tối giản & Hiện đại Hóa Giao diện Zigbee (Minimalist & Clean Layout)
1. **Khắc phục lỗi chuỗi chưa dịch:** Bổ sung key `ZigbeeFieldRole` (`"Vai trò thiết bị:"` / `"Device Operating Role:"`) và `ZigbeePanIdRangeHint` (`"Dải: 0x0001 .. 0xFFFE"` / `"Range: 0x0001 .. 0xFFFE"`), loại bỏ 100% hiện tượng lộ key thô hoặc trộn ngôn ngữ Anh-Việt.
2. **Triệt tiêu văn bản trùng lặp:** Tách phụ đề độc lập cho banner bật tắt (`ZigbeeEnableSubtitle`) và đổi tên card thanh tra bên phải thành `WIRELESS LINK STATUS` để không lặp lại tiêu đề trên cùng.
3. **Thu gọn kích thước nhập liệu theo chuẩn công nghiệp:** Đổi cột Station ID (140px) và PAN ID (150px) về kích thước chuẩn, giúp các ô nhập gọn gàng, mắt quan sát tập trung, không bị kéo dãn toàn màn hình.
4. **Đồng bộ hàng đơn RSSI:** Thống nhất hiển thị RSSI dạng 1 dòng duy nhất gọn gàng chuẩn IBM Carbon đồng nhất 1:1 với phân hệ Wi-Fi (`Signal Strength: -64 dBm`).
5. **Loại bỏ khối Communication Topology:** Theo yêu cầu tối giản và hiện đại hóa giao diện của người dùng ("không cần cái này"), toàn bộ khối hộp vẽ sơ đồ ASCII Topo trong thanh Inspector bên phải đã được gỡ bỏ hoàn toàn. Nhờ đó thanh Inspector bên phải chỉ tập trung vào các thông số viễn trắc đo kiểm thực thời (Trạng thái liên kết, Cường độ sóng, Chất lượng đường truyền LQI, Số lượng node Mesh và Mã lỗi phần cứng), mang lại giao diện tinh tế, chuyên nghiệp và không gây rối mắt cho kỹ sư vận hành.
6. **Tối giản hóa Lựa chọn Kênh Sạch (Clean Channel Option A):** Xóa bỏ hoàn toàn dòng text dài dòng kèm icon và màu sắc (`★ Clean Channel (Recommended) ...`) nằm phía dưới ô ComboBox để giải phóng không gian và loại bỏ chi tiết gây phân tâm. Thay vào đó, nhãn khuyến nghị được tích hợp trực tiếp vào danh sách lựa chọn của ComboBox thông qua `ZigbeeChannelOption` (`15 (Kênh sạch - Khuyên dùng)` / `15 (Clean Channel - Recommended)`), vừa giữ trọn vẹn tri thức hỗ trợ kỹ sư vận hành chọn đúng tần số RF không bị nhiễu Wi-Fi, vừa đem lại giao diện hiện đại, sạch sẽ và tối giản nhất.
7. **Đồng bộ hóa Trạng thái Huy hiệu Sidebar (State Vocabulary Uniformity):** Loại bỏ hoàn toàn sự bất nhất quán về từ ngữ trạng thái (trước đây Wi-Fi hiển thị `On`, MQTT hiển thị `Active`, HTTP/TCP hiển thị phương thức/cổng, v.v.). Thống nhất 100% chuẩn nhị phân công nghiệp trên toàn bộ thanh điều hướng:
   - Khi dịch vụ được bật: Hiển thị đồng nhất **`Active`** (Tiếng Việt: **`Hoạt động`**).
   - Khi dịch vụ tắt: Hiển thị đồng nhất **`Disabled`** (Tiếng Việt: **`Đã tắt`**).
   - Áp dụng xuyên suốt cho toàn bộ các phân hệ mạng: Wi-Fi Network, MQTT Client, HTTP Client, TCP Client và Zigbee Wireless. Toàn bộ logic đã được xác thực tự động qua unit test `DtuGatewayStudioViewModel_SidebarBadges_ReflectProtocolEnabledStates`.





---

## 16. Tích hợp Trực tiếp Phân hệ Zigbee Wireless vào Khối FBD MB_READ & MB_WRITE (Phương án A - Unified Transport Blocks)

### 16.1. Bối cảnh & Bản chất Kỹ thuật (First Principles Doctrine)
- **Bản chất đường truyền Modbus over Zigbee (IEEE 802.15.4):**
  - Zigbee công nghiệp trong hệ thống SimplePLC không phải là một giao thức dữ liệu mới, mà là một **phương thức truyền tải gói tin (Wireless Modbus Transport)** mang các khung Modbus RTU tiêu chuẩn qua sóng RF tần số 2.4 GHz.
  - Khi người dùng cấu hình Zigbee trong dự án và sử dụng các khối logic FBD (`MB_READ`, `MB_WRITE`), khối logic này thực chất đang đọc/ghi các thanh ghi Modbus trên thiết bị ngoại vi không dây thông qua module thu phát Zigbee của PLC/DTU trung tâm.
- **Vấn đề trước đây:**
  - Hộp thoại cấu hình của khối `MB_READ` và `MB_WRITE` chỉ cung cấp danh sách cổng nối tiếp RS-485 (`RS-485 #1` .. `RS-485 #4`), hoàn toàn thiếu tùy chọn cổng Zigbee ngay cả khi dự án đã bật phân hệ Zigbee.
  - Người dùng không có cách nào điều hướng việc đọc/ghi dữ liệu Modbus qua mạng không dây Zigbee từ FBD Canvas.

### 16.2. Giải pháp Kiến trúc Thực hiện (Phương án A - Hợp nhất Chuẩn hóa)
Thay vì tạo thêm khối FBD riêng biệt gây phân mảnh và rối rắm cho người dùng, giải pháp áp dụng nguyên lý mở rộng sạch (Open/Closed Principle) trên hai khối `MB_READ` và `MB_WRITE`:

1. **Chuẩn hóa Định danh Cổng Zigbee (`PortIndex = 4`):**
   - Theo đặc tả nhị phân phần cứng [`SPLC-SPEC-ZIGBEE-006-ICD`](file:///g:/HoaNV/Projects/SimplePLC/docs/firmware/SPLC_SPEC_ZIGBEE_006_ICD.md), các cổng RS-485 vật lý chiếm dải chỉ mục `0 .. 3`. Phân hệ Zigbee Wireless Modbus được quy hoạch cứng với `PortIndex = 4`.
   - Bổ sung thuộc tính `IsZigbeePort => PortIndex == 4` và `PortDisplayName => IsZigbeePort ? "Zigbee Wireless" : $"RS-485 #{PortIndex + 1}"` trong cả `ModbusReadNodeViewModel` và `ModbusWriteNodeViewModel`.

2. **Tự động Cập nhật Danh sách Cổng trong Dialog Cấu hình:**
   - Trong [`ModbusReadConfigDialogViewModel.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/ViewModels/ModbusReadConfigDialogViewModel.cs) và [`ModbusWriteConfigDialogViewModel.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/ViewModels/ModbusWriteConfigDialogViewModel.cs):
     - Khi `ZigbeeConfig.Enabled == true`, danh sách `AvailablePorts` tự động bổ sung mục lựa chọn:
       `🌐 Zigbee Wireless (Kênh {Channel} · PAN 0x{PanIdHex})` (giá trị byte = `4`).
     - Tooltip chỉ dẫn và tóm tắt trạng thái hiển thị rõ ràng thông tin tần số RF và mạng Zigbee.
     - Nếu dự án chưa bật Zigbee nhưng khối FBD trước đó đang chọn cổng 4, danh sách hiển thị kèm cảnh báo vô hiệu hóa `(Chưa bật Zigbee trong dự án)`.

3. **Cơ chế An toàn Phần cứng (Safety Gate Chặn Slave ID 236 & 237):**
   - Khi chọn cổng `Zigbee Wireless` (`PortIndex == 4`), nếu kỹ sư nhập `SlaveId` là **`236 (0xEC)`** hoặc **`237 (0xED)`**, giao diện lập tức kích hoạt cờ lỗi `HasRestrictedIdError = true` và hiển thị cảnh báo đỏ trực quan dưới ô nhập.
   - Khi bấm **Áp dụng** (`Apply`), hệ thống tự động ngăn chặn việc gán ID cấm vào node FBD, hoàn nguyên về ID hợp lệ để phòng chống module RF rơi vào chế độ lệnh AT làm đơ đường truyền.

4. **Trực quan hóa Khối FBD trên Canvas (Visual Truth - Axiom 2):**
   - Trên FBD Canvas, khi một khối `MB_READ` hoặc `MB_WRITE` được định tuyến qua Zigbee, tiêu đề tóm tắt hiển thị rõ biểu tượng vô tuyến:
     - `MB_READ`: `🌐 Zigbee | ID:{SlaveId} | FC{FunctionCode}`
     - `MB_WRITE`: `🌐 Zigbee | ID:{SlaveId} | FC{FunctionCode}` (hoặc `CH:{ChannelCount}`)
   - Trong FBD Toolbox, tiêu đề khối được chuẩn hóa thành `MB_READ (RS-485 / Zigbee)` và `MB_WRITE (RS-485 / Zigbee)` kèm phần mô tả song ngữ giúp kỹ sư dễ dàng tìm kiếm từ khóa `"Zigbee"` hoặc `"RS485"`.

### 16.3. Kết quả Kiểm thử & Nghiệm thu
- **XAML Token Validator:** `[PASS]` 0 vi phạm thiết kế Fluent 2 / IBM Carbon trên cả `ModbusReadConfigDialogView.xaml` và `ModbusWriteConfigDialogView.xaml`.
- **Backend Architecture Validator:** `[PASS]` 0 vi phạm Clean Architecture và IEC 61508-3.
- **QA, TDD & Living Documentation Validator:** `[PASS]` 0 vi phạm quy chuẩn XML doc comments, 100% tỷ lệ đỗ bài kiểm thử.
- **Bộ Kiểm thử Tự động:** **1,975 / 1,975 unit tests PASS (100%)** trên toàn bộ solution SimplePLC (bao gồm 6 bài kiểm thử mới bao phủ toàn diện cả 3 trục Happy Path, Normalization và Adversarial Safety Gate cho Zigbee trong các khối Modbus).


---

## 17. Chuẩn hóa Phân tầng Định danh Phần cứng (Hardware Hierarchy): Device Class vs. Target Model vs. Hardware Version

### 17.1. Bối cảnh & Nguyên nhân Gốc rễ (Root Cause)
- **Lẫn lộn khái niệm tại Hộp thoại `Device Information`:**
  - Trước đây, khi mở xem thông tin thiết bị ở chế độ Ngoại tuyến/Mô phỏng, trường nhãn **`Hardware Version (HW):`** lại hiển thị giá trị là tên thiết bị mục tiêu (ví dụ: `X.Link Zigbee 44` hoặc `SynaptiX Remote I/O`).
  - *Nguyên nhân cốt lõi:* Trong `MainViewModel.cs`, thuộc tính `HwVersion` của `DeviceInfoViewModel` bị gán nhầm trực tiếp từ chuỗi `meta.TargetDevice` khi không có kết nối thiết bị thật qua cổng COM/USB.
  - Về mặt bản chất kỹ thuật hệ thống nhúng: **Hardware Version** là số hiệu phiên bản mạch điện tử / bo mạch in (PCB Revision, ví dụ: `v1.0.0`, `Rev 1.0`), hoàn toàn không phải là Tên mẫu thiết bị thương mại.
- **Thiếu nhất quán tại Hộp thoại `New Project Configuration`:**
  - Danh sách chọn mẫu thiết bị mang nhãn chung chung `Target Hardware Presets:`, trong đó vừa chứa tên Class cấp cao (`SynaptiX Remote I/O`, `SynaptiX Gateway`), vừa chứa tên biến thể phần cứng con của cùng Class đó (`SynaptiX Compact I/O`, `SynaptiX Relay Unit`, `X.Link Zigbee 44` đều thuộc Class `Remote I/O`).
  - Mẫu cấu hình dự phòng `SynaptiX SYNX-PLC-884 (Controller · 8DI / 8DO / 4AI)` bị trùng lặp tài nguyên với `SYNX-RIO-884` và không phản ánh đúng danh mục phần cứng thực tế.

### 17.2. Các Giải pháp Triển khai (Implementation Summary)
Theo chỉ đạo kỹ thuật từ người dùng và nguyên tắc First Principles:

1. **Chuẩn hóa Danh mục Thiết bị Mục tiêu (`HardwareDevicePreset.cs`):**
   - Loại bỏ mẫu trùng lặp `CONTROLLER_STANDALONE` (`SynaptiX SYNX-PLC-884`).
   - Chuẩn hóa tên định danh các dòng thiết bị thương mại trong `BuiltInPresets` theo cú pháp chuẩn công nghiệp: `[Hãng] [Mã P/N] ([Device Class] · [Cấu hình I/O])`:
     - `SynaptiX SYNX-RIO-884 (Remote I/O · 8DI / 8DO / 4AI)` (Mặc định)
     - `SynaptiX SYNX-RIO-440 (Remote I/O · 4DI / 4DO)` (Đổi từ Compact I/O về đúng Class Remote I/O)
     - `SynaptiX SYNX-RIO-880 (Remote I/O · 8DI / 8DO Relay)`
     - `SynaptiX SYNX-DL-424 (Data Logger · 4DI / 2DO / 4AI)`
     - `SynaptiX SYNX-GW-220 (Protocol Gateway · RS-485 / Net)`
     - `SynaptiX SYNX-DTU-485 (Industrial DTU · 4x RS-485 / Net)`
     - `SynaptiX XLINK-ZB-44 (Zigbee Wireless I/O · 4DI / 4DO)`

2. **Tách biệt Độc lập các Tầng Định danh trong `DeviceInfoViewModel` & `MainViewModel`:**
   - Bổ sung thuộc tính `TargetModelText` trong `DeviceInfoViewModel` đại diện cho Mẫu thiết bị mục tiêu.
   - Khi **Ngoại tuyến (Offline / Virtual)**:
     - `DeviceModel` và `TargetModelText`: Gán theo đúng `meta.TargetDevice` của dự án (ví dụ: `SynaptiX XLINK-ZB-44...`).
     - `HwVersion`: Gán đúng bản chất bo mạch `v1.0 (Standard)` / `v1.0 (Mặc định)` thay vì gán tên thiết bị vào đây.
     - `FwVersion`: `Offline` / `Chưa kết nối`.
   - Khi **Trực tuyến (Online)**:
     - `HwVersion`: `v{d.HwVersionString}` (lấy từ Device Descriptor thật trên MCU).
     - `FwVersion`: `v{d.FwVersionString}` (lấy từ Firmware build version).

3. **Tái cấu trúc Thẻ Nhận dạng Thiết bị (`DeviceInfoDialogView.xaml`):**
   - Sắp xếp cân đối 4 hàng thông số:
     - *Hàng 0:* `Device Class` (Lớp thiết bị) | `Device Model` (Mẫu thiết bị mục tiêu - binding `TargetModelText`).
     - *Hàng 1:* `Hardware Variant` (Biến thể I/O) | `Hardware Version (HW)` (Phiên bản bo mạch - binding `HwVersion`).
     - *Hàng 2:* `Wire Profile` (Hồ sơ kết nối dây) | `Firmware Version (FW)` (Phiên bản firmware - binding `FwVersion`).
     - *Hàng 3:* `Rule Format Version` (Định dạng bytecode v2.0).

4. **Nâng cấp Giao diện Hộp thoại Tạo Dự án (`NewProjectDialogView.xaml` & `LocalizationService.cs`):**
   - Đổi nhãn chọn thiết bị từ `Target Hardware Presets:` thành `Target Device Model:` (Tiếng Việt: `Mẫu thiết bị phần cứng mục tiêu:`).
   - Thẻ hiển thị tóm tắt đổi nhãn `Target Microcontroller:` thành `Hardware Series:` (`NewProjectHardwareSeries`) và `Device Family:` thành `Device Class:` (`NewProjectDeviceClass`).

### 17.3. Kết quả Kiểm thử & Nghiệm thu
- **XAML Token Validator:** `[PASS]` 0 vi phạm thiết kế Fluent 2 / IBM Carbon trên cả `DeviceInfoDialogView.xaml` và `NewProjectDialogView.xaml`.
- **Backend Architecture Validator:** `[PASS]` 0 vi phạm Clean Architecture & IEC 61508-3.
- **QA, TDD & Living Documentation Validator:** `[PASS]` 0 vi phạm quy chuẩn XML doc comments, 100% tỷ lệ đỗ bài kiểm thử.
- **Bộ Kiểm thử Tự động:** **1,977 / 1,977 unit tests PASS (100%)** trên toàn bộ solution SimplePLC (bao gồm các test mới `ShowDeviceInfo_WhenOffline_SeparatesTargetModelAndHardwareVersion` và `ShowDeviceInfo_WhenOnline_HardwareVersionFromDescriptor`).

---

## 18. Màn hình Khởi động Tiêu chuẩn Quốc tế (Startup Hub / Welcome Window) & Đọc Toàn Diện Thiết Bị (Full Device Reverse Upload)

### 18.1. Bối cảnh & Mục tiêu
- **Startup Hub / Welcome Window:** Thay thế luồng mở thẳng IDE hoặc popup New Project đơn điệu bằng màn hình khởi động 2 cột hiện đại theo phong cách Visual Studio / JetBrains Rider:
  - Cột trái: Quản lý danh sách dự án mở gần đây (`Recent Projects`) với bộ lọc tìm kiếm thời gian thực, huy hiệu mã phần cứng (`[SYNX-RIO-884]`, `[SYNX-DTU-485]`, `[XLINK-ZB-44]`), thời gian tương đối (`Hôm nay, 14:32`, `2 ngày trước`), chức năng Ghim (`Pin`) và Xóa (`Remove`).
  - Cột phải: 4 thẻ hành động công nghiệp Fluent 2:
    1. ➕ **Tạo dự án mới** (`Create a New Project`)
    2. 📂 **Mở dự án từ máy tính** (`Open Local Project`)
    3. ⚡ **Đọc cấu hình từ thiết bị** (`Upload from Connected Device`) - tích hợp trực tiếp chọn cổng COM và nút Tải lên ngay
    4. 👉 **Bỏ qua & bắt đầu ngay** (`Continue without a project`)
  - Cho phép mở lại bất cứ lúc nào qua Menu `File -> Start Page...` (`Trang khởi động...`).
- **Đọc Toàn Diện Thiết Bị (Full Device Reverse Upload):**
  - Tuân thủ tiên đề *Chân lý Thị giác (Visual Truth)* và *Chân lý Phần cứng (Hardware Ground Truth)*.
  - Tự động đảo ngược 100% hình ảnh thiết bị từ phần cứng MCU: Wi-Fi (SSID, Password, DHCP, Static IP, Gateway, Subnet, DNS), Zigbee (Channel, PAN ID, Station ID, Antenna, AES-128, Role), RS-485 Modbus Multi-Ports, DTU Gateway (HTTP/TCP/MQTT Will), Toàn bộ Bảng Quy tắc (Rule Table) và tái dựng sơ đồ đồ họa khối FBD.

### 18.2. Các Thành phần Đã Triển khai & Tích hợp
1. **Mô hình Dữ liệu & Quản lý Dự án Gần đây:**
   - [`RecentProjectEntry.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Models/RecentProjectEntry.cs): Model dữ liệu lưu trữ `FilePath`, `ProjectName`, `HardwareTarget`, `HardwareBadge`, `LastOpened`, `IsPinned`.
   - [`ProjectFileService.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Services/ProjectFileService.cs): Cập nhật hàm `GetRecentProjectEntries()`, `AddRecentProject()`, `RemoveRecentProject()`, `TogglePinRecentProject()`, `DeriveHardwareBadge()` với khả năng tương thích ngược hoàn toàn với định dạng chuỗi cũ.
2. **ViewModel Điều khiển:**
   - [`RecentProjectItemViewModel.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/ViewModels/RecentProjectItemViewModel.cs): Quản lý từng mục dự án gần đây, định dạng thời gian tương đối song ngữ Anh/Việt (`RelativeTimeText`).
   - [`StartupHubViewModel.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/ViewModels/StartupHubViewModel.cs): Quản lý danh sách tìm kiếm, quét cổng kết nối COM/USB tự động, xử lý 4 hành động chính và phát tín hiệu đóng cửa sổ.
   - [`MainViewModel.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/ViewModels/MainViewModel.cs):
     - Bổ sung `PromptStartupHub()` và delegate hook `ShowStartupHubDialogHandler` phục vụ kiểm thử tự động không giao diện.
     - Đồng bộ toàn diện Wi-Fi và Zigbee trong `UploadFromDeviceAsync()` và làm mới `DtuGatewayStudioVM.ReloadFromMetadata()`.
3. **Giao diện Người dùng XAML Tiêu chuẩn:**
   - [`StartupHubWindow.xaml`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Views/StartupHubWindow.xaml) & [`.xaml.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Views/StartupHubWindow.xaml.cs):
     - Microsoft Fluent 2 Desktop geometry (`CornerRadius="4"` cho inputs/buttons, `6` cho action cards, `8` cho container window).
     - Tuân thủ nguyên tắc 90/10 ANSI/ISA-101 (The Gray Principle).
     - 100% tài nguyên màu tham chiếu `{DynamicResource ...}` từ `Colors.xaml`. Tuyệt đối 0 mã màu hex cứng.
     - 100% biểu tượng vector SVG StreamGeometry từ `Icons.xaml` (`IconSysUpload`, `IconSysPin`, `IconSysClear`, `IconSysSearch`, `IconSysPlus`, `IconSysFolderOpen`). Tuyệt đối 0 ký tự Unicode emoji.
   - [`MainWindow.xaml`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/MainWindow.xaml): Tích hợp mục menu `Trang khởi động...` (`MenuStartPage`).
   - [`MainWindow.xaml.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/MainWindow.xaml.cs): Gọi `vm.PromptStartupHub()` khi nạp ứng dụng chưa mở dự án.
4. **Hạ tầng Giao thức & Phần cứng:**
   - [`FullDeviceConfigurationDto.cs`](file:///g:/HoaNV/Projects/SimplePLC/src/SimplePLC.Protocol/Dto/FullDeviceConfigurationDto.cs): Bổ sung `WifiConfigDto? WifiConfig` và `ZigbeeConfigDto? ZigbeeConfig`.
   - [`RuleTableReader.cs`](file:///g:/HoaNV/Projects/SimplePLC/src/SimplePLC.Infrastructure/Devices/RuleTableReader.cs): Bổ sung `ReadActiveWifiConfigAsync()` và `ReadActiveZigbeeConfigAsync()`.

### 18.3. Kết quả Kiểm thử & Nghiệm thu
- **XAML Token Validator:** `[PASS]` 0 vi phạm thiết kế trên `StartupHubWindow.xaml` và `MainWindow.xaml`.
- **Backend Architecture Validator:** `[PASS]` 0 vi phạm kiến trúc Clean Architecture & IEC 61508-3.
- **QA, TDD & Living Documentation Validator:** `[PASS]` 0 vi phạm tài liệu XML Summary, 100% tỷ lệ pass bài kiểm thử.
- **Bộ Kiểm thử Đơn vị & Tích hợp:**
  - [`StartupHubViewModelTests.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio.Tests/StartupHubViewModelTests.cs): 6/6 test PASS (Tải danh sách, tìm kiếm lọc thời gian thực, ghim/xóa dự án, thực thi các lệnh hành động, kiểm tra format thời gian tương đối song ngữ, tích hợp với MainViewModel).
  - [`FullDeviceUploadRoundtripTests.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio.Tests/FullDeviceUploadRoundtripTests.cs): 3/3 test PASS (Bao gồm test tích hợp mới `Test_FullDeviceUpload_Synchronizes_Wifi_And_Zigbee_Subsystems` xác nhận roundtrip toàn diện Wi-Fi và Zigbee từ Modbus Holding Registers vào Studio ViewModels).

---

## 19. Nâng cấp Bảng Khởi Động Toàn Màn Hình Trong Ứng Dụng (In-App Fullscreen Welcome Dashboard) & Khởi Động Maximized

### 19.1. Bối cảnh & Phản hồi Thực tế
- Sau khi vận hành thử nghiệm popup khởi động modal `StartupHubWindow`, một số bất cập về mặt trải nghiệm thị giác (Visual UX) theo phản ánh từ người dùng bao gồm:
  1. **Hiện tượng "Cửa sổ lồng trong cửa sổ" (Double Window Frame / Double Close Button):** Mở một popup modal che lấp cửa sổ chính tạo cảm giác thô, xuất hiện 2 thanh tiêu đề và 2 nút đóng cửa sổ (X).
  2. **Hội chứng mỏi viền khung (Border Fatigue):** Việc bọc mỗi dòng dự án gần đây trong một hình chữ nhật viền cứng 1px tạo cảm giác rối mắt, nặng nề, thiếu tính tối giản theo phong cách hiện đại của Microsoft Fluent 2 và VS Code / Codesys.
  3. **Rác tệp tạm thời từ Unit Test:** Quá trình chạy kiểm thử tự động `StartupHubViewModelTests` ghi đè các tệp dự án tạm thời (`C:\Users\...\AppData\Local\Temp\...`) vào danh sách dự án gần đây thực tế của người dùng `%APPDATA%\SynaptiX\recent_projects.json`.
  4. **Kích thước mở mặc định:** Người dùng mong muốn khi mở SynaptiX IDE, ứng dụng phải mặc định mở toàn màn hình (`WindowState="Maximized"`).

### 19.2. Giải pháp Kỹ thuật Triệt để (First Principles Engineering)
1. **Chuyển đổi thành Màn hình Toàn bộ Trong Ứng dụng (`StartPageView.xaml` UserControl):**
   - Loại bỏ hoàn toàn cửa sổ popup modal `StartupHubWindow.xaml` và `.xaml.cs`.
   - Tạo mới [`StartPageView.xaml`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Views/StartPageView.xaml) và [`StartPageView.xaml.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Views/StartPageView.xaml.cs) dưới dạng một `UserControl`.
   - Tích hợp trực tiếp vào vùng `ContentControl Content="{Binding CurrentView}"` của `MainWindow.xaml` thông qua `DataTemplate DataType="{x:Type vm:StartupHubViewModel}"`.
   - Tự động ẩn thanh điều hướng Tab phân hệ và thanh kết nối (Row 1) khi Start Page đang kích hoạt (`IsStartPageActive = true`), mang lại không gian khởi động cực kỳ tập trung, khoáng đạt và thanh lịch giống 100% Visual Studio / VS Code.
2. **Thiết kế Danh sách Phẳng (Flat-List) Chống Mỏi Viền:**
   - Thay thế các ô viền bao quanh từng dòng bằng thiết kế phẳng `RecentProjectFlatRowStyle` (`Background="Transparent"`, `BorderThickness="0"`, `CornerRadius="4"`).
   - Tương tác di chuột (Hover): Tô màu nền tinh tế `{DynamicResource SelectionHoverBrush}`.
   - Nút Xóa (X) và nút Ghim (Pin) chỉ làm nổi bật hoặc hiển thị khi trỏ chuột vào dòng tương ứng, giữ cho giao diện luôn thoáng đãng.
3. **Mặc định Cửa sổ Maximized:**
   - Cấu hình `WindowState="Maximized"` ngay trên thẻ gốc `<Window ...>` của `MainWindow.xaml`.
4. **Cô lập Môi trường Kiểm thử (Hermetic Test Isolation - IEC 61508-3):**
   - Bổ sung thuộc tính `ProjectFileService.CustomRecentProjectsFilePath` cho phép các bộ kiểm thử tự động chuyển hướng đường dẫn lưu trữ vào thư mục riêng của test session.
   - Bổ sung bộ lọc trong `ProjectFileService.GetRecentProjectEntries()` và `AddRecentProject()` để loại bỏ hoàn toàn các đường dẫn nằm trong `Path.GetTempPath()`, đảm bảo danh sách dự án gần đây của người dùng thực không bao giờ bị ô nhiễm bởi các tệp sinh ra từ unit test.
   - Triển khai `IDisposable` trong `StartupHubViewModelTests` để xóa sạch dữ liệu sau mỗi lượt chạy.
5. **Đồng bộ Lệnh Điều khiển Menu & Khắc phục Race Condition Khởi động:**
   - Khắc phục triệt để hiện tượng mở nhầm vào Tab Cấu hình Thiết bị (System / RS-485 Modbus) khi khởi động: Trong hàm khởi tạo của `MainViewModel`, khi nạp metadata mặc định ban đầu `CurrentProjectMetadata = new ProjectMetadata()`, bộ lắng nghe `OnCurrentProjectMetadataChanged` trước đây tự động kích hoạt chuyển Tab sang `DtuGatewayStudioVM` do `SelectedTabIndex == 0`. Đã bổ sung cơ chế guard `if (CurrentView is StartupHubViewModel) return;` và gán tường minh `CurrentView = StartupHubVM`, đảm bảo khi khởi động ứng dụng luôn hiển thị 100% màn hình Start Page toàn màn hình.
   - Bổ sung lệnh `CloseProjectCommand` trong `MainViewModel.cs` kèm nhãn song ngữ `"Đóng dự án"` / `"Close Project"` trong Menu File (`Ctrl+W` hoặc chọn từ menu), cho phép đóng dự án hiện tại và lập tức quay về màn hình Start Page.
   - Mở Start Page bất cứ lúc nào qua Menu `File -> Start Page...` (`OpenStartPageCommand`).
6. **Chuẩn hóa Định danh Logo & Thuật ngữ Thiết bị (Device Naming Standard):**
   - Thay thế icon vi mạch trừu tượng trên Header của Start Page bằng Logo SynaptiX chính thức ([`Assets/app.png`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Assets/app.png)) với container Fluent 2 bo góc 6px.
   - Loại bỏ hoàn toàn thuật ngữ vi mạch nội bộ `MCU` trong toàn bộ giao diện người dùng và Localization (`LocalizationService.cs`), chuẩn hóa 100% sang thuật ngữ công nghiệp chuẩn **Thiết bị** (**Device**).
   - Đổi nhãn Card 3 thành **Đọc dự án từ thiết bị** (`Read Project from Device`), mô tả rõ ràng: *"Kết nối cổng COM/USB và đọc 100% logic FBD, RS-485, Wi-Fi, MQTT từ thiết bị"*.
   - Nâng cấp nút bấm thành **⚡ Kết nối & Đọc** (`⚡ Connect & Read`) với icon vector `IconSysConnect` thay cho nhãn dễ gây nhầm lẫn "Upload Now" / "Tải lên ngay".
7. **Thiết kế Hình học Đối xứng & Neo Thị giác Chân trang (Visual Anchoring & Grounded Layout):**
   - Loại bỏ hoàn toàn cảm giác "lơ lửng" bằng cách bọc toàn bộ cột Dự án Gần đây (Recent Projects) vào một thẻ **Container Panel** Fluent 2 chuẩn mực (`Background="{DynamicResource BgPanelBrush}"`, `CornerRadius="6"`, viền `IndustrialBorderDefaultBrush`), cân bằng đối xứng trọng lượng thị giác 1:1 với các Action Cards bên phải.
   - Bổ sung thanh tìm kiếm với màu nền chìm `IndustrialSurfaceInsetBrush` đặt ngay ngắn ở đầu panel.
   - Thêm trục phân cách trung tâm mỏng 1px (`IndustrialBorderSubtleBrush`) giữa hai cột.
   - Căn lề trên chuẩn Ergonomics (`VerticalAlignment="Top"`, `Margin="40,56,40,40"`): Loại bỏ khoảng hụt mắt quá xa từ Menu bar xuống Header, tạo luồng quét mắt tự nhiên từ trên xuống dưới.
   - Giới hạn hiển thị tối đa 5 dự án gần đây (`MaxRecentProjectsDisplay = 5`): Ngăn ngừa danh sách quá dài gây rối mắt, đồng thời giữ cho chiều cao Cột Trái (~340px) và Cột Phải (~355px) cân bằng đối xứng hoàn hảo trên cùng một đường đáy ngang.
8. **Thống nhất 100% Điều hướng vào Sơ đồ Khối Logic (Hero Workspace Navigation):**
   - Bất kể kỹ sư khởi tạo từ hành động nào trong Get Started: **Tạo dự án mới** (`New Project`), **Mở tệp dự án** (`Open Project`), **Kết nối & Đọc** (`Connect & Read`), hoặc **Bỏ qua & bắt đầu ngay** (`Continue without a project`), hệ thống đều nhất quán đưa kỹ sư vào thẳng màn hình **Logic Graph** (`AppTabIndices.LogicGraph`).
   - Khẳng định vai trò trung tâm và bản sắc của SynaptiX IDE là môi trường lập trình logic FBD chuyên nghiệp, giúp kỹ sư bắt tay ngay vào thiết kế thuật toán điều khiển mà không bị phân tán. Tab `Hệ thống` (`System`) luôn sẵn sàng bên cạnh (`Ctrl+1`) khi cần tinh chỉnh thông số cổng.

### 19.3. Kết quả Kiểm thử & Nghiệm thu
- **XAML Token Validator:** `[PASS]` 0 vi phạm thiết kế trên cả `StartPageView.xaml` và `MainWindow.xaml`.
- **Backend Architecture Validator:** `[PASS]` 0 vi phạm Clean Architecture & IEC 61508-3.
- **QA, TDD & Living Documentation Validator:** `[PASS]` 0 vi phạm tài liệu XML Summary, 100% tỷ lệ pass bài kiểm thử.
- **Toàn bộ Solution:** **2,024 / 2,024 unit tests PASS (100%)** với độ cô lập hoàn toàn giữa môi trường test và dữ liệu người dùng thực tế. Biến cảnh báo CS8601 = 0, lỗi CS = 0.

---

## 20. Khắc phục Hiện tượng Báo ảo Badge "1 regs" tại Thanh Điều hướng RS-485 Modbus

### 20.1. Bối cảnh & Nguyên lý Thiết kế (First Principles)
- **Tiên đề 2 (Axiom 2 — The Visual Truth / Single Source of Truth):**
  Giao diện công nghiệp SynaptiX IDE phải là sự phản ánh trung thực 1:1 trạng thái cấu hình thực tế trong bộ nhớ.
- **Hiện tượng khuyết tật:** Khi người dùng mở dự án và vào mục `RS-485 Modbus` trong workspace System, thanh Sidebar điều hướng hiển thị huy hiệu `1 regs`, trong khi bảng cấu hình Modbus Master bên phải thông báo rõ ràng *"No Modbus channels configured"* (0 kênh đọc/ghi).
- **Nguyên nhân gốc rễ (Root Cause):**
  Thuộc tính `Rs485StatusBadge` trong `DtuGatewayStudioViewModel.cs` trước đây được lập trình đọc trực tiếp `FastRegisters.Count` (danh sách thanh ghi nhanh dùng riêng cho wizard DTU trước khi hệ thống hỗ trợ cấu hình đa cổng Modbus Port). Khi `ModbusPortConfigVM` với cấu trúc đa cổng vật lý (Port 1, Port 2) và danh sách `MasterChannels` được tích hợp làm bảng cấu hình chính thức, `Rs485StatusBadge` chưa được đồng bộ sang `ModbusPortConfigVM`, dẫn đến sự sai lệch thị giác giữa huy hiệu Sidebar và nội dung thực tế của bảng Modbus.

### 20.2. Các Thay đổi Triển khai
1. **ViewModel ([`DtuGatewayStudioViewModel.cs`](file:///SimplePLC.Studio/ViewModels/DtuGatewayStudioViewModel.cs)):**
   - Tái cấu trúc logic thuộc tính `Rs485StatusBadge` để ưu tiên lắng nghe và tính toán trực tiếp từ `ModbusPortConfigVM.Ports`:
     - Nếu không có kênh truyền nào (`totalChannels == 0`) và các cổng đang bật: Hiển thị trung thực số lượng cổng vật lý sẵn sàng (ví dụ Tiếng Việt: `"2 cổng"`, Tiếng Anh: `"2 ports"` hoặc `"1 port"`).
     - Nếu đã cấu hình kênh truyền (`totalChannels > 0`): Hiển thị chính xác tổng số kênh truyền đọc/ghi (ví dụ Tiếng Việt: `"1 kênh"`, `"2 kênh"`, Tiếng Anh: `"1 channel"`, `"2 channels"`).
     - Nếu chế độ Slave được chọn (`IsSlaveMode`): Hiển thị huy hiệu `"Slave"`.
     - Nếu tất cả các cổng bị tắt (`activePorts == 0`): Hiển thị `"Đã tắt"` (Tiếng Việt) / `"Disabled"` (Tiếng Anh).
   - Triển khai cơ chế lắng nghe phản ứng nhanh (`WireModbusPortEvents`): Tự động lắng nghe sự kiện `MasterChannels.CollectionChanged`, `port.PropertyChanged` (`Enabled`, `Role`, `BaudRate`) và `Ports.CollectionChanged` để bắn thông báo `OnPropertyChanged(nameof(Rs485StatusBadge))` và `SummaryTopologyText` theo thời gian thực ngay khi người dùng thao tác thêm/xóa kênh hoặc bật/tắt cổng.
   - Bổ sung XML Summary chuẩn tài liệu kỹ thuật cho thuộc tính `Rs485StatusBadge` và phương thức `WireModbusPortEvents`.
2. **Unit Tests ([`DtuGatewayStudioViewModelTests.cs`](file:///SimplePLC.Studio.Tests/DtuGatewayStudioViewModelTests.cs)):**
   - Viết 6 unit tests mới tuân thủ nghiêm ngặt chuẩn IEC 61508-3 SIL2/3 TDD:
     - `Rs485StatusBadge_OnDefaultInitialization_ReflectsActiveModbusPortsCount_NotFastRegistersCount`: Khẳng định khởi tạo mặc định hiển thị đúng số cổng (`2 ports` / `2 cổng`), xóa bỏ hoàn toàn hiện tượng báo ảo `1 regs`.
     - `Rs485StatusBadge_WhenNoChannelsConfigured_DisplaysActivePortsCount`: Kiểm tra song ngữ khi chưa có kênh nào.
     - `Rs485StatusBadge_WhenChannelsAdded_DisplaysChannelCount`: Kiểm tra tăng số lượng kênh chính xác khi bấm "Add Modbus Channel".
     - `Rs485StatusBadge_WhenAllPortsSlave_DisplaysSlave`: Kiểm tra chế độ Slave.
     - `Rs485StatusBadge_WhenAllPortsDisabled_DisplaysDisabled`: Kiểm tra trạng thái tắt toàn bộ cổng.
     - `Rs485StatusBadge_WhenChannelsChanged_RaisesPropertyChangedReactively`: Kiểm tra tính phản ứng (reactive update) của ViewModel qua sự kiện `PropertyChanged`.
   - Áp dụng mẫu cô lập ngôn ngữ `try { ... } finally { LocalizationService.Instance.CurrentLanguage = originalLang; }` để loại bỏ 100% rò rỉ trạng thái giữa các bài kiểm thử.

### 20.3. Kết quả Kiểm thử & Nghiệm thu
- **XAML Token Validator:** `[PASS]` 0 vi phạm thiết kế.
- **Backend Architecture Validator:** `[PASS]` 0 vi phạm kiến trúc Clean Architecture & IEC 61508-3.
- **QA, TDD & Living Documentation Validator:** `[PASS]` 0 vi phạm tài liệu XML Summary, 100% tỷ lệ pass bài kiểm thử.
- **Toàn bộ Solution:** **2,030 / 2,030 unit tests PASS (100%)** (tăng thêm 6 tests, 0 warnings, 0 errors).
- **Trải nghiệm thực tế:** Huy hiệu Sidebar RS-485 Modbus phản ánh chính xác 100% số lượng cổng (`2 cổng` / `2 ports`) khi chưa có kênh, và tự động chuyển sang số lượng kênh (`1 kênh`, `2 kênh`...) khi người dùng thêm kênh polling/command.

---

## 21. Chuẩn hóa Giao diện Header & Dropdown/Popup theo Microsoft Fluent 2 Desktop & Tích hợp Liên kết Web Wiki VitePress

### 21.1. Bối cảnh & Nguyên lý Thiết kế (First Principles)
- **Chuẩn hóa Màu sắc Header & Khắc phục Dải Băng Xám:**
  Thanh Menu ngang trên cùng (`MainWindow.xaml:125`) trước đây dùng màu nền `BgWindowBrush` (`#ECEFF1` - màu xám xi măng đục của IDE công nghiệp cũ). Khi đặt ngay phía trên không gian làm việc trắng sáng của Start Page hoặc FBD Canvas, dải xám này tạo cảm giác một thanh xà ngang tối đè nặng, gây chia cắt giao diện và mỏi mắt.
- **Hệ thống Dropdown & Popup Phẳng (Fluent 2 Flyout Card):**
  Các menu thả xuống (`MenuItem`), chuột phải (`ContextMenu`), và danh sách chọn (`ComboBoxItem`) cần đồng bộ sang phong cách Fluent 2 hiện đại: nền phẳng trắng `{DynamicResource BgPanelBrush}` (`#FFFFFF`), bo góc `CornerRadius="6"` cho khung ngoài, `CornerRadius="4"` cho từng mục bên trong, viền mỏng 1px `{DynamicResource IndustrialBorderDefaultBrush}` (`#CBD5E1`), loại bỏ cột icon xám đục của Windows Aero cũ.
- **Đồng bộ Liên kết Tài liệu Chính thống (Web Wiki `docs-portal`):**
  Lệnh Menu `Help -> SynaptiX IDE Technical Specifications & Docs...` trước đây trỏ về README GitHub. Cần chuyển hướng chính xác đến cổng Web Wiki tài liệu kỹ thuật **VitePress Portal** song ngữ (`/` cho Tiếng Anh, `/vi/` cho Tiếng Việt).

### 21.2. Các Thay đổi Triển khai
1. **Từ điển Màu sắc ([`SimplePLC.Studio/Styles/Colors.xaml`](file:///SimplePLC.Studio/Styles/Colors.xaml)):**
   - Bổ sung định nghĩa `BgMenuBarBrush` ánh xạ màu `ColorBgMenuBar` (`#F8FAFC` - Slate siêu sáng 98% độ sáng).
   - Khai báo chuẩn `BgAppBrush` ánh xạ `ColorBgPanel` (`#FFFFFF`), giải quyết triệt để cảnh báo thiếu token.
2. **Hệ thống ControlTemplate ([`SimplePLC.Studio/Styles/Controls.xaml`](file:///SimplePLC.Studio/Styles/Controls.xaml)):**
   - **`Style TargetType="{x:Type Menu}"`:** Nền `{DynamicResource BgMenuBarBrush}` (`#F8FAFC`), viền đáy mảnh 1px `{DynamicResource IndustrialBorderSubtleBrush}` (`#E2E8F0`).
   - **`Style TargetType="{x:Type MenuItem}"`:** Hỗ trợ đầy đủ 4 Roles:
     - `TopLevelHeader`: Nút menu trên thanh bar (`File`, `Edit`, `View`...), nền trong suốt, khi rê chuột hoặc mở menu bật nền `{DynamicResource SelectionHoverBrush}` (`#F1F5F9`), bo góc 4px.
     - `TopLevelItem`: Nút lệnh trực tiếp trên thanh bar.
     - `SubmenuItem`: Các mục lệnh bên trong dropdown (`New Project`, `Open`...), bo góc 4px, icon 16x16, phím tắt (`InputGestureText`) căn phải màu `{DynamicResource TextMutedBrush}`, hover mượt mà với `{DynamicResource SelectionHoverBrush}`.
     - `SubmenuHeader`: Các mục có menu con cấp 2 (`Import ▸`, `Export ▸`...), tự động vẽ mũi tên chỉ hướng vector Fluent 2 `›` tinh tế.
     - Loại bỏ hoàn toàn thẻ `<ScrollViewer>` bên trong popup menu: Ngăn chặn thanh ScrollBar dọc xuất hiện thừa thãi trên các danh sách menu ngắn (`Tools`, `Help`...), giúp toàn bộ dropdown co giãn ôm sát nội dung (compact & snug).
   - **`Style TargetType="{x:Type ContextMenu}"`:** Khung Flyout Card bo góc 6px, nền trắng `#FFFFFF`, viền mỏng 1px `#CBD5E1`, đệm `Padding="4"`, loại bỏ thẻ `ScrollViewer` thừa.
   - **`Style TargetType="{x:Type Separator}"`:** Đường phân cách phẳng 1px `{DynamicResource IndustrialBorderSubtleBrush}` (`#E2E8F0`), đệm `Margin="4,3"`.
   - **`Style TargetType="{x:Type ComboBoxItem}"`:** Bo góc 4px, hover mượt mà với `{DynamicResource SelectionHoverBrush}`.
3. **Cấu trúc Cửa sổ Chính ([`SimplePLC.Studio/MainWindow.xaml`](file:///SimplePLC.Studio/MainWindow.xaml)):**
   - Dòng 0 (`<Menu>`): Xóa bỏ các thuộc tính override cục bộ `Background="{DynamicResource BgWindowBrush}"` và `BorderBrush`, chuyển sang kế thừa 100% style Menu Fluent 2 toàn cục.
   - Dòng 1 (`Navigation Tabs & Quick Connect`): Đổi nền sang `{DynamicResource IndustrialSurfaceInsetBrush}` (`#F8FAFC`), viền đáy `{DynamicResource IndustrialBorderSubtleBrush}` (`#E2E8F0`), tạo thành khối Header liền mạch hoàn hảo.
   - Các `ItemContainerStyle` động cho danh sách dự án gần đây và danh sách cổng COM: Kế thừa chuẩn `BasedOn="{StaticResource {x:Type MenuItem}}"`.
4. **Điều hướng Web Wiki ([`SimplePLC.Studio/ViewModels/MainViewModel.cs`](file:///SimplePLC.Studio/ViewModels/MainViewModel.cs) & [`AppUpdateService.cs`](file:///SimplePLC.Studio/Services/AppUpdateService.cs)):**
   - Trong `AppUpdateService`: Khai báo hằng số cấu hình:
     ```csharp
     public const string DocumentationPortalUrl = "https://SynaptiXCompany.github.io/SynaptiX-IDE-Releases/";
     ```
   - Trong `MainViewModel`: Bổ sung phương thức `GetDocumentationUrl()` tự động phân luồng theo ngôn ngữ giao diện đang hoạt động (`/vi/` khi là Tiếng Việt, `/` khi là Tiếng Anh). Lệnh `OpenDocSpecCommand` mở liên kết này qua trình duyệt mặc định của hệ điều hành.
5. **Unit Tests ([`SimplePLC.Studio.Tests/MainViewModelIntegrationTests.cs`](file:///SimplePLC.Studio.Tests/MainViewModelIntegrationTests.cs)):**
   - Bổ sung 2 bài kiểm thử tự động xác thực URL tài liệu song ngữ:
     - `GetDocumentationUrl_WhenLanguageIsEnglish_ReturnsRootDocumentationPortalUrl`
     - `GetDocumentationUrl_WhenLanguageIsVietnamese_ReturnsVietnameseDocumentationPortalUrl`
   - Bổ sung XML Summary chuẩn cho `UpdateInfo` và `AppUpdateService`.

### 21.3. Kết quả Kiểm thử & Nghiệm thu
- **XAML Token Validator:** `[PASS]` 0 vi phạm trên cả 3 tệp `Colors.xaml`, `Controls.xaml`, `MainWindow.xaml`.
- **Backend Architecture Validator:** `[PASS]` 0 vi phạm Clean Architecture & IEC 61508-3.
- **QA, TDD & Living Documentation Validator:** `[PASS]` 0 vi phạm tài liệu XML Summary, 100% tỷ lệ pass bài kiểm thử.
- **Toàn bộ Solution:** **2,032 / 2,032 unit tests PASS (100%)** (tăng thêm 2 tests, 0 warnings, 0 errors).
- **Trải nghiệm thực tế:** Thanh Header trên cùng trở nên thanh thoát, đồng điệu tuyệt đối với Start Page và FBD workspace; menu dropdown và context menu bung mở mượt mà như một Flyout Card hiện đại; lệnh Help mở thẳng đến cổng Web Wiki VitePress chuyên nghiệp.

---

## 22. Tái cấu trúc Toàn diện Lõi SimplePLC Core: Clean Architecture Inversion, Wire Contract AuxParam (32-byte) & Phân rã Modular Trình biên dịch RuleCompiler

### 22.1. Bối cảnh & Yêu cầu Kỹ thuật
Sau đợt tổng rà soát toàn bộ phần core của dự án, 3 lỗi kiến trúc cốt lõi (Điểm 3, 4, 5) đã được phân tích và lập kế hoạch xử lý triệt để:
1. **Điểm 3 — Lộn ngược phụ thuộc Clean Architecture (Inverted Dependency):**
   - Dự án `SimplePLC.Domain` trước đây chứa `<ProjectReference Include="..\SimplePLC.Protocol\SimplePLC.Protocol.csproj" />`.
   - Vi phạm nghiêm trọng Nguyên lý Clean Architecture: Tầng Domain (Domain Layer) phải là trung tâm nội tại bất biến, có **zero dependencies**, tuyệt đối không được tham chiếu ra các tầng ngoại vi (Protocol, Infrastructure hay Application).
2. **Điểm 4 — Khai thác 4 Bytes Dư thừa trong Wire Contract `SPLC_RuleRecord_t` (32 bytes):**
   - Struct canonical `SPLC_RuleRecord_t` trên đường truyền Modbus RTU / Native USB CDC chiếm đúng 16 thanh ghi (32 bytes).
   - Trước đây struct có 5 bytes reserved tại thanh ghi 13 Low và thanh ghi 14..15 (`reserved[5]`).
   - Khối `SCALE_TAG` trước đó phải "mượn tạm" `threshold_hi` để lưu giá trị `Offset`, gây xung đột nếu sau này khối Scale hoặc các khối Toán học (Math block: ADD, SUB, MUL, DIV) cần dùng Trigger so sánh ngưỡng.
   - Chuyển đổi thanh ghi 14..15 thành trường `aux_param` (Int32 / Float32), giữ lại 1 byte reserved tại thanh ghi 13 Low. Tổng dung lượng giữ bit-exact 32 bytes, không phá vỡ wire layout.
3. **Điểm 5 — Phân rã Trình biên dịch Nguyên khối `RuleCompiler.cs` (~1,615 dòng):**
   - `RuleCompiler.Compile()` trước đây ôm đồm tất cả các trách nhiệm: kiểm tra an toàn cổng Modbus Slave, trích xuất cấu hình MQTT/DTU/DirectWrite, duyệt ngược AST hành động, sinh FB Timer/Counter chuyên dụng, hạ cấp (macro expansion) cho TON/TOF/TP/Cycle, CTU/CTD, Linear Scaler, Modbus Read Status, phân tích xung đột ghi và sắp xếp Topo.
   - Cần phân rã thành các Passes và Lowering strategies độc lập theo đúng nguyên lý SRP (Single Responsibility Principle) và OCP (Open/Closed Principle).

---

### 22.2. Các Thay đổi Triển khai Chi tiết

#### A. Triệt tiêu Phụ thuộc Ngược trong `SimplePLC.Domain` (Điểm 3)
1. **Gỡ bỏ ProjectReference:** Xóa hoàn toàn tham chiếu `SimplePLC.Protocol.csproj` trong `src/SimplePLC.Domain/SimplePLC.Domain.csproj`. `SimplePLC.Domain` hiện tại có **0 project references**.
2. **Di chuyển Hằng số & Kiểu Liệt kê về Domain:**
   - Tạo [`src/SimplePLC.Domain/Constants/SystemTagConstants.cs`](file:///SimplePLC.Domain/Constants/SystemTagConstants.cs): Định nghĩa `SystemTagBaseIndex = 1000`, `SystemTagCount = 8`, và các chỉ số `SysIndexScanTimeMs`, `SysIndexRuleExecCount`, `SysIndexErrorCode`, v.v.
   - Tạo [`src/SimplePLC.Domain/Enums/SPLC_HardwareCapabilities.cs`](file:///SimplePLC.Domain/Enums/SPLC_HardwareCapabilities.cs): Định nghĩa cờ bit năng lực phần cứng chuẩn MCU.
   - Tạo [`src/SimplePLC.Domain/Enums/DeviceEnums.cs`](file:///SimplePLC.Domain/Enums/DeviceEnums.cs): Định nghĩa `SPLC_DeviceClass`, `SPLC_RemoteIoVariant`, `SPLC_DataloggerVariant`, `SPLC_GatewayVariant`, `SPLC_DtuVariant`.
   - Tạo [`src/SimplePLC.Domain/Enums/ModbusSlaveAccessRight.cs`](file:///SimplePLC.Domain/Enums/ModbusSlaveAccessRight.cs) và [`MqttDataType.cs`](file:///SimplePLC.Domain/Enums/MqttDataType.cs).
   - Di chuyển [`TagLayoutMap.cs`](file:///SimplePLC.Domain/Models/TagLayoutMap.cs) vào `SimplePLC.Domain.Models`.
3. **Thiết lập Chiều Phụ thuộc Đúng chuẩn:**
   - Thêm `<ProjectReference Include="..\SimplePLC.Domain\SimplePLC.Domain.csproj" />` vào `src/SimplePLC.Protocol/SimplePLC.Protocol.csproj` (Protocol phụ thuộc vào Domain, tuân thủ Clean Architecture).
   - Thêm [`GlobalUsings.cs`](file:///SimplePLC.Protocol/GlobalUsings.cs) trong Protocol chuyển tiếp các kiểu enum cho người dùng thư viện Protocol.

#### B. Nâng cấp Wire Contract `AuxParam` 32-Byte (Điểm 4)
1. **C Header Firmware ([`src/SimplePLC.Protocol/Firmware/simpleplc_protocol_v2_0.h`](file:///SimplePLC.Protocol/Firmware/simpleplc_protocol_v2_0.h)):**
   ```c
   /* Reg 13 (byte 26..27): Flags & Reserved Byte */
   uint8_t flags;            /* Byte 26: SPLC_RULE_FLAG_* */
   uint8_t reserved_byte;    /* Byte 27: Reserved for future byte-level routing */
   /* Reg 14..15 (byte 28..31): Aux Param (Int32 / Float32) */
   union {
       int32_t aux_param;    /* Signed 32-bit auxiliary parameter */
       float aux_param_f;    /* IEEE 754 32-bit floating point auxiliary parameter */
   };
   ```
2. **DTO & Codec ([`RuleRecordDto.cs`](file:///SimplePLC.Protocol/Dto/RuleRecordDto.cs) & [`RegisterCodec.cs`](file:///SimplePLC.Protocol/Codec/RegisterCodec.cs)):**
   - Bổ sung `AuxParam` và `AuxParamFloat` vào `RuleRecordDto`.
   - `RegisterCodec.EncodeRuleRecord`: Mã hóa `AuxParam` vào 2 thanh ghi cuối `Slice(14, 2)`.
   - `RegisterCodec.DecodeRuleRecord`: Giải mã `AuxParam` từ `Slice(14, 2)`.
3. **Mô hình Miền & Hạ cấp ([`ActionModel.cs`](file:///SimplePLC.Domain/Models/ActionModel.cs) & [`RuleMapper.cs`](file:///SimplePLC.Application/Mapping/RuleMapper.cs)):**
   - `ActionModel`: Bổ sung thuộc tính `AuxParameter` (double) và constructor hỗ trợ tham số này.
   - `RuleMapper`: Ánh xạ `Action.AuxParameter` $\leftrightarrow$ `dto.AuxParam` / `dto.AuxParamFloat`.
   - `McuReferenceSimulator`: Khối `SCALE_TAG` ưu tiên đọc `AuxParam` trước, fallback về `ThresholdHi` nếu bằng 0 (đảm bảo tương thích ngược 100%).

#### C. Phân rã Modular Trình biên dịch `RuleCompiler.cs` (Điểm 5)
1. **Mô hình Dữ liệu Ứng viên Luật ([`CandidateRule.cs`](file:///SimplePLC.Application/Logic/Compilation/CandidateRule.cs)):**
   - Record đại diện cho một candidate rule độc lập trước khi qua phân tích truy cập và sắp xếp thứ tự topo.
2. **Bộ Tiện ích Biên dịch ([`CompilationHelpers.cs`](file:///SimplePLC.Application/Logic/Compilation/CompilationHelpers.cs)):**
   - Tập trung các phương thức: `InvertCompareOp`, `IsFloatThreshold`, `GetNodeLabel`, `ResolveSourcePortTagIndex`.
3. **Trích xuất Cấu hình Ngoại vi ([`PeripheralConfigExtractor.cs`](file:///SimplePLC.Application/Logic/Compilation/PeripheralConfigExtractor.cs)):**
   - Xử lý kiểm tra an toàn cổng Modbus Slave (Safety Gate).
   - Trích xuất cấu hình MQTT Publish/Subscribe, Modbus Master Read/Write, Direct Physical Write, DTU HTTP & TCP Client.
4. **Truy vết Đường đi Hành động ([`ActionPathTracer.cs`](file:///SimplePLC.Application/Logic/Compilation/ActionPathTracer.cs)):**
   - Duyệt ngược dòng từ Action terminal qua Guard, Trigger, Comparator, Input và sinh các chẩn đoán (diagnostics) chuẩn xác.
5. **Các Chiến lược Hạ cấp Macro ([`src/SimplePLC.Application/Logic/Compilation/MacroLowering/`](file:///SimplePLC.Application/Logic/Compilation/MacroLowering/)):**
   - [`TimerMacroLowering.cs`](file:///SimplePLC.Application/Logic/Compilation/MacroLowering/TimerMacroLowering.cs): Mở rộng TON, TOF, TP, CYCLE thành các cặp luật khởi động, duy trì và ngắt an toàn.
   - [`CounterMacroLowering.cs`](file:///SimplePLC.Application/Logic/Compilation/MacroLowering/CounterMacroLowering.cs): Mở rộng CTU, CTD thành các luật đếm xung, reset, trip và clear ngưỡng.
   - [`ScaleMacroLowering.cs`](file:///SimplePLC.Application/Logic/Compilation/MacroLowering/ScaleMacroLowering.cs): Mở rộng khối Scale, gán `Offset` vào `AuxParameter` (đồng thời duy trì `ThresholdHi` cho legacy runtime).
   - [`ModbusStatusMacroLowering.cs`](file:///SimplePLC.Application/Logic/Compilation/MacroLowering/ModbusStatusMacroLowering.cs): Mở rộng các cờ Valid / Error của Modbus Master Read.
6. **Bộ Điều phối Thu gọn ([`RuleCompiler.cs`](file:///SimplePLC.Application/Logic/Compilation/RuleCompiler.cs)):**
   - Giảm dung lượng từ ~1,615 dòng xuống còn ~260 dòng. Đóng vai trò Pipeline Orchestrator sạch sẽ, dễ bảo trì, sẵn sàng mở rộng các khối Math trong tương lai.

---

### 22.3. Kết quả Kiểm thử & Nghiệm thu
- **Biên dịch Toàn bộ Solution:** `dotnet build SimplePLC.sln` đạt **0 errors, 0 warnings** trên cả 12/12 projects.
- **Backend Architecture Validation:**
  ```powershell
  powershell -ExecutionPolicy Bypass -File .agents/skills/synaptix-backend-arch/scripts/validate-backend-arch.ps1
  ```
  $\rightarrow$ **PASS**: 0 vi phạm kiến trúc Clean Architecture, 0 vi phạm thread/exception.
- **QA, TDD & Living Documentation Validation:**
  ```powershell
  powershell -ExecutionPolicy Bypass -File .agents/skills/synaptix-qa-tdd/scripts/validate-tdd-docs.ps1
  ```
  $\rightarrow$ **PASS**: 0 vi phạm XML summary, 100% Code-to-Test mapping.
- **Toàn bộ Test Suite:** **2,052 / 2,052 unit tests PASS (100%)**:
  - `SimplePLC.Domain.Tests`: 55 passed
  - `SimplePLC.Protocol.Tests`: 232 passed (bổ sung 9 bài kiểm thử chuyên sâu cho `AuxParam` Int/Float boundary)
  - `SimplePLC.Infrastructure.Tests`: 182 passed
  - `SimplePLC.Application.Tests`: 249 passed (bổ sung 7 bài kiểm thử cho `RuleCompilerModularizationTests`)
  - `SimplePLC.Studio.Tests`: 1,334 passed




---

## 23. Chuẩn hóa Lề & Dóng Hàng Sát Lề Cho Menu Dropdown Flyout (Microsoft Fluent 2 Desktop)

### 23.1. Bối cảnh & Vấn đề Cần Khắc Phục
- **Khoảng thụt đầu dòng 34px bất thường:** Trong [`SimplePLC.Studio/Styles/Controls.xaml`](file:///SimplePLC.Studio/Styles/Controls.xaml), cột Icon trước đây được định nghĩa với kích thước cố định `Width="22"` kèm `Padding="8,5"` và `Margin="4,0,10,0"`. Ngay cả khi menu con không có icon nào (như `File`, `Tools`, `Help`), cột này vẫn chiếm không gian 22px, khiến nhãn text (`Start Page...`, `New Project`, `Open Project`) bị thụt sâu vào giữa viên thuốc hover (~34px).
- **Lệch trục với thanh Header:** Nút `File` trên thanh Menu Bar trên cùng có lề trái chỉ 8px + 4px viền ngoài = 12px. Sự chênh lệch 12px vs 34px khiến danh sách menu con trông lơ lửng, mất tính dóng hàng tự nhiên.
- **Lệch trục mép phải giữa phím tắt và mũi tên submenu:** `SubmenuHeaderTemplate` dùng 5 cột với mũi tên ở cột 4, trong khi `SubmenuItemTemplate` dùng 4 cột với phím tắt ở cột 3. Điều này khiến các phím tắt (`Ctrl+N`, `Ctrl+O`) và mũi tên submenu (`›`) không dóng thẳng hàng ở mép phải.

### 23.2. Giải pháp Triển khai (Fluent 2 Architecture)
1. **Thu gọn Cột Icon Động (`Width="Auto"`):**
   - Chuyển `ColumnDefinition Width="22"` sang `Width="Auto"` kết hợp `SharedSizeGroup="MenuItemIconColumnGroup"`.
   - Khi mục không có icon (`Icon == null`) và không có dấu chọn (`IsChecked == false`), cả `Icon` và `Checkmark` đều có `Visibility="Collapsed"`, giúp cột 0 thu gọn tuyệt đối về **0px**.
   - Chữ tiêu đề menu bắt đầu ngay ngắn tại lề trái `Padding="10,5"`, cách mép viên thuốc hover đúng 10px và dóng thẳng hàng tự nhiên với các nút trên thanh Header Menu.
2. **Tích hợp Checkmark Vector `✓` Tinh tế:**
   - Bổ sung `<Path x:Name="Checkmark" ... />` vector Fluent 2 với nét vẽ bo tròn (`StrokeRoundCap`) màu `{DynamicResource PrimaryBrush}` khi `IsChecked="True"`.
   - Khi có mục được chọn (như ngôn ngữ, cổng COM), cột Icon mở rộng gọn gàng 14px + 8px margin và chia sẻ kích thước đều cho các mục trong cùng submenu.
3. **Hợp nhất Mép Phải (Unified Right Column Alignment):**
   - Đồng bộ cấu trúc Grid của cả `SubmenuItemTemplate` và `SubmenuHeaderTemplate` về đúng 3 cột:
     - `Col 0 (Width="Auto" SharedSizeGroup="MenuItemIconColumnGroup")`: Chứa Icon / Checkmark.
     - `Col 1 (Width="*")`: Chứa Header Text.
     - `Col 2 (Width="Auto" SharedSizeGroup="MenuItemGestureColumnGroup")`: Chứa phím tắt hoặc mũi tên con `›`.
   - Đặt `HorizontalAlignment="Right"` và `Margin="16,0,2,0"` cho cả `GestureText` và mũi tên `›`. Nhờ đó, tất cả phím tắt (`Ctrl+N`, `Ctrl+Shift+S`, `Alt+F4`) và mũi tên cấp 2 (`›`) dóng thẳng tắp mép phải trên cùng một đường trục đứng.
   - Thêm Trigger ẩn `GestureText` khi rỗng/null, giúp các menu không có phím tắt (như menu `Help`) thu gọn hoàn toàn cột phải mà không tạo khoảng trống giả.
4. **Phạm vi Chia sẻ Kích thước Độc lập (`Grid.IsSharedSizeScope="True"`):**
   - Đặt `Grid.IsSharedSizeScope="True"` trực tiếp trên `StackPanel` bên trong mỗi `Popup`, đảm bảo việc tính toán kích thước cột được gói gọn độc lập cho từng popup riêng biệt.

### 23.3. Kết quả Kiểm thử & Nghiệm thu
- **XAML Design Token Validator:**
  ```powershell
  powershell -ExecutionPolicy Bypass -File .agents/skills/synaptix-ui-ux/scripts/validate-xaml-tokens.ps1 -FilePath "SimplePLC.Studio/Styles/Controls.xaml"
  ```
  $\rightarrow$ **PASS**: 0 vi phạm mã màu thô, 100% DynamicResource tokens.
- **Backend Architecture Validator:**
  ```powershell
  powershell -ExecutionPolicy Bypass -File .agents/skills/synaptix-backend-arch/scripts/validate-backend-arch.ps1
  ```
  $\rightarrow$ **PASS**: 0 vi phạm kiến trúc.
- **QA/TDD & Living Docs Validator:**
  ```powershell
  powershell -ExecutionPolicy Bypass -File .agents/skills/synaptix-qa-tdd/scripts/validate-tdd-docs.ps1
  ```
  $\rightarrow$ **PASS**: 0 vi phạm XML docs, 100% test pass rate.
- **Biên dịch Toàn bộ Solution:** `dotnet build SimplePLC.sln` đạt **0 errors, 0 warnings**.

---

## 24. Chuẩn hóa Giao diện Toàn cầu Cho Dropdown / ComboBox (Microsoft Fluent 2 Desktop)

### 24.1. Bối cảnh & Hiện tượng Đối nghịch
- **Hiện tượng đối nghịch màu sắc:** Trên giao diện SynaptiX IDE, ô chọn `INT32` trong bảng Tag Catalog hiển thị rất hiện đại (nền trắng/slate phẳng, bo góc mềm `CornerRadius="4"`, mũi tên vector chevron thanh mảnh). Trong khi đó, các dropdown cấu hình truyền thông như `Data Bits: 8`, `Baud Rate`, `Parity`, `Stop Bits` lại bị loang màu xám gradient xi-măng cổ điển của Windows 7/Aero2 (`#E5E5E5`), viền bevel nhọn góc (`CornerRadius="0"`), và mũi tên tam giác đen đặc.
- **Nguyên nhân gốc rễ:** Trong [`Controls.xaml:548`](file:///SimplePLC.Studio/Styles/Controls.xaml#L548), style toàn cục `<Style TargetType="ComboBox">` chỉ thiết lập các thuộc tính bề mặt mà không định nghĩa `ControlTemplate`. Khi không có template, WPF buộc phải dùng template mặc định của Windows (`Aero2.msstyles`). Template này nhúng một `ClassicToggleButton` có mã nhị phân tự vẽ nút xám gradient và bỏ qua thuộc tính `Background`.

### 24.2. Giải pháp Kỹ thuật Triệt để (Fluent 2 Architecture)
1. **Thiết kế Template Nút Bấm ToggleButton Chuẩn Fluent 2 (`FluentComboBoxToggleButton`):**
   - Viền ngoài bo góc `CornerRadius="4"`, hỗ trợ token màu `{DynamicResource BgPanelBrush}` và `{DynamicResource IndustrialBorderDefaultBrush}`.
   - Thay thế mũi tên tam giác đặc Windows bằng SVG vector Path: `<Path Data="M 0 0 L 3.5 3.5 L 7 0" Stroke="{DynamicResource TextMutedBrush}" StrokeThickness="1.2" StrokeStartLineCap="Round" StrokeEndLineCap="Round" />`.
   - Hiệu ứng phản hồi trạng thái: Hover chuyển nền `{DynamicResource SelectionHoverBrush}` và viền `{DynamicResource BorderMediumBrush}`; khi flyout mở (`IsChecked="True"`), viền và mũi tên sáng màu thương hiệu `{DynamicResource PrimaryBrush}`.
2. **Nâng cấp `<Style TargetType="ComboBox">` với `ControlTemplate` Hoàn chỉnh:**
   - **Chống đè chữ lên mũi tên:** Bọc `ContentPresenter` và `PART_EditableTextBox` trong một subgrid có cột phải cố định `Width="18"`. Nhờ đó, bất kể nhãn dài đến đâu, chữ luôn kết thúc an toàn trước mũi tên chevron.
   - **Flyout Dropdown Nổi:** Thẻ chứa danh sách mục con `DropDownBorder` có `CornerRadius="6"`, nền `{DynamicResource BgPanelBrush}` và viền `{DynamicResource IndustrialBorderDefaultBrush}`.
   - **Hỗ trợ `IsEditable` an toàn:** Tích hợp `PART_EditableTextBox` ẩn và tự động hiển thị khi `IsEditable="True"`.
3. **Kế thừa Tự động cho Toàn bộ Hệ thống:**
   - Hơn **109 ComboBox** trên toàn bộ IDE (Modbus, DTU Gateway, MQTT QoS, HTTP Client, Logic Inspector, Timer/Counter Type, Simulator) tự động thừa hưởng template hiện đại này.

### 24.3. Kết quả Kiểm thử & Nghiệm thu
- **XAML Token Validator:**
  ```powershell
  powershell -ExecutionPolicy Bypass -File .agents/skills/synaptix-ui-ux/scripts/validate-xaml-tokens.ps1 -FilePath "SimplePLC.Studio/Styles/Controls.xaml"
  ```
  $\rightarrow$ **PASS**: 0 vi phạm mã màu thô, 100% DynamicResource tokens.
- **Backend Architecture Validator:**
  ```powershell
  powershell -ExecutionPolicy Bypass -File .agents/skills/synaptix-backend-arch/scripts/validate-backend-arch.ps1
  ```
  $\rightarrow$ **PASS**: 0 vi phạm kiến trúc Clean Architecture.
- **QA/TDD & Docs Validator:**
  ```powershell
  powershell -ExecutionPolicy Bypass -File .agents/skills/synaptix-qa-tdd/scripts/validate-tdd-docs.ps1
  ```
  $\rightarrow$ **PASS**: 0 vi phạm XML docs, 100% test suite pass rate.
- **Biên dịch & Test Suite:**
  - `dotnet build SimplePLC.sln`: **0 Warning(s), 0 Error(s)**.
  - `dotnet test SimplePLC.sln --no-build`: **2,055 / 2,055 tests PASS (100%)**.

---

## 25. Chuẩn hóa Giao diện Ô Điền Text (TextBox) & Cụm Nút Phân Đoạn (Microsoft Fluent 2 Desktop)

### 25.1. Bối cảnh & Hiện tượng Đối nghịch
- **Lệch pha hình học giữa các control trên cùng một hàng:** Khi đặt ô chọn ComboBox (như `Periodic` đã bo góc 4px) cạnh ô nhập liệu TextBox (như `1000`) và cụm nút phân đoạn QoS `[ 0 ] [ 1 ] [ 2 ]`, ô TextBox và các nút bấm trước đó hiển thị dạng hình chữ nhật vuông nhọn hoắt 90° (`CornerRadius="0"`), tạo cảm giác chắp vá, thiếu đồng bộ của một IDE công nghiệp chuyên nghiệp.
- **Nguyên nhân gốc rễ:** `<Style TargetType="TextBox">` trong [`Controls.xaml`](file:///SimplePLC.Studio/Styles/Controls.xaml) trước đây không có `ControlTemplate` mà chỉ đặt các thuộc tính màu cục bộ, khiến WPF fallback về template Windows Aero2 với góc vuông và màu viền xám tĩnh. Các nút QoS cũng bị thiếu `ControlTemplate` bo góc.

### 25.2. Giải pháp Kỹ thuật Triệt để (Fluent 2 Architecture)
1. **Thiết kế `ControlTemplate` Hoàn chỉnh cho `<Style TargetType="{x:Type TextBox}">`:**
   - Đặt `CornerRadius="4"` trên thẻ `Border` ngoài cùng, tạo góc bo mềm mại chuẩn Microsoft Fluent 2 (Rule 1.3).
   - Tích hợp `PART_ContentHost` bên trong `ScrollViewer` không viền, `Padding="7,4"` thoáng đãng.
   - Thêm Trigger phản hồi trực quan:
     - `IsMouseOver="True"`: Đổi viền sang `{DynamicResource BorderMediumBrush}`.
     - `IsKeyboardFocused="True"`: Đổi viền sang màu xanh nhận diện thương hiệu `{DynamicResource PrimaryBrush}` (Focus Ring).
     - `IsEnabled="False"` / `IsReadOnly="True"`: Nền chuyển sang `{DynamicResource BgSubtleBrush}`, `Opacity="0.45"`.
   - Toàn bộ token sử dụng `{DynamicResource ...}` triệt để theo Rule 1.1.
2. **Đồng bộ Cụm Nút Phân Đoạn QoS (`MqttTelemetryPublishControl` & `MqttCommandSubscribeControl`):**
   - Kế thừa `BasedOn="{StaticResource TactileToolbarBtnStyle}"` có `CornerRadius="4"`.
   - Toàn bộ hàng ngang (Dropdown $\rightarrow$ TextBox $\rightarrow$ Nút chọn) giờ đây chia sẻ chung bán kính bo góc 4px và ngôn ngữ thiết kế nhất quán 100%.

### 25.3. Kết quả Kiểm thử & Nghiệm thu
- **XAML Token Validator:**
  ```powershell
  powershell -ExecutionPolicy Bypass -File .agents/skills/synaptix-ui-ux/scripts/validate-xaml-tokens.ps1 -FilePath "SimplePLC.Studio/Styles/Controls.xaml"
  powershell -ExecutionPolicy Bypass -File .agents/skills/synaptix-ui-ux/scripts/validate-xaml-tokens.ps1 -FilePath "SimplePLC.Studio/Controls/MqttTelemetryPublishControl.xaml"
  powershell -ExecutionPolicy Bypass -File .agents/skills/synaptix-ui-ux/scripts/validate-xaml-tokens.ps1 -FilePath "SimplePLC.Studio/Controls/MqttCommandSubscribeControl.xaml"
  ```
  $\rightarrow$ **PASS**: 0 vi phạm mã màu thô trên tất cả các file.
- **Backend Architecture Validator:** `[PASS]` 0 vi phạm kiến trúc.
- **QA/TDD & Docs Validator:** `[PASS]` 0 vi phạm XML docs, 100% test pass rate.
- **Biên dịch & Test Suite:**
  - `dotnet build SimplePLC.sln`: **0 Warning(s), 0 Error(s)**.
  - `dotnet test SimplePLC.sln --no-build`: **2,055 / 2,055 tests PASS (100%)**.

---

## 26. Chuẩn hóa Toàn diện Bộ Điều Khiển Tương Tác (PasswordBox, CheckBox, Button Ngầm Định, RadioButton, ToolTip) theo Microsoft Fluent 2 Desktop

### 26.1. Bối cảnh & Hiện tượng Đối nghịch
- **Lệch pha giữa TextBox và PasswordBox:** Sau khi nâng cấp `TextBox` lên `CornerRadius="4"`, tại màn hình cấu hình Wi-Fi ([`WifiConfigControl.xaml`](file:///SimplePLC.Studio/Controls/WifiConfigControl.xaml)), ô `Wi-Fi Network Name (SSID)` đã bo góc `4px`, nhưng ô `Security Password (WPA2/WPA3)` ngay bên dưới vẫn bị **vuông nhọn 90° (`CornerRadius="0"`)** do `PasswordBox` là một lớp điều khiển riêng biệt trong WPF và chưa có style toàn cục.
- **Hộp kiểm CheckBox & Nút bấm mặc định:** Các hộp kiểm `CheckBox` (như `Automatically reconnect on signal drop`, DHCP, Enable service) và các nút `Button` không khai báo style thủ công (nút Close trong DeviceInfo, Run Test, Cancel/Apply trong Dialogs) vẫn sử dụng template mặc định Windows Aero2 cổ điển.

### 26.2. Giải pháp Kỹ thuật Triệt để (Fluent 2 Architecture)
1. **Chuẩn hóa `<Style TargetType="{x:Type PasswordBox}">`:**
   - Xây dựng `ControlTemplate` có `Border CornerRadius="4"`, màu nền `{DynamicResource BgPanelBrush}`, và Triggers viền Focus Ring `{DynamicResource PrimaryBrush}` đồng nhất 100% với `TextBox`.
   - Cập nhật [`WifiConfigControl.xaml`](file:///SimplePLC.Studio/Controls/WifiConfigControl.xaml) sử dụng `BasedOn="{StaticResource {x:Type PasswordBox}}"` để kế thừa góc bo tròn `4px` và viền mới.
2. **Chuẩn hóa `<Style TargetType="{x:Type CheckBox}">`:**
   - Hộp kiểm 16x16 bo góc nhẹ `CornerRadius="3"`, viền `{DynamicResource IndustrialBorderDefaultBrush}`.
   - Khi `IsChecked="True"`: Nền và viền chuyển sang màu xanh nhận diện thương hiệu `{DynamicResource PrimaryBrush}`, dấu tick vector SVG `✓` (`Data="M 3 7.5 L 6 10.5 L 12 3.5"`) màu trắng sắc nét thay thế dấu tick răng cưa của Windows.
   - Hỗ trợ trạng thái ba trạng thái (Indeterminate) với đường gạch ngang `M 4 8 L 12 8`.
3. **Chuẩn hóa `<Style TargetType="{x:Type Button}">` Ngầm Định Toàn Cục:**
   - Gán `BasedOn="{StaticResource BtnSecondary}"` để tất cả các nút bấm không có style chỉ định tự động sở hữu góc bo `CornerRadius="4"`, nền phẳng và hiệu ứng hover mượt mà.
4. **Chuẩn hóa `<Style TargetType="{x:Type RadioButton}">`:**
   - Vòng tròn phẳng 16x16, viền xám tinh tế; khi checked hiển thị chấm tròn xanh 8x8 ở trung tâm.
5. **Chuẩn hóa `<Style TargetType="{x:Type ToolTip}">`:**
   - Bo góc `CornerRadius="4"`, nền `{DynamicResource BgPanelBrush}`, viền xám công nghiệp và padding thoáng đãng `8,4`.

### 26.3. Kết quả Kiểm thử & Nghiệm thu
- **XAML Token Validator:**
  ```powershell
  powershell -ExecutionPolicy Bypass -File .agents/skills/synaptix-ui-ux/scripts/validate-xaml-tokens.ps1 -FilePath "SimplePLC.Studio/Styles/Controls.xaml"
  powershell -ExecutionPolicy Bypass -File .agents/skills/synaptix-ui-ux/scripts/validate-xaml-tokens.ps1 -FilePath "SimplePLC.Studio/Controls/WifiConfigControl.xaml"
  ```
  $\rightarrow$ **PASS**: 0 vi phạm mã màu thô trên tất cả các file.
- **Backend Architecture Validator:** `[PASS]` 0 vi phạm kiến trúc.
- **QA/TDD & Docs Validator:** `[PASS]` 0 vi phạm XML docs, 100% test pass rate.
- **Biên dịch & Test Suite:**
  - `dotnet build SimplePLC.sln`: **0 Warning(s), 0 Error(s)**.
  - `dotnet test SimplePLC.sln --no-build`: **2,055 / 2,055 tests PASS (100%)**.


---

## 27. Tinh chỉnh UI/UX Fluent 2: Đồng bộ kích thước Controls, Căn lề Payload Top, Căn giữa CheckMark & Sửa lỗi Icon Quick Connect

### 27.1. Bối cảnh & Hiện tượng phát hiện từ người dùng
1. **Lệch kích thước ô nhập liệu (24px, 25px, 26px, 28px, 30px):** Các ô form fields (TextBox, ComboBox, segmented buttons) trên các màn hình MQTT, Wi-Fi và bảng dữ liệu có chiều cao không đồng đều.
2. **Payload JSON nằm lơ lửng ở giữa card:** Các ô xem trước/kiểm thử Payload (Payload Preview & Payload Test) bị thuộc tính `VerticalContentAlignment="Center"` đẩy toàn bộ nội dung JSON vào giữa dọc, tạo khoảng trống thừa ở trên và dưới.
3. **Mất icon nút Refresh trên thanh Quick Connect:** Nút Refresh cổng COM kích thước 26x26px bị Style `BtnSecondary` áp `Padding="12,6"`, dẫn đến bề rộng còn lại cho `ContentPresenter` bị ép về 0px ($26 - 24 - 2 = 0$), khiến icon vector 12x12 bị triệt tiêu hoàn toàn.
4. **Dấu tick CheckBox bị lệch tâm:** Nét vẽ checkmark `✓` trong CheckBox 16x16 bị lệch sang trái và lên trên do thiếu `Stretch="Uniform"` và kích thước hình học chuẩn.

### 27.2. Giải pháp kỹ thuật triệt để
1. **Đồng bộ chiều cao Controls chuẩn Fluent 2 Desktop:**
   - Trong [`SimplePLC.Studio/Styles/Controls.xaml`](file:///SimplePLC.Studio/Styles/Controls.xaml): Thiết lập `<Setter Property="MinHeight" Value="28" />` cho `TextBox`, `PasswordBox`, và `ComboBox`.
   - Trong [`MqttCommandSubscribeControl.xaml`](file:///SimplePLC.Studio/Controls/MqttCommandSubscribeControl.xaml): Đồng bộ Topic TextBox (`Height="28"`), nút Clean (`Height="28"`), Grid QoS buttons (`Height="28"`), ComboBox Queue Mode (`Height="28"`).
   - Trong [`WifiConfigControl.xaml`](file:///SimplePLC.Studio/Controls/WifiConfigControl.xaml): Chuẩn hóa các ô SSID, Password, ComboBox APs, Show Password CheckBox, và các ô IP tĩnh từ 30px về đồng nhất `Height="28"`.
2. **Căn lề trên (`Top`) cho Payload Preview & Test:**
   - Trong [`Controls.xaml`](file:///SimplePLC.Studio/Styles/Controls.xaml): Bổ sung trigger trong Style toàn cục `TextBox`:
     ```xaml
     <Style.Triggers>
         <Trigger Property="AcceptsReturn" Value="True">
             <Setter Property="VerticalContentAlignment" Value="Top" />
         </Trigger>
     </Style.Triggers>
     ```
   - Trong [`MqttTelemetryPublishControl.xaml`](file:///SimplePLC.Studio/Controls/MqttTelemetryPublishControl.xaml), [`MqttCommandSubscribeControl.xaml`](file:///SimplePLC.Studio/Controls/MqttCommandSubscribeControl.xaml), và [`HttpClientConfigControl.xaml`](file:///SimplePLC.Studio/Controls/HttpClientConfigControl.xaml): Thêm tường minh `VerticalContentAlignment="Top"` và `AcceptsReturn="True"`.
3. **Khôi phục Icon nút Refresh trên Quick Connect:**
   - Trong [`MainWindow.xaml`](file:///SimplePLC.Studio/MainWindow.xaml): Cập nhật nút Refresh sang `Style="{StaticResource TactileToolbarBtnStyle}"` và gán `Padding="0"`, bảo đảm toàn bộ không gian 24x24px khả dụng cho icon vector `IconIecTimerCycle`.
4. **Căn giữa toán học cho CheckMark trong CheckBox:**
   - Trong [`Controls.xaml`](file:///SimplePLC.Studio/Styles/Controls.xaml): Đặt `Width="9" Height="7"`, `Stretch="Uniform"`, `HorizontalAlignment="Center"`, `VerticalAlignment="Center"` và chuẩn hóa tọa độ vector `Data="M 1 4.5 L 3.5 7 L 8 1.5"` với `Round` caps.
   - Trạng thái Indeterminate: `Width="8" Height="2" Stretch="Uniform" Data="M 1 1 L 7 1"`.
   - Tạo ra lề ngang $2.5\text{px}$ và lề dọc $3.5\text{px}$ đối xứng toán học tuyệt đối trong khung 14x14px.

### 27.3. Kết quả Kiểm thử & Nghiệm thu
- **XAML Token Validator:**
  ```powershell
  powershell -ExecutionPolicy Bypass -File .agents/skills/synaptix-ui-ux/scripts/validate-xaml-tokens.ps1 -FilePath "SimplePLC.Studio/Styles/Controls.xaml"
  powershell -ExecutionPolicy Bypass -File .agents/skills/synaptix-ui-ux/scripts/validate-xaml-tokens.ps1 -FilePath "SimplePLC.Studio/MainWindow.xaml"
  powershell -ExecutionPolicy Bypass -File .agents/skills/synaptix-ui-ux/scripts/validate-xaml-tokens.ps1 -FilePath "SimplePLC.Studio/Controls/MqttCommandSubscribeControl.xaml"
  powershell -ExecutionPolicy Bypass -File .agents/skills/synaptix-ui-ux/scripts/validate-xaml-tokens.ps1 -FilePath "SimplePLC.Studio/Controls/MqttTelemetryPublishControl.xaml"
  powershell -ExecutionPolicy Bypass -File .agents/skills/synaptix-ui-ux/scripts/validate-xaml-tokens.ps1 -FilePath "SimplePLC.Studio/Controls/HttpClientConfigControl.xaml"
  powershell -ExecutionPolicy Bypass -File .agents/skills/synaptix-ui-ux/scripts/validate-xaml-tokens.ps1 -FilePath "SimplePLC.Studio/Controls/WifiConfigControl.xaml"
  ```
  $\rightarrow$ **PASS**: 0 vi phạm trên tất cả các file.
- **Backend Architecture Validator:** `[PASS]` 0 vi phạm kiến trúc.
- **QA/TDD & Docs Validator:** `[PASS]` 0 vi phạm XML docs, 100% test pass rate.
- **Biên dịch & Test Suite:**
  - `dotnet build SimplePLC.sln`: **0 Warning(s), 0 Error(s)**.
  - `dotnet test SimplePLC.sln --no-build`: **2,055 / 2,055 tests PASS (100%)**.


---

## 28. Chuẩn hóa Toàn diện Typography theo Phương án A (Microsoft Fluent 2 Desktop: 100% Segoe UI cho Form Inputs)

### 28.1. Bối cảnh & Hiện tượng bất đồng bộ phông chữ
- **Hiện tượng:** Tại màn hình cấu hình HTTP ([`HttpClientConfigControl.xaml`](file:///SimplePLC.Studio/Controls/HttpClientConfigControl.xaml)) và các màn hình MQTT, Wi-Fi:
  - Ô ComboBox bên trái (`POST (Create / Push Telemetry)`) hiển thị phông chữ hệ thống **`Segoe UI`** (tỷ lệ tự nhiên).
  - Ô TextBox bên phải (`https://api.factory-cloud.io/v1/telemetry`) bị gán cứng **`FontFamily="Consolas"`** (phông đơn khoảng monospace).
  - Hai ô nằm cạnh nhau trên cùng một hàng nhưng mang hai kiểu phông chữ và khoảng cách chữ hoàn toàn khác nhau, gây mất đồng nhất giao diện nghiêm trọng.
  - Tương tự, ô `Timeout (seconds)` và các ô IP tĩnh trong Wi-Fi, MQTT Topic, Field Name cũng bị gán cứng `Consolas`.

### 28.2. Giải pháp Kỹ thuật Triệt để (Phương án A)
1. **Thiết lập chuẩn mực tại Style Toàn cục ([`Controls.xaml`](file:///SimplePLC.Studio/Styles/Controls.xaml)):**
   - Thêm `<Setter Property="FontFamily" Value="Segoe UI" />` vào:
     - `<Style TargetType="{x:Type TextBox}">`
     - `<Style TargetType="{x:Type PasswordBox}">`
     - `<Style TargetType="{x:Type ComboBox}">`
     - `<Style x:Key="BtnSecondary" TargetType="Button">`
   - Đảm bảo toàn bộ controls dạng form khi không gán phông thủ công sẽ mặc định 100% sử dụng `Segoe UI`.
2. **Loại bỏ triệt để `FontFamily="Consolas"` trên toàn bộ Form Inputs:**
   - [`HttpClientConfigControl.xaml`](file:///SimplePLC.Studio/Controls/HttpClientConfigControl.xaml): URL, Custom Headers, TimeoutSec, PeriodicIntervalSec, Key, Method ComboBox (chuẩn hóa toàn bộ về `Segoe UI` và đồng bộ `Height="28"`).
   - [`MqttCommandSubscribeControl.xaml`](file:///SimplePLC.Studio/Controls/MqttCommandSubscribeControl.xaml): Topic TextBox, FieldName TextBox.
   - [`MqttTelemetryPublishControl.xaml`](file:///SimplePLC.Studio/Controls/MqttTelemetryPublishControl.xaml): Topic TextBox, IntervalMs TextBox, DeadbandText TextBox, Key TextBox.
   - [`MqttBrokerConfigControl.xaml`](file:///SimplePLC.Studio/Controls/MqttBrokerConfigControl.xaml): WillPayload TextBox.
   - [`TcpClientConfigControl.xaml`](file:///SimplePLC.Studio/Controls/TcpClientConfigControl.xaml): RemoteHost, RemotePort, ReconnectDelaySec, Delimiter, PollIntervalMs, TxTimeoutMs, RxTimeoutMs (chuẩn hóa về `Segoe UI` và `Height="28"`).
   - [`ModbusPortConfigControl.xaml`](file:///SimplePLC.Studio/Controls/ModbusPortConfigControl.xaml): SlaveId, Address, ScaleText trong bảng kênh Modbus Master.
   - [`WifiConfigControl.xaml`](file:///SimplePLC.Studio/Controls/WifiConfigControl.xaml): Static IP inputs (`StaticIp`, `StaticSubnet`, `StaticGateway`, `StaticDns`).
3. **Bảo vệ nghiêm ngặt vùng kỹ thuật đặc thù (Protected Monospace Zones):**
   - Giữ nguyên `Consolas` cho:
     - Khung hiển thị Payload JSON Preview & Test (`PreviewPayloadText`, `SimulationInput`, `TemplatePreviewText`).
     - Khung soạn thảo Body Template đa dòng (`BodyTemplate`, `StreamPayloadTemplate`).
     - Chuỗi byte nhị phân thô (`RawHex` trong `RuleTableView`).
     - Cột địa chỉ Modbus trong DataGrid `TagCatalogView`.

### 28.3. Kết quả Kiểm thử & Nghiệm thu
- **XAML Token Validator:**
  ```powershell
  powershell -ExecutionPolicy Bypass -File .agents/skills/synaptix-ui-ux/scripts/validate-xaml-tokens.ps1 -FilePath "SimplePLC.Studio/Styles/Controls.xaml"
  powershell -ExecutionPolicy Bypass -File .agents/skills/synaptix-ui-ux/scripts/validate-xaml-tokens.ps1 -FilePath "SimplePLC.Studio/Controls/HttpClientConfigControl.xaml"
  powershell -ExecutionPolicy Bypass -File .agents/skills/synaptix-ui-ux/scripts/validate-xaml-tokens.ps1 -FilePath "SimplePLC.Studio/Controls/MqttCommandSubscribeControl.xaml"
  powershell -ExecutionPolicy Bypass -File .agents/skills/synaptix-ui-ux/scripts/validate-xaml-tokens.ps1 -FilePath "SimplePLC.Studio/Controls/MqttTelemetryPublishControl.xaml"
  powershell -ExecutionPolicy Bypass -File .agents/skills/synaptix-ui-ux/scripts/validate-xaml-tokens.ps1 -FilePath "SimplePLC.Studio/Controls/MqttBrokerConfigControl.xaml"
  powershell -ExecutionPolicy Bypass -File .agents/skills/synaptix-ui-ux/scripts/validate-xaml-tokens.ps1 -FilePath "SimplePLC.Studio/Controls/TcpClientConfigControl.xaml"
  powershell -ExecutionPolicy Bypass -File .agents/skills/synaptix-ui-ux/scripts/validate-xaml-tokens.ps1 -FilePath "SimplePLC.Studio/Controls/ModbusPortConfigControl.xaml"
  powershell -ExecutionPolicy Bypass -File .agents/skills/synaptix-ui-ux/scripts/validate-xaml-tokens.ps1 -FilePath "SimplePLC.Studio/Controls/WifiConfigControl.xaml"
  ```
  $\rightarrow$ **PASS**: 0 vi phạm trên toàn bộ 8 tệp tin XAML.
- **Backend Architecture Validator:** `[PASS]` 0 vi phạm kiến trúc.
- **QA/TDD & Docs Validator:** `[PASS]` 0 vi phạm XML docs, 100% test pass rate.
- **Biên dịch & Test Suite:**
  - `dotnet build SimplePLC.sln`: **0 Warning(s), 0 Error(s)**.
  - `dotnet test SimplePLC.sln --no-build`: **2,055 / 2,055 tests PASS (100%)**.


---

## 29. Cơ Chế Kiểm Soát Quyền Sở Hữu Ghi Độc Quyền Dữ Liệu Ngoại Vi (Exclusive Ingestion Ownership)

### 29.1. Đặt Vấn Đề & Luận Điểm Thiết Kế Theo First Principles
- **Axiom 1 & Axiom 2 (Hardware Ground Truth & Single Source of Truth):**
  - Trong vận hành công nghiệp, các luồng tiếp nhận dữ liệu ngoại vi (Modbus Master Read FC01..04 và MQTT Inbound Subscribe pins) liên tục nạp các giá trị đo đạc/câu lệnh từ mạng vào bộ nhớ nội bộ của PLC.
  - Nếu cho phép nhiều kênh đọc ngoại vi (ví dụ: 2 kênh RS-485 khác nhau hoặc 1 kênh Modbus và 1 pin MQTT) cùng ghi vào một Tag nhớ PLC (như `VREG0`), hiện tượng **Race Condition & Data Thrashing** sẽ lập tức xảy ra: giá trị trong bộ nhớ bị giật cục, sai lệch so với hiển thị, gây tai nạn hoặc sai lệch thuật toán điều khiển.
  - Tuy nhiên, đối với **Logic Editor (FBD / Bảng Quy Tắc Rule Table)**: Việc nhiều quy tắc logic cùng tác động vào 1 Tag đích (ví dụ: Nút A bật đèn, Nút B tắt đèn - Start/Stop state machine latch) là **hoàn toàn cần thiết và bắt buộc phải được giữ nguyên**.
- **Giải Pháp Triệt Để Được Thống Nhất:**
  1. Giữ nguyên Dropdown ComboBox linh hoạt để người dùng tự do lựa chọn Tag đích.
  2. Bổ sung cơ chế hiển thị định danh kênh đang chiếm giữ độc quyền:
     - Tag nào đã bị chiếm dụng bởi kênh đọc khác sẽ bị **Disable (`IsEnabled = false`)**, làm mờ (`Opacity = 0.45`), con trỏ chuyển thành `NotAllowed`.
     - Hiển thị nhãn cảnh báo ngay cạnh tên Tag trong danh sách: `[⛔ Modbus Kênh 1]` hoặc `[⛔ MQTT: temp]`.
     - Tooltip giải thích chi tiết lý do và địa chỉ đang chiếm giữ.
     - **Tự miễn trừ (Self-Exemption):** Chính kênh đang chọn Tag đó vẫn thấy Tag là khả dụng (không bị vô hiệu hóa đối với chính nó).
  3. **Chiều Đọc vs Ghi:**
     - Modbus Read (FC01..04) & MQTT Sub: Luồng INGRESS (ghi vào PLC) $\rightarrow$ Kiểm soát độc quyền 1:1.
     - Modbus Write (FC05..16) & MQTT Pub: Luồng EGRESS (đọc từ PLC phát đi) $\rightarrow$ Cho phép 1:N (nhiều kênh cùng đọc từ 1 Tag).
     - Logic Editor Rules: Điều khiển logic trạng thái $\rightarrow$ Cho phép nhiều luật cùng tác động.
  4. **Cổng An Toàn Nạp (DeploySafetyGate):** Chặn đứng và báo lỗi chẩn đoán chi tiết trước khi nạp xuống thiết bị nếu phát hiện cấu hình xung đột.

---

### 29.2. Các Thành Phần Triển Khai Kiến Trúc

1. **`SimplePLC.Studio/Models/TagOptionItem.cs`:**
   - Model chiếu dữ liệu của từng mục Tag trong Dropdown ComboBox.
   - Chứa: `Tag`, `IsSelectable`, `DisplayText`, `ConflictBadge`, `ConflictTooltip`, `HasConflict`.

2. **`SimplePLC.Studio/Services/TagWriteOwnershipService.cs`:**
   - Singleton service theo dõi tập trung các kênh truyền thông INGRESS đang active.
   - Cung cấp:
     - `RegisterProvider(Func<IEnumerable<TagClaimEntry>> provider)`
     - `UnregisterProvider(...)`
     - `GetClaimant(TagModel? tag, object? currentCaller)` với cơ chế tự miễn trừ `Self-Exemption`.
     - `NotifyOwnershipChanged()` phát sự kiện toàn cục khi có thay đổi lựa chọn.
     - `Reset()` phục vụ cô lập trạng thái kiểm thử TDD.

3. **Cập Nhật ViewModels:**
   - `ModbusMasterChannelItemViewModel` & `ModbusPortItemViewModel`: Expose `TagOptions`, lắng nghe `OwnershipChanged`, tự động cập nhật danh sách Dropdown theo thời gian thực.
   - `MqttPinBindingViewModel` & `MqttSubscribeNodeViewModel`: Expose `TagOptions`, đăng ký chân Subscribe Pins, cập nhật tức thì khi có biến động.
   - `DeployViewModel.cs`: Thêm phương thức `ValidateIngestionOwnership(modbusReads, mqttSubs)` kiểm tra và từ chối deploy nếu có $>1$ luồng ngoại vi cùng ghi vào 1 Tag index.

4. **Giao Diện Dropdown ComboBox Trong XAML:**
   - `ModbusPortConfigControl.xaml` & `MqttCommandSubscribeControl.xaml`:
     ```xaml
     <ComboBox ItemsSource="{Binding TagOptions}"
               SelectedValue="{Binding TargetTag, Mode=TwoWay}"
               SelectedValuePath="Tag"
               Style="{StaticResource FlatCellComboBoxStyle}">
         <ComboBox.ItemContainerStyle>
             <Style TargetType="ComboBoxItem" BasedOn="{StaticResource {x:Type ComboBoxItem}}">
                 <Setter Property="IsEnabled" Value="{Binding IsSelectable}" />
                 <Style.Triggers>
                     <DataTrigger Binding="{Binding IsSelectable}" Value="False">
                         <Setter Property="Opacity" Value="0.45" />
                         <Setter Property="Foreground" Value="{DynamicResource TextMutedBrush}" />
                         <Setter Property="Cursor" Value="NotAllowed" />
                         <Setter Property="ToolTip" Value="{Binding ConflictTooltip}" />
                     </DataTrigger>
                 </Style.Triggers>
             </Style>
         </ComboBox.ItemContainerStyle>
         <ComboBox.ItemTemplate>
             <DataTemplate>
                 <DockPanel LastChildFill="True">
                     <TextBlock Visibility="{Binding HasConflict, Converter={StaticResource BoolToVis}}"
                                Text="{Binding ConflictBadge}" FontSize="9.5" FontWeight="SemiBold"
                                Foreground="{DynamicResource WarningBrush}" Margin="6,0,0,0" DockPanel.Dock="Right" VerticalAlignment="Center" />
                     <TextBlock Text="{Binding DisplayText}" VerticalAlignment="Center" />
                 </DockPanel>
             </DataTemplate>
         </ComboBox.ItemTemplate>
     </ComboBox>
     ```

---

### 29.3. Kết Quả Kiểm Thử, Khắc Phục Lỗi Cursor & Nghiệm Thu
- **Khắc phục lỗi Crash khi mở hoặc thêm kênh Modbus:**
  - *Nguyên nhân:* Thuộc tính con trỏ chuột trong XAML `ItemContainerStyle` đã gán giá trị `Cursor="NotAllowed"`. Trong WPF, `NotAllowed` là tên con trỏ của CSS/Web và không tồn tại trong `System.Windows.Input.CursorType` (WPF sử dụng `Cursor="No"`). Khi template ComboBox được khởi tạo trên UI thread, bộ chuyển đổi `CursorConverter` ném ngoại lệ `ArgumentException: Requested value 'NotAllowed' was not found`, dẫn đến `XamlParseException` làm crash ứng dụng ngay lập tức.
  - *Xử lý:* Đã thay thế thành `Cursor="No"` chuẩn WPF tại [`ModbusPortConfigControl.xaml`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Controls/ModbusPortConfigControl.xaml) và [`MqttCommandSubscribeControl.xaml`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Controls/MqttCommandSubscribeControl.xaml).
  - *Bổ sung STA UI Tests:* Thêm 2 kiểm thử giao diện WPF thực tế trên STA Thread (`ModbusPortConfigView_WhenAddMasterChannelExecuted_DoesNotCrash` và `MqttCommandSubscribeControl_WhenAddPinExecuted_DoesNotCrash`) mô phỏng 100% vòng đời khởi tạo cửa sổ, add channel/pin, và tính toán Layout (Measure/Arrange) để ngăn chặn hoàn toàn lỗi crash runtime tương tự.
- **Khắc phục lỗi Mất Tag kênh 1 khi chọn Tag ở kênh 2 (WPF TwoWay SelectedValue Transient Reset):**
  - *Hiện tượng:* Người dùng chọn Device Tag ở Kênh 1 (Modbus Master Channel 1) là `VREG0`. Sau đó mở Kênh 2 chọn `VREG1`, thì Tag ở Kênh 1 bị biến mất (trở về trống).
  - *Nguyên nhân gốc rễ (Root Cause):*
    1. Khi Kênh 2 chọn một tag, service `TagWriteOwnershipService.Instance.NotifyOwnershipChanged()` phát tín hiệu cập nhật quyền sở hữu tới toàn bộ các kênh khác.
    2. Kênh 1 lắng nghe sự kiện này và gọi `RefreshTagOptions()` để cập nhật danh sách hiển thị (nhằm làm mờ tag mà Kênh 2 vừa chọn). Việc này kích hoạt `OnPropertyChanged(nameof(TagOptions))`.
    3. Trong WPF, ComboBox gắn với `ItemsSource` khi nhận thông báo thay đổi bộ sưu tập sẽ tiến hành làm mới bộ điều khiển. Trong khoảnh khắc chuyển tiếp ngắn (transient reset), `Selector` của WPF tạm thời đẩy giá trị `null` vào thuộc tính được liên kết hai chiều (`SelectedValue="{Binding TargetTag, Mode=TwoWay}"`).
    4. Do setter của `TargetTag` trước đó chưa có cơ chế phòng vệ giá trị rác nhất thời (transient null), giá trị `null` này đã ghi đè lên `_targetTag` của Kênh 1, đồng thời hàm `OnTargetTagChanged` xóa trắng `TargetTagName = string.Empty`, khiến Kênh 1 bị mất tag đã chọn.
  - *Giải pháp triệt để (First Principles Engineering):*
    1. **Kiến trúc ObservableCollection Ổn định & Cập nhật Tại chỗ (In-Place Updates):**
       - Thay vì sử dụng getter `IEnumerable<TagOptionItem> TagOptions` trả về các instance mới và gọi `OnPropertyChanged(nameof(TagOptions))` làm sập toàn bộ cây Items của ComboBox, chuyển `TagOptions` thành `ReadOnlyObservableCollection<TagOptionItem>` backed bởi `ObservableCollection<TagOptionItem>`.
       - Trong `TagOptionItem`, chuyển các thuộc tính `IsSelectable`, `DisplayText`, `ConflictBadge`, `ConflictTooltip` thành `[ObservableProperty]`.
       - Khi sự kiện `OwnershipChanged` phát sinh, hàm `RefreshTagOptions()` duyệt qua danh sách `_tagOptions` hiện có và cập nhật trạng thái xung đột tại chỗ (in-place update). **Hoàn toàn không có thao tác Clear/Reset bộ sưu tập hay gán mới ItemsSource**, triệt tiêu 100% nguyên nhân khiến WPF Selector hủy lựa chọn.
    2. Bổ sung cơ chế **Transient Null Protection** trong Setter của `TargetTag` tại cả [`ModbusMasterChannelItemViewModel.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/ViewModels/ModbusPortConfigViewModel.cs) và [`MqttPinBindingViewModel.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/ViewModels/MqttPinBindingViewModel.cs):
       ```csharp
       set
       {
           // Bỏ qua giá trị null nhất thời do WPF Selector tự động đẩy vào trong chu kỳ làm mới ItemsSource
           if (value == null && _targetTag != null && _targetTag.Kind != TagKind.None && !string.Equals(_targetTag.Name, "NONE", StringComparison.OrdinalIgnoreCase))
           {
               return;
           }
           if (SetProperty(ref _targetTag, value))
           {
               OnTargetTagChanged(value);
           }
       }
       ```
    3. Trong Synaptix IDE, người dùng khi muốn hủy chọn tag sẽ chọn mục `NONE` (tag có `Kind == TagKind.None`), không bao giờ cố ý đẩy `null` từ giao diện. Nhờ đó, bộ lọc này ngăn chặn 100% hiện tượng reset ngoài ý muốn của WPF Selector mà vẫn hỗ trợ chọn lại tag `NONE` bình thường.
    4. Cập nhật và mở rộng cả 2 kiểm thử STA Thread UI (`ModbusPortConfigView_WhenAddMasterChannelExecuted_DoesNotCrash` và `MqttCommandSubscribeControl_WhenAddPinExecuted_DoesNotCrash`) cùng bài test chéo kênh `ModbusChannels_CrossSelection_PreservesBothChannelsTags` để xác nhận trực tiếp rằng khi Kênh 1 và Kênh 2 hoán đổi lựa chọn Tag, cả 2 kênh đều bảo toàn trọn vẹn giá trị `TargetTag` và `TargetTagName`.
- **Bộ Unit Tests Mới `ExclusiveTagOwnershipTests.cs` (9/9 PASS):**
  1. `ModbusReadChannel_WhenTagClaimed_DisablesTagInOtherChannelsDropdown`: Đạt [PASS].
  2. `ModbusReadChannel_SelfExemption_AllowsCurrentChannelToKeepTag`: Đạt [PASS].
  3. `ModbusChannels_CrossSelection_PreservesBothChannelsTags`: Đạt [PASS].
  4. `ModbusWriteChannel_DoesNotClaimTag_AllowsOtherWriters`: Đạt [PASS].
  5. `CrossProtocol_ModbusReadAndMqttSub_EnforcesExclusiveOwnership`: Đạt [PASS].
  6. `DeploySafetyGate_WhenMultipleIngestionWritersTargetSameTag_RejectsDeployment`: Đạt [PASS].
  7. `DeploySafetyGate_WhenLogicEditorRulesTargetSameTag_AllowsDeployment`: Đạt [PASS].
  8. `ModbusPortConfigView_WhenAddMasterChannelExecuted_DoesNotCrash`: Đạt [PASS] (STA UI Lifecycle Test).
  9. `MqttCommandSubscribeControl_WhenAddPinExecuted_DoesNotCrash`: Đạt [PASS] (STA UI Lifecycle Test).
- **XAML Token Validator:** `[PASS]` 0 vi phạm trên cả `ModbusPortConfigControl.xaml` và `MqttCommandSubscribeControl.xaml`.
- **Backend Architecture Validator:** `[PASS]` 0 vi phạm kiến trúc.
- **QA/TDD & Living Docs Validator:** `[PASS]` 0 vi phạm, 100% test pass rate.
- **Toàn Bộ Test Suite:** **2,064 / 2,064 tests PASS (100%)**, 0 errors, 0 warnings.

---

## 30. Tự động Đồng bộ Mẫu JSON Payload Test và Trích xuất Chân ngõ ra trong MQTT Command Subscribe (Smart Reactive Test Payload Auto-Sync)

### 30.1. Bối cảnh & Hiện trạng Vấn đề
- **Hiện trạng:** Trong giao diện cấu hình MQTT Command Subscribe (`MqttSubConfigDialogView` / `MqttCommandSubscribeControl`), khung **Payload Test** bên phải dùng để mô phỏng bóc tách dữ liệu JSON và hiển thị giá trị trích xuất của các chân ngõ ra.
- **Vấn đề phản hồi từ người dùng:** Khi người dùng thêm chân mới, sửa tên trường (`FieldName`) hoặc kiểu dữ liệu (`DataType`), khung JSON mẫu bên phải không tự động cập nhật tương ứng theo bảng chân ngõ ra. Thay vào đó, người dùng phải bấm nút `Reset Sample` thì mẫu JSON mới cập nhật. Nếu không bấm mà ấn luôn `Test Payload`, hệ thống báo lỗi giả `"Missing required data field: Field 'grid' was not found in the payload"`, tạo trải nghiệm UX gián đoạn và không trực quan.

### 30.2. Nguyên nhân Gốc rễ & Giải pháp Triệt để (First Principles Engineering)
1. **Phân tách Trạng thái Tự động (Auto-Sync) vs Tự do Tùy biến (Custom Paste):**
   - Bổ sung cờ `IsUserModified` trong [`MqttSubConfigDialogViewModel.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/ViewModels/MqttSubConfigDialogViewModel.cs).
   - Khi khởi tạo hoặc khi người dùng chưa tự ý gõ/paste JSON tùy biến khác mẫu sinh ra (`IsUserModified == false`), hệ thống tự động sinh cấu trúc JSON mẫu phù hợp theo đúng danh sách chân ngõ ra hiện có.
   - Khi người dùng dán hoặc chỉnh sửa payload thủ công (`OnSimulationInputChanged`), `IsUserModified` tự động chuyển sang `true`, khóa tính năng ghi đè tự động để bảo toàn dữ liệu tùy biến của người dùng.
   - Nút `Reset Sample` tái kích hoạt trạng thái tự động (`IsUserModified = false`), sinh lại mẫu JSON tương ứng với các chân hiện hành và chạy ngay mô phỏng.

2. **Lắng nghe Phản ứng Thời gian thực (Reactive Listeners):**
   - Đăng ký `CollectionChanged` trên danh sách chân (`Node.Pins`).
   - Lắng nghe `PropertyChanged` của từng chân (`FieldName`, `JsonPath`, `Label`, `DataType`). Khi có bất kỳ thay đổi nào (thêm/xóa chân, đổi tên trường, đổi kiểu dữ liệu), nếu `!IsUserModified`, ViewModel lập tức tự động sinh lại `SimulationInput` và kích hoạt `RunSimulation()` để hiển thị ngay giá trị trích xuất màu xanh lá.

3. **Cơ chế Xóa Cached ResolvedPath Khi Đổi Tên Trường:**
   - Trong [`MqttPinBindingViewModel.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/ViewModels/MqttPinBindingViewModel.cs), khi bộ phân tích `MqttSubscriptionParser` chạy mô phỏng lần đầu, nó lưu đường dẫn JSON thành công vào `ResolvedPath` (ví dụ `"values.grid"`).
   - Khi người dùng đổi tên trường `FieldName` thành `"temperature"`, nếu không xóa cache thì `ToDto()` vẫn gửi đường dẫn cũ `ResolvedPath = "values.grid"` dẫn tới lỗi parser không tìm thấy trường mới trong payload mẫu vừa sinh.
   - Đã xử lý triệt để: Trong setter của `FieldName` và `OnJsonPathChanged`, đặt `ResolvedPath = string.Empty;` để tự động làm mới đường dẫn tìm kiếm theo tên trường mới.

### 30.3. Kết quả Kiểm thử & Nghiệm thu
- **Bộ Unit Tests Toàn Diện (4 bài kiểm thử mới):**
  1. `MqttSubConfig_WhenPinAdded_AutomaticallyUpdatesSamplePayloadAndExtractedPins`: Kiểm tra khi thêm chân mới, payload tự động có trường mới và hiển thị giá trị trích xuất mà không báo lỗi thiếu trường.
  2. `MqttSubConfig_WhenFieldNameOrDataTypeChanged_AutomaticallyUpdatesSamplePayload`: Kiểm tra khi sửa tên trường hoặc kiểu dữ liệu, payload mẫu tự động cập nhật khớp theo kiểu dữ liệu mới.
  3. `MqttSubConfig_WhenUserPastesCustomJson_PreservesCustomPayloadOnPinChanges`: Đảm bảo khi người dùng đã dán JSON riêng, các thao tác thêm/sửa chân sẽ không ghi đè payload của người dùng.
  4. `MqttSubConfig_WhenResetSampleExecuted_RevertsToAutoSync`: Xác nhận nút Reset Sample khôi phục lại chế độ tự động đồng bộ.
- **Toàn bộ 18/18 test `MqttSubscribeNodeViewModelTests` [PASS]**.
- **XAML Token Validator:** `[PASS]` 0 vi phạm thiết kế Fluent 2 / Carbon.
- **Backend Architecture Validator:** `[PASS]` 0 vi phạm kiến trúc Clean Architecture / IEC 61508-3.
- **QA/TDD & Living Docs Validator:** `[PASS]` 0 vi phạm, 100% test pass rate.
- **Toàn Bộ Test Suite:** **2,078 / 2,078 tests PASS (100%)**, 0 errors, 0 warnings.


---

## 31. Hệ Thống Nạp Chứng Chỉ Bảo Mật MQTT TLS / mTLS & Smart Sync Checksum (SPLC-SPEC-MQTT-003)

### 31.1. Bối cảnh & Yêu cầu Kỹ thuật
- **Bối cảnh:** Phía Firmware khi kiểm thử MQTT qua cổng TLS (`8883`) và mTLS nhận thấy thiết bị báo chưa có Root CA dù trên Studio đã chọn tệp Root CA trong hộp thoại Broker và bấm **Deploy**.
- **Nguyên nhân:** Các phiên bản Studio trước đây chỉ nạp cấu hình Broker (`0x0E00`) mà chưa nạp các khối phân đoạn chứng chỉ xuống phân vùng `0x0F00`.
- **Yêu cầu & Học thuyết Thiết kế (First Principles Engineering):**
  1. **Trải nghiệm Nhất quán (Unified Single Deploy):** Tuyệt đối **không tạo nút nạp chứng chỉ riêng biệt** gây phân mảnh trải nghiệm. Toàn bộ chứng chỉ (Root CA, Client Certificate, Client Private Key) được tự động đóng gói và nạp trong một quy trình **Deploy** duy nhất.
  2. **Bảo toàn Tuổi thọ Flash & Độ trễ Thấp (Smart Sync Checksum):** Flash vi điều khiển có giới hạn chu kỳ ghi/xóa ($10,000 - 100,000$ cycles). Studio không ghi đè chứng chỉ một cách mù quáng trong mỗi lần nạp logic. Hệ thống sử dụng 3 thanh ghi CRC-16 Checksum (`0x0F44..0x0F46`) để so khớp; nếu chứng chỉ trên MCU đã hợp lệ và trùng khớp CRC, Studio bỏ qua (skip) toàn bộ quá trình nạp phân đoạn chứng chỉ đó, giữ thời gian Deploy thông thường $< 100\text{ms}$.
  3. **Hỗ trợ Đầy đủ Cả One-Way TLS & mTLS:**
     - Type `0x01`: Server Root CA
     - Type `0x02`: Client Certificate (Mutual TLS)
     - Type `0x03`: Client Private Key (Mutual TLS)
  4. **Cổng An toàn Phòng vệ (Fail-Fast Safety Gate):** Nếu người dùng cấu hình TLS nhưng thiết bị vi điều khiển không báo cờ `SPLC_CAP_MQTT_TLS_CERT (0x0080)` tại thanh ghi năng lực phần cứng `0x0A08`, Studio lập tức từ chối và cảnh báo trước khi nạp.

### 31.2. Kiến trúc & Bản đồ Thanh ghi Modbus (ICD V2.0)
| Địa chỉ | Tên Thanh Ghi | FC | Kiểu / Độ dài | Chức năng |
| :--- | :--- | :--- | :--- | :--- |
| `0x0F00..0x0F3F` | `CERT_STAGING_BUFFER` | FC16 | 64 regs (128 bytes) | Buffer nhận phân đoạn chứng chỉ (`SPLC_CertChunk_t`, payload 120 bytes) |
| `0x0F40` | `CERT_ACK_CHUNK_INDEX` | FC03 | 1 reg (UINT16) | Chỉ số chunk MCU vừa tiếp nhận thành công |
| `0x0F41` | `CERT_STATUS_ERROR` | FC03 | 1 reg (UINT16) | Trạng thái lỗi (0=OK, 1=CRC Error, 2=Buffer Overflow, 3=Flash Fail) |
| `0x0F42` | `CERT_COMMIT_COMMAND` | FC16 | 1 reg (UINT16) | Ghi magic `0xA5A5` để MCU ghi chunk buffer vào Flash |
| `0x0F43` | `CERT_ACTIVE_TYPE_MASK`| FC03 | 1 reg (UINT16) | Mask chứng chỉ đang active (Bit 0: Root CA, Bit 1: Client Cert, Bit 2: Client Key) |
| `0x0F44` | `CERT_CRC16_ROOT_CA` | FC03 | 1 reg (UINT16) | CRC-16 Modbus của Root CA hiện có trong Flash |
| `0x0F45` | `CERT_CRC16_CLIENT_CERT`| FC03 | 1 reg (UINT16) | CRC-16 Modbus của Client Cert hiện có trong Flash |
| `0x0F46` | `CERT_CRC16_CLIENT_KEY` | FC03 | 1 reg (UINT16) | CRC-16 Modbus của Client Key hiện có trong Flash |

### 31.3. Các Thành phần Đã Triển khai
1. **Giao thức & DTO (`SimplePLC.Protocol`):**
   - [`src/SimplePLC.Protocol/Dto/MqttTlsCertificatesDto.cs`](file:///g:/HoaNV/Projects/SimplePLC/src/SimplePLC.Protocol/Dto/MqttTlsCertificatesDto.cs): Mang dữ liệu nhị phân/PEM của Root CA, Client Cert, Client Key.
   - [`src/SimplePLC.Protocol/Constants/ModbusRegisterMap.cs`](file:///g:/HoaNV/Projects/SimplePLC/src/SimplePLC.Protocol/Constants/ModbusRegisterMap.cs): Khai báo đầy đủ các địa chỉ `0x0F00..0x0F46`.
   - [`src/SimplePLC.Protocol/Firmware/simpleplc_protocol_v2_0.h`](file:///g:/HoaNV/Projects/SimplePLC/src/SimplePLC.Protocol/Firmware/simpleplc_protocol_v2_0.h): Cập nhật hằng số định nghĩa thanh ghi cho Firmware MCU.
2. **Tầng Ứng dụng & Cổng Trừu tượng (`SimplePLC.Application`):**
   - [`src/SimplePLC.Application/Abstractions/DeviceInterfaces.cs`](file:///g:/HoaNV/Projects/SimplePLC/src/SimplePLC.Application/Abstractions/DeviceInterfaces.cs) & [`DeployRulesUseCase.cs`](file:///g:/HoaNV/Projects/SimplePLC/src/SimplePLC.Application/UseCases/DeployRulesUseCase.cs): Tiếp nhận DTO chứng chỉ và điều phối xuống Writer.
3. **Tầng Hạ tầng & Mô phỏng (`SimplePLC.Infrastructure`):**
   - [`src/SimplePLC.Infrastructure/Devices/RuleTableWriter.cs`](file:///g:/HoaNV/Projects/SimplePLC/src/SimplePLC.Infrastructure/Devices/RuleTableWriter.cs):
     - Bước 3.4 trong quy trình Deploy: `DeployTlsCertificatesAsync`.
     - Smart Sync CRC: Đọc `0x0F43..0x0F46`, so sánh CRC file với CRC trên MCU. Nếu trùng khớp -> Skip chunk upload.
     - Vòng lặp nạp phân đoạn với tối đa 3 lần thử lại (Retries).
     - Atomic Commit qua magic `0xA5A5` tại `0x0F42` và xác minh phản hồi trạng thái tại `0x0F41..0x0F43`.
   - [`src/SimplePLC.Infrastructure/Simulator/McuReferenceSimulator.cs`](file:///g:/HoaNV/Projects/SimplePLC/src/SimplePLC.Infrastructure/Simulator/McuReferenceSimulator.cs) & `SimulatorRegisterMemory.cs`:
     - Bổ sung cờ năng lực mặc định `SPLC_CAP_MQTT_TLS_CERT (0x0080)`.
     - Hỗ trợ ghi phân đoạn 0x0F00, phản hồi ACK 0x0F40, commit 0x0F42, tính toán lưu trữ CRC-16 vào 0x0F44..0x0F46 và cập nhật ActiveMask 0x0F43.
4. **Tầng Studio ViewModel (`SimplePLC.Studio`):**
   - [`SimplePLC.Studio/ViewModels/DeployViewModel.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/ViewModels/DeployViewModel.cs):
     - Tự động trích xuất chuỗi PEM hoặc đọc file chứng chỉ từ `ProjectMetadata` (Root CA, Client Cert, Client Key) và truyền vào `DeployRulesUseCase`.
     - Bảo vệ an toàn chống xung đột quyền sở hữu Tag Ingestion (`ValidateIngestionOwnership`).

### 31.4. Kết quả Kiểm thử & Nghiệm thu
- **Bộ Kiểm thử Hạ tầng (`MqttTlsCertificateDeployTests.cs`):**
  1. `Deploy_OneWayTls_UploadsRootCaAndCommits`: Phân đoạn, ACK, commit thành công Root CA, ActiveMask = 0x01, CRC ghi vào 0x0F44.
  2. `Deploy_SmartSync_SkipsUploadWhenCrcMatches`: Lần nạp thứ hai với cùng chứng chỉ tự động bỏ qua toàn bộ chunk upload.
  3. `Deploy_MutualTls_UploadsAllThreeCertificatesInOrder`: Nạp tuần tự và commit thành công cả 3 chứng chỉ mTLS, ActiveMask = 0x07.
  4. `Deploy_DeltaCertChange_OnlyUploadsChangedCertificate`: Thay đổi duy nhất Client Key thì Root CA và Client Cert được skip, chỉ nạp và commit lại Client Key.
  5. `Deploy_MissingHardwareCapability_AbortsFailFast`: MCU thiếu cờ 0x0080 lập tức bị từ chối với mã lỗi `UNSUPPORTED`.
- **Bộ Kiểm thử Tích hợp Studio (`DeployViewModelIntegrationTests.cs`):**
  1. `StartDeployAsync_WithMqttTlsRootCa_ProvisionsCertificateToMcu`: DeployViewModel đóng gói Root CA từ metadata và nạp thành công xuống MCU.
  2. `StartDeployAsync_WithMqttMtls_ProvisionsAllThreeCertificates`: DeployViewModel đóng gói cả 3 chứng chỉ mTLS và hoàn tất quy trình 5 bước.
- **Tuân thủ Tiêu chuẩn Tuyệt đối:**
  - **XAML Token Validator:** `[PASS]` 0 vi phạm trên toàn bộ các file XAML.
  - **Backend Architecture Validator:** `[PASS]` 0 vi phạm kiến trúc Clean Architecture / SOLID.
  - **QA/TDD & Living Docs Validator:** `[PASS]` 0 vi phạm, 100% test pass rate.
  - **Tài liệu & VitePress Build:** `[PASS]` 0 vi phạm, build sạch hoàn toàn.
  - **Toàn Bộ Test Suite:** **2,085 / 2,085 tests PASS (100%)**, 0 errors, 0 warnings.

---

## 32. Chuẩn Hóa Lựa Chọn Cổng RS-485 Modbus Master & Áp Dụng Phương Án C (In-Place Port Selector)

### 32.1. Bối cảnh & Yêu cầu Kỹ thuật
- Khi cấu hình các khối Modbus Master (`MB_READ` và `MB_WRITE`), nếu một cổng RS-485 trên thiết bị bị tắt (`Enabled == false`) hoặc cấu hình vai trò Modbus Slave, cổng đó không được phép xuất hiện trong danh sách dropdown (`AvailablePorts`).
- Nghiêm cấm hiển thị các mục gắn cờ tạm `[DISABLED]` hoặc `[ĐÃ TẮT]` trong danh sách chọn cổng, gây rối mắt cho kỹ sư vận hành.
- Khi một cổng đang được khối logic chọn nhưng sau đó bị tắt ở Tab System:
  - Khối logic tự động chuyển cổng về cổng Master khả dụng đầu tiên (`selectablePorts[0].PortIndex`).
- Khi trong dự án hoàn toàn không có cổng RS-485 Master nào được kích hoạt:
  - Áp dụng **Phương án C**: Tích hợp thông báo trực tiếp vào vùng điều khiển lựa chọn cổng (Port Selector Control Area) thay vì chèn thanh banner màu hổ phách làm nhảy vị trí các hàng thông số bên dưới.
  - Vùng chọn cổng hiển thị Panel lõm trung tính (Neutral Slate Surface) với icon vector `IconSysInfo` và dòng chữ `[Chưa kích hoạt cổng RS-485 Master]` / `[No RS-485 Master Ports Enabled]`.
  - Bên cạnh là nút bấm hành động `[ ⚙ Cấu hình Cổng RS-485... ]` điều hướng trực tiếp sang cửa sổ cấu hình cổng thiết bị.
  - Toàn bộ thiết kế tuân thủ nghiêm ngặt học thuyết UI/UX `synaptix-ui-ux`:
    - Nguyên tắc xám ANSI/ISA-101 (The Gray Principle 90/10): Sử dụng `{DynamicResource IndustrialSurfaceInsetBrush}`, `{DynamicResource IndustrialBorderDefaultBrush}`, và `{DynamicResource TextMutedBrush}` cho trạng thái rỗng tĩnh.
    - Không dùng icon emoji ký tự (`⚙️`); sử dụng 100% vector SVG `<StreamGeometry>` chuẩn hóa từ [`Icons.xaml`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Styles/Icons.xaml).
    - Chuẩn hình học Microsoft Fluent 2 Desktop (`CornerRadius="4"`).

### 32.2. Các File Đã Chỉnh Sửa
1. **Localization (`LocalizationService.cs`):**
   - Bổ sung key `ModbusNoMasterPortsEnabled`:
     - Tiếng Việt: `"Chưa kích hoạt cổng RS-485 Master"`
     - Tiếng Anh: `"No RS-485 Master Ports Enabled"`
2. **ViewModel (`ModbusWriteConfigDialogViewModel.cs` & `ModbusReadConfigDialogViewModel.cs`):**
   - Lọc triệt để `_modbusPorts`: Chỉ đưa vào `AvailablePorts` các cổng `p.Enabled && p.Role != "Slave"`.
   - Nếu cổng hiện tại bị tắt/chuyển Slave, tự động fallback sang cổng Master đầu tiên.
   - Thêm thuộc tính `HasAvailablePorts => AvailablePorts.Count > 0;` và `HasNoAvailablePorts => AvailablePorts.Count == 0;`.
   - Thêm thuộc tính `NoPortsAvailableMessage`.
3. **View XAML (`ModbusWriteConfigDialogView.xaml` & `ModbusReadConfigDialogView.xaml`):**
   - Xóa bỏ banner cảnh báo Row 1 làm xô lệch bố cục giao diện.
   - Thay thế nút emoji `⚙️` bằng vector SVG path `IconSysSettings`.
   - Triển khai Phương án C: Khi `HasAvailablePorts` hiển thị ComboBox + nút Settings (28x28); khi `HasNoAvailablePorts` hiển thị Inset Border + nút hành động có nhãn "Cấu hình Cổng RS-485...".
4. **Kiểm thử Đơn vị (`ModbusPortFilteringTests.cs`, `ModbusWriteConfigDialogViewModelTests.cs`, `DialogStylingAndWindowResizeTests.cs`):**
   - Cập nhật và bổ sung test cases kiểm chứng:
     - Các cổng bị disabled hoàn toàn không xuất hiện trong dropdown.
     - Tự động chuyển cổng khi cổng cũ bị tắt.
     - Khi tắt hết cổng, `AvailablePorts` rỗng và kích hoạt cờ `HasNoAvailablePorts == true`.


## 33. Khắc Phục Xung Đột Vòng Đời Cấu Hình RS-485 & Đồng Bộ Hai Chiều Popup - Tab Hệ Thống

### 33.1. Bối cảnh & Hiện tượng
1. **Lỗi Crash WPF:** Khi tắt toàn bộ cổng RS-485, khối `MB_WRITE` hiển thị nhãn rỗng kèm nút cấu hình bánh răng. Mở popup `ModbusPortConfigDialogView`, bật 1 cổng và nhấn **Áp dụng (Apply)** thì phần mềm bị crash với lỗi:
   `InvalidOperationException: DialogResult can be set only after Window is created and shown as dialog.`
2. **Lỗi Không Đồng Bộ (Bật cổng ở popup nhưng quay lại Tab Hệ Thống vẫn tắt):**
   Sau khi bật cổng ở popup và lưu thành công, quay trở lại Tab 0 (Hệ Thống / System Tab) thì cổng vừa bật vẫn hiển thị ở trạng thái `Disable`.

### 33.2. Cơ chế Lỗi Gốc (Root Cause)
1. **Double-Apply Triggering InvalidOperationException:**
   - Khi nhấn **Áp dụng** trong popup, button XAML đã kích hoạt lệnh `portVm.ApplyCommand`, thực hiện ghi dữ liệu vào `ProjectMetadata` và gọi `RequestClose`.
   - `ModbusPortConfigDialogView.xaml.cs` bắt `RequestClose` và gán `DialogResult = true`, khiến cửa sổ popup đóng lại.
   - Tại `LogicEditorViewModel.cs`, sau khi `portDlg.ShowDialog() == true`, code lại gọi thừa `portVm.Apply()` lần 2. Lần gọi này kích hoạt tiếp `RequestClose` và cố gắng gán `DialogResult = true` lên cửa sổ đã đóng, gây crash lập tức.
2. **Stale Auto-Commit On Tab Switch (Xóa Đè Cấu Hình):**
   - Trong `MainViewModel.cs`, sự kiện `OnSelectedTabIndexChanged` trước đây gọi vô điều kiện `DtuGatewayStudioVM?.TriggerAutoCommitToProject()` khi người dùng chuyển sang bất kỳ tab nào.
   - Khi người dùng đang ở Tab Canvas (Tab 1) hoặc mở popup cấu hình, `CurrentProjectMetadata` đã cập nhật cổng là `Enabled = true`.
   - Khi người dùng nhấp chuột chuyển sang Tab 0 (Tab Hệ Thống), `OnSelectedTabIndexChanged(0)` chạy và gọi `TriggerAutoCommitToProject()` **trước khi** `DtuGatewayStudioVM` kịp tải dữ liệu mới.
   - Thao tác này đã lấy dữ liệu cũ trong bộ nhớ RAM của `DtuGatewayStudioVM` (vẫn đang lưu cổng là `Enabled = false`) ghi đè ngược trở lại `CurrentProjectMetadata`, xóa sạch thay đổi của người dùng.

### 33.3. Các Giải Pháp Đã Triển Khai
1. **Chuẩn hóa vòng đời Modal Dialog (`LogicEditorViewModel.cs` & `ModbusPortConfigDialogView.xaml.cs`):**
   - Loại bỏ lệnh gọi trùng lặp `portVm.Apply()` tại `OpenModbusReadConfig` và `OpenModbusWriteConfig`.
   - Bọc sự kiện gán `DialogResult` trong khối `try...catch (InvalidOperationException)` an toàn.
   - Bổ sung `NotifyProjectMetadataChanged?.Invoke()` khi đóng popup để kích hoạt reload ngay lập tức.
2. **Đồng bộ hai chiều chuẩn xác theo Single Source of Truth (`MainViewModel.cs`):**
   - Tại `OnSelectedTabIndexChanged`:
     - Nếu chuyển **vào** Tab Hệ Thống (`value == AppTabIndices.Device`): Gọi `DtuGatewayStudioVM?.ReloadFromMetadata();` để đọc dữ liệu mới nhất từ `CurrentProjectMetadata`.
     - Nếu chuyển **ra** khỏi Tab Hệ Thống (`value != AppTabIndices.Device`): Mới gọi `TriggerAutoCommitToProject()` để lưu các chỉnh sửa từ Tab Hệ Thống vào `CurrentProjectMetadata`.
3. **Kiểm thử tự động hóa TDD (`HardwareAwareRs485StudioTests.cs`):**
   - Bổ sung `DtuGatewayStudioViewModel_ReloadFromMetadata_RefreshesPortStateFromProjectMetadata` kiểm chứng quy trình bật cổng ngoài popup và kiểm tra Tab Hệ Thống cập nhật chuẩn 100%.
   - Toàn bộ 2,166/2,166 unit test pass 100%.


## 34. Chuẩn Hóa Cơ Chế Nạp Sang Chuẩn IDE Công Nghiệp (Universal Industrial IDE Download Standard)

### 34.1. Bối cảnh & Hiện tượng
Người dùng phản ánh: Trước đây, nếu dự án không có ít nhất 1 Rule logic thì không thể nạp xuống thiết bị (kể cả khi đã cấu hình đầy đủ MQTT Broker, TLS, LWT, DTU Gateway, RS-485...). IDE hiển thị thông báo "Chưa có chương trình logic hợp lệ để nạp" và vô hiệu hóa nút nạp.

### 34.2. Cơ chế Lỗi Gốc (Root Cause)
1. **Tư duy cục bộ dạng Form Submit:**
   - Trong `DeployViewModel.CanDeploy`, code cũ ràng buộc: Bắt buộc `_ruleTableVM.Rules.Count > 0` hoặc `CurrentProgram.RuleCount > 0` hoặc có kênh Modbus Master ở Tab 0 (`HasDeviceModbusConfigToDeploy()`).
   - Nếu dự án chỉ cấu hình MQTT Broker / LWT / TLS, DTU Gateway, hoặc Modbus Slave mode mà không có rule nào, `CanDeploy` trả về `false`.
2. **Đồng nhất sai giữa "0 Rule" và "Lỗi cú pháp":**
   - Khi canvas trống, `LogicEditorViewModel.CompileState` chuyển về `CompileState.NotCompiled`.
   - `UpdatePreflightAndStatus()` kiểm tra `CompileState != CompileState.Valid` và lập tức chặn nạp, gán nhãn nút nạp thành "Mở Đồ Thị Logic Để Sửa".
3. **Trong `StartDeployAsync()`:**
   - Khối lệnh `if (ruleTable.Count == 0 && totalFbs == 0 ...)` cũ bỏ sót cấu hình MQTT Broker và chứng chỉ TLS trong Metadata, dẫn tới việc chặn nạp kể cả khi đã cấu hình ngoại vi.

### 34.3. Các Giải Pháp Đã Triển Khai
1. **Chuẩn hóa triết lý `CanDeploy` theo chuẩn IDE Công nghiệp (TIA Portal / CODESYS / Studio 5000):**
   - Luôn cho phép nạp khi thỏa mãn 2 điều kiện tiên quyết:
     $$\text{Thiết bị đã kết nối (Hardware Link Active)} \quad \land \quad \text{Không có lỗi biên dịch cú pháp đỏ (CompileState != Invalid \& Stale)}$$
   - Dự án có 0 Rule nhưng có cấu hình (MQTT Broker, TLS, LWT, DTU, RS-485...): Nạp cấu hình đầy đủ và nạp 0 Rule (xóa sạch logic cũ). Preflight Summary hiển thị: *"Sẵn sàng nạp: Cấu hình {Cấu hình} xuống thiết bị (0 Rule logic)."*
   - Dự án hoàn toàn trắng (0 Rule, 0 cấu hình ngoại vi): Cho phép nạp với vai trò **Wipe / Reset Logic** về mặc định an toàn. Preflight Summary hiển thị: *"Sẵn sàng nạp: Đặt lại cấu hình và xóa sạch logic thiết bị (0 Rule logic)."*
2. **Bảo toàn Cổng An Toàn (Safety Gate) thực thụ:**
   - Nếu thiết bị mất kết nối: Chặn nạp (`CanDeploy = false`).
   - Nếu bản vẽ đang có lỗi cú pháp nghiêm trọng (`CompileState == Invalid`): Chặn nạp, hướng dẫn kỹ sư sửa lỗi trên canvas.
   - Nếu bản vẽ đang có thay đổi chưa biên dịch (`CompileState == Stale`): Chặn nạp trên Deploy tab để bảo toàn Visual Truth (hoặc auto-compile khi bấm Quick Deploy từ Toolbar).
   - Nếu phát hiện xung đột ghi Tag (`Exclusive Ingestion Ownership Conflict`): Chặn nạp và báo lỗi chi tiết.
3. **Cập nhật & Bổ sung Test Cases TDD (`DeploySafetyGateTests.cs` & `McuStudioRoundtripAutomationTests.cs`):**
   - Chuyển đổi test case cũ thành `Deploy_WhenZeroRules_AndNoModbusChannels_CanDeployIsTrue_AndPreflightIsCleanWipe`.
   - Bổ sung `Deploy_WhenDisconnected_CanDeployIsFalse`.
   - Bổ sung `Deploy_WhenZeroRules_WithMqttBrokerConfig_CanDeployIsTrue_AndDeploysBrokerToMcu`.
   - Bổ sung `Deploy_WhenZeroRules_WithMqttTls_ProvisionsCertificatesSuccessfully`.
   - Bổ sung `Deploy_WhenZeroRules_BlankProject_WipesMcuRulesSuccessfully`.
   - Cập nhật `Test_03_Studio_DeploySafetyGate_ZeroRuleSucceeds_AndBlocksInvalidOrStale` trong bộ test roundtrip.
4. **Nghiệm thu toàn diện:**
   - 2,170 / 2,170 unit tests toàn giải pháp vượt qua 100% (`[PASS]`).
   - `validate-backend-arch.ps1`: Đạt `[PASS]` với 0 vi phạm kiến trúc.
   - `validate-tdd-docs.ps1`: Đạt `[PASS]` với 0 vi phạm tài liệu XML và 100% test pass.


## 35. Chuẩn Hóa Trạng Thái Biên Dịch Đa Phân Hệ (Multi-Subsystem Compact Tokens) & Tự Động Đồng Bộ Bảng Rule Table

### 35.1. Bối cảnh & Hiện tượng (Visual Discrepancy & Tab Isolation)
- **Hiện tượng 1:** Khi kỹ sư kéo các khối ngoại vi (MQTT PUB, MQTT SUB, MB_READ, MB_WRITE, Function Blocks) lên Canvas mà không có khối Action logic (chưa nối Coil ra ngõ ra):
  - Khi đang vẽ (Auto-sync), thanh dưới hiển thị `4 function/MQTT blocks ready`.
  - Nhưng khi bấm `Save & Compile (Ctrl+S)`, cả thanh trạng thái dưới và thẻ Inspector bên phải lại bị ghi đè thành `"0 valid rules"` (`0 Rule hợp lệ`), khiến kỹ sư hiểu nhầm rằng hệ thống bị lỗi hoặc không nhận các khối MQTT / Modbus.
- **Hiện tượng 2:** Khi chuyển sang Tab Bảng Rule (`Rule Table`), mục `MQTT Services (0)` hiển thị trống trơn ("No MQTT services configured") mặc dù trên Canvas đang có 4-5 khối MQTT hoạt động bình thường.

### 35.2. Phân Tích Cơ Chế Gốc (Root Cause Analysis)
1. **Sự khác biệt bản chất giữa Logic Rule và Ngoại vi (Axiom 1 & Axiom 2):**
   - Trong PLC, Logic Rule là chuỗi điều kiện Boolean kết thúc bằng cuộn hút Action (`result.Rules.Count`).
   - Các khối MQTT PUB/SUB và Modbus Master là các tiến trình nền công nghiệp (Peripheral Subsystems).
   - Trước đây tại dòng 5322 của `LogicEditorViewModel.cs`, nhánh `!isAutoSync` gán cứng:
     `SaveStatusMessage = string.Format(LocalizationService.Tr("SaveStatusValid"), result.Rules.Count);`
     Do đó khi `result.Rules.Count == 0`, chuỗi trở thành `"0 valid rules"`.
2. **Ngắt mạch đồng bộ Tab (Tab Switching Short-Circuit):**
   - Trong `MainViewModel.OnSelectedTabIndexChanged`, nhánh `if (_previousTabIndex == AppTabIndices.Device)` dùng chuỗi `else if` nối tiếp nuốt mất nhánh chuyển sang Tab 3 (`RuleTable`).
   - Đồng thời, `isAutoSync` không gọi `_ruleTable.UpdateSubsystemsFromProgram(result.Program)`, khiến Bảng Rule Table không được nạp dữ liệu từ Canvas nếu kỹ sư chưa chủ động nhấn Save & Compile.

### 35.3. Các Giải Pháp Kỹ Thuật Đã Triển Khai
1. **Hệ Thống Compact Token Biên Dịch Đa Phân Hệ (`UpdateCompileStatusMessage`):**
   - Áp dụng thống nhất cho cả 2 luồng: Tự động gom mạch khi vẽ (`isAutoSync = true`) và Lưu & Biên dịch chủ động (`isAutoSync = false`).
   - Tự động phát hiện cơ cấu phân hệ và hiển thị chuỗi ngắn gọn ($\le 180\text{px} - 200\text{px}$):
     - Chỉ có MQTT: `{n} MQTT ready` (`{n} MQTT sẵn sàng`)
     - Chỉ có Modbus: `{n} Modbus ready` (`{n} Modbus sẵn sàng`)
     - Chỉ có Logic Rule: `{n} valid rules` (`{n} Rule hợp lệ`)
     - Phối hợp Rule + MQTT: `{r} rules, {m} MQTT`
     - Phối hợp đa phân hệ: `{r}R, {m}M, {b}MB ready`
   - Bổ sung `SaveStatusTooltip` (Hover giải trình chi tiết từng phân hệ) và `SaveStatusDetail` (thẻ Inspector bên phải).
   - Kích hoạt Save Toast thông báo lưu thành công khi `totalDeployableExplicit > 0` (`Saved 5 MQTT`, `Saved 2 Modbus`, v.v.).
2. **Tự Động Đồng Bộ 100% Sang Bảng Rule Table Khi Chuyển Tab:**
   - Trong `MainViewModel.cs`: Tách việc kích hoạt `value == AppTabIndices.RuleTable` thành nhánh độc lập.
   - Luôn tự động gọi `RuleTableVM.UpdateSubsystemsFromProgram(LogicEditorVM.CurrentProgram)` và `RefreshFromCompiledGraph()`.
   - Kết quả: Khi chuyển sang Tab Rule Table, các thẻ phụ `MQTT Services (n)`, `Modbus Master (n)` lập tức cập nhật đầy đủ bảng danh mục dịch vụ mà không cần bất kỳ thao tác thủ công nào.
3. **Bộ Kiểm Thử Độc Lập TDD (`CompileStatusMessageTests.cs`):**
   - Kiểm thử 6 kịch bản đa dạng từ đơn phân hệ, phối hợp, tràn kích thước token đến tự động đồng bộ khi chuyển Tab.
   - Toàn bộ 2,182 / 2,182 unit tests toàn solution PASS 100%.

---

## 36. Kiến Trúc Tài Liệu Kỹ Thuật: Đồng Bộ Wire Contract 32-Byte `AuxParam` & Đặc Tả Hạ Cấp Khối `SCALE` và Tích Hợp Toán Học

### 36.1. Bối cảnh & Yêu cầu Kỹ thuật
Theo học thuyết **SynaptiX Documentation & Living Docs Architecture (3 Pillars)**:
1. **Pillar 2 (Firmware & MCU Communication):** File đặc tả hợp chuẩn MCU `docs/firmware/MCU_CONFORMANCE_SPECIFICATION_V2_0.md` trước đó vẫn còn giữ khai báo struct cũ `reserved[5]` tại các thanh ghi 13 Low..15, chưa phản ánh trường `aux_param` (Reg 14..15) đã được chuẩn hóa trong `simpleplc_protocol_v2_0.h`.
2. **Pillar 3 (Web Wiki & Portal):** Trang tài liệu khối `SCALE` song ngữ (`docs-portal/vi/blocks/scale.md` và `docs-portal/en/blocks/scale.md`) chưa có mục giải trình hạ cấp mã máy 32-byte, vai trò của `aux_param`, việc giải phóng `threshold_lo/hi` triệt tiêu xung đột điều kiện trigger, và kiến trúc ghép chuỗi (Rule Chaining Pipeline) khi kết hợp với các khối Toán học (Math Blocks) trong cùng chu kỳ quét 10ms.

### 36.2. Các Nội Dung Cập Nhật
1. **Cập nhật Đặc tả Phần cứng MCU ([`docs/firmware/MCU_CONFORMANCE_SPECIFICATION_V2_0.md`](file:///g:/HoaNV/Projects/SimplePLC/docs/firmware/MCU_CONFORMANCE_SPECIFICATION_V2_0.md)):**
   - Cập nhật struct `SPLC_RuleRecord_t` byte-exact với `aux_param` / `aux_param_f` (4 bytes tại Reg 14..15) và `reserved_byte` (Reg 13 Low).
   - Thêm `_Static_assert(sizeof(SPLC_RuleRecord_t) == 32)` bảo đảm hợp đồng nhị phân 32-byte bất biến.
   - Làm rõ quy tắc `ACT_SCALE_TAG (7)` sử dụng `action_param_f` (Gain $k$) và `aux_param_f` (Offset $b$), độc lập hoàn toàn với `threshold_lo/hi`.
2. **Bổ sung Mục 7 Song ngữ cho Khối `SCALE` ([`scale.md (VI)`](file:///g:/HoaNV/Projects/SimplePLC/docs-portal/vi/blocks/scale.md) & [`scale.md (EN)`](file:///g:/HoaNV/Projects/SimplePLC/docs-portal/en/blocks/scale.md)):**
   - Bảng ánh xạ chi tiết 16 thanh ghi Modbus vào struct 32-byte khi biên dịch.
   - Phân tích nguyên nhân và giải pháp triệt tiêu xung đột ngưỡng kích hoạt (Threshold Invariant).
   - Sơ đồ Mermaid minh họa cơ chế ghép chuỗi qua thanh ghi biến ảo `VREG0..15` trong cùng chu kỳ quét tiền định 10ms (100Hz).

### 36.3. Kết Quả Kiểm Thử Nghiệm Thu
- Chạy lệnh kiểm tra tự động:
  `powershell -ExecutionPolicy Bypass -File .agents/skills/synaptix-docs-techwriter/scripts/validate-docs.ps1`
- Kết quả: **`[PASS]` 0 vi phạm**, 89 trang markdown hợp lệ, 14 tài liệu ICD đồng bộ và VitePress build thành công 100%.

---

## 37. Đặc Tả Toàn Diện Khối Hàm Toán Học `MATH` cho Cả Phần Mềm (App/IDE) & Thiết Bị (MCU Firmware)

### 37.1. Bối cảnh & Yêu cầu Kỹ thuật
- Thiết lập tài liệu thiết kế và khế ước giao tiếp hoàn chỉnh cho khối xử lý số học **`MATH`** (Arithmetic & Algebra Operations) theo chuẩn IEC 61131-3, đảm bảo:
  1. **Đội ngũ Firmware (MCU / Device):** Có đặc tả chi tiết về opcode thực thi (`SPLC_ACT_MATH_OP = 12`, `SUB = 8`, `MUL = 9`, `DIV = 10`, `MOD = 11`), cách trích xuất 2 toán hạng từ struct 32-byte (`trigger_tag`, `guard_tag`, `aux_param`), cơ chế xử lý chia cho 0 không làm crash CPU, khử NaN/Infinity và kẹp tràn số.
  2. **Đội ngũ Phần mềm (Studio / App):** Có tài liệu về FBD visual node trên Canvas, các tham số Inspector, chiến lược phân rã biểu thức phức tạp (Macro Lowering AST $\rightarrow$ chuỗi Micro-Rule 32-byte) và liên kết thanh ghi tạm `VREG0..15`.

### 37.2. Các Tệp Tài Liệu Đã Ban Hành & Cập Nhật
1. **Phân Định Phạm Vi Tài Liệu (Strict Separation):**
   - Giữ nguyên `docs-portal/` sạch sẽ, chỉ dành riêng cho tài liệu Web Wiki hướng dẫn người dùng cuối (User Guides) của các tính năng đã phát hành trên IDE.
   - Toàn bộ đặc tả kỹ thuật, khế ước nhị phân, thuật toán vi điều khiển và kiến trúc phần mềm được tập trung 100% trong thư mục tài liệu nội bộ `docs/`.
2. **Ban Hành Đặc tả Phần cứng & Firmware Vi điều khiển (Pillar 2 — ICD Nội Bộ):**
   - [`docs/firmware/SPLC_SPEC_MATH_007_ICD.md`](file:///g:/HoaNV/Projects/SimplePLC/docs/firmware/SPLC_SPEC_MATH_007_ICD.md): Tài liệu đặc tả giao diện phần cứng (ICD) chuẩn hóa mã `SPLC_ACT_MATH_OP = 12`, bản đồ thanh ghi Modbus 32-byte, trích xuất toán hạng qua `guard_tag` và `aux_param`, thuật toán phòng vệ chống chia cho 0, khử NaN/Infinity và mã nguồn C tham chiếu cho firmware vi điều khiển.
   - [`docs/firmware/MCU_CONFORMANCE_SPECIFICATION_V2_0.md`](file:///g:/HoaNV/Projects/SimplePLC/docs/firmware/MCU_CONFORMANCE_SPECIFICATION_V2_0.md): Cập nhật bảng ActionType tương thích.
3. **Ban Hành Đặc tả Kiến trúc Phần mềm & Trình biên dịch (Pillar 1 — Architecture Nội Bộ):**
   - [`docs/architecture/SPLC_SPEC_MATH_BLOCK_ARCHITECTURE.md`](file:///g:/HoaNV/Projects/SimplePLC/docs/architecture/SPLC_SPEC_MATH_BLOCK_ARCHITECTURE.md): Đặc tả phân lớp Clean Architecture cho khối Math, mô hình miền `MathNodeModel`, chiến lược hạ cấp `MathMacroLowering`, thiết kế UI/UX theo Fluent 2 và kế hoạch kiểm thử TDD Triad (Happy path, Overflow, Fault injection).

### 37.3. Kết Quả Kiểm Thử Nghiệm Thu
- Lệnh: `powershell -ExecutionPolicy Bypass -File .agents/skills/synaptix-docs-techwriter/scripts/validate-docs.ps1`
- Kết quả: **`[PASS]` 0 vi phạm**, 89/89 trang markdown hợp lệ, 15/15 ICDs phần cứng đồng bộ, VitePress build sạch sẽ 100%.



---

## 38. Triển Khai Toàn Diện Khối Hàm Số Học `MATH` (IEC 61131-3 ALU) Trên Tất Cả Các Tầng Clean Architecture

### 38.1. Mục Tiêu & Phạm Vi Công Việc
- Triển khai hoàn chỉnh khối hàm tính toán số học **`MATH`** theo chuẩn công nghiệp IEC 61131-3 trên toàn bộ kiến trúc Clean Architecture của SimplePLC (.NET 8 WPF SynaptiX IDE, Application Compiler, Domain Invariant, Infrastructure Simulator, Protocol Wire Contract và UI/UX Presentation).
- Tuân thủ nghiêm ngặt học thuyết First Principles, Zero Hardcoded Hex tokens, ANSI/ISA-101 Gray Principle, Microsoft Fluent 2 Desktop Geometry, IEC 61508-3 SIL2 Triad Testing và Living Documentation.

### 38.2. Các Thành Phần Đã Hiện Thực
1. **Tầng 1 — Domain Layer ([`src/SimplePLC.Domain/`](file:///g:/HoaNV/Projects/SimplePLC/src/SimplePLC.Domain/)):**
   - [`RuleEnums.cs`](file:///g:/HoaNV/Projects/SimplePLC/src/SimplePLC.Domain/Enums/RuleEnums.cs): Bổ sung `ActionKind.MathOp = 12`, `MathOperator` (8 toán tử: `Add`, `Sub`, `Mul`, `Div`, `Mod`, `Min`, `Max`, `Clamp`), và `MathTypeMode` (`Auto`, `Int32`, `Float32`).
   - [`MathPinModel.cs`](file:///g:/HoaNV/Projects/SimplePLC/src/SimplePLC.Domain/Models/MathPinModel.cs): Model chân toán hạng hỗ trợ cả dynamic tag (`TagIndex`) và hằng số số học (`ConstantValue`).
   - [`MathNodeModel.cs`](file:///g:/HoaNV/Projects/SimplePLC/src/SimplePLC.Domain/Models/MathNodeModel.cs): Mô hình miền bất biến của khối Math, định nghĩa giới hạn chân (2..8 chân, `Sub`/`Div`/`Mod` nghiêm ngặt 2 chân, `Clamp` nghiêm ngặt 3 chân).
   - [`RuleValidator.cs`](file:///g:/HoaNV/Projects/SimplePLC/src/SimplePLC.Domain/Validation/RuleValidator.cs): Miễn trừ điều kiện kiểu Boolean cho `Guard` đối với `ActionKind.MathOp` để cho phép `Guard.Tag` giữ toán hạng số học thứ hai (INT32/REAL).

2. **Tầng 2 — Protocol Layer ([`src/SimplePLC.Protocol/`](file:///g:/HoaNV/Projects/SimplePLC/src/SimplePLC.Protocol/)):**
   - [`RuleEnums.cs`](file:///g:/HoaNV/Projects/SimplePLC/src/SimplePLC.Protocol/Enums/RuleEnums.cs): Định nghĩa `SPLC_ACT_MATH_OP = 12` và enum `SPLC_MathOp` (`ADD=0` .. `CLAMP=7`).
   - [`simpleplc_protocol_v2_0.h`](file:///g:/HoaNV/Projects/SimplePLC/src/SimplePLC.Protocol/Firmware/simpleplc_protocol_v2_0.h): Cập nhật header C tương thích vi điều khiển.

3. **Tầng 3 — Application Layer ([`src/SimplePLC.Application/`](file:///g:/HoaNV/Projects/SimplePLC/src/SimplePLC.Application/)):**
   - [`LogicGraphModel.cs`](file:///g:/HoaNV/Projects/SimplePLC/src/SimplePLC.Application/Logic/Graph/LogicGraphModel.cs): Bổ sung `LogicNodeKind.Math = 16`, `MathPinData`, `MathNodeData`.
   - [`MathMacroLowering.cs`](file:///g:/HoaNV/Projects/SimplePLC/src/SimplePLC.Application/Logic/Compilation/MacroLowering/MathMacroLowering.cs): Thuật toán phân rã biểu thức đa đầu vào ($N$ pins $\rightarrow N-1$ micro-rules 32-byte). Thuật toán cấp phát thanh ghi ảo ngược an toàn từ `VREG15` xuống `VREG0` nhằm triệt tiêu va chạm với các thanh ghi do người dùng định nghĩa ở dải thấp.
   - [`RuleCompiler.cs`](file:///g:/HoaNV/Projects/SimplePLC/src/SimplePLC.Application/Logic/Compilation/RuleCompiler.cs) & [`GraphGrammarV1.cs`](file:///g:/HoaNV/Projects/SimplePLC/src/SimplePLC.Application/Logic/Graph/GraphGrammarV1.cs): Tích hợp phân giải khối Math trong đồ thị logic.

4. **Tầng 4 — Infrastructure Layer ([`src/SimplePLC.Infrastructure/`](file:///g:/HoaNV/Projects/SimplePLC/src/SimplePLC.Infrastructure/)):**
   - [`McuReferenceSimulator.cs`](file:///g:/HoaNV/Projects/SimplePLC/src/SimplePLC.Infrastructure/Simulator/McuReferenceSimulator.cs): Hiện thực `EvaluateMathAlu` và `ExecuteMathOp` xử lý chuẩn xác cả 8 toán tử trên `Int32` và `Float32`, cơ chế bảo vệ chia cho 0 an toàn không crash CPU (trả về 0 hoặc MaxValue/NaN guard), kẹp bão hòa 64-bit tránh tràn số và khử giá trị NaN/Infinity.

5. **Tầng 5 — Presentation UI/UX Layer ([`SimplePLC.Studio/`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/)):**
   - [`Colors.xaml`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Styles/Colors.xaml): Bổ sung semantic token `{DynamicResource NodeMathBrush}` (`#0F766E` - Teal công nghiệp) tuân thủ 100% Zero Hardcoded Hex.
   - [`Icons.xaml`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Styles/Icons.xaml): Thêm vector SVG `<StreamGeometry>` `IconMathAlu`.
   - [`MathPinBindingViewModel.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/ViewModels/MathPinBindingViewModel.cs): Finite State Machine quản lý trạng thái soạn thảo (Drafting) và xác nhận (Committed), tự động chuẩn hóa (`"100.0"` $\rightarrow$ `"100"` với INT) và tự động hoàn tác khi nhập dữ liệu không hợp lệ.
   - [`MathNodeViewModel.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/ViewModels/MathNodeViewModel.cs): Quản lý trực quan khối Math, chân đầu vào động (2..8), kẹp ràng buộc theo toán tử, hiển thị biểu thức thời gian thực `[IN1 + IN2 -> OUT]`.
   - [`NodeTemplates.xaml`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Styles/NodeTemplates.xaml): DataTemplate đồ họa FBD cho `MathNodeViewModel` với hình học Fluent 2 (`CornerRadius="6"`, padding 7,3).
   - [`LogicEditorView.xaml`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Views/LogicEditorView.xaml): Bảng thuộc tính Inspector hoàn chỉnh cho khối Math.
   - [`LocalizationService.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Services/LocalizationService.cs): Hỗ trợ bản địa hóa song ngữ EN/VI đầy đủ.
   - [`ProjectModel.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Models/ProjectModel.cs): Lưu trữ và phục hồi hoàn hảo cấu hình khối Math trong tệp dự án.

### 38.3. Kết Quả Kiểm Thử & Nghiệm Thu
1. **Kiểm Tra Token UI/UX XAML:**
   - `validate-xaml-tokens.ps1 -FilePath "SimplePLC.Studio/Views/LogicEditorView.xaml"`: **`[PASS]` 0 violations**.
   - `validate-xaml-tokens.ps1 -FilePath "SimplePLC.Studio/Styles/NodeTemplates.xaml"`: **`[PASS]` 0 violations**.
2. **Kiểm Tra Kiến Trúc Backend:**
   - `validate-backend-arch.ps1`: **`[PASS]` 0 architecture violations**.
3. **Kiểm Tra QA, TDD & Living Docs:**
   - `validate-tdd-docs.ps1`: **`[PASS]` 0 violations** (100% public members có XML doc summary).
4. **Kiểm Tra Tài Liệu Kỹ Thuật & ICD:**
   - `validate-docs.ps1`: **`[PASS]` 0 violations**, 89 trang wiki và 15 ICDs phần cứng đồng bộ, VitePress build sạch sẽ 100%.
5. **Biên Dịch & Bộ Kiểm Thử Tự Động Toàn Hệ Thống:**
   - `dotnet build SimplePLC.sln`: **0 Errors, 0 Warnings**.
   - `dotnet test SimplePLC.sln --no-build`: **2,223 / 2,223 tests PASS (100%)**, trong đó có 38 tests mới kiểm thử toàn diện Happy Path, Boundary/Overflow và Fault Injection cho khối Math.


---

## 39. Chuẩn Hóa Khối Hàm `MATH` (Pure Arithmetic) & Tinh Gọn Mã Lệnh Liên Tục `SPLC_ACT_MATH_OP = 8`

### 39.1. Bối Cảnh & Động Lực Kỹ Thuật
- Sau khi rà soát kiến trúc theo chuẩn quốc tế IEC 61131-3 và đối chiếu với các phần mềm PLC hàng đầu thế giới (Siemens TIA Portal, Beckhoff TwinCAT, Rockwell Studio 5000), hệ thống nhận diện 2 điểm cần tối ưu hóa:
  1. **Tính thuần khiết của khối Math (Pure Arithmetic):** Khối `MATH` chỉ nên tập trung cho 5 phép toán số học đại số thuần túy (`ADD`, `SUB`, `MUL`, `DIV`, `MOD`). Các hàm chọn lọc và giới hạn (`MIN`, `MAX`, `CLAMP/LIMIT`) thuộc nhóm *Selection & Limiting Functions*, sẽ được phân tách thành khối riêng biệt để tránh lai tạp và giảm tải nhận thức cho kỹ sư.
  2. **Dọn dẹp khoảng trống mã hành động (Contiguous ActionKind):** Loại bỏ hoàn toàn các enum dư thừa `SubTag (8)`, `MulTag (9)`, `DivTag (10)`, `ModTag (11)`. Thu gọn mã hành động duy nhất của khối toán học từ `12` về đúng giá trị **`SPLC_ACT_MATH_OP = 8`** (`ActionKind.MathOp = 8`), đảm bảo dãy mã hành động liên tục, không bị đứt đoạn hay để lại "lỗ hổng" trong bảng opcode.

### 39.2. Các Hạng Mục Đã Triển Khai
1. **Domain Layer ([`src/SimplePLC.Domain/`](file:///g:/HoaNV/Projects/SimplePLC/src/SimplePLC.Domain/)):**
   - [`RuleEnums.cs`](file:///g:/HoaNV/Projects/SimplePLC/src/SimplePLC.Domain/Enums/RuleEnums.cs): Cập nhật `ActionKind.MathOp = 8`. Rút gọn `MathOperator` còn 5 toán tử chuẩn: `Add = 0`, `Subtract = 1` (`Sub`), `Multiply = 2` (`Mul`), `Divide = 3` (`Div`), `Modulo = 4` (`Mod`).
   - [`MathNodeModel.cs`](file:///g:/HoaNV/Projects/SimplePLC/src/SimplePLC.Domain/Models/MathNodeModel.cs): Loại bỏ hoàn toàn các ràng buộc riêng cho Clamp.
2. **Protocol Layer ([`src/SimplePLC.Protocol/`](file:///g:/HoaNV/Projects/SimplePLC/src/SimplePLC.Protocol/)):**
   - [`RuleEnums.cs`](file:///g:/HoaNV/Projects/SimplePLC/src/SimplePLC.Protocol/Enums/RuleEnums.cs): `SPLC_ActionType.MATH_OP = 8`. Enum `SPLC_MathOp` giới hạn trong dải `ADD=0..MOD=4`.
   - [`simpleplc_protocol_v2_0.h`](file:///g:/HoaNV/Projects/SimplePLC/src/SimplePLC.Protocol/Firmware/simpleplc_protocol_v2_0.h): Cập nhật `SPLC_ACT_MATH_OP = 8` và enum `SPLC_MathOp_t` tương thích vi điều khiển.
3. **Application Layer ([`src/SimplePLC.Application/`](file:///g:/HoaNV/Projects/SimplePLC/src/SimplePLC.Application/)):**
   - [`MathMacroLowering.cs`](file:///g:/HoaNV/Projects/SimplePLC/src/SimplePLC.Application/Logic/Compilation/MacroLowering/MathMacroLowering.cs): Bổ sung chốt kiểm soát toán học: `SUB`, `DIV`, `MOD` là các phép toán không giao hoán, bắt buộc chỉ có chính xác 2 chân ngõ vào. Nếu cố tình cấp $>2$ chân, trình biên dịch báo lỗi `ErrMathNoInput`.
4. **Infrastructure Layer ([`src/SimplePLC.Infrastructure/`](file:///g:/HoaNV/Projects/SimplePLC/src/SimplePLC.Infrastructure/)):**
   - [`McuReferenceSimulator.cs`](file:///g:/HoaNV/Projects/SimplePLC/src/SimplePLC.Infrastructure/Simulator/McuReferenceSimulator.cs): Dọn sạch các case thừa trong `ExecuteAction`, tối ưu hóa `EvaluateMathAlu` cho 5 phép toán với khả năng chống chia cho 0 và bão hòa 64-bit int clamp.
5. **Presentation Layer ([`SimplePLC.Studio/`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/)):**
   - [`MathNodeViewModel.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/ViewModels/MathNodeViewModel.cs): Xóa bỏ cờ `IsClampOperator`. Tự động cắt giảm chân về 2 khi kỹ sư chuyển từ `Add`/`Mul` sang `Sub`/`Div`/`Mod`. Ký hiệu badge hiển thị chuẩn xác (`+`, `−`, `×`, `÷`, `%`).
   - [`GraphNodeViewModel.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/ViewModels/GraphNodeViewModel.cs) & [`LocalizationService.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Services/LocalizationService.cs): Cập nhật dropdown Inspector và từ điển song ngữ.
6. **Tài Liệu Kỹ Thuật (Docs & Specifications):**
   - Đồng bộ [`MCU_CONFORMANCE_SPECIFICATION_V2_0.md`](file:///g:/HoaNV/Projects/SimplePLC/docs/03-firmware/00-MCU_CONFORMANCE_SPECIFICATION_V2_0.md), [`SPLC_SPEC_MATH_007_ICD.md`](file:///g:/HoaNV/Projects/SimplePLC/docs/03-firmware/14-SPLC_SPEC_MATH_007_ICD.md), và [`SPLC_SPEC_MATH_BLOCK_ARCHITECTURE.md`](file:///g:/HoaNV/Projects/SimplePLC/docs/01-architecture/07-SPLC_SPEC_MATH_BLOCK_ARCHITECTURE.md) với `SPLC_ACT_MATH_OP = 8` và bảng ALU opcode 0..4.

### 39.3. Kết Quả Kiểm Thử & Nghiệm Thu
1. **Frontend XAML Design Token Check:** `[PASS]` 0 violations.
2. **Backend Clean Architecture Invariants:** `[PASS]` 0 architecture violations.
3. **QA, TDD & Living Documentation:** `[PASS]` 0 violations (100% public APIs có XML doc summary).
4. **Firmware ICD & VitePress Portal:** `[PASS]` 0 violations, VitePress build 100% sạch sẽ.
5. **Toàn Bộ Bộ Kiểm Thử Hệ Thống:**
   - `dotnet build SimplePLC.sln`: **0 Errors, 0 Warnings**.
   - `dotnet test SimplePLC.sln --no-build`: **2,222 / 2,222 tests PASS (100%)**.

---

## 40. Hộp Thoại Cấu Hình Chuyên Sâu Khối `MATH` (`MathConfigDialogView`) & Chuẩn Hóa Toán Hạng `ANY_NUM`

### 40.1. Vấn Đề Nhận Diện Từ Thực Tế Sử Dụng
1. **Ô nhiễm biến ảo `NhietDo_C`:** Khi khởi động ứng dụng mà chưa khai báo tag/alias nào, danh mục tag vẫn xuất hiện `[NhietDo_C] — Cảm biến nhiệt độ`. Nguyên nhân gốc rễ là do hàm khởi tạo `DtuGatewayStudioViewModel.cs` nạp sẵn 1 bản ghi mẫu vào `FastRegisters`, vô tình đồng bộ vào `TagCatalog.RegisterTags`.
2. **Vi phạm chuẩn đại số IEC 61131-3:** Danh sách chọn toán hạng ở thanh Inspector trước đó cho phép chọn cả biến kiểu logic `BOOL` (`DI0..7`, `DO0..7`, `VFLAG0..31`). Phép toán ALU là số học đại số thuần túy (`ANY_NUM`), không được phép cộng trừ trên biến logic nhị phân.
3. **Bố cục thanh bên (Inspector) chật hẹp:** Thêm nhiều chân toán hạng trên thanh bên 250px khiến giao diện bị dồn cục, khó thao tác và không trực quan. Cần một hộp thoại cấu hình độc lập (popup dialog) chuẩn hóa như các khối `HttpClient`, `MqttSub`, `ModbusRead`, `DirectWrite`.

### 40.2. Các Giải Pháp & Hạng Mục Đã Triển Khai
1. **Xử Lý Triệt Để Tag Ảo `NhietDo_C`:**
   - Xóa bỏ dòng gán mẫu trong constructor của [`DtuGatewayStudioViewModel.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/ViewModels/DtuGatewayStudioViewModel.cs#L1494-L1496). `TagCatalog` khởi động hoàn toàn sạch sẽ, không có bất kỳ tag ma nào.
2. **Chuẩn Hóa Bộ Sưu Tập Toán Hạng Số Học `ANY_NUM`:**
   - Bổ sung bộ sưu tập `NumericTags` vào [`TagCatalogViewModel.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/ViewModels/TagCatalogViewModel.cs): Tự động lọc chỉ những Tag có kiểu số (`AnalogInput`, `VirtualRegister`, `VirtualRegisterRetain`, `Counter`, `ModbusHolding/Input`) và loại bỏ hoàn toàn các Tag kiểu `Boolean` / `IsDigital`.
   - Cập nhật cả trong Inspector và Hộp thoại cấu hình: Toán hạng chỉ hiển thị danh sách `NumericTags`.
3. **Thiết Kế Hộp Thoại Cấu Hình Desktop Chuẩn Công Nghiệp ([`MathConfigDialogView.xaml`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Views/MathConfigDialogView.xaml)):**
   - **Kích thước chuẩn:** `Width="1040" Height="660"`, `WindowChromeHelper.EnableIndependentMinimize="True"`, `Background="{DynamicResource BgWindowBrush}"`.
   - **Header Banner:** Khối 38x38px chứa vector SVG `IconMathAlu`, tiêu đề và phụ đề song ngữ rõ ràng, badge hiển thị trạng thái toán tử hiện tại.
   - **Cột Trái (Thiết Lập & Danh Sách Chân - 1.35*):**
     - Card 1: Tham số chung (Toán tử số học, Kiểu dữ liệu, Tag ngõ ra OUT nhận kết quả từ `RegisterTags` hoặc `(Chưa gán)`).
     - Card 2: Bảng toán hạng đầu vào (Chuẩn IBM Carbon data density): Cột `#`, `Chân`, `Chế độ` (Tag / Hằng số), `Toán hạng` (Dropdown `NumericTags` hoặc ô nhập số với LostFocus auto-normalize/auto-revert), `Kiểu DL` (REAL/INT32), nút `Xóa`.
     - Nút `+ Thêm toán hạng` tự động vô hiệu hóa khi toán tử là `SUB`, `DIV`, `MOD` (giới hạn cứng 2 chân) hoặc khi đã đạt 8 chân.
   - **Cột Phải (Chẩn Đoán ALU & Mô Phỏng Tức Thời - 360px):**
     - Khung công thức đại số monospace thời gian thực (ví dụ `VREG0 = AI0 + 10.5`).
     - Khung nhập giá trị kiểm thử mô phỏng (Live Simulation Inputs).
     - Khung kết quả ALU nổi bật: Hiển thị giá trị tức thời, kiểu suy diễn (`REAL` / `INT32`), badge trạng thái `✓ HỢP LỆ` hoặc `⚠️ LỖI CHIA CHO 0`.
     - Banner quy tắc chuẩn hóa IEC 61131-3 (Type promotion, xử lý chia cho 0 an toàn).
   - **Footer Action Bar:** Nút `Khôi phục mặc định` (bên trái), `Hủy bỏ` và `Lưu cấu hình` (bên phải).
4. **Tích Hợp ViewModel & Thao Tác Trực Tiếp:**
   - [`MathConfigDialogViewModel.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/ViewModels/MathConfigDialogViewModel.cs): Quản lý vòng đời hộp thoại, tính toán công thức, mô phỏng kết quả tức thời, đồng bộ ngược về `MathNodeViewModel` khi bấm Apply.
   - [`LogicEditorViewModel.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/ViewModels/LogicEditorViewModel.cs): Bổ sung lệnh `OpenMathConfigCommand`.
   - [`LogicEditorView.xaml.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Views/LogicEditorView.xaml.cs): Nhấp đúp chuột (Double Click) vào khối Math trên canvas sẽ tự động mở hộp thoại cấu hình.
   - [`LogicEditorView.xaml`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Views/LogicEditorView.xaml): Bổ sung nút bấm `⚙ Cấu hình Khối...` trên thanh Inspector.
   - [`LocalizationService.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Services/LocalizationService.cs): Bổ sung toàn bộ từ điển song ngữ sạch sẽ, không trộn lẫn tiếng Anh - tiếng Việt trong cùng một nhãn.

### 40.3. Nghiệm Thu & Kiểm Thử
1. **Frontend XAML Design Token Check:** `[PASS]` 0 violations (`MathConfigDialogView.xaml`, `LogicEditorView.xaml`).
2. **Backend Clean Architecture Invariants:** `[PASS]` 0 architecture violations.
3. **QA, TDD & Living Documentation:** `[PASS]` 0 violations (100% test suite pass rate).
4. **Biên Dịch Toàn Giải Pháp:** `dotnet build SimplePLC.sln` đạt **0 Errors, 0 Warnings**.

---

## 41. Chuẩn Hóa Song Ngữ Thuần, Cân Bằng Bố Cục Dialog & Hỗ Trợ Phép Trừ (SUB) Đa Toán Hạng

### 41.1. Bối Cảnh & Vấn Đề
1. **Lẫn lộn Tiếng Anh / Tiếng Việt:**
   - Thanh Inspector ở chế độ tiếng Anh vẫn hiển thị nhãn số lượng chân có đuôi tiếng Việt `"2 chân"` do hardcoded `StringFormat='{}{0} chân'`.
   - Khi Tag ngõ ra OUT chưa gán, trường hiển thị bị rỗng hoặc cố định tiếng Việt `(Chưa gán)`.
   - Hộp thoại cấu hình có các placeholder cố định tiếng Việt trong ComboBox ItemTemplate `TargetNullValue='(Chưa gán / Nối dây trên Canvas)'`.
2. **Bố Cục (Layout) Chưa Cân Xứng:**
   - Các đoạn phụ đề trong thẻ cấu hình bị cắt cụt do thuộc tính `TextTrimming="CharacterEllipsis"` trên khung cố định (`BOOL logic ty...`, `IEC 61...`).
   - Cột bên phải (Chẩn đoán ALU) đặt `VerticalAlignment="Top"` tạo ra một khoảng trống màu xám lớn bên dưới, mất cân đối với cột bên trái.
   - Biểu tượng `ℹ️` dùng ký tự Emoji Unicode thay vì vector SVG `<Path>`.
3. **Phép Trừ (SUB) Bị Reset Về 2 Chân:**
   - Do ràng buộc `IsSubOrDivOrMod`, khi chuyển toán tử sang phép trừ `SUB`, hệ thống tự động xóa bỏ các chân thứ 3 trở đi và khóa nút thêm chân, trong khi phép trừ đại số nhiều ngõ vào $(A - B - C)$ là chuỗi trừ trái-sang-phải hoàn toàn hợp lệ trong tính toán công nghiệp.

### 41.2. Giải Pháp Triển Khai
1. **Bổ Sung & Tách Biệt Bộ Từ Điển Song Ngữ Sạch:**
   - Bổ sung `CommonPinsCountFormat` (`"{0} chân"` / `"{0} pins"`), `CommonUnassigned` (`"(Chưa gán)"` / `"(Unassigned)"`).
   - Chuẩn hóa các nhãn tóm tắt trên Inspector: `Formula:`, `Operator:`, `Data Type:`, `Output (OUT):`, `Operands:`, `Result:`.
   - Dùng `DataTrigger` trong XAML để tự động cập nhật text placeholder của ComboBox ngõ ra (`MathConfigOptNoneTag`) theo ngôn ngữ hiện hành mà không cần giá trị hardcoded.
2. **Cân Bằng Bố Cục Giao Diện Dialog & Inspector:**
   - Chuyển `TextTrimming="CharacterEllipsis"` sang `TextWrapping="Wrap"` cho các đoạn mô tả phụ đề.
   - Mở rộng cột phải sang `VerticalAlignment="Stretch"` và neo khung quy tắc chuẩn hóa IEC 61131-3 xuống đáy thẻ, lấp đầy không gian một cách hài hòa.
   - Thay thế emoji thô `ℹ️` bằng SVG Vector Icon `IconInfoOutline`.
3. **Mở Rộng Phép Trừ (SUB) Hỗ Trợ 2 Đến 8 Toán Hạng:**
   - Điều chỉnh `MathMacroLowering`, `MathNodeViewModel`, `MathConfigDialogViewModel`: chuyển điều kiện khóa 2 chân từ `IsSubOrDivOrMod` sang `IsDivOrMod` (chỉ phép chia `DIV` và `MOD` bị giới hạn 2 chân).
   - Phép trừ `SUB` hỗ trợ chuỗi toán hạng kết hợp trái sang phải: `OUT = IN1 - IN2 - IN3 ...`, hạ cấp thành chuỗi Micro-Rules trung gian chính xác.

### 41.3. Kết Quả Kiểm Thử & Nghiệm Thu
1. **UI/UX Token Validator:** `[PASS]` 0 violations.
2. **Clean Architecture Validator:** `[PASS]` 0 violations.
3. **QA / TDD Suite:** `[PASS]` 2,226 / 2,226 unit tests passed (100% pass rate).
4. **Docs Portal VitePress Build:** `[PASS]` 0 violations.

---

## 42. ĐỒNG BỘ TÀI LIỆU KHỐI MATH TRÊN CỔNG WEB WIKI DOCS-PORTAL (VITEPRESS)

### 42.1. Mục Tiêu & Chuẩn Hóa
- Đồng bộ hóa tài liệu kỹ thuật của khối **`MATH`** với toàn bộ các khối chức năng khác trên hệ thống (`SCALE`, `COMPARATOR`, `TON`, `CTU`,...).
- Tuân thủ nghiêm ngặt chỉ dẫn: tập trung vào đặc tả kỹ thuật, hướng dẫn vận hành, hợp đồng chân nối, quy tắc chuẩn hóa IEC 61131-3 và ví dụ thực tế; không đưa các chi tiết mã nguồn nội bộ tầng sâu vào tài liệu người dùng.

### 42.2. Các Tệp Tài Liệu Đã Bổ Sung & Cập Nhật
1. **Trang tài liệu kỹ thuật chi tiết song ngữ (Datasheet):**
   - [`docs-portal/en/blocks/math.md`](file:///g:/HoaNV/Projects/SimplePLC/docs-portal/en/blocks/math.md): Tài liệu tiếng Anh chuẩn IEC 61131-3 ANY_NUM, bảng đặc tả 5 toán tử, quy tắc nâng kiểu tự động (Type Promotion), giao diện hộp thoại cấu hình và kịch bản thực tế (Multi-Sensor Flow Summation).
   - [`docs-portal/vi/blocks/math.md`](file:///g:/HoaNV/Projects/SimplePLC/docs-portal/vi/blocks/math.md): Bản tiếng Việt tương ứng, giải thích chi tiết bảng chẩn đoán ALU và bảo vệ chống chia cho 0.
2. **Cập nhật Mục lục Tổng quan Danh mục Khối:**
   - [`docs-portal/en/blocks/overview.md`](file:///g:/HoaNV/Projects/SimplePLC/docs-portal/en/blocks/overview.md) và [`docs-portal/vi/blocks/overview.md`](file:///g:/HoaNV/Projects/SimplePLC/docs-portal/vi/blocks/overview.md): Bổ sung `MATH` vào nhóm *Timing, Counting & Scaling*.
3. **Cập nhật Cây Điều Hướng Sidebar:**
   - [`docs-portal/.vitepress/locales/en.mts`](file:///g:/HoaNV/Projects/SimplePLC/docs-portal/.vitepress/locales/en.mts) và [`docs-portal/.vitepress/locales/vi.mts`](file:///g:/HoaNV/Projects/SimplePLC/docs-portal/.vitepress/locales/vi.mts): Đưa mục `MATH` vào menu sidebar của cả 2 ngôn ngữ.
4. **Cập nhật Đặc Tả Kiến Trúc:**
   - [`docs/01-architecture/07-SPLC_SPEC_MATH_BLOCK_ARCHITECTURE.md`](file:///g:/HoaNV/Projects/SimplePLC/docs/01-architecture/07-SPLC_SPEC_MATH_BLOCK_ARCHITECTURE.md): Bổ sung ghi nhận hạ cấp đa toán hạng cho phép `SUB` và đặc tả giao diện hộp thoại cấu hình `MathConfigDialogView`.

### 42.3. Kết Quả Kiểm Tra
- Chạy `validate-docs.ps1`: Quét 91 tệp markdown, kiểm tra 15 đặc tả ICD firmware, biên dịch VitePress hoàn toàn thành công với **0 lỗi và 0 cảnh báo**.

---

## 43. Tích Hợp Trọn Vẹn Bộ Cài Đặt Desktop Installer (Inno Setup 6) Vào Pipeline CI/CD & Phát Hành Đa Định Dạng

### 43.1. Bối cảnh & Yêu cầu Kỹ thuật
- Nhằm cung cấp trải nghiệm cài đặt tiêu chuẩn công nghiệp (tương tự VS Code / Siemens TIA Portal), dự án đã bổ sung phân hệ Inno Setup 6 tại `installer/`:
  - `SynaptixIDE.iss`: Kịch bản biên dịch cài đặt hiện đại, nén LZMA2 Ultra64 Solid, tự động đăng ký file association `.synx`, shortcut Desktop/Start Menu và đóng gói kèm công cụ giả lập `Tools/SimplePLC.McuEmulator.exe`.
  - `build-installer.ps1`: Tự động tạo branding assets và kích hoạt Inno Setup Compiler (`ISCC.exe`).
- **Yêu cầu CI/CD:** Tích hợp bộ cài đặt này vào chu trình phát hành tự động của dự án cả ở môi trường cục bộ (`cicd_local.bat` / `scripts/cicd_local.ps1`) và môi trường tự động hóa đám mây GitHub Actions (`.github/workflows/ci.yml`).

### 43.2. Các Giải Pháp Đã Triển Khai
1. **Tham số hóa Phiên bản Động (Dynamic Versioning):**
   - Trong [`installer/SynaptixIDE.iss`](file:///g:/HoaNV/Projects/SimplePLC/installer/SynaptixIDE.iss): Bọc định nghĩa phiên bản trong khối tiền xử lý `#ifndef MyAppVersion ... #endif`.
   - Trong [`installer/build-installer.ps1`](file:///g:/HoaNV/Projects/SimplePLC/installer/build-installer.ps1): Nhận diện tham số `$Version` tự động và truyền `/DMyAppVersion=$Version` vào trình biên dịch `ISCC.exe`.
2. **Nâng cấp Runner CI/CD Cục bộ ([`scripts/cicd_local.ps1`](file:///g:/HoaNV/Projects/SimplePLC/scripts/cicd_local.ps1)):**
   - Thêm bước **[6/7] Building official Windows desktop setup installer**: Tự động phát hiện Inno Setup Compiler, biên dịch ra `installer/dist/SynaptiX_IDE_Setup_v$Version.exe`, sao chép vào `publish/release/` và tính toán mã băm SHA256.
   - Bổ sung cờ `-SkipInstaller` cho phép bỏ qua bước biên dịch installer khi cần kiểm thử nhanh.
   - Cập nhật [`SHA256SUMS.txt`](file:///g:/HoaNV/Projects/SimplePLC/publish/release/SHA256SUMS.txt) ghi nhận đầy đủ 3 tệp phát hành: Installer `.exe`, Portable `.exe` và `.zip`.
   - Cập nhật [`RELEASE_NOTES_v$Version.md`](file:///g:/HoaNV/Projects/SimplePLC/publish/release/RELEASE_NOTES_v2.15.1.md) và danh mục `$assetsToUpload` cho GitHub Releases.
3. **Cập nhật GitHub Actions Pipeline ([`.github/workflows/ci.yml`](file:///g:/HoaNV/Projects/SimplePLC/.github/workflows/ci.yml)):**
   - Bổ sung bước `Build Official Windows Setup Installer` tự động biên dịch và sao chép gói setup vào `./publish/win-x64/` trước khi upload artifact và publish GitHub Release.
4. **Cập nhật Cổng Thông tin & Tài liệu ([`README.md`](file:///g:/HoaNV/Projects/SimplePLC/README.md)):**
   - Phân định rõ 2 tùy chọn tải về: Bản cài đặt chính thức (Khuyên dùng) và Bản Portable độc lập.

### 43.3. Kết quả Kiểm thử & Nghiệm thu
- **Biên dịch Inno Setup Độc lập:** Tạo thành công `installer/dist/SynaptiX_IDE_Setup_v2.15.1.exe`.
- **Local CI/CD Runner:** `.\cicd_local.bat` hoàn thành 100% với exit code 0, tạo đủ 3 định dạng trong `publish/release/` cho phiên bản `v2.15.1`.

---

## 44. Tinh Chỉnh Khối MATH ALU, Đồng Bộ Hóa Dây Nối FBD Canvas & Chốt Hợp Đồng Wire Giao Tiếp Firmware

### 44.1. Bối cảnh & Yêu cầu Kỹ thuật
- Hoàn thiện trải nghiệm người dùng trên hộp thoại cấu hình khối `MATH` (`MathConfigDialogView.xaml` & `MathConfigDialogViewModel.cs`):
  1. Loại bỏ banner tĩnh `IEC 61131-3 Industrial Rules` theo yêu cầu trực quan của người dùng.
  2. Cho phép đổi tên nhãn chân PIN trực tiếp (Rename Pin) thông qua ô TextBox với cập nhật hai chiều đồng bộ tới Connector trên Canvas, công thức Algebraic Formula và bảng mô phỏng Live Simulation.
  3. Tự động đồng bộ toán hạng khi nối dây trên Canvas: chân đã có dây nối sẽ khóa ô chọn (disable control) và hiển thị trực tiếp Tag nối từ Canvas mà không thêm chữ thừa rườm rà.
  4. Quản lý Tag ngõ ra `OUT`: giữ ô chọn trên popup nhưng tự động khóa lại khi chân `OUT` của Math đã được kéo dây vào khối Action; đồng thời lọc động danh mục `AvailableOutputTags` theo kiểu dữ liệu suy diễn (`INT32` -> các tag thanh ghi nguyên; `REAL` -> các tag thanh ghi số thực Float32).
- Thống nhất và chốt hợp đồng wire 32-byte với phân hệ Firmware MCU:
  1. Giữ nguyên 5 toán tử số học cơ bản (`ADD=0, SUB=1, MUL=2, DIV=3, MOD=4`), tạm hoãn 3 hàm `MIN/MAX/CLAMP`.
  2. Bắt lỗi chia cho 0 với hằng số ngay tại tầng biên dịch compiler (`SPLC-COMP-MATH-DIV-ZERO`).
  3. Phòng thủ an toàn kiểu dữ liệu nghiêm ngặt: cấm trộn trực tiếp tag `INT32` và tag `Float32` trong cùng một khối Math (`SPLC-COMP-MATH-TYPE-MISMATCH`).
  4. Khai báo Bit 16 (`SPLC_CAP_MATH_ALU = 0x00010000`) trong `SPLC_HardwareCapabilities` tại thanh ghi 0x0A08.
  5. Khai báo Bit 3 (`SPLC_HEALTH_MATH_FAULT = 1 << 3`) trong `SPLC_HealthFlags` để firmware báo cờ chia 0 khi chạy thực tế.

### 44.2. Các Giải Pháp Đã Triển Khai
1. **Frontend UI/UX ([`MathConfigDialogView.xaml`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Views/MathConfigDialogView.xaml) & [`MathConfigDialogViewModel.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/ViewModels/MathConfigDialogViewModel.cs)):**
   - **Tái cấu trúc bố cục 1 cột dọc chuẩn Fluent 2 (Single-Column Architecture):** Thu gọn kích thước cửa sổ từ `1060 × 720` về `750 × 620` (nhỏ gọn, vừa vặn không chiếm trọn màn hình).
   - **Phân bổ lại diện tích hợp lý:**
     * Cột Tên chân PIN được tăng chiều rộng từ `54px` lên **`130px`**, khắc phục triệt để lỗi cụt chữ khi người dùng nhập tên dài (`counter 1`, `Sensor_Temp`).
     * Ô chọn `Output Tag (OUT)` được tăng tỷ lệ lên `2*` (gần 50% hàng) để hiển thị đầy đủ tên Tag + Alias.
     * Cột chọn toán hạng Tag/Constant được mở rộng trọn chiều ngang (`Width="*"`), thênh thang cho tên tag công nghiệp.
     * Cột chế độ Mode thu gọn về `95px`, `#` là `32px`, `Type` là `65px`, `Delete` là `32px`.
   - **Gỡ bỏ cột phải trùng lặp:** Loại bỏ bảng danh sách chân thứ hai và khối xám inset cồng kềnh. Chuyển dòng công thức đại số và kết quả mô phỏng tức thời xuống thành **Thanh Preview Bar tích hợp ngang** gọn gàng (cao 40px) dưới chân bảng toán hạng.
   - **Đồng nhất 100% thị giác & Typography:**
     * Toàn bộ nhãn, nút bấm, header dùng font `Segoe UI`.
     * Tên biến, công thức, giá trị số dùng font monospace `Consolas` sắc nét.
     * Toàn bộ màu sắc dùng chuẩn `{DynamicResource ...}` từ `Colors.xaml`, loại bỏ badge `• Add (ALU)` thừa thãi trên header.
   - Thêm `InverseBooleanConverter` để khóa các điều khiển khi `IsWiredFromCanvas` hoặc `IsOutputWiredFromCanvas` là `true`.
   - Bổ sung hàm `UpdateAvailableOutputTags(bool isReal)` tự động cập nhật danh sách thanh ghi hợp lệ phù hợp với kiểu dữ liệu kết quả suy diễn từ các toán hạng đầu vào.
   - Khóa nút xóa chân (`CanDelete = false`) khi chân đó đang có dây kết nối trên Canvas.
2. **Logic Editor Canvas & Reactive Wire Binding ([`LogicEditorViewModel.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/ViewModels/LogicEditorViewModel.cs)):**
   - Trong `SyncWireBinding`: bổ sung nhánh xử lý khi nối dây vào chân vào của `MathNodeViewModel` (`pin.Tag = sourceTag; pin.IsConstant = false; mathTarget.NotifyBindingChanged()`) và khi nối chân ra của `MathNodeViewModel` vào khối `ActionNode` (`mathNodeFrom.OutputTag = actNode.TargetTag`).
   - Trong `ClearWireBinding`: bổ sung nhánh dọn dẹp tương ứng khi tháo dây nối.
3. **Trình Biên Dịch Compiler Diagnostics & Lowering ([`MathMacroLowering.cs`](file:///g:/HoaNV/Projects/SimplePLC/src/SimplePLC.Application/Logic/Compilation/MacroLowering/MathMacroLowering.cs) & [`RuleCompiler.cs`](file:///g:/HoaNV/Projects/SimplePLC/src/SimplePLC.Application/Logic/Compilation/RuleCompiler.cs)):**
   - Bổ sung mã lỗi `SPLC-COMP-MATH-DIV-ZERO` phát hiện chia cho hằng số 0 ($|val| < 10^{-9}$).
   - **Hiện thực hóa Phương án B (Tự động thăng hạng kiểu dữ liệu - Implicit Type Promotion):** Khi biểu thức số học chạy ở chế độ số thực (`isFloat == true`), toàn bộ các toán hạng là biến số nguyên (`INT32` / `AnalogInput`) được Compiler tự động sinh micro-rule `SCALE_TAG` ngầm ($k=1.0, b=0$) nạp vào thanh ghi nháp `VREG_SCRATCH` trước khi thực thi lệnh Math. Đảm bảo chip STM32 thực thi chuẩn xác 100% trong cùng một scan cycle mà người dùng không cần thủ công kéo thêm khối chuyển đổi.
4. **Giao Thức Giao Tiếp & Năng Lực Phần Cứng ([`SPLC_HardwareCapabilities.cs`](file:///g:/HoaNV/Projects/SimplePLC/src/SimplePLC.Domain/Enums/SPLC_HardwareCapabilities.cs) & [`DeviceEnums.cs`](file:///g:/HoaNV/Projects/SimplePLC/src/SimplePLC.Protocol/Enums/DeviceEnums.cs)):**
   - Bổ sung `MATH_ALU = 0x00010000` (Bit 16).
   - Bổ sung `MATH_FAULT = 1 << 3` (Bit 3) trong `SPLC_HealthFlags`.

### 44.3. Kết Quả Kiểm Thử & Nghiệm Thu
- **Unit Tests:**
  - `MathMacroLoweringTests.cs`: 18/18 tests PASS (kiểm tra đầy đủ chia 0 với hằng, modulo với hằng 0, tự động thăng hạng kiểu dữ liệu 2 chân và chuỗi 3 chân hỗn hợp qua rule SCALE ngầm).
  - `MathNodeViewModelTests.cs`: 18/18 tests PASS (kiểm tra đổi tên nhãn chân, đồng bộ 2 chiều dialog/node, cấm xóa chân có dây, lọc tag ngõ ra theo kiểu dữ liệu, bảo lưu tag khi nối dây).
  - `CanvasWireAutoBindingTests.cs`: 22/22 tests PASS (kiểm tra nối dây tự động gán tag, tháo dây tự động xóa tag cho cả input và output của khối Math).
  - Toàn bộ solution: **2,302 / 2,302 tests PASS (100%)** không có bất kỳ test nào thất bại hay bị bỏ qua.
- **Bộ 4 Script Kiểm Tra Tiêu Chuẩn Công Nghiệp:**
  - `validate-xaml-tokens.ps1`: PASS (0 violations).
  - `validate-backend-arch.ps1`: PASS (0 violations).
  - `validate-tdd-docs.ps1`: PASS (0 violations, 100% test pass).
  - `validate-docs.ps1`: PASS (0 violations, VitePress build sạch sẽ).

### 44.4. Tinh Chỉnh Giao Diện & Đồng Nhất Typography Card 3 (MathConfigDialogView)
- **Căn Chỉnh Thẳng Hàng Hoàn Tuyệt (Eliminate Layout Misalignment):**
  - Hợp nhất toàn bộ hàng hiển thị công thức và kết quả mô phỏng vào chung một dải container Inset (`Border` có `Height="34"` và `Padding="12,0"`).
  - Công thức đại số nằm bên trái (`Column="0"`, `VerticalAlignment="Center"`), nhãn kết quả và giá trị số nằm bên phải (`Column="1"`, `VerticalAlignment="Center"`), triệt tiêu hoàn toàn hiện tượng kết quả số bị bay lên cao ngang hàng với tiêu đề.
  - Hàng tiêu đề của Card 3 đưa nhãn công thức sang trái và huy hiệu kiểu dữ liệu (`Type: REAL` / `INT32`) cùng trạng thái (`VALID` / `FAULT`) sang góc phải trên cùng một đường cơ sở ngang (`DockPanel`).
- **Khử Khoảng Trống Thừa Bên Dưới (Eliminate Bottom Vertical Void):**
  - Chuyển `Window` từ chiều cao cố định `Height="620"` sang cơ chế tự động co giãn theo nội dung `SizeToContent="Height"`, kèm theo giới hạn `MinHeight="380"`.
  - Hộp thoại ôm khít vừa vặn 3 card nội dung (General, Operands, Formula), loại bỏ triệt để khoảng trống xám vô nghĩa giữa Card 3 và thanh nút bấm thao tác ở chân hộp thoại.
- **Đồng Nhất Một Màu Đen Duy Nhất Cho Văn Bản (Single Black Color Doctrine):**
  - Chuyển toàn bộ màu chữ của công thức, kết quả số, nhãn kết quả, kiểu dữ liệu và trạng thái từ màu xanh petrol (`PrimaryBrush`) hoặc xám mờ (`TextMutedBrush`) sang màu đen kỹ thuật chuẩn `{DynamicResource TextPrimaryBrush}` (`#111827`).
- **Đồng Bộ Phông Chữ & Kích Thước (Harmonized Typography):**
  - Toàn bộ dải tính toán bên trong khung Inset sử dụng thống nhất một họ font monospace kỹ thuật `Consolas` kích thước 12px.
  - Loại bỏ hoàn toàn kích thước số khổng lồ 18pt gây lệch lạc thị giác, đảm bảo tính thẩm mỹ chuẩn công nghiệp ANSI/ISA-101 và Microsoft Fluent 2.


---

## 45. Kiến Trúc Bảo Toàn Danh Tính Tag (Tag Identity Preservation), Tinh Chỉnh Nền Tảng AI Copilot & Phát Hành v2.15.3

### 45.1. Bối cảnh & Yêu cầu Kỹ thuật
1. **Bảo toàn danh tính Tag (Tag Identity Preservation):** Khi người dùng kết nối phần cứng PLC hoặc thực hiện đồng bộ `ProductDefinition`, phương thức `ApplyDeviceTags` trước đây khởi tạo lại các thực thể `TagModel` mới, dẫn đến việc các liên kết WPF DataGrid và ComboBox chọn tag bị reset hoặc mất kết nối. Cần cơ chế cập nhật tại chỗ (in-place mutation) để bảo lưu 100% tham chiếu đối tượng.
2. **Nâng cấp nền tảng AI Copilot:**
   - Tối ưu hóa kích thước prompt và bộ ngữ cảnh nạp vào mô hình LLM (`AiContextAggregator`, `AiPromptBuilder`).
   - Hỗ trợ các endpoint tương thích chuẩn OpenAI (`OpenAiCompatibleApiClient`) với cơ chế xử lý lỗi và cấu hình linh hoạt.
3. **Phát hành phiên bản v2.15.3:** Đóng gói toàn bộ các nâng cấp trên vào bộ cài đặt setup wizard `SynaptiX_IDE_Setup_v2.15.3.exe`, bản portable và tệp zip phân phối.

### 45.2. Các Giải Pháp Đã Triển Khai
1. **Tag Identity Preservation Architecture ([`TagCatalogViewModel.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/ViewModels/TagCatalogViewModel.cs)):**
   - Áp dụng kỹ thuật in-place mutation cho các Tag hiện có: chỉ cập nhật giá trị thuộc tính (`Direction`, `DataType`, `Description`) thay vì xóa và tạo mới instance `TagModel`.
   - Bổ sung bộ kiểm thử TDD chuyên sâu [`TagIdentityPreservationTests.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio.Tests/TagIdentityPreservationTests.cs) xác nhận tính ổn định của DataGrid binding.
2. **AI Copilot Token & Provider Optimization:**
   - Hoàn thiện `OpenAiCompatibleApiClient.cs` với token streaming và error handling.
   - Bổ sung các bài kiểm thử `AiChatBilingualSyncTests.cs` và `AiChatProviderAndTokenOptimizationTests.cs`.
3. **Đóng gói phát hành đa định dạng v2.15.3:**
   - `SynaptiX_IDE_Setup_v2.15.3.exe` (Inno Setup 6).
   - `SynaptixIDE.exe` / `SimplePLC.Studio.exe` (Portable).
   - `SynaptiX-IDE-v2.15.3-win-x64.zip`.

---

## 46. Kiến Trúc Bảo Toàn Chuỗi JSON MQTT Subscribe, Chuẩn Hóa JSON Key Nạp Xuống Thiết Bị & Phát Hành v2.15.4

### 46.1. Bối cảnh & Phát hiện Lỗi (The Visual Truth & Hardware Ground Truth Violations)
- **Vấn đề phản hồi:** Khi người dùng mở hộp thoại cấu hình **MQTT SUB**, chỉnh sửa/xóa vỏ bọc `"values": { ... }` ở ô kiểm thử bên phải để test JSON phẳng (ví dụ `{"grid": 10}`), thực hiện lưu dự án (`Ctrl+S`) rồi mở lại thì chuỗi JSON lại tự động quay về mẫu mặc định có chứa `"values"`.
- **Nguyên nhân kỹ thuật & rủi ro phần cứng:**
  1. `ProjectNodeData` thiếu trường `MqttSubSimPayload`, khiến chuỗi `SimInputPayload` của khối MQTT Subscribe không bao giờ được ghi xuống đĩa khi lưu tệp `.splc`.
  2. Constructor của `MqttSubConfigDialogViewModel` luôn tự động gọi `GenerateAndApplySamplePayload()` tạo ra mẫu có chứa `["values"] = valuesDict`, đè bẹp chuỗi do người dùng nhập.
  3. **Rủi ro nghiêm trọng:** `MqttPinBindingViewModel.ToDto()` trước đây thực hiện:
     ```csharp
     JsonPath = !string.IsNullOrWhiteSpace(ResolvedPath) ? ResolvedPath : JsonPath;
     ```
     Khi ô test sinh ra mẫu có bọc `"values"`, `ResolvedPath` bị ô nhiễm thành `"values.grid"`, đẩy thẳng xuống 12 bytes Modbus `json_key` của vi điều khiển (MCU). Khi SCADA bên ngoài gửi gói tin phẳng `{"grid": 10}`, MCU tìm kiếm `"values.grid"` sẽ thất bại và từ chối gói tin.

### 46.2. Các Giải Pháp Đã Triển Khai
1. **Bảo Toàn Chuỗi JSON Test (Persistence & State Management):**
   - Bổ sung `MqttSubSimPayload` vào `ProjectNodeData` ([`ProjectModel.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Models/ProjectModel.cs)).
   - Đồng bộ trong `LogicEditorViewModel.ExportSingleNodeData` và `CreateNodeFromData`.
   - Bổ sung trường `MqttWrapValues` cho khối MQTT PUB để bảo toàn tùy chọn bỏ bọc `values`.
2. **Khởi Tạo Hộp Thoại Thông Minh (Smart Dialog Initialization):**
   - Trong `MqttSubConfigDialogViewModel`, nếu `Node.SimInputPayload` đã có dữ liệu tùy biến, nạp trực tiếp vào `SimulationInput`, đặt `IsUserModified = true` và chạy `RunSimulation()` mà không sinh đè mẫu.
   - Nút `Reset Sample` khôi phục lại chế độ tự sinh (`Node.SimInputPayload = string.Empty; IsUserModified = false`).
   - Nút *Apply* đảm bảo đồng bộ `Node.SimInputPayload = SimulationInput`.
3. **Chuẩn Hóa `json_key` Gửi Xuống Phần Cứng (Hardware Ground Truth):**
   - Trong `MqttPinBindingViewModel.ToDto()`, trường `JsonPath` gửi xuống Device luôn lấy theo cấu hình người dùng (`JsonPath` hoặc `Label`), tuyệt đối không bị kết quả chạy thử trong Simulator tự động gắn prefix `"values."`.

### 46.3. Kết Quả Kiểm Thử & Nghiệm Thu
- **Bộ Kiểm Thử Đơn Vị TDD Mới:**
  - `MqttPinBinding_WhenSimulatedWithWrappedJson_PreservesCleanJsonPathInDto`: Xác nhận dù JSON mô phỏng có bọc `values: { grid: 5 }`, `ToDto().JsonPath` vẫn giữ sạch sẽ `"grid"`.
  - `MqttSub_WhenCustomPayloadEntered_PersistsAcrossExportAndImport`: Xác nhận chuỗi JSON tùy biến được lưu và khôi phục toàn vẹn 100% qua chu trình Export/Import.
  - `MqttSubConfig_WhenNodeHasExistingPayload_DoesNotOverwriteWithDefaultSample`: Xác nhận dialog khởi tạo từ node có sẵn payload không bị ghi đè bởi mẫu mặc định.
  - `MqttPublishNode_WrapValuesFalse_PersistsAcrossExportAndImport`: Xác nhận tùy chọn bỏ bọc `values` của MQTT PUB được bảo lưu khi Save/Open.
- **Toàn Bộ Test Suite:** **2,385 / 2,385 tests PASS (100%)**, 0 errors, 0 warnings.
- **Bộ Kiểm Định Tiêu Chuẩn Công Nghiệp:**
  - `validate-backend-arch.ps1`: PASS (0 violations).
  - `validate-tdd-docs.ps1`: PASS (0 violations, 100% test pass).

