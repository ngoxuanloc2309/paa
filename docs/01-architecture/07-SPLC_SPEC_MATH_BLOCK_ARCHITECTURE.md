# ĐẶC TẢ KIẾN TRÚC PHẦN MỀM & TRÌNH BIÊN DỊCH
## SPLC-ARCH-MATH: THIẾT KẾ KHỐI HÀM MATH TRÊN SYNAPTIX IDE & RUNTIME

**Mã tài liệu:** `SPLC-ARCH-MATH-001`  
**Phiên bản:** `v1.0.0`  
**Ngày phát hành:** `2026-11-16`  
**Trạng thái:** `APPROVED SPECIFICATION`  
**Phạm vi áp dụng:** SynaptiX IDE Studio, Trình biên dịch RuleCompiler, Domain Models, Protocol Codecs, Trình mô phỏng McuReferenceSimulator.

---

### 1. Phân Lớp Kiến Trúc (Clean Architecture Layers)

Tuân thủ nghiêm ngặt nguyên lý **Clean Architecture Inversion**:

```
┌─────────────────────────────────────────────────────────────┐
│ 1. SimplePLC.Domain                                         │
│    - ActionKind.MathOp, MathOperator, MathOperandMode       │
│    - MathNodeModel : IFbdNodeModel                          │
│    (Zero dependencies, Pure Business Logic & State)         │
├─────────────────────────────────────────────────────────────┤
│ 2. SimplePLC.Protocol                                       │
│    - SPLC_ActionType.MATH_OP = 8                           │
│    - RuleRecordDto (ThresholdLo/Hi, AuxParam, CompareOp)    │
│    - RegisterCodec (Mã hóa/Giải mã 16 thanh ghi Modbus)     │
├─────────────────────────────────────────────────────────────┤
│ 3. SimplePLC.Application                                    │
│    - MathMacroLowering.cs (Phân rã cây AST thành Rule 32B)  │
│    - RuleMapper.cs (Ánh xạ Domain ActionModel <-> DTO)      │
│    - GraphGrammarV1.cs (Kiểm tra hợp lệ chân nối & kiểu)    │
├─────────────────────────────────────────────────────────────┤
│ 4. SimplePLC.Infrastructure                                 │
│    - McuReferenceSimulator.cs (Chạy kiểm thử giả lập ALU)   │
│    - RuleTableWriter.cs (Ghi gói nhị phân xuống MCU qua USB)│
├─────────────────────────────────────────────────────────────┤
│ 5. SimplePLC.Studio                                         │
│    - MathNodeViewModel.cs & MathNodeView.xaml (WPF Canvas)  │
│    - MathInspectorViewModel.cs (Fluent 2 Desktop UI)        │
└─────────────────────────────────────────────────────────────┘
```

---

### 2. Mô Hình Miền (Domain Model)

Trong dự án `SimplePLC.Domain/Models/`:

```csharp
namespace SimplePLC.Domain.Models;

/// <summary>
/// Danh mục toán tử số học hỗ trợ bởi khối Math.
/// </summary>
public enum MathOperator : byte
{
    Add = 0,
    Subtract = 1,
    Multiply = 2,
    Divide = 3,
    Modulo = 4,
    Min = 5,
    Max = 6,
    Clamp = 7
}

/// <summary>
/// Nguồn cấp giá trị cho Toán hạng thứ hai.
/// </summary>
public enum MathOperandMode : byte
{
    Constant = 0,
    Tag = 1
}

/// <summary>
/// Mô hình nút khối Math trên đồ thị FBD.
/// </summary>
public sealed class MathNodeModel : IFbdNodeModel
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Label { get; set; } = "MATH";
    public MathOperator Operator { get; set; } = MathOperator.Add;
    public MathOperandMode Operand2Mode { get; set; } = MathOperandMode.Constant;
    public double ConstantValue { get; set; } = 0.0;
    public TagModel? InputTag1 { get; set; }
    public TagModel? InputTag2 { get; set; }
    public TagModel? OutputTag { get; set; }
    public bool IsFloat { get; set; } = true;
}
```

---

### 3. Chiến Lược Hạ Cấp Trình Biên Dịch (Macro Lowering Pass)

Triển khai tại `src/SimplePLC.Application/Logic/Compilation/MacroLowering/MathMacroLowering.cs`:

1. **Trường hợp Phép toán Cơ bản 2 Toán hạng:**
   - Trình biên dịch tạo ra 1 `CandidateRule` duy nhất:
     - `TriggerTag`: `InputTag1.TagIndex`
     - `GuardTag`: Nếu `Operand2Mode == Tag`, dùng `InputTag2.TagIndex`. Nếu `Constant`, dùng `ModbusRegisterMap.GuardTagNone (0x7FFF)`.
     - `AuxParameter`: Giá trị `ConstantValue` khi dùng chế độ `Constant`.
     - `ActionKind`: `ActionKind.MathOp` (ánh xạ mã số `8` xuống wire contract).
     - `CompareOp`: `(byte)Operator`.
     - `TargetTag`: `OutputTag.TagIndex`.

2. **Trường hợp Mở rộng Đa Toán Hạng (Multi-Pin Chaining $2 \le N \le 8$):**
   - Áp dụng cho các toán tử giao hoán / kết hợp và chuỗi liên tục: **Cộng (`ADD`)**, **Trừ (`SUB`)**, **Nhân (`MUL`)**:
     - Phép toán trừ nhiều toán hạng: tính tuần tự từ trái sang phải: $OUT = A - B - C - \dots$
     - Phép toán chia (`DIV`) và lấy dư (`MOD`): giới hạn nghiêm ngặt đúng 2 toán hạng theo IEC 61131-3.
   - Khi $N > 2$, trình biên dịch tự động hạ cấp thành chuỗi $(N - 1)$ Micro-Rules 32-byte liên tiếp:
     - Bước 1: Tính toán $A \text{ op } B \rightarrow \text{VREG}_{\text{scratch}}$.
     - Bước 2: Chuỗi liên tiếp $\text{VREG}_{\text{scratch}} \text{ op } C \rightarrow \dots \rightarrow OUT$.
     - Bảo toàn tính toàn vẹn 32-byte của Wire Contract MCU mà không cần thay đổi firmware runtime.

---

### 4. Thiết Kế Giao Diện UI/UX (SynaptiX IDE Studio)

Tuân thủ nghiêm ngặt **Học thuyết Frontend & UI/UX (AGENTS.md & ANSI/ISA-101)**:

1. **Khối đồ họa trên Canvas (`MathNodeView.xaml`):**
   - Dùng phong cách thẻ Fluent 2 Desktop (`CornerRadius="6"` cho khung khối).
   - Màu sắc ngữ nghĩa từ `Colors.xaml`:
     - Viền: `{DynamicResource IndustrialBorderDefaultBrush}`.
     - Nền: `{DynamicResource BgPanelBrush}`.
     - Thanh tiêu đề: `{DynamicResource BgSubtleBrush}` kèm Vector SVG Icon (`IconSysMath`).
   - Hiển thị trực quan tóm tắt công thức đại số và kết quả thời gian thực.

2. **Thanh thuộc tính Inspector (`LogicEditorView.xaml`):**
   - Hiển thị tóm tắt công thức, toán tử, chế độ kiểu dữ liệu và số lượng chân.
   - Nút **Configure Block...** mở hộp thoại cấu hình nâng cao.

3. **Hộp thoại Cấu hình Chuyên dụng (`MathConfigDialogView.xaml` & `MathConfigDialogViewModel.cs`):**
   - **Bảng trái:** Tham số chung (Toán tử, Kiểu dữ liệu Auto/INT32/REAL, Tag ngõ ra OUT) và Danh sách toán hạng 2..8 chân (`+ Add Operand`, chế độ Tag/Constant, phím xóa chân).
   - **Bảng phải (ALU Diagnostics & Live Simulation):**
     - Hộp hiển thị công thức đại số xem trước (Formula Preview).
     - Bàn thử nghiệm chẩn đoán thời gian thực (Live Simulation Test Bench): cho phép kỹ sư nhập giá trị thử nghiệm cho từng toán hạng và kiểm tra tức thời kết quả `ALU Output Result`, kiểu suy diễn (`Inferred Type`), và huy hiệu trạng thái hợp lệ/lỗi (`✓ VALID` / `FAULT: DIV_BY_ZERO`).
     - Hướng dẫn quy tắc số học công nghiệp IEC 61131-3.

---

### 5. Kế Hoạch Triển Khai & Kiểm Thử TDD Triad

Trước khi đưa mã nguồn vào nhánh chính, bắt buộc thực hiện kiểm thử Triad theo tiêu chuẩn IEC 61508-3:
1. **Happy Path:**
   - Thực thi cộng, trừ, nhân, chia danh định trên số nguyên và số thực IEEE-754.
   - So sánh kết quả giả lập với `BitConverter.SingleToInt32Bits()`.
2. **Boundary & Overflow:**
   - Nhân 2 số nguyên lớn $2 \times 10^9 \times 2 \times 10^9 \implies$ kiểm tra kẹp bão hòa `INT32_MAX`.
   - Số thực xấp xỉ biên `1e-38` và `1e+38`.
3. **Fault Injection:**
   - Chia cho 0: Bơm $\text{Op}_2 = 0$ hoặc $0.0\text{f} \implies$ Assert không crash, cờ cảnh báo `SPLC_ERR_DIV_ZERO` bật, ngõ ra giữ nguyên giá trị chu kỳ trước.
   - Bơm `float.NaN` và `float.PositiveInfinity` $\implies$ Assert ngõ ra tự động khử về `0.0f`.
