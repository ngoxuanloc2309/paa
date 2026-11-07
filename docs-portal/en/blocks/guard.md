# GUARD — Safety Interlock Gate

<div class="doc-meta-bar">
  <span><strong>Block Code:</strong> GUARD</span>
  <span><strong>Category:</strong> Core Rule Pipeline</span>
  <span><strong>Execution Class:</strong> Synchronous Permissive Gate</span>
  <span><strong>Lowering Target:</strong> Rule Stage 2 (Guard Interlock Engine)</span>
</div>

## 1. Synopsis & Execution Semantics

The **`GUARD`** block is a synchronous permissive and safety interlock gate placed inline between an upstream event source ([`TRIGGER`](/blocks/trigger), [`INPUT`](/blocks/input), [`COMPARATOR`](/blocks/comparator), [`MB_READ`](/blocks/mb-read), or [`MQTT_SUB`](/blocks/mqtt-sub)) and a downstream executor ([`ACTION`](/blocks/action), [`TON`](/blocks/ton), [`CTU`](/blocks/ctu), [`MB_WRITE`](/blocks/mb-write), [`DIRECT_WRITE`](/blocks/direct-write), or IIoT client).

- **Pass-or-Block Event Gating:** `GUARD` never initiates an execution pulse on its own. Instead, at the exact scan cycle when an upstream pulse arrives at `In`, `GUARD` inspects the latched process image state of `GuardTag`:
  - **Pass (`Condition True`):** The execution pulse propagates unimpeded out of `Out` within the same scan cycle (`0` scan delay).
  - **Block (`Condition False`):** The execution pulse is suppressed immediately; downstream actuators remain untouched.
- **Normal vs. Inverted (`NOT`) Polarity:** Supports active-high permissives (`Negate = false`, pass when `GuardTag == 1`) such as *"Safety Door Closed = 1"* and active-low fault inhibits (`Negate = true`, pass when `GuardTag == 0`) such as *"Thermal Overload Fault == 0"*.
- **Primary Industrial Use Cases:** Machine guard door interlocks, E-Stop and thermal relay permissives, Auto/Manual mode selection gates, and pump dry-run protection.

---

## 2. Pinout & Signal Contract

| Pin | Direction | Signal Kind | Data Type | Description |
| :--- | :--- | :--- | :--- | :--- |
| `In` | Input | Event / Execution (`IsEvent = true`) | Event Pulse | Receives an upstream execution transition from [`TRIGGER`](/blocks/trigger), [`INPUT`](/blocks/input), [`COMPARATOR`](/blocks/comparator), or fieldbus/cloud `Valid`/`Done` pins. |
| `Out` | Output | Event / Execution (`IsEvent = true`) | Gated Event Pulse | Forwards the upstream execution pulse if and only if the `GuardTag` permissive evaluates true during that scan pass. |

::: warning Single-Stage Guard Topology Rule
Direct guard chaining (`GUARD → GUARD`) and backward loops (`GUARD → TRIGGER`) are rejected by the graph compiler to guarantee flat, constant-time rule execution. To require multiple safety permissives simultaneously (e.g., `Door_Closed AND Oil_Pressure_OK`), combine them into an intermediate `VFLAG` tag and bind that `VFLAG` to `GuardTag`.
:::

---

## 3. Inspector Parameters & Validation Limits

| Parameter | Type / Options | Default | Constraints | Runtime Behavior |
| :--- | :--- | :--- | :--- | :--- |
| `GuardTag` | Digital Tag Selector (`DI`, `DO`, `VFLAG`) | Unassigned *(must be selected)* | Must reference a digital boolean tag (`IsDigital == true`). Selecting an analog/register tag triggers `IsInvalidTagType`. | Sampled synchronously from the process image whenever an incoming pulse arrives on `In`. |
| `Negate` | Boolean Toggle (`Pass when 1/ON` vs. `Invert NOT — Pass when 0/OFF`) | `false` (`Pass when 1/ON`) | Binary polarity option. | **`false`:** Passes pulse when `GuardTag != 0`. **`true`:** Passes pulse only when `GuardTag == 0` (renders `! TagName` badge). |
| `CustomLabel` | Free-text String | Empty (`"Guard"`) | Optional schematic title override. | Customizes the node header on the Logic Graph Canvas. |

### Polarity Truth Table

| `Negate` Setting | Canvas Badge | `GuardTag = 0` (`OFF`) | `GuardTag = 1` (`ON`) | Typical Wiring Pattern |
| :--- | :--- | :--- | :--- | :--- |
| `false` (Normal) | `Tag` (`1/ON`) | **BLOCKED** (Pulse dropped) | **PASSED** (`Out` fires) | Fail-safe NC safety circuit (`1` = Healthy / Closed) |
| `true` (Inverted `NOT`) | `! Tag` (`0/OFF`) | **PASSED** (`Out` fires) | **BLOCKED** (Pulse dropped) | Fault flag or busy inhibit (`0` = No Fault Active) |

---

## 4. Real-Time Interlock Gating Rules

During each controller scan cycle, the `GUARD` block evaluates safety conditions synchronously:
- **Normal Gating (`Negate = false`):** Pulses at pin `In` only pass to `Out` if `GuardTag == 1` (e.g., safety guard is closed `SafetyDoorOk == 1`). If `GuardTag == 0`, the pulse is immediately suppressed.
- **Inverted Gating (`Negate = true`):** Pulses only pass if `GuardTag == 0` (e.g., no fault active `FaultFlag == 0`). If a fault exists (`GuardTag == 1`), the actuation pulse is blocked.

---

## 5. Input Defense & Live Diagnostics

### 5.1 Digital-Only Type Guard (`IsInvalidTagType`)

Physical safety interlocks must evaluate deterministic binary states (`0` or `1`), never unthresholded raw analog counts. If an engineer inadvertently assigns a non-digital tag (`AI`, `VREG`, `COUNTER`) to `GuardTag`, `GUARD` immediately raises `IsInvalidTagType = true`, displaying an inline **ANSI Z535.6 warning banner** in the Inspector prompting the user to select a digital tag (`DI`, `DO`, `VFLAG`) or insert a [`COMPARATOR`](/blocks/comparator) upstream.

### 5.2 Canvas Badges in Simulator (`F5`) & Online Live Debug (`F8`)

- **Simulator Quick-Toggle & Gate Verdict (`F5`):** Provides an inline `○ 0` / `● 1` toggle button for `GuardTag` during desktop simulation and displays explicit `PASSED` or `BLOCKED` status feedback whenever an upstream trigger fires.
- **20 FPS Live Interlock Readout (`F8`):** Displays both the polarity contract and the real-time hardware bit state on a single pill—for example, `Pass 1/ON (Door_Closed: 1)` or `Invert 0/OFF (Overload_Alm: 0)`.
- **Stale & Offline Retention:** Appends ` [STALE]` if telemetry quality degrades (`TagQuality.Stale`) and preserves the last known digital state on disconnect.

---

## 6. Practical Industrial Wiring Example

### Scenario: Hydraulic Press Cycle Start Gated by Safety Light Curtain

An operator start pushbutton on `DI0` triggers the downward stroke solenoid (`DO0`) of a hydraulic stamping press. For operator safety, the stroke command must be strictly blocked unless the optical **Safety Light Curtain** (`DI1`, fail-safe wired `1` = Clear / Unobstructed) is healthy at the instant `DI0` is pressed, while an independent rule immediately drops `DO0` to `0` if `DI1` falls to `0` mid-stroke.

### Tag Configuration

| Tag Name | Kind | Data Type | Description / Alias |
| :--- | :--- | :--- | :--- |
| `DI0` | `DI` | `BOOL` | `Operator Cycle Start Pushbutton (NO)` |
| `DI1` | `DI` | `BOOL` | `Safety Light Curtain OSSD (1 = Clear, 0 = Beam Broken)` |
| `DO0` | `DO` | `BOOL` | `Hydraulic Press Downward Solenoid` |

### Step-by-Step Canvas Wiring

1. **Rung 1 (Gated Start):** Place **`INPUT`** (`DI0`) → **[`TRIGGER`](/blocks/trigger)** (`ON_RISE`, `ForMs = 50` ms pushbutton debounce) → **`GUARD`** (`GuardTag = DI1`, `Negate = false`) → **[`ACTION`](/blocks/action)** (`SET_TAG`, `TargetTag = DO0`, `ActionParam = 1`).
2. **Rung 2 (Immediate Beam-Break Trip):** Place **`INPUT`** (`DI1`) → **[`TRIGGER`](/blocks/trigger)** (`ON_FALL`, `ForMs = 0` ms immediate) → **[`ACTION`](/blocks/action)** (`SET_TAG`, `TargetTag = DO0`, `ActionParam = 0`).
3. Press `F5` to verify both nominal and fault scenarios:
   - With `DI1 = 0` (beam obstructed), toggle `DI0` `0 → 1`: `GUARD` reports **BLOCKED** and `DO0` stays `0`.
   - Set `DI1 = 1` (curtain clear), toggle `DI0` `0 → 1` for `50 ms`: `GUARD` reports **PASSED** and `DO0` energizes to `1`.
   - Toggle `DI1` `1 → 0`: Rung 2 immediately trips `DO0` back to `0` on the very first scan cycle.

<div class="synx-node-chain" style="margin-bottom: 12px;">
  <div class="synx-canvas-node is-input">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L12,8 M8,4 L12,8 L8,12" /></svg>
        <span>INPUT</span>
      </div>
      <span class="synx-node-status is-live">● LIVE</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in" style="visibility: hidden;">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">DI0 • BOOL</span>
        <span class="synx-node-alias">Start Pushbutton</span>
        <span class="synx-node-val">Press Cycle Command</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">EDGE</span>
  </div>

  <div class="synx-canvas-node is-trigger">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M10,1 L5,8.5 L8.5,8.5 L6,15 L11,7.5 L7.5,7.5 Z" /></svg>
        <span>TRIGGER</span>
      </div>
      <span class="synx-node-status is-rise">● ON_RISE</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">50 ms Dwell</span>
        <span class="synx-node-alias">Debounce Filter</span>
        <span class="synx-node-val">Steady Operator Press</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">TRUE</span>
  </div>

  <div class="synx-canvas-node is-guard">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M8,1.5 L13.5,4 L13.5,9.5 Q13.5,13.5 8,15 Q2.5,13.5 2.5,9.5 L2.5,4 Z" /></svg>
        <span>GUARD</span>
      </div>
      <span class="synx-node-status is-pass">● PASS</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">DI1 == 1</span>
        <span class="synx-node-alias">Safety Light Curtain</span>
        <span class="synx-node-val">Optical Path Clear</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">ENABLE</span>
  </div>

  <div class="synx-canvas-node is-action">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L4,8 M12,8 L14,8 M4,4.5 A3.5,3.5 0 1 1 4,11.5" /></svg>
        <span>ACTION</span>
      </div>
      <span class="synx-node-status is-active">● SET 1</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">DO0 = 1</span>
        <span class="synx-node-alias">Advance Ram</span>
        <span class="synx-node-val">Energize Hydraulic Valve</span>
      </div>
      <div class="synx-node-pin is-out" style="visibility: hidden;">Out ●</div>
    </div>
  </div>
</div>

<div class="synx-node-chain" style="margin-top: 12px;">
  <div class="synx-canvas-node is-input">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L12,8 M8,4 L12,8 L8,12" /></svg>
        <span>INPUT</span>
      </div>
      <span class="synx-node-status is-live">● OSSD</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in" style="visibility: hidden;">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">DI1 • OSSD</span>
        <span class="synx-node-alias">Curtain Receiver</span>
        <span class="synx-node-val">Beam Interrupted (1 → 0)</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">EDGE</span>
  </div>

  <div class="synx-canvas-node is-trigger">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M10,1 L5,8.5 L8.5,8.5 L6,15 L11,7.5 L7.5,7.5 Z" /></svg>
        <span>TRIGGER</span>
      </div>
      <span class="synx-node-status is-rise">● ON_FALL</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">0 ms Instant</span>
        <span class="synx-node-alias">Immediate Trip</span>
        <span class="synx-node-val">Hard Fall Edge</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">ABORT</span>
  </div>

  <div class="synx-canvas-node is-action">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L4,8 M12,8 L14,8 M4,4.5 A3.5,3.5 0 1 1 4,11.5" /></svg>
        <span>ACTION</span>
      </div>
      <span class="synx-node-status is-pass">● SET 0</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">DO0 = 0</span>
        <span class="synx-node-alias">Emergency Cutout</span>
        <span class="synx-node-val">Dump Hydraulic Pressure</span>
      </div>
      <div class="synx-node-pin is-out" style="visibility: hidden;">Out ●</div>
    </div>
  </div>
</div>
