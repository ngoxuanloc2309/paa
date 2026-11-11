

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
