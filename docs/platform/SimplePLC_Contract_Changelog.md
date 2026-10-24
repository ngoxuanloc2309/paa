# SimplePLC Platform Contract Changelog

## [2.11.2 / Studio v2.11.2] - 2026-10-24 (MQTT PUB COMMIT VALIDATION, AUTO-GENERATED CUSTOM TEMPLATE & TAG DATA TYPE REGISTRY 0x0080 SYNC)

### Added & Fixed (Firmware MCU & App Studio Alignment)
- **1. Đồng Bộ Bảng Kiểu Dữ Liệu Tag Registry (`0x0080..0x00BF`, 128 Tags x 1 Byte, SPLC-SPEC-IEC-002)**:
  - Tự động nạp bảng kiểu dữ liệu (`1=BOOL, 2=INT32, 3=REAL`) từ Tag Catalog trước khi thực hiện COMMIT.
  - Tự động nhận diện các chân kiểu `Real` từ khối `MB_READ` và đánh dấu kiểu `REAL (3)` cho tag đích (như tag 40, 72), khắc phục triệt để hiện tượng in số thực dưới dạng số nguyên raw bit (như 1130823396 thay vì 229.5).
- **2. Tự Động Sinh Template Chuẩn Trong Chế Độ Visual Fields / JSON**:
  - Studio tự động sinh Custom Template chuẩn từ danh sách Key và ánh xạ tới các token `{val1}`..`{val8}` tương ứng với `bound_tags[0..7]`:
    `{"device":"{device}","ts":{ts},"values":{"v_avg":{val1},"i_avg":{val2}}}`
    và chuyển cờ nạp xuống thiết bị thành `PayloadMode = 2 (Custom)`.
  - Giúp broker nhận được JSON có đầy đủ tên thuộc tính đúng như Payload Preview mà không đòi hỏi MCU phải lưu trữ chuỗi tên trường.
  - Chặn các token tùy biến không hợp lệ trong chế độ nhập tay, cảnh báo người dùng ngay trên giao diện.
- **3. Bắt Lỗi Commit Trung Thực & Xác Thực Phiên Bản (`ActiveRuleVersion 0xA001`)**:
  - Khắc phục lỗi false positive (Studio báo thành công dù MCU từ chối commit do sai cú pháp template).
  - Đọc phiên bản active ban đầu tại `0xA001`, bắt lập tức trạng thái `CONFIG_STATUS == 4 (ERROR)` và hiển thị mã lỗi chi tiết từ `0xA003 (CONFIG_ERROR)`.
  - Chỉ công nhận Deploy thành công khi `CONFIG_STATUS == 3 (READY)` VÀ `ActiveRuleVersion` thực sự tăng lên.
- **4. Kiểm Thử Tự Động (Verification & Test Coverage)**:
  - Bổ sung bộ kiểm thử `RuleCompiler_VisualFieldsMode_GeneratesCustomTemplateWithValPlaceholders` và xác thực xử lý lỗi commit.
  - Vượt qua 100% toàn bộ 1,489 bài kiểm thử tự động trên toàn giải pháp.

---

## [2.11.1 / Studio v2.11.1] - 2026-10-24 (MODBUS CONFIG PERSISTENCE, DIALOG ERGONOMICS, VECTOR REMOVE ICONS & PIN DISPLAY SIMPLIFICATION)

### Added & Standardized
- **1. Bảo Toàn Cấu Hình Modbus Master Khi Tải/Đổi Cổng RS-485 (`ModbusReadConfigDialogViewModel`, `ModbusWriteConfigDialogViewModel`)**:
  - Triển khai cơ chế đồng bộ in-place (`RefreshAvailablePorts`): Cập nhật danh sách cổng mà không xóa/gán lại toàn bộ collection, bảo toàn 100% `PortIndex`, `ReadMode`, `FunctionCode`, `TriggerMode`.
  - Khắc phục hoàn toàn tình trạng mất dữ liệu cấu hình và viền đỏ validation ComboBox khi mở lại hộp thoại cấu hình cổng.
- **2. Chuẩn Hóa Cửa Sổ Cấu Hình & Công Thái Học HMI (ISA-101 / Slate Gray Ergonomics)**:
  - Loại bỏ hoàn toàn nút Minimize và Maximize (`ResizeMode="NoResize"`) trên toàn bộ 5 hộp thoại cấu hình (`ModbusReadConfigDialogView`, `ModbusWriteConfigDialogView`, `MqttConfigDialogView`, `MqttSubConfigDialogView`, `DirectWriteConfigDialogView`), chỉ giữ lại duy nhất nút đóng **[X]**.
  - Thiết kế `TableRowDeleteBtnStyle` chuẩn kích thước `25x25px`, vector `Path` đối xứng 4 chiều (`M 3,3 L 13,13 M 13,3 L 3,13`) nằm chính giữa `Canvas (16x16)` & `Viewbox (9x9)`, triệt tiêu hiện tượng lệch subpixel và méo viền nút xóa.
  - Đồng bộ chiều cao toàn bộ các điều khiển trên dòng cấu hình (`Height="25"` cho TextBox, ComboBox, Type Border, Remove Button), loại bỏ rung giật baseline khi hiển thị.
- **3. Tối Giản Nhãn Chân Khối Modbus FBD (Nodify Canvas Clarity)**:
  - Đơn giản hóa `DisplayTitle` và `Connector.Title` của `ModbusPinBindingViewModel` chỉ hiển thị nhãn ngắn gọn của Pin (ví dụ: `Total_kWh`), triệt tiêu xung đột va chạm chữ với các tham số khối ở cột trái trên Canvas.
  - Lưu giữ thông tin thanh ghi chi tiết (`offset +58`, `VREG_RETAIN...`) trong tooltip `Connector.Description` đa ngữ.
- **4. Tách Bạch Khối Đọc Modbus và Khối Phát MQTT Qua Thanh Ghi PLC**:
  - Chuẩn hóa kiến trúc kết nối: `MB_READ` đưa trực tiếp dữ liệu vào các thanh ghi PLC (`VREG`, `VREG_RETAIN`), `MQTT_PUB` trỏ trực tiếp đến thanh ghi trong bảng cấu hình mà không cần dây nối bắc cầu phức tạp.
- **5. Kiểm Thử & Kiểm Định Độ Tin Cậy (Verification & 100% Pass Rate)**:
  - Bổ sung bộ kiểm thử `DialogStylingAndWindowResizeTests` và `ModbusReadNodeViewModelTests`.
  - Khắc phục race condition trong stress test `F3_02_Unplug_During_ActivePolling_TerminatesLoop_RecoversOnReplug`.
  - Vượt qua toàn bộ 1,483 / 1,483 test case (0 Failed) trên toàn bộ 5 dự án test.

---

## [2.12.0 / Studio v2.8.0] - 2026-10-23 (WIRE PROTOCOL V2.0 CONTRACT HARMONIZATION: TAG REGISTRY 0x0080, ORTHOGONAL RULE FLAGS & MB_WRITE HARDWARE LIMITS)

### Added & Standardized (Firmware MCU & App Studio Alignment)
- **1. Bảng Kiểu Dữ Liệu Tag (`0x0080..0x00BF`) & Bit Capability `SPLC_CAP_FLOAT32` (0x0800)**:
  - Khởi tạo chính thức bit capability phần cứng `SPLC_CAP_FLOAT32 = 0x0800` (Bit 11) trong thanh ghi `0x0A08` (`simpleplc_protocol_v2_0.h`, `DeviceEnums.cs`).
  - Hiện thực hóa hàm mã hóa/giải mã bảng kiểu tag `RegisterCodec.EncodeTagDataTypes` và `DecodeTagDataTypes` (64 registers = 128 bytes, 2 tags/reg: High Byte = Tag $2k$, Low Byte = Tag $2k+1$).
  - Tích hợp bước nạp an toàn bảng `0x0080..0x00BF` qua `ModbusChunkPlanner` trong `RuleTableWriter.DeployRulesAsync` trước khi kích hoạt atomic commit `0xA000 = 0xA5A5`.
  - Mặc định byte `0 = SPLC_DATA_TYPE_NONE` được coi là `INT32` tương thích ngược 100%.
- **2. Chuẩn Hóa Ranh Giới Giới Hạn Khung Tin `MB_WRITE` (ICD MBM-002 Alignment)**:
  - Hiệu chỉnh tài liệu ICD MBM-002: Direct Mode giới hạn `1..16` coils/registers (khớp chính xác với mảng vật lý `direct_pins[4]` và bộ đệm RTOS MCU); Channel-Indexed Mode giới hạn `1..64` kênh.
  - Bổ sung chốt an toàn xác thực 2 cấp: Chặn ngay tại UI Dialog (`ModbusWriteConfigDialogViewModel.cs`) và chặn tại `RuleCompiler.cs` với chẩn đoán lỗi `ERR_MB_WRITE_DIRECT_LIMIT` / `ERR_MB_WRITE_CHANNEL_LIMIT`.
  - Xác nhận dùng chung bit capability `SPLC_CAP_MODBUS_MASTER = 0x0100` cho cả `MB_READ` và `MB_WRITE`.
- **3. Phân Tách Trực Giao Cờ `flags` Rule (Orthogonal Rule Flags - Khắc Phục Lỗi Ép Kiểu IEEE-754)**:
  - Tách trường `flags` (Byte 13 High trong `SPLC_RuleRecord_t`) thành 2 cờ độc lập:
    * `Bit 0 (0x01)`: `SPLC_RULE_FLAG_TRIGGER_FLOAT` (Quy định ngưỡng `threshold_lo` và `threshold_hi` là số thực Float32 IEEE-754).
    * `Bit 1 (0x02)`: `SPLC_RULE_FLAG_ACTION_FLOAT` (Quy định tham số tác động `action_param` là số thực Float32 IEEE-754).
    * `0x03`: `SPLC_RULE_FLAG_FLOAT_DATA` (Legacy alias khi cả Trigger và Action đều là Float32).
  - Khắc phục triệt để lỗi ép kiểu sai số nhị phân khi Trigger là số nguyên INT32 (ví dụ `AI > 80`) nhưng Action là số thực REAL (ví dụ `SET VREG_REAL = 2.5f`), đảm bảo MCU so sánh nguyên chính xác và ghi đúng giá trị float.
  - Chuẩn hóa: `ACT_SCALE_TAG` dùng `flags = 0x03`, `INC_COUNTER` chỉ hỗ trợ số nguyên, `ADD_TAG` hỗ trợ số thực khi `flags & 0x02`.
- **4. Độ Tin Cậy & Bộ Kiểm Thử (Verification & Testing)**:
  - Bổ sung kiểm thử `RegisterCodecTests` cho bảng `0x0080` và cờ trực giao `SPLC_RuleFlags`.
  - Bổ sung kiểm thử `RuleMapperTests` cho mixed rule (Trigger Int + Action Float).
  - Bổ sung kiểm thử `McuReferenceSimulatorTests` đọc/ghi `0x0080` và quét chu kỳ thực tế với mixed rule.
  - Toàn bộ 1,446 unit tests trên cả 5 project test suite (`Protocol`, `Application`, `Domain`, `Infrastructure`, `Studio`) đều vượt qua 100%.

---

## [2.11.0 / Studio v2.7.0] - 2026-10-23 (MULTI-SUBSYSTEM RULE TABLE HUB, LEFT-ALIGNED INDUSTRIAL ERGONOMICS & TWO-WAY CANVAS DELETION SYNC)

### Added & Standardized
- **Trung Tâm Quản Trị Luật & Phân Hệ Đa Dịch Vụ 4 Sub-Tabs (`RuleTableView.xaml`, `RuleTableViewModel.cs`)**:
  - Tái cấu trúc màn hình Rule Table thành trung tâm chỉ huy 4 Sub-Tabs chuyên biệt:
    1. **SubTab 0 (Logic Rules)**: Tập luật điều khiển tự động hóa (Trigger - Guard - Action), bộ lọc Status Pills (All / Active / Disabled), tìm kiếm thời gian thực, thẻ thanh tra giải mã bytecode 32-byte.
    2. **SubTab 1 (Modbus Master)**: Tổng hợp toàn bộ lệnh `MB_READ`/`MB_WRITE` từ Canvas (FC01/FC02/FC03/FC04/FC05/FC06/FC15/FC16), phân định cổng RS-485 Port 1..4, Slave ID, Địa chỉ thanh ghi (Hex/Dec), Độ dài, Chế độ đọc/ghi (Periodic/Event), Chân/VREG đích.
    3. **SubTab 2 (MQTT Services)**: Tổng hợp toàn bộ dịch vụ viễn trắc `MQTT_PUB` và điều khiển `MQTT_SUB`, Topic URI, Cấp độ QoS (0/1/2), Payload Summary và trạng thái kết nối.
    4. **SubTab 3 (Function Blocks)**: Tổng hợp các khối hàm chuẩn IEC 61131-3 (Bộ định thời TON/TOF/TP/CYCLE, Bộ đếm CTU/CTD, Khối chuẩn hóa Scale), thông số Preset ($PT$, $PV$), Tín hiệu vào/ra.
- **Cơ Chế Đồng Bộ & Xóa Hai Chiều (Bidirectional Deletion & Canvas Sync)**:
  - Bổ sung lệnh xóa và callback điều phối: `DeleteModbusCommandCommand`, `DeleteMqttServiceCommand`, `DeleteFunctionBlockCommand`.
  - Khi xóa một dòng lệnh từ bất kỳ Sub-Tab nào trong Rule Table, hệ thống tự động tìm và xóa node tương ứng trên FBD Canvas, ngắt các dây kết nối liên quan, dọn dẹp Tag Catalog và cập nhật lại bộ nhớ định mức.
  - Tách bạch cấu hình logic và phần cứng: Các thông số vật lý (Baud rate cổng RS-485, Wi-Fi credentials, MQTT Broker IP) được liên kết trực tiếp tới hộp thoại Device Settings.
- **Chuẩn Hóa Công Thái Học & Thẩm Mỹ Công Nghiệp (ISA-101 / Slate Gray Ergonomics)**:
  - **Căn lề trái thẳng hàng tuyệt đối (Left-Aligned Precision)**: Toàn bộ Column Headers và Text Cells trong các bảng đều được căn lề trái chuẩn xác với lề thụt `Margin="10,0"` khớp với `Padding="10,8"` của Header, khắc phục hoàn toàn hiện tượng lệch hàng ziczac.
  - **Triệt tiêu ô Selection dư thừa (Hidden RowHeader)**: Cấu hình `HeadersVisibility="Column"` và ẩn triệt để `DataGridRowHeader (Width=0, Visibility=Collapsed)` trên toàn bộ hệ thống Studio.
  - **Chuẩn hóa cột Action đồng bộ (Uniform 120px Action Column)**: Cột Action trên cả 4 Sub-Tabs đều có độ rộng cố định `120px` với 2 nút icon xúc giác nhỏ gọn: `[ ✏️ ]` (*View on Canvas*) và `[ 🗑️ ]` (*Delete*) kèm Tooltip đa ngữ đầy đủ.
- **Hỗ Trợ Song Ngữ Toàn Diện (Full Bilingual i18n Vi/En)**:
  - Bản địa hóa 100% các tiêu đề Sub-Tab, tiêu đề cột bảng dữ liệu, thanh thông báo liên kết phần cứng và trạng thái trống (Empty State Cards).
- **Độ Tin Cậy & Bộ Kiểm Thử (Verification & Testing)**:
  - Bổ sung unit test `Test_RuleTableViewModel_DeleteSubsystemItems_RemovesFromCollectionAndFiresCallback` trong `SimplePLC.Studio.Tests`.
  - Bộ kiểm thử 827/827 tests vượt qua 100%, biên dịch sạch 0 cảnh báo, 0 lỗi.

---

## [v2.0.1 / Studio v2.6.1] - 2026-10-22
### Added
- **Cycle Timer Function Block (`REPEAT_CYCLE` / `SPLC_TimerMode.CYCLE = 4`)**:
  - Khối định thời chu kỳ lặp lại tuần hoàn với 2 thông số thời gian độc lập $T_{ON}$ (Thời gian bật) và $T_{OFF}$ (Thời gian nghỉ).
  - Hỗ trợ lựa chọn pha khởi đầu: Bật trước (`StartWithOn = true`) hoặc Nghỉ trước (`StartWithOn = false`).
  - Cơ chế bảo vệ ngắt an toàn tức thì (Instant Fail-Safe Cutoff): Khi $In = 0$, $Q$ rớt về $0$ ngay lập tức và $ET$ reset về $0$.
  - Compiler Step 2b: Phân rã macro thành 4 candidate rules an toàn kết hợp Stable Topological Sort.
  - Đồng bộ bảng Dedicated Timers MCU (`0x0B00..0x0B3F` mode `CYCLE = 4`) và firmware header `SPLC_TIMER_MODE_CYCLE = 4`.
  - Hỗ trợ song ngữ đầy đủ Tiếng Việt và Tiếng Anh trên UI Studio, Inspector và Natural Logic Narrative.

---

## [2.10.0] - 2026-10-22 (HARDWARE-AWARE RS-485 AUTO-SYNC, MULTI-PORT MODBUS MASTER CONFIGURATION & SAFETY GATES)

### Added & Standardized
- **Thanh Ghi Tự Mô Tả Số Cổng RS-485 Vật Lý (`0x0A0A`, SPLC_ADDR_HW_RS485_PORT_COUNT)**:
  - Bổ sung thanh ghi 16-bit `0x0A0A` (uint16, FC03 RO) vào [`src/SimplePLC.Protocol/Firmware/simpleplc_protocol_v2_0.h`](file:///g:/HoaNV/Projects/SimplePLC/src/SimplePLC.Protocol/Firmware/simpleplc_protocol_v2_0.h) và [`src/SimplePLC.Protocol/Constants/ModbusRegisterMap.cs`](file:///g:/HoaNV/Projects/SimplePLC/src/SimplePLC.Protocol/Constants/ModbusRegisterMap.cs).
  - Phản ánh số lượng cổng RS-485 vật lý thực tế trên vi điều khiển: `0` (None), `1` (Micro), `2` (Standard SimplePLC), `4` (Modular/Gateway).
  - Tự động nhận diện trong bước bắt tay kết nối (Handshake Step 3b); đồng bộ danh sách cổng hiển thị trên Studio, khóa/mờ các cổng không tồn tại, cập nhật ComboBox chọn cổng trên khối FBD.
  - Cơ chế fail-safe fallback: Nếu đọc `0x0A0A` lỗi hoặc timeout trên firmware cũ V1, tự động fallback về 2 cổng nếu có cờ `SPLC_CAP_MODBUS_MASTER (0x0100)` tại `0x0A08`.
- **Phân Vùng Cấu Hình Đa Cổng RS-485 Modbus Master (`0x1400..0x143F`, SPLC-SPEC-MBM-003)**:
  - Quy hoạch 64 thanh ghi nhị phân cố định (128 bytes) cho tối đa 4 cổng RS-485 vật lý (16 thanh ghi = 32 bytes mỗi cổng).
  - Cấu trúc nhị phân C byte-exact `SPLC_ModbusPortConfigRecord_t` `#pragma pack(push, 1)`: Control/Port, BaudRate (uint32 BE), Parity, Framing (Data/Stop bits), Timeout, Delay, Retries, PortName (12B ASCII).
  - Bổ sung codec 2 chiều `ModbusPortConfigCodec.cs` mã hóa/giải mã toàn vẹn 64 thanh ghi.
- **Hai Tầng Chốt An Toàn Chặn Lỗi Vượt Cổng (Safety Gates)**:
  - **Studio Compiler Gate (`RuleCompiler.cs`)**: Quét mọi khối `MB_READ`/`MB_WRITE`. Nếu `PortIndex >= physicalRs485PortCount`, compiler ngắt biên dịch và báo lỗi chặn `DiagnosticSeverity.Error`.
  - **Deploy Gate (`RuleTableWriter.cs`)**: Chặn nạp và trả về `SPLC_ErrorCode.UNSUPPORTED (0x0002)` nếu dự án kích hoạt cổng vượt quá phần cứng thực tế.
- **Tiêu Chí Nghiệm Thu Firmware Mới (CHKL-22)**:
  - Bổ sung `CHKL-22: RS-485 Physical Port Count & Multi-Port Config Conformance` vào cả Master Spec ([`docs/platform/SimplePLC_App_MCU_Structs_v2.0_Self_Describing_Profile.md`](file:///g:/HoaNV/Projects/SimplePLC/docs/platform/SimplePLC_App_MCU_Structs_v2.0_Self_Describing_Profile.md)) và MCU Conformance Spec ([`docs/firmware/MCU_CONFORMANCE_SPECIFICATION_V2_0.md`](file:///g:/HoaNV/Projects/SimplePLC/docs/firmware/MCU_CONFORMANCE_SPECIFICATION_V2_0.md)).

## [2.9.0] - 2026-10-21 (REAL / FLOAT32 SPECIFICATION STANDARDIZATION, TAG DATA TYPE REGISTRY & FIRMWARE CONFORMANCE SYNC)

### Added & Standardized
- **Bảng Khai Báo Kiểu Dữ Liệu Tag (Tag DataType Registry: `0x0080..0x00BF`)**:
  - Bổ sung phân vùng 64 thanh ghi Modbus (128 bytes) tại địa chỉ `0x0080` (FC03/FC16 R/W) vào [`src/SimplePLC.Protocol/Firmware/simpleplc_protocol_v2_0.h`](file:///g:/HoaNV/Projects/SimplePLC/src/SimplePLC.Protocol/Firmware/simpleplc_protocol_v2_0.h) (`SPLC_ADDR_TAG_DATA_TYPES`) và [`src/SimplePLC.Protocol/Constants/ModbusRegisterMap.cs`](file:///g:/HoaNV/Projects/SimplePLC/src/SimplePLC.Protocol/Constants/ModbusRegisterMap.cs) (`TagDataTypesBaseAddress`).
  - Mỗi Tag trong Tag Catalog được biểu diễn bởi 1 byte theo enum `SPLC_TagDataType_t`: `1 = BOOL`, `2 = INT32`, `3 = REAL`. Đóng gói 2 tags/register (High Byte: Tag $2k$, Low Byte: Tag $2k+1$).
  - Host Studio ghi mảng này xuống `0x0080` khi nạp cấu hình (Deploy) để MCU nhận biết chính xác kiểu dữ liệu tự nhiên của từng Tag.
- **Quy Chuẩn Xử Lý Số Thực `REAL` (Float32 IEEE-754) Bắt Buộc Cho Firmware MCU**:
  - **Rule Engine**: Khi cờ `rule.flags & 0x01 != 0` (`SPLC_RULE_FLAG_FLOAT_DATA`), MCU bắt buộc thực hiện so sánh số thực Float32 IEEE-754 với sai số $\epsilon = 10^{-5}$ đối với các phép so sánh `=`, `!=`, `>`, `<`, `>=`, `<=`, `BETWEEN`. Tuyệt đối không từ chối nạp hay trả Exception `0x03`.
  - **MB_READ (Modbus Master Poller)**:
    - Nếu chân đọc cấu hình `DATA_TYPE == 6` (`Real`): MCU đọc float IEEE-754, nhân `multiplier`, ghi thẳng vào `g_tag_store[target].as_real`.
    - Nếu chân đọc là số nguyên nhưng `multiplier` có phần lẻ (ví dụ: `0.1`): Tra cứu bảng `0x0080`: nếu Tag đích là `REAL` $\to$ ghi float `as_real` ($25.3$); nếu Tag đích là `INT32` $\to$ làm tròn số nguyên gần nhất `(int32_t)roundf(raw * multiplier)` ($25$).
  - **MQTT Telemetry (`0x0C00`)**: Tra cứu bảng `0x0080`: Tag `REAL` xuất định dạng `snprintf(buf, len, "%.4g", tag.as_real)`; Tag `INT32` xuất `%ld`; Tag `BOOL` xuất `0/1`.
- **Đồng Bộ Hoàn Toàn Các Điểm Lệch Kỹ Thuật Giữa ICD & Header C**:
  - **`SPLC_WifiConfig_t` (128 bytes)**: Loại bỏ triệt để trường `reserved_flags` khỏi mọi tài liệu ([`docs/firmware/SPLC_SPEC_WIFI_004_ICD.md`](file:///g:/HoaNV/Projects/SimplePLC/docs/firmware/SPLC_SPEC_WIFI_004_ICD.md) và [`docs/platform/SimplePLC_App_MCU_Structs_v2.0_Self_Describing_Profile.md`](file:///g:/HoaNV/Projects/SimplePLC/docs/platform/SimplePLC_App_MCU_Structs_v2.0_Self_Describing_Profile.md)), đảm bảo kích thước struct đúng 128 bytes (64 registers `0x1300..0x133F`) và vượt qua `_Static_assert` trên GCC C11.
  - **`pwm_freq_hz`**: Thống nhất thứ tự byte **High Word trước (Big-Endian)**, Reg 4 = High Word, Reg 5 = Low Word, khớp 100% giữa ICD và Conformance Spec.
  - **`MB_WRITE`**: Đồng bộ struct trong ICD với header C: khai báo đầy đủ các tag index trạng thái `trig_tag_index` (+11), `done_tag_index` (+12), `busy_tag_index` (+13), `error_tag_index` (+14), `err_code_tag_index` (+15) kèm giá trị sentinel `0xFFFF` khi không gán.

## [2.8.1] - 2026-10-19 (PULSE_OUT STANDARDIZATION, MULTI-SUBSYSTEM DEPLOYMENT SEQUENCE & FIRMWARE CONFORMANCE)

### Added & Standardized
- **Chuẩn Hóa Khối `PULSE_OUT` (Pulse Output / Bộ Phát Xung Phần Cứng)**:
  - Chuẩn hóa tên gọi khối logic từ `DIRECT_WRITE` sang `PULSE_OUT`, huy hiệu Canvas đổi sang **`PULSE`** (thay cho `DIR_WR` bị tràn mép).
  - Loại bỏ hoàn toàn thuật ngữ "cọc", thay bằng "chân ngõ ra số phần cứng" / "ngõ ra số vật lý DO0..DO7".
  - Bổ sung typedef aliases trong [`src/SimplePLC.Protocol/Firmware/simpleplc_protocol_v2_0.h`](file:///g:/HoaNV/Projects/SimplePLC/src/SimplePLC.Protocol/Firmware/simpleplc_protocol_v2_0.h):
    `SPLC_ADDR_PULSE_OUT`, `SPLC_LEN_PULSE_OUT`, `SPLC_PulsePin_t`, `SPLC_PulseMode_t`, `SPLC_PulseTrigger_t`, `SPLC_PulseOutBlock_t` kèm `_Static_assert` 32 bytes compile-time.
  - Cập nhật tài liệu ICD [`docs/firmware/SPLC_SPEC_DIRECT_WRITE_003_ICD.md`](file:///g:/HoaNV/Projects/SimplePLC/docs/firmware/SPLC_SPEC_DIRECT_WRITE_003_ICD.md) sang mã `SPLC-SPEC-PULSE-003`.
- **Đặc Tả Quy Trình Nạp Cấu Hình Đa Phân Hệ (Multi-Subsystem Staged Deployment Sequence)**:
  - Bổ sung quy trình nạp 6 bước chuẩn vào [`docs/firmware/MCU_CONFORMANCE_SPECIFICATION_V2_0.md`](file:///g:/HoaNV/Projects/SimplePLC/docs/firmware/MCU_CONFORMANCE_SPECIFICATION_V2_0.md) và [`docs/platform/SimplePLC_App_MCU_Structs_v2.0_Self_Describing_Profile.md`](file:///g:/HoaNV/Projects/SimplePLC/docs/platform/SimplePLC_App_MCU_Structs_v2.0_Self_Describing_Profile.md):
    Khảo sát Capabilities (`0x0A08`) $\to$ Ghi Holding Registers ngoại vi (`0x0B00`, `0x0B40`, `0x0E00`, `0x0C00`, `0x0D80`, `0x1000`, `0x1100`, `0x1200`, `0x1300`) $\to$ Nạp Rule Staging (`0x9010`) $\to$ Ghi CRC-16 (`0x9003`) $\to$ Atomic Commit (`0xA000 = 0xA5A5`) $\to$ MCU tráo bảng nguyên tử và xuất log `app_configured=1 generation=N`.
  - Mở rộng bảng tiêu chí nghiệm thu Firmware lên 21 tiêu chí chuẩn (bổ sung CHKL-20 `PULSE_OUT` và CHKL-21 `ST67 Wi-Fi`).
- **Bảo Vệ Tính Toàn Vẹn Hợp Đồng Triển Khai (Deploy Contract Enforcement)**:
  - Loại bỏ Default Interface Methods gây nuốt tham số trong `IRuleTableWriter`, chuyển toàn bộ overloads tiện ích thành static extension methods.
  - Ép buộc `RuleTableGateway` override và chuyển tiếp đầy đủ 10 tham số xuống `RuleTableWriter`.

## [2.8.0] - 2026-10-19 (ST67 WI-FI SUBSYSTEM, WIRELESS ICD & UNIFIED STUDIO CONFIGURATION)

### Added
- **Phân Hệ Truyền Thông Không Dây Wi-Fi ST67 (`0x1300..0x13FF`)**:
  - Định nghĩa vùng nhớ 256 Holding Registers (512 Bytes) phân làm 3 vùng:
    - `0x1300..0x133F`: Cấu hình mạng (SSID, Password, DHCP/Static IP) và Live Telemetry (Status, RSSI, Current IP, MAC).
    - `0x1340..0x134F`: Bắt tay lệnh (`START_SCAN`, `CONNECT`, `DISCONNECT`, `FORGET`) và trạng thái tiến độ quét sóng.
    - `0x1350..0x13FF`: Bảng kết quả tối đa 8 Access Points quét được (22 registers/AP = 44 bytes/AP).
  - Khai báo bit năng lực phần cứng `SPLC_CAP_WIFI = 0x0200` tại thanh ghi `0x0A08`.
  - Bổ sung các cấu trúc C chuẩn pack(1) trong [`src/SimplePLC.Protocol/Firmware/simpleplc_protocol_v2_0.h`](file:///g:/HoaNV/Projects/SimplePLC/src/SimplePLC.Protocol/Firmware/simpleplc_protocol_v2_0.h):
    `SPLC_WifiConfig_t` (128 bytes), `SPLC_WifiScanControl_t` (32 bytes), `SPLC_WifiApInfo_t` (44 bytes), `SPLC_WifiScanTable_t` (352 bytes) kèm `_Static_assert` byte-exact compile-time.
  - Ban hành tài liệu đặc tả ICD hoàn chỉnh: [`docs/firmware/SPLC_SPEC_WIFI_004_ICD.md`](file:///g:/HoaNV/Projects/SimplePLC/docs/firmware/SPLC_SPEC_WIFI_004_ICD.md).
- **Giao Diện Cấu Hình Wi-Fi Tinh Gọn (Unified Industrial Flow)**:
  - Hộp thoại cấu hình 1 trang duy nhất không chia tab, tích hợp luồng: Quét sóng trực tiếp $\to$ Click chọn mạng tự điền SSID $\to$ Nhập mật khẩu $\to$ Lưu & Kết nối.
  - Thẻ Live Telemetry giám sát trạng thái sóng, IP thực tế và MAC theo thời gian thực (Polling 1500ms).

## [2.7.0] - 2026-10-19 (ACTUATION SUITE: MODBUS WRITE & DIRECT WRITE, FAIL-SAFE DEPLOY, INDUSTRIAL SLATE PALETTE & SPEC UNIFICATION)

### Added
- **Phân Hệ Điều Khiển Chấp Hành Modbus Master RTU (`MB_WRITE`, `0x1100..0x11FF`)**:
  - Định nghĩa vùng nhớ cấu hình 4 kênh Modbus Master Write tại `0x1100..0x11FF` (256 Holding Registers = 4 kênh $\times$ 64 thanh ghi = 128 bytes/kênh).
  - Bổ sung cấu trúc chuẩn C firmware `SPLC_ModbusMasterWriteBlock_t` (128 bytes/kênh) trong [`src/SimplePLC.Protocol/Firmware/simpleplc_protocol_v2_0.h`](file:///g:/HoaNV/Projects/SimplePLC/src/SimplePLC.Protocol/Firmware/simpleplc_protocol_v2_0.h).
  - Hỗ trợ 2 chế độ ghi linh hoạt: `Direct Addressing` (ghi cố định giá trị từ các chân logic) và `Channel-Indexed Addressing` (ghi theo kênh động, tính toán địa chỉ `base_addr + channel_idx * offset`).
  - Hỗ trợ 4 hàm ghi Modbus tiêu chuẩn: FC05 (Write Single Coil), FC06 (Write Single Register), FC15 (Write Multiple Coils), FC16 (Write Multiple Registers).
  - 3 chế độ kích hoạt: `Rising Edge (Trig)`, `On Change`, `Cyclic Interval` kèm các chân trạng thái `Done`, `Busy`, `Error`, `ErrCode`.
  - Ban hành tài liệu đặc tả ICD: [`docs/firmware/SPLC_SPEC_MODBUS_MASTER_002_WRITE_ICD.md`](file:///g:/HoaNV/Projects/SimplePLC/docs/firmware/SPLC_SPEC_MODBUS_MASTER_002_WRITE_ICD.md).
- **Phân Hệ Điều Khiển Phần Cứng Cục Bộ (`DIRECT_WRITE`, `0x1200..0x12FF`)**:
  - Hỗ trợ điều khiển trực tiếp 8 cọc đầu ra vật lý `DO0..DO7` theo 4 chế độ: `Digital Output` (0/1), `PWM Duty Cycle` (0..100%), `Pulse Train Output (PTO)` cho động cơ bước/servo, và `Addressable Bus` cho dải LED địa chỉ UCS16703 / WS2812.
  - Ban hành tài liệu đặc tả ICD: [`docs/firmware/SPLC_SPEC_DIRECT_WRITE_003_ICD.md`](file:///g:/HoaNV/Projects/SimplePLC/docs/firmware/SPLC_SPEC_DIRECT_WRITE_003_ICD.md).
- **Phân Hệ Thu Thập Cảm Biến Modbus Master RTU (`MB_READ`, `0x1000..0x10FF`)**:
  - Định nghĩa vùng nhớ cấu hình 4 kênh Modbus Master Read tại `0x1000..0x10FF` (256 Holding Registers = 4 kênh $\times$ 64 thanh ghi = 128 bytes/kênh).
  - Bổ sung cấu trúc chuẩn C firmware `SPLC_ModbusMasterPinBinding_t` (24 bytes/pin, tối đa 4 pins/kênh) và `SPLC_ModbusMasterReadBlock_t` (128 bytes/kênh) trong [`src/SimplePLC.Protocol/Firmware/simpleplc_protocol_v2_0.h`](file:///g:/HoaNV/Projects/SimplePLC/src/SimplePLC.Protocol/Firmware/simpleplc_protocol_v2_0.h).
  - Hỗ trợ đầy đủ 4 hàm đọc tiêu chuẩn Modbus: FC03 (Holding Registers), FC04 (Input Registers), FC01 (Coils), FC02 (Discrete Inputs) và 4 quy ước Endianness.
  - Ban hành tài liệu đặc tả ICD: [`docs/firmware/SPLC_SPEC_MODBUS_MASTER_001_READ_ICD.md`](file:///g:/HoaNV/Projects/SimplePLC/docs/firmware/SPLC_SPEC_MODBUS_MASTER_001_READ_ICD.md).
- **Cơ Chế Khóa An Toàn Khi Nạp Cấu Hình (Fail-Safe Capabilities Deploy Gate)**:
  - Ngăn chặn hoàn toàn lỗi nuốt exception khi đọc thanh ghi `0x0A08` (`HardwareCapabilities`).
  - Tự động kiểm tra và khóa an toàn, báo lỗi tường minh khi nạp sơ đồ logic có cấu hình MQTT/Modbus vào vi điều khiển không hỗ trợ phần cứng tương ứng.
- **Thư Viện Dự Án Mẫu Công Nghiệp Chuẩn Quốc Tế (`examples/`)**:
  - Cung cấp 3 dự án mẫu công nghiệp hoàn chỉnh: `SPX_Sort_To_Light_Logistics_Center.synx`, `IIoT_Cold_Chain_Smart_Gateway.synx`, `Modbus_RTU_Master_VFD_And_Sensor_Loop.synx`.
- **Hệ Thống Kiểm Thử Tự Động Toàn Diện (1.264 / 1.264 Unit Tests + Full UART COM7/COM9 Pass 100%)**:
  - 100% test coverage trên toàn bộ 5 project test (Protocol, Domain, Infrastructure, Application, Studio) và bộ Hardware Automation Test (7/7 steps full, 5/5 diag scenarios, 10/10 mqtt cases).

### Changed
- **Quy Hoạch Tài Liệu Chuẩn Master V2.0 & Dọn Dẹp Bản Nháp**:
  - Hợp nhất toàn bộ bản đồ bộ nhớ, 15 cấu trúc nhị phân và danh mục kiểm tra nghiệm thu vào tài liệu chuẩn duy nhất: [`docs/platform/SimplePLC_App_MCU_Structs_v2.0_Self_Describing_Profile.md`](file:///g:/HoaNV/Projects/SimplePLC/docs/platform/SimplePLC_App_MCU_Structs_v2.0_Self_Describing_Profile.md).
  - Đồng bộ file header C firmware duy nhất: [`src/SimplePLC.Protocol/Firmware/simpleplc_protocol_v2_0.h`](file:///g:/HoaNV/Projects/SimplePLC/src/SimplePLC.Protocol/Firmware/simpleplc_protocol_v2_0.h).
  - Di chuyển các bản đặc tả cũ v1.9 vào `docs/history/` và xóa bỏ các tài liệu phân mảnh trùng lặp.
- **Chuẩn Hóa Bảng Màu Công Nghiệp Toàn Diện (Industrial Slate Gray - ISA-101 / IEC 60073)**:
  - Loại bỏ hoàn toàn mã màu neon web, chuẩn hóa toàn bộ các màn hình Studio, FBD Canvas, NodeTemplates, và các hộp thoại cấu hình sang bảng màu công nghiệp ANSI/ISA-101.01-2015.
- **Chuẩn Hóa Thuật Ngữ Quốc Tế Theo Đặc Tả Modbus.org**:
  - Đồng bộ thuật ngữ chuẩn Modbus: `Starting Address` và `Quantity of Registers` kèm cơ chế Smart Auto-Fit.
- **Đồng Bộ Kích Thước Khối Canvas FBD (185px)**:
  - Căn chỉnh chiều ngang khối `MB_READ`, `MB_WRITE`, `DIRECT_WRITE` đồng bộ pixel hoàn hảo với khối `MQTT PUB`, `MQTT SUB` (185px).

## [2.6.0] - 2026-10-14 (IEC 61131-3 DATA TYPES STANDARDIZATION & NATIVE TELEMETRY WIRE PROFILE)

### Added
- **Chuẩn hóa Kiểu Dữ Liệu Quốc Tế IEC 61131-3 Cho Toàn Bộ Hệ Thống (Host & MCU Firmware)**:
  - Bổ sung định nghĩa chuẩn `SPLC_TagDataType_t` (`SPLC_DATA_TYPE_BOOL = 1`, `SPLC_DATA_TYPE_INT32 = 2`, `SPLC_DATA_TYPE_REAL = 3`) và cấu trúc hợp nhất 32-bit `SPLC_TagValue_t` vào file header C chính thức [`src/SimplePLC.Protocol/Firmware/simpleplc_protocol_v2_0.h`](file:///g:/HoaNV/Projects/SimplePLC/src/SimplePLC.Protocol/Firmware/simpleplc_protocol_v2_0.h).
  - Ánh xạ rõ ràng kiểu dữ liệu theo bản chất phần cứng và logic:
    - **`BOOL` (1-bit)**: Dành riêng cho `DI` (Discrete Input), `DO` (Discrete Output), `VFLAG` (Virtual Flag), `ModbusCoil`.
    - **`DINT` / `INT32` (32-bit Signed Integer)**: Dành cho `AI` (ADC raw 0..4095), `COUNTER`, `VREG` (số nguyên), `VREG_RETAIN` (số nguyên), `ModbusHolding`.
    - **`REAL` / `Float32` (32-bit Floating Point IEEE-754)**: Dành cho `AI` (kênh đo lường vật lý: °C, bar, RPM, kW), `VREG` (kết quả tính toán thực), `VREG_RETAIN` (ngưỡng setpoint thực), `ModbusHolding`.
  - Hỗ trợ người dùng chuyển đổi linh hoạt kiểu dữ liệu `DINT` <-> `REAL` cho tất cả các thanh ghi 32-bit (`AI`, `VREG`, `VREG_RETAIN`, `ModbusHolding`) trực tiếp tại bảng Tag Catalog trong SimplePLC Studio.
- **Chuẩn hóa Giao Thức MQTT Subscribe ICD (SPLC-SPEC-MQTT-002)**:
  - Định nghĩa vùng nhớ cấu hình thuê bao lệnh điều khiển inbound MQTT SUB tại `0x0D80..0x0DFF` (128 thanh ghi = 2 khối $\times$ 64 thanh ghi).
  - Bổ sung cấu trúc chuẩn C firmware `SPLC_MqttSubPinBinding_t` (12 bytes/pin, tối đa 8 pins/block) và `SPLC_MqttSubscriptionBlock_t` (128 bytes/block) trong `simpleplc_protocol_v2_0.h`.
  - Hỗ trợ cơ chế hàng đợi lệnh (`Fifo` vs `LatestWins`) và chính sách an toàn Fail-Safe Timeout (`KeepLastValue`, `SetToDefault`, `ZeroOutput`) khi mất kết nối Broker.
  - Ban hành tài liệu ICD: `docs/firmware/SPLC_SPEC_MQTT_002_SUBSCRIBE_ICD.md`.
- **Giải Pháp Công Nghiệp Đóng Gói SPX Logistics Sort-to-Light**:
  - Ban hành Hồ sơ đề xuất giải pháp kỹ thuật chuẩn quốc tế: `docs/proposals/SPX_Sort_to_Light_Solution_Proposal_V2.docx`.
  - Dự án mẫu `SPX_SortToLight_Standard.synx` tích hợp 2 luồng logic trực giao: Nhận lệnh WMS qua MQTT SUB (Timer TON 5s auto-off) và Cảm biến xác nhận (DI0) gửi viễn trắc MQTT PUB.


### Changed
- **Loại Bỏ Hoàn Toàn Anti-Pattern "Decimals" & Hệ Số Nhân Ảo $10^{dec}$**:
  - Loại bỏ hoàn toàn trường số chữ số lẻ khỏi Tag Catalog và bảng cấu hình MQTT Publish.
  - Bảng cấu hình xuất bản MQTT Telemetry (`0x0C00..0x0D7F`): Chuẩn hóa thanh ghi `Offset +15` thành `reserved_header` (luôn ghi/đọc `0x0000`).
  - Dữ liệu `REAL` được trao đổi trực tiếp trên đường truyền Modbus RTU và bộ nhớ Tag Store (`0x0900..0x09FF`) dưới dạng nhị phân 32-bit IEEE-754 (High Word trước, Low Word sau), đảm bảo độ chính xác tuyệt đối, loại bỏ lỗi làm tròn và đơn giản hóa mã nguồn MCU.
- **Quy Chuẩn Xuất Bản Viễn Trắc Tự Nhiên (Native Telemetry Formatting)**:
  - MCU Firmware phân giải token JSON `{val1}`..`{val8}` / `{val}` / `{vals}` trực tiếp theo kiểu dữ liệu tự nhiên của Tag: `REAL` xuất ra số thực (`%.4g`), `DINT` xuất ra số nguyên (`%ld`), `BOOL` xuất ra boolean (`1`/`0` hoặc `true`/`false`).
- **Nâng Cấp Giao Diện Bảng Tag Catalog Đạt Tiêu Chuẩn Công Nghiệp ISA-101**:
  - Thay thế toàn bộ mã màu neon web bằng bảng màu công nghiệp **Industrial Slate Gray** (`#FFFFFF`, `#D1D5DB`, `#1E293B`, `#E2E8F0`), tương đồng giao diện Siemens TIA Portal V19 / Rockwell Studio 5000.
  - Sửa lỗi crash ứng dụng khi chuyển tab Tag Catalog do sai kiểu enum `KeyboardNavigation.DirectionalNavigation`.
- **Hoàn Thiện Bộ Tài Liệu Kỹ Thuật Firmware & Kiến Trúc**:
  - Ban hành tài liệu đặc tả kiến trúc: [`docs/architecture/SPLC_SPEC_IEC61131_DATA_TYPES_AND_MQTT_STANDARDIZATION.md`](file:///g:/HoaNV/Projects/SimplePLC/docs/architecture/SPLC_SPEC_IEC61131_DATA_TYPES_AND_MQTT_STANDARDIZATION.md).
  - Bổ sung **Mục 13 & Tiêu chí nghiệm thu CHKL-16** trong [`docs/firmware/MCU_CONFORMANCE_SPECIFICATION_V2_0.md`](file:///g:/HoaNV/Projects/SimplePLC/docs/firmware/MCU_CONFORMANCE_SPECIFICATION_V2_0.md).
  - Cập nhật [`docs/firmware/splc_mqtt_wire.h`](file:///g:/HoaNV/Projects/SimplePLC/docs/firmware/splc_mqtt_wire.h) và [`docs/firmware/SPLC_SPEC_MQTT_001_TELEMETRY_ICD.md`](file:///g:/HoaNV/Projects/SimplePLC/docs/firmware/SPLC_SPEC_MQTT_001_TELEMETRY_ICD.md).

## [2.5.0] - 2026-10-13 (PLATFORM V2.0 STANDARDIZATION & MULTI-CLASS MCU GATEWAY CONNECTIVITY)

### Added
- **Hỗ trợ Đa chủng loại Thiết bị V2.0 (Multi-Class MCU Capability Profiles)**:
  - Bổ sung cấu hình nhận dạng và tương thích đầy đủ trong `SupportedDeviceProfile` cho các dòng thiết bị chuẩn V2.0:
    - **Remote I/O (`DeviceClass = 0x0001`)**: `RemoteIo8Di8Do4AiV2_0`, `RemoteIo16Di16DoV2_0`.
    - **Datalogger (`DeviceClass = 0x0002`)**: `Datalogger8AiV2_0`.
    - **IoT Gateway (`DeviceClass = 0x0003`)**: `GatewayRs485EthV2_0` (hỗ trợ đầy đủ kết nối MCU Gateway qua Modbus RTU).
    - **Controller (`DeviceClass = 0x0004`)**: `ControllerV2_0`.
  - Bổ sung các bài kiểm thử kết nối thiết bị tự mô tả độc lập trong `SelfDescribingDeviceConnectionTests` cho tất cả các phân lớp thiết bị (Gateway, Datalogger, Controller).
  - Cung cấp 2 dự án ví dụ mẫu (`examples/`) chuyên sâu cho giao thức IIoT MQTT: `MqttTelemetryPublisherExample.splc` (MQTT Publish định kỳ & theo ngưỡng) và `MqttInboundControlExample.splc` (MQTT Subscribe điều khiển động).

### Changed
- **Chuẩn hóa Toàn bộ Hệ sinh thái thuần V2.0**:
  - Loại bỏ hoàn toàn định dạng và file header cũ `simpleplc_protocol_reference_v1_9.h` để làm sạch mã nguồn.
  - Cập nhật chuẩn giao tiếp: `WireProfile = 2`, `ProtocolVersion = 2`, `RuleFormatVersion = 7`, `FirmwareVersion = 2.0.0`.
  - Cập nhật đồng bộ `ProductDefinition`, `DeviceResourceInfoDto`, `HardwareDevicePreset`, `MainViewModel`, `NewProjectViewModel` và `McuReferenceSimulator`.
  - Cập nhật đặc tả kỹ thuật `MCU_CONFORMANCE_SPECIFICATION_V2_0.md` xác nhận sự tương thích với toàn bộ các dải lớp thiết bị `0x0001..0x0004`.
  - Chuẩn hóa ngôn ngữ thông báo không tiền tố biểu tượng, nhất quán hai ngôn ngữ Tiếng Anh / Tiếng Việt.

## [2.4.0] - 2026-10-13 (MQTT IIOT INBOUND SUBSCRIBE & INDUSTRIAL CANVAS STANDARDIZATION)

### Added
- **Phân hệ MQTT Subscribe & Dynamic Data Extraction**:
  - Bổ sung bộ phân tích cú pháp an toàn thời gian thực `MqttSubscriptionParser` áp dụng nguyên lý **Atomic All-or-Nothing Transaction**: Nếu bất kỳ một trường dữ liệu nào bị thiếu, sai kiểu hoặc vượt ngưỡng an toàn, toàn bộ gói tin bị từ chối 100%, bảo vệ an toàn tuyệt đối cho đầu ra PLC khỏi giá trị rác.
  - Hỗ trợ 4 cấp độ định dạng: Flat JSON tự do, Industrial Standard Envelopes (`values`, `data`, `payload`), Nested dot notation (`sensor.temp`) và Single Raw String.
  - Hỗ trợ cơ chế sinh chân ngõ ra động (Dynamic Output Pins) gắn kết tức thì trên FBD Canvas (`MqttSubscribeNodeViewModel`, `MqttPinBindingViewModel`, `MqttSubConfigDialogView`).
  - Hộp thoại cấu hình `MqttSubConfigDialogView`: Khung kiểm thử Sandbox mở rộng, hỗ trợ test payload trực tiếp, loại bỏ trường Topic lặp và nút Reset thừa thãi.
  - Bổ sung 9 bài kiểm thử đơn vị độc lập tầng Protocol (`MqttSubscriptionParserTests`) và 9 bài kiểm thử ViewModel (`MqttSubscribeNodeViewModelTests`), đạt 100% Pass.

### Changed
- **Chuẩn hóa Toàn diện Công thái học Giao diện FBD Canvas & Inspector cho cả MQTT PUB và SUB**:
  - **Khóa chiều rộng cố định chuẩn công nghiệp `185px`**: Đồng bộ hoàn toàn với các khối chuẩn `[TON]`, `[CTU]`, `[Math]`, loại bỏ triệt để hiện tượng vỡ khổ ngang hoặc lệch khối.
  - **Badge Định Danh Phân Loại**: Bổ sung badge góc trên bên phải `[PUB]` và `[SUB]` với tone màu chuẩn `NodeMqttBrush` (`#006487`).
  - **Thân giữa khối (Center Body)**: Loại bỏ hoàn toàn chuỗi Topic dài bị cắt cụt `...`; thay bằng thông số kỹ thuật ngắn gọn (`PublishModeSummaryText`, `PinsCountSummary`) và **Status Pill** thời gian thực (`● READY`, `● 3 msgs`). Chuỗi Topic được xem trọn vẹn tại Inspector và Tooltip.
  - **Thanh thuộc tính Inspector (Node Properties)**: Đồng bộ Header Banner chuẩn Industrial Slate Gray (`#F1F5F9`), tích hợp cụm nút chọn **Quality of Service (QoS): [QoS 0] [QoS 1] [QoS 2]** cho cả hai chiều, và chuẩn hóa nút mở popup sang `TiaTactileBtnStyle`.
  - **Hộp thoại Cấu hình Publish (`MqttConfigDialogView`)**: Loại bỏ 3 checkbox vụn vặt và nút Reset template gây rủi ro mất dữ liệu, mặc định đóng gói chuẩn IIoT quốc tế (`device`, `ts`, `values`).
  - Toàn bộ 47/47 bài test tự động chuyên sâu liên quan đến MQTT đều vượt qua (100% PASS).

## [2.3.2] - 2026-10-12 (RTC CLOCK & GLOBAL TIMEZONE SYNCHRONIZATION)

### Added / Changed
- **Chính sách An toàn Múi giờ khi Kết nối (On-Connect Read-Only Policy)**:
  - Khi Studio kết nối với bộ điều khiển qua cổng COM/Modbus, hệ thống **chỉ thực hiện FC03 đọc** `0x0810..0x0813` để lấy thời gian và trạng thái phần cứng (`HasHardwareRtc`, `IsBatteryLow`) phục vụ giám sát thanh Status Bar.
  - **Tuyệt đối không tự động ghi đè giờ PC xuống MCU khi vừa kết nối**, bảo vệ nguyên vẹn múi giờ quốc tế (ví dụ: UTC-5, UTC+1, UTC+9) đã cấu hình trước đó của dây chuyền trước rủi ro bị máy tính bảo trì ở Việt Nam (UTC+7) ghi đè.
- **Dịch vụ Đồng bộ Múi giờ Vận hành Tùy chọn (`IRtcClockClient.SyncToCustomTimezoneAsync`)**:
  - Hỗ trợ tính toán chính xác epoch UTC và `tz_offset_min` theo bất kỳ múi giờ nào trong số 141 múi giờ toàn cầu chuẩn Windows/IANA.
  - Tự động áp dụng quy tắc Read-Before-Write để bảo toàn cờ `HW_PRESENT` và `BATTERY_LOW` của MCU.
- **Chuẩn hóa Giao diện Hộp thoại RTC (`RtcSyncDialogView`)**:
  - Loại bỏ hoàn toàn khối nhập tay thủ công (Manual Date/Time Override) nhằm triệt tiêu rủi ro sai lệch múi giờ và loại bỏ thanh cuộn dọc (Zero-Scroll Dialog, chiều cao co giãn tự động ~320px).
  - Tái cấu trúc bảng chọn múi giờ: Mục đầu tiên là giờ máy tính hiện tại kèm tên địa lý đầy đủ, tiếp theo là 141 múi giờ quốc tế sắp xếp tuần tự theo trục kinh độ từ Tây sang Đông (`UTC-12:00` đến `UTC+14:00`).
  - Tích hợp tính năng gõ phím tìm kiếm tức thì (`IsTextSearchEnabled="True"`) và căn giữa hoàn hảo theo chiều dọc.
  - Đồng bộ 100% token kiểu dáng, màu sắc công nghiệp Siemens Petrol Blue (`#006487`), font kỹ thuật `Consolas/Segoe UI`, và style nút `BtnSecondary`, `BtnPrimary`.

## [2.3.1] - 2026-10-10 (TIMER TELEMETRY RULE_REF LINKING)

### Added / Changed
- **Function Block Timer `rule_ref` Binding (`0x0B00 + 8*i`, offset +6)**:
  - Nâng cấp thanh ghi offset `+6` (trước đây là reserved, ghi 0) thành `rule_ref` (R/W, 1-based index `1..100`, ghi `0` nếu unlinked/không dùng). Offset `+7` tiếp tục là reserved (ghi 0).
  - Cập nhật struct `SPLC_FbTimerRecord_t` và `SPLC_PlcTimer_t` trong C header và tài liệu Wire Contract.
  - Cập nhật DTO `FbTimerRecordDto.RuleRef` và codec `FunctionBlockCodec` (Encode/Decode).
  - Cập nhật `RuleCompiler`: tính toán và gán `rule_ref = (vị trí rule đếm giờ trong bảng rule gửi xuống, tính từ 0) + 1` sau khi chốt thứ tự topo cuối cùng của bảng Rule.
  - Phục vụ firmware theo dõi bộ đếm thời gian thực của Rule Engine và phản hồi viễn trắc `ET`, `RUNNING`, `Q`, `IN` về `0x0B00`.

## [2.3.0] - 2026-10-10 (MQTT IIOT GATEWAY & TELEMETRY SUBSYSTEM PROFILE)

### Added
- **MQTT IIoT Gateway & Telemetry Binary Structs Specification (`SPLC-DOC-SPEC-V20-MQTT-STRUCTS`)**:
  - Ban hành tài liệu đặc tả nền tảng chuẩn: [`docs/platform/SimplePLC_App_MCU_Structs_MQTT_IIoT_Profile.md`](file:///g:/HoaNV/Projects/SimplePLC/docs/platform/SimplePLC_App_MCU_Structs_MQTT_IIoT_Profile.md).
  - Phân vùng cấu hình Máy chủ Broker toàn cục (`0x0E00..0x0E5F`, 96 holding registers = 192 bytes): Host/IP (64B), Port, TLS/SSL, Client ID, Username, Password, KeepAlive và 12 registers phản hồi trạng thái Live từ MCU.
  - Phân vùng 4 khối xuất bản Telemetry FBD (`0x0C00..0x0D7F`, 384 holding registers = 768 bytes): Topic (32B), Timing ms, COV Deadband float32, mảng 8 TagIndex và Bản mẫu Custom JSON Payload (128B).
  - Cập nhật Master Memory Map V2.0 trong [`docs/platform/SimplePLC_App_MCU_Structs_v2.0_Self_Describing_Profile.md`](file:///g:/HoaNV/Projects/SimplePLC/docs/platform/SimplePLC_App_MCU_Structs_v2.0_Self_Describing_Profile.md).
  - Hoàn thiện bộ Codec Host C# (`MqttBrokerConfigCodec.cs`, `MqttTelemetryCodec.cs`) và Authoritative C Header [`docs/firmware/splc_mqtt_wire.h`](file:///g:/HoaNV/Projects/SimplePLC/docs/firmware/splc_mqtt_wire.h).
  - Xác thực nghiệm thu tự động 10/10 test cases qua phần cứng ảo UART `COM7` <-> `COM9` và 100% unit tests.

## [2.2.6] - 2026-10-08 (COUNTER STORAGE BINDING, DYNAMIC TAG MAP & SAVE/LOAD FIX)

### Fixed
- **Counter Storage Register (CV) Binding & Telemetry**:
  - Broadened `RetainTagIndex` binding in `RuleCompiler` to accept any valid storage register (`VREG`, `VREG_RETAIN`, `VFLAG`, `COUNTER`) instead of strictly requiring `VREG_RETAIN`.
  - Firmware now correctly resolves the storage tag index and populates `CurrentValue` in FB Counter telemetry (`0x0B40..0x0B7F`), allowing Canvas Counter nodes to reflect real-time count progression.
  - Automatic fallback to dedicated `COUNTER{i}` tag index when CV is unassigned.
- **Project File Save / Load Graph Preservation**:
  - Fixed issue where saved projects opened as blank canvas by ensuring `RuleTable` loads first and nodes are automatically restored from the project graph or compiled rules.
- **Device Info Dialog Card Ordering**:
  - Corrected order to [1] Device Identification -> [2] I/O & Memory Resource Map -> [3] Physical Communication Parameters.

### Changed
- **Dynamic Tag Index Packing (`TagLayoutMap`)**:
  - Consecutive tag layout dynamically packed from MCU `DeviceResourceInfo` (`0x0020`), removing all hardcoded static offsets.
- **Simulator & Hardware Test Automation**:
  - All 748 unit tests passing 100%.
  - Verified 7/7 physical hardware test steps and 5/5 diagnostic scenarios over UART COM7 <-> COM9.

## [2.1.0] - 2026-10-08 (DYNAMIC TAG LAYOUT ARCHITECTURE)

### Changed
- **Dynamic Tag Index Packing (`TagLayoutMap`)**:
  - Removed static hardcoded Base Indices (`DiBaseIndex=0`, `DoBaseIndex=8`, `AiBaseIndex=16`...).
  - Introduced `TagLayoutMap` struct in `SimplePLC.Protocol.Models` and `ModbusRegisterMap.ComputeLayout(...)`.
  - Tag base offsets are now packed consecutively according to the exact number of hardware pins declared by MCU in `DeviceResourceInfo` (`0x0020`):
    - `DiBase = 0`
    - `DoBase = DiCount` (e.g. 4 DI board has `DO0` at index 4 instead of index 8, eliminating empty dummy slots).
    - `AiBase = DiCount + DoCount`
    - `VflagBase = DiCount + DoCount + AiCount`, etc.
  - Studio `TagCatalogViewModel` dynamically rebuilds Tag Catalog and channel assignments upon receiving device profile via `RebuildFromLayout(TagLayoutMap)`.
  - Updated all simulators, emulators, and test suites across all 5 test projects (747/747 passed).

## [2.0.0] - 2026-10-07 (PLATFORM V2.0 RELEASE)

### Added
- **Diagnostic & Commissioning Subsystem (Wire Profile V2)**:
  - Specified `wire_profile = 2` as a strict superset of Wire Profile V1.
  - Added dedicated `Diagnostic Control Block` at `0x0A20..0x0A24` (5 registers) separated from one-shot system commands:
    - `0x0A20`: `DIAG_COMMAND` (WO command mailbox: `ENTER_DIAG=1`, `HEARTBEAT=2`, `EXIT_DIAG=3`, `COMMIT_RETAIN=4`, `DISCARD_RETAIN=5`).
    - `0x0A21`: `DIAG_STATE` (RO observable state: `ENGINE_RUNNING=1`, `DIAG_CONTROL=2`, `TRANSITIONING=3`, `FAULT=4`).
    - `0x0A22`: `DIAG_FLAGS` (RO bitmask: `RETAIN_DIRTY=bit0`, `LEASE_ACTIVE=bit1`).
    - `0x0A23`: `DIAG_LEASE_REMAINING_MS` (RO countdown in ms, default 3000ms watchdog lease).
    - `0x0A24`: `DIAG_ERROR_CODE` (RO diagnostic error code: `NONE=0`, `DENIED_FAULT=1`, `LEASE_EXPIRED=2`, `FLASH_CRC_MISMATCH=3`, `INVALID_COMMAND=4`, `RETAIN_DIRTY=5`).
  - Defined mutual-exclusive Tag Store ownership: `ENGINE_RUNNING` (Rule Engine owns tags; Modbus writes to `0x0900` locked by safety interlock) vs `DIAG_CONTROL` (Studio owns tags; FC16 writes enabled).
- **Dedicated Function Blocks Subsystem (`0x0B00..0x0B7F`)**:
  - `0x0B00..0x0B3F`: 8 Dedicated Timers (TON, TOF, TP, CYCLE), 8 registers each (16 bytes).
  - `0x0B40..0x0B7F`: 8 Dedicated Counters (CTU, CTD, CTUD), 8 registers each (16 bytes).
- **Real-Time Clock Subsystem (RTC Clock `0x0810..0x0813`)**:
  - `epoch_utc_s` (u32), `tz_offset_min` (i16), `rtc_flags` (u16: `SYNCED 0x0001`, `HW_PRESENT 0x0002`, `BATTERY_LOW 0x0004`).
  - Protocol Read-Before-Write: Studio reads `0x0810..0x0813` prior to synchronization, preserving hardware RTC flags and battery status.
- **Hardware Integration Test Plan V2.0**:
  - Expanded test plan with 14 full hardware test cases (`HIT-001` through `HIT-014`).

---

## [1.9.0] - 2026-09-16 (FROZEN RELEASE)

### Added
- **Self-Describing Device Profile (Contract V1.9)**:
  - Added `DeviceResourceInfo` holding register region at `0x0020..0x0029` (10 registers / 20 bytes, FC03).
  - Added `DeviceResourceInfoDto` wire DTO in `SimplePLC.Protocol`.
  - Added `RegisterCodec.EncodeDeviceResourceInfo` and `RegisterCodec.DecodeDeviceResourceInfo`.
  - Added `DeviceProfileMapper` in `SimplePLC.Application` as a pure data adapter.
  - Added dynamic `ProductDefinition` construction in `SimplePLC.Domain.Builders.DeviceProfileBuilder`.
  - Added runtime capability derivation: `HasRuleEngine` derived from `MaxRules > 0`, `HasRetentiveMemory` derived from `RetentiveRegisters > 0`.
- **Formal Contract Freeze Tests (Phase E)**:
  - Added `WireContractV1_9FreezeTests.cs`: Literal value assertions freezing all register addresses, block lengths, base indices, and binary offsets.
  - Added `ProtocolNumericContractTests.cs`: Literal value assertions freezing all protocol enum integers (`SPLC_DeviceClass`, `SPLC_RemoteIoVariant`, `SPLC_ResetReason`, `SPLC_HealthFlags`, `SPLC_SystemCommand`, `SPLC_CommandStatus`, `SPLC_ConfigStatus`, `SPLC_ErrorCode`, `SPLC_TriggerType`, `SPLC_CompareOp`, `SPLC_ActionType`, `ModbusExceptionCode`).
  - Added `DomainNumericContractAndMappingFreezeTests.cs`: Literal assertions for `TagKind`, `TriggerKind`, `CompareOperator`, `ActionKind` and 1:1 direct cast roundtrip guarantees.
  - Added `PlatformArchitectureRegressionTests.cs`: Behavioral tests verifying mapper adapter purity, builder gatekeeper strictness, and un-hardcoded unknown variant support.
  - Added `GoldenVectorV1_9Tests.cs` and `golden_vectors_v1_9.json`: 9 canonical test vectors (GV-001..GV-009) with mathematically verified Modbus CRC-16 frames and payload checksums.
- **Explicit Enum Definition**:
  - Formalized `SPLC_ConfigStatus` (`0=IDLE, 1=RECEIVING, 2=VERIFYING, 3=READY, 4=ERROR`) in `DeviceEnums.cs` and `simpleplc_protocol_v1_7.h`.
- **Documentation Freeze**:
  - Centralized platform standards in `docs/platform/SimplePLC_Wire_Contract_V1_9.md` and `docs/platform/SimplePLC_Modbus_Register_Map_V1.md`.

### Changed
- Decoupled product configuration from static C# code: Studio connects to any microcontroller conforming to Wire Profile V1 without code changes or redeployments.
- Standardized scan time field in `DeviceHealth`: confirmed millisecond unit (`scan_time_ms`, nominal 10 ms).
- Clarified CRC scopes: explicitly separated Rule Table Payload CRC16 (staged byte stream) from Modbus RTU Frame CRC16 (transport frame checksum).

---

## [1.7.0] - 2026-09-15

### Added
- Standardized 32-byte `RuleRecord` wire format (16 Modbus registers).
- Atomic staging and commit handshake mechanism via registers `0x9000..0xA001`.
- Commit magic word `0xA5A5` for atomic pointer swap.
- Retentive virtual registers (`VREG_RETAIN0..31` at TagIndex 84..115).
- System maintenance commands (`REBOOT`, `FACTORY_RESET`, `CLEAR_RULES`, `CLEAR_RETAIN` at `0x0A00`).
- Initial C header specification `simpleplc_protocol_v1_7.h`.

---

## [1.0.0] - 2026-09-01

### Added
- Initial SimplePLC Remote I/O V1 specification (8 DI, 8 DO, 4 AI).
- Modbus RTU communication over USB-CDC virtual COM port.
- Basic runtime tag monitoring at `0x0900`.
