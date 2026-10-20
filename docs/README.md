# SimplePLC Engineering Documentation (Tài Liệu Kỹ Thuật Dự Án)

Chào mừng đến với cổng tài liệu kỹ thuật chính thức của **SimplePLC / SynaptiX IDE Platform V2.0**.  
Toàn bộ tài liệu được chuẩn hóa, phân loại rõ ràng theo từng lĩnh vực chuyên môn (User Manuals, Architecture, Firmware Specs, Wire Contracts, Solutions & Proposals, Design Audits, Hardware Testing, và Design Presentations).

---

## 🧭 Bản Đồ Chỉ Mục Tài Liệu (Master Sitemap)

```
docs/
├── README.md                                          # Cổng chỉ mục tài liệu tổng thể (File này)
├── ENGINEERING_RULES.md                              # 8 nguyên tắc kỹ thuật cốt lõi bất biến
├── SYNAPTIX_IDE_USER_MANUAL_VI.md                    # Sổ tay vận hành SynaptiX IDE (Tiếng Việt)
├── SYNAPTIX_IDE_USER_MANUAL_EN.md                    # Sổ tay vận hành SynaptiX IDE (Tiếng Anh)
├── SynaptiX_IDE_User_Manual_VI.docx                  # Bản xuất Word sổ tay Tiếng Việt
├── SynaptiX_IDE_User_Manual_EN.docx                  # Bản xuất Word sổ tay Tiếng Anh
├── architecture/                                      # Tài liệu kiến trúc phần mềm Clean Architecture
├── firmware/                                          # Đặc tả kỹ thuật và ICD cho Firmware MCU STM32/ESP32
├── platform/                                          # Khế ước truyền thông Modbus RTU & cấu trúc nhị phân Wire Contract
├── proposals/                                         # Hồ sơ giải pháp công nghiệp & đề xuất Sort-to-Light
├── audit/                                             # Báo cáo kiểm toán thị giác & ma trận màu sắc ISA-101/IEC 60073
├── testing/                                           # Kế hoạch và kịch bản đo kiểm tích hợp phần cứng
├── design_presentations/                              # Bản vẽ, slide thuyết minh kiến trúc và sơ đồ hệ thống
└── history/                                           # Lịch sử nguyên mẫu và ghi chú phiên bản tiền nhiệm
```

---

## 📁 Danh Mục Chi Tiết Theo Chuyên Môn

### 1. 📖 [Sổ Tay Hướng Dẫn Sử Dụng SynaptiX IDE](SYNAPTIX_IDE_USER_MANUAL_VI.md)
Tài liệu hướng dẫn trực quan dành cho Kỹ sư Vận hành, Tích hợp Hệ thống (SI) và Kỹ sư Tự động hóa:
* 🇻🇳 [`SYNAPTIX_IDE_USER_MANUAL_VI.md`](SYNAPTIX_IDE_USER_MANUAL_VI.md) | 📄 [`SynaptiX_IDE_User_Manual_VI.docx`](SynaptiX_IDE_User_Manual_VI.docx): Hướng dẫn sử dụng IDE chi tiết Tiếng Việt (Tổng quan, Cài đặt, Logic Editor FBD, Quản lý Tag/Rule, Cấu hình IIoT MQTT, Khối Modbus Master `MB_READ`, Đồng bộ RTC, Chẩn đoán Diag, AI Copilot).
* 🇬🇧 [`SYNAPTIX_IDE_USER_MANUAL_EN.md`](SYNAPTIX_IDE_USER_MANUAL_EN.md) | 📄 [`SynaptiX_IDE_User_Manual_EN.docx`](SynaptiX_IDE_User_Manual_EN.docx): Official User Manual in English (Full visual guide covering FBD logic engine, MQTT edge broker, Modbus Master poller, real-time diagnostics, and AI assistant).
* ⚙️ [`ENGINEERING_RULES.md`](ENGINEERING_RULES.md): 8 nguyên tắc kỹ thuật cốt lõi bất biến bảo đảm độ tin cậy thời gian thực, an toàn công nghiệp và chất lượng mã nguồn.

---

### 2. 🏛️ [Kiến Trúc Hệ Thống (`docs/architecture/`)](architecture/)
Tài liệu phân tầng Clean Architecture, các ranh giới phụ thuộc và nguyên tắc thiết kế lõi của giải pháp:
* [`CURRENT_ARCHITECTURE.md`](architecture/CURRENT_ARCHITECTURE.md): **[KIẾN TRÚC TỔNG THỂ]** Bản đồ cấu trúc 5 tầng (`Domain`, `Protocol`, `Application`, `Infrastructure`, `SimplePLC.Studio`), bộ kiểm thử 1.220 test cases, quy hoạch thanh ghi V2.0 và chiến lược triển khai nạp an toàn.
* [`ARCHITECTURE_LAYERS.md`](architecture/ARCHITECTURE_LAYERS.md): Đặc tả phân bổ trách nhiệm giữa các tầng và luồng phụ thuộc Inversion of Control (IoC).
* [`IDE_FEATURE_ARCHITECTURE_REVIEW.md`](architecture/IDE_FEATURE_ARCHITECTURE_REVIEW.md): Báo cáo rà soát toàn diện tính năng & kiến trúc IDE công nghiệp so sánh với chuẩn mực CODESYS V3.5 và Siemens TIA Portal V19.
* [`NEXTGEN_AI_COPILOT_SPECIFICATION.md`](architecture/NEXTGEN_AI_COPILOT_SPECIFICATION.md): Đặc tả kỹ thuật trợ lý AI Copilot thế hệ mới (Deep Project Semantic Context, Function Calling / Tool Execution, Diff Preview Ghost Nodes).
* [`SPLC_SPEC_IEC61131_DATA_TYPES_AND_MQTT_STANDARDIZATION.md`](architecture/SPLC_SPEC_IEC61131_DATA_TYPES_AND_MQTT_STANDARDIZATION.md): Chuẩn hóa hệ thống kiểu dữ liệu công nghiệp IEC 61131-3 và quy cách truyền thông IIoT MQTT Telemetry / Subscribe.

---

### 3. ⚡ [Đặc Tả Kỹ Thuật Firmware MCU (`docs/firmware/`)](firmware/)
Tập tài liệu đặc tả giao diện kỹ thuật (Interface Control Documents - ICD) chuyên sâu cho từng phân hệ phần cứng:
* [`SPLC_SPEC_MQTT_001_TELEMETRY_ICD.md`](firmware/SPLC_SPEC_MQTT_001_TELEMETRY_ICD.md): **[MQTT TELEMETRY ICD]** Đặc tả giao diện truyền thông nhị phân MQTT Broker Gateway (`0x0E00`) và 4 khối phát tán dữ liệu Telemetry Pub (`0x0C00..0x0D7F`).
* [`SPLC_SPEC_MQTT_002_SUBSCRIBE_ICD.md`](firmware/SPLC_SPEC_MQTT_002_SUBSCRIBE_ICD.md): **[MQTT SUBSCRIBE ICD]** Đặc tả giao diện thu nhận dữ liệu Inbound Subscribe & Bóc tách Dữ liệu Động Atomic All-or-Nothing (`0x0D80..0x0DFF`).
* [`SPLC_SPEC_MQTT_003_TLS_CERTIFICATE_WIRE_ICD.md`](firmware/SPLC_SPEC_MQTT_003_TLS_CERTIFICATE_WIRE_ICD.md): **[MQTT TLS ICD]** Khế ước nhị phân truyền tải Chứng chỉ bảo mật TLS X.509 phân đoạn Chunked (`0x0F00..0x0F43`) nạp vào bộ nhớ Flash an toàn.
* [`SPLC_SPEC_MODBUS_MASTER_001_READ_ICD.md`](firmware/SPLC_SPEC_MODBUS_MASTER_001_READ_ICD.md): **[MODBUS MASTER READ ICD]** Thu thập dữ liệu cảm biến ngoại vi RS-485 Modbus RTU Master (`MB_READ`, `0x1000..0x10FF`), 4 kênh với 4 chuẩn Endianness.
* [`SPLC_SPEC_MODBUS_MASTER_002_WRITE_ICD.md`](firmware/SPLC_SPEC_MODBUS_MASTER_002_WRITE_ICD.md): **[MODBUS MASTER WRITE ICD]** Phát lệnh chấp hành RS-485 Modbus RTU Master (`MB_WRITE`, `0x1100..0x11FF`).
* [`SPLC_SPEC_DIRECT_WRITE_003_ICD.md`](firmware/SPLC_SPEC_DIRECT_WRITE_003_ICD.md): **[PULSE OUT ICD]** Khối phát xung phần cứng & điều khiển chấp hành chân ngõ ra số vật lý DO0..DO7 (`PULSE_OUT`, badge `PULSE`, tương thích `DIRECT_WRITE`, `0x1200..0x12FF`).
* [`SPLC_SPEC_WIFI_004_ICD.md`](firmware/SPLC_SPEC_WIFI_004_ICD.md): **[WI-FI ST67 SUBSYSTEM ICD]** Đặc tả giao diện truyền thông nhị phân Wi-Fi module ST67W611M1 (`0x1300..0x13FF`, Cấu hình mạng, Bắt tay quét sóng không đồng bộ FreeRTOS và Bảng 8 AP lân cận).

---

### 4. 🔌 [Bản Đặc Tả Chuẩn Cốt Lõi Duy Nhất (`docs/platform/`)](platform/)
Cổng đặc tả kỹ thuật nền tảng toàn diện và duy nhất dành cho Kỹ sư Firmware MCU & Kỹ sư Hệ thống:
* 🌟 [`SimplePLC_App_MCU_Structs_v2.0_Self_Describing_Profile.md`](platform/SimplePLC_App_MCU_Structs_v2.0_Self_Describing_Profile.md): **[BẢN ĐẶC TẢ MASTER CHUẨN CỐT LÕI DUY NHẤT]**  
  Tài liệu bao trọn 100% kiến trúc, Master Memory Map V2.0 (`0x0000..0x13FF`, `0x9000..0xA001`), tất cả 16 C binary structs, quy chuẩn an toàn Diag `0x0A20`, kiểm tra năng lực phần cứng `0x0A08` Fail-Safe (`SPLC_CAP_WIFI`), phân bổ 124 Active Tags, checklist nghiệm thu CHKL-01..21 và hướng dẫn lập trình C/C++ mẫu. Kỹ sư MCU chỉ cần đọc file này làm tài liệu chuẩn duy nhất và từ đó dẫn ra các ICD chuyên sâu ở Mục 3 khi cần.
* [`SimplePLC_Contract_Changelog.md`](platform/SimplePLC_Contract_Changelog.md): Nhật ký phiên bản và lịch sử thay đổi khế ước truyền thông (từ V1.0 đến V2.8.1).
* [`STANDARD_RELEASE_PROCEDURE_SOP.md`](platform/STANDARD_RELEASE_PROCEDURE_SOP.md): Quy trình thao tác chuẩn (SOP) cho việc đóng gói, gắn thẻ phiên bản và phát hành hệ thống.
* *(Các bản nháp cũ V1.9, V1 Register Map đã được lưu trữ an toàn trong `docs/history/`).*

---

### 5. 📦 [Giải Pháp Kỹ Thuật & Đề Xuất Ứng Dụng (`docs/proposals/`)](proposals/)
Hồ sơ giải pháp kỹ thuật, đề xuất kiến trúc hệ thống và tài liệu kỹ thuật dự án Sort-to-Light:
* [`SPX_Sort_to_Light_Solution_Proposal_V3.docx`](proposals/SPX_Sort_to_Light_Solution_Proposal_V3.docx): Đề xuất giải pháp Sort-to-Light V3 (Bản cập nhật mới nhất kèm ảnh chụp màn hình minh họa IDE).
* [`SPX_Sort_to_Light_Solution_Proposal_V2.docx`](proposals/SPX_Sort_to_Light_Solution_Proposal_V2.docx): Đề xuất giải pháp Sort-to-Light V2.
* [`SPX_Sort_to_Light_Technical_Proposal.docx`](proposals/SPX_Sort_to_Light_Technical_Proposal.docx): Đề xuất kỹ thuật chi tiết hệ thống phân loại Sort-to-Light.
* [`[IoT Sort to Light] Hardware Specification.pdf`](proposals/[IoT%20Sort%20to%20Light]%20Hardware%20Specification.pdf): Tài liệu đặc tả kỹ thuật phần cứng mô-đun Sort-to-Light.
* [`images/`](proposals/images/): Bộ hình ảnh trực quan chụp giao diện SynaptiX IDE phục vụ tài liệu đề xuất và sổ tay người dùng.

---

### 6. 🎨 [Báo Cáo Kiểm Toán Giao Diện & Tiêu Chuẩn Màu Sắc (`docs/audit/`)](audit/)
Báo cáo kiểm toán độc lập về công thái học thị giác và chuẩn hóa bảng màu công nghiệp:
* [`VISUAL_DESIGN_AUDIT_REPORT.md`](audit/VISUAL_DESIGN_AUDIT_REPORT.md): Báo cáo kiểm toán thiết kế giao diện SynaptiX IDE theo tiêu chuẩn độ tương phản WCAG AA và công thái học phòng điều khiển.
* [`VERIFIED_INDUSTRIAL_PALETTE_RUBRIC.md`](audit/VERIFIED_INDUSTRIAL_PALETTE_RUBRIC.md): Bảng ma trận đối chiếu bảng màu Industrial Slate Gray tuân thủ tiêu chuẩn IEC 60073 và ISA-101 (loại bỏ triệt để màu neon thương mại).

---

### 7. 🧪 [Kiểm Thử & Tích Hợp Phần Cứng (`docs/testing/`)](testing/)
Kế hoạch và quy trình thử nghiệm đo kiểm thực tế trên băng thử (bench test):
* [`HARDWARE_INTEGRATION_TEST_PLAN.md`](testing/HARDWARE_INTEGRATION_TEST_PLAN.md): Kế hoạch kiểm thử tích hợp phần cứng USB CDC / RS485 chuẩn Platform V2.0 (14 ca kiểm thử từ HIT-001 đến HIT-014).

---

### 8. 📊 [Bản Vẽ & Slide Thiết Kế Kiến Trúc (`docs/design_presentations/`)](design_presentations/)
Tài liệu slide thuyết trình và sơ đồ kiến trúc phục vụ đào tạo và báo cáo dự án:
* `IIoT_Firmware_Core_Architecture.pptx`: Slide kiến trúc lõi IIoT Firmware.
* `SimplePLC_Firmware_Engineering_Overview.pptx`: Tổng quan kỹ thuật Firmware SimplePLC.
* `SimplePLC_Config_UI_Wireframes.pptx`: Bản vẽ khung giao diện cấu hình (Wireframes).
* `SimplePLC_Worked_Examples_UI_to_MCU.pptx`: Các ví dụ mẫu luồng dữ liệu từ UI xuống MCU.
* `SPLC_DIAG_Design_Diagrams_v2.docx`: Sơ đồ thiết kế phân hệ Chẩn đoán Diag V2.

---

### 9. 📜 [Lịch Sử & Giai Đoạn Nguyên Mẫu (`docs/history/`)](history/)
Lưu trữ các ghi chú nghiên cứu và mô hình giao diện sơ khai trong giai đoạn R&D:
* [`LEGACY_IMPLEMENTATION_NOTES.md`](history/LEGACY_IMPLEMENTATION_NOTES.md): Ghi chú kỹ thuật các phiên bản kiến trúc tiền nhiệm.
* [`html_prototypes/`](history/html_prototypes/): Các bản nháp nguyên mẫu giao diện HTML ban đầu.
