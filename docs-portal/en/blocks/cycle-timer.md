# CYCLE_TIMER — Asymmetric Oscillator

<div class="doc-meta-bar">
  <span><strong>Block Code:</strong> CYCLE_TIMER (CYCLE)</span>
  <span><strong>Category:</strong> Timing, Counting &amp; Scaling</span>
  <span><strong>Execution Class:</strong> Stateful Asymmetric Flasher / Duty-Cycle Oscillator</span>
  <span><strong>Lowering Target:</strong> Dual-Phase Monotonic Oscillator Engine</span>
</div>

## 1. Synopsis & Execution Semantics

The **`CYCLE_TIMER`** block is a continuous, level-enabled **asymmetric square-wave oscillator (flasher)**. Whenever its enable input `In` is active (`1`), `CYCLE_TIMER` alternates boolean output `Q` continuously between a configurable **ON phase (`OnTimeMs`)** and an independent **OFF phase (`OffTimeMs`)**, starting in either `ON` or `OFF` phase as configured.

- **Independent Asymmetric Duty Cycle:** Unlike fixed `50%` clock bits that require external counters and comparators to achieve short duty ratios, `CYCLE_TIMER` lets you combine millisecond pulses with minute/hour pauses in a single node (for example, `OnTimeMs = 500 ms` lubrication shot every `OffTimeMs = 15 min`).
- **Configurable Initial Phase (`StartWithOn`):**
  - **`Start with ON` (`true`):** Immediately asserts `Q = 1` for `OnTimeMs` on the very scan `In` becomes `1`, then pauses `Q = 0` for `OffTimeMs`, repeating indefinitely.
  - **`Start with OFF` (`false`):** Enters the `OffTimeMs` pause (`Q = 0`) first when `In` becomes `1`, asserting `Q = 1` only after `OffTimeMs` elapses.
- **Clean De-Energize on Disable:** The instant `In` drops to `0`, oscillation halts immediately, internal phase timers reset to `0`, and `Q` forces cleanly to `0`.
- **Primary Industrial Use Cases:** Andon fault tower strobe beacons and intermittent alarm buzzers, centralized bearing lubrication pumps, greenhouse misting/fogging cycles, and periodic filter backwash agitation.

---

## 2. Pinout & Signal Contract

| Pin | Direction | Signal Kind | Data Type | Description |
| :--- | :--- | :--- | :--- | :--- |
| `In` | Input | Event / Execution (`IsEvent = true`) | Level Enable (`BOOL`) | Enables continuous `ON`/`OFF` oscillation while `1`; halts oscillation and forces `Q = 0` immediately when `0`. |
| `Q` | Output | Value / Data (`IsEvent = false`) | `BOOL` (`0` / `1`) | Asymmetric periodic pulse output bound to `OutputTag`; wires to [`ACTION`](/blocks/action), [`MQTT_PUB`](/blocks/mqtt-pub), [`HTTP_CLIENT`](/blocks/http-client), or [`TCP_CLIENT`](/blocks/tcp-client). |

---

## 3. Inspector Parameters & Validation Limits

| Parameter | Type / Options | Default | Constraints | Runtime Behavior |
| :--- | :--- | :--- | :--- | :--- |
| `OnDurationValueText` + `OnTimeUnit` | Non-negative Numeric + Unit (`ms`, `s`, `min`, `h`) | `"1"` `s` (`1000 ms`) | Independent unit scaling; `OnTimeMs` clamps to `[1 .. 4,294,967,295] ms`. | Sets exact energized duration (`Q = 1`) per cycle and displays `= 1,000 ms (1s)` summary. |
| `OffDurationValueText` + `OffTimeUnit` | Non-negative Numeric + Unit (`ms`, `s`, `min`, `h`) | `"1"` `s` (`1000 ms`) | Independent unit scaling; `OffTimeMs` clamps to `[1 .. 4,294,967,295] ms`. | Sets exact de-energized pause duration (`Q = 0`) per cycle and displays `= 1,000 ms (1s)` summary. |
| `StartWithOn` (`StartPhaseIndex`) | `Start with ON` (`0`) \| `Start with OFF` (`1`) | `Start with ON` (`true`) | 2-option selector. | Selects whether `Q` begins immediately at `1` (`OnTimeMs`) or waits in `0` (`OffTimeMs`) when `In` transitions `0 → 1`. |
| `InputTag` | Digital Tag (`DI`, `DO`, `VFLAG`) | Auto-bound from upstream wire | Must reference a digital boolean tag. | Level-sensitive enable source sustaining oscillation while `!= 0`. |
| `OutputTag` (`Q`) | Writable Digital Tag (`DO`, `VFLAG`) | Unassigned *(must be selected)* | Must reference a writable digital tag (`DO` or `VFLAG`). | Toggles continuously between `1` (`OnTimeMs`) and `0` (`OffTimeMs`) while `In == 1`; forced `0` when `In == 0`. |
| `CustomLabel` | Free-text String | Empty (`"Cycle Timer"`) | Optional schematic title override. | Customizes the header title on the Logic Graph Canvas. |

::: warning Solid-State vs. Electromechanical Output Selection
For rapid strobe cadences (`OnTimeMs` or `OffTimeMs` below `2,000 ms`), always assign `OutputTag` to a **transistor / MOSFET / SSR discrete output** rather than a mechanical relay coil to prevent premature contact wear.
:::

---

## 4. Cyclic Oscillation Rules & Operating Phases

When enabled (`In == 1`), `CYCLE_TIMER` alternates between two distinct phases:
- **On-Phase (`OnTime`):** Output `Q = 1` for the duration of `OnTimeMs`.
- **Off-Phase (`OffTime`):** Output `Q = 0` for the duration of `OffTimeMs`.
- **Initial Phase (`StartWithOn`):** When `StartWithOn = true`, oscillation begins immediately in the On-Phase (`Q = 1`). When `false`, it begins with an Off-Phase rest.
- **Immediate Safe Abort:** The instant `In` drops to `0`, output `Q` drops to `0` and internal phase timers reset safely.

---

## 5. Input Defense & Live Diagnostics

### 5.1 Independent Dual-Unit Scaling & Bad-Data Defense

- **Cross-Scale Unit Freedom:** `OnTimeUnit` and `OffTimeUnit` are completely independent—allowing engineers to configure `OnDuration = 500 ms` alongside `OffDuration = 10 min` without converting `10 min` to `600,000 ms` by hand. Both fields render explicit millisecond verification summaries (`CalculatedOnMsSummary` and `CalculatedOffMsSummary`).
- **Tolerant Float Input & Boundary Clamping:** Comma decimals (`"0,5"`) convert automatically to invariant floating-point numbers, `"0"` clamps safely to `1 ms` to prevent zero-period infinite loops, and malformed text is ignored without corrupting active timing parameters.

### 5.2 Canvas Badges in Simulator (`F5`) & Online Live Debug (`F8`)

- **Asymmetric Timing Summary & Live Blink Pill:** Displays `CYCLE (250ms/1.75s) → DO3` and `ON: 250ms | OFF: 1.75s` on the node card, while the live status pill alternates crisply between `● ON` (energized green) and `● OFF` (neutral slate) in both Simulator (`F5`) and 20 FPS Online Live Debug (`F8`).

---

## 6. Practical Industrial Wiring Example

### Scenario: Asymmetric Machine Fault Warning Beacon (`250 ms` Flash / `1750 ms` Pause)

Whenever an unacknowledged machine fault latch (`VFLAG0` = `1`) is active, an amber/red LED stack-light beacon wired to solid-state output `DO3` must emit a high-visibility **short-flash warning pattern** (`250 ms ON`, `1,750 ms OFF` = `2.0 s` period, `12.5%` duty cycle) starting immediately with an `ON` flash the instant the fault occurs, and extinguishing immediately when the operator presses Fault Reset (`DI4`).

### Tag Configuration

| Tag Name | Kind | Data Type | Description / Alias |
| :--- | :--- | :--- | :--- |
| `VFLAG0` | `VFLAG` | `BOOL` | `Active Machine Fault Latch (1 = Fault Present)` |
| `DI4` | `DI` | `BOOL` | `Operator Fault Reset Pushbutton` |
| `DO3` | `DO` | `BOOL` | `LED Warning Beacon (Transistor Output)` |

### Step-by-Step Canvas Wiring

1. **Rung 1 (Asymmetric Beacon Oscillator):** Place **`INPUT`** (`Tag = VFLAG0`, `CustomLabel = "Machine Fault Flag"`) and wire `INPUT.Out` → **`CYCLE_TIMER`**.
2. In the `CYCLE_TIMER` **Inspector**, configure:
   - `OnDurationValueText = "250"`, `OnTimeUnit = "ms"` (`= 250 ms`)
   - `OffDurationValueText = "1750"`, `OffTimeUnit = "ms"` (or `"1.75"` `s`, `= 1,750 ms`)
   - `StartWithOn = Start with ON`
   - `OutputTag = DO3` (`CustomLabel = "Fault Strobe Beacon"`)
3. **Rung 2 (Operator Fault Clear):** Place **`INPUT`** (`Tag = DI4`) → **[`TRIGGER`](/blocks/trigger)** (`ON_RISE`, `ForMs = 50` ms) → **[`ACTION`](/blocks/action)** (`SET_TAG`, `TargetTag = VFLAG0`, `ActionParam = 0`).
4. Press `F5` to simulate: toggle `VFLAG0` to `1` and verify `DO3` immediately flashes `● ON` (`250 ms`) and `● OFF` (`1,750 ms`) repeatedly; press `DI4` to clear `VFLAG0 = 0` and verify `DO3` stops immediately at `0`.

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
        <span class="synx-node-tag">VFLAG0 • BOOL</span>
        <span class="synx-node-alias">Fault Latch</span>
        <span class="synx-node-val">Machine Interlock Flag</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">ENABLE</span>
  </div>

  <div class="synx-canvas-node is-timer">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M8,1.5 A6.5,6.5 0 1 1 1.5,8 M8,1.5 L8,8 L11,10" /></svg>
        <span>CYCLE_TIMER</span>
      </div>
      <span class="synx-node-status is-active">● 250 / 1750 ms</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">ON 250ms / OFF 1750ms</span>
        <span class="synx-node-alias">Flash Strobe DO3</span>
        <span class="synx-node-val">Periodic Alarm Pulse</span>
      </div>
      <div class="synx-node-pin is-out">Q ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">STROBE</span>
  </div>

  <div class="synx-canvas-node is-action">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L4,8 M12,8 L14,8 M4,4.5 A3.5,3.5 0 1 1 4,11.5" /></svg>
        <span>ACTION</span>
      </div>
      <span class="synx-node-status is-active">● FLASH</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">DO3 = 1</span>
        <span class="synx-node-alias">Warning Beacon</span>
        <span class="synx-node-val">Amber Light Strobe</span>
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
        <span class="synx-node-tag">DI4 • BOOL</span>
        <span class="synx-node-alias">Reset Button</span>
        <span class="synx-node-val">Operator Pushbutton</span>
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
        <span class="synx-node-val">Clean Pulse Detection</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">RESET</span>
  </div>

  <div class="synx-canvas-node is-action">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L4,8 M12,8 L14,8 M4,4.5 A3.5,3.5 0 1 1 4,11.5" /></svg>
        <span>ACTION</span>
      </div>
      <span class="synx-node-status is-pass">● CLEAR</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">VFLAG0 = 0</span>
        <span class="synx-node-alias">Clear Fault Latch</span>
        <span class="synx-node-val">Extinguish Beacon DO3</span>
      </div>
      <div class="synx-node-pin is-out" style="visibility: hidden;">Out ●</div>
    </div>
  </div>
</div>
