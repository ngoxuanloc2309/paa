# TP — Monostable Pulse Timer

<div class="doc-meta-bar">
  <span><strong>Block Code:</strong> TP</span>
  <span><strong>Category:</strong> Timing, Counting &amp; Scaling</span>
  <span><strong>Execution Class:</strong> Non-Retriggerable Monostable Pulse (IEC 61131-3 TP)</span>
  <span><strong>Lowering Target:</strong> Timer Macro / Fixed-Width Pulse Generator</span>
</div>

## 1. Synopsis & Execution Semantics

The **`TP`** (**Timer Pulse**) block is a **monostable, non-retriggerable one-shot pulse generator**. Upon detecting a rising edge on input `In` (`0 → 1`), `TP` immediately drives boolean output `Q` to `1` for an **exact, immutable duration `PresetMs`**, then returns `Q` to `0`.

- **Strict Non-Retriggerable Guarantee:** Once a pulse is active (`0 < ET < PresetMs`), `Q` stays locked at `1` regardless of what happens on `In`:
  - If `In` drops back to `0` after just `10 ms`, `Q` continues holding `1` until the full `PresetMs` elapses.
  - If `In` bounces `1 → 0 → 1` repeatedly during the active pulse, `ET` is **never reset or extended**—guaranteeing an exact pulse width with millisecond precision.
- **Re-Arm Requirement When `In` Stays High:** If `In` is still held at `1` when `ET` reaches `PresetMs`, `Q` drops cleanly to `0` anyway and will not fire a second pulse until `In` first returns to `0` and produces a fresh rising edge (`0 → 1`).
- **Primary Industrial Use Cases:** Volumetric solenoid dosing shots, pneumatic cylinder eject/reject kicks, baghouse dust-collector jet-pulse blowdown valves, horn chirp acknowledgments, and fixed-width reset strobes for external drives.

---

## 2. Pinout & Signal Contract

| Pin | Direction | Signal Kind | Data Type | Description |
| :--- | :--- | :--- | :--- | :--- |
| `In` | Input | Event / Execution (`IsEvent = true`) | Rising-Edge Trigger (`BOOL`) | Initiates a `PresetMs` pulse on rising transition `0 → 1` when idle; ignored while pulse is in flight. |
| `Q` | Output | Value / Data (`IsEvent = false`) | `BOOL` (`0` / `1`) | Fixed-duration pulse output bound to `OutputTag`; drives downstream [`ACTION`](/blocks/action), [`MQTT_PUB`](/blocks/mqtt-pub), [`HTTP_CLIENT`](/blocks/http-client), or [`TCP_CLIENT`](/blocks/tcp-client). |

---

## 3. Inspector Parameters & Validation Limits

| Parameter | Type / Options | Default | Constraints | Runtime Behavior |
| :--- | :--- | :--- | :--- | :--- |
| `DurationValueText` | Non-negative Float / Integer | `"3"` | Accepts dot or comma decimals (`"0.25"`, `"0,25"`). Clamps `PresetMs` to `[1 .. 4,294,967,295] ms`. | Multiplied by `TimeUnit` to establish the exact pulse width `PresetMs`. |
| `TimeUnit` | `ms`, `s`, `min`, `h` | `"s"` (`×1,000`) | Unit multipliers: `ms = 1`, `s = 1,000`, `min = 60,000`, `h = 3,600,000`. | Auto-formats loaded millisecond presets and displays `= 3,000 ms (3s)` verification text. |
| `PresetMs` | Unsigned 32-bit Integer (`uint`) | `3000` (`3,000 ms`) | `1 .. 4,294,967,295 ms` (~`49.7 days`). | Exact duration for which `Q` stays `1` on each valid trigger. |
| `InputTag` | Digital Tag (`DI`, `DO`, `VFLAG`) | Auto-bound from upstream wire | Must reference a digital boolean tag. | Sampled each scan cycle for rising-edge (`0 → 1`) arming and re-arming transitions. |
| `OutputTag` (`Q`) | Writable Digital Tag (`DO`, `VFLAG`) | Unassigned *(must be selected)* | Must reference a writable digital tag (`DO` or `VFLAG`). | Pulsed to `1` for `PresetMs`, then forced back to `0`. |
| `CustomLabel` | Free-text String | Empty (`"TP Timer"`) | Optional schematic title override. | Customizes the header title on the Logic Graph Canvas. |

---

## 4. Monostable Pulse Rules & Protection Guarantees

The table below highlights both immunity to mid-pulse input bounce (the `TP` block ignores `In` transitions while firing) and the safe lock-out state when an input remains stuck high (`In == 1`) beyond `PresetMs`:

| Scenario | `In` Behavior | `Q` Output Duration | Protection Guarantee |
| :--- | :--- | :--- | :--- |
| **Brief Trigger Pulse (`20 ms`)** | `0 → 1 → 0` rapidly | Exact `PresetMs` | Stretches short sensor strobes into reliable valve actuation |
| **Noisy Bouncing Input** | `0 → 1 → 0 → 1` mid-shot | Exact `PresetMs` | Prevents double-dosing or extended pulse stretching |
| **Stuck-High Sensor (`In = 1` forever)** | `0 → 1` (never drops) | Exact `PresetMs`, then `0` | Prevents runaway chemical overflow if a bottle jams on conveyor |

---

## 5. Input Defense & Live Diagnostics

### 5.1 Precision Sub-Second & Multi-Unit Input Defense

- **Millisecond & Fractional Second Accuracy:** Jet-pulse and reject-solenoid applications frequently require sub-second precision (`150 ms` or `0.25 s`). Selecting `ms` and typing `"150"` (or selecting `s` and typing `"0,25"`) locks `PresetMs` deterministically to `150 ms` or `250 ms` with live confirmation text (`= 250 ms (0.25s)`).
- **Zero / Malformed Input Protection:** Values of `0` clamp safely to `1 ms`; negative or non-numeric strings are rejected without corrupting `PresetMs`.

### 5.2 Canvas Badges in Simulator (`F5`) & Online Live Debug (`F8`)

- **Real-Time Shot Progress (`F5`):** Displays `Q = ON` and live millisecond progress (`ElapsedMs / PresetMs`) throughout the monostable shot, returning cleanly to `OFF` upon completion even if the simulated `INPUT` toggle remains `● 1`.
- **20 FPS Live Pulse Monitoring (`F8`):** Streams real-time `OutputTag` state onto the canvas node at 20 FPS (`Q = ON` active green highlight during shots) with `[STALE]` quality supervision.

---

## 6. Practical Industrial Wiring Example

### Scenario: Bottling Line Additive Dosing Valve (`750 ms` Exact Volumetric Shot)

As bottles pass a retro-reflective photo-eye (`DI0`) on a filling conveyor, a high-speed dispensing solenoid valve (`DO2`) must open for **exactly `750 ms`** to inject `15 mL` of flavor concentrate—only when the conveyor drive is running (`DI1 = 1`). Even if the conveyor stops with a bottle blocking `DI0`, `DO2` must close strictly after `750 ms` to prevent tank spillage.

### Tag Configuration

| Tag Name | Kind | Data Type | Description / Alias |
| :--- | :--- | :--- | :--- |
| `DI0` | `DI` | `BOOL` | `Bottle Present Photo-Eye (0 -> 1 on Bottle Leading Edge)` |
| `DI1` | `DI` | `BOOL` | `Conveyor Drive Run Status Interlock` |
| `DO2` | `DO` | `BOOL` | `Concentrate Dosing Solenoid Valve` |
| `VREG_R1` | `VREG_R` | `INT32` | `Total Dosed Bottles Counter` |

### Step-by-Step Canvas Wiring

1. Place **`INPUT`** (`Tag = DI0`) → **[`TRIGGER`](/blocks/trigger)** (`TriggerType = ON_RISE`, `ForMs = 15` ms optical anti-glare debounce) → **[`GUARD`](/blocks/guard)** (`GuardTag = DI1`, `Negate = false`).
2. Wire `GUARD.Out` → **`TP`** timer (`DurationValueText = "750"`, `TimeUnit = "ms"`, `OutputTag = DO2`, `CustomLabel = "750ms Dose Shot"`).
3. Wire `TP.Q` → **[`ACTION`](/blocks/action)** (`ActionType = INC_COUNTER`, `TargetTag = VREG_R1`, `ActionParam = 1`) to increment the dosed bottle totalizer on every completed shot.
4. Press `F5` to test: set `DI1 = 1`, toggle `DI0` `0 → 1` and **leave `DI0` at `1`** (simulating a jammed bottle). Verify `DO2` energizes for exactly `750 ms` and then drops to `0`, refusing to dose again until `DI0` returns to `0` and sees the next bottle.

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
        <span class="synx-node-tag">DI0 • BOOL</span>
        <span class="synx-node-alias">Photo Sensor</span>
        <span class="synx-node-val">Bottle Detection</span>
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
        <span class="synx-node-tag">15 ms Dwell</span>
        <span class="synx-node-alias">Debounce Filter</span>
        <span class="synx-node-val">Rising Lead Edge</span>
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
        <span class="synx-node-alias">Conveyor Permissive</span>
        <span class="synx-node-val">Belt In Motion</span>
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
        <span>TP TIMER</span>
      </div>
      <span class="synx-node-status is-active">● 750 ms</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">PT = 750 ms • DO2</span>
        <span class="synx-node-alias">Dosing Shot Pulse</span>
        <span class="synx-node-val">750ms Output Shot</span>
      </div>
      <div class="synx-node-pin is-out">Q ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">DONE</span>
  </div>

  <div class="synx-canvas-node is-action">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L4,8 M12,8 L14,8 M4,4.5 A3.5,3.5 0 1 1 4,11.5" /></svg>
        <span>ACTION</span>
      </div>
      <span class="synx-node-status is-active">● +1</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">INC VREG_R1</span>
        <span class="synx-node-alias">Totalizer Count</span>
        <span class="synx-node-val">Increment Completed</span>
      </div>
      <div class="synx-node-pin is-out" style="visibility: hidden;">Out ●</div>
    </div>
  </div>
</div>
