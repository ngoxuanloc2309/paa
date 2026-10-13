# Project: Senior Industrial AI Copilot for SimplePLC Studio

## Architecture
SimplePLC Studio integrates an AI Copilot into its WPF/MVVM industrial automation environment. The architecture follows a 4-Stage Pipeline:
1. **Stage 1: Intent & Semantic Context Aggregation**:
   - `AiIntentClassifier`: Distinguishes `ConsultativeQnA`, `FbdSynthesis`, and `ProjectDiagnose`.
   - `AiContextAggregator`: Prioritized tag filtering, active canvas topology, and compiler diagnostics.
   - `AiPromptBuilder`: Natural, empathetic Senior Industrial Automation specialist persona (IEC 60204-1, fail-safe, interlocks).
2. **Stage 2: Dual-Mode Reasoning & Action Execution**:
   - For `ConsultativeQnA`: Deep technical explanations in rich Markdown without canvas pollution or ghost nodes.
   - For `FbdSynthesis`: Strict Tool Calling (9 schemas) or structured JSON rules, synthesizing DraftGraphTransactions.
   - `AiExplanationSynthesizer`: Clear 3-part rationale with full IEC Macro support (TON, TOF, TP, CTU, CTD, SCALE).
3. **Stage 3: Pre-Execution Safety Gate & Macro Binding Integrity**:
   - `AiSafetyGate`: Validates Graph Grammar V1, write conflicts, read-only tag invariants, and macro bindings.
   - `DraftGraphTransaction`: Full 9-tool execution engine including `configure_scale`, `rename_tag`, `disconnect_wire`.
4. **Stage 4: Ghost Diff Preview & Human Affirmation Gate**:
   - Canvas Ghost rendering (Slate Gray `#475569`, dashed wires, non-interactive connectors).
   - Floating Action Bar & Chat Action Cards: Accept (atomic commit, undo snapshot `Ctrl+Z`, 32-byte rule compile) or Reject (clean revert).
   - `MarkdownTextBlock` & State-Aware Contextual Suggestions in `AiChatView.xaml`.

## Feature Inventory
| # | Feature | Description | Milestone | Source |
|---|---------|-------------|-----------|--------|
| 1 | Natural & Empathetic Tone | Flexible, empathetic explanations like Gemini/Claude; no rigid text overwrite; explain industrial standards (IEC 60204-1, fail-safe) | M1 | Survey 1, ORIGINAL_REQUEST §1 |
| 2 | Intent Classification | Classify user intent into ConsultativeQnA, FbdSynthesis, ProjectDiagnose; prevent rogue regex rule extraction on Q&A | M1 | Survey 1, Survey 2, ORIGINAL_REQUEST §2 |
| 3 | Multi-Turn Memory Optimization | Sliding window context, preserve tool history, prioritized tag injection (reducing 12KB payload by 80%) | M1 | Survey 1, ORIGINAL_REQUEST §2 |
| 4 | Full 9-Tool Execution Engine | Implement missing tools in DraftGraphTransaction (configure_scale, rename_tag, disconnect_wire, auto_layout, diagnose) | M2 | Survey 1, Survey 2, ORIGINAL_REQUEST §3 |
| 5 | IEC Macro Parameter Binding | Fix Scale properties in DraftNode and Timer/Counter InputTagName mapping in ApplyAiProposalToCanvas | M2 | Survey 2, ORIGINAL_REQUEST §3 |
| 6 | Macro Rule Synthesis | Update AiExplanationSynthesizer to generate valid rule sets for TON (2 rules), TOF (2 rules), TP (2 rules), CTU/CTD (3-4 rules), SCALE | M2 | Survey 1, Survey 2, ORIGINAL_REQUEST §3 |
| 7 | Ghost Connector Hit-Test Isolation | Set IsHitTestVisible=False on ghost connectors to prevent accidental wiring | M2 | Survey 2, ORIGINAL_REQUEST §3 |
| 8 | Non-Destructive Graph Reconstruction | Make ReconstructGraphFromRule non-destructive or safely isolated from user canvas | M2 | Survey 2, ORIGINAL_REQUEST §3 |
| 9 | Native WPF Markdown Formatter | Render Headings (###), Bold (**), Inline Code (`), Tag Badges (DI/DO/AI), Bullet Lists, and Code Blocks in Industrial Slate Gray | M3 | Survey 3, ORIGINAL_REQUEST §4 |
| 10 | Multi-Line Input Box Polish | Watermark placeholder, Top vertical alignment to prevent jitter, focus retention on mouse send | M3 | Survey 3, ORIGINAL_REQUEST §4 |
| 11 | Dynamic Contextual Suggestions | State-aware suggestion chips based on canvas state, selected node, linter diagnostics, and simulation mode | M3 | Survey 3, ORIGINAL_REQUEST §4 |
| 12 | Active Context Status Pills | Real-time indicators showing active canvas nodes, compiler validity, tag counts in chat header | M3 | Survey 3, ORIGINAL_REQUEST §4 |
| 13 | Industrial Color Token Adherence | Eliminate hardcoded color hex values in AiChatView.xaml; adhere to Colors.xaml tokens | M3 | Survey 3, ORIGINAL_REQUEST §4 |
| 14 | Zero Warning, Zero Regression Verification | Maintain 0 Warning, 0 Error on SimplePLC.sln and pass 100% 748 existing tests + new unit tests | M4 | Survey 3, ORIGINAL_REQUEST §5 |

## Milestones
| # | Name | Scope | Dependencies | Status |
|---|------|-------|-------------|--------|
| M1 | Core AI Domain, Intent & Memory | AiIntentClassifier, AiPromptBuilder, AiChatViewModel intent routing, multi-turn memory optimization | none | DONE |
| M2 | IEC Macros, 4-Stage Pipeline & Ghost Engine | DraftGraphTransaction tools, DraftNode Scale props, Timer input mapping, Macro rule synthesis, ghost hit-testing | M1 | DONE |
| M3 | UI/UX AiChatView Polish | Markdown renderer, multiline watermark & alignment, dynamic contextual suggestion chips, color tokens | M1, M2 | DONE |
| M4 | Regression Verification & Audit | Full solution build (0W, 0E), 748+ unit tests pass, challenger verification, forensic integrity audit | M1, M2, M3 | PLANNED |

## Interface Contracts
### Intent Classifier ↔ AiChatViewModel
- `AiUserIntent AiIntentClassifier.Classify(string prompt, bool hasCanvasElements, bool hasLinterErrors)`
- Returns: `AiUserIntent.ConsultativeQnA`, `AiUserIntent.FbdSynthesis`, `AiUserIntent.ProjectDiagnose`.

### AiPromptBuilder ↔ GeminiApiClient
- `string BuildSystemInstruction(AiUserIntent intent = AiUserIntent.FbdSynthesis)`
- Generates natural, empathetic tone for Q&A without forced JSON blocks, and strict tool/schema instructions for synthesis.

### DraftGraphTransaction ↔ LogicEditorViewModel
- `DraftNode`: includes `Gain`, `Offset`, `Unit`, `DecimalPlaces`, `IsClamped`, `ClampMin`, `ClampMax`, `InputTagName`.
- `ApplyToolCall`: handles `configure_scale`, `rename_tag`, `disconnect_wire`, `configure_timer_counter`.
- `ApplyAiProposalToCanvas`: maps `InputTagName` correctly to `TimerNodeViewModel` / `CounterNodeViewModel`.

### Markdown Formatter ↔ AiChatView
- Native WPF Inline/Block presenter `MarkdownTextBlock` (or `MarkdownInlineHelper.MarkdownTextProperty`) bound to `DisplayContent`.

## Code Layout
- `src/SimplePLC.Domain/`: Domain entities and rule grammar.
- `src/SimplePLC.Application/`: Compiler and runtime execution.
- `SimplePLC.Studio/Services/Ai/`:
  - `AiIntentClassifier.cs` (New)
  - `AiPromptBuilder.cs`
  - `AiContextAggregator.cs`
  - `AiExplanationSynthesizer.cs`
  - `AiSafetyGate.cs`
  - `DraftGraphTransaction.cs`
  - `AiToolDeclarations.cs`
- `SimplePLC.Studio/Controls/` or `Converters/`:
  - `MarkdownTextBlock.cs` (New)
- `SimplePLC.Studio/ViewModels/`:
  - `AiChatViewModel.cs`
  - `LogicEditorViewModel.cs`
- `SimplePLC.Studio/Views/`:
  - `AiChatView.xaml`, `AiChatView.xaml.cs`
- `SimplePLC.Studio/Models/`:
  - `AiSuggestionChip.cs` (New)
  - `AiRuleSpecModel.cs`
  - `ChatMessageModel.cs`
- `SimplePLC.Studio.Tests/`:
  - `AiCopilotTests.cs`
  - `AiIntentAndMemoryTests.cs` (New)
