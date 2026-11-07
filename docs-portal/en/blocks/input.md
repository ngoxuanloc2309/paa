# INPUT — Tag Source Node

<div class="doc-meta-bar">
  <span><strong>Block Code:</strong> INPUT</span>
  <span><strong>Category:</strong> Core Rule Pipeline</span>
  <span><strong>Execution Class:</strong> Synchronous Scan Source (0 Latency)</span>
  <span><strong>Lowering Target:</strong> Process Image Tag Binding</span>
</div>

## 1. Synopsis & Execution Semantics

The **`INPUT`** block is the foundational signal entry point on the **SynaptiX IDE** Logic Graph Canvas. It binds a single physical I/O channel or virtual memory variable from the project [Tag Catalog](/workspaces/tag-catalog-tab) and exposes its live state onto the `Out` value connector on every scan cycle.

- **Deterministic Process Image Read:** At the start of each scan cycle on the **SynaptiX Runtime**, hardware inputs are latched into an immutable process image snapshot. Every `INPUT` block referencing the same tag reads an identical, race-free value throughout that scan pass.
- **Zero-Overhead Lowering:** `INPUT` does not consume a standalone execution slot; during compilation (`F7`), its bound tag index and physical data type (`BOOL`, `INT32`, or IEEE-754 `REAL`) propagate directly into downstream [`TRIGGER`](/blocks/trigger), [`COMPARATOR`](/blocks/comparator), [`SCALE`](/blocks/scale), [`TON`](/blocks/ton), [`CTU`](/blocks/ctu), or [`MQTT_PUB`](/blocks/mqtt-pub) blocks.
- **Primary Industrial Use Cases:** Reading discrete limit switches and E-Stop contacts (`DI`), monitoring coil feedback (`DO`), feeding raw `4–20 mA` / `0–10 V` sensor channels (`AI`) into calibration chains, and routing internal state flags (`VFLAG`) or retentive setpoints (`VREG_R`) across multi-rung control sequences.

---

## 2. Pinout & Signal Contract

`INPUT` acts strictly as a pure signal source and therefore exposes zero input connectors and one universal value output connector.

| Pin | Direction | Signal Kind | Data Type | Description |
| :--- | :--- | :--- | :--- | :--- |
| `Out` | Output | Value / Data (`IsEvent = false`) | Polymorphic (`BOOL` \| `INT32` \| `REAL`) | Emits the live value and tag metadata of the selected `Tag`. Capable of `1:N` fan-out to multiple downstream blocks simultaneously. |

---

## 3. Inspector Parameters & Validation Limits

Selecting an `INPUT` node opens its property sheet in the right-hand **Inspector** panel:

| Parameter | Type / Options | Default | Constraints | Runtime Behavior |
| :--- | :--- | :--- | :--- | :--- |
| `Tag` | Tag Selector (`DI`, `DO`, `AI`, `VFLAG`, `VREG`, `VREG_R`, `COUNTER`) | `DI0` (first available discrete input) | Must reference a valid defined tag (`Index < 65535`). Unassigned tags raise a pre-deploy compile error. | Determines both the physical memory address sampled each scan and the strict data type contract (`BOOL`, `INT32`, or `REAL`) enforced downstream. |
| `CustomLabel` | Free-text String | Empty *(uses `Tag.Name`, e.g., `DI0`)* | Optional display override; does not alter underlying tag address or alias in the Tag Catalog. | Replaces the header title on the canvas node for schematic readability (e.g., `"Tank High Float"`). |
| `TagValueText` *(Simulator)* | Type-Locked Input / Toggle (`[ 0 ]` `[ 1 ]` for `BOOL`; numeric field for `INT32` / `REAL`) | `"0"` (`"0.0"` for `REAL`) | Active in Offline & Simulator mode (`F5`). Subject to Bad-Data Defense normalization and auto-revert. | Injects synthetic process values directly into the simulated process image to exercise downstream triggers, timers, and comparators. |

### Supported Tag Memory Domains

| Tag Kind Badge | Memory Domain | Native Data Type | Typical Source |
| :--- | :--- | :--- | :--- |
| `DI` | Discrete Input | `BOOL` (`0` or `1`) | Opto-isolated physical digital input terminals (`24 VDC` sensors, pushbuttons). |
| `DO` | Discrete Output Readback | `BOOL` (`0` or `1`) | Current state of physical relay, transistor, or solid-state output channels. |
| `AI` | Analog Input | `INT32` / `REAL` | Onboard ADC channels (`4–20 mA`, `0–10 V`, NTC/RTD conditioning). |
| `VFLAG` | Virtual Flag | `BOOL` (`0` or `1`) | Internal volatile boolean state bit for multi-branch interlocking. |
| `VREG` | Virtual Register | `INT32` / `REAL` | Internal volatile 32-bit register for intermediate math, scaling, or fieldbus values. |
| `VREG_R` | Retentive Virtual Register | `INT32` / `REAL` | Non-volatile 32-bit register preserved across power cycles (recipes, calibration offsets). |
| `COUNTER` | Counter Accumulator (`CV`) | `INT32` | Live accumulated count register driven by high-speed or logic counters. |

---

## 4. Simulation Stimulus & Interactive Testing (`F5`)

During offline simulation (`F5`), the `INPUT` block allows commissioning engineers to inject test stimuli directly on the canvas:
- **Digital Quick-Toggle (`BOOL`):** Clicking the on-canvas toggle button alternates the tag value cleanly between `0` (`○ 0`) and `1` (`● 1`).
- **Analog 4-Step Quick-Cycle (`AI` / `VREG`):** Clicking the quick-cycle button on an analog node steps through standard test checkpoints `0 → 50 → 90 → 100 → 0`, while exact numerical values can be entered into `TagValueText` in the Inspector.

---

## 5. Input Defense & Live Diagnostics

### 5.1 Bad-Data Defense (`SmartInputParser`)

To uphold the **Visual Truth Guarantee**—ensuring the Inspector display never contradicts physical memory—`TagValueText` enforces two-phase input resolution upon pressing `Enter` or leaving the field (`LostFocus`):

- **Digital (`BOOL`) Tags:** The Inspector presents dedicated binary `[ 0 ]` and `[ 1 ]` buttons alongside tolerant parsing (`"true"`, `"on"`, `"yes"` → `"1"`; `"false"`, `"off"`, `"no"` → `"0"`). Unparseable text immediately reverts to the last committed `"0"` or `"1"`.
- **Integer (`INT32`) Tags:** Accepts signed 32-bit integers. Tolerates harmless `.0` suffixes (`"100.0"` → normalized to `"100"`), but strictly **rejects fractional decimals** such as `"25.5"` or alphabetic strings (`"abc"`), reverting `TagValueText` to the exact integer stored in memory.
- **Floating-Point (`REAL`) Tags:** Accepts both dot and comma decimal separators (`"25,5"` → `"25.5"`) and automatically formats whole numbers with an explicit `.0` suffix (`"25"` → `"25.0"`) to confirm IEEE-754 32-bit float storage.

### 5.2 Canvas Badges in Simulator (`F5`) & Online Live Debug (`F8`)

| Operating Mode | Node Visual Indicator | Diagnostic Behavior |
| :--- | :--- | :--- |
| **Offline / Design** | Tag Kind pill (`DI`, `AI`, `VREG_R`) + `ValueBadgeText` | Displays initial/simulated value formatted strictly per tag data type (`0`/`1`, integer, or `0.0###` float). |
| **Simulator (`F5`)** | Interactive `○ 0` / `● 1` or step-cycle button + energized border | Border and output wire illuminate green whenever a digital tag equals `1` or an analog tag exceeds `0`. |
| **Online Live Debug (`F8`)** | 20 FPS `LiveBadgeText` (`1`, `0`, `1,250`, `36.5`) | Streams live hardware readings. Only boolean tags trigger active green node illumination; numeric registers display live formatted numbers cleanly on a neutral slate card. |
| **Quality Degradation** | ` [STALE]` suffix / Last Known badge | Appends ` [STALE]` if tag quality drops, and retains the final sampled reading on disconnect so intermittent wiring faults remain visible. |

---

## 6. Practical Industrial Wiring Example

### Scenario: Fan-Out from a Single Analog Pressure Sensor to Dual Threshold Alarms

A `4–20 mA` discharge pressure transmitter wired to analog input `AI0` (`0–10.0 bar` scaled into `VREG0` or read directly as raw millibar counts `0–10000`) must drive both a **High-Pressure Warning** (`8000 mbar`) and an **Over-Pressure Emergency Pump Trip** (`9500 mbar`) from a single `INPUT` source node.

### Tag Configuration

| Tag Name | Kind | Data Type | Description / Alias |
| :--- | :--- | :--- | :--- |
| `AI0` | `AI` | `INT32` | `Discharge Pressure (0-10000 mbar)` |
| `VFLAG0` | `VFLAG` | `BOOL` | `High Pressure Warning Flag` |
| `DO0` | `DO` | `BOOL` | `Main Booster Pump Contactor` |

### Step-by-Step Canvas Wiring

1. Press `Ctrl+2` to open the **Logic Graph Canvas** and add one **`INPUT`** block; bind `Tag = AI0` and set `CustomLabel = "Discharge Pressure"`.
2. With the `INPUT` node selected, add a **[`COMPARATOR`](/blocks/comparator)** (`CompareOp = GTE`, `ThresholdLo = 8000`) followed by a **[`TRIGGER`](/blocks/trigger)** (`TriggerType = ON_RISE`, `ForMs = 500`) and an **[`ACTION`](/blocks/action)** (`ActionType = SET_TAG`, `TargetTag = VFLAG0`, `ActionParam = 1`).
3. Drag a second wire (`1:N` fan-out) from the same `INPUT.Out` pin to a second **[`COMPARATOR`](/blocks/comparator)** (`CompareOp = GTE`, `ThresholdLo = 9500`) → **[`TRIGGER`](/blocks/trigger)** (`TriggerType = ON_RISE`, `ForMs = 100`) → **[`ACTION`](/blocks/action)** (`ActionType = SET_TAG`, `TargetTag = DO0`, `ActionParam = 0`).
4. Press `F5` to launch the **Simulator**, select the `AI0` `INPUT` node, and enter `"8200"` in `TagValueText` (`Enter`): verify `VFLAG0` latches `1` after `500 ms` while `DO0` stays `1`. Next, enter `"9600"` and confirm `DO0` trips to `0` after `100 ms`.

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
        <span class="synx-node-tag">AI0 • INT16</span>
        <span class="synx-node-alias">Discharge Pressure</span>
        <span class="synx-node-val">Raw Pipeline Pressure</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">8200</span>
  </div>

  <div class="synx-canvas-node is-comparator">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M3.5,6 L8,9 L3.5,12 M9.5,7 L13,7 M9.5,10.5 L13,10.5" /></svg>
        <span>COMPARATOR</span>
      </div>
      <span class="synx-node-status is-true">● GTE 8000</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">&gt;= 8000</span>
        <span class="synx-node-alias">Warning Threshold</span>
        <span class="synx-node-val">Pre-Alarm Check</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">TRUE</span>
  </div>

  <div class="synx-canvas-node is-trigger">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M10,1 L5,8.5 L8.5,8.5 L6,15 L11,7.5 L7.5,7.5 Z" /></svg>
        <span>TRIGGER</span>
      </div>
      <span class="synx-node-status is-rise">● 500 ms</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">500 ms Filter</span>
        <span class="synx-node-alias">Noise Dwell Filter</span>
        <span class="synx-node-val">Confirm Pressure Rise</span>
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
        <span class="synx-node-tag">VFLAG0 = 1</span>
        <span class="synx-node-alias">Latch Warning Flag</span>
        <span class="synx-node-val">Amber Beacon Alert</span>
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
      <span class="synx-node-status is-live">● LIVE</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in" style="visibility: hidden;">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">AI0 • INT16</span>
        <span class="synx-node-alias">Discharge Pressure</span>
        <span class="synx-node-val">Raw Pipeline Pressure</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">9600</span>
  </div>

  <div class="synx-canvas-node is-comparator">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M3.5,6 L8,9 L3.5,12 M9.5,7 L13,7 M9.5,10.5 L13,10.5" /></svg>
        <span>COMPARATOR</span>
      </div>
      <span class="synx-node-status is-true">● GTE 9500</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">&gt;= 9500</span>
        <span class="synx-node-alias">Emergency High Trip</span>
        <span class="synx-node-val">Severe Overpressure</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">TRIP</span>
  </div>

  <div class="synx-canvas-node is-trigger">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M10,1 L5,8.5 L8.5,8.5 L6,15 L11,7.5 L7.5,7.5 Z" /></svg>
        <span>TRIGGER</span>
      </div>
      <span class="synx-node-status is-rise">● 100 ms</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">100 ms Filter</span>
        <span class="synx-node-alias">Rapid Trip Filter</span>
        <span class="synx-node-val">Protect Burst Pipe</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">CUTOUT</span>
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
        <span class="synx-node-alias">Trip Pump DO0</span>
        <span class="synx-node-val">Emergency Depressurize</span>
      </div>
      <div class="synx-node-pin is-out" style="visibility: hidden;">Out ●</div>
    </div>
  </div>
</div>
