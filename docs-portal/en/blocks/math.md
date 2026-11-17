# MATH — Arithmetic ALU Operations

<div class="doc-meta-bar">
  <span><strong>Block Code:</strong> MATH</span>
  <span><strong>Category:</strong> Timing, Counting &amp; Scaling</span>
  <span><strong>Execution Class:</strong> Combinational Arithmetic ALU (IEC 61131-3)</span>
  <span><strong>Standard:</strong> IEC 61131-3 ANY_NUM Operations</span>
</div>

## 1. Synopsis & Execution Semantics

The **`MATH`** (**Arithmetic Logic Unit**) function block evaluates mathematical operations across numeric operands (`INT32` and `REAL` / `Float32`) and writes the computed result to a designated destination tag (`OUT`).

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
| **Arithmetic Operator** | `ADD`, `SUB`, `MUL`, `DIV`, `MOD` | `ADD` (`+`) | Selects the arithmetic function evaluated by the block. |
| **Data Type Mode** | `Auto`, `INT32`, `REAL` | `Auto` | Selects computation data type. In `Auto`, promotes to `REAL` if any operand is floating-point. |
| **Output Result Tag (`OUT`)** | `TagModel?` (`VREG`) | `VREG0` | Destination virtual register storing the computation result. |
| **Input Operands List** | $2 \dots 8$ Pins | 2 Pins (`IN1`, `IN2`) | Dynamic list of input operands. Each pin can be independently assigned to a Tag Variable or a Constant scalar value. |

---

## 4. Supported Operators & Industrial Arithmetic Rules

### 4.1 Operator Specifications

| Operator | Symbol | Allowable Pins | Algebraic Formula (Left-to-Right) | Industrial Behavior |
| :--- | :---: | :---: | :--- | :--- |
| **Addition (`ADD`)** | `+` | $2 \dots 8$ | $OUT = IN_1 + IN_2 + \dots + IN_n$ | Summation of multiple process variables, offsets, or sensor readings. |
| **Subtraction (`SUB`)** | `-` | $2 \dots 8$ | $OUT = IN_1 - IN_2 - \dots - IN_n$ | Evaluates sequential difference from left to right (e.g., differential pressure, gross-to-tare deduction). |
| **Multiplication (`MUL`)** | `*` | $2 \dots 8$ | $OUT = IN_1 \times IN_2 \times \dots \times IN_n$ | Multi-factor product (e.g., volumetric rate $\times$ density $\times$ correction factor). |
| **Division (`DIV`)** | `/` | Exactly 2 | $OUT = IN_1 / IN_2$ | Quotients. Division by zero ($IN_2 = 0$) prevents CPU halt and flags a runtime fault. |
| **Modulo (`MOD`)** | `%` | Exactly 2 | $OUT = IN_1 \pmod{IN_2}$ | Remainder of integer division (e.g., circular queue indexing, step distribution). |

### 4.2 IEC 61131-3 Type Promotion Rules

In **`Automatic (Auto)`** mode, data types are resolved according to standard industrial rules:

| Operand Types Present | Target Evaluation Mode | Result Data Type | Notes |
| :--- | :---: | :---: | :--- |
| All operands are `INT32` (or integer constants) | `INT32` | `INT32` | Truncates fractional division remainders towards zero. |
| At least one operand is `REAL` (or float constant) | `REAL` | `REAL` (`Float32`) | Automatic promotion to IEEE-754 single precision floating-point. |
| Explicit `INT32` mode selected | `INT32` | `INT32` | Forces integer truncation regardless of operand types. |
| Explicit `REAL` mode selected | `REAL` | `REAL` (`Float32`) | Converts all integer operands to IEEE-754 floats before calculation. |

---

## 5. Dedicated Configuration Dialog (`MathConfigDialogView`)

Clicking **`Configure Block...`** in the Inspector or double-clicking the canvas node opens the comprehensive Math Configuration dialog:

```
┌───────────────────────────────────────────────┬───────────────────────────────────────────────┐
│ LEFT PANE: Configuration Parameters           │ RIGHT PANE: ALU Diagnostics & Live Sim        │
├───────────────────────────────────────────────┼───────────────────────────────────────────────┤
│ • General Parameters:                         │ • Algebraic Formula Preview:                  │
│   Operator (+, -, *, /, %)                    │   VREG0 = AI0 + AI1 + 10                      │
│   Data Type Mode (Auto, INT32, REAL)          │                                               │
│   Output Tag (OUT) dropdown                   │ • Live Simulation Test Bench:                 │
│                                               │   Interactive inputs to test calculation      │
│ • Input Operands List (2 .. 8 pins):          │                                               │
│   Pin Mode: Tag Variable / Constant           │ • ALU Output Result:                          │
│   Operand Selection & Type Badges             │   Displays live computed value + Valid badge  │
│   [+ Add Operand] & [Delete] buttons          │                                               │
│                                               │ • IEC 61131-3 Industrial Rules Summary        │
└───────────────────────────────────────────────┴───────────────────────────────────────────────┘
```

### 5.1 Real-Time Diagnostics Test Bench

The right pane of the dialog provides an instant verification workbench:
1. **Formula Preview:** Displays the exact mathematical expression constructed from your tags and constants.
2. **Interactive Simulation Inputs:** Allows typing trial numeric values into each operand slot without modifying physical PLC tags.
3. **Live Result & Status Badge:** Displays the computed outcome immediately, along with inferred data type (`INT32` or `REAL`) and operational status:
   - `✓ VALID`: Normal computation completed successfully.
   - `FAULT: DIV_BY_ZERO`: Zero divisor detected in division mode.

---

## 6. Practical Industrial Example

### Scenario: Multi-Sensor Total Feedwater Flow Summation

A boiler feedwater header receives flow from three supply lines measured by analog flow transmitters (`AI0`, `AI1`, and `AI2` in $\text{m}^3/\text{h}$, scaled to `REAL` in `VREG0`, `VREG1`, `VREG2`). Calculate the total feedwater flow and write to `VREG10`:

$$\text{Total Flow (VREG10)} = \text{VREG0} + \text{VREG1} + \text{VREG2}$$

### Step-by-Step Setup:

1. Drag a **`MATH`** block from the **Analog & Actuators** toolbox onto the canvas.
2. Open the configuration dialog by clicking **`Configure Block...`**.
3. Set **Operator** to `+ Addition (ADD)` and **Data Type** to `Automatic (Auto)`.
4. Set **Output Tag (OUT)** to `VREG10`.
5. Click **`+ Add Operand`** to add a third pin (`IN3`).
6. Configure the three pins:
   - `IN1`: Tag Variable $\rightarrow$ `VREG0`
   - `IN2`: Tag Variable $\rightarrow$ `VREG1`
   - `IN3`: Tag Variable $\rightarrow$ `VREG2`
7. In the **Simulation Input Values** pane on the right, enter trial values (`25.5`, `30.2`, `14.3`) and verify that the ALU Output Result calculates `70.0` with `Inferred Type: REAL`.
8. Click **`Apply`** to commit changes to the Logic Graph.
9. Wire `MATH.Out` to a [`COMPARATOR`](/blocks/comparator) block to monitor for header over-capacity thresholds.
