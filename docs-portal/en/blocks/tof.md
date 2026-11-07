# TOF — Off-Delay Timer

<div class="doc-meta-bar">
  <span><strong>Block Code:</strong> TOF</span>
  <span><strong>Category:</strong> Timing, Counting &amp; Scaling</span>
  <span><strong>Execution Class:</strong> Stateful Monotonic Timer (IEC 61131-3 TOF)</span>
  <span><strong>Lowering Target:</strong> Timer Macro / Post-Run Hold State Machine</span>
</div>

## 1. Synopsis & Execution Semantics

The **`TOF`** (**Timer Off-Delay**) block energizes its boolean output `Q` (`1`) instantaneously when input `In` asserts `1`, and **holds `Q = 1` for a configurable overrun duration `PresetMs`** after `In` transitions from `1 → 0`.

- **Zero-Latency Turn-On, Delayed Turn-Off:** While `In == 1`, `Q` stays `1` and elapsed off-time `ET` remains `0`. The monotonic countdown begins strictly on the **falling edge** (`In: 1 → 0`).
- **Seamless Retrigger / Cancellation:** If `In` rises back to `1` while the off-delay countdown is still in progress (`0 < ET < PresetMs`), `ET` resets immediately to `0` and `Q` stays continuously `1` without any dropout glitch. Only when `In` stays `0` uninterrupted for the full `PresetMs` does `Q` return to `0`.
- **Primary Industrial Use Cases:** CNC spindle and furnace forced-air cooling fan post-run, compressor lubrication oil pump coast-down hold, stairwell and warehouse motion-activated lighting hold, and air-lock exhaust purge extensions.

---

## 2. Pinout & Signal Contract

| Pin | Direction | Signal Kind | Data Type | Description |
| :--- | :--- | :--- | :--- | :--- |
| `In` | Input | Event / Execution (`IsEvent = true`) | Enabling State (`BOOL`) | Drives `Q = 1` immediately when `1`; initiates the `PresetMs` off-delay hold timer when transitioning `1 → 0`. |
| `Q` | Output | Value / Data (`IsEvent = false`) | `BOOL` (`0` / `1`) | Off-delayed boolean output bound to `OutputTag`; wires to [`ACTION`](/blocks/action), [`MQTT_PUB`](/blocks/mqtt-pub), [`HTTP_CLIENT`](/blocks/http-client), or [`TCP_CLIENT`](/blocks/tcp-client). |

---

## 3. Inspector Parameters & Validation Limits

| Parameter | Type / Options | Default | Constraints | Runtime Behavior |
| :--- | :--- | :--- | :--- | :--- |
| `DurationValueText` | Non-negative Float / Integer | `"3"` | Accepts dot or comma decimals (`"1.5"`, `"1,5"`). Clamps `PresetMs` to `[1 .. 4,294,967,295] ms`. | Multiplied by `TimeUnit` to compute exact post-run hold duration `PresetMs`. |
| `TimeUnit` | `ms`, `s`, `min`, `h` | `"s"` (`×1,000`) | Unit multipliers: `ms = 1`, `s = 1,000`, `min = 60,000`, `h = 3,600,000`. | Automatically scales `PresetMs` and displays `= 3,000 ms (3s)` confirmation text. |
| `PresetMs` | Unsigned 32-bit Integer (`uint`) | `3000` (`3,000 ms`) | `1 .. 4,294,967,295 ms` (~`49.7 days`). | Exact duration `Q` remains latched at `1` after `In` falls to `0`. |
| `InputTag` | Digital Tag (`DI`, `DO`, `VFLAG`) | Auto-bound from upstream wire | Must reference a digital boolean tag. | Monitored each scan cycle for immediate turn-on (`1`) and falling-edge countdown start (`1 → 0`). |
| `OutputTag` (`Q`) | Writable Digital Tag (`DO`, `VFLAG`) | Unassigned *(must be selected)* | Must reference a writable digital tag (`DO` or `VFLAG`). | Driven to `1` while `In == 1` or during active off-delay hold; drops to `0` once `ET >= PresetMs`. |
| `CustomLabel` | Free-text String | Empty (`"TOF Timer"`) | Optional schematic title override. | Customizes the header title on the Logic Graph Canvas. |

---

## 4. Off-Delay Rules & Waveform Phases

The table below details immediate turn-on (`In = 1`), falling-edge post-run hold (`In: 1 → 0`), mid-hold re-assertion (`In: 0 → 1` canceling countdown cleanly), and final turn-off expiration:

| Waveform Phase | Input `In` | Elapsed Off-Time `ET` | Output `Q` | Engineering State |
| :--- | :--- | :--- | :--- | :--- |
| **1. Initial Idle** | `0` | `0 ms` | `0` | System at rest (`OFF`) |
| **2. Input Energized** | `1` | `0 ms` | `1` (`ON`) | Immediate output activation (`0 ms` delay) |
| **3. Falling Edge (`1 → 0`)** | `0` | `0 → PresetMs` | `1` (`ON`) | Post-run hold active; `ET` counting toward `PresetMs` |
| **4. Re-Triggered Early** | `1` | Resets to `0 ms` | `1` (`ON`) | Seamless continuation with zero output chatter |
| **5. Hold Expired** | `0` | `PresetMs` | `0` (`OFF`) | Post-run completed; output de-energizes |

---

## 5. Input Defense & Live Diagnostics

### 5.1 Multi-Unit Scaling & Bad-Data Defense

- **Human-Readable Unit Conversion:** Long post-run cooling intervals such as `2 min` or `0.5 h` can be entered directly using the `TimeUnit` selector (`min` or `h`) without error-prone manual millisecond multiplication (`= 120,000 ms (2min)`).
- **Safe Boundary Enforcement:** Comma decimals (`"0,5"`) convert automatically to invariant floating-point values, zero entries clamp safely to `1 ms`, and invalid alphabetic strings are ignored without altering the committed `PresetMs`.

### 5.2 Canvas Badges in Simulator (`F5`) & Online Live Debug (`F8`)

- **Post-Run Countdown Visualization (`F5`):** In Simulator mode, `TOF` displays `Q = ON` immediately when `In = 1`, and displays real-time elapsed hold progress (`ElapsedMs / PresetMs`) after `In` drops to `0` until `Q` finally turns `OFF`.
- **20 FPS Hardware Telemetry (`F8`):** Tracks live `OutputTag` state (`Q = ON` vs. `OFF`) at 20 FPS with automatic `[STALE]` quality flagging if telemetry updates expire.

---

## 6. Practical Industrial Wiring Example

### Scenario: CNC Milling Spindle Forced-Air Cooling Fan `2-Minute` Post-Run

When a high-speed CNC spindle inverter (`DI1` run-feedback contact) operates, its external forced-air cooling blower (`DO1`) must turn on immediately. After the machining program stops the spindle (`DI1: 1 → 0`), the cooling blower must **continue running for `2 minutes` (`120,000 ms`)** to dissipate residual stator heat—and remain continuously on if a new part cycle restarts the spindle before the 2 minutes expire.

### Tag Configuration

| Tag Name | Kind | Data Type | Description / Alias |
| :--- | :--- | :--- | :--- |
| `DI1` | `DI` | `BOOL` | `Spindle VFD Run Feedback (1 = Running)` |
| `DO1` | `DO` | `BOOL` | `Spindle Forced-Air Cooling Blower Contactor` |

### Step-by-Step Canvas Wiring

1. Place **`INPUT`** (`Tag = DI1`, `CustomLabel = "Spindle Running"`).
2. Select the `INPUT` block and click **`TOF`** in the toolbox to wire `INPUT.Out → TOF.In`.
3. In the `TOF` **Inspector**, set `DurationValueText = "2"`, select `TimeUnit = "min"` (confirming `= 120,000 ms (2min)`), and bind `OutputTag = DO1` (`CustomLabel = "Cooling Post-Run"`).
4. Press `F5` to verify in the **Simulator** (for rapid desktop verification, temporarily test with `3 s`, then restore `2 min` before deployment):
   - Toggle `DI1` `0 → 1`: `DO1` turns `1` immediately.
   - Toggle `DI1` `1 → 0`: `DO1` stays `1` while `ET` counts up toward `PresetMs`.
   - Toggle `DI1` `0 → 1` mid-countdown: `ET` resets to `0` and `DO1` stays `1` without flickering.
   - Toggle `DI1` `1 → 0` and let `PresetMs` elapse completely: `DO1` drops cleanly to `0`.

<div class="synx-node-chain">
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
        <span class="synx-node-tag">DI1 • BOOL</span>
        <span class="synx-node-alias">Spindle Running</span>
        <span class="synx-node-val">VFD Run Feedback</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">TRUE</span>
  </div>

  <div class="synx-canvas-node is-timer">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M8,1.5 A6.5,6.5 0 1 1 1.5,8 M8,1.5 L8,8 L11,10" /></svg>
        <span>TOF TIMER</span>
      </div>
      <span class="synx-node-status is-active">● 120,000 ms</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">PT = 2 min • DO1</span>
        <span class="synx-node-alias">Post-Stop Cooling</span>
        <span class="synx-node-val">Maintain Fan 2 Min</span>
      </div>
      <div class="synx-node-pin is-out">Q ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">DO1 = 1</span>
  </div>

  <div class="synx-canvas-node is-action">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L4,8 M12,8 L14,8 M4,4.5 A3.5,3.5 0 1 1 4,11.5" /></svg>
        <span>ACTION</span>
      </div>
      <span class="synx-node-status is-active">● COMMIT</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">DO1 • Exhaust Fan</span>
        <span class="synx-node-alias">Cooling Contactor</span>
        <span class="synx-node-val">Auto Stop After 2 Min</span>
      </div>
      <div class="synx-node-pin is-out" style="visibility: hidden;">Out ●</div>
    </div>
  </div>
</div>
