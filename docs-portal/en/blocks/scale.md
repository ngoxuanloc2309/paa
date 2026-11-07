# SCALE — Linear Calibration & Clamping

<div class="doc-meta-bar">
  <span><strong>Document ID:</strong> SYNX-DOC-BLK-SCALE</span>
  <span><strong>Block Category:</strong> Timing, Counting &amp; Scaling</span>
  <span><strong>Execution Class:</strong> Continuous Analog Transfer Function</span>
  <span><strong>Standard:</strong> IEC 61131-3 Signal Conditioning</span>
</div>

## 1. Synopsis & Execution Semantics

The **`SCALE`** (**Analog Linear Scaler & Calibrator**) function block transforms raw integer hardware readings (such as `0–10,000 mV`, `4–20 mA` loop counts, `0–4095` 12-bit ADC codes, or unscaled Modbus registers) into calibrated IEEE-754 floating-point engineering units (`bar`, `°C`, `m³/h`, `RPM`, `%`).

Every scan cycle, `SCALE` evaluates the standard industrial linear transfer equation followed by optional saturation clamping:

$$y_{\text{raw}} = \text{Gain} \cdot x + \text{Offset}$$

$$y_{\text{out}} = \begin{cases} \min(\max(y_{\text{raw}}, \text{ClampMin}), \text{ClampMax}) & \text{if } \text{IsClamped} = \text{true} \\ y_{\text{raw}} & \text{if } \text{IsClamped} = \text{false} \end{cases}$$

To eliminate error-prone hand calculations during field commissioning, the Inspector includes a **2-Point Calibration Calculator** that automatically derives `Gain`, `Offset`, `ClampMin`, and `ClampMax` from two known physical reference points `(CalibRaw1, CalibEng1)` and `(CalibRaw2, CalibEng2)`.

---

## 2. Pinout & Signal Contract

`SCALE` operates continuously on every scan without requiring an event trigger pin:

| Pin | Direction | Signal Kind | Data Type | Visibility | Functional Contract |
| :--- | :---: | :---: | :---: | :---: | :--- |
| **`IN`** | Input | Value (`x`) | `INT32` / `REAL` | Always visible | **Raw Signal Input:** Receives raw transducer counts from an upstream `INPUT` / `MB_READ` wire or directly from `InputTag` (`AI0..AI3`, `VREG`). |
| **`OUT`** | Output | Value (`y`) | `REAL` (`FLOAT32`) | Always visible | **Scaled Engineering Output:** Emits the calibrated, optionally clamped engineering value to downstream [`COMPARATOR`](/blocks/comparator), [`MQTT_PUB`](/blocks/mqtt-pub), or `OutputTag`. |

---

## 3. Inspector Parameters & Constraints

### 3.1. Primary Scaling & Clamping Properties

| Parameter | Property Key | Type & Domain | Default | Description & Physical Contract |
| :--- | :--- | :--- | :---: | :--- |
| **Input Tag** | `InputTag` | `TagModel?` | `null` | Source analog/integer tag (`AI0..AI3` or `VREG`). Displays `🔗 Connected: <Tag>` badge when linked. |
| **Output Tag** | `OutputTag` | `TagModel?` (`REAL`) | `null` | Destination `FLOAT32` virtual register (`VREG`) storing the calibrated engineering result. |
| **Gain (Slope)** | `Gain` | `Double` (`REAL`) | `0.01` | Linear multiplication factor applied to raw input `x`. |
| **Offset (Intercept)** | `Offset` | `Double` (`REAL`) | `0.0` | Zero-point offset added after multiplying by `Gain`. Supports negative offsets (e.g., `-2.5` for `4–20 mA` zero elevation). |
| **Engineering Unit** | `Unit` | `String` | `"bar"` | Physical unit label (`bar`, `°C`, `L/min`, `%`, `kPa`) rendered on the canvas pill and Live Watch. |
| **Decimal Places** | `DecimalPlaces` | `INT32` (`0..3`) | `1` | Visual formatting precision (`0` to `3` fractional digits) on the canvas status badge. |
| **Enable Clamping** | `IsClamped` | `BOOL` | `true` | Restricts `OUT` strictly within `[ClampMin .. ClampMax]` to prevent wire-break underflow spikes from corrupting control logic. |
| **Clamp Minimum** | `ClampMin` | `Double` (`REAL`) | `0.0` | Lower saturation floor when `IsClamped = true`. |
| **Clamp Maximum** | `ClampMax` | `Double` (`REAL`) | `100.0` | Upper saturation ceiling when `IsClamped = true`. |

### 3.2. Two-Point Calibration Calculator (`CalculateFromCalibPoints`)

Instead of solving simultaneous linear equations manually, enter two empirical sensor points in the **2-Point Calibration** section and click **Calculate Gain & Offset**:

| Calibration Input | ViewModel Field | Default | Meaning |
| :--- | :--- | :---: | :--- |
| **Point 1 — Raw (`X1`)** | `CalibRaw1` (`CalibX1`) | `0` | Raw ADC / mV / register reading at low reference point (e.g., `2000` mV at `4 mA`). |
| **Point 1 — Eng (`Y1`)** | `CalibEng1` (`CalibY1`) | `0` | True engineering value at Point 1 (e.g., `0.0` bar). |
| **Point 2 — Raw (`X2`)** | `CalibRaw2` (`CalibX2`) | `10000` | Raw ADC / mV / register reading at full-scale reference point (e.g., `10000` mV at `20 mA`). |
| **Point 2 — Eng (`Y2`)** | `CalibEng2` (`CalibY2`) | `100` | True engineering value at Point 2 (e.g., `16.0` bar). |

Upon clicking **Calculate**, the IDE computes:

$$\text{Gain} = \text{round}\!\left(\frac{Y_2 - Y_1}{X_2 - X_1},\; 6\right), \qquad \text{Offset} = \text{round}\!\left(Y_1 - \text{Gain} \cdot X_1,\; 6\right)$$

and automatically sets `ClampMin = min(Y1, Y2)`, `ClampMax = max(Y1, Y2)`, and `IsClamped = true`.

### Standard Industrial Transducer Reference Table

| Sensor Standard | Raw Input Range (`X1 .. X2`) | Target Range (`Y1 .. Y2`) | Computed `Gain` | Computed `Offset` |
| :--- | :---: | :---: | :---: | :---: |
| **`0–10 V` Pressure (`0..10 bar`)** | `0 .. 10000` (mV) | `0.0 .. 10.0` bar | `0.001` | `0.0` |
| **`4–20 mA` Pressure (`0..16 bar` via `500 Ω`)** | `2000 .. 10000` (mV) | `0.0 .. 16.0` bar | `0.002` | `-4.0` |
| **`4–20 mA` PT100 Temp (`-50..+150 °C`)** | `4000 .. 20000` (µA) | `-50.0 .. 150.0` °C | `0.0125` | `-100.0` |
| **`12-Bit ADC` Level (`0..4095` → `0..100 %`)** | `0 .. 4095` (counts) | `0.0 .. 100.0` % | `0.02442` | `0.0` |

---

## 4. Signal Conditioning & Clamping Rules

The `SCALE` block computes its calibrated output across two deterministic stages:
1. **Linear Transformation:** $y = \text{Gain} \cdot x + \text{Offset}$ (where $x$ is the raw input from `InputTag` or pin `In`).
2. **Safe Clamping (`IsClamped = true`):** If the computed value falls outside `[ClampMin .. ClampMax]`, it is automatically clamped to the nearest boundary, protecting downstream logic from broken sensor cables or ADC outliers.

---

## 5. Validation, Error Handling & Live Diagnostics

- **Zero-Span Division Guard:** In the 2-Point Calibration Calculator, if `|CalibRaw2 - CalibRaw1| < 1e-6` (identical raw points), the calculator safely aborts without overwriting `Gain` or producing `NaN` / `Infinity`.
- **Inverted Clamp Order Safety:** Even if an operator enters `ClampMin > ClampMax` for a reverse-acting sensor, the runtime normalizes `min = Math.Min(ClampMin, ClampMax)` and `max = Math.Max(ClampMin, ClampMax)` deterministically.
- **Type-Safe Target Binding:** Always bind `OutputTag` to a **`REAL` (`FLOAT32`)** virtual register in [Tag Catalog](/workspaces/tag-catalog-tab) so fractional precision (e.g., `6.45 bar`) is preserved in physical memory without integer truncation.
- **Live Canvas Progress Bar (`F8`):** Displays both the live formula (`y = 0.002·AI0 - 4`) when idle and the real-time scaled readout with span percentage **`6.4 bar (40%)`** when online.

---

## 6. Practical Industrial Wiring Example

### `4–20 mA` Booster Pump Discharge Pressure Control (`0.0 .. 16.0 bar`)

**Objective:** Read a `4–20 mA` pressure transmitter wired across a precision `500 Ω` shunt on `AI0` (`2000 mV` at `0 bar`, `10000 mV` at `16 bar`), clamp broken-wire `0 mV` readings safely at `0.0 bar`, store the calibrated pressure in `VREG0_PipeBar` (`REAL`), and trip high-pressure cutoff above `12.5 bar`.

<div class="synx-node-chain" style="margin-bottom: 12px;">
  <div class="synx-canvas-node is-input">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L12,8 M8,4 L12,8 L8,12" /></svg>
        <span>INPUT</span>
      </div>
      <span class="synx-node-status is-live">● AI0</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in" style="visibility: hidden;">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">4-20mA Sensor</span>
        <span class="synx-node-alias">Raw Voltage AI0</span>
        <span class="synx-node-val">2000..10000 mV</span>
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
      <span class="synx-node-status is-active">● CALIBRATED</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Linear Scaling</span>
        <span class="synx-node-alias">Register VREG0</span>
        <span class="synx-node-val">0.0 .. 16.0 bar</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">13.1 bar</span>
  </div>

  <div class="synx-canvas-node is-comparator">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M3.5,6 L8,9 L3.5,12 M9.5,7 L13,7 M9.5,10.5 L13,10.5" /></svg>
        <span>COMPARATOR</span>
      </div>
      <span class="synx-node-status is-true">● TRIP HIGH</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Over-Pressure Trip</span>
        <span class="synx-node-alias">Limit Threshold</span>
        <span class="synx-node-val">&gt; 12.5 bar</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">TRIP</span>
  </div>

  <div class="synx-canvas-node is-direct">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L4,8 M12,8 L14,8 M4,4.5 A3.5,3.5 0 1 1 4,11.5" /></svg>
        <span>DIRECT_WRITE</span>
      </div>
      <span class="synx-node-status is-active">● SHUTDOWN</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Trip Pump Relay</span>
        <span class="synx-node-alias">Safety Cutoff</span>
        <span class="synx-node-val">DO0 = 0</span>
      </div>
      <div class="synx-node-pin is-out" style="visibility: hidden;">Out ●</div>
    </div>
  </div>
</div>

**Configuration Steps:**
1. Create `VREG0_PipeBar` in [Tag Catalog](/workspaces/tag-catalog-tab) with Data Type = **`REAL` (`FLOAT32`)**.
2. Drop a **`SCALE`** block onto the canvas, open the **2-Point Calibration** tool in Inspector, and enter:
   - `CalibRaw1` = `2000`, `CalibEng1` = `0`
   - `CalibRaw2` = `10000`, `CalibEng2` = `16`
   - Click **Calculate** → `Gain` becomes `0.002`, `Offset` becomes `-4`, `ClampMin` = `0`, `ClampMax` = `16`, `IsClamped` = `true`.
3. Set `Unit` = `"bar"`, `DecimalPlaces` = `2`, `InputTag` = `AI0`, and `OutputTag` = `VREG0_PipeBar`.
4. Wire **`OUT`** to [`COMPARATOR`](/blocks/comparator) (`> 12.5`) and [`MQTT_PUB`](/blocks/mqtt-pub) (`COV`, `Deadband = 0.2`).
