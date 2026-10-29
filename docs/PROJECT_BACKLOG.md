# SimplePLC Studio - Product Engineering Backlog

Tài liệu quản lý Backlog kỹ thuật chuẩn hóa giao diện, trải nghiệm nhập liệu và tính năng công nghiệp cho **SimplePLC Studio**.

---

## 1. Tổng quan Sprints & Ma trận Backlog

| Mã Task | Tên Task / Hạng mục | Component | Độ ưu tiên | Người thực hiện (Assignee) | Effort (Story Points) | Ước tính (Estimate) | Trạng thái (Status) |
| :--- | :--- | :--- | :---: | :--- | :---: | :---: | :---: |
| **`SPLC-TASK-001`** | Smart Type-Safe Input & Data Normalization | Studio Core / Input UX | **P0** | Senior WPF Dev | 5 SP | 8h (1 ngày) | **Ready for Dev** |
| **`SPLC-TASK-002`** | Canvas Wire Auto-Binding & Tag Visualization | Logic Editor / FBD Engine | **P0** | Senior Canvas Dev | 5 SP | 8h (1 ngày) | **Ready for Dev** |
| **`SPLC-TASK-003`** | Commercial Icon System (Lucide Vector - MIT) | Styles / Resources | **P1** | UI/UX & WPF Dev | 3 SP | 6h (0.75 ngày) | **Ready for Dev** |
| **`SPLC-TASK-004`** | Toolbox & Block Naming Standardization | Localization / UI | **P1** | System Analyst / Dev | 2 SP | 3h (0.5 ngày) | **Ready for Dev** |
| **`SPLC-TASK-005`** | Live Watch UI Streamlining & Performance Polish | Live Monitoring View | **P1** | WPF UI Dev | 2 SP | 4h (0.5 ngày) | **Ready for Dev** |
| **`SPLC-TASK-006`** | Modbus Slave Architecture Revamp & Native Map | Protocol / Modbus Dialog | **P2** | Fullstack / Protocol Dev | 8 SP | 12h (1.5 ngày) | **✅ Done** |

---

## 2. Chi tiết từng Backlog Item

---

### 🟢 `SPLC-TASK-001`: Smart Type-Safe Input & Data Normalization
* **Mô tả (User Story):**
  > *Là kỹ sư lập trình PLC, tôi muốn hệ thống nhập liệu thông minh tự nhận biết kiểu dữ liệu của Tag (Bool/Int/Float) và tự chuẩn hóa định dạng (ví dụ nhập `25` thành `25.0`, nhập `1`/`true` thành `1`), để không bị báo lỗi cứng nhắc và thao tác nhanh chóng, an toàn.*
* **Phạm vi tác động (Scope):**
  * Inspector Panel: `ActionParam`, `ThresholdLo`, `ThresholdHi`, `Scale` parameters.
  * `RuleTableView`: Cột `Action Param`, `Trigger Param`.
  * `LiveWatchView`: Ô điều khiển cưỡng bức `Manual Force Value`.
  * `SimulatorView`: Giá trị điều chỉnh thanh trượt/nút nhấn.
* **Chi tiết kỹ thuật (Technical Spec):**
  1. Xây dựng tiện ích `SmartInputParser` xử lý 3 kiểu dữ liệu:
     * **BOOL:** Chỉ nhận `0` và `1`. Hỗ trợ nhập `true`, `false`, `on`, `off`, `bật`, `tắt` $\rightarrow$ quy về `0` hoặc `1`. Cung cấp 2 nút bấm một chạm `[ 0 ]` `[ 1 ]`.
     * **REAL (Float32):** Nhập `25` $\rightarrow$ tự format `25.0`. Chấp nhận cả dấu phẩy `25,5` $\rightarrow$ `25.5`.
     * **INT32:** Tự động cắt khoảng trắng, ép kiểu số nguyên an toàn, nhập `25.0` $\rightarrow$ `25`.
  2. Đồng bộ hiển thị: Bỏ chuỗi dài `BẬT (1)`, `TẮT (0)`, `ON`, `OFF` trong các bảng danh sách, chuẩn hóa về số nhị phân `0`/`1`.
* **Tiêu chí nghiệm thu (Acceptance Criteria - DoD):**
  * [ ] Khi Tag là `VFLAG` hoặc `DO`, ô *Action Parameter* không cho nhập số khác ngoài `0` hoặc `1`; có nút bấm `[ 0 ]` `[ 1 ]`.
  * [ ] Khi Tag là `REAL` (AI, Float), gõ `25` hoặc `25,0` tự chuyển thành `25.0` khi rời chuột (Focus Lost) hoặc Enter, không báo lỗi.
  * [ ] Mọi bảng hiển thị giá trị Tag (Live Watch, Simulator) đều hiển thị nhất quán `0`/`1` cho bool và format số thực cho float.
* **Effort:** 5 Story Points | **Estimate:** 8 giờ.

---

### 🟢 `SPLC-TASK-002`: Canvas Wire Auto-Binding & In-Place Tag Visualization
* **Mô tả (User Story):**
  > *Là người vẽ sơ đồ logic FBD, khi tôi kéo dây nối từ một khối Tag đầu vào (hoặc Scale) cắm vào chân của khối khác (như Compare, Trigger, Scale), tôi muốn khối đích tự động nhận diện Tag đó và cập nhật ngay tên biến lên mặt khối, không bắt tôi phải chọn lại thủ công.*
* **Phạm vi tác động (Scope):**
  * `SimplePLC.Studio\ViewModels\LogicEditorViewModel.cs` (hàm `Connect` và `RemoveConnection`).
  * `ComparatorNodeViewModel.cs`, `TriggerNodeViewModel.cs`, `ScaleNodeViewModel.cs`.
  * `SimplePLC.Studio\Styles\NodeTemplates.xaml`.
* **Chi tiết kỹ thuật (Technical Spec):**
  1. **Khối So sánh (`Comparator`):**
     * Trong `Connect()`: Khi chân `In` nhận dây từ `InputNodeViewModel` hoặc `ScaleNodeViewModel` $\rightarrow$ gán `cmp.InputTag = sourceTag`.
     * Tự động cập nhật công thức hiển thị trên khối: ví dụ `AI0 > 50.0` (hoặc `Nhiệt độ > 50.0`).
     * Tự động khóa / hiển thị badge `🔗 Connected: AI0` trên bảng Inspector bên phải.
  2. **Khối Kích hoạt (`Trigger`):**
     * Khi nối dây từ `InputNode` vào chân `In` $\rightarrow$ gán `trigger.InputTag = inputNode.Tag`.
     * Mặt khối hiển thị rõ ràng: `DI0: Sườn lên (0 → 1)` thay vì chỉ hiện dòng chữ sườn lên đơn thuần.
  3. **Khối Tuyến tính (`Scale`):**
     * Thay chữ `x` trong công thức bằng tên Tag thật: `y = 0.01 · AI0 + 0 (bar)`.
     * Nối chân `Out` sang Action $\rightarrow$ tự nhận `OutputTag`.
  4. **Khối `Modbus Write`:**
     * Nối dây vào các Pin dữ liệu $\rightarrow$ Pin tự động liên kết với Tag đầu vào.
  5. **Ngắt kết nối (`RemoveConnection`):**
     * Reset các Tag liên kết về null, trả lại nhãn chờ kết nối mặc định (`IN`).
* **Tiêu chí nghiệm thu (Acceptance Criteria - DoD):**
  * [ ] Kéo dây từ `AI0` vào chân `In` của khối `CMP` $\rightarrow$ mặt khối CMP lập tức đổi thành `AI0 > 50.0`, không cần chọn thủ công ở Inspector.
  * [ ] Kéo dây từ `DI0` vào chân `In` của `Trigger` $\rightarrow$ khối Trigger hiển thị `DI0: Sườn lên (0 → 1)`.
  * [ ] Xóa dây nối $\rightarrow$ khối tự reset về nhãn mặc định.
* **Effort:** 5 Story Points | **Estimate:** 8 giờ.

---

### 🟢 `SPLC-TASK-003`: Commercial Icon System (Lucide Vector - MIT License)
* **Mô tả (User Story):**
  > *Là nhà phát triển phần mềm thương mại, tôi muốn SimplePLC Studio sử dụng 100% icon vector đạt chuẩn quốc tế và cấp phép MIT License miễn phí thương mại, thay thế toàn bộ ký tự Emoji để đảm bảo tính thẩm mỹ công nghiệp và an toàn bản quyền.*
* **Phạm vi tác động (Scope):**
  * `SimplePLC.Studio\Styles\Icons.xaml` (tạo mới/chuẩn hóa).
  * `LogicEditorView.xaml`, `NodeTemplates.xaml`, `MainWindow.xaml`, `RuleTableView.xaml`.
* **Chi tiết kỹ thuật (Technical Spec):**
  1. Tích hợp bộ icon mã nguồn mở **Lucide Icons** (MIT License).
  2. Xây dựng ResourceDictionary `Icons.xaml` chứa các vector `PathGeometry` chuẩn:
     * `IconTimer`, `IconCounter`, `IconLogic`, `IconIoTerminal`, `IconAnalog`, `IconCommNetwork`.
     * `IconSave`, `IconCompile`, `IconDelete`, `IconAdd`, `IconExport`.
  3. Quét và loại bỏ toàn bộ Emoji (`⏳`, `🕒`, `⌛`, `⚡`, `⚙️`, `🔌`, `📦`, `🛡️`, `📐`, `⚖️`) khỏi XAML và ViewModels.
* **Tiêu chí nghiệm thu (Acceptance Criteria - DoD):**
  * [ ] Không còn bất kỳ ký tự Emoji thô nào trong giao diện Toolbox, Node Header, Toolbar và Dialogs.
  * [ ] Tất cả icon hiển thị sắc nét ở mọi độ phân giải màn hình (1080p, 2K, 4K) và đồng bộ theo Theme màu hệ thống.
  * [ ] Bộ icon có giấy phép bản quyền rõ ràng (MIT License), an toàn thương mại hóa.
* **Effort:** 3 Story Points | **Estimate:** 6 giờ.

---

### 🟢 `SPLC-TASK-004`: Toolbox & Block Naming Standardization
* **Mô tả (User Story):**
  > *Là người sử dụng, tôi muốn danh mục Toolbox và tên các khối logic được đặt tên ngắn gọn, chuẩn xác theo quy ước IEC 61131-3, không dùng các tiền tố rườm rà như STANDARD...*
* **Phạm vi tác động (Scope):**
  * `SimplePLC.Studio\Services\LocalizationService.cs`.
  * `SimplePLC.Studio\Views\LogicEditorView.xaml`.
* **Chi tiết kỹ thuật (Technical Spec):**
  1. Cập nhật Localization key cho cả Tiếng Anh và Tiếng Việt:
     * `STANDARD TIMERS` $\rightarrow$ **`Timers`** (`Bộ định thời`)
     * `STANDARD COUNTERS` $\rightarrow$ **`Counters`** (`Bộ đếm`)
     * `LOGIC & INTERLOCKS` $\rightarrow$ **`Logic`** (`Cổng Logic`)
     * `PROCESS I/O & SIGNALS` $\rightarrow$ **`I/O`** (`Ngõ vào / Ra`)
     * `ANALOG SIGNAL PROCESSING` $\rightarrow$ **`Analog`** (`Xử lý Tương tự`)
     * `NETWORK COMMUNICATION (MQTT)` $\rightarrow$ **`Communication`** (`Truyền thông`)
  2. Chuẩn hóa tên khối IEC 61131-3: `TON`, `TOF`, `TP`, `CYCLE`, `CTU`, `CTD`, `CMP`, `SCALE`.
* **Tiêu chí nghiệm thu (Acceptance Criteria - DoD):**
  * [ ] Thanh Toolbox hiển thị đúng tên danh mục tinh gọn cả ở chế độ Tiếng Anh và Tiếng Việt.
* **Effort:** 2 Story Points | **Estimate:** 3 giờ.

---

### 🟢 `SPLC-TASK-005`: Live Watch UI Streamlining & Performance Polish
* **Mô tả (User Story):**
  > *Là kỹ sư vận hành debug hệ thống, tôi muốn màn hình Live Watch rộng rãi, hiển thị nhanh và không bị giật lag bởi các thông tin không cần thiết như thời gian cập nhật mili-giây hay trạng thái Quality.*
* **Phạm vi tác động (Scope):**
  * `SimplePLC.Studio\Views\LiveWatchView.xaml`.
  * `SimplePLC.Studio\ViewModels\LiveWatchViewModel.cs`.
* **Chi tiết kỹ thuật (Technical Spec):**
  1. Lược bỏ hoàn toàn 2 cột trong DataGrid:
     * Cột `Quality` (Độ tin cậy).
     * Cột `Last Time` (Timestamp cập nhật `HH:mm:ss.fff`).
  2. Tái phân bổ độ rộng các cột cốt lõi:
     * Tên biến, Bí danh (Alias), Phân loại (Kind), Kiểu dữ liệu (Type), Địa chỉ Modbus.
     * Cột `Giá trị (Value)`: Hiển thị nổi bật nhị phân `0`/`1` hoặc số thực chuẩn.
     * Cột `Cưỡng bức (Force Control)`: Rộng rãi, dễ thao tác nút bấm `Force 0/1`.
* **Tiêu chí nghiệm thu (Acceptance Criteria - DoD):**
  * [ ] Màn hình Live Watch không còn 2 cột Quality và Last Time.
  * [ ] Bảng hiển thị rộng rãi, không bị co kéo hay xuất hiện thanh cuộn ngang không mong muốn ở độ phân giải tiêu chuẩn.
* **Effort:** 2 Story Points | **Estimate:** 4 giờ.

---

### 🟢 `SPLC-TASK-006`: Modbus Slave Architecture Revamp & Native Memory Map (Hoàn thành - GitHub #11)
* **Mô tả (User Story):**
  > *Là kỹ sư SCADA/HMI, tôi muốn thiết bị Modbus Slave sử dụng bản đồ địa chỉ thanh ghi cố định chuẩn của thiết bị, không bắt cấu hình bảng ánh xạ ảo phức tạp, và có thể xuất bảng địa chỉ ra file CSV để cấu hình SCADA nhanh chóng.*
* **Phạm vi tác động (Scope):**
  * `SimplePLC.Protocol`: `ModbusSlaveFixedMap.cs` (khế ước địa chỉ nhị phân cố định).
  * `SimplePLC.Studio`: `ModbusPortConfigDialogView.xaml`, `ModbusPortConfigViewModel.cs`, `ModbusSlaveFixedRegisterViewModel.cs`, `LocalizationService.cs`.
  * `SimplePLC.Studio.Tests`: `ModbusSlaveMapStudioTests.cs`.
  * `docs/manuals`: `MODBUS_SLAVE_MAPPING_AND_SCADA_INTEGRATION_MANUAL.md`.
* **Chi tiết kỹ thuật đã thực hiện:**
  1. Loại bỏ cấu hình `Inter-frame Delay (ms)` và `Sandbox Memory` đối với vai trò Slave.
  2. Bổ sung các Tab phân vùng bộ nhớ phong cách Moxa (`Tất cả`, `Holding Registers 4xxxx`, `Coils 0xxxx`, `Discrete Inputs 1xxxx`) kèm bộ đếm live.
  3. Bảng DataGrid 5 cột cốt lõi: `Modbus Address (1-based)`, `Device Tag`, `Data Type`, `Access Right`, `Description` (kèm hover Tooltip hiển thị Protocol Wire Offset và Modbus Function Code).
  4. Căn lề trái toàn bộ cột, chuẩn hóa phông chữ hệ thống `Segoe UI` đồng bộ Fluent 2 Desktop.
  5. Xuất tài liệu bảng địa chỉ ra CSV (UTF-8 with BOM) và Markdown không chứa thuật ngữ "PLC".
* **Tiêu chí nghiệm thu (Acceptance Criteria - DoD):**
  * [x] Không còn trường `Inter-frame Delay` và `Sandbox Memory`.
  * [x] Người dùng không phải ánh xạ thủ công từng dòng thanh ghi Slave.
  * [x] Giao diện phân loại Tabs Moxa, bảng 5 cột tối giản, Segoe UI, căn lề trái.
  * [x] 100% tài liệu hướng dẫn và khế ước kỹ thuật được cập nhật.
* **Effort:** 8 Story Points | **Estimate:** 12 giờ.
* **Trạng thái:** **Hoàn thành 100%** (1,625 / 1,625 tests passed).

---

## 3. Tổng kết Kế hoạch Triển khai (Execution Plan)

* **Tổng Effort 5 Task trọng tâm (Sprint 1):** `17 Story Points` (~`29 giờ làm việc`).
* **Thứ tự thực hiện đề xuất:**
  1. **Bước 1:** `SPLC-TASK-004` (Đổi tên Block/Toolbox) & `SPLC-TASK-003` (Chuẩn hóa Icon Lucide MIT) - *Tạo nền tảng giao diện chuẩn*.
  2. **Bước 2:** `SPLC-TASK-001` (Smart Input & Kiểu dữ liệu) - *Xây dựng bộ parser & format dùng chung*.
  3. **Bước 3:** `SPLC-TASK-002` (Auto-Binding & Hiển thị trên Canvas) - *Nâng cấp logic nối dây FBD*.
  4. **Bước 4:** `SPLC-TASK-005` (Tinh gọn Live Watch) - *Tối ưu màn hình giám sát*.
