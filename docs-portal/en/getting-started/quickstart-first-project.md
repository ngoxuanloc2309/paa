# 10-Minute First Project Walkthrough

<div class="doc-meta-bar">
  <span><strong>Document ID:</strong> SYNX-DOC-GS-003</span>
  <span><strong>Chapter:</strong> 02. Installation &amp; Quickstart</span>
  <span><strong>Estimated Time:</strong> 10 Minutes (Offline + Hardware Optional)</span>
  <span><strong>Target Preset:</strong> SynaptiX Controller</span>
</div>

In this hands-on walkthrough, you will engineer, simulate, compile, atomically deploy, and live-monitor a real-world industrial **Over-Temperature Cooling Interlock** from scratch in under 10 minutes.

### Functional Specification of What We Will Build
- **Process Sensor (`AI0` -> `TankTemp`, `REAL`):** Continuously measures mixing tank temperature in `°C`.
- **Safety Permissive (`DI0` -> `SafetyDoorOk`, `BOOL`):** Closed (`1`) when the fan shroud maintenance door is latched; open (`0`) when personnel open the guard door.
- **Actuator Output (`DO0` -> `CoolingFan`, `BOOL`):**
  - **Start Rule:** Energize `CoolingFan = 1` whenever `TankTemp > 65.0` **AND** `SafetyDoorOk == 1`.
  - **Stop / Safety Cutout Rules:** De-energize `CoolingFan = 0` when `TankTemp <= 60.0` (5 °C thermal hysteresis to prevent contactor chattering) **OR** immediately if `SafetyDoorOk == 0`.

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
        <span class="synx-node-tag">AI0 • REAL</span>
        <span class="synx-node-alias">TankTemp</span>
        <span class="synx-node-val">Tank Temperature</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">72.4 °C</span>
  </div>

  <div class="synx-canvas-node is-comparator">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M3.5,6 L8,9 L3.5,12 M9.5,7 L13,7 M9.5,10.5 L13,10.5" /></svg>
        <span>COMPARATOR</span>
      </div>
      <span class="synx-node-status is-true">● TRUE</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">&gt; 65.0 °C</span>
        <span class="synx-node-alias">High Temp Check</span>
        <span class="synx-node-val">Trip Overheat GTE</span>
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
        <span class="synx-node-tag">DI0 == 1</span>
        <span class="synx-node-alias">Safety Door OK</span>
        <span class="synx-node-val">Door Interlock Closed</span>
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
        <span class="synx-node-alias">SET CoolingFan</span>
        <span class="synx-node-val">Energize Contactor</span>
      </div>
      <div class="synx-node-pin is-out" style="visibility: hidden;">Out ●</div>
    </div>
  </div>
</div>

---

## Step 1: Provision a `SynaptiX Controller` Project (`Ctrl+N`)

1. Launch **SynaptiX IDE** and press **`Ctrl+N`** (or select **File -> New Project**).
2. In the **Project Information** section:
   - **Project Name:** Enter `Tank_Cooling_Interlock`.
   - **Save Location:** Leave the default `Documents\SynaptiX\Projects` folder or click **Browse** to choose a custom workspace directory.
3. In the **Target Hardware** section:
   - Select **`SynaptiX Controller`** from the **Target Hardware Preset** dropdown (or click **Detect from Connected Device** if a physical `SynaptiX Controller` is already connected).
   - Verify on the right-hand **Hardware Specification Preview** card that the project allocates `8 DI`, `8 DO`, `4 AI`, `32 VFLAG`, `32 VREG`, `32 RETAIN`, `8 COUNTER` (`124 / 128 tags`) and `100 rules`.
4. Click **Create Project**.

![Provisioning the SynaptiX Controller Preset in New Project Dialog](/images/synaptix_new_project.png)

---

## Step 2: Configure Tag Aliases & Data Types in Tag Catalog (`Ctrl+3`)

Before wiring blocks, assign meaningful engineering names and physical data types so every block on your canvas is self-documenting:

1. Switch to the **[Tag Catalog](/workspaces/tag-catalog-tab)** workspace by pressing **`Ctrl+3`**.
2. Locate the three process tags and configure their **Alias** and **Data Type** columns as follows:

| Tag ID | Tag Kind | Alias to Enter | Data Type to Select | Engineering Role |
| :--- | :--- | :--- | :---: | :--- |
| **`AI0`** | Analog Input | `TankTemp` | **`REAL`** | Calibrated 32-bit IEEE-754 floating-point tank temperature (`°C`). |
| **`DI0`** | Digital Input | `SafetyDoorOk` | `BOOL` *(Fixed)* | Maintenance door limit switch (`1` = Closed/Safe, `0` = Open). |
| **`DO0`** | Digital Output | `CoolingFan` | `BOOL` *(Fixed)* | Cooling fan contactor relay output (`1` = Running, `0` = Stopped). |

![Configuring Symbolic Aliases and REAL Data Type in Tag Catalog](/images/synaptix_tag_catalog.png)

::: tip Why Switch `AI0` to `REAL` First?
Changing `AI0` from default `INT32` to **`REAL`** tells **SynaptiX IDE**'s [Bad-Data Defense](/concepts/data-model-and-types#visual-truth-input-state-machine-bad-data-defense) engine that `TankTemp` represents an IEEE-754 floating-point number. Any downstream [`COMPARATOR`](/blocks/comparator) connected to `AI0` will automatically accept fractional thresholds (such as `65.0` or `65.5`) and format whole numbers like `"65"` into `"65.0"`.
:::

---

## Step 3: Wire the Interlock on Logic Graph (`Ctrl+2`)

1. Press **`Ctrl+2`** to open the **[Logic Graph Canvas](/workspaces/logic-graph-tab)**.
2. **Build Rule 1 (Over-Temperature Fan Start with Door Safety Guard):**
   - From the left **Block Toolbox**, drag **`INPUT`** (under *Process I/O*), **`COMPARATOR`** (under *Logic*), **`GUARD`** (under *Logic*), and **`ACTION`** (under *Process I/O*) onto the 20 px grid.
   - Connect the output pin of **`INPUT`** -> input pin of **`COMPARATOR`** -> **`GUARD`** -> **`ACTION`**.
   - Click each block and configure its parameters in the right-hand **Property Inspector**:
     - **`INPUT` Block:** Select Source Tag `[AI0] — TankTemp`.
     - **`COMPARATOR` Block:** Set Operator to `` `>` ``, Compare Mode to `Constant`, and Threshold Value to `65.0` *(notice typing `65` and pressing `Enter` auto-normalizes to `65.0`)*.
     - **`GUARD` Block:** Select Guard Tag `[DI0] — SafetyDoorOk`, Operator `` `==` ``, Value `1`.
     - **`ACTION` Block:** Select Target Tag `[DO0] — CoolingFan` and Operation `SET` (sets output to `1`).
3. **Build Rule 2 & Rule 3 (Thermal Hysteresis Stop & Door-Open Cutout):**
   - Add a second chain: **`INPUT`** (`[AI0] — TankTemp`) -> **`COMPARATOR`** (`` `<=` `` `60.0`) -> **`ACTION`** (`RESET` `[DO0] — CoolingFan`).
   - Add a third safety cutout chain: **`INPUT`** (`[DI0] — SafetyDoorOk`) -> **`COMPARATOR`** (`` `==` `` `0`) -> **`ACTION`** (`RESET` `[DO0] — CoolingFan`).
4. Press **`Alt+G`** to snap all blocks cleanly onto the engineering grid, and **`Ctrl+S`** to save the project.

![Visual Interlock Authoring on the Logic Graph Canvas](/images/synaptix_logic_editor.png)

---

## Step 4: Verify in Offline Simulator (`F5`) & Inspect Rule Table (`Ctrl+4`)

Never deploy untested control logic to plant machinery. Validate every nominal and fault path first using the built-in desktop simulator:

1. Press **`F5`** to open the docked **[Offline Simulator](/tools/simulator-and-live-debug)** drawer at the bottom of the Logic Graph.
2. **Test Case A — Blocked by Open Safety Door:**
   - Leave `[DI0] — SafetyDoorOk` at `0` (Door Open).
   - Set `[AI0] — TankTemp` to `70.0` and press `Enter`.
   - Observe the canvas: the wire between `INPUT` and `COMPARATOR` highlights green (`70.0 > 65.0` is `TRUE`), **but stops at the `GUARD` block**. `[DO0] — CoolingFan` remains safely `0`!
3. **Test Case B — Normal Over-Temperature Activation:**
   - Toggle `[DI0] — SafetyDoorOk` to `1` (Door Closed).
   - Immediately, the `GUARD` permissive passes, the entire chain illuminates green, and `[DO0] — CoolingFan` switches to `1`.
4. **Test Case C — Hysteresis Hold & Cool-Down Reset:**
   - Lower `[AI0] — TankTemp` to `62.5`. Because `62.5` is between `60.0` and `65.0`, `CoolingFan` stays latched at `1` without chattering.
   - Lower `[AI0] — TankTemp` to `59.0`. Rule 2 fires and cleanly resets `CoolingFan` to `0`.
5. Exit simulation (`F5`), press **`F7`** to run full **Compile**, and press **`Ctrl+4`** to inspect the **[Rule Table](/workspaces/rule-table-tab)** — confirming your 3 compiled 4-stage rules and `3 / 100` rule budget usage.

![Testing Nominal and Interlock-Blocked States in Offline Simulator](/images/synaptix_simulator.png)

---

## Step 5: Connect (`F4`), Atomic Deploy (`Ctrl+F5`) & Live Watch (`Ctrl+7`)

With offline verification complete, commission the compiled logic onto a physical **SynaptiX Controller**:

1. **Connect (`F4`):**
   - On the top-right Unified Connection Bar, choose **`USB CDC`** (and select your `COMx` port) or **`Modbus TCP`** (enter controller `IP : 502`).
   - Press **`F4`** (or click **Connect**). Confirm the bottom status bar badge turns emerald green.
2. **Execute Two-Phase Atomic Deploy (`Ctrl+F5`):**
   - Press **`Ctrl+6`** to open the **[Atomic Deploy](/workspaces/deploy-tab)** workspace (or press **`Ctrl+F5`** directly from any tab).
   - Review the **Pre-Flight Safety Gate** checklist (Hardware Class Match, Protocol Compatibility, Tag & Rule Capacity Within Limits).
   - Confirm deployment. **SynaptiX IDE** transfers the configuration into isolated staging memory, verifies integrity checksums, and atomically swaps the active flash image so power interruptions can never corrupt the controller.

![Pre-Flight Validation & Two-Phase Atomic Deploy Workspace](/images/synaptix_deploy_gate.png)

3. **Verify Real-Time Trends in Live Watch (`Ctrl+7`) & Live Debug (`F8`):**
   - Press **`Ctrl+7`** to switch to **[Live Watch & Trends](/workspaces/live-watch-tab)**. Add `TankTemp`, `SafetyDoorOk`, and `CoolingFan` to the multi-trace trend chart to observe live 20 FPS telemetry and export commissioning logs to CSV if required.
   - Optionally return to **Logic Graph (`Ctrl+2`)** and press **`F8`** to activate **Online Live Debug**, watching physical sensor transitions illuminate your diagram wires in real time.

![Monitoring Real-Time 20 FPS Tag Values and Trend Charts in Live Watch](/images/synaptix_live_watch.png)

---

## Where to Go Next

- **Explore All 7 Workspaces:** Dive deeper into [Chapter 03 — IDE Workspaces](/workspaces/device-tab).
- **Master All 18 Function Blocks:** Add `SCALE` calibration, `TON` debounce timers, `MB_READ` RS-485 polling, and `MQTT_PUB` cloud telemetry in [Chapter 04 — Function Blocks Reference](/blocks/overview).
- **Accelerate Authoring with Templates & AI:** Load ready-made industrial patterns from the [Blueprints Library (`Ctrl+5`)](/workspaces/blueprints-tab) or generate validated circuits using [SynaptiX AI Copilot (`Ctrl+Space`)](/tools/ai-copilot).
