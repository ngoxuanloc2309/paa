# SPLC-TEST-HW-001: Kế Hoạch & Ma Trận Kiểm Thử Đồng Bộ Cổng RS-485 Phần Cứng (Hardware-Aware RS-485 Auto-Sync)

> **Mã kế hoạch:** `SPLC-TEST-HW-001`  
> **Trạng thái:** Sẵn sàng kiểm thử (Ready)  
> **Áp dụng cho:** `SimplePLC.Protocol`, `SimplePLC.Domain`, `SimplePLC.Infrastructure`, `SimplePLC.Studio`, `SimplePLC.McuEmulator`

---

## 1. Mục Tiêu Kiểm Thử

Đảm bảo tính năng tự động nhận diện số lượng cổng RS-485 vật lý từ phần cứng MCU (`0x0A0A`) hoạt động chính xác, ổn định, có khả năng xử lý ngoại lệ an toàn và đáp ứng trọn vẹn các yêu cầu:
1. Độ chính xác giao thức Modbus RTU và ánh xạ thanh ghi.
2. Khả năng chịu lỗi và cơ chế dự phòng (Fail-Safe Fallback) khi giao tiếp với firmware đời cũ.
3. Chặn đứng các vi phạm logic tại tầng biên dịch (Compiler Gate) và tầng nạp xuống thiết bị (Deploy Gate).
4. Khả năng co giãn động của giao diện người dùng theo số lượng cổng thực tế.

---

## 2. Ma Trận Kịch Bản Kiểm Thử (Test Cases Matrix)

| STT | Mã Test Case | Tầng Kiểm Thử | Tên Test Case | Điều Kiện Tiền Đề | Các Bước Thực Hiện | Kết Quả Kỳ Vọng |
| :---: | :--- | :--- | :--- | :--- | :--- | :--- |
| 1 | **`TC-HW-MB-001`** | Protocol | `VerifyRegisterMapConstants_Rs485PhysicalPortCount` | Không | Kiểm tra hằng số trong `ModbusRegisterMap` và header C | Địa chỉ `0x0A0A`, độ dài 1 thanh ghi |
| 2 | **`TC-HW-MB-002`** | Domain | `DeviceProfileBuilder_WithCustomRs485PortCount_AssignsCorrectly` | Cấu hình profile với `portCount = 1, 2, 4` | Gọi `DeviceProfileBuilder.TryBuild(...)` | `ProductDefinition.PhysicalRs485PortCount` nhận đúng giá trị truyền vào |
| 3 | **`TC-HW-MB-003`** | Domain | `ProductDefinition_FallbackWhenModbusMasterDisabled_ReturnsZero` | Bitmask không có `MODBUS_MASTER` | Tạo `ProductDefinition` không có năng lực Modbus Master | `PhysicalRs485PortCount` tự động là 0 |
| 4 | **`TC-HW-MB-004`** | Infrastructure | `DeviceDescriptorReader_Reads0x0A0A_ReturnsHardwareCount` | `McuReferenceSimulator` nạp giá trị `1` tại `0x0A0A` | Gọi `ReadPhysicalRs485PortCountAsync()` qua fake Modbus client | Trả về chính xác giá trị `1` |
| 5 | **`TC-HW-MB-005`** | Infrastructure | `DeviceDescriptorReader_When0x0A0ATimeout_FallbackToCapabilities` | Simulator giả lập lỗi timeout tại địa chỉ `0x0A0A` | Gọi `ReadPhysicalRs485PortCountAsync()` | Bắt lỗi an toàn và fallback về 2 (khi có MODBUS_MASTER) hoặc 0 |
| 6 | **`TC-HW-MB-006`** | Infrastructure | `RuleTableWriter_FailsDeploy_WhenTargetPortExceedsPhysicalCount` | MCU cấu hình chỉ có 1 cổng RS-485 (`0x0A0A = 1`), Staging nạp rule trỏ vào Port 2 | Gọi `DeployRulesAsync(...)` | Nạp bị từ chối với `IsSuccess = false`, `ErrorCode = UNSUPPORTED` |
| 7 | **`TC-HW-MB-007`** | Studio | `ModbusPortConfigViewModel_AdaptsToPhysicalPortCount` | `ProjectMetadata` cấu hình `PhysicalRs485PortCount = 1` | Khởi tạo `ModbusPortConfigViewModel` | Danh sách `Ports` chỉ có đúng 1 phần tử (Cổng 1), không sinh thừa Cổng 2..4 |
| 8 | **`TC-HW-MB-008`** | Studio | `RuleCompiler_EmitsError_WhenBlockUsesPortExceedingPhysicalCount` | Target `ProductDefinition` có 1 cổng RS-485, canvas có khối Modbus Read trỏ vào Cổng 2 | Gọi `RuleCompiler.Compile()` | Trả về diagnostic mức `DiagnosticSeverity.Error`, chỉ rõ số cổng vật lý bị vượt quá |
| 9 | **`TC-HW-MB-009`** | Emulator / CLI | `Emulator_SupportsCommandLineOption_AndExposesPortCount` | Chạy lệnh `SimplePLC.McuEmulator --rs485-ports 1` | Đọc holding register `0x0A0A` qua Modbus RTU | Trả về giá trị 1 và in thông báo trên banner |

---

## 3. Tiêu Chí Nghiệm Thu (Acceptance Criteria)

- [ ] 100% test case trong ma trận được hiện thực hóa bằng mã kiểm thử tự động xUnit.
- [ ] Toàn bộ bộ kiểm thử của giải pháp (`dotnet test SimplePLC.sln`) vượt qua 100% không có lỗi hồi quy (zero regression).
- [ ] Giao diện người dùng phản hồi chính xác khi kết nối thiết bị thật hoặc thiết bị mô phỏng có số cổng khác nhau.
