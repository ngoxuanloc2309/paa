# MATH — Arithmetic ALU Operations

<div class="doc-meta-bar">
  <span><strong>Block Code:</strong> MATH</span>
  <span><strong>Category:</strong> Timing, Counting &amp; Scaling</span>
  <span><strong>Execution Class:</strong> Combinational Arithmetic ALU (IEC 61131-3)</span>
  <span><strong>Standard:</strong> IEC 61131-3 ANY_NUM Operations</span>
</div>

## 1. Synopsis & Execution Semantics

The `MATH` (**Arithmetic Logic Unit**) function block evaluates mathematical operations across numeric operands (`INT32` and `REAL` / `Float32`) and writes the computed result to a designated destination tag (`OUT`).

- **IEC 61131-3 Arithmetic Operators:** Supports fundamental mathematical operations: Addition (`ADD`), Subtraction (`SUB`), Multiplication (`MUL`), Division (`DIV`), and Modulo Remainder (`MOD`).
- **Extensible Operand Chaining ($2 \dots 8$ Pins):** While conventional PLC math blocks often restrict operations to two inputs, `MATH` allows chaining up to 8 operands for Addition, Subtraction, and Multiplication (evaluated left-to-right: $A - B - C - \dots$). Division and Modulo strictly enforce 2 operands per industrial safety standards.
- **Dynamic Type Promotion (`ANY_NUM`):** When set to `Automatic (Auto)`, the block automatically promotes the calculation to IEEE-754 `REAL` if any operand is floating-point; otherwise, it computes in signed 32-bit integer arithmetic (`INT32`).
- **Division-by-Zero Protection:** In event of a zero divisor ($x / 0$), the block suppresses runtime crashes, retains the last valid output state, and raises a diagnostic fault indicator.
- **Interactive Live Simulation:** The configuration dialog includes a real-time ALU diagnostics test bench where engineers can inject sample values and verify formulas before commissioning to hardware.

---

## 2. Pinout & Signal Contract

`MATH` operates combinatorially on every scan cycle or when connected to upstream data sources:

| Pin | Direction | Signal Kind | Data Type | Description |
| :--- | :--- | :--- | :--- | :--- |
| `IN1` .. `IN8` | Input | Value / Data (`IsEvent = false`) | `INT32` \| `REAL` | Operands for the arithmetic operation. Can be bound to tags (`AI`, `VREG`, `COUNTER`) or configured as fixed numerical constants. |
| `Out` | Output | Value / Data (`IsEvent = false`) | `INT32` \| `REAL` | Evaluated mathematical result. Emits the calculated value to downstream blocks ([`COMPARATOR`](/blocks/comparator), [`SCALE`](/blocks/scale), [`ACTION`](/blocks/action)) or updates the bound `OutputTag`. |

---

## 3. Inspector Parameters & Configuration

| Parameter | Type / Domain | Default | Description & Operational Behavior |
| :--- | :--- | :---: | :--- |
| Arithmetic Operator | `ADD`, `SUB`, `MUL`, `DIV`, `MOD` | `ADD` (`+`) | Selects the arithmetic function evaluated by the block. |
| Data Type Mode | `Auto`, `INT32`, `REAL` | `Auto` | Selects computation data type. In `Auto`, promotes to `REAL` if any operand is floating-point. |
| Output Result Tag (`OUT`) | `TagModel?` (`VREG`) | `VREG0` | Destination virtual register storing the computation result. |
| Input Operands List | $2 \dots 8$ Pins | 2 Pins (`IN1`, `IN2`) | Dynamic list of input operands. Each pin can be independently assigned to a Tag Variable or a Constant scalar value. |

---

## 4. Supported Operators & Industrial Arithmetic Rules

### 4.1 Operator Specifications

| Operator | Symbol | Allowable Pins | Algebraic Formula (Left-to-Right) | Industrial Behavior |
| :--- | :---: | :---: | :--- | :--- |
| Addition (`ADD`) | `+` | $2 \dots 8$ | $OUT = IN_1 + IN_2 + \dots + IN_n$ | Summation of multiple process variables, offsets, or sensor readings. |
| Subtraction (`SUB`) | `-` | $2 \dots 8$ | $OUT = IN_1 - IN_2 - \dots - IN_n$ | Evaluates sequential difference from left to right (e.g., differential pressure, gross-to-tare deduction). |
| Multiplication (`MUL`) | `*` | $2 \dots 8$ | $OUT = IN_1 \times IN_2 \times \dots \times IN_n$ | Multi-factor product (e.g., volumetric rate $\times$ density $\times$ correction factor). |
| Division (`DIV`) | `/` | Exactly 2 | $OUT = IN_1 / IN_2$ | Quotients. Division by zero ($IN_2 = 0$) prevents CPU halt and flags a runtime fault. |
| Modulo (`MOD`) | `%` | Exactly 2 | $OUT = IN_1 \pmod{IN_2}$ | Remainder of integer division (e.g., circular queue indexing, step distribution). |

### 4.2 IEC 61131-3 Type Promotion Rules

In `Automatic (Auto)` mode, data types are resolved according to standard industrial rules:

| Operand Types Present | Target Evaluation Mode | Result Data Type | Notes |
| :--- | :---: | :---: | :--- |
| All operands are `INT32` (or integer constants) | `INT32` | `INT32` | Truncates fractional division remainders towards zero. |
| At least one operand is `REAL` (or float constant) | `REAL` | `REAL` (`Float32`) | Automatic promotion to IEEE-754 single precision floating-point. |
| Explicit `INT32` mode selected | `INT32` | `INT32` | Forces integer truncation regardless of operand types. |
| Explicit `REAL` mode selected | `REAL` | `REAL` (`Float32`) | Converts all integer operands to IEEE-754 floats before calculation. |

---

## 5. Dedicated Configuration Dialog (`MathConfigDialogView`)

Clicking `Configure Block...` in the Inspector or double-clicking the canvas node opens the comprehensive, industrial-grade Fluent 2 Desktop configuration dialog:

![MATH Function Block Configuration Dialog in SynaptiX IDE](/images/synaptix_math_config_dialog.png)

### 5.1 Two-Column Master-Detail Architecture

The dialog divides engineering parameters into two dedicated operational panels:
- **Left Column (Master Settings & Input Operands Table):**
  - **Group 1 — General Parameters:** Selects arithmetic function (`ADD`, `SUB`, `MUL`, `DIV`, `MOD`), data type promotion mode (`Auto`, `INT32`, `REAL`), and destination output register (`OUT`).
  - **Group 2 — Operands Table:** Supports chaining 2 to 8 input pins. Each row allows selecting between **Tag Variable** (bound to Tag Catalog) or **Constant** scalar input. Engineers can dynamically click `+ Add Operand` or remove pins as needed.
- **Right Column (Sticky ALU Diagnostics & Live Simulation Test Bench):**
  - **Algebraic Formula Preview:** Real-time synthesis of the active mathematical formula (e.g., `VREG10 = IN1 + IN2 + IN3`).
  - **Interactive Simulation Inputs:** Allows typing trial numeric values into each operand slot without overwriting physical PLC registers.
  - **ALU Output Result & Safety Badge:** Instant evaluation displaying calculated value, inferred type (`REAL` or `INT32`), and operational status (`✓ VALID` or `FAULT: DIV_BY_ZERO`).

---

## 6. Practical Industrial Examples & Wiring Schematics

### 6.1. Example 1: Multi-Sensor Total Feedwater Flow Summation & Over-Capacity Trip (`ADD`)

**Objective:**
A boiler feedwater header receives flow from three supply lines measured by analog flow transmitters (`AI0`, `AI1`, and `AI2` in $\text{m}^3/\text{h}$, calibrated to `REAL` in virtual registers `VREG0_Line1Flow`, `VREG1_Line2Flow`, and `VREG2_Line3Flow`).
The `MATH` block evaluates a 3-pin summation to compute the total feedwater flow into `VREG10_TotalFlow`:

$$\text{Total Feedwater Flow (VREG10)} = \text{VREG0} + \text{VREG1} + \text{VREG2}$$

When total header flow exceeds the safe design threshold ($> 80.0\text{ m}^3/\text{h}$), the downstream `COMPARATOR` block energizes high-alarm annunciator `DO0_ALARM_HIGH`.

#### Logic Graph Wiring Schematic:

<div class="synx-node-chain" style="margin-bottom: 12px;">
  <div class="synx-canvas-node is-input">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L12,8 M8,4 L12,8 L8,12" /></svg>
        <span>INPUT (3x)</span>
      </div>
      <span class="synx-node-status is-live">● VREG0..2</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in" style="visibility: hidden;">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">3 Supply Lines</span>
        <span class="synx-node-alias">Flow Transmitters</span>
        <span class="synx-node-val">25.5 + 30.2 + 14.3</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">Flow m³/h</span>
  </div>

  <div class="synx-canvas-node is-timer">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M4,7 L12,7 M8,3 L8,11 M14,14 L18,18 M18,14 L14,18 M4,16 L10,16 M4,18 L10,18" /></svg>
        <span>MATH</span>
      </div>
      <span class="synx-node-status is-active">● ADD (+)</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● IN1..3</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">3-Pin Summation</span>
        <span class="synx-node-alias">VREG10_TotalFlow</span>
        <span class="synx-node-val">70.0 m³/h (REAL)</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">70.0 m³/h</span>
  </div>

  <div class="synx-canvas-node is-comparator">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M3.5,6 L8,9 L3.5,12 M9.5,7 L13,7 M9.5,10.5 L13,10.5" /></svg>
        <span>COMPARATOR</span>
      </div>
      <span class="synx-node-status is-true">● RELATIONAL</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Over-Capacity Limit</span>
        <span class="synx-node-alias">Header High Alarm</span>
        <span class="synx-node-val">&gt; 80.0 m³/h</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">ALARM</span>
  </div>

  <div class="synx-canvas-node is-direct">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L4,8 M12,8 L14,8 M4,4.5 A3.5,3.5 0 1 1 4,11.5" /></svg>
        <span>ACTION</span>
      </div>
      <span class="synx-node-status is-active">● ACTUATE</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Alarm Horn Relay</span>
        <span class="synx-node-alias">Over-Capacity Beacon</span>
        <span class="synx-node-val">DO0 = 1</span>
      </div>
      <div class="synx-node-pin is-out" style="visibility: hidden;">Out ●</div>
    </div>
  </div>
</div>

#### Block Parameter Configuration:

| Configuration Property | Engineering Value | Description |
| :--- | :--- | :--- |
| Arithmetic Operator | `ADD` (`+`) | Sums volumetric flow rates across all 3 supply branches. |
| Data Type Mode | `Auto (ANY_NUM)` | Automatically promotes to `REAL` floating-point precision. |
| Output Destination (OUT) | `VREG10_TotalFlow` | Virtual register recording total flow for SCADA logging and alarms. |
| Operand Pin IN1 | `VREG0_Line1Flow` | Instantaneous line 1 flow reading ($25.5\text{ m}^3/\text{h}$). |
| Operand Pin IN2 | `VREG1_Line2Flow` | Instantaneous line 2 flow reading ($30.2\text{ m}^3/\text{h}$). |
| Operand Pin IN3 | `VREG2_Line3Flow` | Instantaneous line 3 flow reading ($14.3\text{ m}^3/\text{h}$). |

#### Industrial Commissioning & Truth Matrix:

| Operational State | Line 1 (`VREG0`) | Line 2 (`VREG1`) | Line 3 (`VREG2`) | Total ALU (`VREG10`) | Relational Trip (`> 80.0`) | Alarm Horn (`DO0`) |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: |
| Nominal Production | $25.5\text{ m}^3/\text{h}$ | $30.2\text{ m}^3/\text{h}$ | $14.3\text{ m}^3/\text{h}$ | $70.0\text{ m}^3/\text{h}$ | `FALSE` (Within safe bounds) | `0` (Alarm OFF) |
| Single Line Maintenance | $0.0\text{ m}^3/\text{h}$ | $35.0\text{ m}^3/\text{h}$ | $20.0\text{ m}^3/\text{h}$ | $55.0\text{ m}^3/\text{h}$ | `FALSE` (Within safe bounds) | `0` (Alarm OFF) |
| Peak Surge Exceeded | $32.0\text{ m}^3/\text{h}$ | $38.5\text{ m}^3/\text{h}$ | $15.5\text{ m}^3/\text{h}$ | $86.0\text{ m}^3/\text{h}$ | `TRUE` (Over-Capacity Trip) | `1` (Alarm Energized) |

---

### 6.2. Example 2: Automatic Silo Bagging Tare Deduction & Cutoff (`SUB`)

**Objective:**
An automated bulk bagging station reads gross hopper weight via an analog loadcell on `AI0` ($0 \dots 100\text{ kg}$). When an empty packaging bag is clamped into position, the tare weight is captured into `VREG1_TareWeight` ($2.5\text{ kg}$).
The `MATH` block performs algebraic subtraction to derive the net material weight:

$$\text{Net Weight (VREG2\_NetWeight)} = \text{AI0\_GrossWeight} - \text{VREG1\_TareWeight}$$

When net material weight reaches target capacity ($50.0\text{ kg}$), the downstream `COMPARATOR` de-energizes feed valve `DO1_VALVE_CLOSE`.

#### Logic Graph Wiring Schematic:

<div class="synx-node-chain" style="margin-bottom: 12px;">
  <div class="synx-canvas-node is-input">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L12,8 M8,4 L12,8 L8,12" /></svg>
        <span>INPUT</span>
      </div>
      <span class="synx-node-status is-live">● AI0 / VREG1</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in" style="visibility: hidden;">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Loadcell Sensors</span>
        <span class="synx-node-alias">Gross &amp; Tare Registers</span>
        <span class="synx-node-val">Gross: 52.5 kg, Tare: 2.5 kg</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">Weight (kg)</span>
  </div>

  <div class="synx-canvas-node is-timer">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M4,7 L12,7 M8,3 L8,11 M14,14 L18,18 M18,14 L14,18 M4,16 L10,16 M4,18 L10,18" /></svg>
        <span>MATH</span>
      </div>
      <span class="synx-node-status is-active">● SUB (-)</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● IN1, IN2</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Tare Deduction</span>
        <span class="synx-node-alias">VREG2_NetWeight</span>
        <span class="synx-node-val">50.0 kg (Net)</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">50.0 kg</span>
  </div>

  <div class="synx-canvas-node is-comparator">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M3.5,6 L8,9 L3.5,12 M9.5,7 L13,7 M9.5,10.5 L13,10.5" /></svg>
        <span>COMPARATOR</span>
      </div>
      <span class="synx-node-status is-true">● TARGET REACHED</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Net Batch Threshold</span>
        <span class="synx-node-alias">Bag Target Limit</span>
        <span class="synx-node-val">&gt;= 50.0 kg</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">CUTOFF</span>
  </div>

  <div class="synx-canvas-node is-direct">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L4,8 M12,8 L14,8 M4,4.5 A3.5,3.5 0 1 1 4,11.5" /></svg>
        <span>ACTION</span>
      </div>
      <span class="synx-node-status is-active">● CLOSE VALVE</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Feeder Gate Relay</span>
        <span class="synx-node-alias">Material Cutoff</span>
        <span class="synx-node-val">DO1 = 0 (Closed)</span>
      </div>
      <div class="synx-node-pin is-out" style="visibility: hidden;">Out ●</div>
    </div>
  </div>
</div>

#### Block Configuration:
- **Operator:** `SUB (-)` (Sequential Subtraction).
- **Operand IN1:** `AI0_GrossWeight` (Current weight read from loadcell amplifier).
- **Operand IN2:** `VREG1_TareWeight` (Weight of empty packaging container captured at clamp).
- **Output Destination OUT:** `VREG2_NetWeight` (Computed net payload weight).
- **Zero-Floor Protection:** If a bag is prematurely unclamped (`Gross < Tare`), output could temporarily read negative. Pair with a [`SCALE`](/blocks/scale) block clamp (`ClampMin = 0.0`) to prevent negative weight readouts on operator HMI screens.