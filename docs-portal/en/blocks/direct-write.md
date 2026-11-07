# DIRECT_WRITE — Direct Hardware Terminal Driver

<div class="doc-meta-bar">
  <span><strong>Document ID:</strong> SYNX-DOC-BLK-DIRWRITE</span>
  <span><strong>Block Category:</strong> Fieldbus &amp; Hardware Actuation</span>
  <span><strong>Execution Class:</strong> Deterministic Hardware Timer / GPIO Peripheral Driver</span>
  <span><strong>Standard:</strong> IEC 61131-3 Digital, PWM &amp; PTO Actuation</span>
</div>

## 1. Synopsis & Execution Semantics

The **`DIRECT_WRITE`** (**Pulse & Hardware Terminal Output Driver**) function block binds directly to a single physical transistor/relay output terminal (**`DO0 .. DO7`**) on the SynaptiX edge controller, unlocking hardware timer peripherals beyond basic boolean coils.

A single `DIRECT_WRITE` instance can operate in **four hardware actuation modes**:
1. **Digital Output (`DigitalOutput` — DO `0/1`):** Drives 24 VDC relays, solenoid valves, contactors, and pilot lamps with configurable `IsActiveHigh` polarity and deterministic `FailSafeState` behavior on fault/stop.
2. **PWM Duty Cycle (`PwmDuty` — `0..100%`):** Generates hardware timer pulse-width modulation at `PwmFrequencyHz` with `[DutyMin .. DutyMax]` clamping for proportional valves, DC motor speed drives, heaters, and LED dimmers.
3. **Pulse Train Output (`PulseTrain` — PTO):** Dispatches deterministic high-speed step bursts (`Pulses` count at `Freq` Hz, bounded by `MaxFrequencyHz` and `PulseWidthUs`) for stepper and servo `STEP/DIR` motion indexers.
4. **Addressable Actuator Bus (`AddressableBus`):** Drives single-wire serial addressable RGB/actuator chains (`1..ChannelCount`) with optional `IsExclusiveChannel` one-hot selection and `DefaultColorRgb`.

---

## 2. Pinout & Signal Contract

`DIRECT_WRITE` dynamically reconfigures its left-hand input connectors (`SynchronizeConnectors`) for the active **`Mode` × `TriggerMode`** combination so engineers only see pins that physically apply:

### 2.1. Dynamic Input Connector Topology Matrix

| Operating Mode | `Continuous` or `OnChangeOfValue` (COV) | `OnTriggerRisingEdge` | Input Signal Semantics |
| :--- | :---: | :---: | :--- |
| **1. `DigitalOutput`** | `[In]`, `[En]` | `[Trig]`, `[In]`, `[En]` | `In` (`BOOL`): desired state (`0`/`1`) · `En` (`BOOL`): output enable gate · `Trig`: rising-edge latch trigger. |
| **2. `PwmDuty`** | `[Duty]` | `[Trig]`, `[Duty]` | `Duty` (`INT32`/`REAL`, `0..100%`): target PWM duty cycle clamped to `[DutyMin..DutyMax]`. (`Param` pin hidden). |
| **3. `PulseTrain` (PTO)** | `[Pulses]`, `[Freq]` | `[Trig]`, `[Pulses]`, `[Freq]` | `Pulses` (`INT32`): step count to emit · `Freq` (`INT32` Hz): step frequency clamped to `MaxFrequencyHz`. |
| **4. `AddressableBus`** | `[Chan]`, `[Param]` | `[Chan]`, `[Param]` | `Chan` (`1..ChannelCount`): target pixel/node · `Param` (`UINT32` RGB / state). (`Trig` pin hidden). |

### 2.2. Fixed Output Connectors

| Pin | Direction | Signal Kind | Data Type | Visibility | Functional Contract |
| :--- | :---: | :---: | :---: | :---: | :--- |
| **`Done`** | Output | Event (Pulse) | `BOOL` | Always visible | Pulses `1` when a triggered write commits or when a PTO pulse burst finishes outputting all requested steps. |
| **`Err`** | Output | Value | `BOOL` | Always visible | Asserts `1` if `TargetPin` exceeds the active device preset's physical DO count or if hardware peripheral contention occurs. |

---

## 3. Inspector Parameters & Constraints

Click **`[ Configure... ]`** (or double-click the node) to open the **Direct Hardware Output Dialog**:

### 3.1. Terminal & Trigger Settings

| Parameter | Property Key | Type & Domain | Default | Description & Hardware Guard |
| :--- | :--- | :--- | :---: | :--- |
| **Target Terminal** | `TargetPin` | `DO0 .. DO7` | `DO0` | Physical output terminal driven by this block. Validated dynamically against the active [Device Preset](/concepts/ecosystem-overview) DO count. |
| **Operating Mode** | `Mode` | `DigitalOutput`, `PwmDuty`, `PulseTrain`, `AddressableBus` | `DigitalOutput` | Selects the MCU GPIO / Hardware Timer peripheral personality for `TargetPin`. |
| **Trigger Mode** | `TriggerMode` | `Continuous`, `OnTriggerRisingEdge`, `OnChangeOfValue` | `OnTriggerRisingEdge` | `Continuous`: updates every scan · `OnTriggerRisingEdge`: updates on `Trig` `0 → 1` · `OnChangeOfValue`: updates when `Δ ≥ Deadband`. |
| **COV Deadband** | `Deadband` | `Float` (`≥ 0.0`) | `0.5` | Minimum input delta required to update hardware in `OnChangeOfValue` mode (visible only when `IsCovMode = true`). |

### 3.2. Mode-Specific Hardware Parameters

| Mode | Parameter | Property Key | Default | Constraint / Physical Meaning |
| :--- | :--- | :--- | :---: | :--- |
| **1. `DigitalOutput`** | **Active Polarity** | `IsActiveHigh` | `true` | `true`: Active-High (`1 = 24V ON, 0 = 0V OFF`) · `false`: Active-Low (`0 = 24V ON, 1 = 0V OFF`). |
| **1. `DigitalOutput`** | **Fail-Safe State** | `FailSafeState` | `0` | `0`: De-energize (`OFF`) on stop/fault · `1`: Energize (`ON`) on stop/fault (e.g., normally-energized cooling valve). |
| **2. `PwmDuty`** | **PWM Frequency** | `PwmFrequencyHz` | `1000` Hz | Carrier frequency in Hz (use high-speed transistor outputs rather than mechanical relays!). |
| **2. `PwmDuty`** | **Duty Limits** | `DutyMin` .. `DutyMax` | `0` .. `100` % | Hardware saturation floor and ceiling (`0%..100%`). |
| **2. `PwmDuty`** | **Initial Duty** | `InitialDuty` | `0` % | Startup duty cycle applied upon controller boot before first trigger. |
| **3. `PulseTrain`** | **Pulse Width** | `PulseWidthUs` | `50` µs | High-pulse duration in microseconds (match stepper optocoupler minimum width, typically `5..100 µs`). |
| **3. `PulseTrain`** | **Max Frequency** | `MaxFrequencyHz` | `20000` Hz | Safety speed ceiling (`Hz`) protecting stepper mechanics from stall overspeed. |
| **4. `AddressableBus`** | **Channel Count** | `ChannelCount` | `18` | Number of daisy-chained addressable actuator/LED nodes on the wire. |
| **4. `AddressableBus`** | **Exclusive Channel** | `IsExclusiveChannel` | `true` | `true`: One-hot illumination (only active `Chan` stays lit) · `false`: Independent multi-node states. |
| **4. `AddressableBus`** | **Default Color RGB** | `DefaultColorRgb` | `0x00FF00` | Fallback 24-bit RGB hex value (`#00FF00` emerald green) when `Param` is `0`. |

::: danger Mechanical Relay Wear Warning in PWM & PTO Modes
Never configure `PwmDuty` or high-frequency `PulseTrain` on electromechanical relay outputs — rapid switching will weld relay contacts within minutes. Always select solid-state NPN/PNP transistor `DO` terminals as specified in your hardware preset datasheet.
:::

---

## 4. Hardware Driving & Trigger Modes

The `DIRECT_WRITE` block interfaces directly with microcontroller peripherals based on its configured mode:
- **Digital Output (`DigitalOutput`):** Drives physical output pins `DO0..DO7` with selectable polarity (`Active-High` or `Active-Low`) and configurable Fail-Safe states upon stop/fault.
- **PWM Speed Control (`PwmDuty`):** Generates high-frequency PWM waves at `PwmFrequencyHz` with duty cycles safely bounded within `[DutyMin .. DutyMax]%`.
- **Pulse Train Output (`PulseTrain`):** Generates high-speed pulse bursts for stepper/servo drivers bounded below `MaxFrequencyHz`.
- **Addressable Bus (`AddressableBus`):** Serial bitstream multiplexing for multi-channel indicator arrays or actuator banks.

---

## 5. Validation, Error Handling & Live Diagnostics

- **Out-of-Range Hardware Pin Check (`(!)`):** If you switch a project from an 8-DO controller preset down to a 4-DO gateway preset (`DO0..DO3`), any `DIRECT_WRITE` assigned to `DO4..DO7` immediately flags `IsHardwarePinOutOfRange = true`, displays **`PIN: DO4 (!)`** on the node header, turns the status pill to **`ERR: PIN EXCEEDED`**, and blocks unsafe deployment in [Atomic Deploy](/workspaces/deploy-tab).
- **Duplicate Terminal Conflict Check:** The compiler verifies that two `DIRECT_WRITE` blocks do not simultaneously claim conflicting timer modes (e.g., `PwmDuty` and `DigitalOutput`) on the same `DOx` pin.
- **Interactive Signal Breakdown Preview:** The configuration modal includes a live simulator where you can test `SimDoValue`, `SimPwmDuty`, `SimPtoPulses`/`SimPtoFreq`, or `SimBusChannel` and inspect the generated hardware timing parameters prior to flashing.

---

## 6. Practical Industrial Wiring Example

### Proportional Cooling Fan PWM Control (`SCALE` → `DIRECT_WRITE` PWM on `DO0`)

**Objective:** Convert cabinet temperature (`25.0 °C .. 65.0 °C`) scaled by [`SCALE`](/blocks/scale) into a `20% .. 100%` `1000 Hz` PWM signal on solid-state output `DO0`, updating only when duty changes by at least `1.0%` (`OnChangeOfValue`).

<div class="synx-node-chain" style="margin-bottom: 12px;">
  <div class="synx-canvas-node is-input">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L12,8 M8,4 L12,8 L8,12" /></svg>
        <span>INPUT</span>
      </div>
      <span class="synx-node-status is-live">● AI1</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in" style="visibility: hidden;">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">NTC Thermistor</span>
        <span class="synx-node-alias">Cabinet Temperature</span>
        <span class="synx-node-val">AI1 • INT32</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">Raw ADC</span>
  </div>

  <div class="synx-canvas-node is-timer">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,13 L5,13 L5,9.5 L8,9.5 L8,6 L11,6 L11,3 L14,3" /></svg>
        <span>SCALE</span>
      </div>
      <span class="synx-node-status is-active">● 25..65°C</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Duty % Scaling</span>
        <span class="synx-node-alias">Fan Modulation</span>
        <span class="synx-node-val">20.0 .. 100.0 %</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">Duty %</span>
  </div>

  <div class="synx-canvas-node is-direct">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L4,8 M12,8 L14,8 M4,4.5 A3.5,3.5 0 1 1 4,11.5" /></svg>
        <span>DIRECT_WRITE</span>
      </div>
      <span class="synx-node-status is-active">● 1000Hz PWM</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● Duty</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Solid-State Output</span>
        <span class="synx-node-alias">Cooling Fan PWM</span>
        <span class="synx-node-val">DO0 • PwmDuty</span>
      </div>
      <div class="synx-node-pin is-out" style="visibility: hidden;">Out ●</div>
    </div>
  </div>
</div>

**Configuration Steps:**
1. Configure [`SCALE`](/blocks/scale) using 2-Point Calibration (`X1 = 250, Y1 = 20`; `X2 = 650, Y2 = 100`, `IsClamped = true`) so output spans `20.0 .. 100.0 %`.
2. Add **`DIRECT_WRITE`** onto the canvas and open its configuration dialog:
   - `TargetPin = DO0`
   - `Mode = PwmDuty`
   - `PwmFrequencyHz = 1000`, `DutyMin = 20`, `DutyMax = 100`, `InitialDuty = 25`
   - `TriggerMode = OnChangeOfValue`, `Deadband = 1.0`
3. Notice the input connectors automatically simplify to a single **`Duty`** pin; wire `SCALE.OUT` directly to **`Duty`**.
