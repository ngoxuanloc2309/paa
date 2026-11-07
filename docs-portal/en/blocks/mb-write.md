# MB_WRITE — Modbus RTU Master Writer

<div class="doc-meta-bar">
  <span><strong>Document ID:</strong> SYNX-DOC-BLK-MBWRITE</span>
  <span><strong>Block Category:</strong> Fieldbus &amp; Hardware Actuation</span>
  <span><strong>Execution Class:</strong> Non-Blocking Asynchronous RTU Command Dispatcher</span>
  <span><strong>Standard:</strong> Modbus Application Protocol V1.1b3 &bull; IEC 61131-3</span>
</div>

## 1. Synopsis & Execution Semantics

The **`MB_WRITE`** (**Modbus RTU Master Write**) function block dispatches non-blocking command and setpoint telegrams over RS-485 (`Port #1` or `Port #2`) to external slave actuators, variable frequency drives (VFDs), remote I/O islands, Sort-to-Light indicator banks, and proportional valve manifolds.

`MB_WRITE` unifies **two distinct architectural write paradigms** inside a single block:
1. **Mode 1 — `ChannelIndexed` (Channel-Indexed Target, Default):** Purpose-built for multi-chute Sort-to-Light / Pick-to-Light racks, multi-valve manifolds, and stack lights. Logic drives three semantic pins (`Channel`, `Command`, `Param`) mapped automatically to `BaseAddressCommand + (Channel - 1)` and `BaseAddressParam + (Channel - 1)`, with optional **Exclusive Channel** mutual exclusion (`IsExclusiveChannel = true`) so lighting chute `#4` automatically extinguishes all other chutes.
2. **Mode 2 — `DirectRegisters` (Direct Modbus Registers):** Standard register/coil write engine supporting **`FC 05`** (Write Single Coil), **`FC 06`** (Write Single Register), **`FC 15`** (Write Multiple Coils), and **`FC 16`** (Write Multiple Registers) with dynamic typed input pins, 32-bit endianness encoding, and scaling.

---

## 2. Pinout & Signal Contract

`MB_WRITE` dynamically synchronizes its left-side input connectors according to both **`TriggerMode`** and **`WriteMode`**, leaving zero orphaned pins on the canvas:

### 2.1. Input Connectors Matrix

| Pin | Direction | Signal Kind | Data Type | Visibility Condition | Functional Contract |
| :--- | :---: | :---: | :---: | :---: | :--- |
| **`Trig`** | Input | Event (`0 → 1`) | `BOOL` | **Visible ONLY when `TriggerMode = OnTriggerRisingEdge`** *(hidden in `Cyclic` & `OnDataChange`)* | **Transmit Trigger:** Queues a Modbus write transaction on the rising edge (`0 → 1`). |
| **`Channel`** | Input | Value | `UINT16` | **Visible ONLY when `WriteMode = ChannelIndexed`** | **Target Channel (`1..ChannelCount`):** Selects which indexed actuator slot receives the command. |
| **`Command`** | Input | Value | `UINT16` | **Visible ONLY when `WriteMode = ChannelIndexed`** | **Actuator Action Code:** Written to `BaseAddressCommand + (Channel - 1)` (e.g., `0` = OFF, `1` = Solid, `2` = Blink). |
| **`Param`** | Input | Value | `INT32` / `UINT16` | **Visible ONLY when `WriteMode = ChannelIndexed`** | **Actuator Parameter:** Written to `BaseAddressParam + (Channel - 1)` (e.g., color code or blink duty). |
| **`<Pin.Label>`** | Input *(Dynamic)* | Value | `BOOL` / `INT32` / `REAL` | **Visible ONLY when `WriteMode = DirectRegisters`** | **Dynamic Register Input(s):** One input pin per configured row in `Pins`, encoded and written starting at `StartAddress + RegOffset`. |

### 2.2. Fixed Handshake & Diagnostic Output Connectors

| Pin | Direction | Signal Kind | Data Type | Visibility | Functional Contract |
| :--- | :---: | :---: | :---: | :---: | :--- |
| **`Done`** | Output | Event (1-Scan Pulse) | `BOOL` | Always visible | Pulses `1` for one scan cycle when the external slave echoes a valid write confirmation frame. |
| **`Busy`** | Output | Value | `BOOL` | Always visible | Remains `1` while the RS-485 request is queued or awaiting slave acknowledgment. |
| **`Error`** | Output | Value (Latched) | `BOOL` | Always visible | Latches `1` if `TimeoutMs` expires, CRC-16 fails, or the slave returns a Modbus exception PDU. |
| **`ErrCode`** | Output | Value | `UINT16` | Always visible | Diagnostic fault code (`0` = None, `1` = Timeout, `2` = CRC-16 Error, `3` = Slave Exception, `4` = Malformed Echo). |

---

## 3. Inspector Parameters & Constraints

### 3.1. Common Transport & Trigger Parameters

| Parameter | Property Key | Type & Domain | Default | Description & Bus Protection Behavior |
| :--- | :--- | :--- | :---: | :--- |
| **RS-485 Port** | `PortIndex` | `0` (`RS-485 #1`) \| `1` (`RS-485 #2`) | `0` | Selects physical RS-485 port configured as Modbus RTU Master. |
| **Slave ID** | `SlaveId` | `Byte` (`1..247`) | `1` | Target slave device address on the RS-485 bus. |
| **Write Mode** | `WriteMode` | `ChannelIndexed` \| `DirectRegisters` | `ChannelIndexed` | Switches between indexed multi-channel actuator control and raw `FC 05/06/15/16` register writes. |
| **Trigger Mode** | `TriggerMode` | `OnTriggerRisingEdge` \| `Cyclic` \| `OnDataChange` | `OnTriggerRisingEdge` | Controls when frames transmit: rising edge on `Trig`, periodic timer, or Change-of-Value (COV). |
| **Cyclic Interval** | `CyclicIntervalMs` | `UINT32` (`50..60000 ms`) | `500` | Periodic write interval when `TriggerMode = Cyclic` (useful for slave watchdog keep-alive). |
| **COV Deadband** | `Deadband` | `Float` (`≥ 0.0`) | `0.5` | Minimum absolute value change (`Δ ≥ Deadband`) required to trigger a write when `TriggerMode = OnDataChange`. Prevents RS-485 bus saturation from ADC jitter. |
| **Response Timeout** | `TimeoutMs` | `UINT32` (`50..5000 ms`) | `300` | Maximum wait time for slave write-echo confirmation before asserting `Error = 1`. |

### 3.2. Mode 1 Parameters — `ChannelIndexed`

| Parameter | Property Key | Type & Domain | Default | Description |
| :--- | :--- | :--- | :---: | :--- |
| **Command Base Address** | `BaseAddressCommand` | `UINT16` (Hex/Dec) | `0x0100` (`256`) | Starting Holding Register for channel commands (`CH 1 → 0x0100`, `CH k → BaseAddressCommand + k - 1`). |
| **Param Base Address** | `BaseAddressParam` | `UINT16` (Hex/Dec) | `0x0120` (`288`) | Starting Holding Register for channel parameters (`CH 1 → 0x0120`, `CH k → BaseAddressParam + k - 1`). |
| **Channel Count** | `ChannelCount` | `UINT16` (`1..64`) | `9` | Valid upper bound of channels managed by this actuator bank. |
| **Exclusive Channel** | `IsExclusiveChannel` | `BOOL` | `true` | When `true`, activating channel `k` automatically clears all other channels (`1..ChannelCount ≠ k`) to `0` (One-Hot Sort-to-Light mode). When `false`, each channel switches independently. |

### 3.3. Mode 2 Parameters — `DirectRegisters`

| Parameter | Property Key | Type & Domain | Default | Description |
| :--- | :--- | :--- | :---: | :--- |
| **Function Code** | `FunctionCode` | `FC 05`, `FC 06`, `FC 15`, `FC 16` | `FC 06` | `FC 05`: Single Coil (`0x`) · `FC 06`: Single Register (`4x`) · `FC 15`: Multiple Coils (`0x`) · `FC 16`: Multiple Registers (`4x`). |
| **Start Address** | `StartAddress` | `UINT16` (`0..65535`) | `0` | Target starting coil or Holding Register address on the external slave. |
| **Point Count** | `PointCount` | `UINT16` (`1..16`) | `1` | Total coils/registers written in multi-point `FC 15` / `FC 16` mode. |
| **Dynamic Input Pins** | `Pins` | `0..4` bindings | `1` pin | Configures `Label`, `RegOffset`, `DataType` (`BOOL`, `INT16`, `UINT16`, `INT32`, `UINT32`, `REAL`), `Endianness` (`ABCD`, `CDAB`, `BADC`, `DCBA`), and `Multiplier`. |

---

## 4. Trigger Dispatch & Confirmation Rules

The `MB_WRITE` block executes commanded writes to external slave devices with deterministic handshaking:
- **Trigger Conditions:** Dispatched via a rising edge on `Trig`, cyclic timer ticks (`CyclicIntervalMs`), or change-of-value triggers (`Δ ≥ Deadband`).
- **Busy Guard (`Busy = 1`):** While the frame is in transit on the serial wire, pin `Busy` asserts `1` to prevent request collisions.
- **Success Handshake (`Done = 1`):** Upon receiving a valid echo reply with matching CRC, pin `Done` pulses `1` for one scan cycle and clears `Busy`.
- **Fault Handling (`Error = 1`):** If the slave fails to respond within `TimeoutMs` or returns an exception PDU, pin `Error` asserts with diagnostic `ErrCode (1..4)`.

---

## 5. Validation, Error Handling & Live Diagnostics

- **Live Frame Breakdown & Simulator:** The `MB_WRITE` configuration modal includes an interactive **Actuator Channel Mapping Table** and **Modbus Frame Breakdown** preview showing exact TX hex bytes (`Slave ID`, `FC`, `Addr Hi/Lo`, `Data`, `CRC-16`) before deployment.
- **Bus Storm Prevention:** Avoid wiring noisy raw analog signals directly into `MB_WRITE` with `Deadband = 0` in `OnDataChange` mode. Keep `Deadband ≥ 0.5` (e.g., `0.5 Hz` for VFD speed setpoints) so RS-485 airtime remains below 30% utilization.
- **Canvas Status Pill (`F8`):** Cycles dynamically between **`READY`**, **`TX...`** (while `Busy = 1`), **`DONE #N`** (total confirmed writes), and **`ERR: <Reason>`**.

---

## 6. Practical Industrial Wiring Example

### Cloud-Driven 9-Chute Sort-to-Light Rack (`MQTT_SUB` → `MB_WRITE` Channel-Indexed)

**Objective:** Receive a parcel sorting JSON command `{"grid": 4, "color": 2}` from the warehouse management broker via [`MQTT_SUB`](/blocks/mqtt-sub) and immediately illuminate Chute `#4` on an RS-485 Sort-to-Light controller (`SlaveId = 3`) while automatically extinguishing the previous chute.

<div class="synx-node-chain">
  <div class="synx-canvas-node is-trigger">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M8,14 L8,8 M5,10 A4,4 0 0 1 11,10 M3,7.5 A7,7 0 0 1 13,7.5 M1.5,5 A9.5,9.5 0 0 1 14.5,5" /></svg>
        <span>MQTT_SUB</span>
      </div>
      <span class="synx-node-status is-active">● FIFO</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in" style="visibility: hidden;">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">sorter1/cmd</span>
        <span class="synx-node-alias">Inbound Order</span>
        <span class="synx-node-val">{grid: 4, color: 2}</span>
      </div>
      <div class="synx-node-pin is-out">Valid ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">TRIG</span>
  </div>

  <div class="synx-canvas-node is-direct">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,5 L14,5 M14,5 L10,2 M14,5 L10,8 M14,11 L2,11 M2,11 L6,8 M2,11 L6,14" /></svg>
        <span>MB_WRITE</span>
      </div>
      <span class="synx-node-status is-active">● EXCLUSIVE</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● Trig</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">RS-485 #1 • Slave 3</span>
        <span class="synx-node-alias">Channel Indexed</span>
        <span class="synx-node-val">Channel 4 • Color 2</span>
      </div>
      <div class="synx-node-pin is-out">Done ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">CHUTE_4</span>
  </div>

  <div class="synx-canvas-node is-action">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,8 L4,8 M12,8 L14,8 M4,4.5 A3.5,3.5 0 1 1 4,11.5" /></svg>
        <span>ACTION</span>
      </div>
      <span class="synx-node-status is-active">● LIT</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Sort-to-Light</span>
        <span class="synx-node-alias">Illuminate Chute #4</span>
        <span class="synx-node-val">Extinguish Previous Chute</span>
      </div>
      <div class="synx-node-pin is-out" style="visibility: hidden;">Out ●</div>
    </div>
  </div>
</div>

**Configuration Steps:**
1. Configure [`MQTT_SUB`](/blocks/mqtt-sub) on topic `synaptix/sorter1/cmd` (`QueueMode = FIFO`) with dynamic pins `grid` (`INT16`) and `color` (`INT16`).
2. Place **`MB_WRITE`** on the canvas and configure:
   - `PortIndex = RS-485 #1`, `SlaveId = 3`
   - `WriteMode = ChannelIndexed`, `ChannelCount = 9`, `IsExclusiveChannel = true`
   - `BaseAddressCommand = 0x0100`, `BaseAddressParam = 0x0120`
   - `TriggerMode = OnTriggerRisingEdge`, `TimeoutMs = 250`
3. Wire `MQTT_SUB.Valid` → `MB_WRITE.Trig`, `MQTT_SUB.grid` → `MB_WRITE.Channel`, set `Command = 1` (Solid ON), and wire `MQTT_SUB.color` → `MB_WRITE.Param`.
