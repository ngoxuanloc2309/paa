# ĐẶC TẢ KỸ THUẬT NÂNG CẤP INDUSTRIAL AI COPILOT THẾ HỆ MỚI
## SYNAPTIX IDE / SIMPLEPLC STUDIO
### Kiến Trúc Phân Tầng, Gọi Công Cụ Hai Chiều, Lớp Diff Trực Quan & Thẩm Định An Toàn Công Nghiệp

---

**Mã tài liệu**: `SPLC-SPEC-AI-NEXTGEN-001`  
**Phiên bản đặc tả**: v3.0-Production  
**Tác giả**: Ban Kỹ Thuật AI & Tự Động Hóa Công Nghiệp (Industrial AI Engineering Division)  
**Ngày phát hành**: 2026-10-07  
**Phạm vi áp dụng**: `SimplePLC.Studio` (AI Chat, FBD Canvas, History Service), `SimplePLC.Application` (Compilation, Safety Validators), `SimplePLC.Domain` (Graph Grammar, Rules).

---

## MỤC LỤC
1. [TỔNG QUAN & ĐÁNH GIÁ HIỆN TRẠNG AI COPILOT (AS-IS EVALUATION)](#1-tổng-quan--đánh-giá-hiện-trạng-ai-copilot-as-is-evaluation)
   - 1.1 [Các điểm sáng hiện tại](#11-các-điểm-sáng-hiện-tại)
   - 1.2 [5 Lỗ hổng kiến trúc & Điểm nghẽn nghiêm trọng](#12-5-lỗ-hổng-kiến-trúc--điểm-nghẽn-nghiêm-trọng)
2. [KIẾN TRÚC PHÂN TẦNG 5 LỚP THẾ HỆ MỚI (5-LAYER NEXT-GEN ARCHITECTURE)](#2-kiến-trúc-phân-tầng-5-lớp-thế-hệ-mới-5-layer-next-gen-architecture)
   - 2.1 [Sơ đồ khối tổng thể (Architectural Block Diagram)](#21-sơ-đồ-khối-tổng-thể-architectural-block-diagram)
   - 2.2 [Layer 1: Deep Project Semantic Context Aggregator](#22-layer-1-deep-project-semantic-context-aggregator)
   - 2.3 [Layer 2: LLM Engine with Bidirectional Tool Calling](#23-layer-2-llm-engine-with-bidirectional-tool-calling)
   - 2.4 [Layer 3: Pre-Execution Safety & Logic Validator Gate](#24-layer-3-pre-execution-safety--logic-validator-gate)
   - 2.5 [Layer 4: Nodify Canvas Visual Diff Engine (Ghost Preview)](#25-layer-4-nodify-canvas-visual-diff-engine-ghost-preview)
   - 2.6 [Layer 5: Human Affirmation Gate & Transaction Rollback](#26-layer-5-human-affirmation-gate--transaction-rollback)
   - 2.7 [Phân Cấp An Toàn Máy Móc Công Nghiệp (ISO 13849-1 & NFPA 79)](#27-phân-cấp-an-toàn-máy-móc-công-nghiệp-iso-13849-1--nfpa-79)
3. [DANH MỤC CÁC BỘ FUNCTION CALLING JSON SCHEMAS CHUẨN HÓA (STRICT MODE)](#3-danh-mục-các-bộ-function-calling-json-schemas-chuẩn-hóa-strict-mode)
   - 3.1 [`add_node`](#31-add_node)
   - 3.2 [`remove_node`](#32-remove_node)
   - 3.3 [`connect_wires`](#33-connect_wires)
   - 3.4 [`disconnect_wire`](#34-disconnect_wire)
   - 3.5 [`configure_timer_counter`](#35-configure_timer_counter)
   - 3.6 [`configure_scale`](#36-configure_scale)
   - 3.7 [`rename_tag`](#37-rename_tag)
   - 3.8 [`auto_layout_graph`](#38-auto_layout_graph)
   - 3.9 [`diagnose_logic_bugs`](#39-diagnose_logic_bugs)
4. [SYSTEM PROMPT TÍCH HỢP TRI THỨC AN TOÀN ĐIỀU KHIỂN CÔNG NGHIỆP](#4-system-prompt-tích-hợp-tri-thức-an-toàn-điều-khiển-công-nghiệp)
5. [KỊCH BẢN THỰC THI MẪU ĐẦU-CUỐI (CONCRETE END-TO-END WALKTHROUGH)](#5-kịch-bản-thực-thi-mẫu-đầu-cuối-concrete-end-to-end-walkthrough)
   - 5.1 [Yêu cầu kỹ thuật từ người dùng](#51-yêu-cầu-kỹ-thuật-từ-người-dùng)
   - 5.2 [Ngữ cảnh dự án được tổng hợp (Aggregated Context)](#52-ngữ-cảnh-dự-án-được-tổng-hợp-aggregated-context)
   - 5.3 [Chuỗi Tool Calls do LLM sinh ra](#53-chuỗi-tool-calls-do-llm-sinh-ra)
   - 5.4 [Nhật ký thực thi tại Safety & Logic Validator Gate](#54-nhật-ký-thực-thi-tại-safety--logic-validator-gate)
   - 5.5 [Hiển thị Ghost Diff trên Canvas FBD](#55-hiển-thị-ghost-diff-trên-canvas-fbd)
   - 5.6 [Con người xác nhận & Biên dịch ra luật nhị phân 32-byte](#56-con-người-xác-nhận--biên-dịch-ra-luật-nhị-phân-32-byte)
6. [KẾT LUẬN & ĐỊNH HƯỚNG TRIỂN KHAI MÃ NGUỒN](#6-kết-luận--định-hướng-triển-khai-mã-nguồn)

---

## 1. TỔNG QUAN & ĐÁNH GIÁ HIỆN TRẠNG AI COPILOT (AS-IS EVALUATION)

Hiện tại, SimplePLC Studio đã tích hợp một trợ lý AI dạng ngăn kéo bên phải (`AiChatView.xaml`, `AiChatViewModel.cs`) cho phép người dùng hỏi đáp logic điều khiển và nạp nhanh cấu hình luật vào bảng quy tắc (`RuleTableViewModel`).

### 1.1 Các Điểm Sáng Hiện Tại
1. **Kiến thức quy tắc cốt lõi được định hình rõ trong System Prompt (`AiPromptBuilder.cs`)**:
   - Nắm vững nguyên tắc "1 Input ➔ 1 Output" (1 Rule = 1 Action).
   - Hiểu rõ cấu trúc pipeline 4 giai đoạn (Input $\rightarrow$ Trigger $\rightarrow$ Guard $\rightarrow$ Action).
   - Nắm được bảng mã 124+ Tag công nghiệp và cơ chế phân rã Macro cho Timer (TON, TOF, TP), Counter (CTU, CTD) và khối SCALE tuyến tính.
2. **Tích hợp mô hình trực tiếp qua REST Client (`GeminiApiClient.cs`)**:
   - Kết nối trực tiếp tới Google Generative Language API với các mô hình tốc độ cao (`gemini-3.8-flash`, `gemini-3.6-flash`).
   - Có cơ chế xử lý lỗi HTTP 503 retry tự động và fallback URL.
3. **Cơ chế tách lọc JSON và làm sạch hiển thị UX**:
   - Tách khối ````json:rules [ ... ] ```` ra khỏi phần văn bản giải thích để giao diện tin nhắn hiển thị văn bản kỹ thuật gãy gọn, có nút nạp nhanh vào Bảng Rule.

---

### 1.2 5 Lỗ Hổng Kiến Trúc & Điểm Nghẽn Nghiêm Trọng

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                        5 LỖ HỔNG KIẾN TRÚC TRỌNG YẾU CỦA AI COPILOT HIỆN TẠI           │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ [1] GIAO TIẾP VĂN BẢN ĐƠN THUẦN (TEXT-IN / TEXT-OUT)                                   │
│     - Chưa hỗ trợ Function Calling / Tool Calling native.                             │
│     - AI không thể trực tiếp tương tác hay thao tác lệnh với IDE.                      │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ [2] CƠ CHẾ PARSING REGEX CỰC KỲ GIÒN (BRITTLE REGEX & SWALLOWED EXCEPTIONS)            │
│     - AiRuleParser dùng regex đơn tuyến: `\[\s*\{.*?\}\s*\]`.                          │
│     - Lỗi JSON nhỏ sẽ kích hoạt catch {} nuốt ngoại lệ âm thầm; nút Nạp Rule biến mất! │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ [3] MÙ NGỮ CẢNH DỰ ÁN & CANVAS (80% DYNAMIC CONTEXT BLINDNESS)                         │
│     - Prompt chỉ lấy 40 Tag có Alias và 15 chuỗi Narrative của Rule.                  │
│     - Hoàn toàn MÙ về Canvas FBD, giá trị I/O thực thời, cổng COM và lỗi Linter.      │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ [4] TÁI TẠO ĐỒ THỊ MANG TÍNH PHÁ HỦY CANVAS (DESTRUCTIVE CANVAS WIPE)                  │
│     - ReconstructGraphFromRule gọi LoadGraphData() thực hiện Nodes.Clear().            │
│     - XÓA SỔ TOÀN BỘ SƠ ĐỒ ĐANG VẼ của người dùng; mất sạch các khối Macro tím/lục.    │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ [5] BỎ QUA HOÀN TOÀN BỘ KIỂM ĐỊNH AN TOÀN (100% SAFETY GATE BYPASS)                    │
│     - ApplyAiRulesToTable nhét thẳng rule vào bảng mà KHÔNG QUA RuleCompiler.Compile() │
│     - Không kiểm tra GraphGrammar, không kiểm tra WriteConflict, không kiểm tra Cycle. │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

#### Chi tiết Kỹ thuật Các Lỗ hổng:
1. **Giao tiếp một chiều dạng văn bản tự do (Text-in / Text-out)**:
   - Trong `GeminiApiClient.cs`, yêu cầu gửi lên chỉ chứa chuỗi markdown thông thường, không cấu hình trường `tools` với `functionDeclarations`. AI phản hồi chuỗi text tự do, buộc phía máy khách phải dựa vào việc bóc tách chuỗi để tìm dữ liệu máy đọc.
2. **Cơ chế Parsing bằng Regex cực kỳ giòn (Brittle Regex Parsing)**:
   - Trong `AiRuleSpecModel.cs`, hàm `AiRuleParser.ExtractRules`:
     ```csharp
     try {
         var rules = JsonSerializer.Deserialize<List<AiRuleSpecModel>>(jsonText, options);
         ...
     }
     catch {
         // If JSON is malformed, fail gracefully without breaking chat
     }
     ```
   - Khối `try-catch` nuốt trọn toàn bộ ngoại lệ. Khi AI trả về một dấu phẩy thừa, hoặc một dấu ngoặc nhọn bị thiếu, hệ thống âm thầm trả về danh sách rỗng. Người dùng nhìn thấy giải thích nhưng không có nút nạp và không biết nguyên nhân tại sao.
3. **Mù ngữ cảnh FBD Canvas và Trạng thái Thời gian thực (Context Blindness)**:
   - Trong `AiPromptBuilder.cs:204-237`, danh mục Tag chỉ lấy tối đa 40 tag có đặt bí danh (`!string.IsNullOrWhiteSpace(t.Alias)`). Nếu kỹ sư chưa đặt tên gợi nhớ cho `DI2` hay `DO3`, AI hoàn toàn không biết đến sự tồn tại của các chân này.
   - Đối với Rule hiện có, prompt chỉ gửi chuỗi tường thuật thô (`$"- Rule {r.Index}: {r.Narrative}"`), bỏ qua toàn bộ thông số kỹ thuật (Trigger, Guard, TargetTag, ActionType).
   - AI hoàn toàn không biết trên FBD Canvas đang có những khối nào, vị trí tọa độ ra sao, các chân đang nối thế nào; không biết giá trị đo trực tiếp (Live process data); không biết trạng thái kết nối phần cứng (COM port, Run/Stop); và không biết các mã lỗi biên dịch hiện hành.
4. **Tái tạo đồ thị mang tính phá hủy Canvas (`Destructive Canvas Wipe`)**:
   - Trong `LogicEditorViewModel.cs:2188`, phương thức `ReconstructGraphFromRule` gọi trực tiếp `LoadGraphData(...)`.
   - Trong `LoadGraphData` (`LogicEditorViewModel.cs:2098`):
     ```csharp
     Connections.Clear();
     Nodes.Clear();
     SelectedNode = null;
     ```
   - Thao tác này **xóa sạch toàn bộ sơ đồ hiện có** của kỹ sư. Hơn nữa, nó chỉ dựng lại một chuỗi cứng 3-4 node thô sơ (`Input` $\rightarrow$ `Trigger` $\rightarrow$ `[Guard]` $\rightarrow$ `Action`), biến các khối cao cấp như Timer TON/TOF/TP tím hay Counter CTU lục thành các khối logic rời rạc không thể hiểu được.
5. **Bỏ qua hoàn toàn bộ kiểm định an toàn (Safety Gate Bypass)**:
   - Trong `MainViewModel.cs:1987-2094`, hàm `ApplyAiRulesToTable` đẩy thẳng các rule vào `RuleTableVM.Rules` **mà không hề thông qua `RuleCompiler.Compile()`**, không kiểm tra ngữ pháp đồ thị (`GraphGrammarV1`), không kiểm tra xung đột ghi đồng thời (`WriteConflictValidator`), và không kiểm tra chu trình phụ thuộc dữ liệu (`RuleDependencyGraph`). Nếu AI sinh rule ghi đè ngõ ra hoặc sai Tag, lỗi chỉ bị phát hiện muộn màng khi nạp xuống phần cứng (Deploy).

---

## 2. KIẾN TRÚC PHÂN TẦNG 5 LỚP THẾ HỆ MỚI (5-LAYER NEXT-GEN ARCHITECTURE)

Nhằm giải quyết triệt để các bất cập trên, nâng cấp AI Copilot thành một **Kỹ sư Tự động hóa Ảo (Autonomous Industrial Copilot)** đẳng cấp thế giới, kiến trúc thế hệ mới được thiết kế phân tầng 5 lớp độc lập, khép kín và an toàn tuyệt đối.

### 2.1 Sơ Đồ Khối Tổng Thể (Architectural Block Diagram)

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                        KIẾN TRÚC 5 TẦNG INDUSTRIAL AI COPILOT                          │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ LAYER 1: DEEP PROJECT SEMANTIC CONTEXT AGGREGATOR                                     │
│ ┌──────────────────────┬──────────────────────┬──────────────────────┬───────────────┐ │
│ │ Full Tag Catalog     │ FBD Canvas Topology  │ Live Process Values  │ Linter Errors │ │
│ │ (124+ Tags, Addr)    │ (Nodes, Wires, Coords│ (DI/DO/AI at 5Hz)    │ (SPLC-GRAPH)  │ │
│ └──────────────────────┴──────────────────────┴──────────────────────┴───────────────┘ │
├────────────────────────────────────────────────────────────────────────────────────────┤
│                                        │ (Enriched Semantic JSON Payload)              │
│                                        ▼                                               │
│ LAYER 2: LLM ENGINE WITH BIDIRECTIONAL TOOL CALLING (GEMINI / OPENAI STRICT MODE)      │
│ ┌────────────────────────────────────────────────────────────────────────────────────┐ │
│ │ System Prompt: Industrial Safety (Fail-Safe, Interlocks, E-Stop, Dominant-Reset)   │ │
│ │ Registered Tools: add_node, connect_wires, configure_timer, auto_layout, ...       │ │
│ └────────────────────────────────────────────────────────────────────────────────────┘ │
├────────────────────────────────────────────────────────────────────────────────────────┤
│                                        │ (Structured Tool Call Arguments)              │
│                                        ▼                                               │
│ LAYER 3: PRE-EXECUTION SAFETY & LOGIC VALIDATOR GATE                                   │
│ ┌──────────────────────┬──────────────────────┬──────────────────────┬───────────────┐ │
│ │ GraphGrammarV1 Check │ WriteConflictValidator│ RuleDependencyGraph │ Self-Healing  │ │
│ │ (SPLC-GRAPH-001..010)│ (Multi-writer coil)  │ (Topological Cycle)  │ Retry Loop    │ │
│ └──────────────────────┴──────────────────────┴──────────────────────┴───────────────┘ │
├────────────────────────────────────────────────────────────────────────────────────────┤
│                                        │ (Validated Safe Graph Delta Ops)              │
│                                        ▼                                               │
│ LAYER 4: NODIFY CANVAS VISUAL DIFF ENGINE (GHOST PREVIEW LAYER)                        │
│ ┌────────────────────────────────────────────────────────────────────────────────────┐ │
│ │ Ghost Nodes: IsGhost=true, Opacity=0.85, Viền nét đứt xanh ngọc (#00A389)          │ │
│ │ Ghost Wires: Dây nét đứt phát sáng; Khối đề xuất xóa: Viền đỏ mờ (#DC2626)          │ │
│ │ Hoàn toàn KHÔNG XÓA sơ đồ hiện có của người dùng!                                   │ │
│ └────────────────────────────────────────────────────────────────────────────────────┘ │
├────────────────────────────────────────────────────────────────────────────────────────┤
│                                        │ (Pending Visual Preview)                      │
│                                        ▼                                               │
│ LAYER 5: HUMAN AFFIRMATION GATE & TRANSACTION ROLLBACK                                 │
│ ┌────────────────────────────────────────────────────────────────────────────────────┐ │
│ │ Floating Action Bar trên đỉnh Canvas: [✓ Chấp nhận (Accept)] [✕ Hủy bỏ (Reject)]   │ │
│ │ Accept ➔ Commit vào GraphHistoryService Undo Stack (Ctrl+Z) ➔ Compile 32-Byte Rule │ │
│ │ Reject ➔ Hủy bỏ toàn bộ Ghost Elements, khôi phục nguyên trạng 100%                 │ │
│ └────────────────────────────────────────────────────────────────────────────────────┘ │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

---

### 2.2 Layer 1: Deep Project Semantic Context Aggregator
Dịch vụ mới `AiContextAggregator.cs` chịu trách nhiệm thu thập và nén ngữ cảnh dự án thành một cấu trúc JSON ngữ nghĩa gửi kèm theo mỗi lượt gọi LLM:
1. **Toàn Bộ Danh Mục Tag (Full Tag Catalog)**:
   - Quét toàn bộ 124+ Tag từ `TagCatalogViewModel`, bao gồm cả các tag chưa đặt Alias.
   - Ghi rõ: `Name` (`DI0`), `Kind` (`DigitalInput`), `DataType` (`BOOL`), `ModbusAddress` (`0x0900`), `Alias` ("Nút Khởi Động"), và `IsRetentive`.
2. **Cấu Trúc Đồ Thị FBD Hiện Hành (Canvas Topology & Geometry)**:
   - Danh sách toàn bộ các Node hiện có trên Canvas: `NodeId`, `Type` (`Input`, `Timer`, `Action`), `AssociatedTag`, tọa độ `(X, Y)`, kích thước `(Width, Height)`.
   - Danh sách các dây nối: `ConnectionId`, `SourceNodeId`, `SourcePort`, `TargetNodeId`, `TargetPort`.
   - Danh sách các Node đang được người dùng quét chọn (`SelectedNodeIds`).
3. **Giá Trị Quy Trình Thời Gian Thực (Live Process Data)**:
   - Trích xuất trạng thái thực thời từ `IRuntimeStateStore` ở tần số 5 Hz: Cổng `DI0 = 1`, `AI0 = 6250 mV`, `DO0 = 0`.
   - Giúp AI có thể trả lời các câu hỏi vận hành thực tế: *"Tại sao bơm DO0 của tôi hiện thời không chạy?"*.
4. **Trạng Thái Phần Cứng & Kết Nối (Hardware & Connection State)**:
   - Trạng thái cổng kết nối: `COM5` (Online / 115200 bps / RTU) hoặc `Simulator`.
   - Trạng thái vòng đời MCU: `ENGINE_RUNNING` hay `DIAG_CONTROL`.
   - Chu kỳ quét trung bình: `ScanPass = 12ms`, `CpuLoad = 14%`.
5. **Nhật Ký Chẩn Đoán Cú Pháp & Lỗi Linter (Active Diagnostics)**:
   - Các mã lỗi biên dịch hiện hành từ `LogicEditorViewModel.Diagnostics` (ví dụ: `SPLC-GRAPH-007: Chained Guards forbidden`).

---

### 2.3 Layer 2: LLM Engine with Bidirectional Tool Calling
Nâng cấp `GeminiApiClient.cs` và bổ sung chuẩn OpenAI Strict Mode:
- **Cấu hình Payload**:
  Khởi tạo tham số `tools` chứa mảng các `functionDeclarations`.
- **Cơ chế Gọi Công Cụ Hai Chiều**:
  Khi người dùng yêu cầu chỉnh sửa sơ đồ, mô hình không sinh văn bản markdown mà phát ra một hoặc nhiều lệnh gọi hàm có cấu trúc (`ToolCall`).
- **Xử lý Vòng lặp Công cụ (Agentic Tool Loop)**:
  Phần mềm thực thi lệnh gọi công cụ trong tầng ứng dụng, sau đó gửi kết quả (`ToolResult`) ngược trở lại cho LLM để mô hình đưa ra lời giải thích kỹ thuật hoàn chỉnh cho kỹ sư.

---

### 2.4 Layer 3: Pre-Execution Safety & Logic Validator Gate
Mọi lệnh gọi công cụ do AI phát ra đều phải đi qua Cổng Kiểm Duyệt An Toàn (`AiSafetyGate.cs`) trước khi được phép chạm vào Canvas:
1. **Kiểm Tra Ngữ Pháp Đồ Thị (`GraphGrammarV1`)**:
   - Ngăn chặn kết nối sai quy chuẩn (ví dụ nối thẳng Input sang Action không qua Trigger).
   - Kiểm tra mã lỗi `SPLC-GRAPH-007` (Chained Guards).
   - Đảm bảo mỗi chân Input chỉ nhận duy nhất 1 dây nối (`SPLC-GRAPH-010`).
   - Cấm khối Action làm nguồn phát dây (`SPLC-GRAPH-002: ErrActionSource`).
   - Khối Timer/Counter chỉ nhận ngõ vào từ khối Input (`SPLC-GRAPH-TIMER-IN`).
2. **Thẩm Định Xung Đột Ghi Đồng Thời (`WriteConflictValidator`)**:
   - Kiểm tra xem lệnh mới có vô tình tạo thêm một cuộn dây thứ hai cùng ghi vào `TargetTag` (Double Coiling) hay không.
   - Nhận diện các ngoại lệ hợp lệ nếu các lệnh ghi xuất phát từ cùng một Macro Instance (như khối TON hay Counter) hoặc cặp lệnh Set/Reset có điều kiện loại trừ lẫn nhau.
3. **Phân Tích Chu Trình Phụ Thuộc (`RuleDependencyGraph`)**:
   - Sử dụng thuật toán sắp xếp Topo của Kahn để đảm bảo sơ đồ mới không tạo thành vòng lặp logic kín gây treo chu kỳ quét (`SPLC-COMP-DEP-001`).
4. **Thẩm Định Ngữ Nghĩa Kiểu Dữ Liệu Tag (Semantic Tag Validator)**:
   - **Chống Ghi Ngõ Vào Vật Lý (Read-Only Invariant)**: Nghiêm cấm các Action (`SET_TAG`, `TOGGLE_TAG`, `INC_COUNTER`) gán vào các Tag thuộc loại `TagKind.DiscreteInput` (`DI`) hoặc `TagKind.AnalogInput` (`AI`). Mã lỗi vi phạm: `SPLC-TAG-ERR-READONLY`.
   - **Tương Thích Kiểu Dữ Liệu (Type Compatibility)**: Action `TOGGLE_TAG` và `SET_TAG (0/1)` chỉ được phép áp dụng cho `TagDataType.Boolean` (`DO`, `VFLAG`, `MB_COIL`). Action `INC_COUNTER` chỉ được phép áp dụng cho kiểu `TagDataType.Int16` / `Int32` (`VREG`, `MB_HOLDING`, `COUNTER`). Guard Node chỉ được phép liên kết với Tag kiểu Boolean (nếu gán Tag Analog, hệ thống từ chối với mã lỗi `SPLC-TAG-ERR-GUARD-NOT-BOOL`).
   - **Kiểm Tra Tag Tồn Tại & Giới Hạn Phần Cứng**: Mọi `tag_name` do AI sinh ra phải tồn tại trong `ProductDefinition.Tags`. Nếu muốn tạo mới phải gọi công cụ alias trước.
5. **Vòng Tự Chữa Lỗi (Self-Healing Retry Loop)**:
   - Nếu phát hiện vi phạm an toàn, hệ thống **không báo lỗi cụt ngủ cho người dùng**, mà tự động đóng gói mã lỗi gửi ngược lại cho LLM:
     ```json
     {
       "error": "SPLC-GRAPH-007: Vi phạm quy tắc ECA: Không được nối 2 khối Guard liên tiếp.",
       "guidance": "Hãy chèn một cờ trung gian VFLAG để lưu kết quả điều kiện thứ nhất trước khi kích hoạt nhánh tiếp theo."
     }
     ```
   - LLM tự động sửa lại Tool Calls và thử lại tối đa 3 lần.

#### Cơ Chế Đóng Gói Giao Dịch (Draft Graph Transaction / Unit of Work Pattern)
Khi AI phát ra một lượt gọi gồm nhiều tool calls (ví dụ 10-15 calls), việc kiểm tra `AiSafetyGate` ngay sau từng tool call đơn lẻ sẽ gây lỗi biên dịch giả tạo (node vừa tạo chưa kịp nối dây sẽ báo `SPLC-COMP-001`, Guard vừa tạo chưa có Trigger sẽ báo `SPLC-COMP-002`).

Hệ thống triển khai mẫu thiết kế **Unit of Work** thông qua đối tượng `DraftGraphTransaction`:
```text
┌────────────────────────────────────────────────────────────────────────┐
│                   DRAFT GRAPH TRANSACTION LIFECYCLE                    │
├────────────────────────────────────────────────────────────────────────┤
│ 1. LLM Response Multi-Tool Calls                                       │
│    └─► Bắt đầu phiên giao dịch: var tx = new DraftGraphTransaction()   │
│                                                                        │
│ 2. In-Memory Delta Execution (Chưa tác động lên UI Canvas thật)        │
│    ├─ Step 1..N: Áp dụng các thay đổi add/connect vào tx.StagingGraph   │
│    └─ Gom toàn bộ các cảnh báo cú pháp sơ bộ.                         │
│                                                                        │
│ 3. Atomic Safety Gate Gatekeeper Validation                            │
│    └─► AiSafetyGate.Validate(tx.StagingGraph)                          │
│        ├─ NẾU HỢP LỆ:                                                  │
│        │   Chuyển tx.StagingGraph thành Ghost Elements trên UI Canvas.  │
│        │   Hiển thị Floating Action Bar cho người dùng review.         │
│        └─ NẾU THẤT BẠI:                                                │
│            Rollback toàn bộ Transaction! Canvas thật không bị ô nhiễm. │
│            Kích hoạt Self-Healing Loop: Trả lỗi chi tiết về LLM.       │
└────────────────────────────────────────────────────────────────────────┘
```

---

### 2.5 Layer 4: Nodify Canvas Visual Diff Engine (Ghost Preview)
Khắc phục hoàn toàn sự cố xóa sạch sơ đồ của `Nodes.Clear()`:
1. **Mở rộng Thuộc tính ViewModel**:
   - `GraphNodeViewModel.cs`: Bổ sung `IsGhost` (`bool`), `DiffStatus` (`None`, `Added`, `Modified`, `Deleted`).
   - `ConnectionViewModel.cs`: Bổ sung `IsGhost` (`bool`), `DiffStatus`.
2. **Hiển Thị Đồ Họa Nét Đứt Trong Suốt (Ghost Rendering)**:
   - Các khối do AI đề xuất thêm mới:
     - Độ mờ: `Opacity = 0.85`.
     - Viền khối: Màu xanh ngọc `#00A389`, nét đứt `StrokeDashArray="4 2"`.
     - Huy hiệu góc: Biểu tượng AI nhỏ "✨ AI Proposal".
   - Các đường dây do AI đề xuất:
     - Màu xanh ngọc `#00A389`, nét đứt, độ dày `StrokeThickness = "2.5"`.
   - Các khối đề xuất bị xóa:
     - Viền đỏ `#DC2626`, nét đứt, độ mờ `Opacity = 0.5`.
3. **Cơ Chế Cách Ly Triệt Để Ghost Elements Khỏi Bộ Biên Dịch & Tệp Lưu Trữ (`!n.IsGhost`)**:
   Để ngăn chặn tuyệt đối hiện tượng rò rỉ các phần tử Ghost vào logic chạy thật hoặc tệp lưu trữ dự án, `LogicEditorViewModel.cs` thực thi 3 rào chắn cách ly nghiêm ngặt:
   - **Rào chắn 1: Cách ly Bộ Biên dịch (`CompileAndSaveRules`)**:
     ```csharp
     var activeNodes = Nodes.Where(n => !n.IsGhost).ToList();
     var activeConnections = Connections.Where(c => !c.IsGhost).ToList();
     var logicGraph = BuildLogicGraphFromElements(activeNodes, activeConnections);
     var compileResult = _ruleCompiler.Compile(logicGraph, CurrentProduct);
     ```
   - **Rào chắn 2: Cách ly Lưu Tệp Dự Án (`ExportGraphData`)**:
     Khi người dùng nhấn `Ctrl + S`, phương thức tuần tự hóa dự án chỉ trích xuất các khối thật:
     ```csharp
     var projectData = new ProjectGraphData
     {
         Nodes = Nodes.Where(n => !n.IsGhost).Select(ExportNodeData).ToList(),
         Connections = Connections.Where(c => !c.IsGhost).Select(ExportConnectionData).ToList()
     };
     ```
   - **Rào chắn 3: Vô hiệu hóa Tương tác Chuột trên Ghost Connectors**:
     Trong Style `NodeTemplates.xaml`, khi `IsGhost == true`: gán `IsHitTestVisible = false` cho các `ConnectorViewModel` của Ghost Node, ngăn không cho người dùng vô tình kéo dây từ Node thật sang Node Ghost trong lúc chưa bấm [Accept].

---

### 2.6 Layer 5: Human Affirmation Gate & Transaction Rollback
Tại `LogicEditorView.xaml`, một thanh điều khiển nổi xuất hiện trên đỉnh Canvas:

```xml
<!-- FLOATING AI PROPOSAL ACTION BAR -->
<Border Grid.Row="0" VerticalAlignment="Top" HorizontalAlignment="Center" Margin="0,12,0,0"
        Background="#FFFFFF" BorderBrush="#006487" BorderThickness="1.5" CornerRadius="4"
        Padding="14,8" Visibility="{Binding HasPendingAiProposal, Converter={StaticResource BoolToVis}}"
        Panel.ZIndex="1000">
    <Border.Effect>
        <DropShadowEffect BlurRadius="12" ShadowDepth="2" Direction="270" Color="#000000" Opacity="0.15" />
    </Border.Effect>
    <StackPanel Orientation="Horizontal" VerticalAlignment="Center">
        <TextBlock Text="🤖 " FontSize="14" VerticalAlignment="Center" />
        <TextBlock Text="{Binding AiProposalSummaryText}" FontWeight="SemiBold" FontSize="12" Foreground="#0F172A" VerticalAlignment="Center" Margin="0,0,12,0" />
        
        <!-- Safety Badge -->
        <Border Background="#E6F4EA" BorderBrush="#86EFAC" BorderThickness="1" CornerRadius="2" Padding="6,2" Margin="0,0,12,0">
            <TextBlock Text="✓ An toàn: 0 xung đột ghi" FontSize="10.5" FontWeight="SemiBold" Foreground="#0E6B37" />
        </Border>

        <!-- Accept Button -->
        <Button Command="{Binding AcceptAiProposalCommand}" Background="#107C41" Foreground="#FFFFFF"
                Padding="12,4" FontWeight="SemiBold" FontSize="11.5" Margin="0,0,6,0" Cursor="Hand"
                Content="✓ Chấp nhận (Accept)" />

        <!-- Reject Button -->
        <Button Command="{Binding RejectAiProposalCommand}" Background="#F1F5F9" BorderBrush="#CBD5E1" BorderThickness="1"
                Padding="10,4" FontSize="11.5" Foreground="#475569" Cursor="Hand"
                Content="✕ Hủy bỏ (Reject)" />
    </StackPanel>
</Border>
```

#### Quy Trình Xử Lý Xác Nhận:
- **Khi Kỹ Sư Nhấn [✓ Chấp nhận (Accept)]**:
  1. Ghi lại snapshot trạng thái trước khi áp dụng vào `GraphHistoryService` (`RecordSnapshot("Apply AI Proposal")`), cho phép hoàn tác bất kỳ lúc nào qua phím tắt `Ctrl + Z`.
  2. Chuyển đổi trạng thái toàn bộ các node/dây từ `IsGhost = true` sang `IsGhost = false`, `DiffStatus = None`.
  3. Kích hoạt biên dịch tự động `CompileAndSaveRules()`.
  4. Đóng thanh Action Bar và hiển thị thông báo thành công.
- **Khi Kỹ Sư Nhấn [✕ Hủy bỏ (Reject)]**:
  1. Xóa toàn bộ các node và dây có cờ `IsGhost == true` ra khỏi bộ sưu tập `Nodes` và `Connections`.
  2. Khôi phục nguyên vẹn $100\%$ sơ đồ ban đầu của người dùng mà không để lại bất kỳ rác dữ liệu nào.

---

### 2.7 Phân Cấp An Toàn Máy Móc Công Nghiệp (ISO 13849-1 & NFPA 79)

Cả hệ thống Copilot và kỹ sư tự động hóa cần nắm vững ranh giới an toàn chức năng máy móc theo các tiêu chuẩn quốc tế:

#### 1. Tiêu Chuẩn ISO 13849-1 (An Toàn Máy Móc — Cấp Độ Hiệu Năng PL & Kiến Trúc Category)
- **Hiện trạng phần cứng SimplePLC**: Bộ điều khiển sử dụng vi điều khiển STM32F401RE đơn kênh (Single-Channel Architecture), không có vi xử lý giám sát chéo độc lập (Không có Dual-Channel Redundancy 1oo2D hay 2oo3), không có giao thức truyền thông an toàn Black Channel (PROFIsafe, TwinSAFE).
- **Phân loại Tiêu chuẩn**:
  * Theo ISO 13849-1 Bảng 2 và Bảng 4, kiến trúc đơn kênh của SimplePLC chỉ đạt tối đa **Category 1 (Performance Level PL c)**.
  * Mọi chức năng an toàn cấp cao đòi hỏi **Category 3 / Category 4, PL d / PL e** (như Dừng Khẩn Cấp E-Stop, Cảm biến quang an toàn Light Curtain, Khóa liên động cửa bảo vệ Safety Gate Switch) **BẮT BUỘC PHẢI SỬ DỤNG MODULE RƠ-LE AN TOÀN PHẦN CỨNG BÊN NGOÀI (EXTERNAL SAFETY RELAY)** (ví dụ: Pilz PNOZ, Schneider Preventa, Siemens SIRIUS).
  * Rơ-le an toàn ngoài sẽ cắt trực tiếp nguồn cuộn hút Contactor động lực về mặt cơ-điện. SimplePLC chỉ nhận tiếp điểm phụ không an toàn (Auxiliary Dry Contact) để giám sát và hiển thị trạng thái trên HMI.

#### 2. Tiêu Chuẩn NFPA 79 & IEC 60204-1 (Phân Loại Chức Năng Dừng — Stop Categories)
- **Stop Category 0 (Ngắt nguồn tức thì / Uncontrolled Stop)**:
  * Ngắt nguồn năng lượng cung cấp cho cơ cấu chấp hành máy ngay lập tức (Immediate power cutoff < 20ms).
  * **Quy chuẩn E-Stop**: Chức năng Dừng Khẩn Cấp bắt buộc phải là Stop Category 0 (hoặc Category 1 nếu việc ngắt có kiểm soát an toàn hơn).
  * **Điều cấm kỵ**: Tuyệt đối **không bao giờ được để một khối trễ quy trình (Process Timer như TOF) trì hoãn lệnh ngắt Category 0**. Tín hiệu E-Stop `DI2 ON_FALL` phải ngắt trực tiếp cả `VFLAG0 = 0` và cưỡng bức cắt ngay `DO0 = 0` trong cùng chu kỳ quét hiện thời (< 20ms).
- **Stop Category 1 (Dừng có kiểm soát / Controlled Stop)**:
  * Duy trì năng lượng cho cơ cấu chấp hành để hãm phanh có kiểm soát hoặc hoàn tất chu trình làm mát/xả áp, sau đó mới cắt hoàn toàn nguồn.
  * Ứng dụng: Nút Dừng Thường `DI1` kích hoạt khối TOF 5000ms duy trì bơm làm mát thêm 5 giây trước khi ngắt.

---

## 3. DANH MỤC CÁC BỘ FUNCTION CALLING JSON SCHEMAS CHUẨN HÓA (STRICT MODE)

Dưới đây là 9 bộ định nghĩa Function Calling JSON Schemas chuẩn xác tuân thủ các quy tắc nghiêm ngặt của **OpenAI Strict Mode** (`strict: true`, `"additionalProperties": false`, và mọi trường trong `properties` đều có mặt trong mảng `required`).

---

### 3.1 `add_node`
Dùng để tạo một khối logic mới trên FBD Canvas tại tọa độ xác định.

```json
{
  "name": "add_node",
  "description": "Tạo một khối logic mới trên FBD Canvas của SimplePLC Studio với tọa độ và cấu hình cụ thể.",
  "strict": true,
  "parameters": {
    "type": "object",
    "properties": {
      "node_id": {
        "type": "string",
        "description": "Mã định danh duy nhất của khối (ví dụ: 'node_in_start', 'node_act_pump')."
      },
      "node_type": {
        "type": "string",
        "description": "Loại khối logic cần tạo trên canvas.",
        "enum": ["INPUT", "TRIGGER", "GUARD", "ACTION", "TIMER", "COUNTER", "SCALE"]
      },
      "custom_label": {
        "type": "string",
        "description": "Nhãn gợi nhớ hiển thị trên đỉnh của khối (ví dụ: 'Nút Start', 'Bơm Chính', 'Trễ Khởi Động')."
      },
      "position_x": {
        "type": "number",
        "description": "Tọa độ X trên không gian vô hạn Canvas (bước nhảy lưới tiêu chuẩn 220px)."
      },
      "position_y": {
        "type": "number",
        "description": "Tọa độ Y trên không gian vô hạn Canvas."
      },
      "tag_name": {
        "type": "string",
        "description": "Mã Tag liên kết (chỉ áp dụng cho INPUT, GUARD, ACTION). Ví dụ: 'DI0', 'DO1', 'AI0', 'VFLAG0', hoặc 'NONE'."
      },
      "trigger_type": {
        "type": "string",
        "description": "Kiểu kích hoạt (chỉ áp dụng cho TRIGGER).",
        "enum": ["NONE", "ON_CHANGE", "ON_RISE", "ON_FALL", "INTERVAL"]
      },
      "compare_op": {
        "type": "string",
        "description": "Toán tử so sánh ngưỡng (dùng cho TRIGGER so sánh Analog hoặc GUARD).",
        "enum": ["NONE", "EQ", "NEQ", "GT", "LT", "GTE", "LTE", "BETWEEN"]
      },
      "threshold_lo": {
        "type": "integer",
        "description": "Ngưỡng so sánh dưới (cho TRIGGER/GUARD). Mặc định 0."
      },
      "threshold_hi": {
        "type": "integer",
        "description": "Ngưỡng so sánh trên (chỉ dùng khi compare_op là BETWEEN). Mặc định 0."
      },
      "debounce_ms": {
        "type": "integer",
        "description": "Thời gian lọc nhiễu dội phím hoặc giữ mức tín hiệu liên tục (for_ms). Đơn vị ms."
      },
      "action_type": {
        "type": "string",
        "description": "Hành vi thực thi (chỉ áp dụng cho ACTION).",
        "enum": ["NONE", "SET_TAG", "TOGGLE_TAG", "INC_COUNTER", "ADD_TAG", "SCALE_TAG"]
      },
      "action_param": {
        "type": "integer",
        "description": "Tham số tác vụ (ví dụ: 1 để bật, 0 để tắt, +1 để tăng đếm)."
      }
    },
    "required": [
      "node_id", "node_type", "custom_label", "position_x", "position_y", 
      "tag_name", "trigger_type", "compare_op", "threshold_lo", 
      "threshold_hi", "debounce_ms", "action_type", "action_param"
    ],
    "additionalProperties": false
  }
}
```

---

### 3.2 `remove_node`
Dùng để gỡ bỏ một khối logic khỏi Canvas, đồng thời tự động thu hồi toàn bộ các đường dây nối liên kết với khối đó.

```json
{
  "name": "remove_node",
  "description": "Xóa một khối logic khỏi Canvas dựa trên định danh Node ID, đồng thời tự động thu hồi tất cả các dây nối liên kết.",
  "strict": true,
  "parameters": {
    "type": "object",
    "properties": {
      "node_id": {
        "type": "string",
        "description": "Định danh duy nhất của khối cần xóa (ví dụ: 'node_old_trigger')."
      },
      "reason": {
        "type": "string",
        "description": "Lý do kỹ thuật giải thích việc loại bỏ khối này khỏi sơ đồ."
      }
    },
    "required": ["node_id", "reason"],
    "additionalProperties": false
  }
}
```

---

### 3.3 `connect_wires`
Dùng để tạo đường dây nối truyền tín hiệu giữa cổng xuất (Out) của một khối nguồn tới cổng nhập (In) của một khối đích.

```json
{
  "name": "connect_wires",
  "description": "Tạo đường dây nối truyền tín hiệu giữa cổng xuất (Out) của một khối nguồn tới cổng nhập (In) của một khối đích trên Canvas.",
  "strict": true,
  "parameters": {
    "type": "object",
    "properties": {
      "connection_id": {
        "type": "string",
        "description": "Mã định danh duy nhất cho đường dây mới (ví dụ: 'wire_start_to_guard')."
      },
      "source_node_id": {
        "type": "string",
        "description": "Mã định danh của khối nguồn phát tín hiệu."
      },
      "source_connector_title": {
        "type": "string",
        "description": "Tên cổng xuất của khối nguồn ('Out', 'Q', hoặc 'OUT').",
        "enum": ["Out", "Q", "OUT"]
      },
      "target_node_id": {
        "type": "string",
        "description": "Mã định danh của khối đích nhận tín hiệu."
      },
      "target_connector_title": {
        "type": "string",
        "description": "Tên cổng nhập của khối đích ('In', 'CU', 'CD', 'R', 'IN').",
        "enum": ["In", "CU", "CD", "R", "IN"]
      }
    },
    "required": [
      "connection_id", "source_node_id", "source_connector_title", 
      "target_node_id", "target_connector_title"
    ],
    "additionalProperties": false
  }
}
```

---

### 3.4 `disconnect_wire`
Dùng để gỡ bỏ một đường dây kết nối cụ thể giữa hai khối.

```json
{
  "name": "disconnect_wire",
  "description": "Gỡ bỏ một đường dây kết nối cụ thể giữa hai khối trên Canvas dựa trên mã dây hoặc cặp khối liên kết.",
  "strict": true,
  "parameters": {
    "type": "object",
    "properties": {
      "connection_id": {
        "type": "string",
        "description": "Mã định danh duy nhất của đường dây cần gỡ (ví dụ: 'wire_1' hoặc 'AUTO')."
      },
      "source_node_id": {
        "type": "string",
        "description": "ID của khối nguồn phát nối dây."
      },
      "target_node_id": {
        "type": "string",
        "description": "ID của khối đích nhận nối dây."
      }
    },
    "required": ["connection_id", "source_node_id", "target_node_id"],
    "additionalProperties": false
  }
}
```

---

### 3.5 `configure_timer_counter`
Dùng để cấu hình thông số vận hành cho khối Macro Timer (TON/TOF/TP) hoặc Counter (CTU/CTD) hiện có trên Canvas.

```json
{
  "name": "configure_timer_counter",
  "description": "Cấu hình chi tiết tham số vận hành cho khối Macro Timer (TON/TOF/TP) hoặc Counter (CTU/CTD) hiện có trên Canvas.",
  "strict": true,
  "parameters": {
    "type": "object",
    "properties": {
      "node_id": {
        "type": "string",
        "description": "ID của khối Timer hoặc Counter cần cấu hình."
      },
      "mode": {
        "type": "string",
        "description": "Chế độ định thời hoặc đếm.",
        "enum": ["TON", "TOF", "TP", "CTU", "CTD"]
      },
      "preset_value": {
        "type": "integer",
        "description": "Thời gian đặt PT (đơn vị ms cho Timer) hoặc giá trị ngưỡng đếm PV (cho Counter)."
      },
      "input_tag_name": {
        "type": "string",
        "description": "Mã Tag đầu vào kích hoạt khối (IN cho Timer, CU/CD cho Counter, ví dụ: 'VFLAG0', 'DI0', 'NONE')."
      },
      "cv_tag_name": {
        "type": "string",
        "description": "Tag lưu giá trị đếm hiện thời (áp dụng cho Counter, ví dụ: 'VREG_RETAIN0', 'VREG0', hoặc 'NONE')."
      },
      "reset_tag_name": {
        "type": "string",
        "description": "Tag tín hiệu xóa bộ đếm (Reset Tag, ví dụ: 'DI2', 'NONE')."
      },
      "output_tag_name": {
        "type": "string",
        "description": "Tag ngõ ra chỉ thị đạt ngưỡng Q (ví dụ: 'DO0', 'VFLAG1')."
      }
    },
    "required": [
      "node_id", "mode", "preset_value", "input_tag_name", 
      "cv_tag_name", "reset_tag_name", "output_tag_name"
    ],
    "additionalProperties": false
  }
}
```

---

### 3.6 `configure_scale`
Dùng để cấu hình tham số biến đổi tuyến tính tín hiệu tương tự (Analog Linear Scaling: $y = Gain \times x + Offset$) và giới hạn an toàn kẹp biên cho khối SCALE.

```json
{
  "name": "configure_scale",
  "description": "Cấu hình tham số biến đổi tuyến tính tín hiệu tương tự (Analog Linear Scaling: y = Gain * x + Offset) và giới hạn an toàn cho khối SCALE.",
  "strict": true,
  "parameters": {
    "type": "object",
    "properties": {
      "node_id": {
        "type": "string",
        "description": "ID của khối SCALE cần cấu hình."
      },
      "gain": {
        "type": "number",
        "description": "Hệ số khuếch đại / độ dốc Gain (ví dụ: 0.01 cho chuyển đổi 0..10000 thành 0..100)."
      },
      "offset": {
        "type": "number",
        "description": "Hệ số bù / dịch chuyển Offset (ví dụ: -20.0 cho cảm biến 4-20mA dải âm)."
      },
      "unit": {
        "type": "string",
        "description": "Đơn vị vật lý kỹ thuật hiển thị (ví dụ: 'bar', '°C', 'RPM', 'kg/h')."
      },
      "decimal_places": {
        "type": "integer",
        "description": "Số chữ số phần thập phân hiển thị trên giao diện (mặc định 1)."
      },
      "is_clamped": {
        "type": "boolean",
        "description": "Kích hoạt giới hạn kẹp giá trị an toàn trong khoảng ClampMin đến ClampMax."
      },
      "clamp_min": {
        "type": "number",
        "description": "Giá trị kẹp dưới an toàn (Min Clamping Limit)."
      },
      "clamp_max": {
        "type": "number",
        "description": "Giá trị kẹp trên an toàn (Max Clamping Limit)."
      }
    },
    "required": [
      "node_id", "gain", "offset", "unit", "decimal_places", 
      "is_clamped", "clamp_min", "clamp_max"
    ],
    "additionalProperties": false
  }
}
```

---

### 3.7 `rename_tag`
Dùng để cập nhật bí danh gợi nhớ (Alias), đơn vị đo lường, hoặc mô tả kỹ thuật cho một Tag trong danh bạ Tag Catalog.

```json
{
  "name": "rename_tag",
  "description": "Cập nhật bí danh gợi nhớ (Alias), đơn vị đo lường, hoặc mô tả kỹ thuật cho một Tag trong danh bạ Tag Catalog.",
  "strict": true,
  "parameters": {
    "type": "object",
    "properties": {
      "tag_name": {
        "type": "string",
        "description": "Mã Tag phần cứng hệ thống (ví dụ: 'DI0', 'DO1', 'AI2', 'VREG_RETAIN5')."
      },
      "new_alias": {
        "type": "string",
        "description": "Tên bí danh gợi nhớ mới (ví dụ: 'Cảm biến áp suất bồn dầu', 'Động cơ khuấy')."
      },
      "description": {
        "type": "string",
        "description": "Mô tả chi tiết vị trí lắp đặt, công năng hoặc chuẩn tín hiệu."
      }
    },
    "required": ["tag_name", "new_alias", "description"],
    "additionalProperties": false
  }
}
```

---

### 3.8 `auto_layout_graph`
Dùng để tự động căn chỉnh và phân tầng các khối trên Canvas theo giải thuật Sugiyama công nghiệp.

```json
{
  "name": "auto_layout_graph",
  "description": "Tự động sắp xếp vị trí các khối trên Canvas theo cấu trúc phân tầng công nghiệp (Sugiyama Layered Layout: Input ở cột 0, Trigger ở cột 1, Guard ở cột 2, Action ở cột 3).",
  "strict": true,
  "parameters": {
    "type": "object",
    "properties": {
      "diagram_id": {
        "type": "string",
        "description": "Định danh sơ đồ cần sắp xếp hoặc 'ALL' cho toàn bộ canvas."
      },
      "horizontal_spacing": {
        "type": "number",
        "description": "Khoảng cách giữa các cột phân tầng (khuyến nghị 260px)."
      },
      "vertical_spacing": {
        "type": "number",
        "description": "Khoảng cách giữa các hàng song song (khuyến nghị 120px)."
      }
    },
    "required": ["diagram_id", "horizontal_spacing", "vertical_spacing"],
    "additionalProperties": false
  }
}
```

---

### 3.9 `diagnose_logic_bugs`
Dùng để kích hoạt bộ kiểm tra tĩnh toàn diện của SimplePLC (Graph Grammar, Write Conflict, Cyclic Dependency) nhằm phát hiện lỗi logic tiềm ẩn.

```json
{
  "name": "diagnose_logic_bugs",
  "description": "Kích hoạt bộ kiểm tra tĩnh toàn diện của SimplePLC (Graph Grammar, Xung đột ghi Write Conflict, và Chu trình lặp Cyclic Dependency) để phát hiện lỗi logic tiềm ẩn.",
  "strict": true,
  "parameters": {
    "type": "object",
    "properties": {
      "check_write_conflicts": {
        "type": "boolean",
        "description": "Kiểm tra xem có 2 khối Action cùng ghi vào một Tag đích trong cùng chu kỳ quét hay không."
      },
      "check_cyclic_loops": {
        "type": "boolean",
        "description": "Kiểm tra chu trình khép kín gây treo hoặc bất định chu kỳ quét."
      },
      "auto_propose_fixes": {
        "type": "boolean",
        "description": "Nếu phát hiện lỗi, tự động đề xuất phương án chỉnh sửa dây nối hoặc chèn cờ trung gian VFLAG."
      }
    },
    "required": ["check_write_conflicts", "check_cyclic_loops", "auto_propose_fixes"],
    "additionalProperties": false
  }
}
```

---

## 4. SYSTEM PROMPT TÍCH HỢP TRI THỨC AN TOÀN ĐIỀU KHIỂN CÔNG NGHIỆP

Dưới đây là nội dung System Instruction hoàn chỉnh, được tích hợp các nguyên lý kỹ thuật an toàn công nghiệp nặng để nạp vào LLM:

```markdown
Bạn là Industrial AI Copilot — Kỹ sư Tự động hóa Ảo cấp cao của SynaptiX IDE (SimplePLC Studio). Nhiệm vụ của bạn là tư vấn kỹ thuật, phát hiện lỗi logic và thực thi các thao tác can thiệp trực tiếp lên FBD Canvas thông qua cơ chế Function Calling.

BẠN PHẢI TUÂN THỦ TUYỆT ĐỐI CÁC QUY TẮC AN TOÀN ĐIỀU KHIỂN CÔNG NGHIỆP SAU ĐÂY:

1. NGUYÊN TẮC FAIL-SAFE (DE-ENERGIZE TO TRIP):
- Mọi hệ thống an toàn phải chuyển về trạng thái AN TOÀN khi mất nguồn hoặc đứt dây tín hiệu.
- Nút Dừng (Stop Pushbutton), Dừng Khẩn Cấp (E-Stop), Tiếp điểm Rơ-le nhiệt (Thermal Overload 95-96) và Công tắc hành trình giới hạn BẮT BUỘC sử dụng tiếp điểm Thường Đóng (NC - Normally Closed).
- Trong điều kiện bình thường, dòng điện 24V đi qua tiếp điểm NC vào PLC -> Tín hiệu ngõ vào là DI = 1 (Mức Cao).
- Khi người vận hành bấm dừng HOẶC dây tín hiệu bị đứt/chuột cắn -> DI = 0 (Mức Thấp).
- TUYỆT ĐỐI KHÔNG lập trình điều kiện dừng bằng sườn lên ON_RISE trên nút Stop NC! Điều kiện cho phép máy chạy là DI_Stop == 1. Khi DI_Stop == 0 hoặc sườn xuống ON_FALL, hệ thống PHẢI ngắt ngõ ra ngay lập tức.

2. KHÓA LIÊN ĐỘNG (INTERLOCKS) & THỜI GIAN TRỄ DẬP HỒ QUANG (DEAD-TIME):
- Đối với mạch đảo chiều động cơ (Thuận/Nghịch - Forward/Reverse), Van 2 ngả, hoặc Cầu trục Nâng/Hạ:
  + Phải có khóa liên động phần mềm: Lệnh kích mở Thuận DO_Fwd phải có Guard: DO_Rev == 0 VÀ Rev_Aux_Feedback == 0.
  + BẮT BUỘC chèn thời gian trễ dập hồ quang (Dead-time Delay) tối thiểu 100ms - 300ms (sử dụng Timer TON) giữa thời điểm ngắt cuộn Thuận và kích cuộn Nghịch để bảo vệ Contactor chống nổ ngắn mạch 3 pha.

3. PHÂN CẤP AN TOÀN NÚT DỪNG KHẨN CẤP (E-STOP HIERARCHY):
- Nút E-Stop cơ khí phải cắt nguồn động lực cấp 1 qua Rơ-le An toàn phần cứng chuyên dụng (Safety Relay). Tín hiệu vào PLC chỉ là tiếp điểm phụ giám sát.
- Khi người vận hành nhả nút E-Stop cơ khí: HỆ THỐNG TUYỆT ĐỐI KHÔNG ĐƯỢC TỰ ĐỘNG TÁI KHỞI ĐỘNG!
- Khởi động lại bắt buộc phải qua chu trình 4 bước: (1) Khảo sát hiện trường, (2) E-Stop đóng lại, (3) Kỹ sư ấn nút [Nút Reset An Toàn / Safety Master Reset] (ON_RISE), (4) Ấn nút Start.

4. MẠCH TỰ GIỮ CÔNG NGHIỆP (DOMINANT-RESET SELF-HOLDING):
- Mạch tự giữ động cơ phải tuân thủ nghiêm ngặt nguyên tắc ƯU TIÊN RESET (Dominant-Reset / RS Flip-Flop).
- Nếu người vận hành ấn đồng thời cả nút Start và Stop trong cùng chu kỳ quét -> Ngõ ra BẮT BUỘC PHẢI DỪNG (Q = 0).
- Biến tự giữ phải lưu trên RAM thông thường (không retain). Khi có điện trở lại sau cúp nguồn, máy không được tự chạy lại.

5. ĐỊNH THỜI TUẦN TỰ (IEC 61131-3 TIMERS):
- TON (On-Delay): Trễ bật. Ngõ vào IN phải duy trì liên tục đủ thời gian PT thì ngõ ra Q mới lên 1. Nếu IN ngắt giữa chừng, timer lập tức reset về 0.
- TOF (Off-Delay): Trễ ngắt. Khi IN lên 1 thì Q lên 1 ngay. Khi IN xuống 0, Q duy trì bật thêm thời gian PT rồi mới ngắt. Thường dùng cho quạt làm mát xả nhiệt dư sau khi dừng máy nén.
- TP (Pulse Timer): Định thời phát xung. Sườn lên của IN tạo xung ngõ ra Q có độ rộng chuẩn đúng bằng PT, bất chấp IN nhả sớm hay giữ lâu.

6. LỌC NHIỄU CƠ KHÍ & VÙNG CHẾT ANALOG (DEBOUNCING & DEADBAND):
- Tiếp điểm cơ khí (nút nhấn, phao cơ) có hiện tượng rung dội (Chatter). Mọi khối Trigger bắt sườn tín hiệu nút nhấn vật lý phải cấu hình thời gian lọc nhiễu debounce_ms = 30..50ms.
- Tín hiệu Analog luôn có nhiễu dao động điện từ. Khi so sánh ngưỡng nhiệt độ/áp suất để đóng cắt cơ cấu chấp hành, BẮT BUỘC sử dụng vi mạch trễ Schmitt Trigger (Deadband / Hysteresis) tối thiểu 2-5% dải đo để chống hiện tượng đóng cắt liên tục (hunting) gây cháy cuộn hút Contactor.

7. QUY TẮC NGỮ PHÁP ĐỒ THỊ ECA (GRAPH GRAMMAR V1):
- Tuyệt đối tuân thủ mô hình ECA: Input -> Trigger -> [Guard] -> Action.
- CẤM nối 2 khối Guard liên tiếp (Lỗi SPLC-GRAPH-007). Khi cần kết hợp nhiều điều kiện AND, hãy sử dụng cờ nhớ trung gian VFLAG.
- CẤM tạo 2 khối Action cùng ghi vào 1 ngõ ra DO (Lỗi Write Conflict SPLC-COMP-DEP-002), trừ trường hợp các khối này là thành phần của cùng một cụm Macro hợp lệ.
```

---

## 5. KỊCH BẢN THỰC THI MẪU ĐẦU-CUỐI (CONCRETE END-TO-END WALKTHROUGH)

Kịch bản này minh họa toàn bộ hành trình xử lý từ ngôn ngữ tự nhiên đến nạp luật nhị phân an toàn.

### 5.1 Yêu Cầu Kỹ Thuật Từ Người Dùng
Kỹ sư nhập lệnh vào khung chat:
> *"Tạo mạch tự giữ bơm DO0 có trễ ngắt 5 giây và khóa chéo nút dừng khẩn cấp DI2"*

---

### 5.2 Ngữ Cảnh Dự Án Được Tổng Hợp (Aggregated Context)
Dịch vụ `AiContextAggregator` tự động đóng gói trạng thái gửi lên API:

```json
{
  "project_name": "Tram_Bom_Cap_Nuoc",
  "hardware_profile": {
    "target": "STM32F401RE",
    "port": "COM5",
    "baud": 115200,
    "status": "ENGINE_RUNNING",
    "scan_time_ms": 12
  },
  "tag_catalog": [
    { "name": "DI0", "kind": "DigitalInput", "type": "BOOL", "alias": "Nút Start", "live_val": 0 },
    { "name": "DI1", "kind": "DigitalInput", "type": "BOOL", "alias": "Nút Stop NC", "live_val": 1 },
    { "name": "DI2", "kind": "DigitalInput", "type": "BOOL", "alias": "Nút E-Stop NC", "live_val": 1 },
    { "name": "DO0", "kind": "DigitalOutput", "type": "BOOL", "alias": "Động Cơ Bơm", "live_val": 0 },
    { "name": "VFLAG0", "kind": "VirtualFlag", "type": "BOOL", "alias": "Cờ Bơm Đang Chạy", "live_val": 0 }
  ],
  "canvas_state": {
    "active_nodes_count": 0,
    "selected_node_ids": []
  },
  "linter_diagnostics": []
}
```

---

### 5.3 Chuỗi Tool Calls Do LLM Sinh Ra
Mô hình AI phân tích:
- Nút Start `DI0` (NO) kích hoạt sườn lên.
- Nút Stop `DI1` (NC) và E-Stop `DI2` (NC) là các điều kiện cho phép (Guard `DI1 == 1` và Guard `DI2 == 1`).
- Mạch tự giữ sử dụng cờ trung gian `VFLAG0` (Aux Run Flag).
- Trễ ngắt 5 giây (5000ms) yêu cầu khối Macro **TOF** nối từ `VFLAG0` sang cuộn hút `DO0`.
- Tọa độ được tính toán phân tầng theo bước lưới (X cách nhau 240px).

LLM phát ra chuỗi Tool Calls tuần tự:

```json
[
  {
    "name": "add_node",
    "arguments": {
      "node_id": "node_in_start",
      "node_type": "INPUT",
      "custom_label": "Nút Start",
      "position_x": 60,
      "position_y": 140,
      "tag_name": "DI0",
      "trigger_type": "NONE",
      "compare_op": "NONE",
      "threshold_lo": 0,
      "threshold_hi": 0,
      "debounce_ms": 30,
      "action_type": "NONE",
      "action_param": 0
    }
  },
  {
    "name": "add_node",
    "arguments": {
      "node_id": "node_trg_start",
      "node_type": "TRIGGER",
      "custom_label": "Bắt Sườn Lên Start",
      "position_x": 300,
      "position_y": 140,
      "tag_name": "NONE",
      "trigger_type": "ON_RISE",
      "compare_op": "NONE",
      "threshold_lo": 0,
      "threshold_hi": 0,
      "debounce_ms": 30,
      "action_type": "NONE",
      "action_param": 0
    }
  },
  {
    "name": "add_node",
    "arguments": {
      "node_id": "node_grd_estop",
      "node_type": "GUARD",
      "custom_label": "Khóa An Toàn E-Stop NC",
      "position_x": 540,
      "position_y": 140,
      "tag_name": "DI2",
      "trigger_type": "NONE",
      "compare_op": "EQ",
      "threshold_lo": 1,
      "threshold_hi": 0,
      "debounce_ms": 0,
      "action_type": "NONE",
      "action_param": 0
    }
  },
  {
    "name": "add_node",
    "arguments": {
      "node_id": "node_act_latch",
      "node_type": "ACTION",
      "custom_label": "Bật Cờ Tự Giữ Bơm",
      "position_x": 780,
      "position_y": 140,
      "tag_name": "VFLAG0",
      "trigger_type": "NONE",
      "compare_op": "NONE",
      "threshold_lo": 0,
      "threshold_hi": 0,
      "debounce_ms": 0,
      "action_type": "SET_TAG",
      "action_param": 1
    }
  },
  {
    "name": "add_node",
    "arguments": {
      "node_id": "node_in_vflag0",
      "node_type": "INPUT",
      "custom_label": "Cờ Tự Giữ Bơm",
      "position_x": 60,
      "position_y": 380,
      "tag_name": "VFLAG0",
      "trigger_type": "NONE",
      "compare_op": "NONE",
      "threshold_lo": 0,
      "threshold_hi": 0,
      "debounce_ms": 0,
      "action_type": "NONE",
      "action_param": 0
    }
  },
  {
    "name": "add_node",
    "arguments": {
      "node_id": "tm_pump_tof",
      "node_type": "TIMER",
      "custom_label": "Trễ Ngắt Bơm 5s",
      "position_x": 300,
      "position_y": 380,
      "tag_name": "NONE",
      "trigger_type": "NONE",
      "compare_op": "NONE",
      "threshold_lo": 0,
      "threshold_hi": 0,
      "debounce_ms": 0,
      "action_type": "NONE",
      "action_param": 0
    }
  },
  {
    "name": "configure_timer_counter",
    "arguments": {
      "node_id": "tm_pump_tof",
      "mode": "TOF",
      "preset_value": 5000,
      "cv_tag_name": "NONE",
      "reset_tag_name": "NONE",
      "output_tag_name": "DO0"
    }
  },
  {
    "name": "connect_wires",
    "arguments": {
      "connection_id": "wire_1",
      "source_node_id": "node_in_start",
      "source_connector_title": "Out",
      "target_node_id": "node_trg_start",
      "target_connector_title": "In"
    }
  },
  {
    "name": "connect_wires",
    "arguments": {
      "connection_id": "wire_2",
      "source_node_id": "node_trg_start",
      "source_connector_title": "Out",
      "target_node_id": "node_grd_estop",
      "target_connector_title": "In"
    }
  },
  {
    "name": "connect_wires",
    "arguments": {
      "connection_id": "wire_3",
      "source_node_id": "node_grd_estop",
      "source_connector_title": "Out",
      "target_node_id": "node_act_latch",
      "target_connector_title": "In"
    }
  },
  {
    "name": "connect_wires",
    "arguments": {
      "connection_id": "wire_4",
      "source_node_id": "node_in_vflag0",
      "source_connector_title": "Out",
      "target_node_id": "tm_pump_tof",
      "target_connector_title": "IN"
    }
  }
]
```

---

### 5.4 Nhật Ký Thực Thi Tại Safety & Logic Validator Gate
Hệ thống chuyển giao danh sách thao tác tới `AiSafetyGate.cs` để thẩm định trước:

```
[SAFETY GATE EXECUTION LOG]
2026-10-07 10:15:02.114 [INFO] Nhận 10 thao tác Delta Graph từ AI Copilot.
2026-10-07 10:15:02.115 [CHECK-1] Kiểm tra cú pháp GraphGrammarV1:
                                  - node_in_start -> node_trg_start: Input to Trigger [PASSED]
                                  - node_trg_start -> node_grd_estop: Trigger to Guard [PASSED]
                                  - node_grd_estop -> node_act_latch: Guard to Action [PASSED]
                                  - node_in_vflag0 -> tm_pump_tof: Input to Timer Macro [PASSED]
                                  - SPLC-GRAPH-007 (Chained Guards): Không có guard nối tiếp guard [PASSED]
                                  - SPLC-GRAPH-010 (Single Inbound Port): Tất cả cổng nhận đúng 1 dây [PASSED]
2026-10-07 10:15:02.118 [CHECK-2] Kiểm tra xung đột ghi WriteConflictValidator:
                                  - VFLAG0 được ghi bởi duy nhất 1 node_act_latch (SET_TAG 1) [PASSED]
                                  - DO0 được điều khiển độc quyền bởi Macro Instance tm_pump_tof [PASSED]
                                  - Không có xung đột đa nguồn ghi (Coil Conflict = 0) [PASSED]
2026-10-07 10:15:02.120 [CHECK-3] Phân tích chu trình phụ thuộc RuleDependencyGraph:
                                  - Thuật toán Kahn duyệt 4 node: In-degree = 0, đồ thị acyclic [PASSED]
                                  - Mã lỗi SPLC-COMP-DEP-001: 0 chu trình phát hiện.
2026-10-07 10:15:02.122 [RESULT]  THẨM ĐỊNH AN TOÀN THÀNH CÔNG 100%. Cho phép nạp Ghost Preview!
```

---

### 5.5 Hiển Thị Ghost Diff Trên Canvas FBD
1. `LogicEditorViewModel` tiếp nhận danh sách khối hợp lệ.
2. Thiết lập cờ `IsGhost = true`, `DiffStatus = DiffState.Added` cho 5 khối và 4 dây nối mới.
3. Trên Nodify Canvas:
   - Các khối `node_in_start`, `node_trg_start`, `node_grd_estop`, `node_act_latch`, và `tm_pump_tof` hiện lên với viền nét đứt màu xanh ngọc `#00A389` và độ mờ trong suốt `Opacity = 0.85`.
   - Các đường dây nối phát sáng với hiệu ứng nét đứt chỉ báo chiều tín hiệu.
   - Sơ đồ ban đầu của người dùng được giữ nguyên vẹn $100\%$.
4. Thanh điều khiển nổi xuất hiện trên đỉnh màn hình:
   > *"🤖 AI Copilot đề xuất: Mạch Bơm tự giữ trễ ngắt 5s với E-Stop (+5 khối, +4 dây) | ✓ An toàn: 0 xung đột"*
   > `[✓ Chấp nhận (Accept)]` `[✕ Hủy bỏ (Reject)]`

---

### 5.6 Con Người Xác Nhận & Biên Dịch Ra Bản Ghi Nhị Phân 32-Byte Chuẩn SPLC (V1.7)
1. Kỹ sư quan sát thấy sơ đồ trực quan đúng ý đồ công nghệ.
2. Kỹ sư click nút `[✓ Chấp nhận (Accept)]`.
3. Hệ thống:
   - Lưu 1 bước Undo vào `GraphHistoryService` (`RecordSnapshot`).
   - Xóa bỏ cờ `IsGhost = false`, `DiffStatus = DiffState.None`.
   - Đóng thanh Action Bar.
   - Kích hoạt `RuleCompiler.Compile()` sinh ra các luật thực thi và đóng gói nhị phân 32-byte Big-Endian (16 Modbus Holding Registers, chuẩn Data Contract V1.7 theo `RuleBinaryEncoder.EncodeProgramV17`):

```
BẢNG LUẬT ĐÃ BIÊN DỊCH TỪ ĐỒ THỊ AI:
┌────────┬─────────────┬──────────┬───────────┬──────────────┬────────────┬────────────────────────┐
│ RuleId │ Trigger     │ Event    │ Guard     │ Condition    │ Action     │ Description            │
├────────┼─────────────┼──────────┼───────────┼──────────────┼────────────┼────────────────────────┤
│ 0x0001 │ DI0 (Start) │ ON_RISE  │ DI2 (ES)  │ EQ 1 (Safe)  │ VFLAG0 = 1 │ Tự giữ bơm             │
│ 0x0002 │ DI2 (E-Stop)│ ON_FALL  │ NONE      │ NONE         │ VFLAG0 = 0 │ Trip E-Stop Dominant   │
│ 0x0003 │ VFLAG0      │ ON_RISE  │ NONE      │ NONE         │ DO0 = 1    │ TOF Bật tức            │
│ 0x0004 │ VFLAG0      │ ON_FALL  │ NONE      │ Dwell 5000ms │ DO0 = 0    │ TOF Trễ ngắt 5s        │
└────────┴─────────────┴──────────┴───────────┴──────────────┴────────────┴────────────────────────┘
```

Dự án đã sẵn sàng để người dùng nhấn **Deploy** nạp xuống vi điều khiển qua cổng COM với quy trình an toàn 4 bước.

---

## 6. KẾT LUẬN & ĐỊNH HƯỚNG TRIỂN KHAI MÃ NGUỒN

Bản đặc tả kỹ thuật này thiết lập một bước nhảy vọt toàn diện cho hệ thống SimplePLC Studio:
1. **Chấm dứt hoàn toàn sự giòn gãy**: Loại bỏ parser Regex và cơ chế xóa canvas mang tính phá hủy.
2. **Nâng tầm trải nghiệm người dùng**: Chuyển sang mô hình tương tác văn minh với **Ghost Diff Preview** và thanh kiểm duyệt **Human-in-the-Loop**, cho phép Undo/Redo tức thì.
3. **Bảo vệ tuyệt đối an toàn nhà máy**: Tích hợp các quy tắc an toàn công nghiệp chuẩn mực (Fail-Safe NC, Interlock Dead-time, E-Stop hierarchy, Dominant-Reset) trực tiếp vào System Prompt và Cổng Kiểm Duyệt Pre-Commit Gate.

Đội ngũ kỹ thuật phát triển sẽ căn cứ vào bản đặc tả này để triển khai mã nguồn các lớp `AiContextAggregator.cs`, `AiSafetyGate.cs`, `CanvasGhostRenderer.cs`, và cập nhật `GeminiApiClient.cs` theo lộ trình Giai đoạn 2 đã được phê duyệt.

---
**Tài liệu liên quan**:
- `SPLC-ENG-REV-2026-IDE-001`: Đánh giá chuyên sâu kiến trúc & tính năng IDE công nghiệp SimplePLC Studio.
- `SPLC-ARCH-CLEAN-001`: Kiến trúc phân tầng và ranh giới phụ thuộc SimplePLC Clean Architecture.
