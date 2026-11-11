# Antigravity Agent Guidelines for SimplePLC & SynaptiX IDE

This repository contains **SynaptiX IDE** (WPF / .NET 8 desktop industrial IDE), PLC runtime engine, and firmware communication stacks.
All AI agents modifying code in this repository MUST adhere strictly to the following engineering and UI/UX doctrines.

---

## 🧭 0. The First Principles Engineering Doctrine (Mandatory for ALL Actions)

Every engineering decision, architecture design, UI/UX control, and unit test in this repository MUST be derived from fundamental physical and technical axioms rather than reasoning by analogy or superficial patchwork:

### 0.1. Axiom 1 — Hardware Ground Truth (IEC 61131-3)
- Microcontrollers (STM32/MCU) and PLCs operate strictly on physical memory structures:
  - **Bit / Coil:** 1-bit boolean ($0$ or $1$).
  - **Integer Registers (INT16 / INT32):** Signed 2's complement integers. **A fractional decimal value like `25.5` does NOT physically exist in an integer register.**
  - **Real Registers (REAL / Float32):** 32-bit IEEE 754 floating point numbers.
- **Doctrine:** Data types are NOT merely C# cosmetic labels; they are **physical memory contracts**. Never allow malformed, fractional, or type-violating values to penetrate the IDE presentation layer into the runtime domain or hardware registers.

### 0.2. Axiom 2 — The Visual Truth (Single Source of Truth)
- An industrial IDE / HMI is a **faithful 1:1 reflection of the machine's internal memory state**.
- **STRICT PROHIBITION:** The interface must NEVER lie to the operator. If a TextBox displays `"25.5"` while the underlying system stores integer `25`, that is a critical industrial defect. Discrepancies between what an operator sees and what the PLC executes cause industrial accidents, equipment damage, and plant downtime.

### 0.3. Axiom 3 — Input State Machine (Drafting vs. Committed)
- A text input field is a finite state machine (FSM) with two distinct phases:
  - **Phase A — Drafting State (In-flight typing):** The operator is actively editing. Transient incomplete characters (`"-"`, `"25."`) may exist temporarily.
  - **Phase B — Committed State (LostFocus / Enter):** The resolution event MUST deterministically resolve the input:
    - *Valid input:* **Auto-Normalize** (`"100.0"` $\rightarrow$ `"100"` for INT; `"25"` $\rightarrow$ `"25.0"` for REAL).
    - *Invalid / Type-mismatched input:* **Auto-Revert** to the last valid committed state with visual feedback (tooltip/highlight). **Never leave unparsed or invalid drafting text lingering in the committed UI.**

### 0.4. Axiom 4 — End-to-End Invariant & Anti-"Test Myopia"
- A feature is NEVER complete simply because an isolated utility function (`SmartInputParser.TryParse`) passes a unit test.
- A feature is only complete when the entire chain of custody:
  $$\text{User Keystroke} \longrightarrow \text{WPF Control} \longrightarrow \text{Binding Engine} \longrightarrow \text{ViewModel} \longrightarrow \text{Domain Model} \longrightarrow \text{Firmware Bytecode}$$
  is thoroughly verified and proven consistent under both nominal and adversarial inputs.

---

## 🎨 1. Frontend & UI/UX Doctrine (Mandatory for ALL XAML Changes)

Whenever creating, modifying, styling, or refactoring ANY XAML view, dialog, template, or style in `SimplePLC.Studio/`:

### 1.1. Zero Hardcoded Hex Token Doctrine
- **STRICT PROHIBITION:** NEVER write raw hex colors (e.g., `#FFFFFF`, `#1E1E1E`, `#B91C1C`, `#107C41`) or named colors (e.g., `Red`, `Black`, `White`) directly in XAML.
- **ALWAYS** reference semantic tokens defined in [`SimplePLC.Studio/Styles/Colors.xaml`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Styles/Colors.xaml) via `{DynamicResource ...}`:
  - Surfaces: `{DynamicResource BgAppBrush}`, `{DynamicResource BgPanelBrush}`, `{DynamicResource BgSubtleBrush}`, `{DynamicResource IndustrialSurfaceInsetBrush}`
  - Borders: `{DynamicResource IndustrialBorderDefaultBrush}`, `{DynamicResource BorderLightBrush}`
  - Typography: `{DynamicResource TextPrimaryBrush}`, `{DynamicResource TextSecondaryBrush}`, `{DynamicResource TextMutedBrush}`
  - Semantic States: `{DynamicResource SuccessBrush}`, `{DynamicResource DangerBrush}`, `{DynamicResource WarningBrush}`, `{DynamicResource PrimaryBrush}`

### 1.2. ANSI/ISA-101 "The Gray Principle" (90/10 Rule)
- 90% of screens must remain neutral slate / grayscale to prevent operator eye fatigue during 8–12 hour factory shifts.
- Bright saturated colors are **strictly reserved** for real-time operational states:
  - Normal / Run OK $\rightarrow$ `{DynamicResource SuccessBrush}` (`#107C41`)
  - Fault / Blocked / Danger $\rightarrow$ `{DynamicResource DangerBrush}` (`#B91C1C`)
  - Warning / Wait / Force $\rightarrow$ `{DynamicResource WarningBrush}` (`#C05621`)
  - Primary / Selection $\rightarrow$ `{DynamicResource PrimaryBrush}` (`#006487`)
- Never use bright colors for decorative headers or random card backgrounds.

### 1.3. Microsoft Fluent 2 Desktop Geometry
- **Interactive Controls** (Buttons, TextBoxes, ComboBoxes, CheckBoxes): `CornerRadius="4"`
- **Container Cards, GroupBoxes, Flyout Panels**: `CornerRadius="6"`
- **Main Dialogs & Modal Windows**: `CornerRadius="8"`
- **Tactile Toolbar Buttons**: Use `Style="{StaticResource TactileToolbarBtnStyle}"` or `Style="{StaticResource TactileToolbarCheckBoxStyle}"` with `Padding="7,3"` and `CornerRadius="4"`.
- **Never** use round web-pill shapes ($16\text{px}-24\text{px}$) in desktop dialogs.

### 1.4. IBM Carbon Data Table Density
- Left-align text labels, identifiers, tag names.
- Right-align numeric values, elapsed times, addresses, frequencies with monospace font (`Consolas` / `Cascadia Code`).
- Center-align booleans, checkboxes, and status badges.
- Keep table rows compact ($28\text{px}-32\text{px}$).

### 1.5. Crisp Vector Icons (No Text Emojis)
- NEVER use raw Unicode emojis (e.g., `⏸`, `▶`, `↺`) for interactive buttons. Windows renders emoji characters with colored boxes that break industrial aesthetics.
- ALWAYS use vector SVG `<StreamGeometry>` from [`SimplePLC.Studio/Styles/Icons.xaml`](file:///g:/HoaNV/Projects/SimplePLC/SimplePLC.Studio/Styles/Icons.xaml) via `<Path Style="{StaticResource IecStatusIconStyle}" Data="{StaticResource IconSys...}" />`.

### 1.6. Industrial Form Input & Bad-Data Defense Doctrine
- **Visual Truth Guarantee:** Every bound TextBox must guarantee that what is displayed on screen matches the committed value in memory.
- **LostFocus & Enter Lifecycle:** All numeric input fields must implement a committed resolution lifecycle:
  - **Auto-Revert:** If an operator types an invalid or type-mismatched string (e.g. typing `"25.5"` or `"abc"` into an integer input), upon `LostFocus` or `Enter`, the field MUST revert to the last valid committed integer value.
  - **Auto-Normalize:** Valid but unformatted inputs MUST automatically reformat upon `LostFocus`:
    - INT32: `"100.0"` or `"100,0"` $\rightarrow$ `"100"`.
    - REAL: `"25"` or `"25,0"` $\rightarrow$ `"25.0"`.
    - BOOL: `"true"`, `"on"`, `"bật"` $\rightarrow$ `"1"`; `"false"`, `"off"`, `"tắt"` $\rightarrow$ `"0"`.
- **Type-Safe Control Selection for Booleans:** For all digital tags (BOOL), the UI must provide binary toggle buttons `[ 0 ]` `[ 1 ]` or industrial toggle switches rather than unconstrained free-text fields.

---

## 🏛️ 2. Backend & Core Architecture Doctrine (Mandatory for ALL C# Changes)

Whenever creating, modifying, refactoring, or reviewing C# code in `src/` or `SimplePLC.Studio/Services/`:

### 2.1. Clean Architecture Layer Dependencies
- Dependencies may **only point inward**: `Domain` $\leftarrow$ `Application` $\leftarrow$ `Infrastructure` / `Studio`.
- **`SimplePLC.Domain`:** Zero dependencies. Pure C# business invariants, state machines, and mathematical definitions. Never reference OS, sockets, serial ports, or UI frameworks.
- **`SimplePLC.Application`:** References only `Domain`. Contains compilers, lowering logic, and abstract Ports (`ISerialTransport`, `IClock`). Never reference `Infrastructure` or `Studio`.

### 2.2. SOLID Principles & Deterministic Concurrency
- **Single Responsibility (SRP):** Split packet parsing, register polling, and state caching into dedicated classes.
- **Open/Closed (OCP):** Add new FBD blocks or communication protocols via Factory/Strategy patterns without modifying core compiler loops.
- **Liskov Substitution (LSP):** Software emulators (`McuEmulator`) must be 100% substitutable for physical hardware under test.
- **Interface Segregation (ISP):** Segregate client roles (e.g., `ITagReader` vs `ITagWriter`).

### 2.3. Event-Driven Reactive Streams (The Beckhoff ADS Model)
- **STRICT PROHIBITION:** NEVER write continuous polling loops with `Thread.Sleep()`.
- Use push-based in-process channels (`System.Threading.Channels`) or `IObservable<T>` for hardware telemetry.
- Downsample and debounce high-frequency streams ($10\text{ms}$) to 20 FPS ($50\text{ms}$) before marshaling to the UI thread to prevent UI freezing.

### 2.4. Zero-Allocation Hot Path & Monotonic Time
- In recurring scan cycles and packet parsing: use `ReadOnlySpan<byte>`, `Span<byte>`, and `ArrayPool<byte>.Shared`. Zero heap allocations.
- Never call `DateTime.Now` inside domain logic or rule evaluation. Always inject `ISimulatedClock` / `IClock` for deterministic testing.
- Never silently swallow exceptions with empty catch blocks (`catch { }`). Document teardown fallbacks explicitly with justification comments.

---

## 🧪 3. QA, TDD & Living Documentation Doctrine (Mandatory for ALL Changes)

Whenever creating, modifying, refactoring, or fixing ANY code in `src/` or `SimplePLC.Studio/`:

### 3.1. Strict TDD Cycle & Reproduction Test Rule
- **Bug Fixes:** NEVER apply a bugfix without first writing a **Reproduction Unit Test** reproducing the failure (Red). Fix the code to pass (Green), then optimize (Refactor).
- **New Features:** Define unit tests asserting expected behaviors before or concurrently with implementation.
- **Test Naming:** Must strictly follow `[UnitOfWork]_[StateUnderTest]_[ExpectedBehavior]`.

### 3.2. Hermetic Isolation & Zero Hardware Coupling
- **STRICT PROHIBITION:** Unit tests MUST NEVER connect to real physical COM ports, physical USB devices, or external networks.
- Always inject test doubles: `McuEmulator`, `FakeModbusClient`, and `System.TimeProvider` / `TestTimeProvider`. Zero `Thread.Sleep` or real time waiting in tests.

### 3.3. IEC 61508-3 Triad Testing Rule
Every service or algorithmic logic must be tested across three axes:
1. **Happy Path:** Nominal contract execution.
2. **Boundary & Overflow:** Min/max integers, empty arrays, sparse addresses, capacity limits.
3. **Fault Injection:** Unexpected cable pull / transport drop, CRC corruption, busy lease contention, flash write commit failure.

### 3.4. Living Documentation (Docs-as-Code)
- Every `public` class, interface, method, property, and record MUST have XML doc comments (`<summary>`, `<param>`, `<returns>`).
- Modbus wire contract changes in `SimplePLC.Protocol` MUST immediately update register mapping documentation.
- Every atomic change MUST update `walkthrough.md`.

### 3.5. ViewModel Integration Contract & Negative Testing Rule
- Testing static utility parsers in isolation is insufficient. All interactive input logic MUST have unit tests executed directly against the target ViewModel (`ActionNodeViewModel`, `ComparatorNodeViewModel`, `InputNodeViewModel`, `LiveWatchViewModel`).
- Every input property must be tested against three lifecycle cases:
  1. **Nominal Input:** Valid type entry commits correctly to both text and underlying value.
  2. **Tolerant Normalization:** Permissible variants (e.g. `"100.0"` for INT, `"25"` for REAL) normalize upon resolution.
  3. **Adversarial / Negative Input:** Invalid type entry (e.g. `"25.5"` or `"abc"` for INT) is rejected, does NOT corrupt the underlying model, and reverts the displayed text upon resolution.

### 3.6. Full Scope Traceability Matrix
- Before declaring ANY multi-node or cross-cutting feature complete, the agent MUST assemble an explicit Traceability Matrix covering 100% of the nodes and views defined in the specification scope.
- If even a single node in scope (e.g. `ComparatorNode` or `SimulatorView`) is left unverified, the task MUST NOT be closed.

---

## ⚡ 4. Mandatory Post-Edit Validation Commands

### 4.1. Frontend XAML Validation
After modifying ANY file ending in `.xaml` within `SimplePLC.Studio/`:
```powershell
powershell -ExecutionPolicy Bypass -File .agents/skills/synaptix-ui-ux/scripts/validate-xaml-tokens.ps1 -FilePath "SimplePLC.Studio/Path/To/ModifiedFile.xaml"
```
Must output `[PASS]` with **0 violations**.

### 4.2. Backend Architecture Validation
After modifying ANY C# file in `src/` or `SimplePLC.Studio/Services/`:
```powershell
powershell -ExecutionPolicy Bypass -File .agents/skills/synaptix-backend-arch/scripts/validate-backend-arch.ps1
```
Must output `[PASS]` with **0 architecture violations**.

### 4.3. QA, TDD & Living Documentation Validation
After modifying ANY C# file across the repository:
```powershell
powershell -ExecutionPolicy Bypass -File .agents/skills/synaptix-qa-tdd/scripts/validate-tdd-docs.ps1
```
Must output `[PASS]` with **0 violations** and **100% test pass rate**.

### 4.4. Technical Documentation & Web Wiki Validation
After modifying ANY documentation in `docs/` or `docs-portal/`:
```powershell
powershell -ExecutionPolicy Bypass -File .agents/skills/synaptix-docs-techwriter/scripts/validate-docs.ps1
```
Must output `[PASS]` with **0 violations** and clean VitePress build.

---

## 🛠️ 5. Build & Test Verification

Before proposing or finalizing any pull request or merge:
1. Ensure no locking instances of `SynaptixIDE.exe` are running:
   ```powershell
   Stop-Process -Name SynaptixIDE -Force -ErrorAction SilentlyContinue
   ```
2. Build solution:
   ```powershell
   dotnet build SimplePLC.sln
   ```
   (Must result in 0 errors and 0 warnings).
3. Run the complete unit test suite across all projects:
   ```powershell
   dotnet test SimplePLC.sln --no-build
   ```
   (Must pass 100% of tests, currently 1,614/1,614).

---

## 📚 6. Architecture & Design Skills Reference
- **Frontend UI/UX Skill:** [`g:\HoaNV\Projects\SimplePLC\.agents\skills\synaptix-ui-ux\SKILL.md`](file:///g:/HoaNV/Projects/SimplePLC/.agents/skills/synaptix-ui-ux/SKILL.md)
- **Backend Architecture Skill:** [`g:\HoaNV\Projects\SimplePLC\.agents\skills\synaptix-backend-arch\SKILL.md`](file:///g:/HoaNV/Projects/SimplePLC/.agents/skills/synaptix-backend-arch/SKILL.md)
- **QA, TDD & Living Docs Skill:** [`g:\HoaNV\Projects\SimplePLC\.agents\skills\synaptix-qa-tdd\SKILL.md`](file:///g:/HoaNV/Projects/SimplePLC/.agents/skills/synaptix-qa-tdd/SKILL.md)
- **Technical Documentation & Web Wiki Skill:** [`g:\HoaNV\Projects\SimplePLC\.agents\skills\synaptix-docs-techwriter\SKILL.md`](file:///g:/HoaNV/Projects/SimplePLC/.agents/skills/synaptix-docs-techwriter/SKILL.md)
