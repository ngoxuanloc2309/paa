# SimplePLC Engineering Documentation (Tài Liệu Kỹ Thuật Dự Án)

Chào mừng đến với hệ thống tài liệu kỹ thuật chính thức của **SimplePLC**.  
Toàn bộ tài liệu được phân loại theo từng lĩnh vực chuyên môn (Architecture, Firmware, Platform Wire Contract, Hardware Testing, và Design Presentations).

---

## 📁 Danh Mục Cấu Trúc Tài Liệu

### 1. [Kiến Trúc Hệ Thống (`docs/architecture/`)](architecture/)
Tài liệu phân tầng Clean Architecture, các ranh giới phụ thuộc và nguyên tắc thiết kế lõi:
* [`CURRENT_ARCHITECTURE.md`](architecture/CURRENT_ARCHITECTURE.md): Kiến trúc tổng thể hiện hành của dự án (Domain, Protocol, Application, Infrastructure, Studio).
* [`ARCHITECTURE_LAYERS.md`](architecture/ARCHITECTURE_LAYERS.md): Đặc tả phân bổ trách nhiệm giữa các tầng và luồng phụ thuộc Inversion of Control.

---

### 2. [Quy Chuẩn Firmware STM32 (`docs/firmware/`)](firmware/)
Tài liệu định hướng và đặc tả kỹ thuật dành riêng cho team phát triển Firmware Vi điều khiển:
* [`MCU_CONFORMANCE_SPECIFICATION_V2_0.md`](firmware/MCU_CONFORMANCE_SPECIFICATION_V2_0.md): **[CHUẨN CỐT LÕI]** Đặc tả kiểm thử sự phù hợp của Firmware MCU STM32 với Wire Profile V2.0, Function Block TON/TOF/TP/CTU/CTD, RTC Clock, và hệ thống Chẩn đoán `0x0A20` (Watchdog Lease 3000ms, Failsafe DO, Retain Memory).

---

### 3. [Khế Ước Giao Thức & Cấu Trúc Dữ Liệu (`docs/platform/`)](platform/)
Đặc tả chi tiết giao thức truyền thông Modbus RTU và khế ước dữ liệu qua đường dây (Wire Contracts):
* [`SimplePLC_App_MCU_Structs_v2.0_Self_Describing_Profile.md`](platform/SimplePLC_App_MCU_Structs_v2.0_Self_Describing_Profile.md): **[CHUẨN CỐT LÕI]** Cấu trúc hồ sơ thiết bị tự mô tả Self-Describing Device Profile V2.0 (Wire Profile 2, RTC Clock Read-Before-Write, Dedicated Function Blocks).
* [`SimplePLC_Wire_Contract_V2_Draft.md`](platform/SimplePLC_Wire_Contract_V2_Draft.md): Đặc tả phân hệ Chẩn đoán Diag & Commissioning Control V2.0 (`0x0A20`).
* [`SimplePLC_Modbus_Register_Map_V1.md`](platform/SimplePLC_Modbus_Register_Map_V1.md): Bản đồ phân bổ thanh ghi Modbus chuẩn hóa Platform V2.0 (`0x0000..0xA001`).
* [`SimplePLC_Wire_Contract_V1_9.md`](platform/SimplePLC_Wire_Contract_V1_9.md): Đặc tả khế ước nhị phân nền tảng kế thừa V1.9 (Rule 32-byte Big-Endian, CRC-16/MODBUS).
* [`SimplePLC_Contract_Changelog.md`](platform/SimplePLC_Contract_Changelog.md): Nhật ký phiên bản và lịch sử thay đổi khế ước truyền thông V1.0 -> V2.0.

---

### 4. [Kiểm Thử & Tích Hợp Phần Cứng (`docs/testing/`)](testing/)
Kế hoạch và quy trình thử nghiệm đo kiểm thực tế trên bench test:
* [`HARDWARE_INTEGRATION_TEST_PLAN.md`](testing/HARDWARE_INTEGRATION_TEST_PLAN.md): Kế hoạch kiểm thử tích hợp phần cứng USB CDC / RS485 chuẩn Platform V2.0 (14 ca kiểm thử từ HIT-001 đến HIT-014).

---

### 5. [Bản Vẽ & Slide Thiết Kế (`docs/design_presentations/`)](design_presentations/)
Tài liệu slide thuyết trình và hình ảnh phục vụ thuyết minh, báo cáo kiến trúc:
* `IIoT_Firmware_Core_Architecture.pptx`
* `SimplePLC_Firmware_Engineering_Overview.pptx`
* `SimplePLC_Config_UI_Wireframes.pptx`
* `SimplePLC_Worked_Examples_UI_to_MCU.pptx`
* `SPLC_DIAG_Design_Diagrams_v2.docx`

---

### 6. [Lịch Sử & Giai Đoạn Nguyên Mẫu (`docs/history/`)](history/)
* [`LEGACY_IMPLEMENTATION_NOTES.md`](history/LEGACY_IMPLEMENTATION_NOTES.md): Ghi chú triển khai các thế hệ trước.
* [`html_prototypes/`](history/html_prototypes/): Các bản nháp giao diện HTML sơ khai.

---

### 7. Quy Tắc Kỹ Thuật Mã Nguồn
* [`ENGINEERING_RULES.md`](ENGINEERING_RULES.md): Các quy tắc bất biến trong lập trình và bảo đảm chất lượng code của SimplePLC.
