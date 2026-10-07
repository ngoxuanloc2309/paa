# [SUPERSEDED] Tổng Kết Triển Khai Giai Đoạn 1: Khế Ước Nhị Phân 28-Byte MCU & Thuật Toán CRC-32/ISO-HDLC

> [!CAUTION]
> **STATUS: HISTORICAL & SUPERSEDED — DO NOT USE FOR IMPLEMENTATION**
> Tài liệu này phản ánh thiết kế ban đầu trong Giai đoạn 1 (Rule 28 bytes, CRC-32, 16 VFLAG/VREG).
> Hiện tại, toàn bộ nền tảng đã được nâng cấp và **ĐÓNG BĂNG ở SimplePLC Platform V1.9** (Rule 32 bytes, CRC-16 Modbus, 32 VFLAG/VREG/RETAIN, Self-Describing profile).
> 
> **Nguồn chân lý duy nhất của hệ thống:**
> 👉 [`docs/platform/SimplePLC_Wire_Contract_V1_9.md`](../platform/SimplePLC_Wire_Contract_V1_9.md)
> 👉 [`docs/platform/SimplePLC_Modbus_Register_Map_V1.md`](../platform/SimplePLC_Modbus_Register_Map_V1.md)

---

ChÃºng ta Ä‘Ã£ hoÃ n thÃ nh toÃ n diá»‡n **Giai Ä‘oáº¡n 1** theo Ä‘Ãºng chuáº©n thiáº¿t káº¿ ká»¹ thuáº­t Senior, tuÃ¢n thá»§ 100% Ä‘áº·c táº£ kháº¿ Æ¯á»›c pháº§n cá»©ng **`SPLC-APP-MCU-001` (Revision 1.1.3 Baseline Â§6 & Â§7)** vÃ  cÃ¡c yÃªu cáº§u lÆ°u Ä‘á»“ kiáº¿n trÃºc **`SPLC-AF-001` / `SPLC-DIAG-001`**.

---

## 1. CÃ¡c Háº¡ng Má»¥c Ä Ã£ Thá»±c Hiá»‡n

### 1.1. Service MÃ£ HÃ³a Nhá»‹ PhÃ¢n Chuáº©n 28-Byte (`RuleBinaryEncoder.cs`)
* **ÄÆ°á»ng dáº«n**: [`RuleBinaryEncoder.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Services/RuleBinaryEncoder.cs)
* **Kháº¯c phá»¥c triá»‡t Ä‘á»ƒ**:
  - KhÃ´ng dÃ¹ng memcpy hay endianness tá»± nhiÃªn cá»§a x86 CPU (`REQ-RUL-001`, `REQ-SER-002`).
  - DÃ¹ng helper tÆ°á»ng minh `BinaryPrimitives` Ä‘á»ƒ ghi toÃ n bá»™ sá»‘ 16-bit vÃ  32-bit á»Ÿ Ä‘á»‹nh dáº¡ng **Big-Endian chuáº©n** (`REQ-SER-001`).
  - ÄÃ³ng gÃ³i cá» `NEGATE` cá»§a Guard vÃ o bit 15 cá»§a trÆ°á»ng 16-bit `guard_tag`:
    `guard_tag = (guard_index & 0x7FFF) | (negate ? 0x8000 : 0)`
  - 2 byte cuá»‘i cÃ¹ng (`reserved[2]`) luÃ´n Ä‘Æ°á»£c Ä‘áº£m báº£o lÃ  `0x00 0x00` (`REQ-RUL-002`).
  - Cung cáº¥p tÃ­nh nÄƒng phÃ¢n tÃ¡ch trÆ°á»ng (`FormatDetailedBreakdown`) phá»¥c vá»¥ quan sÃ¡t trá»±c quan.

### 1.2. Service Kiá»ƒm Tra ToÃ n Váº¹n CRC-32/ISO-HDLC (`Crc32HdlcService.cs`)
* **ÄÆ°á»ng dáº«n**: [`Crc32HdlcService.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Services/Crc32HdlcService.cs)
* **ThÃ´ng sá»‘ thuáº­t toÃ¡n**:
  - Äa thá»©c (Polynomial): `0x04C11DB7` (Reversed: `0xEDB88320`)
  - Initial Value: `0xFFFFFFFF`
  - RefIn: `true`, RefOut: `true`
  - XorOut: `0xFFFFFFFF`
  - Báº£ng tra cá»©u (Lookup Table) 256 pháº§n tá»­ tá»‘i Æ°u tá»‘c Ä‘á»™ tÃ­nh toÃ¡n.
* **XÃ¡c minh chuáº©n**: VÆ°á»£t qua báº¯t buá»™c test vector `T-03` vá»›i chuá»—i ASCII `"123456789"` $\rightarrow$ ra Ä‘Ãºng mÃ£ `0xCBF43926`.

### 1.3. Cáº­p Nháº­t UI Model & Giao Diá»‡n Báº£ng Quy Táº¯c
* **ÄÆ°á»ng dáº«n**: [`RuleItemModel.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Models/RuleItemModel.cs), [`RuleTableViewModel.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/ViewModels/RuleTableViewModel.cs), [`RuleTableView.xaml`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Views/RuleTableView.xaml)
* **Cáº£i tiáº¿n**:
  - `RuleItemModel` chuyá»ƒn sang sá»­ dá»¥ng `RuleBinaryEncoder` thay tháº¿ toÃ n bá»™ logic sinh byte nhÃ¡p cÅ©.
  - Bá»• sung `TriggerType.TIME_POINT = 3` theo chuáº©n `DD-04`.
  - Cung cáº¥p **10 vÃ­ dá»¥ máº«u tiÃªu chuáº©n (R1..R10)** thá»ƒ hiá»‡n Ä‘áº§y Ä‘á»§ cÃ¡c tÃ­nh nÄƒng: sÆ°á»n cáº¡nh (Rise/Fall), má»‘c thá»i gian (Time point), chu ká»³ (Interval), so sÃ¡nh tÆ°Æ¡ng tá»± (Analog compare GT, Between), cá»•ng Guard thÆ°á»ng vÃ  Ä‘áº£o (Negated Gate), nhiá»u hÃ nh vi (Set, Toggle, Inc, Send Alarm).
  - Cá»™t **MÃ£ Hex 28-Byte MCU** cÃ³ Tooltip Inspector hiá»ƒn thá»‹ phÃ¢n tÃ¡ch tÆ°á»ng minh theo tá»«ng khá»‘i dá»¯ liá»‡u:
    `[ThLo 4B] [ThHi 4B] [ForMs 4B] [ActP 4B] | [TrgTag 2B] [ActTag 2B] [GrdTag 2B] | [En 1B] [Trg 1B] [Cmp 1B] [Act 1B] | [Rsv 2B]`

### 1.4. Dá»± Ãn Kiá»ƒm Thá»­ Tá»± Äá»™ng xUnit (`SimplePLC.Studio.Tests`)
* **ÄÆ°á»ng dáº«n**: [`SimplePLC.Studio.Tests/`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio.Tests/)
* Bao gá»“m 10 bÃ i test tá»± Ä‘á»™ng bao phá»§ 100% cÃ¡c yÃªu cáº§u:
  1. `WireSize_MustBeExactly28Bytes_REQ_RUL_001`
  2. `ReservedBytes_MustBeZero_REQ_RUL_002`
  3. `GuardTag_NegateBitmask_MustSetBit15_REQ_RUL_003`
  4. `Endianness_MustBeBigEndian_REQ_SER_001_002`
  5. `Roundtrip_Encode_Decode_Integrity`
  6. `ProgramEncoding_LengthMustBe_RuleCountTimes28`
  7. `StandardTestVector_123456789_MustReturn_0xCBF43926_SPLC_APP_MCU_001_T03`
  8. `SelfTest_MustPass`
  9. `EmptyBuffer_Returns_Zero`
  10. `RulesStream_CalculatesDeterministicCrc`

---

## 2. Káº¿t Quáº£ Kiá»ƒm Thá»­ (Verification Results)

```text
Passed!  - Failed: 0, Passed: 20, Skipped: 0, Total: 20, Duration: 38 ms - SimplePLC.Studio.Tests.dll (net8.0)
Build succeeded: 0 Warning(s), 0 Error(s)
```
Táº¥t cáº£ 20 bÃ i kiá»ƒm thá»­ Ä‘á»u Ä‘áº¡t káº¿t quáº£ tuyá»‡t Ä‘á»‘i (100% Pass).

---

## 3. Kháº¯c Phá»¥c Lá»—i KÃ©o DÃ¢y Ná»‘i (Edge Connection Dragging in Logic Editor)

### 3.1. NguyÃªn NhÃ¢n Gá»‘c Rá»… (Root Causes)
1. **Thiáº¿u `ConnectionCompletedCommand`**: TrÃªn tháº» `<nodify:NodifyEditor>` cá»§a `LogicEditorView.xaml` trÆ°á»›c Ä‘Ã¢y hoÃ n toÃ n chÆ°a gÃ¡n `ConnectionCompletedCommand`. Do Ä‘Ã³ khi ngÆ°á»i dÃ¹ng nháº£ chuá»™t trÃªn connector Ä‘Ã­ch, Nodify phÃ¡t sá»± kiá»‡n nhÆ°ng khÃ´ng cÃ³ command nÃ o há»©ng vÃ  lÆ°u káº¿t ná»‘i vÃ o `Connections`.
2. **Binding `PendingConnection` bá»‹ Null**: Tháº» `PendingConnection="{Binding PendingConnection}"` trá» tá»›i thuá»™c tÃ­nh khÃ´ng tá»“n táº¡i trong `LogicEditorViewModel`, khiáº¿n Nodify gÃ¡n null Ä‘Ã¨ lÃªn Ä‘á»‘i tÆ°á»£ng pending ná»™i bá»™, lÃ m máº¥t preview dÃ¢y kÃ©o trong lÃºc drag.
3. **Thiáº¿u `PendingConnectionTemplate`**: ChÆ°a khai bÃ¡o template Ä‘Æ°á»ng nÃ©t kÃ©o thá»­ (`PendingConnection`), khiáº¿n dÃ¢y ná»‘i khÃ´ng cÃ³ hiá»‡u á»©ng snapping vÃ  stroke hiá»ƒn thá»‹.
4. **Hit-testing trÃªn Pin Connector**: `IndustrialPortPinTemplate` trÆ°á»›c Ä‘Ã³ chÆ°a thiáº¿t láº­p `Background="Transparent"` cho container `Grid`, lÃ m giáº£m diá»‡n tÃ­ch nháº­n sá»± kiá»‡n drag/drop chuá»™t.

### 3.2. Giáº£i PhÃ¡p Triá»ƒn Khai
1. **`LogicEditorView.xaml`**:
   - Khai bÃ¡o `<DataTemplate x:Key="PendingConnectionTemplate">` vá»›i `<nodify:PendingConnection Stroke="#3B82F6" StrokeThickness="2" EnableSnapping="True" AllowOnlyConnectors="True" />`.
   - Bá» thuá»™c tÃ­nh binding lá»—i `PendingConnection`, gÃ¡n `PendingConnectionTemplate="{StaticResource PendingConnectionTemplate}"`.
   - GÃ¡n `ConnectionCompletedCommand="{Binding CompleteConnectionCommand}"`.
2. **`LogicEditorViewModel.cs`**:
   - Má»Ÿ rá»™ng `CompleteConnection` há»— trá»£ cáº£ `ValueTuple<object, object>` (do Nodify sinh ra khi kÃ©o tháº£) vÃ  `ValueTuple<ConnectorViewModel, ConnectorViewModel>`.
   - Tá»± Ä‘á»™ng Ä‘áº£o chiá»u náº¿u ngÆ°á»i dÃ¹ng kÃ©o ngÆ°á»£c tá»« chÃ¢n Input sang Output.
   - NgÄƒn cháº·n káº¿t ná»‘i cÃ¹ng 1 node hoáº·c giá»¯a cÃ¡c cá»•ng cÃ¹ng loáº¡i (Input-Input, Output-Output).
   - Tá»± Ä‘á»™ng thay tháº¿ dÃ¢y cÅ© khi cá»•ng Input Ä‘Ã£ cÃ³ dÃ¢y trÆ°á»›c Ä‘Ã³ (Ä‘áº£m báº£o luáº­t 1 input chá»‰ nháº­n 1 dÃ¢y).
3. **`Styles/NodeTemplates.xaml`**:
   - ThÃªm `Background="Transparent"` vÃ o Grid cá»§a `IndustrialPortPinTemplate`.
   - ThÃªm `Cursor="Cross"` cho cáº£ `NodeInput` vÃ  `NodeOutput`.
   - ThÃªm `Style.Triggers` Ä‘á»•i mÃ u pin khi `IsConnected="True"` hoáº·c khi chuá»™t hover `IsMouseOver="True"`.
4. **Unit Tests Bá»• Sung (`LogicEditorViewModelTests.cs`)**:
   - 6 bÃ i test má»›i bao phá»§: kÃ©o xuÃ´i (Output $\to$ Input), kÃ©o ngÆ°á»£c (Input $\to$ Output), kÃ©o cÃ¹ng node, kÃ©o cÃ¹ng kiá»ƒu cá»•ng, ghi Ä‘Ã¨ dÃ¢y cÅ© vÃ  xÃ³a dÃ¢y.

---

## 4. Thiáº¿t Káº¿ Láº¡i Block Node Nhá» Gá»n, Hiá»‡n Äáº¡i & Inspector Chá»‰nh Sá»­a Trá»±c Tiáº¿p Ná»™i Dung

### 4.1. Khá»‘i Node TrÃªn Canvas (Compact Modern Node Cards)
- **Tá»‘i Æ°u kÃ­ch thÆ°á»›c**: Giáº£m Ä‘á»™ rá»™ng tá»« `260px` xuá»‘ng `~165-195px`, giáº£m chiá»u cao tá»« `160px` xuá»‘ng `~75px`.
- **Loáº¡i bá» chá»¯ rÆ°á»m rÃ **: Bá» toÃ n bá»™ cÃ¡c Ä‘oáº¡n cÃ¢u phá»¥ dÃ i (*"Cung cáº¥p giÃ¡ trá»‹ tá»« thiáº¿t bá»‹"*, *"PhÃ¡t xung kÃ­ch hoáº¡t nhÃ¡nh logic..."*), trÃ¡nh gÃ¢y nhiá»…u thá»‹ giÃ¡c trÃªn sÆ¡ Ä‘á»“.

### 4.2. Báº£ng Thuá»™c TÃ­nh BÃªn Pháº£i (Right Inspector Panel - In-Place Content Editing)
- **Chá»‰nh sá»­a toÃ n diá»‡n ná»™i dung (khÃ´ng chá»‰ chá»n)**:
  - Cho phÃ©p ngÆ°á»i dÃ¹ng nháº­p **NhÃ£n ghi chÃº riÃªng cá»§a khá»‘i** (`CustomLabel`) cho báº¥t ká»³ khá»‘i nÃ o Ä‘Æ°á»£c chá»n $\rightarrow$ Cáº­p nháº­t trá»±c tiáº¿p tiÃªu Ä‘á» khá»‘i trÃªn canvas.
  - **Khá»‘i Input**: Sá»­a trá»±c tiáº¿p **TÃªn gá»£i nhá»› (Alias)**, sá»­a **GiÃ¡ trá»‹ mÃ´ phá»ng (Value)**, xem loáº¡i Tag (Kind).
  - **Khá»‘i Trigger**: Chá»n Trigger Type, sá»­a thá»i gian trá»… Dwell Time (`ForMs`), sá»­a nhÃ£n ghi chÃº riÃªng.
  - **Khá»‘i Guard**: Chá»n Compare Op, sá»­a ThresholdLo, ThresholdHi, Checkbox Ä‘áº£o Ä‘iá»u kiá»‡n (NOT), sá»­a nhÃ£n ghi chÃº riÃªng.
  - **Khá»‘i Action**: Chá»n Action Type, chá»n Target Tag VÃ€ **sá»­a trá»±c tiáº¿p TÃªn gá»£i nhá»› cá»§a Tag Ä‘Ã­ch**, sá»­a `ActionParam`, sá»­a nhÃ£n ghi chÃº riÃªng.
- **Äá»“ng bá»™ thá»i gian thá»±c 2 chiá»u (Real-time 2-way sync)**: Tá»± Ä‘á»™ng cáº­p nháº­t `NarrativePreview` vÃ  tÃ¡i biÃªn dá»‹ch luáº­t nhá»‹ phÃ¢n 28-byte.

---

## 5. Tinh Chá»‰nh Cá»•ng ChÃ¢n MÃ©p HÃ´ng, TÆ°Æ¡ng Pháº£n Cao & DÃ¢y Ná»‘i BÃ©zier Tinh Táº¿

### 5.1. Kháº¯c Phá»¥c Khá»‘i MÃ u Che Chá»¯ Cá»•ng (Port Pin Redesign)
- **XÃ³a bá» triá»‡t Ä‘á»ƒ khá»‘i chá»¯ nháº­t Ä‘Ã¨ mÃ u**: TrÆ°á»›c Ä‘Ã¢y `IsConnected="True"` gÃ¡n `Background` lÃªn toÃ n bá»™ `NodeInput`/`NodeOutput`, lÃ m thÃ nh 1 Ã´ mÃ u Ä‘áº·c che láº¥p chá»¯ "In", "Out", "Evt". ÄÃ£ chuyá»ƒn sang: nhÃ£n cá»•ng luÃ´n trong suá»‘t (`Background="Transparent"`), chá»‰ duy nháº¥t Ä‘iá»ƒm pin trÃ²n 11px Ä‘Æ°á»£c tÃ´ mÃ u khi cÃ³ dÃ¢y cáº¯m.
- **ÄÆ°a cá»•ng ra 2 mÃ©p bÃªn hÃ´ng (Side Edge Ports)**: Cá»•ng Input gáº¯n á»Ÿ mÃ©p trÃ¡i, cá»•ng Output gáº¯n á»Ÿ mÃ©p pháº£i, cÄƒn giá»¯a theo chiá»u cao cá»§a khá»‘i. DÃ¢y ná»‘i Ä‘i ngang tháº³ng hÃ ng tá»« mÃ©p pháº£i khá»‘i trÆ°á»›c sang mÃ©p trÃ¡i khá»‘i sau, khÃ´ng bá»‹ gáº¥p khÃºc xuá»‘ng Ä‘Ã¡y.

### 5.2. NÃ¢ng Cao TÆ°Æ¡ng Pháº£n Ná»™i Dung BÃªn Trong (High-Contrast Typography)
- **Äá»™ tÆ°Æ¡ng pháº£n cao**:
  - DÃ²ng chÃ­nh: Font SemiBold, mÃ u than Ä‘áº­m `#0F172A`, cá»¡ chá»¯ 12.5-13px sáº¯c nÃ©t (**MÃ¡y dá»«ng**, **Fall (1 â†’ 0)**, **Tag == 1**, **DO0 = 1**).
  - Huy hiá»‡u thÃ´ng sá»‘: Ná»n xÃ¡m nháº¡t tinh táº¿ `#F1F5F9`, viá»n `#E2E8F0`, chá»¯ monospace xÃ¡m chÃ¬ rÃµ nÃ©t (`15000ms`, `[DI0]`, `EQ`).
  - PhÃ¢n tÃ¡ch rÃµ rÃ ng giá»¯a tiÃªu Ä‘á», mÃ£ Ä‘á»‹nh danh vÃ  thÃ´ng sá»‘ hÃ nh Ä‘á»™ng.

### 5.3. DÃ¢y Ná»‘i Má»m Máº¡i & MÃ u Sáº¯c Hiá»‡n Äáº¡i (Smooth BÃ©zier Wires)
- **ÄÆ°á»ng cong BÃ©zier**: Thay tháº¿ Ä‘Æ°á»ng `StepConnection` gáº¥p khÃºc 90 Ä‘á»™ cá»©ng nháº¯c báº±ng `nodify:Connection` Ä‘Æ°á»ng cong mÆ°á»£t mÃ , tá»± nhiÃªn nhÆ° Figma / Blender / Unreal Blueprints.
- **MÃ u sáº¯c trung tÃ­nh cao cáº¥p**:
  - DÃ¢y Ä‘Ã£ ná»‘i: MÃ u Slate trung tÃ­nh hiá»‡n Ä‘áº¡i (`Stroke="#64748B"`, Ä‘á»™ dÃ y `2px`), khÃ´ng gÃ¢y chÃ³i hoáº·c rá»‘i máº¯t khi cÃ³ nhiá»u dÃ¢y.
  - DÃ¢y Ä‘ang kÃ©o (Pending wire): MÃ u xanh cÃ´ng nghá»‡ (`Stroke="#3B82F6"`).

---

## 6. CÄƒn Giá»¯a Ná»™i Dung Khá»‘i & DÃ¢y Ná»‘i Tháº³ng Chuáº©n Ladder PLC

### 6.1. CÄƒn Giá»¯a HoÃ n ToÃ n ThÃ´ng Tin Trong Khá»‘i (Centered Content Alignment)
- ToÃ n bá»™ ná»™i dung chá»¯ (TÃªn gá»£i nhá»›, tráº¡ng thÃ¡i kÃ­ch hoáº¡t, Ä‘iá»u kiá»‡n so sÃ¡nh, lá»‡nh Ä‘iá»u khiá»ƒn) vÃ  cÃ¡c tháº» huy hiá»‡u badge thÃ´ng sá»‘ (`[DI0]`, `15000ms`, `EQ`, `DO0`) Ä‘á»u Ä‘Æ°á»£c thiáº¿t láº­p `HorizontalAlignment="Center"` vÃ  `TextAlignment="Center"`.
- Bá»‘ cá»¥c khá»‘i trá»Ÿ nÃªn Ä‘á»‘i xá»©ng, cÃ¢n Ä‘á»‘i hoÃ n háº£o vÃ  chuyÃªn nghiá»‡p.

### 6.2. DÃ¢y Ná»‘i Tháº³ng Chuáº©n Báº­c Thang PLC (Straight Ladder Rung Wires)
- Chuyá»ƒn sang sá»­ dá»¥ng `nodify:LineConnection` Ä‘i tháº³ng táº¯p tá»« cá»•ng xuáº¥t (Output pin) sang cá»•ng nháº­n (Input pin) theo Ä‘Ãºng chuáº©n báº­c thang Ladder PLC.
- Thiáº¿t láº­p `SourceOffset="0,0"` vÃ  `TargetOffset="0,0"` nháº±m triá»‡t tiÃªu hoÃ n toÃ n khoáº£ng cÃ¡ch offset máº·c Ä‘á»‹nh 14px cá»§a Nodify (nguyÃªn nhÃ¢n khiáº¿n mÅ©i tÃªn trÆ°á»›c Ä‘Ã³ bá»‹ lÆ¡ lá»­ng, há»¥t máº¥t má»™t khoáº£ng trÆ°á»›c khi cháº¡m vÃ o pin nháº­n).
- MÅ©i tÃªn vÃ  dÃ¢y ná»‘i giá» Ä‘Ã¢y cáº¯m liá»n máº¡ch, tiáº¿p xÃºc trá»±c tiáº¿p vÃ  chuáº©n xÃ¡c vÃ o chÃ¢n pin cá»§a khá»‘i Ä‘á»‘i diá»‡n.

### 6.3. Tá»‘i Giáº£n Báº£ng Thuá»™c TÃ­nh (Loáº¡i Bá» Ã” TrÃ¹ng Láº·p GÃ¢y Rá»‘i)
- Loáº¡i bá» hoÃ n toÃ n trÆ°á»ng nháº­p "NhÃ£n ghi chÃº cá»§a khá»‘i" thá»«a thÃ£i trong Inspector.
- Vá»›i khá»‘i Input Tag, ngÆ°á»i dÃ¹ng chá»‰ cáº§n má»™t Ã´ duy nháº¥t lÃ  **TÃªn gá»£i nhá»› (Alias)** (VD: *MÃ¡y Ä‘ang cháº¡y*, *Cáº£m biáº¿n cá»­a*).
- Giao diá»‡n form thuá»™c tÃ­nh trá»Ÿ nÃªn gá»n gÃ ng, trá»±c quan vÃ  khÃ´ng cÃ²n bá»‹ trÃ¹ng láº·p khÃ¡i niá»‡m.

### 6.4. DÃ¢y Ná»‘i Tháº³ng Thuáº§n TÃºy (Loáº¡i Bá» MÅ©i TÃªn - Pure Straight Line Connections)
- Äáº·t `ArrowEnds="None"` trÃªn `nodify:LineConnection` á»Ÿ cáº£ [`NodeTemplates.xaml`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Styles/NodeTemplates.xaml) vÃ  [`LogicEditorView.xaml`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Views/LogicEditorView.xaml).
- DÃ¢y ná»‘i giá» Ä‘Ã¢y lÃ  má»™t Ä‘Æ°á»ng tháº³ng sáº¡ch sáº½ (`#475569`, 2px), ná»‘i trá»±c tiáº¿p tá»« tÃ¢m pin xuáº¥t sang tÃ¢m pin nháº­n theo Ä‘Ãºng phong cÃ¡ch sÆ¡ Ä‘á»“ báº­c thang ladder cÃ´ng nghiá»‡p, khÃ´ng cÃ²n cÃ¡c Ä‘áº§u mÅ©i tÃªn thá»«a gÃ¢y rá»‘i máº¯t.

---

## 7. Thiáº¿t Káº¿ Láº¡i ToÃ n Diá»‡n Card Khá»‘i (Triá»‡t TiÃªu HoÃ n ToÃ n ThÃ´ng Tin TrÃ¹ng Láº·p)

### 7.1. PhÃ¢n TÃ­ch Lá»—i Thiáº¿t Káº¿ CÅ© (Váº¥n Äá» NgÆ°á»i DÃ¹ng Pháº£n Ãnh)
- Khá»‘i **Input Tag** trÆ°á»›c Ä‘Ã¢y hiá»ƒn thá»‹ thÃ´ng tin cá»±c ká»³ láº·p láº¡i vÃ  rá»‘i máº¯t:
  - GÃ³c trÃªn bÃªn trÃ¡i tiÃªu Ä‘á» ghi cá»©ng: `"Input Tag"`.
  - GÃ³c trÃªn bÃªn pháº£i huy hiá»‡u ghi: `"DI0"`.
  - Giá»¯a thÃ¢n khá»‘i ghi TÃªn gá»£i nhá»›: `"MÃ¡y Ä‘ang cháº¡y"`.
  - Ngay dÆ°á»›i thÃ¢n khá»‘i láº¡i xuáº¥t hiá»‡n má»™t huy hiá»‡u ná»¯a cÅ©ng ghi: `"DI0"`.
- Hai huy hiá»‡u cÃ¹ng ná»™i dung `"DI0"` xuáº¥t hiá»‡n cáº¡nh nhau trÃªn má»™t khá»‘i nhá» chá»‰ cao 75px, Ä‘á»“ng thá»i tiÃªu Ä‘á» `"Input Tag"` lÃ  thá»«a thÃ£i.
- TrÃªn khá»‘i **Action Node**, chuá»—i `"DO0"` cÅ©ng bá»‹ láº·p láº¡i trong cáº£ biá»ƒu thá»©c thá»±c thi (`DO0 = 1`) láº«n tÃªn hiá»ƒn thá»‹ (`ÄÃ¨n cÃ²i (DO0)`).

### 7.2. Giáº£i PhÃ¡p TÃ¡i Thiáº¿t Káº¿ Chuáº©n Senior Industrial UI
1. **Khá»‘i Input Tag ([`InputNodeViewModel.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/ViewModels/GraphNodeViewModel.cs))**:
   - **TiÃªu Ä‘á» khá»‘i (Header Left)**: Hiá»ƒn thá»‹ trá»±c tiáº¿p **MÃ£ Tag** chÃ­nh thá»©c cá»§a ngÃµ vÃ o: `ðŸ·ï¸ DI0` (Font Consolas/Segoe UI Ä‘áº­m 11.5px, mÃ u `#0F172A`), giÃºp nháº­n diá»‡n tá»©c thÃ¬ khá»‘i mÃ  khÃ´ng cáº§n tá»« ngá»¯ chung chung `"Input Tag"`.
   - **Huy hiá»‡u loáº¡i Tag (Header Right)**: Hiá»ƒn thá»‹ phÃ¢n loáº¡i pháº§n cá»©ng: `DI` (Digital Input), `AI` (Analog Input), `VFLAG`, `VREG` (huy hiá»‡u xanh cÃ´ng nghá»‡ tinh gá»n).
   - **Ná»™i dung thÃ¢n khá»‘i (Body Center)**: Hiá»ƒn thá»‹ rÃµ nÃ©t **TÃªn gá»£i nhá»› (Alias)**: `MÃ¡y Ä‘ang cháº¡y` (Font 13px SemiBold, cÄƒn giá»¯a tuyá»‡t Ä‘á»‘i). Náº¿u chÆ°a nháº­p alias, hiá»ƒn thá»‹ mÃ´ táº£ chá»©c nÄƒng ngÃµ vÃ o thay vÃ¬ láº·p láº¡i mÃ£ tag.
   - **Huy hiá»‡u tráº¡ng thÃ¡i logic (Body Status)**: Thay tháº¿ huy hiá»‡u `DI0` trÃ¹ng láº·p báº±ng **Tráº¡ng thÃ¡i mÃ´ phá»ng thá»±c táº¿**: `â—‹ OFF (0)` hoáº·c `â— ON (1)` (phÃ¹ há»£p 100% vá»›i tÆ° duy kiá»ƒm tra tÃ­n hiá»‡u PLC/SCADA).
2. **Khá»‘i Action Node ([`ActionNodeViewModel.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/ViewModels/GraphNodeViewModel.cs))**:
   - DÃ²ng biá»ƒu thá»©c: `DO0 = 1`.
   - Huy hiá»‡u Tag Ä‘Ã­ch: Sá»­ dá»¥ng `TargetTagShortText` chá»‰ hiá»ƒn thá»‹ **TÃªn gá»£i nhá»›** `ÄÃ¨n cÃ²i Ä‘á»` (hoáº·c chá»©c nÄƒng ngÃµ ra), loáº¡i bá» hoÃ n toÃ n viá»‡c láº·p láº¡i mÃ£ `(DO0)`.
3. **Káº¿t quáº£ Ä‘áº¡t Ä‘Æ°á»£c**:
   - **0% thÃ´ng tin láº·p láº¡i** (Zero Redundancy): Má»—i chi tiáº¿t chá»¯ trÃªn khá»‘i Ä‘á»u cÃ³ Ã½ nghÄ©a Ä‘á»‹nh danh hoáº·c giÃ¡m sÃ¡t tráº¡ng thÃ¡i riÃªng biá»‡t.
   - Bá»‘ cá»¥c thoÃ¡ng máº¯t, cÃ¢n xá»©ng, hiá»‡n Ä‘áº¡i vÃ  chuáº©n nghiá»‡p vá»¥ tá»± Ä‘á»™ng hÃ³a cÃ´ng nghiá»‡p.

---

## 8. HoÃ n Thiá»‡n Há»‡ Thá»‘ng Quáº£n LÃ½ Cáº¥u HÃ¬nh (Project Lifecycle & Import/Export Config)

### 8.1. CÃ¡c Háº¡ng Má»¥c ÄÃ£ Triá»ƒn Khai
1. **Lá»›p Tá»‡p Dá»± Ãn ToÃ n Diá»‡n (`*.splc` - JSON Schema v1)**:
   - **MÃ£ nguá»“n**: [`ProjectModel.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Models/ProjectModel.cs), [`ProjectFileService.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Services/ProjectFileService.cs).
   - ÄÃ³ng gÃ³i Ä‘áº§y Ä‘á»§ 100% dá»¯ liá»‡u dá»± Ã¡n:
     - Metadata (TÃªn dá»± Ã¡n, TÃ¡c giáº£, NgÃ y táº¡o, NgÃ y cáº­p nháº­t, `schemaVersion: 1`, Thiáº¿t bá»‹ Ä‘Ã­ch).
     - ToÃ n bá»™ danh má»¥c Tag I/O (`Index`, `Name`, `Alias`, `Kind`, `Channel`, `Group`, `Value`).
     - ToÃ n bá»™ Ä‘á»“ thá»‹ Logic Canvas (Tá»a Ä‘á»™ $X, Y$ cá»§a tá»«ng khá»‘i, cÃ¡c tham sá»‘ `ForMs`, `CompareOp`, `ThresholdLo/Hi`, `Negate`, `ActionParam`, vÃ  chÃ¢n káº¿t ná»‘i liÃªn káº¿t).
   - Thao tÃ¡c: `Táº¡o má»›i (Ctrl+N)`, `Má»Ÿ dá»± Ã¡n (Ctrl+O)`, `LÆ°u dá»± Ã¡n (Ctrl+S)`, `LÆ°u thÃ nh file khÃ¡c... (Ctrl+Shift+S)`.
   - CÆ¡ cháº¿ ghi file an toÃ n qua file táº¡m `.tmp` chá»‘ng há»ng dá»¯ liá»‡u khi máº¥t Ä‘iá»‡n Ä‘á»™t ngá»™t.
2. **Theo DÃµi Thay Äá»•i ChÆ°a LÆ°u (Unsaved Changes & UX Lifecycle)**:
   - Theo dÃµi cá» `IsProjectDirty` trÃªn toÃ n bá»™ tÆ°Æ¡ng tÃ¡c: thÃªm/xÃ³a/di chuyá»ƒn khá»‘i, sá»­a tÃªn gá»£i nhá»›, ná»‘i/ngáº¯t dÃ¢y.
   - TiÃªu Ä‘á» cá»­a sá»• Ä‘á»™ng: `SimplePLC Studio - [TÃªnDá»±Ãn*] [Cá»•ng COM / Offline]`.
   - Há»™p thoáº¡i há»i lÆ°u xÃ¡c nháº­n (Yes/No/Cancel) khi táº¡o má»›i (`Ctrl+N`), má»Ÿ file khÃ¡c (`Ctrl+O`), hoáº·c thoÃ¡t á»©ng dá»¥ng (`Alt+F4` qua sá»± kiá»‡n `Window_Closing`).
   - Danh sÃ¡ch 5 dá»± Ã¡n má»Ÿ gáº§n nháº¥t (`Recent Projects`) tá»± Ä‘á»™ng lÆ°u trá»¯ trong `%APPDATA%/SimplePLC/recent_projects.json`.
3. **Cáº¥u HÃ¬nh Nhá»‹ PhÃ¢n Vi Äiá»u Khiá»ƒn (`*.bin` 28-Byte MCU)**:
   - **Export Binary**: Gá»i [`RuleBinaryEncoder.EncodeProgram`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Services/RuleBinaryEncoder.cs) xuáº¥t tá»‡p nhá»‹ phÃ¢n $N \times 28$ byte kÃ¨m tÃ­nh toÃ¡n vÃ  thÃ´ng bÃ¡o mÃ£ kiá»ƒm tra toÃ n váº¹n CRC-32/ISO-HDLC.
   - **Import Binary**: Gá»i `RuleBinaryEncoder.DecodeProgram` giáº£i mÃ£ file `.bin` ngÆ°á»£c láº¡i thÃ nh danh sÃ¡ch Rule hoÃ n chá»‰nh, tá»± Ä‘á»™ng map cÃ¡c chÃ¢n Tag nguá»“n/Ä‘Ã­ch vÃ  náº¡p vÃ o Rule Table Ä‘á»ƒ kiá»ƒm tra/chá»‰nh sá»­a.
4. **Báº£ng Danh Má»¥c Biáº¿n I/O (`*.csv`)**:
   - **MÃ£ nguá»“n**: [`TagCsvService.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Services/TagCsvService.cs).
   - **Export CSV**: Xuáº¥t báº£ng I/O chuáº©n UTF-8 BOM Ä‘á»ƒ má»Ÿ trÃªn Microsoft Excel mÃ  khÃ´ng bá»‹ lá»—i font tiáº¿ng Viá»‡t.
   - **Import CSV**: Äá»c vÃ  phÃ¢n tÃ­ch báº£ng I/O tá»« file CSV, tá»± Ä‘á»™ng gÃ¡n tÃªn gá»£i nhá»› (Alias), kÃªnh pháº§n cá»©ng, phÃ¢n loáº¡i ngÃµ vÃ o/ngÃµ ra mÃ  khÃ´ng cáº§n gÃµ tay.
5. **Äa NgÃ´n Ngá»¯ (Localization)**:
   - Cáº­p nháº­t Ä‘áº§y Ä‘á»§ cÃ¡c nhÃ£n menu, submenu vÃ  há»™p thoáº¡i thÃ´ng bÃ¡o trong [`LocalizationService.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Services/LocalizationService.cs).

### 8.2. Káº¿t Quáº£ Kiá»ƒm Thá»­ Tá»± Äá»™ng ToÃ n Diá»‡n
Bá»• sung 3 bá»™ test tá»± Ä‘á»™ng má»›i:
- [`ProjectFileServiceTests.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio.Tests/ProjectFileServiceTests.cs): Kiá»ƒm tra roundtrip LÆ°u $\to$ Má»Ÿ dá»± Ã¡n `.splc` báº£o toÃ n 100% thuá»™c tÃ­nh, tá»a Ä‘á»™ $X,Y$ vÃ  dÃ¢y ná»‘i; kiá»ƒm tra báº¯t lá»—i file khÃ´ng tá»“n táº¡i; kiá»ƒm tra quáº£n lÃ½ danh sÃ¡ch Recent Projects.
- [`RuleBinaryDecoderTests.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio.Tests/RuleBinaryDecoderTests.cs): Kiá»ƒm tra roundtrip MÃ£ hÃ³a $\to$ Giáº£i mÃ£ nhá»‹ phÃ¢n 28-byte; kiá»ƒm tra báº¯t lá»—i bá»™ Ä‘á»‡m há»ng.
- [`TagCsvServiceTests.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio.Tests/TagCsvServiceTests.cs): Kiá»ƒm tra roundtrip Xuáº¥t $\to$ Nháº­p báº£ng Tag CSV cÃ³ chá»©a dáº¥u pháº©y, dáº¥u ngoáº·c kÃ©p vÃ  tiáº¿ng Viá»‡t cÃ³ dáº¥u.

```text
Passed!  - Failed: 0, Passed: 33, Skipped: 0, Total: 33, Duration: 96 ms - SimplePLC.Studio.Tests.dll (net8.0)
Build succeeded: 0 Warning(s), 0 Error(s)
```
ToÃ n bá»™ **33/33 Unit Tests** Ä‘á»u Ä‘áº¡t káº¿t quáº£ 100%. ÄÃ£ commit vÃ  push trá»±c tiáº¿p lÃªn nhÃ¡nh `dev` (`3c8b34e`).

---

## 9. Kháº¯c Phá»¥c DÃ¢y Ná»‘i Co DÃ£n Linh Hoáº¡t KhÃ´ng Bá»‹ Gáº¥p KhÃºc Zigzag

### 9.1. PhÃ¢n TÃ­ch NguyÃªn NhÃ¢n Gá»‘c Rá»… (Root Cause Analysis)
* **Hiá»‡n tÆ°á»£ng trong áº£nh thá»±c táº¿ (`media_1789122305457.png`)**:
  - Khi kÃ©o hai khá»‘i láº¡i gáº§n nhau (khoáº£ng cÃ¡ch ngang $\Delta X < 60\text{px}$) hoáº·c khi cÃ³ Ä‘á»™ lá»‡ch cao Ä‘á»™ nhá» giá»¯a cá»•ng xuáº¥t vÃ  cá»•ng nháº­p ($\Delta Y \approx 10\text{--}20\text{px}$), dÃ¢y ná»‘i giá»¯a chÃ¢n `Out` (Trigger) vÃ  chÃ¢n `Evt` (Guard) bá»‹ gáº­p ngÆ°á»£c láº¡i thÃ nh hÃ¬nh chá»¯ `Z` / zigzag nhá»n gÃ³c $45^\circ$.
* **NguyÃªn nhÃ¢n**:
  - `nodify:LineConnection` trong thÆ° viá»‡n Nodify cÃ³ thuá»™c tÃ­nh máº·c Ä‘á»‹nh `Spacing = 30`.
  - Khi khÃ´ng Ä‘áº·t `Spacing="0"`, Nodify tá»± Ä‘á»™ng cá»™ng thÃªm Ä‘oáº¡n vÆ°Æ¡n $30\text{px}$ vá» phÃ­a trÆ°á»›c cá»§a Source, vÃ  lÃ¹i $30\text{px}$ vá» phÃ­a trÆ°á»›c cá»§a Target.
  - Khi khoáº£ng cÃ¡ch giá»¯a hai cá»•ng $< 60\text{px}$, tá»a Ä‘á»™ Ä‘iá»ƒm vÆ°Æ¡n cá»§a Source (`X1 + 30`) vÆ°á»£t quÃ¡ Ä‘iá»ƒm Ä‘Ã³n cá»§a Target (`X2 - 30`). Káº¿t quáº£ lÃ  dÃ¢y ná»‘i pháº£i báº» ngÆ°á»£c láº¡i vá» phÃ­a sau Ä‘á»ƒ ná»‘i vÃ o Ä‘iá»ƒm Target, táº¡o ra Ä‘oáº¡n gáº­p khÃºc zigzag báº¥t thÆ°á»ng!
  - Thuá»™c tÃ­nh `ItemContainer.Location` cáº§n thiáº¿t láº­p tÆ°á»ng minh `Mode=TwoWay` Ä‘á»ƒ viá»‡c kÃ©o tháº£ vÃ  cáº­p nháº­t tá»a Ä‘á»™ khá»‘i diá»…n ra liÃªn tá»¥c, mÆ°á»£t mÃ .

### 9.2. Giáº£i PhÃ¡p Triá»ƒn Khai
1. **Thiáº¿t láº­p `Spacing="0"` triá»‡t Ä‘á»ƒ trÃªn cáº£ 2 Template káº¿t ná»‘i**:
   - Trong [`LogicEditorView.xaml`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Views/LogicEditorView.xaml):
     ```xaml
     <DataTemplate x:Key="ConnectionTemplate" DataType="{x:Type vm:ConnectionViewModel}">
         <nodify:LineConnection Source="{Binding Source.Anchor}"
                                Target="{Binding Target.Anchor}"
                                SourceOffset="0,0"
                                TargetOffset="0,0"
                                Spacing="0"
                                ArrowEnds="None"
                                Stroke="#475569"
                                StrokeThickness="2" />
     </DataTemplate>
     ```
   - Trong [`NodeTemplates.xaml`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Styles/NodeTemplates.xaml): cáº¥u hÃ¬nh Ä‘á»“ng bá»™ `Spacing="0"` vÃ  `SourceOffset="0,0"`, `TargetOffset="0,0"`.
2. **KÃ­ch hoáº¡t `Mode=TwoWay` trÃªn `ItemContainer.Location`**:
   - Äáº£m báº£o khi di chuyá»ƒn báº¥t ká»³ khá»‘i nÃ o trÃªn canvas (kÃ©o gáº§n láº¡i, Ä‘áº©y ra xa, di chuyá»ƒn chÃ©o), vá»‹ trÃ­ chÃ¢n cá»•ng Ä‘Æ°á»£c cáº­p nháº­t láº­p tá»©c vÃ  dÃ¢y ná»‘i tá»± Ä‘á»™ng kÃ©o dÃ£n / thu ngáº¯n linh hoáº¡t theo Ä‘Æ°á»ng tháº³ng trá»±c tiáº¿p tá»« tÃ¢m chÃ¢n pin nÃ y sang tÃ¢m chÃ¢n pin kia.
3. **BÃ i kiá»ƒm tra tá»± Ä‘á»™ng xUnit**:
   - Bá»• sung kiá»ƒm thá»­ tá»± Ä‘á»™ng [`LineConnection_ZeroSpacing_GeneratesDirectStraightLine`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio.Tests/LogicEditorViewModelTests.cs) Ä‘á»ƒ xÃ¡c minh hÃ¬nh há»c Ä‘Æ°á»ng ná»‘i á»Ÿ khoáº£ng cÃ¡ch ngáº¯n ($\Delta X=20, \Delta Y=-20$): Ä‘áº£m báº£o khÃ´ng phÃ¡t sinh báº¥t ká»³ Ä‘iá»ƒm gáº­p khÃºc trung gian nÃ o ($130$ hoáº·c $90$).

### 9.3. Káº¿t Quáº£ XÃ¡c Minh
```text
Passed!  - Failed: 0, Passed: 34, Skipped: 0, Total: 34, Duration: 86 ms - SimplePLC.Studio.Tests.dll (net8.0)
Build succeeded: 0 Warning(s), 0 Error(s)
```
ToÃ n bá»™ **34/34 Unit Tests** Ä‘áº¡t 100% Pass. MÃ£ nguá»“n Ä‘Ã£ Ä‘Æ°á»£c commit vÃ  push lÃªn nhÃ¡nh `dev` (`bcddc24`).

---

## 10. XÃ³a Äá»“ng Thá»i Táº¥t Cáº£ CÃ¡c Khá»‘i Khi QuÃ©t Chá»n (Box Selection / Multi-Node Deletion)

### 10.1. PhÃ¢n TÃ­ch NguyÃªn NhÃ¢n Gá»‘c Rá»… (Root Cause Analysis)
* **Hiá»‡n tÆ°á»£ng**:
  - Khi ngÆ°á»i dÃ¹ng kÃ©o chuá»™t quÃ©t chá»n nhiá»u khá»‘i trÃªn khung váº½ (marquee box-select) rá»“i nháº¥n phÃ­m `Delete`, á»©ng dá»¥ng khÃ´ng xÃ³a háº¿t táº¥t cáº£ cÃ¡c khá»‘i Ä‘Ã£ chá»n cÃ¹ng má»™t lÃºc mÃ  chá»‰ xÃ³a tá»«ng khá»‘i má»™t (hoáº·c khÃ´ng xÃ³a náº¿u trÆ°á»›c Ä‘Ã³ chÆ°a click chuá»™t trá»±c tiáº¿p vÃ o má»™t khá»‘i cá»¥ thá»ƒ).
* **NguyÃªn nhÃ¢n**:
  1. `LogicEditorViewModel.DeleteSelectedNode()` chá»‰ thao tÃ¡c trÃªn biáº¿n Ä‘Æ¡n láº» `SelectedNode`:
     - Khi `SelectedNode` bá»‹ xÃ³a, nÃ³ láº¥y `Nodes.LastOrDefault()` gÃ¡n vÃ o `SelectedNode`, khiáº¿n ngÆ°á»i dÃ¹ng pháº£i nháº¥n Delete nhiá»u láº§n Ä‘á»ƒ xÃ³a tá»«ng khá»‘i má»™t ("delete tá»«ng khá»‘i").
     - HoÃ n toÃ n bá» qua cá» `node.IsSelected` cá»§a cÃ¡c khá»‘i náº±m trong vÃ¹ng quÃ©t chá»n.
  2. Sá»± kiá»‡n phÃ­m `Delete` trong [`LogicEditorView.xaml.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Views/LogicEditorView.xaml.cs) chá»‰ kÃ­ch hoáº¡t khi `vm.SelectedNode != null`. Khi quÃ©t chá»n trÃªn canvas mÃ  chÆ°a click vÃ o khá»‘i nÃ o, `SelectedNode` cÃ³ thá»ƒ lÃ  `null` nÃªn phÃ­m `Delete` bá»‹ vÃ´ hiá»‡u hÃ³a.
  3. `SelectedItems` trÃªn `nodify:NodifyEditor` trÆ°á»›c Ä‘Ã³ Ä‘áº·t `Mode=OneWay` vÃ  trá» vÃ o thuá»™c tÃ­nh chÆ°a khai bÃ¡o trong ViewModel, khiáº¿n danh sÃ¡ch cÃ¡c pháº§n tá»­ Ä‘Æ°á»£c chá»n khÃ´ng Ä‘á»“ng bá»™.

### 10.2. Giáº£i PhÃ¡p Triá»ƒn Khai
1. **Bá»• sung vÃ  Ä‘á»“ng bá»™ táº­p há»£p Ä‘a khá»‘i `SelectedNodes`**:
   - Trong [`LogicEditorViewModel.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/ViewModels/LogicEditorViewModel.cs):
     - Khai bÃ¡o `public ObservableCollection<GraphNodeViewModel> SelectedNodes { get; } = new();`.
     - Láº¯ng nghe `PropertyChanged` cá»§a tá»«ng node: khi `node.IsSelected` thay Ä‘á»•i (do ngÆ°á»i dÃ¹ng quÃ©t chá»n trÃªn canvas), tá»± Ä‘á»™ng thÃªm/bá»›t vÃ o `SelectedNodes` vÃ  cáº­p nháº­t `SelectedNode` hiá»ƒn thá»‹ Inspector.
     - Láº¯ng nghe `SelectedNodes.CollectionChanged` Ä‘á»“ng bá»™ 2 chiá»u vá»›i Nodify selection.
2. **NÃ¢ng cáº¥p phÆ°Æ¡ng thá»©c `DeleteSelectedNode()` xÃ³a Ä‘á»“ng thá»i táº¥t cáº£ cÃ¡c khá»‘i**:
   - Thu tháº­p toÃ n bá»™ cÃ¡c khá»‘i cÃ³ `IsSelected == true`, cÃ¡c khá»‘i trong `SelectedNodes` vÃ  `SelectedNode`.
   - TÃ¬m vÃ  gá»¡ bá» toÃ n bá»™ táº¥t cáº£ cÃ¡c dÃ¢y káº¿t ná»‘i (`Connections`) dÃ­nh lÃ­u Ä‘áº¿n **báº¥t ká»³** khá»‘i nÃ o bá»‹ xÃ³a trong má»™t lÆ°á»£t duy nháº¥t.
   - Cáº­p nháº­t láº¡i cá» `IsConnected = false` cho cÃ¡c chÃ¢n pin cá»§a cÃ¡c khá»‘i ngoÃ i vÃ¹ng quÃ©t cÃ²n tá»“n táº¡i.
   - XÃ³a sáº¡ch toÃ n bá»™ cÃ¡c khá»‘i Ä‘Æ°á»£c chá»n khá»i danh sÃ¡ch `Nodes` vÃ  `SelectedNodes`.
   - Tá»± Ä‘á»™ng biÃªn dá»‹ch láº¡i Rules (`CompileAndSaveRules(true)`).
3. **Cáº­p nháº­t giao diá»‡n XAML vÃ  Code-Behind**:
   - Trong [`LogicEditorView.xaml`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Views/LogicEditorView.xaml):
     - `SelectedItems="{Binding SelectedNodes}"`
     - `<Setter Property="IsSelected" Value="{Binding IsSelected, Mode=TwoWay}" />`
   - Trong [`LogicEditorView.xaml.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Views/LogicEditorView.xaml.cs):
     - Äiá»u kiá»‡n phÃ­m `Delete`: cho phÃ©p xÃ³a khi `vm.SelectedNode != null || vm.SelectedNodes.Count > 0 || vm.Nodes.Any(n => n.IsSelected)`.
     - Nháº¥p chuá»™t pháº£i vÃ o khá»‘i náº±m trong vÃ¹ng quÃ©t chá»n: báº£o lÆ°u toÃ n bá»™ vÃ¹ng quÃ©t chá»n Ä‘á»ƒ thao tÃ¡c menu ngá»¯ cáº£nh (Context Menu) "XÃ³a khá»‘i Ä‘ang chá»n".

### 10.3. Káº¿t Quáº£ Kiá»ƒm Thá»­ Tá»± Äá»™ng (Unit Tests)
Bá»• sung 2 bÃ i test tá»± Ä‘á»™ng má»›i trong [`LogicEditorViewModelTests.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio.Tests/LogicEditorViewModelTests.cs):
1. `DeleteSelectedNode_MultipleSelectedNodes_DeletesAllSelectedNodesAndAllConnections`: QuÃ©t chá»n 4 khá»‘i (Input, Trigger, Guard, Action) vÃ  nháº¥n Delete $\to$ XÃ³a sáº¡ch 100% cáº£ 4 khá»‘i vÃ  3 dÃ¢y ná»‘i trong 1 lá»‡nh duy nháº¥t.
2. `DeleteSelectedNode_SubsetSelected_DeletesOnlySelectedAndCleansConnectedPins`: QuÃ©t chá»n 2 khá»‘i á»Ÿ giá»¯a (Trigger, Guard) $\to$ XÃ³a sáº¡ch 2 khá»‘i vÃ  dÃ¢y ná»‘i dÃ­nh lÃ­u, Input vÃ  Action cÃ²n láº¡i chuyá»ƒn tráº¡ng thÃ¡i `IsConnected = false` an toÃ n.

```text
Passed!  - Failed: 0, Passed: 36, Skipped: 0, Total: 36, Duration: 97 ms - SimplePLC.Studio.Tests.dll (net8.0)
Build succeeded: 0 Warning(s), 0 Error(s)
```
ToÃ n bá»™ **36/36 Unit Tests** Ä‘áº¡t káº¿t quáº£ tuyá»‡t Ä‘á»‘i (100% Pass). ÄÃ£ commit vÃ  push lÃªn nhÃ¡nh `dev` (`0253086`).

---

## 11. Sá»­a Lá»—i Tháº£ Khá»‘i Sai Vá»‹ TrÃ­ (Drop Position Jump) vÃ  Bá» Chá»n Khá»‘i CÅ© Khi Chá»n Khá»‘i KhÃ¡c (Exclusive Selection)

### 11.1. PhÃ¢n TÃ­ch NguyÃªn NhÃ¢n Gá»‘c Rá»… (Root Cause Analysis)
1. **Lá»—i tháº£ khá»‘i bá»‹ nháº£y vá»‹ trÃ­ (Drop Position Jump)**:
   - Trong [`LogicEditorView.xaml.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Views/LogicEditorView.xaml.cs), sá»± kiá»‡n `NodifyEditor_Drop` trÆ°á»›c Ä‘Ã¢y sá»­ dá»¥ng `e.GetPosition((IInputElement)sender)`.
   - `e.GetPosition(sender)` tráº£ vá» tá»a Ä‘á»™ pixel mÃ n hÃ¬nh (screen/control space) tÆ°Æ¡ng Ä‘á»‘i cá»§a `NodifyEditor` mÃ  **khÃ´ng há» tÃ­nh Ä‘áº¿n** Ä‘á»™ dá»‹ch chuyá»ƒn khung nhÃ¬n (`ViewportLocation`) vÃ  tá»‰ lá»‡ thu phÃ³ng (`ViewportZoom`). Khi ngÆ°á»i dÃ¹ng Ä‘Ã£ cuá»™n (pan) khung váº½ hoáº·c phÃ³ng to/thu nhá» (zoom), tá»a Ä‘á»™ gÃ¡n cho khá»‘i má»›i bá»‹ lá»‡ch hoÃ n toÃ n so vá»›i con trá» chuá»™t thá»±c táº¿ táº¡i thá»i Ä‘iá»ƒm tháº£.
2. **Lá»—i khá»‘i cÅ© váº«n cÃ²n Ä‘Æ°á»£c chá»n (Selection Not Cleared)**:
   - Khi kÃ©o tháº£ khá»‘i má»›i tá»« thanh cÃ´ng cá»¥ ra canvas, phÆ°Æ¡ng thá»©c `AddNode` chá»‰ gÃ¡n `newNode.IsSelected = true` mÃ  khÃ´ng há»§y chá»n cÃ¡c khá»‘i Ä‘Ã£ chá»n trÆ°á»›c Ä‘Ã³, khiáº¿n khá»‘i cÅ© váº«n sÃ¡ng viá»n chá»n song song vá»›i khá»‘i má»›i.
   - Khi nháº¥p chuá»™t trÃ¡i (single-click) vÃ o má»™t khá»‘i khÃ¡c trÃªn canvas, cÆ¡ cháº¿ máº·c Ä‘á»‹nh cá»§a Nodify hoáº·c tÆ°Æ¡ng tÃ¡c ItemContainer khÃ´ng tá»± Ä‘á»™ng há»§y chá»n cÃ¡c khá»‘i khÃ¡c náº¿u khÃ´ng click ra ngoÃ i canvas trá»‘ng, dáº«n Ä‘áº¿n nhiá»u khá»‘i Ä‘á»“ng thá»i Ä‘Æ°á»£c chá»n gÃ¢y hiá»ƒu nháº§m hiá»‡u á»©ng thá»‹ giÃ¡c.

### 11.2. Giáº£i PhÃ¡p Triá»ƒn Khai
1. **Chuyá»ƒn Ä‘á»•i tá»a Ä‘á»™ tháº£ chÃ­nh xÃ¡c theo Editor Pan/Zoom**:
   - Sá»­ dá»¥ng API tÃ­ch há»£p chuyÃªn biá»‡t cá»§a thÆ° viá»‡n Nodify: `editor.GetLocationInsideEditor(e)`.
   - API nÃ y tá»± Ä‘á»™ng tÃ­nh toÃ¡n ma tráº­n chuyá»ƒn Ä‘á»•i tá»« tá»a Ä‘á»™ chuá»™t cá»§a `DragEventArgs` sang Ä‘Ãºng tá»a Ä‘á»™ Ä‘á»“ thá»‹ (Graph Coordinate Space) tÆ°Æ¡ng á»©ng vá»›i vá»‹ trÃ­ con trá» chuá»™t thá»±c táº¿, báº¥t ká»ƒ canvas Ä‘ang pan tá»›i Ä‘Ã¢u hay zoom á»Ÿ tá»‰ lá»‡ nÃ o.
2. **Há»§y chá»n toÃ n bá»™ khá»‘i cÅ© khi tháº£ khá»‘i má»›i**:
   - ThÃªm phÆ°Æ¡ng thá»©c `DeselectAllNodes()` trong [`LogicEditorViewModel.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/ViewModels/LogicEditorViewModel.cs).
   - Trong `AddNode(string nodeType, Point position)`: gá»i `DeselectAllNodes()` trÆ°á»›c khi thÃªm vÃ  kÃ­ch hoáº¡t `newNode.IsSelected = true`.
3. **CÆ¡ cháº¿ chá»n Ä‘á»™c quyá»n khi click chuá»™t (Single-Click Exclusive Selection)**:
   - ThÃªm phÆ°Æ¡ng thá»©c `SelectExclusive(GraphNodeViewModel node)` trong [`LogicEditorViewModel.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/ViewModels/LogicEditorViewModel.cs): bá» cá» `IsSelected = false` cho má»i khá»‘i khÃ¡c, lÃ m sáº¡ch `SelectedNodes`, vÃ  chá»‰ chá»n duy nháº¥t khá»‘i Ä‘Ã­ch.
   - Trong [`LogicEditorView.xaml.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Views/LogicEditorView.xaml.cs):
     - Táº¡i `ItemContainer_PreviewMouseDown`: náº¿u ngÆ°á»i dÃ¹ng click chuá»™t trÃ¡i thÃ´ng thÆ°á»ng (khÃ´ng kÃ¨m phÃ­m Ctrl hoáº·c Shift) vÃ o má»™t khá»‘i chÆ°a chá»n, láº­p tá»©c gá»i `vm.SelectExclusive(node)`.
     - Bá»• sung `ItemContainer_PreviewMouseUp`: náº¿u click chuá»™t táº¡i chá»— vÃ o má»™t khá»‘i Ä‘ang náº±m trong nhÃ³m nhiá»u khá»‘i Ä‘Æ°á»£c chá»n (mÃ  khÃ´ng pháº£i thao tÃ¡c kÃ©o rÃª di chuyá»ƒn cáº£ nhÃ³m $\Delta < 4\text{px}$), há»‡ thá»‘ng sáº½ tá»± Ä‘á»™ng thu gá»n vÃ¹ng chá»n vá» duy nháº¥t khá»‘i Ä‘Ã³ thÃ´ng qua `vm.SelectExclusive(node)`.
     - Váº«n báº£o lÆ°u trá»n váº¹n thao tÃ¡c quÃ©t chá»n Ä‘a khá»‘i (Marquee / Box Selection) vÃ  chá»n cá»™ng dá»“n (Ctrl/Shift-click).

### 11.3. Káº¿t Quáº£ Kiá»ƒm Thá»­ Tá»± Äá»™ng (Unit Tests)
Bá»• sung 2 bÃ i test tá»± Ä‘á»™ng chuyÃªn sÃ¢u trong [`LogicEditorViewModelTests.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio.Tests/LogicEditorViewModelTests.cs):
1. `AddNode_DeselectsPreviouslySelectedNodes_AndSelectsNewNode`: XÃ¡c minh khi kÃ©o tháº£ khá»‘i má»›i vÃ o canvas, cÃ¡c khá»‘i cÅ© Ä‘Æ°á»£c chá»n trÆ°á»›c Ä‘Ã³ láº­p tá»©c bá»‹ há»§y chá»n, vÃ  chá»‰ duy nháº¥t khá»‘i má»›i Ä‘Æ°á»£c chá»n.
2. `SelectExclusive_DeselectsAllOtherNodes_AndExclusivelySelectsTarget`: XÃ¡c minh khi chá»n Ä‘á»™c quyá»n má»™t khá»‘i, toÃ n bá»™ cÃ¡c khá»‘i khÃ¡c Ä‘á»u bá»‹ há»§y chá»n vÃ  danh sÃ¡ch `SelectedNodes` chá»‰ chá»©a duy nháº¥t khá»‘i Ä‘Ã³.

```text
Passed!  - Failed: 0, Passed: 38, Skipped: 0, Total: 38, Duration: 95 ms - SimplePLC.Studio.Tests.dll (net8.0)
Build succeeded: 0 Warning(s), 0 Error(s)
```
ToÃ n bá»™ **38/38 Unit Tests** Ä‘áº¡t káº¿t quáº£ 100% Pass. ÄÃ£ commit vÃ  push mÃ£ nguá»“n lÃªn nhÃ¡nh `dev` (`555bc59`).

---

## 12. Tinh Chá»‰nh LÆ°á»›i Äá»“ Thá»‹ (Fine 10px Grid), Báº¯t DÃ­nh Tá»± Äá»™ng (Auto-Snapping) vÃ  CÃ´ng Cá»¥ CÄƒn Tháº³ng HÃ ng Ngang (Horizontal Alignment)

### 12.1. PhÃ¢n TÃ­ch NguyÃªn NhÃ¢n Gá»‘c Rá»… (Root Cause Analysis)
* **Hiá»‡n tÆ°á»£ng**:
  - NgÆ°á»i dÃ¹ng pháº£n Ã¡nh kÃ©o rÃª cÃ¡c khá»‘i cáº£m tháº¥y bá»‹ giáº­t cá»¥c ("cÃ¡c lÆ°á»›i trong Ã´ khÃ´ng Ä‘Æ°á»£c má»‹n") vÃ  ráº¥t khÃ³ Ä‘á»ƒ cÄƒn cÃ¡c khá»‘i náº±m trÃªn cÃ¹ng má»™t hÃ ng ngang tháº³ng táº¯p.
* **NguyÃªn nhÃ¢n ká»¹ thuáº­t**:
  1. `GridCellSize` trÆ°á»›c Ä‘Ã¢y Ä‘áº·t báº±ng `20`:
     - Trong thuáº­t toÃ¡n kÃ©o rÃª cá»§a Nodify (`DraggingSimple`), Ä‘á»™ dá»‹ch chuyá»ƒn tá»©c thá»i cá»§a khá»‘i Ä‘Æ°á»£c tÃ­nh báº±ng:
        `Delta_snap = floor(Delta_mouse / GridCellSize) * GridCellSize`
     - Vá»›i `GridCellSize = 20`, má»—i bÆ°á»›c dá»‹ch chuyá»ƒn chuá»™t bá»‹ nháº£y tá»«ng náº¥c 20px ráº¥t lá»›n (thay vÃ¬ di chuyá»ƒn mÆ°á»£t mÃ ), táº¡o cáº£m giÃ¡c thÃ´ rÃ¡p vÃ  khÃ³ tinh chá»‰nh vá»‹ trÃ­.
  2. Thuá»™c tÃ­nh `NodifyEditor.EnableSnappingCorrection` bá»‹ táº¯t theo máº·c Ä‘á»‹nh (`false`):
     - Khi káº¿t thÃºc kÃ©o rÃª (`DraggingSimple.End`), náº¿u cá» nÃ y lÃ  `false`, Nodify sáº½ **khÃ´ng hiá»‡u chá»‰nh tá»a Ä‘á»™** `Location` cá»§a khá»‘i vá» bá»™i sá»‘ cá»§a máº¯t lÆ°á»›i, mÃ  giá»¯ nguyÃªn pháº§n tháº­p phÃ¢n ban Ä‘áº§u.
  3. Tá»a Ä‘á»™ tháº£ khá»‘i (`Drop`) khÃ´ng Ä‘Æ°á»£c lÃ m trÃ²n theo máº¯t lÆ°á»›i:
     - Khi tháº£ khá»‘i tá»« toolbox vÃ o canvas, khá»‘i nháº­n tá»a Ä‘á»™ chuá»™t tÃ¹y Ã½ (vÃ­ dá»¥ `Y1 = 123.4` vÃ  `Y2 = 135.8`). Do khi kÃ©o rÃª cáº£ hai Ä‘á»u chá»‰ cá»™ng thÃªm bá»™i sá»‘ 20px, `Y1` vÃ  `Y2` khÃ´ng bao giá» trÃ¹ng nhau, dáº«n Ä‘áº¿n dÃ¢y ná»‘i giá»¯a chÃºng khÃ´ng thá»ƒ náº±m ngang pháº³ng tuyá»‡t Ä‘á»‘i.

### 12.2. Giáº£i PhÃ¡p Triá»ƒn Khai
1. **Tinh chá»‰nh máº¯t lÆ°á»›i má»‹n hÆ¡n (`GridCellSize = 10`)**:
   - Trong [`LogicEditorView.xaml`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Views/LogicEditorView.xaml), cáº¥u hÃ¬nh `GridCellSize="10"`.
   - Giáº£m bÆ°á»›c nháº£y kÃ©o rÃª tá»« 20px xuá»‘ng cÃ²n 10px (má»‹n hÆ¡n gáº¥p 2 láº§n), giÃºp thao tÃ¡c kÃ©o chuá»™t cá»±c ká»³ Ãªm vÃ  dá»… kiá»ƒm soÃ¡t.
2. **KÃ­ch hoáº¡t tá»± Ä‘á»™ng báº¯t dÃ­nh máº¯t lÆ°á»›i (`EnableSnappingCorrection = true`)**:
   - Trong [`LogicEditorView.xaml.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Views/LogicEditorView.xaml.cs), báº­t cá» static:
     `Nodify.NodifyEditor.EnableSnappingCorrection = true;`
   - Báº¥t cá»© khi nÃ o ngÆ°á»i dÃ¹ng tháº£ chuá»™t sau khi di chuyá»ƒn khá»‘i, há»‡ thá»‘ng tá»± Ä‘á»™ng giÃ³ng tá»a Ä‘á»™ $X, Y$ vá» máº¯t lÆ°á»›i 10px gáº§n nháº¥t.
3. **Báº¯t dÃ­nh tá»a Ä‘á»™ khi tháº£ khá»‘i tá»« Toolbox**:
   - Trong `NodifyEditor_Drop`: tá»± Ä‘á»™ng lÃ m trÃ²n tá»a Ä‘á»™ tháº£ vá» bá»™i sá»‘ cá»§a `GridCellSize` ($10\text{px}$):
     $$X = \text{round}(X / 10) \times 10, \quad Y = \text{round}(Y / 10) \times 10$$
   - Khi kÃ©o nhiá»u khá»‘i ra canvas á»Ÿ Ä‘á»™ cao tÆ°Æ¡ng Ä‘Æ°Æ¡ng, cÃ¡c khá»‘i tá»± Ä‘á»™ng hÃ­t vÃ o cÃ¹ng má»™t Ä‘Æ°á»ng lÆ°á»›i $Y$.
4. **Bá»• sung lá»‡nh vÃ  thanh cÃ´ng cá»¥ "CÄƒn tháº³ng hÃ ng ngang" (Horizontal Alignment)**:
   - Trong [`LogicEditorViewModel.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/ViewModels/LogicEditorViewModel.cs):
     - `AlignSelectedNodesHorizontallyCommand`: Tá»± Ä‘á»™ng giÃ³ng hÃ ng ngang táº¥t cáº£ cÃ¡c khá»‘i Ä‘ang chá»n (hoáº·c toÃ n bá»™ Ä‘á»“ thá»‹ náº¿u chÆ°a chá»n) vá» cÃ¹ng má»™t tá»a Ä‘á»™ $Y$ chuáº©n theo máº¯t lÆ°á»›i.
     - `SnapAllNodesToGridCommand`: Báº¯t dÃ­nh toÃ n bá»™ tá»a Ä‘á»™ cÃ¡c khá»‘i vá» bá»™i sá»‘ 10px.
   - Giao diá»‡n:
     - ThÃªm thanh cÃ´ng cá»¥ ná»•i tinh táº¿ á»Ÿ gÃ³c trÃªn bÃªn pháº£i khung váº½ (Top-Right Canvas Toolbar) gá»“m: NÃºt **â” CÄƒn tháº³ng hÃ ng**, NÃºt **âŒ— CÄƒn lÆ°á»›i**, vÃ  Huy hiá»‡u tráº¡ng thÃ¡i **LÆ°á»›i: 10px âœ“**.
     - TÃ­ch há»£p 2 lá»‡nh nÃ y vÃ o Menu ngá»¯ cáº£nh (Context Menu) chuá»™t pháº£i trÃªn tá»«ng khá»‘i.

### 12.3. Káº¿t Quáº£ XÃ¡c Minh (Unit Tests)
Bá»• sung 2 bÃ i test tá»± Ä‘á»™ng chuyÃªn sÃ¢u trong [`LogicEditorViewModelTests.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio.Tests/LogicEditorViewModelTests.cs):
1. `AlignSelectedNodesHorizontally_MultipleSelectedNodes_AlignsToSameY`: Kiá»ƒm tra chá»n 3 khá»‘i cÃ³ Ä‘á»™ cao $Y$ lá»‡ch nhau ($73.4, 126.8, 88.2$), kÃ­ch hoáº¡t lá»‡nh cÄƒn tháº³ng hÃ ng $\to$ Cáº£ 3 khá»‘i nháº­n chÃ­nh xÃ¡c cÃ¹ng má»™t tá»a Ä‘á»™ $Y = 70$.
2. `SnapAllNodesToGrid_SnapsFractionalCoordinatesToMultiplesOfTen`: Kiá»ƒm tra cÃ¡c khá»‘i cÃ³ tá»a Ä‘á»™ láº» ($63.4, 87.6$) vÃ  ($308.2, 142.1$) $\to$ Tá»± Ä‘á»™ng chuáº©n hÃ³a vá» ($60, 90$) vÃ  ($310, 140$).

```text
Passed!  - Failed: 0, Passed: 40, Skipped: 0, Total: 40, Duration: 104 ms - SimplePLC.Studio.Tests.dll (net8.0)
Build succeeded: 0 Warning(s), 0 Error(s)
```
ToÃ n bá»™ **40/40 Unit Tests** Ä‘áº¡t 100% Pass. MÃ£ nguá»“n Ä‘Ã£ Ä‘Æ°á»£c commit vÃ  push lÃªn nhÃ¡nh `dev` (`d2a5da4`).

---

## 13. Äá»“ng Bá»™ Chiá»u Cao Tuyá»‡t Äá»‘i Khá»‘i Card (86px Fixed) & CÆ¡ Cháº¿ Báº¯t DÃ­nh Nam ChÃ¢m ThÃ´ng Minh (Smart Magnetic Alignment)

### 13.1. PhÃ¢n TÃ­ch HÃ¬nh áº¢nh & Báº£n Cháº¥t Váº¥n Äá» Tá»« Pháº£n Há»“i NgÆ°á»i DÃ¹ng
* **HÃ¬nh áº£nh ngÆ°á»i dÃ¹ng cung cáº¥p**:
  ![Lá»‡ch cao Ä‘á»™ giá»¯a 2 khá»‘i](C:\Users\DELL\.gemini\antigravity\brain\f2072594-41fc-4d10-83aa-f90614bb1427\.user_uploaded\media_1789179797681.png)
* **Báº£n cháº¥t nguyÃªn nhÃ¢n cá»‘t lÃµi**:
  1. **KÃ­ch thÆ°á»›c cÃ¡c khá»‘i khÃ´ng Ä‘á»“ng nháº¥t (Variable Dynamic Height)**:
     - TrÆ°á»›c Ä‘Ã¢y, style [`IndustrialNodeStyle`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Styles/NodeTemplates.xaml) khÃ´ng cá»‘ Ä‘á»‹nh `Height`, vÃ  pháº§n Header sá»­ dá»¥ng `Height="Auto"`.
     - PhÃ´ng chá»¯ vÃ  Ä‘á»™ dÃ i ná»™i dung cá»§a tá»«ng loáº¡i khá»‘i (Input, Trigger, Guard, Action) lÃ m chiá»u cao card dao Ä‘á»™ng (vÃ­ dá»¥: khá»‘i Input cao 86px, Trigger cao 82px).
     - Do cÃ¡c chÃ¢n cá»•ng (Connectors) Ä‘Æ°á»£c cÄƒn giá»¯a theo chiá»u dá»c (`VerticalAlignment="Center"`), khi hai khá»‘i cÃ³ chiá»u cao khÃ¡c nhau, dÃ¹ ngÆ°á»i dÃ¹ng cÃ³ giÃ³ng mÃ©p trÃªn báº±ng nhau (`Y_top` trÃ¹ng nhau) thÃ¬ **chÃ¢n cá»•ng váº«n bá»‹ lá»‡ch nhau 2-4px**.
     - NgÆ°á»£c láº¡i, náº¿u ngÆ°á»i dÃ¹ng cá»‘ kÃ©o lá»‡ch Ä‘á»ƒ dÃ¢y ná»‘i tháº³ng thÃ¬ mÃ©p trÃªn cá»§a hai khá»‘i láº¡i bá»‹ cá»c cáº¡ch (Trigger nhÃ´ cao hÆ¡n DI0 nhÆ° trong áº£nh chá»¥p).
  2. **Thiáº¿u cÆ¡ cháº¿ báº¯t dÃ­nh nam chÃ¢m (Magnetic Snap) khi kÃ©o rÃª**:
     - NgÆ°á»i dÃ¹ng pháº£i cÄƒn báº±ng máº¯t thÆ°á»ng vÃ  vi chá»‰nh chuá»™t tá»«ng pixel má»™t cÃ¡ch cÄƒng tháº³ng.
     - Khi nháº£ chuá»™t, náº¿u khÃ´ng cÃ³ cÆ¡ cháº¿ tá»± Ä‘á»™ng báº¯t dÃ­nh vÃ o cao Ä‘á»™ cá»§a khá»‘i Ä‘ang ná»‘i dÃ¢y, khá»‘i sáº½ dá»… dÃ ng lá»‡ch khá»i hÃ ng.

### 13.2. Giáº£i PhÃ¡p Triá»ƒn Khai
1. **Chuáº©n hÃ³a kÃ­ch thÆ°á»›c hÃ¬nh há»c tuyá»‡t Ä‘á»‘i (Fixed 180x86px)**:
   - Trong [`NodeTemplates.xaml`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Styles/NodeTemplates.xaml):
     - `Width = 180`, `Height = 86` cá»‘ Ä‘á»‹nh cho toÃ n bá»™ 4 loáº¡i khá»‘i.
     - Row 0 (Accent Stripe): cá»‘ Ä‘á»‹nh `2.5px`.
     - Row 1 (Header): cá»‘ Ä‘á»‹nh `26px`, font size tiÃªu chuáº©n `11pt`.
     - Row 2 (Body): cá»‘ Ä‘á»‹nh `57.5px`.
     - Vá»‹ trÃ­ tÃ¢m chÃ¢n pin cá»§a Táº¤T Cáº¢ cÃ¡c khá»‘i Ä‘á»u náº±m á»Ÿ cÃ¹ng má»™t khoáº£ng cÃ¡ch cá»‘ Ä‘á»‹nh tÃ­nh tá»« Ä‘á»‰nh:
        `Y_pin = 2.5 + 26 + (57.5 / 2) = 57.25 px`
     - Khi `Y1 = Y2`: Cáº£ Ä‘á»‰nh card, Ä‘Ã¡y card vÃ  dÃ¢y ná»‘i ladder Ä‘á»u **pháº³ng tuyá»‡t Ä‘á»‘i 100%**.
2. **CÆ¡ cháº¿ Báº¯t DÃ­nh Nam ChÃ¢m ThÃ´ng Minh (Smart Magnetic Alignment)**:
   - Trong [`LogicEditorViewModel.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/ViewModels/LogicEditorViewModel.cs):
     - Bá»• sung hÃ m `ApplyMagneticAlignment(GraphNodeViewModel node)`.
     - Khi ngÆ°á»i dÃ¹ng nháº£ chuá»™t sau khi kÃ©o khá»‘i, há»‡ thá»‘ng kiá»ƒm tra cÃ¡c khá»‘i lÃ¢n cáº­n (Æ°u tiÃªn khá»‘i cÃ³ dÃ¢y ná»‘i trá»±c tiáº¿p). Náº¿u Ä‘á»™ lá»‡ch $|\Delta Y| \le 20\text{px}$, khá»‘i tá»± Ä‘á»™ng **hÃ­t cháº·t** vÃ o Ä‘Ãºng cao Ä‘á»™ $Y$ cá»§a khá»‘i kia.
3. **Tá»± Ä‘á»™ng cÄƒn tháº³ng hÃ ng ngay khi ná»‘i dÃ¢y**:
   - Trong `CompleteConnection`: Khi ngÆ°á»i dÃ¹ng kÃ©o dÃ¢y tá»« chÃ¢n Out cá»§a khá»‘i nÃ y sang chÃ¢n In cá»§a khá»‘i kia, náº¿u hai khá»‘i Ä‘ang náº±m xáº¥p xá»‰ ngang hÃ ng ($|\Delta Y| \le 35\text{px}$), khá»‘i Ä‘Ã­ch sáº½ tá»± Ä‘á»™ng cÄƒn tháº³ng hÃ ng ngang vá»›i khá»‘i nguá»“n ngay láº­p tá»©c.
4. **CÄƒn hÃ ng ngang thÃ´ng minh khi chá»‰ chá»n 1 khá»‘i**:
   - Khi chá»n 1 khá»‘i duy nháº¥t vÃ  nháº¥n nÃºt "â” CÄƒn tháº³ng hÃ ng", há»‡ thá»‘ng tá»± Ä‘á»™ng tÃ¬m khá»‘i Ä‘ang ná»‘i dÃ¢y vá»›i nÃ³ vÃ  giÃ³ng tháº³ng hÃ ng vá»›i khá»‘i Ä‘Ã³.

### 13.3. Káº¿t Quáº£ XÃ¡c Minh (Unit Tests)
Bá»• sung 3 bÃ i test tá»± Ä‘á»™ng chuyÃªn sÃ¢u:
1. `ApplyMagneticAlignment_ConnectedNeighborNearby_SnapsToExactSameY`: Khá»‘i Ä‘Æ°á»£c kÃ©o lá»‡ch 7.5px $\to$ Tá»± Ä‘á»™ng hÃ­t nam chÃ¢m vá» $Y = 80$.
2. `CompleteConnection_NodesNearby_AutoAlignsTargetToSourceY`: Vá»«a ná»‘i dÃ¢y $\to$ Tá»± Ä‘á»™ng giÃ³ng tháº³ng hÃ ng ngang hai khá»‘i.
3. `AlignSelectedNodesHorizontally_SingleSelectedNode_AlignsWithConnectedNeighbor`: Chá»‰ chá»n 1 khá»‘i $\to$ Tá»± tÃ¬m khá»‘i liÃªn káº¿t vÃ  cÄƒn tháº³ng hÃ ng.

```text
Passed!  - Failed: 0, Passed: 43, Skipped: 0, Total: 43, Duration: 107 ms - SimplePLC.Studio.Tests.dll (net8.0)
Build succeeded: 0 Warning(s), 0 Error(s)
```
ToÃ n bá»™ **43/43 Unit Tests** Ä‘áº¡t 100% Pass. ÄÃ£ commit vÃ  push mÃ£ nguá»“n lÃªn nhÃ¡nh `dev` (`cd8f711`).

---

## 14. Tá»• Chá»©c Láº¡i CÃ´ng Cá»¥ & CÄƒn Chá»‰nh Theo Chuáº©n Pháº§n Má»m Ká»¹ Thuáº­t (Engineering Tools Restructuring)

### 14.1. YÃªu Cáº§u & PhÃ¢n TÃ­ch Tá»« Pháº£n Há»“i NgÆ°á»i DÃ¹ng
* **YÃªu cáº§u cá»§a ngÆ°á»i dÃ¹ng**:
  1. Loáº¡i bá» thanh cÃ´ng cá»¥ dáº¡ng floating pill lÆ¡ lá»­ng trÃªn gÃ³c canvas Ä‘á»“ thá»‹ ("khÃ´ng cáº§n hiá»‡n pháº§n nÃ y mÃ  hÃ£y cho vÃ o tool").
  2. Bá»• sung tÃ­nh nÄƒng **CÄƒn giá»¯a cÃ¡c khá»‘i / CÄƒn Ä‘á»u khoáº£ng cÃ¡ch** (cÄƒn theo trá»¥c dá»c, phÃ¢n bá»‘ Ä‘á»u khoáº£ng cÃ¡ch ngang vÃ  dá»c).
  3. PhÃ¢n chia, tÃ¡i cáº¥u trÃºc menu cÃ´ng cá»¥ (`MenuTools`, `MenuEdit`, Context Menu) chuyÃªn nghiá»‡p, trá»±c quan theo chuáº©n cÃ¡c pháº§n má»m ká»¹ thuáº­t cÃ´ng nghiá»‡p (nhÆ° Siemens TIA Portal, Altium Designer, AutoCAD, Microsoft Visio).

### 14.2. CÃ¡c Giáº£i PhÃ¡p Triá»ƒn Khai

1. **Gá»¡ bá» hoÃ n toÃ n thanh ná»•i (Floating Pill Bar) trÃªn Canvas**:
   - Trong [`LogicEditorView.xaml`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Views/LogicEditorView.xaml): ÄÃ£ gá»¡ bá» khá»‘i `<Border>` lÆ¡ lá»­ng á»Ÿ gÃ³c trÃªn bÃªn pháº£i canvas, tráº£ láº¡i toÃ n bá»™ khÃ´ng gian thoÃ¡ng Ä‘Ã£ng, táº­p trung cho Ä‘á»“ thá»‹ máº¡ch Ä‘iá»‡n.

2. **Bá»• sung cÃ¡c thuáº­t toÃ¡n CÄƒn chá»‰nh & PhÃ¢n bá»‘ Ä‘á»u (Alignment & Distribution)**:
   - Trong [`LogicEditorViewModel.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/ViewModels/LogicEditorViewModel.cs):
     - `AlignSelectedNodesHorizontally()`: CÄƒn tháº³ng hÃ ng ngang (cÃ¹ng cao Ä‘á»™ Y). Há»— trá»£ quÃ©t nhiá»u khá»‘i, cÄƒn theo khá»‘i liÃªn káº¿t náº¿u chá»‰ chá»n 1 khá»‘i, hoáº·c cÄƒn toÃ n bá»™ Ä‘á»“ thá»‹.
     - `AlignSelectedNodesVertically()`: CÄƒn tháº³ng hÃ ng dá»c (cÃ¹ng tá»a Ä‘á»™ X).
     - `DistributeNodesHorizontally()`: CÄƒn Ä‘á»u khoáº£ng cÃ¡ch ngang giá»¯a cÃ¡c khá»‘i (CÄƒn giá»¯a).
       - Khi chá»n tá»« 3 khá»‘i trá»Ÿ lÃªn (hoáº·c toÃ n bá»™ Ä‘á»“ thá»‹ $\ge 3$ khá»‘i): Giá»¯ cá»‘ Ä‘á»‹nh khá»‘i Ä‘áº§u vÃ  khá»‘i cuá»‘i, phÃ¢n chia khoáº£ng cÃ¡ch Ä‘á»u Ä‘áº·n cho cÃ¡c khá»‘i á»Ÿ giá»¯a.
       - Khi chá»n 1 khá»‘i duy nháº¥t náº±m giá»¯a 2 khá»‘i liÃªn káº¿t: Tá»± Ä‘á»™ng Ä‘Æ°a khá»‘i vÃ o vá»‹ trÃ­ chÃ­nh giá»¯a hai khá»‘i hai bÃªn.
     - `DistributeNodesVertically()`: CÄƒn Ä‘á»u khoáº£ng cÃ¡ch dá»c giá»¯a cÃ¡c khá»‘i.
     - `SnapAllNodesToGrid()`: Báº¯t dÃ­nh cÃ¡c khá»‘i vÃ o máº¯t lÆ°á»›i chuáº©n 10px.

3. **Cáº¥u trÃºc láº¡i Menu Há»‡ Thá»‘ng theo Chuáº©n Pháº§n Má»m Ká»¹ Thuáº­t**:
   - Trong [`MainWindow.xaml`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/MainWindow.xaml):
     - **Menu CÃ´ng Cá»¥ (`MenuTools`)**:
       - ðŸ“ **1. CÄƒn chá»‰nh & Bá»‘ cá»¥c sÆ¡ Ä‘á»“ (`MenuLayoutAndAlign`)**:
         - â” CÄƒn tháº³ng hÃ ng ngang (`AlignSelectedNodesHorizontallyCommand`)
         - â”ƒ CÄƒn tháº³ng hÃ ng dá»c (`AlignSelectedNodesVerticallyCommand`)
         - â«¸ CÄƒn Ä‘á»u khoáº£ng cÃ¡ch ngang / CÄƒn giá»¯a (`DistributeNodesHorizontallyCommand`)
         - â«¶ CÄƒn Ä‘á»u khoáº£ng cÃ¡ch dá»c (`DistributeNodesVerticallyCommand`)
         - âŒ— CÄƒn dÃ­nh vÃ o máº¯t lÆ°á»›i 10px (`SnapAllNodesToGridCommand`)
       - âš¡ **2. BiÃªn dá»‹ch & Kiá»ƒm tra logic**: BiÃªn dá»‹ch Rule (F7), Kiá»ƒm tra tÃ­nh há»£p lá»‡.
       - ðŸ’¾ **3. Quáº£n lÃ½ cáº¥u hÃ¬nh & Xuáº¥t dá»¯ liá»‡u**: Xuáº¥t tá»‡p nhá»‹ phÃ¢n 28-Byte MCU (.bin), Xuáº¥t danh má»¥c Tag (.csv).
       - âš™ï¸ **4. CÃ i Ä‘áº·t & TÃ¹y chá»n há»‡ thá»‘ng**: Äá»•i ngÃ´n ngá»¯ Tiáº¿ng Viá»‡t / English.
     - **Menu Chá»‰nh Sá»­a (`MenuEdit`)**: CÅ©ng Ä‘Æ°á»£c tÃ­ch há»£p danh má»¥c `CÄƒn chá»‰nh & Bá»‘ cá»¥c` Ä‘á»ƒ ngÆ°á»i dÃ¹ng thao tÃ¡c tiá»‡n lá»£i.
     - **Menu Chuá»™t Pháº£i (Context Menu)**: Cáº£ trÃªn tá»«ng khá»‘i node vÃ  trÃªn ná»n trá»‘ng canvas Ä‘á»u cÃ³ menu ngá»¯ cáº£nh nhanh Ä‘á»ƒ thao tÃ¡c cÄƒn chá»‰nh vÃ  phÃ¢n bá»‘ tá»©c thÃ¬.

4. **Äa NgÃ´n Ngá»¯ (Localization)**:
   - Cáº­p nháº­t [`LocalizationService.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Services/LocalizationService.cs) Ä‘áº§y Ä‘á»§ cáº£ tiáº¿ng Viá»‡t vÃ  tiáº¿ng Anh cho cÃ¡c lá»‡nh cÄƒn chá»‰nh vÃ  menu má»›i.

### 14.3. Káº¿t Quáº£ XÃ¡c Minh (Unit Tests)
Bá»• sung 3 bÃ i kiá»ƒm thá»­ tá»± Ä‘á»™ng trong [`LogicEditorViewModelTests.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio.Tests/LogicEditorViewModelTests.cs):
1. `AlignSelectedNodesVertically_MultipleSelectedNodes_AlignsToSameX`: 3 khá»‘i lá»‡ch X $\to$ Sau khi cÄƒn dá»c Ä‘á»u cÃ³ $X = 140$ chÃ­nh xÃ¡c.
2. `DistributeNodesHorizontally_ThreeNodes_DistributesEvenly`: 3 khá»‘i vá»›i tá»a Ä‘á»™ X = (100, 150, 500) $\to$ Sau khi phÃ¢n bá»‘ Ä‘á»u khá»‘i giá»¯a tá»± Ä‘á»™ng dá»‹ch chuyá»ƒn vá» vá»‹ trÃ­ chÃ­nh giá»¯a $X = 300$.
3. `DistributeNodesVertically_ThreeNodes_DistributesEvenly`: 3 khá»‘i vá»›i tá»a Ä‘á»™ Y = (100, 150, 700) $\to$ Sau khi phÃ¢n bá»‘ Ä‘á»u khá»‘i giá»¯a chuyá»ƒn vá» $Y = 400$.

```text
Passed!  - Failed: 0, Passed: 46, Skipped: 0, Total: 46, Duration: 102 ms - SimplePLC.Studio.Tests.dll (net8.0)
Build succeeded: 0 Warning(s), 0 Error(s)
```
ToÃ n bá»™ **46/46 Unit Tests** Ä‘á»u Ä‘áº¡t káº¿t quáº£ tuyá»‡t Ä‘á»‘i (100% Pass). ÄÃ£ push lÃªn nhÃ¡nh `dev` (`4979883`).

---

## 15. Chuáº©n HÃ³a Khá»‘i Guard: 1 ChÃ¢n VÃ o Duy Nháº¥t, Cáº¥u HÃ¬nh Tag Trong Inspector & Triá»‡t TiÃªu DÃ¢y Chá»¯ V

### 15.1. PhÃ¢n TÃ­ch HÃ¬nh áº¢nh & Báº¥t Cáº­p CÅ©
* **HÃ¬nh áº£nh ngÆ°á»i dÃ¹ng cung cáº¥p**:
  ![DÃ¢y cháº½ nhÃ¡nh chá»¯ V giá»¯a Trigger vÃ  Guard](C:\Users\DELL\.gemini\antigravity\brain\f2072594-41fc-4d10-83aa-f90614bb1427\.user_uploaded\media_1789181670809.png)
* **Báº¥t cáº­p cá»‘t lÃµi**:
  1. Khá»‘i `Guard` trÆ°á»›c Ä‘Ã¢y Ä‘á»‹nh nghÄ©a **2 chÃ¢n vÃ o** (`Evt` vÃ  `Tag`) náº±m cáº¡nh nhau bÃªn trÃ¡i. Do Ä‘Ã³ khÃ´ng chÃ¢n nÃ o náº±m á»Ÿ cao Ä‘á»™ tÃ¢m chuáº©n ($57.25\text{px}$).
  2. Khi ná»‘i tá»« chÃ¢n ra cá»§a `Trigger` sang cáº£ 2 chÃ¢n cá»§a `Guard`, dÃ¢y bá»‹ **cháº½ Ä‘Ã´i thÃ nh hÃ¬nh chá»¯ V (nhÆ° nÃ¡ cao su)** vÃ  bá»‹ xiÃªn chÃ©o, khÃ´ng thá»ƒ tháº³ng hÃ ng ngang.
  3. Thiáº¿u tÃ­nh nháº¥t quÃ¡n UX: Khá»‘i `Action` chá»n Tag Ä‘Ã­ch trá»±c tiáº¿p trong báº£ng Thuá»™c tÃ­nh (Inspector), trong khi khá»‘i `Guard` láº¡i táº¡o thÃªm 1 chÃ¢n `Tag` trÃªn sÆ¡ Ä‘á»“ lÃ m rá»‘i máº¯t.

### 15.2. Giáº£i PhÃ¡p Triá»ƒn Khai
1. **Chuáº©n hÃ³a khá»‘i Guard vá» Ä‘Ãºng 1 cá»•ng vÃ o (`In`) vÃ  1 cá»•ng ra (`Out`)**:
   - Trong [`GraphNodeViewModel.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/ViewModels/GraphNodeViewModel.cs):
     - `GuardNodeViewModel`: Chá»‰ cÃ³ duy nháº¥t 1 chÃ¢n vÃ o (`Title = "In"`, `IsEvent = true`) vÃ  1 chÃ¢n ra (`Title = "Out"`, `IsEvent = true`).
     - TÃ¢m cá»•ng náº±m chÃ­nh xÃ¡c á»Ÿ cao Ä‘á»™ $57.25\text{px}$, dÃ¢y ná»‘i tá»« `Trigger` sang `Guard` luÃ´n lÃ  **Ä‘Æ°á»ng tháº³ng ngang táº¯p 100%**.
     - Bá»• sung thuá»™c tÃ­nh `GuardTag` (`[ObservableProperty] private TagModel? _guardTag;`).
     - Cáº­p nháº­t `ConditionExpression` vÃ  `SummaryText` Ä‘á»ƒ hiá»ƒn thá»‹ tÃªn Tag Ä‘iá»u kiá»‡n thá»±c táº¿ (vÃ­ dá»¥: `VFLAG0 == 1`, `AI0 > 85`).
2. **TÃ­ch há»£p chá»n Tag Ä‘iá»u kiá»‡n vÃ o Báº£ng Thuá»™c TÃ­nh (Inspector)**:
   - Trong [`LogicEditorView.xaml`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Views/LogicEditorView.xaml):
     - ThÃªm ComboBox chá»n `GuardTag` tá»« danh má»¥c Tag há»‡ thá»‘ng cÃ¹ng Ã´ nháº­p `Alias` thÃ¢n thiá»‡n, giá»‘ng há»‡t cÃ¡ch chá»n Tag cá»§a khá»‘i `Action`.
3. **Template hiá»ƒn thá»‹ pháº³ng & Tinh gá»n**:
   - Trong [`NodeTemplates.xaml`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Styles/NodeTemplates.xaml): Loáº¡i bá» chá»¯ "Evt" / "Tag" in cáº¡nh chÃ¢n pin, giao diá»‡n pháº³ng chuáº©n cÃ´ng nghiá»‡p.
4. **BiÃªn dá»‹ch & NgÄƒn cháº·n ná»‘i dÃ¢y trÃ¹ng láº·p**:
   - Trong [`RuleCompiler.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Services/RuleCompiler.cs) vÃ  [`LogicEditorViewModel.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/ViewModels/LogicEditorViewModel.cs):
     - Äá»c trá»±c tiáº¿p `guard.GuardTag` cho Rule firmware.
     - `CompleteConnection`: NgÄƒn cháº·n táº¡o káº¿t ná»‘i trÃ¹ng láº·p giá»¯a cÃ¡c cá»•ng.
5. **Äa ngÃ´n ngá»¯ & ThÆ° viá»‡n máº«u**:
   - Cáº­p nháº­t [`LocalizationService.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Services/LocalizationService.cs) vá»›i cÃ¡c chuá»—i `InspectorGuardTag`.
   - Cáº­p nháº­t [`BlueprintsViewModel.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/ViewModels/BlueprintsViewModel.cs) khá»Ÿi táº¡o sáºµn `GuardTag`.

### 15.3. Káº¿t Quáº£ XÃ¡c Minh (Unit Tests)
Bá»• sung bÃ i kiá»ƒm thá»­ tá»± Ä‘á»™ng `GuardNode_SingleInSingleOut_AndConfiguredGuardTag_CompilesCorrectly` trong [`LogicEditorViewModelTests.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio.Tests/LogicEditorViewModelTests.cs):
- XÃ¡c minh Guard cÃ³ Ä‘Ãºng 1 cá»•ng vÃ o ("In") vÃ  1 cá»•ng ra ("Out").
- XÃ¡c minh `ConditionExpression` hiá»ƒn thá»‹ Ä‘Ãºng Tag Ä‘Æ°á»£c chá»n (`Trong ca (in_shift) == 1`).
- XÃ¡c minh chuá»—i 4 khá»‘i `[Input] -> [Trigger] -> [Guard] -> [Action]` sinh Rule firmware hoÃ n chá»‰nh vá»›i Ä‘Ãºng `GuardTag`.

```text
Passed!  - Failed: 0, Passed: 47, Skipped: 0, Total: 47, Duration: 105 ms - SimplePLC.Studio.Tests.dll (net8.0)
Build succeeded: 0 Warning(s), 0 Error(s)
```
ToÃ n bá»™ **47/47 Unit Tests** Ä‘á»u Ä‘áº¡t káº¿t quáº£ tuyá»‡t Ä‘á»‘i (100% Pass). ÄÃ£ push lÃªn nhÃ¡nh `dev` (`c89aa4d`).

---

## 16. Tinh Gá»n Thuá»™c TÃ­nh Khá»‘i Guard: áº¨n/Hiá»‡n NgÆ°á»¡ng Äá»™ng Theo PhÃ©p So SÃ¡nh (Adaptive Threshold Inspector)

### 16.1. PhÃ¢n TÃ­ch HÃ¬nh áº¢nh & Pháº£n Há»“i Tá»« NgÆ°á»i DÃ¹ng
* **HÃ¬nh áº£nh ngÆ°á»i dÃ¹ng cung cáº¥p**:
  ![Inspector cá»§a Guard hiá»ƒn thá»‹ cáº£ Threshold Hi khi chá»n phÃ©p =](C:\Users\DELL\.gemini\antigravity\brain\f2072594-41fc-4d10-83aa-f90614bb1427\.user_uploaded\media_1789182437357.png)
* **Váº¥n Ä‘á» Ä‘Æ°á»£c chá»‰ ra**:
  - Khi ngÆ°á»i dÃ¹ng chá»n cÃ¡c phÃ©p so sÃ¡nh Ä‘Æ¡n thÃ´ng thÆ°á»ng nhÆ° `=`, `!=`, `>`, `<`, `>=`, `<=`, há»‡ thá»‘ng váº«n hiá»ƒn thá»‹ cáº£ Ã´ `Threshold Hi (khi dÃ¹ng khoáº£ng): 0`.
  - Äiá»u nÃ y thá»«a thÃ£i, gÃ¢y nháº§m láº«n (ngÆ°á»i dÃ¹ng khÃ´ng biáº¿t sá»‘ 0 á»Ÿ Threshold Hi cÃ³ áº£nh hÆ°á»Ÿng gÃ¬ tá»›i phÃ©p so sÃ¡nh báº±ng hay khÃ´ng).

### 16.2. Giáº£i PhÃ¡p Triá»ƒn Khai
1. **Bá»• sung thuá»™c tÃ­nh Ä‘á»™ng trong [`GuardNodeViewModel.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/ViewModels/GraphNodeViewModel.cs)**:
   - `IsRangeComparison`: Tráº£ vá» `true` **duy nháº¥t** khi toÃ¡n tá»­ so sÃ¡nh lÃ  `CompareOp.BETWEEN` (trong khoáº£ng).
   - `HasComparison`: Tráº£ vá» `false` khi chá»n `CompareOp.NONE`.
   - `ThresholdPromptText`: Tá»± Ä‘á»™ng thay Ä‘á»•i nhÃ£n gá»£i Ã½ theo ngá»¯ cáº£nh:
     - Khi chá»n phÃ©p so sÃ¡nh Ä‘Æ¡n (`=`, `>`, `<`...): NhÃ£n lÃ  `GiÃ¡ trá»‹ so sÃ¡nh (Threshold):` (EN: `Threshold Value:`).
     - Khi chá»n phÃ©p so sÃ¡nh trong khoáº£ng (`BETWEEN`): NhÃ£n Ä‘á»•i thÃ nh `NgÆ°á»¡ng dÆ°á»›i (Tá»« / Min):` (EN: `Lower Threshold (Min):`).
2. **áº¨n/Hiá»‡n Ä‘á»™ng trÃªn giao diá»‡n [`LogicEditorView.xaml`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Views/LogicEditorView.xaml)**:
   - Ã” nháº­p **NgÆ°á»¡ng trÃªn (Threshold Hi)** Ä‘Æ°á»£c gáº¯n binding `Visibility="{Binding SelectedGuardNode.IsRangeComparison, Converter={StaticResource BoolToVis}}"`.
   - Khi chá»n `=`, `>`, `<`...: Ã” `Threshold Hi` **hoÃ n toÃ n biáº¿n máº¥t (Collapsed)**, giao diá»‡n báº£ng thuá»™c tÃ­nh trá»Ÿ nÃªn gá»n gÃ ng, rÃµ rÃ ng vÃ  táº­p trung.
   - Khi chá»n `[Lo .. Hi]  Between Range`: Ã” `Threshold Hi` láº­p tá»©c xuáº¥t hiá»‡n vá»›i nhÃ£n chuáº©n `NgÆ°á»¡ng trÃªn (Äáº¿n / Max):`.
3. **Äa ngÃ´n ngá»¯**:
   - Cáº­p nháº­t [`LocalizationService.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Services/LocalizationService.cs) vá»›i cÃ¡c nhÃ£n tiáº¿ng Viá»‡t vÃ  tiáº¿ng Anh chuáº©n ká»¹ thuáº­t.

### 16.3. Káº¿t Quáº£ XÃ¡c Minh (Unit Tests)
Bá»• sung unit test tá»± Ä‘á»™ng `GuardNode_ComparisonMode_TogglesRangeAndPromptsProperly` trong [`LogicEditorViewModelTests.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio.Tests/LogicEditorViewModelTests.cs):
- XÃ¡c minh khi `CompareOp == EQ`: `IsRangeComparison == false`, `HasComparison == true`, nhÃ£n hiá»ƒn thá»‹ `"GiÃ¡ trá»‹ so sÃ¡nh"`.
- XÃ¡c minh khi `CompareOp == BETWEEN`: `IsRangeComparison == true`, nhÃ£n Ä‘á»•i thÃ nh `"NgÆ°á»¡ng dÆ°á»›i"`.
- XÃ¡c minh khi `CompareOp == NONE`: `HasComparison == false`.

```text
Passed!  - Failed: 0, Passed: 48, Skipped: 0, Total: 48, Duration: 113 ms - SimplePLC.Studio.Tests.dll (net8.0)
Build succeeded: 0 Warning(s), 0 Error(s)
```
ToÃ n bá»™ **48/48 Unit Tests** Ä‘á»u Ä‘áº¡t káº¿t quáº£ tuyá»‡t Ä‘á»‘i (100% Pass).

---

## 17. Trang Bá»‹ Äáº§y Äá»§ CÃ¡c Thao TÃ¡c Soáº¡n Tháº£o CÆ¡ Báº£n: Undo/Redo, Clipboard (Cut/Copy/Paste/Duplicate), Select All & Context Menus

### 17.1. PhÃ¢n TÃ­ch YÃªu Cáº§u & Bá»‘i Cáº£nh
* **YÃªu cáº§u tá»« ngÆ°á»i dÃ¹ng**: *"trong app chÃºng ta Ä‘ang thiáº¿u cÃ¡c thao tÃ¡c cÆ¡ báº£n kiá»ƒu ctrl Z, C, V, etc"*
* **Hiá»‡n tráº¡ng trÆ°á»›c Ä‘Ã³**:
  - `Undo()` vÃ  `Redo()` trong `MainViewModel` chá»‰ lÃ  placeholder rá»—ng.
  - ChÆ°a cÃ³ cÆ¡ cháº¿ Clipboard cho cÃ¡c khá»‘i vÃ  dÃ¢y ná»‘i Ä‘á»“ thá»‹.
  - PhÃ­m táº¯t `Ctrl+C`, `Ctrl+X`, `Ctrl+V`, `Ctrl+D`, `Ctrl+A` chÆ°a Ä‘Æ°á»£c há»— trá»£.
  - Menu `Chá»‰nh sá»­a` thiáº¿u cÃ¡c tÃ¹y chá»n thao tÃ¡c cÆ¡ báº£n.

### 17.2. CÃ¡c Giáº£i PhÃ¡p Ká»¹ Thuáº­t ÄÃ£ Triá»ƒn Khai

1. **Quáº£n LÃ½ Lá»‹ch Sá»­ Theo Memento Pattern ([`GraphHistoryService.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Services/GraphHistoryService.cs))**:
   - LÆ°u trá»¯ cÃ¡c cáº¥u trÃºc snapshot nháº¹ `GraphSnapshot` gá»“m danh sÃ¡ch khá»‘i, dÃ¢y ná»‘i vÃ  tráº¡ng thÃ¡i lá»±a chá»n. Giá»›i háº¡n tá»‘i Ä‘a 50 bÆ°á»›c Ä‘á»ƒ tá»‘i Æ°u dung lÆ°á»£ng RAM (< 150KB).
   - Tá»± Ä‘á»™ng báº¯t snapshot trÆ°á»›c cÃ¡c tÃ¡c vá»¥ biáº¿n Ä‘á»•i Ä‘á»“ thá»‹:
     - ThÃªm khá»‘i (`AddNode`), XÃ³a khá»‘i (`DeleteSelectedNode`).
     - Ná»‘i dÃ¢y (`CompleteConnection`), RÃºt dÃ¢y (`DisconnectConnector`, `RemoveConnection`).
     - Báº¯t Ä‘áº§u vÃ  káº¿t thÃºc kÃ©o chuá»™t di chuyá»ƒn khá»‘i (`ItemContainer_PreviewMouseDown` / `Up`).
     - CÃ¡c lá»‡nh cÄƒn chá»‰nh bá»‘ cá»¥c (`Align`, `Distribute`, `SnapToGrid`, `ClearCanvas`).
     - DÃ¡n khá»‘i (`Paste`).
   - Tá»± Ä‘á»™ng biÃªn dá»‹ch láº¡i Firmware Rule thá»i gian thá»±c ngay khi `Undo` hoáº·c `Redo`.

2. **CÆ¡ Cháº¿ Bá»™ Nhá»› Táº¡m & Thao TÃ¡c Khá»‘i Äá»“ Thá»‹ ([`LogicEditorViewModel.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/ViewModels/LogicEditorViewModel.cs))**:
   - **Sao chÃ©p (`Copy` - Ctrl+C)**: Xuáº¥t cÃ¡c khá»‘i Ä‘ang chá»n vÃ  **toÃ n bá»™ dÃ¢y ná»‘i ná»™i bá»™ giá»¯a cÃ¡c khá»‘i Ä‘Ã³** vÃ o bá»™ nhá»› táº¡m in-memory (`GraphClipboardData`) láº«n System Clipboard (JSON).
   - **Cáº¯t (`Cut` - Ctrl+X)**: Copy vÃ o clipboard rá»“i xÃ³a khá»i canvas (cÃ³ há»— trá»£ Undo).
   - **DÃ¡n (`Paste` - Ctrl+V)**:
     - Tá»± Ä‘á»™ng sinh ID má»›i duy nháº¥t (`Guid`) cho tá»«ng khá»‘i dÃ¡n Ä‘á»ƒ trÃ¡nh xung Ä‘á»™t mÃ£.
     - TÃ¡i thiáº¿t láº­p chÃ­nh xÃ¡c cÃ¡c liÃªn káº¿t dÃ¢y ná»‘i ná»™i bá»™ giá»¯a cÃ¡c khá»‘i má»›i theo báº£n Ä‘á»“ Ã¡nh xáº¡ ID.
     - Tá»‹nh tiáº¿n vá»‹ trÃ­ lá»‡ch gÃ³c (+30px, +30px theo cáº¥p sá»‘ nhÃ¢n sá»‘ láº§n dÃ¡n) Ä‘á»ƒ ngÆ°á»i dÃ¹ng dá»… quan sÃ¡t.
     - Tá»± Ä‘á»™ng chuyá»ƒn vÃ¹ng chá»n sang nhÃ³m khá»‘i má»›i dÃ¡n.
   - **NhÃ¢n báº£n (`Duplicate` - Ctrl+D)**: NhÃ¢n báº£n tá»©c thá»i nhÃ³m khá»‘i Ä‘ang chá»n chá»‰ vá»›i má»™t phÃ­m táº¯t.
   - **Chá»n táº¥t cáº£ (`Select All` - Ctrl+A)**: QuÃ©t chá»n toÃ n bá»™ cÃ¡c khá»‘i trÃªn khung váº½.

3. **Giao Diá»‡n Menu & Context Menu Chuá»™t Pháº£i**:
   - **Menu Chá»‰nh sá»­a (`MainWindow.xaml`)**: Bá»• sung Ä‘áº§y Ä‘á»§ HoÃ n tÃ¡c (Ctrl+Z), LÃ m láº¡i (Ctrl+Y / Ctrl+Shift+Z), Cáº¯t (Ctrl+X), Sao chÃ©p (Ctrl+C), DÃ¡n (Ctrl+V), NhÃ¢n báº£n (Ctrl+D), XÃ³a (Del), Chá»n táº¥t cáº£ (Ctrl+A).
   - **Context Menu trÃªn khá»‘i**: Nháº¥p chuá»™t pháº£i vÃ o khá»‘i/nhÃ³m khá»‘i má»Ÿ menu ngá»¯ cáº£nh vá»›i Cáº¯t, Sao chÃ©p, DÃ¡n, NhÃ¢n báº£n, XÃ³a, CÄƒn chá»‰nh.
   - **Context Menu trÃªn Canvas**: Nháº¥p chuá»™t pháº£i vÃ o ná»n khung váº½ má»Ÿ menu ngá»¯ cáº£nh vá»›i HoÃ n tÃ¡c, LÃ m láº¡i, DÃ¡n, Chá»n táº¥t cáº£, Báº¯t dÃ­nh lÆ°á»›i.
   - **Báº£o toÃ n nháº­p liá»‡u trong Inspector ([`LogicEditorView.xaml.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Views/LogicEditorView.xaml.cs))**: Tá»± Ä‘á»™ng bá» qua viá»‡c can thiá»‡p phÃ­m táº¯t canvas khi ngÆ°á»i dÃ¹ng Ä‘ang gÃµ vÄƒn báº£n trong `TextBoxBase`.

4. **Äa ngÃ´n ngá»¯ ([`LocalizationService.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Services/LocalizationService.cs))**:
   - Bá»• sung `MenuCut`, `MenuCopy`, `MenuPaste`, `MenuDuplicate`, `MenuSelectAll` song ngá»¯ Viá»‡t - Anh.

### 17.3. Káº¿t Quáº£ Kiá»ƒm Thá»­ Tá»± Äá»™ng (Unit Tests)
Bá»• sung bá»™ kiá»ƒm thá»­ chuyÃªn sÃ¢u [`UndoRedoClipboardTests.cs`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio.Tests/UndoRedoClipboardTests.cs) gá»“m 6 bÃ i test:
1. `UndoRedo_AddNode_CanUndoAndRedo`: ThÃªm khá»‘i $\rightarrow$ Undo biáº¿n máº¥t $\rightarrow$ Redo xuáº¥t hiá»‡n láº¡i.
2. `UndoRedo_Connection_CanUndoAndRedo`: Ná»‘i dÃ¢y $\rightarrow$ Undo rÃºt dÃ¢y $\rightarrow$ Redo ná»‘i láº¡i dÃ¢y.
3. `UndoRedo_DeleteNode_RestoresNodeAndConnections`: XÃ³a khá»‘i trung gian $\rightarrow$ Undo phá»¥c há»“i Ä‘áº§y Ä‘á»§ khá»‘i vÃ  toÃ n bá»™ dÃ¢y ná»‘i vÃ o/ra.
4. `Clipboard_CopyPaste_ClonesNodesWithNewIdsAndRecreatesInternalConnections`: Sao chÃ©p vÃ  dÃ¡n 2 khá»‘i cÃ³ dÃ¢y ná»‘i $\rightarrow$ sinh 2 khá»‘i má»›i vá»›i ID má»›i vÃ  Ä‘Ãºng 1 dÃ¢y ná»‘i ná»™i bá»™ giá»¯a chÃºng.
5. `Clipboard_Duplicate_DuplicatesSelection`: NhÃ¢n báº£n sao chÃ©p Ä‘áº§y Ä‘á»§ thuá»™c tÃ­nh vÃ  vá»‹ trÃ­ lá»‡ch chuáº©n 30px.
6. `SelectAll_SelectsAllNodesOnCanvas`: Chá»n toÃ n bá»™ khá»‘i trÃªn canvas khÃ´ng bá»‹ trÃ¹ng láº·p.

```text
Passed!  - Failed: 0, Passed: 54, Skipped: 0, Total: 54, Duration: 149 ms - SimplePLC.Studio.Tests.dll (net8.0)
Build succeeded: 0 Warning(s), 0 Error(s)
```
ToÃ n bá»™ **54/54 Unit Tests** Ä‘á»u Ä‘áº¡t káº¿t quáº£ 100% Pass.

---

## 18. Chá»n vÃ  XÃ³a DÃ¢y Káº¿t Ná»‘i (Wire Selection & Deletion with Undo/Redo)

### Má»¥c tiÃªu & Tráº£i nghiá»‡m ngÆ°á»i dÃ¹ng
- Cho phÃ©p ngÆ°á»i dÃ¹ng chá»n dÃ¢y (connection) trá»±c quan trÃªn canvas báº±ng chuá»™t trÃ¡i hoáº·c chuá»™t pháº£i.
- Dá»… dÃ ng thao tÃ¡c chuá»™t (hit-testing) mÃ  khÃ´ng sá»£ trÆ°á»£t nhá» lá»›p Ä‘Ã³n sá»± kiá»‡n dÃ y 12px vÃ´ hÃ¬nh.
- Pháº£n há»“i trá»±c quan rÃµ rÃ ng:
  - **Hover**: DÃ¢y chuyá»ƒn sang mÃ u sÃ¡ng xanh dÆ°Æ¡ng `#3B82F6` (Ä‘á»™ dÃ y 3px).
  - **Selected**: DÃ¢y ná»•i báº­t mÃ u xanh dÆ°Æ¡ng Ä‘áº­m `#2563EB` (Ä‘á»™ dÃ y 3.5px).
- XÃ³a dÃ¢y nhanh chÃ³ng:
  - PhÃ­m `Delete` xÃ³a cÃ¡c dÃ¢y (vÃ  khá»‘i) Ä‘ang Ä‘Æ°á»£c chá»n.
  - Chuá»™t pháº£i vÃ o dÃ¢y má»Ÿ Context Menu: **"XÃ³a dÃ¢y káº¿t ná»‘i nÃ y (Delete Connection)" [Del]**.
- TÃ­ch há»£p toÃ n diá»‡n vÃ o há»‡ thá»‘ng Undo/Redo (`Ctrl+Z` / `Ctrl+Y`): phá»¥c há»“i ngay láº­p tá»©c dÃ¢y Ä‘Ã£ xÃ³a cÃ¹ng tráº¡ng thÃ¡i `IsConnected` cá»§a cÃ¡c chÃ¢n cá»•ng (pin).

### CÃ¡c thay Ä‘á»•i chÃ­nh
1. **[ConnectionViewModel](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/ViewModels/ConnectionViewModel.cs)**:
   - ThÃªm thuá»™c tÃ­nh `[ObservableProperty] private bool _isSelected;`.
2. **[LocalizationService](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Services/LocalizationService.cs)**:
   - ThÃªm resource `MenuDeleteConnection`:
     - Tiáº¿ng Viá»‡t: `"XÃ³a dÃ¢y káº¿t ná»‘i nÃ y"`
     - Tiáº¿ng Anh: `"Delete Connection"`
3. **[LogicEditorViewModel](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/ViewModels/LogicEditorViewModel.cs)**:
   - `HasSelection`: Kiá»ƒm tra cáº£ `Nodes.Any(n => n.IsSelected) || Connections.Any(c => c.IsSelected)`.
   - `DeselectAllConnections()`: Bá» chá»n toÃ n bá»™ dÃ¢y khi click vÃ o ná»n canvas hoáº·c khi click vÃ o node mÃ  khÃ´ng nháº¥n Ctrl.
   - Theo dÃµi `PropertyChanged` cá»§a tá»«ng `ConnectionViewModel` Ä‘á»ƒ cáº­p nháº­t `HasSelection` vÃ  `DeleteSelectedNodeCommand.NotifyCanExecuteChanged()`.
   - `DeleteSelectedNode()`: Thu tháº­p danh sÃ¡ch dÃ¢y Ä‘ang chá»n (`Connections.Where(c => c.IsSelected)`), ngáº¯t káº¿t ná»‘i cÃ¡c pin liÃªn quan, xÃ³a dÃ¢y khá»i danh sÃ¡ch vÃ  ghi láº¡i snapshot lá»‹ch sá»­ (`RecordSnapshot("Delete")`).
   - `[RelayCommand] DeleteConnection(ConnectionViewModel? conn)`: Há»— trá»£ lá»‡nh xÃ³a trá»±c tiáº¿p tá»« Context Menu cá»§a dÃ¢y.
4. **[LogicEditorView.xaml](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Views/LogicEditorView.xaml) & [NodeTemplates.xaml](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Styles/NodeTemplates.xaml)**:
   - Äá»‹nh nghÄ©a `ConnectionContextMenu` vá»›i item xÃ³a dÃ¢y.
   - ThÃªm Ä‘Æ°á»ng line trong suá»‘t `Stroke="Transparent"` `StrokeThickness="12"` `Cursor="Hand"` xáº¿p lá»›p phÃ­a dÆ°á»›i Ä‘á»ƒ má»Ÿ rá»™ng vÃ¹ng click dá»… dÃ ng.
   - Bá»• sung `Triggers` Ä‘á»•i mÃ u vÃ  phÃ³ng to stroke khi hover hoáº·c selected.
   - Báº¯t sá»± kiá»‡n click chuá»™t trÃ¡i/pháº£i trÃªn dÃ¢y Ä‘á»ƒ chá»n hoáº·c bá» chá»n dÃ¢y khÃ¡c.
   - Báº¯t sá»± kiá»‡n click trÃªn ná»n NodifyEditor Ä‘á»ƒ há»§y chá»n dÃ¢y.
5. **[SimplePLC.Studio.Tests](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio.Tests/UndoRedoClipboardTests.cs)**:
   - ThÃªm test `Wire_SelectAndExecuteDelete_DeletesWireAndSupportsUndo`.
   - ThÃªm test `Wire_DeleteConnectionCommand_DeletesWireDirectly`.
   - ToÃ n bá»™ 56/56 unit tests vÆ°á»£t qua xuáº¥t sáº¯c.

---

## 19. Bá»™ Lá»‡nh Nhanh CÄƒn Chá»‰nh Khá»‘i (Align Top, Middle, Bottom, Left, Center, Right & Quick Shortcuts)

### Má»¥c tiÃªu & Tráº£i nghiá»‡m ngÆ°á»i dÃ¹ng
- Cung cáº¥p trá»n bá»™ cÃ´ng cá»¥ cÄƒn chá»‰nh vá»‹ trÃ­ cÃ¡c khá»‘i logic trÃªn canvas theo Ä‘Ãºng chuáº©n cÃ¡c pháº§n má»m ká»¹ thuáº­t cao cáº¥p (CAD / Visio / Figma):
  - **CÄƒn lá» trÃªn (Align Top)**: ÄÆ°a táº¥t cáº£ cÃ¡c khá»‘i Ä‘Æ°á»£c chá»n vá» cÃ¹ng má»™t Ä‘Æ°á»ng biÃªn trÃªn (tá»a Ä‘á»™ Y nhá» nháº¥t).
  - **CÄƒn giá»¯a theo chiá»u dá»c / hÃ ng ngang (Align Middle)**: CÄƒn tÃ¢m trá»¥c dá»c cá»§a cÃ¡c khá»‘i vá» vá»‹ trÃ­ trung bÃ¬nh, tháº³ng hÃ ng ngang hoÃ n háº£o.
  - **CÄƒn lá» dÆ°á»›i (Align Bottom)**: ÄÆ°a táº¥t cáº£ cÃ¡c khá»‘i vá» cÃ¹ng má»™t Ä‘Æ°á»ng biÃªn dÆ°á»›i (tá»a Ä‘á»™ Y lá»›n nháº¥t).
  - **CÄƒn lá» trÃ¡i (Align Left)**: ÄÆ°a táº¥t cáº£ cÃ¡c khá»‘i vá» cÃ¹ng má»™t Ä‘Æ°á»ng biÃªn trÃ¡i (tá»a Ä‘á»™ X nhá» nháº¥t).
  - **CÄƒn giá»¯a theo chiá»u ngang / hÃ ng dá»c (Align Center)**: CÄƒn tÃ¢m trá»¥c ngang cá»§a cÃ¡c khá»‘i vá» vá»‹ trÃ­ trung bÃ¬nh, tháº³ng hÃ ng dá»c hoÃ n háº£o.
  - **CÄƒn lá» pháº£i (Align Right)**: ÄÆ°a táº¥t cáº£ cÃ¡c khá»‘i vá» cÃ¹ng má»™t Ä‘Æ°á»ng biÃªn pháº£i (tá»a Ä‘á»™ X lá»›n nháº¥t).
  - **CÄƒn Ä‘á»u khoáº£ng cÃ¡ch ngang (Distribute Horizontally)** & **CÄƒn Ä‘á»u khoáº£ng cÃ¡ch dá»c (Distribute Vertically)**.
  - **CÄƒn dÃ­nh máº¯t lÆ°á»›i (Snap to Grid)**: Báº¯t dÃ­nh vá»‹ trÃ­ cÃ¡c khá»‘i vÃ o bÆ°á»›c lÆ°á»›i 10px.
- Há»— trá»£ tÃ­nh nÄƒng thÃ´ng minh: náº¿u chá»‰ chá»n 1 khá»‘i cÃ³ ná»‘i dÃ¢y, lá»‡nh cÄƒn chá»‰nh sáº½ tá»± Ä‘á»™ng cÄƒn theo khá»‘i lÃ¡ng giá»ng liÃªn káº¿t!
- Há»— trá»£ toÃ n diá»‡n Undo / Redo (`Ctrl+Z` / `Ctrl+Y`) cho táº¥t cáº£ cÃ¡c thao tÃ¡c cÄƒn chá»‰nh.

### PhÃ­m táº¯t (Lá»‡nh nhanh)
| Thao tÃ¡c | PhÃ­m táº¯t chÃ­nh | PhÃ­m táº¯t phá»¥ | Biá»ƒu tÆ°á»£ng |
| :--- | :--- | :--- | :---: |
| **CÄƒn lá» trÃªn (Align Top)** | `Alt + Up` | `Ctrl + Shift + Up` | `â¤’` |
| **CÄƒn giá»¯a dá»c (Align Middle)** | `Alt + M` | `Ctrl + Shift + M` | `â•Œ` |
| **CÄƒn lá» dÆ°á»›i (Align Bottom)** | `Alt + Down` | `Ctrl + Shift + Down` | `â¤“` |
| **CÄƒn lá» trÃ¡i (Align Left)** | `Alt + Left` | `Ctrl + Shift + Left` | `â‡¤` |
| **CÄƒn giá»¯a ngang (Align Center)** | `Alt + C` | `Ctrl + Shift + C` | `â”†` |
| **CÄƒn lá» pháº£i (Align Right)** | `Alt + Right` | `Ctrl + Shift + Right` | `â‡¥` |
| **CÄƒn Ä‘á»u ngang (Distribute H)** | `Alt + Shift + H` | `Ctrl + Shift + H` | `â«¸` |
| **CÄƒn Ä‘á»u dá»c (Distribute V)** | `Alt + Shift + V` | `Ctrl + Shift + V` | `â«¶` |
| **CÄƒn dÃ­nh lÆ°á»›i (Snap Grid)** | `Alt + G` | `Ctrl + Shift + G` | `âŒ—` |

### CÃ¡c file Ä‘Ã£ chá»‰nh sá»­a & bá»• sung:
1. **[LocalizationService.cs](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Services/LocalizationService.cs)**: Bá»• sung song ngá»¯ (VI/EN) cho 6 lá»‡nh cÄƒn chá»‰nh má»›i.
2. **[LogicEditorViewModel.cs](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/ViewModels/LogicEditorViewModel.cs)**:
   - ThÃªm helper `GetAlignmentTargetNodes(out GraphNodeViewModel? connectedNeighbor)` xá»­ lÃ½ Ä‘a khá»‘i hoáº·c 1 khá»‘i liÃªn káº¿t.
   - ThÃªm cÃ¡c RelayCommand: `AlignTopCommand`, `AlignMiddleCommand`, `AlignBottomCommand`, `AlignLeftCommand`, `AlignCenterCommand`, `AlignRightCommand`.
   - TÃ­ch há»£p ghi nháº­n lá»‹ch sá»­ `RecordSnapshot(...)` cho tá»«ng lá»‡nh.
3. **[MainViewModel.cs](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/ViewModels/MainViewModel.cs)**: Chuyá»ƒn tiáº¿p cÃ¡c lá»‡nh cÄƒn chá»‰nh sang `LogicEditorVM`.
4. **[LogicEditorView.xaml.cs](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Views/LogicEditorView.xaml.cs)**: Báº¯t cÃ¡c tá»• há»£p phÃ­m `Alt + Arrows/M/C/G`, `Alt + Shift + H/V`, `Ctrl + Shift + Arrows/M/C/H/V/G` trong `PreviewKeyDown`.
5. **[MainWindow.xaml](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/MainWindow.xaml)**: Cáº­p nháº­t menu `Edit -> CÄƒn chá»‰nh & Bá»‘ cá»¥c` vÃ  `Tools -> CÄƒn chá»‰nh & Bá»‘ cá»¥c`.
6. **[LogicEditorView.xaml](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Views/LogicEditorView.xaml)**: Cáº­p nháº­t menu chuá»™t pháº£i cá»§a khá»‘i vÃ  cá»§a ná»n canvas vá»›i Ä‘áº§y Ä‘á»§ lá»‡nh, icon vÃ  nhÃ£n phÃ­m táº¯t.
7. **[LogicEditorViewModelTests.cs](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio.Tests/LogicEditorViewModelTests.cs)**: ThÃªm 7 unit tests má»›i kiá»ƒm tra Ä‘á»™ chÃ­nh xÃ¡c tá»a Ä‘á»™ vÃ  tÃ­nh nÄƒng hoÃ n tÃ¡c Undo. Äáº¡t **63/63 tests Pass**.

---

