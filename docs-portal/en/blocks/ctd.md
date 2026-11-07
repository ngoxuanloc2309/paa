# CTD — Count Down Counter

<div class="doc-meta-bar">
  <span><strong>Document ID:</strong> SYNX-DOC-BLK-CTD</span>
  <span><strong>Block Category:</strong> Timing, Counting &amp; Scaling</span>
  <span><strong>Execution Class:</strong> Stateful Edge-Triggered Decrementer</span>
  <span><strong>Standard:</strong> IEC 61131-3 Counter Model</span>
</div>

## 1. Synopsis & Execution Semantics

The **`CTD`** (**Count Down Counter**) function block is a preset-loaded decrementing event counter tailored for remaining-inventory tracking, dispensing countdowns, magazine empty detection, and scheduled tool-life expiration interlocks.

During each deterministic scan cycle of the SynaptiX runtime engine:
1. **Preset Load / Reset Priority (`R` / `ResetTag`):** When the **`R`** pin (or bound `ResetTag`) transitions active, the accumulator **Current Value (`CV`)** is immediately loaded with **`PresetValue` (`PV`)** (`CV ← PV`). Because `PV > 0`, output **`Q`** immediately evaluates to `0` (`FALSE`).
2. **Rising-Edge Decrement (`CD`):** While `R` is inactive (`0`), each rising-edge transition (`0 → 1`) on **`CD`** decrements `CV` by `-1` (`CV ← CV - 1`). Holding `CD` continuously `HIGH` across multiple scans does not cause additional decrements.
3. **Zero-Depletion Evaluation (`CV ≤ 0`):** Output **`Q`** asserts `1` (`TRUE`) as soon as `CV ≤ 0`, signaling that the preset quota has been completely consumed.
4. **Tag Synchronization:** `CV` writes continuously to **`CvTag`** (`COUNTER0..COUNTER7` or `VREG`), and `Q` writes to **`OutputTag`**.

::: tip Difference Between `CTU` Reset and `CTD` Reset
In [`CTU`](/blocks/ctu), activating `R` clears `CV → 0`. In **`CTD`**, activating `R` reloads the full starting quota **`CV ← PV`** (equivalent to the standard IEC 61131-3 `LD` preset-load semantics) so the machine is armed for the next countdown batch.
:::

---

## 2. Pinout & Signal Contract

`CTD` shares the `CounterNodeViewModel` hardware architecture with `CounterMode = "CTD"`, exposing two input pins on the left and one output pin on the right:

| Pin | Direction | Signal Kind | Data Type | Visibility | Functional Contract |
| :--- | :---: | :---: | :---: | :---: | :--- |
| **`CD`** | Input | Event (`0 → 1`) | `BOOL` | Always visible | **Count Down Trigger:** Decrements `CV` by `1` on each rising edge (`0 → 1`). Can be wired on canvas or bound directly via `InputTag`. |
| **`R`** | Input | Event / Level | `BOOL` | Always visible | **Preset Load / Reset Input:** Reloads `CV ← PV` and clears `Q → 0` (for `PV > 0`). Overrides `CD` when active simultaneously. |
| **`Q`** | Output | Value | `BOOL` | Always visible | **Depletion / Zero Reached Output:** Asserts `1` (`TRUE`) whenever `CV ≤ 0`; remains `0` (`FALSE`) while `CV > 0`. |

---

## 3. Inspector Parameters & Constraints

Selecting a `CTD` node on the [Logic Graph Canvas](/workspaces/logic-graph-tab) presents the following parameters in the **Inspector** panel:

| Parameter | Property Key | Type & Domain | Default | Description & Physical Contract |
| :--- | :--- | :--- | :---: | :--- |
| **Custom Label** | `CustomLabel` | `String` | `"CTD Counter"` | Custom block title displayed on the Logic Graph card. |
| **Counter Mode** | `CounterMode` | `"CTU"` \| `"CTD"` | `"CTD"` | Selects downward countdown semantics (`CD`, `CV ≤ 0`, reload `CV ← PV` on reset). |
| **Preset Value (`PV`)** | `PresetValue` | `INT32` (`≥ 1`) | `10` | Starting quota loaded into `CV` whenever `R` / `ResetTag` triggers. Enforces strict `INT32` Bad-Data Defense. |
| **Countdown Input Tag** | `InputTag` | `TagModel?` (`BOOL`) | `null` | Optional direct digital tag (`DI0..DI7`, `VBIT`) supplying `CD` pulses without an upstream canvas wire. |
| **Current Value Tag (`CV`)** | `CvTag` | `TagModel?` (`INT32`) | `COUNTER0` | Counter register (`COUNTER0..COUNTER7`) or virtual integer tag (`VREG`) tracking remaining items. |
| **Output Tag (`Q`)** | `OutputTag` | `TagModel?` (`BOOL`) | `null` | Boolean destination tag driven `1` when `CV ≤ 0` (e.g., magazine empty alarm or feeder stop interlock). |
| **Load / Reset Tag (`R`)** | `ResetTag` | `TagModel?` (`BOOL`) | `None` | Optional boolean tag that reloads `CV ← PV` when asserted. |

---

## 4. Scan-Cycle Timing Walkthrough (`PV = 3`)

The table below illustrates a realistic down-counting cycle starting with a loaded tray of 3 parts and asserting `Q = 1` when depleted (`CV ≤ 0`):

| Scan Event | `CD` Pin | `R` Pin | Remaining `CV` | Output `Q` (`CV ≤ 0`) | Canvas Status Pill |
| :--- | :---: | :---: | :---: | :---: | :--- |
| Operator presses Magazine Reload | `0` | `0 → 1` | `3` | `0` | `PV: 3` (`3 / 3`) |
| Reload button released | `0` | `0` | `3` | `0` | `PV: 3` (`3 / 3`) |
| Stroke #1 dispensed | `0 → 1` | `0` | `2` | `0` | `PV: 3` (`2 / 3`) |
| Stroke #2 dispensed | `0 → 1` | `0` | `1` | `0` | `PV: 3` (`1 / 3`) |
| Stroke #3 dispensed (Empty) | `0 → 1` | `0` | `0` | **`1`** | **`Q = ON`** (`0 / 3`) |
| Operator refills & presses Reload | `0` | `0 → 1` | `3` | `0` | `PV: 3` (`3 / 3`) |

---

## 5. Validation, Error Handling & Live Diagnostics

- **Initial Boot Consideration:** Remember that before `CV` is loaded via `R` / `ResetTag` (if `CvTag` initializes at `0`), the condition `CV ≤ 0` is immediately satisfied (`Q = 1`). Always pulse `R` upon magazine refill or bind `CvTag` with a non-zero initial value in [Tag Catalog](/workspaces/tag-catalog-tab) if the machine starts pre-loaded.
- **Underflow Protection:** Firmware arithmetic clamps `CV` against signed 32-bit integer underflow so repeated `CD` pulses while `CV ≤ 0` never wrap around to positive numbers.
- **Live Canvas Telemetry (`F8`):** Displays remaining quota as **`{CurrentCount} / {PresetValue}`** and highlights the status pill **`Q = ON`** whenever the counter reaches zero depletion.

---

## 6. Practical Industrial Wiring Example

### CNC Magazine Pallet Feeder Empty Interlock (`50` Blanks per Tray)

**Objective:** Load a tray of `50` aluminum blanks when the operator presses the `DI2_TrayReloaded` pushbutton. Each feeder stroke (`DI0_FeederStroke`) decrements the remaining blank count by `1`. When `0` blanks remain (`Q = 1`), illuminate the amber refill beacon (`DO1_TrayEmptyLamp`) and block the cycle start via a [`GUARD`](/blocks/guard) gate.

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
        <span class="synx-node-alias">Feeder Stroke</span>
        <span class="synx-node-val">Pusher Limit Switch (CD)</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">DEC</span>
  </div>

  <div class="synx-canvas-node is-counter">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M5.5,3 L5.5,13 M10.5,3 L10.5,13 M2,6.5 L14,6.5 M2,10 L14,10" /></svg>
        <span>CTD COUNTER</span>
      </div>
      <span class="synx-node-status is-active">● 0 / 50</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● CD</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">PV = 50 • CV COUNTER1</span>
        <span class="synx-node-alias">Remaining Blanks</span>
        <span class="synx-node-val">Q = 1 when CV == 0</span>
      </div>
      <div class="synx-node-pin is-out">Q ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">EMPTY</span>
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
        <span class="synx-node-tag">DO1 = 1</span>
        <span class="synx-node-alias">Tray Empty Beacon</span>
        <span class="synx-node-val">Amber Light Refill Alert</span>
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
        <span class="synx-node-tag">DI2 • BOOL</span>
        <span class="synx-node-alias">Reload Button</span>
        <span class="synx-node-val">Tray Confirm Button (LD)</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">LOAD</span>
  </div>

  <div class="synx-canvas-node is-counter">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M5.5,3 L5.5,13 M10.5,3 L10.5,13 M2,6.5 L14,6.5 M2,10 L14,10" /></svg>
        <span>CTD RELOAD</span>
      </div>
      <span class="synx-node-status is-active">● 50 / 50</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● LD</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Preset COUNTER1 = 50</span>
        <span class="synx-node-alias">Reload 50 Blanks</span>
        <span class="synx-node-val">Q Resets To 0</span>
      </div>
      <div class="synx-node-pin is-out">Q ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">CLEAR</span>
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
        <span class="synx-node-tag">DO1 = 0</span>
        <span class="synx-node-alias">Clear Refill Beacon</span>
        <span class="synx-node-val">Resume Auto Machine Cycle</span>
      </div>
      <div class="synx-node-pin is-out" style="visibility: hidden;">Out ●</div>
    </div>
  </div>
</div>

**Configuration Steps:**
1. Define `DI0_FeederStroke`, `DI2_TrayReloaded`, `CNT_RemainingBlanks` (`COUNTER1`, initial value `50`), and `DO1_TrayEmptyLamp` in [Tag Catalog](/workspaces/tag-catalog-tab).
2. Add a **`CTD`** block onto the canvas and configure:
   - `PresetValue` = `50`
   - `CvTag` = `CNT_RemainingBlanks` (`COUNTER1`)
   - `OutputTag` = `DO1_TrayEmptyLamp`
3. Connect `DI0_FeederStroke` to **`CD`** and `DI2_TrayReloaded` to **`R`**.
4. Verify in the [Desktop Simulator](/tools/simulator-and-live-debug) (`F5`): pulsing `DI2_TrayReloaded` sets `50 / 50` (`Q = 0`), and reaching `0 / 50` asserts `Q = ON`.
