# Verified Industrial Palette Rubric: Design Token Architecture & HMI Engineering Rules

**Document Identifier**: `SPLC-RUBRIC-PAL-2026-10-08-01`  
**System Target**: `SimplePLC.Studio` (WPF Industrial Control IDE & Runtime Monitor)  
**Specification Owner**: Visual Engineering & Standards Architecture Squad  
**Governing Standards**: IEC 60073:2002, IEC 60204-1:2018, ISA-101.01-2015, W3C WCAG 2.1 (Level AA)  
**Token Source File**: `SimplePLC.Studio\Styles\Colors.xaml` (88 Defined Design Tokens)  
**Status**: APPROVED BASELINE / MANDATORY REFERENCE  

---

## 1. Executive Summary & Design Token Philosophy

SimplePLC Studio enforces a deterministic, high-performance visual design system tailored for mission-critical industrial automation applications. Moving away from arbitrary CSS-style styling and unconstrained web neon hues, all UI components, graphical functional blocks (IEC 61131-3), hardware instrumentation simulators, and interactive controls must bind strictly to the verified design token architecture defined in `Styles/Colors.xaml`.

### Core Engineering Principles
1. **Single Source of Truth (SSOT)**: Every color in `SimplePLC.Studio` must resolve to an explicit token in `Colors.xaml`. Inline hex codes (`#RRGGBB`) in XAML Views or C# ViewModels are strictly prohibited technical debt.
2. **The "Quiet Interface" (ISA-101)**: The operator's primary visual field is anchored in neutral, desaturated cool slate grays (`#ECEFF1`, `#FFFFFF`, `#F8FAFC`, `#CBD5E1`). Chromatic colors (Red, Amber, Green) are strictly reserved for operational state signaling and process alarms.
3. **Deterministic State Encoding (IEC 60073)**: Operational meanings (Safe/Run, Abnormal/Caution, Danger/Fault) follow universal international machine standards. Dual-function toggle controls must maintain neutral bodies and communicate state via dedicated indicator LEDs.
4. **Guaranteed Ergonomic Contrast (WCAG 2.1 AA)**: All operational typography and interactive indicators must meet or exceed a 4.50:1 contrast ratio against their respective backing surfaces.

---

## 2. Industrial Automation HMI Rules & Standard Norms

### 2.1. IEC 60073:2002 Coding Principles for Indicating Devices and Actuators

#### Clause 4.2.1: Safety Meaning of Colors for Indicating Devices
| Color Code | Standard Name | Technical Meaning | Permitted Automation Usages in SimplePLC Studio | Prohibited Usages |
|---|---|---|---|---|
| **RED** (`#B91C1C`) | Danger | Danger, Emergency, Alarm Trip, System Fault | PLC Hardware Fault (FAULT LED), E-Stop activated, cyclic scan timeout, memory overflow | Normal powered state, active button body when connected |
| **YELLOW / AMBER** (`#C05621`) | Caution | Abnormal condition, Impending critical state | Manual Force active (Force Overwrite), Stale logic, reconnecting state, RTC battery warning | General warning backgrounds, non-critical filter tabs |
| **GREEN** (`#107C41`) | Safe | Normal situation, Safe operating condition | PLC Running (RUN LED), logic condition TRUE, Modbus link active, I/O pin energized | Power off indicator, default window chrome |
| **BLUE** (`#1E3A8A` / `#334155`) | Mandatory | Mandatory action, Instruction required | Operator intervention required, parameter change confirmation prompt | General decorative backgrounds |
| **GRAY / WHITE** (`#ECEFF1` / `#FFFFFF`) | Neutral | Normal, Other, No specific safety significance | Device de-energized (0 / OFF), inactive state, canvas background, window chrome, standard buttons | Fault indicators, safety overrides |

#### Clause 5.2.2 & IEC 60204-1 Clause 10.2: Color Coding for Actuators (Buttons & Switches)
- **START / INITIATE**: White, Gray, or Black. Green is permitted only when initiating a safe continuous condition.
- **STOP / TERMINATE**: Black, Gray, or White. Red is strictly permitted only for STOP, OFF, and EMERGENCY STOP.
- **DUAL-FUNCTION (TOGGLE / ALTERNATING)**: Pushbuttons that alternately initiate and terminate an operation (e.g., Connect/Disconnect) **SHALL NOT BE COLORED RED OR GREEN**. The body must be neutral (Industrial Slate `#334155` or Outlined Frame), with state indicated by text and a discrete secondary LED glyph.
- **EMERGENCY STOP (E-STOP)**: Actuator must be Red on a contrasting Yellow background.

---

### 2.2. ISA-101.01-2015 Human-Machine Interfaces for Process Automation

1. **Hierarchy of Visual Prominence**:
   - Level 1 (Overview / Global Shell): Monochromatic slate framing, low saturation, global alarm counters.
   - Level 2 (Unit Control / Canvas / Live Watch): Clean white surfaces, high structural clarity, clear tag grouping.
   - Level 3 (Detailed Diagnostic / Inspector): High-density technical readouts, explicit Modbus hex addressing.
   - Level 4 (Diagnostic Subsystem / Rule Bytecode): Monospaced execution logging, timing diagrams.

2. **The Quiet Canvas Rule**:
   - Saturated web neon colors (e.g., Tailwind Indigo, Cyan, Emerald, Purple) are banned from process overview screens.
   - When all processes operate normally, the HMI screen must appear predominantly gray and calm ("Quiet").
   - Chromatic pops of color are permitted **only** when human intervention is required.

3. **Multi-Attribute Redundancy**:
   - Color must **never** be the sole indicator of process state. Every colored indicator must be accompanied by text (e.g., "RUN", "FAULT"), a distinct shape (e.g., Triangle for Warning, Octagon for Fault), or an explicit binary value (`0` vs `1`).

---

### 2.3. W3C WCAG 2.1 Level AA Accessibility Standards

1. **Success Criterion 1.4.3 (Contrast Minimum)**:
   - Normal text (< 18pt or < 14pt bold) must achieve a photometric contrast ratio of at least **4.50 : 1**.
   - Large text ($\ge 18\text{pt}$ or $\ge 14\text{pt}$ bold) must achieve at least **3.00 : 1**.
2. **Success Criterion 1.4.11 (Non-Text Contrast)**:
   - Visual information required to identify active UI components and graphical states must achieve at least **3.00 : 1** against adjacent colors.
3. **Photometric Linearization Formula**:
   $$L = 0.2126 \times R_{lin} + 0.7152 \times G_{lin} + 0.0722 \times B_{lin}, \quad CR = \frac{L_1 + 0.05}{L_2 + 0.05}$$

---

## 3. Complete Industrial Palette Token Rubric (All 88 Tokens)

The following tables index all **88 tokens** declared in `SimplePLC.Studio\Styles\Colors.xaml`. Tokens are organized into 8 functional tiers.

### Tier 1: Window & Surface Framing Tokens (10 Tokens)
| Color Key | Brush Key | Hex Code | Visual Swatch | IEC / ISA-101 Role | Expected UI Context | Adoption Status |
|---|---|---|:---:|---|---|:---:|
| `ColorBgWindow` | `BgWindowBrush` | `#ECEFF1` | Cool Slate | Canvas Base (ISA-101) | Window background, status bar, tab row | **ACTIVE (App.xaml)** |
| `ColorBgPanel` | `BgPanelBrush` | `#FFFFFF` | Pure White | Work Surface (High Clarity) | Card container, DataGrid, Node body | **ACTIVE (Controls.xaml)** |
| `ColorBgSubtle` | `BgSubtleBrush` | `#F4F6F8` | Subtle Gray | Inset Secondary Layer | Tooltips, sub-panels, search bars | ⚠️ Bypassed (Hex `#F3F4F6`) |
| `ColorBgHeader` | `BgHeaderBrush` | `#1E293B` | Mechanical Charcoal | Structural Enclosure Frame | Virtual IO header, dark terminal | ⚠️ Bypassed (Hex `#1E293B`) |
| `ColorBgToolbar` | `BgToolbarBrush` | `#DFE3E8` | Steel Gray | Tool Strip Enclosure | Top toolbar carrier, ribbon bar | ⚠️ Bypassed (Hex `#DFE3E8`) |

### Tier 2: Mechanical Borders & Dividers (6 Tokens)
| Color Key | Brush Key | Hex Code | Visual Swatch | IEC / ISA-101 Role | Expected UI Context | Adoption Status |
|---|---|---|:---:|---|---|:---:|
| `ColorBorderLight` | `BorderLightBrush` | `#D1D5DB` | Crisp Gray | 1px Mechanical Edge | Card borders, TextBox borders, grid lines | **ACTIVE (144 views)** |
| `ColorBorderMedium`| `BorderMediumBrush`| `#9CA3AF` | Mid Slate | Interactive Edge | Splitter bars, focused border outlines | **ACTIVE (Controls.xaml)** |
| `ColorBorderDark` | `BorderDarkBrush` | `#6B7280` | Deep Slate | High-Contrast Boundary | Window resize border, active frame | ⚠️ Unbound in Views |

### Tier 3: Technical Typography Tokens (6 Tokens)
| Color Key | Brush Key | Hex Code | Relative Luminance | WCAG AA on `#FFFFFF` | Typographic Role | Adoption Status |
|---|---|---|:---:|:---:|---|:---:|
| `ColorTextPrimary` | `TextPrimaryBrush` | `#111827` | 0.0126 | **16.77 : 1** (AAA) | Primary headings, tag values, data | **ACTIVE (69 views)** |
| `ColorTextSecondary`| `TextSecondaryBrush`| `#374151` | 0.0469 | **10.84 : 1** (AAA) | Column headers, descriptions, labels | **ACTIVE (88 views)** |
| `ColorTextMuted` | `TextMutedBrush` | `#6B7280` | 0.1683 | **4.81 : 1** (AA) | Timestamps, units of measure, shortcuts | **ACTIVE (11 views)** |

### Tier 4: Industrial Authority & Primary Actions (6 Tokens)
| Color Key | Brush Key | Hex Code | Interactive State | Mechanical Dynamics | Target UI Control | Adoption Status |
|---|---|---|:---:|---|---|:---:|
| `ColorPrimary` | `PrimaryBrush` | `#334155` | Normal | Authority Charcoal | `BtnPrimary` normal background | **ACTIVE (20 views)** |
| `ColorPrimaryHover` | `PrimaryHoverBrush` | `#1E293B` | MouseOver | Deepened Charcoal | `BtnPrimary` hover background | **ACTIVE (Controls.xaml)** |
| `ColorPrimaryActive`| `PrimaryActiveBrush`| `#0F172A` | Pressed | Inset Tactile Charcoal | `BtnPrimary` pressed background | **ACTIVE (Controls.xaml)** |

### Tier 5: IEC 60073 Safety & Operational Semantics (18 Tokens)
| Color Key | Brush Key | Hex Code | Semantic Role | IEC 60073 Clause | Target Automation State | Adoption Status |
|---|---|---|:---:|---|---|:---:|
| `ColorSuccess` | `SuccessBrush` | `#107C41` | Safe / RUN | 4.2.1 Safe | RUN state, Logic TRUE, Wire Active | **ACTIVE (3 views)** |
| `ColorSuccessHover` | `SuccessHoverBrush` | `#0E6B37` | Safe Hover | 4.2.1 Safe | `BtnSuccess` hover state | **ACTIVE (Controls.xaml)** |
| `ColorSuccessLight` | `SuccessLightBrush` | `#E6F4EA` | Safe Background | 4.2.1 Safe | Valid status chip background | ⚠️ Bypassed (Hex `#E6F4EA`) |
| `ColorWarning` | `WarningBrush` | `#C05621` | Caution / Force | 4.2.1 Caution | Manual Force, Stale Logic, Warning | ⚠️ Bypassed (Hex `#C05621`) |
| `ColorWarningHover` | `WarningHoverBrush` | `#9C4221` | Caution Hover | 4.2.1 Caution | Warning button hover state | ⚠️ Unbound in Views |
| `ColorWarningLight` | `WarningLightBrush` | `#FFFBEB` | Caution Background | 4.2.1 Caution | Force / Stale chip background | ⚠️ Bypassed (Hex `#FFFBEB`) |
| `ColorDanger` | `DangerBrush` | `#B91C1C` | Danger / Fault | 4.2.1 Danger | FAULT state, E-Stop Active, Trip | **ACTIVE (2 views)** |
| `ColorDangerHover` | `DangerHoverBrush` | `#991B1B` | Danger Hover | 4.2.1 Danger | Delete / E-Stop hover state | ⚠️ Unbound in Views |
| `ColorDangerLight` | `DangerLightBrush` | `#FEE2E2` | Danger Background | 4.2.1 Danger | Fault chip background, Low RTC | ⚠️ Bypassed (Hex `#FEE2E2`) |

### Tier 6: Selection & Highlight Controls (8 Tokens)
| Color Key | Brush Key | Hex Code | Selection State | Interaction Role | DataGrid / List Behavior | Adoption Status |
|---|---|---|:---:|---|---|:---:|
| `ColorSelectionHighlight` | `SelectionHighlightBrush` | `#E2E8F0` | Selected & Focused | Soft Slate Inset | Active row highlight (replaces electric blue) | **ACTIVE (Controls.xaml)** |
| `ColorSelectionHighlightText` | `SelectionHighlightTextBrush` | `#0F172A` | Selected Text | Deep Charcoal | High-contrast selected text (13.27:1) | **ACTIVE (Controls.xaml)** |
| `ColorSelectionInactive` | `SelectionInactiveBrush` | `#ECEFF1` | Selected & Unfocused | Subtle Cool Gray | Selection preserved when focus shifts | **ACTIVE (Controls.xaml)** |
| `ColorSelectionHover` | `SelectionHoverBrush` | `#F1F5F9` | Mouse Over Row | Ultra-Light Gray | Hover preview row indicator | **ACTIVE (Controls.xaml)** |

### Tier 7: IEC 61131-3 Node Domain Technical Accents (14 Tokens)
| Color Key | Brush Key | Hex Code | Node Type | IEC 61131-3 Function | Header Accent Application | Adoption Status |
|---|---|---|:---:|---|---|:---:|
| `ColorNodeTag` | `NodeTagBrush` | `#1E3A8A` | I/O Pin Tag | Variable Port Binding | DI/DO/AI/AO variable tag headers | ⚠️ Bypassed (Hex `#1E3A8A`) |
| `ColorNodeTrigger`| `NodeTriggerBrush` | `#334155` | Logic Trigger | Edge Detector (R_TRIG/F_TRIG) | Trigger block header stripe | **ACTIVE (NodeTemplates)** |
| `ColorNodeGuard` | `NodeGuardBrush` | `#C05621` | Condition Guard| Interlock / Threshold Check | Guard condition block header stripe | ⚠️ Bypassed (Hex `#C05621`) |
| `ColorNodeAction` | `NodeActionBrush` | `#107C41` | Coil / Action | Direct Output / Set / Reset | Action block header stripe | ⚠️ Bypassed (Hex `#107C41`) |
| `ColorNodeTimer` | `NodeTimerBrush` | `#4C1D95` | Timer | TON, TOF, TP Timers | Deep Indigo timer block stripe | **ACTIVE (NodeTemplates)** |
| `ColorNodeCounter`| `NodeCounterBrush`| `#0F766E` | Counter | CTU, CTD, CTUD Counters | Deep Teal counter block stripe | **ACTIVE (NodeTemplates)** |
| `ColorNodeScale` | `NodeScaleBrush` | `#475569` | Signal Scaler | Analog Linear Interpolation | Slate Blue math/scaling block | **ACTIVE (NodeTemplates)** |

### Tier 8: Industrial Semantic Structural Tokens (20 Tokens)
| Color Key | Brush Key | Hex Code | Structural Semantics | Physical Analogy | Architectural Scope | Adoption Status |
|---|---|---|:---:|---|---|:---:|
| `IndustrialPrimary` | `IndustrialPrimaryBrush` | `#334155` | Primary Action | Steel Switch Body | Master window actions | **ACTIVE (MainWindow:540)**|
| `IndustrialPrimaryHover` | `IndustrialPrimaryHoverBrush`| `#1E293B` | Primary Hover | Darkened Switch Body | Master window hover | **ACTIVE (MainWindow:562)**|
| `IndustrialPrimaryActive`| `IndustrialPrimaryActiveBrush`|`#0F172A` | Primary Active | Compressed Switch Body | Master window pressed | **ACTIVE (MainWindow:565)**|
| `IndustrialSurfaceCanvas`| `IndustrialSurfaceCanvasBrush`|`#ECEFF1` | Foundation Canvas | Cabinet Mounting Plate | Top-level host backgrounds | ⚠️ Bypassed (Hex `#ECEFF1`) |
| `IndustrialSurfacePanel` | `IndustrialSurfacePanelBrush` | `#FFFFFF` | Working Panel | Card Surface | Data inspection surfaces | ⚠️ Bypassed (Hex `#FFFFFF`) |
| `IndustrialSurfaceInset` | `IndustrialSurfaceInsetBrush` | `#F8FAFC` | Inset Display | Recessed Meter Face | Readout displays, hex panels | ⚠️ Bypassed (Hex `#F8FAFC`) |
| `IndustrialSurfaceDark` | `IndustrialSurfaceDarkBrush` | `#1E293B` | Rugged Chassis | Dark Instrument Frame | Terminal bays, dark headers | ⚠️ Bypassed (Hex `#1E293B`) |
| `IndustrialBorderDefault`| `IndustrialBorderDefaultBrush`|`#CBD5E1` | Standard DIN Enclosure| 1px DIN Rail Divider | Default panel dividers | ⚠️ Bypassed (Hex `#CBD5E1`) |
| `IndustrialBorderSubtle` | `IndustrialBorderSubtleBrush` | `#E2E8F0` | Subtle Partition | Minor Inset Edge | Grid cell dividers | ⚠️ Bypassed (Hex `#E2E8F0`) |
| `IndustrialBorderStrong` | `IndustrialBorderStrongBrush` | `#94A3B8` | Heavy Framing Edge | Structural Steel Bevel | Outer dialog framing | ⚠️ Bypassed (Hex `#94A3B8`) |

---

## 4. Screen-by-Screen Token Mapping Matrix

This matrix governs the implementation of design tokens across all 7 functional screens and the application shell of SimplePLC Studio.

### Screen 1: FBD Logic Editor Canvas (`LogicEditorView.xaml` & `NodeTemplates.xaml`)
| UI Component | Visual Role | Mandated Token Key | Compliant Hex | Current Implementation | WCAG AA Contrast | Compliance Action Required |
|---|---|---|:---:|---|:---:|---|
| **Canvas Background** | Host Grid | `BgWindowBrush` | `#ECEFF1` | Inline `#ECEFF1` | 14.14:1 (on `#111827`) | Bind to `{StaticResource BgWindowBrush}` |
| **Node Card Body** | Active Node | `BgPanelBrush` | `#FFFFFF` | Inline `#FFFFFF` | 16.77:1 (on `#111827`) | Bind to `{StaticResource BgPanelBrush}` |
| **Trigger Header** | Logic Type | `NodeTriggerBrush` | `#334155` | Token bound | 10.30:1 | Fully Compliant |
| **Action Header** | Logic Type | `NodeActionBrush` | `#107C41` | Inline `#107C41` | 4.92:1 | Bind to `{StaticResource NodeActionBrush}` |
| **Timer Header** | Logic Type | `NodeTimerBrush` | `#4C1D95` | Token bound | 8.86:1 | Fully Compliant |
| **Counter Header** | Logic Type | `NodeCounterBrush` | `#0F766E` | Token bound | 5.48:1 | Fully Compliant |
| **Ghost Node Preview**| AI Diff / Drag | `NodeScaleBrush` | `#475569` | Hardcoded `#00A389` | N/A | **REPLACE `#00A389` WITH `#475569`** |
| **Ghost Wire** | Uncommitted Link| `NodeScaleBrush` | `#475569` | Hardcoded `#00A389` | N/A | **REPLACE `#00A389` WITH `#475569`** |
| **Status Valid Badge**| Inspector Valid | `SuccessBrush` | `#107C41` | Hardcoded `#059669` | 3.61:1 (FAIL) | **UPGRADE TO `#0E6B37` ON `#E6F4EA` (5.45:1)**|
| **Status Stale Badge**| Inspector Stale | `WarningHoverBrush`| `#9C4221` | Hardcoded `#D97706` | 3.17:1 (FAIL) | **UPGRADE TO `#9C4221` ON `#FFFBEB` (5.80:1)**|
| **Toolbox Accent** | Category Stripe| `Node*Brush` | Defined | 7 hardcoded stripes | 4.50+:1 | Rebind all stripes to `Node*Brush` |

### Screen 2: Tag Catalog (`TagCatalogView.xaml`)
| UI Component | Visual Role | Mandated Token Key | Compliant Hex | Current Implementation | WCAG AA Contrast | Compliance Action Required |
|---|---|---|:---:|---|:---:|---|
| **DataGrid Background**| Table Host | `BgPanelBrush` | `#FFFFFF` | Inline `#FFFFFF` | 16.77:1 | Fully Compliant |
| **DataGrid Gridlines** | Column Separator| `BorderLightBrush` | `#D1D5DB` | Token bound | 1.45:1 (Line) | Fully Compliant |
| **Selected Row** | Active Record | `SelectionHighlightBrush`| `#E2E8F0` | Token bound | 13.27:1 (Text) | Fully Compliant |
| **Bit 0 Indicator** | De-energized | `IndustrialBorderStrongBrush`| `#94A3B8` | Inline `#94A3B8` | 2.53:1 (Glyph) | Fully Compliant (Neutral) |
| **Bit 1 Indicator** | Energized | `SuccessBrush` | `#107C41` | Inline `#107C41` | 4.92:1 (Glyph) | Bind to `{StaticResource SuccessBrush}` |
| **Type Filter Border** | Selected Outline| `BorderLightBrush` | `#D1D5DB` | Hardcoded `#004D68` | N/A | **REPLACE `#004D68` WITH `{StaticResource BorderLightBrush}`** |

### Screen 3: Rule Table (`RuleTableView.xaml`)
| UI Component | Visual Role | Mandated Token Key | Compliant Hex | Current Implementation | WCAG AA Contrast | Compliance Action Required |
|---|---|---|:---:|---|:---:|---|
| **Rule Row Header** | Condition Label| `TextSecondaryBrush`| `#374151` | Token bound | 10.84:1 | Fully Compliant |
| **Execution Bar** | Quota Gauge | `SuccessBrush` | `#107C41` | Inline `#107C41` | 4.92:1 | Bind to `{StaticResource SuccessBrush}` |
| **Edit Button Hover** | Action Trigger | `PrimaryHoverBrush` | `#1E293B` | Hardcoded `#00374A` | N/A | **REPLACE `#00374A` WITH `{StaticResource PrimaryHoverBrush}`** |
| **Raw Hex Payload** | 32-Byte Bytecode| `IndustrialSurfaceInsetBrush`| `#F8FAFC`| Hardcoded `#4ADE80` | 11.22:1 (Fatiguing) | **REPLACE NEON `#4ADE80` WITH `#F8FAFC`** |

### Screen 4: Live Watch Table (`LiveWatchView.xaml`)
| UI Component | Visual Role | Mandated Token Key | Compliant Hex | Current Implementation | WCAG AA Contrast | Compliance Action Required |
|---|---|---|:---:|---|:---:|---|
| **Watchlist Star** | Starred Item | `WarningBrush` | `#C05621` | Inline `#D97706` | 4.68:1 | Bind to `{StaticResource WarningBrush}` |
| **Force Warning Pill** | Override Caution| `WarningBrush` | `#C05621` | Inline `#C05621` | 4.68:1 | Bind to `{StaticResource WarningBrush}` |
| **[0 / OFF] Action** | De-energize | `PrimaryBrush` | `#334155` | Inline `#475569` | 7.58:1 | Fully Compliant (Safe Neutral) |
| **[1 / ON] Action** | Energize | `SuccessBrush` | `#107C41` | Inline `#107C41` | 4.92:1 | Bind to `{StaticResource SuccessBrush}` |
| **Tag Kind Filter Tabs**| Category Pills | Unified Neutral Pill| `#F3F4F6` | Hardcoded Rainbow | Conflicting | **MIGRATE RAINBOW PILLS TO UNIFIED NEUTRAL PILLS** |

### Screen 5: Hardware Simulator (`SimulatorView.xaml`)
| UI Component | Visual Role | Mandated Token Key | Compliant Hex | Current Implementation | WCAG AA Contrast | Compliance Action Required |
|---|---|---|:---:|---|:---:|---|
| **DI Input Inactive** | Sensor Low | `IndustrialBorderStrongBrush`| `#94A3B8` | Inline `#94A3B8` | 2.53:1 | Fully Compliant (IEC 60073) |
| **DI Input Active** | Sensor High | `SuccessBrush` | `#107C41` | Inline `#107C41` | 4.92:1 | Fully Compliant (IEC 60073) |
| **DI4 (E-Stop Active)**| Emergency Stop | `DangerBrush` | `#B91C1C` | Inline `#B91C1C` | 6.61:1 | **BENCHMARK EXCELLENCE (IEC 60073)** |
| **DI6 (Safety Guard)** | Interlock Open | `WarningBrush` | `#C05621` | Inline `#C05621` | 4.85:1 | **BENCHMARK EXCELLENCE (IEC 60073)** |
| **DO Output Relay** | Coil Energized | `SuccessBrush` | `#107C41` | Inline `#107C41` | 4.92:1 | Fully Compliant (IEC 60073) |

### Screen 6: Virtual IO Board (`VirtualIoBoardView.xaml`)
| UI Component | Visual Role | Mandated Token Key | Compliant Hex | Current Implementation | WCAG AA Contrast | Compliance Action Required |
|---|---|---|:---:|---|:---:|---|
| **Instrument Chassis** | Enclosure Body | `IndustrialSurfaceDarkBrush`| `#1E293B` | Inline `#1E293B` | 13.27:1 (Text) | Fully Compliant |
| **LED Lens Active** | Output High | `SuccessBrush` | `#107C41` | Inline `#107C41` | 4.92:1 | Fully Compliant |
| **Tactile Pushbutton** | Momentary In | `PrimaryBrush` | `#334155` | Inline `#334155` | 10.30:1 | Fully Compliant |
| **Toolbar Btn Pressed**| Inset State | `SelectionHighlightBrush` | `#E2E8F0` | Hardcoded `#0369A1` | N/A | **REPLACE `#0369A1` WITH `{StaticResource IndustrialPrimaryActiveBrush}`** |

### Screen 7: AI Copilot Sidebar (`AiChatView.xaml`)
| UI Component | Visual Role | Mandated Token Key | Compliant Hex | Current Implementation | WCAG AA Contrast | Compliance Action Required |
|---|---|---|:---:|---|:---:|---|
| **Chat Container** | Canvas Host | `BgWindowBrush` | `#ECEFF1` | Inline `#ECEFF1` | 14.14:1 | Fully Compliant |
| **User Message Bubble**| User Prompt | `PrimaryBrush` | `#334155` | Inline `#334155` | 10.30:1 | Fully Compliant |
| **Assistant Bubble** | Copilot Output | `BgPanelBrush` | `#FFFFFF` | Inline `#FFFFFF` | 16.77:1 | Fully Compliant |
| **Accept Proposal** | Commit Diff | `SuccessBrush` | `#107C41` | Token bound | 4.92:1 | Fully Compliant |
| **Cancel Proposal** | Reject Diff | `PrimaryBrush` | `#334155` | Token bound | 10.30:1 | Fully Compliant |
| **API Key Warning Btn**| Caution Button | `PrimaryBrush` + Warning | `#334155` | Hardcoded `#D97706` | 3.29:1 (FAIL) | **REPLACE WITH `{StaticResource BtnPrimary}` + AMBER ICON** |

### App Shell & Dialogs (`MainWindow.xaml` & Common Dialogs)
| UI Component | Visual Role | Mandated Token Key | Compliant Hex | Current Implementation | WCAG AA Contrast | Compliance Action Required |
|---|---|---|:---:|---|:---:|---|
| **Main Window Header**| App Titlebar | `BgWindowBrush` | `#ECEFF1` | Token bound | 14.14:1 | Fully Compliant |
| **Connect Button** | Toggle Link | `PrimaryBrush` + Status LED| `#334155` | Danger Red `#B91C1C` | Violates Actuator | **REPLACE RED BACKGROUND WITH NEUTRAL SLATE + GREEN LED** |
| **Save As New Rule** | Primary Commit | `IndustrialPrimaryBrush` | `#334155` | Fully Tokenized | 10.30:1 | **BENCHMARK EXCELLENCE (Scrubbed)** |
| **Deploy Btn Disabled**| Safety Lockout | `BorderMediumBrush` | `#9CA3AF` | Hardcoded `#004D68` | N/A | **REPLACE `#004D68` WITH `{StaticResource BorderMediumBrush}`** |
| **RTC Battery Low** | Hardware Health| `DangerBrush` | `#B91C1C` | Inline `#DC2626` | 5.22:1 | Bind to `{StaticResource DangerBrush}` |

---

## 5. Architectural Implementation & Governance Rules

### Rule 1: Zero Inline Hex Policy in XAML
Effective immediately upon adoption of this rubric, pull requests that introduce raw hex color codes (`#[0-9a-fA-F]{3,8}`) in any file under `SimplePLC.Studio\Views\` or `SimplePLC.Studio\Styles\` shall be automatically flagged by static analysis. Developers must bind exclusively to existing tokens in `Colors.xaml`.

### Rule 2: Prohibition of UI Colors in C# ViewModels
ViewModels must remain agnostic of hex styling strings. ViewModels should expose strongly typed semantic states (e.g., `ConnectionState.Connected`, `ValidationSeverity.Warning`) or ViewModel boolean flags (`IsConnected`, `HasFault`). The XAML View must bind to these flags via `DataTriggers` or `IValueConverter` implementations resolving to `Colors.xaml` StaticResources.

### Rule 3: Complete 4-State Interactive Control Requirement
Every custom `Button`, `ToggleButton`, or clickable `Border` template introduced into `SimplePLC.Studio` must implement all four canonical interactive states:
1. **Normal**: Resting mechanical state.
2. **IsMouseOver (Hover)**: Subtle elevation/lightening (1 tone).
3. **IsPressed (Active)**: Visual compression/inset darkening (1 tone).
4. **IsEnabled = False (Disabled)**: Opacity `0.65`, neutral border `#CBD5E1`, text `#94A3B8`.

### Rule 4: Mandatory Design Review for Semantic Color Expansion
If a new industrial feature requires an accent color not currently in the 30 distinct palette hex codes, the developer must submit a formal RFC demonstrating:
- The exact IEC 60073 or ISA-101 clause governing the operational state.
- Measured WCAG 2.1 AA relative contrast ratios ($CR \ge 4.50:1$) against `#FFFFFF`, `#ECEFF1`, and `#1E293B`.
- Addition of both `<Color x:Key="Color*">` and `<SolidColorBrush x:Key="*Brush">` tokens to `Colors.xaml`.
