# SimplePLC Platform Contract Changelog

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
  - `0x0B00..0x0B3F`: 8 Dedicated Timers (TON, TOF, TP), 8 registers each (16 bytes).
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
