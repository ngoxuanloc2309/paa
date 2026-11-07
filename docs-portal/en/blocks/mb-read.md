# MB_READ — Modbus RTU Master Poller

<div class="doc-meta-bar">
  <span><strong>Document ID:</strong> SYNX-DOC-BLK-MBREAD</span>
  <span><strong>Block Category:</strong> Fieldbus &amp; Hardware Actuation</span>
  <span><strong>Execution Class:</strong> Non-Blocking Asynchronous RTU Poller</span>
  <span><strong>Standard:</strong> Modbus Application Protocol V1.1b3 &bull; IEC 61131-3</span>
</div>

## 1. Synopsis & Execution Semantics

The **`MB_READ`** (**Modbus RTU Master Read**) function block enables a SynaptiX edge controller to act as an RS-485 **Modbus RTU Master**, actively polling external fieldbus slave instruments such as multifunction energy meters, variable frequency drives (VFDs), temperature/humidity transmitters, ultrasonic flow meters, and industrial weighing indicators.

![SynaptiX Modbus RTU Master Read Configuration Dialog](/images/synaptix_modbus_read.png)

Key runtime execution guarantees:
- **Zero Scan-Loop Blocking:** All RS-485 frame transmissions, UART DMA reception, and `TimeoutMs` watchdogs run on a dedicated background communication worker. Slow or disconnected sensors never stall the deterministic `10 ms` logic scan loop.
- **Single-Telegram Multi-Pin Extraction:** A single contiguous Modbus request (`StartAddress`, `RegCount`) decodes up to **4 dynamic output pins** simultaneously with per-pin word offset, data type (`BOOL`, `INT16`, `UINT16`, `INT32`, `UINT32`, `REAL`), 32-bit byte/word endianness, and linear `Multiplier` scaling.
- **Atomic Scan Commit:** Decoded register values and the 1-scan **`Valid`** pulse commit atomically between scan cycles after CRC-16 verification passes.

---

## 2. Pinout & Signal Contract

`MB_READ` dynamically adapts its left-hand input connector topology based on `ReadMode` to keep the canvas clean and unambiguous:

### 2.1. Fixed Control & Diagnostic Pins

| Pin | Direction | Signal Kind | Data Type | Visibility Condition | Functional Contract |
| :--- | :---: | :---: | :---: | :---: | :--- |
| **`EN`** | Input *(Internal)* | Level | `BOOL` | Managed automatically in `Cyclic` mode | Enables background polling scheduler when the block is active in the deployed graph. |
| **`Trig`** | Input | Event (`0 → 1`) | `BOOL` | **Visible ONLY when `ReadMode = OnTriggerRisingEdge`** *(hidden in `Cyclic` mode)* | **On-Demand Read Trigger:** Dispatches one Modbus RTU request frame on each rising edge (`0 → 1`). |
| **`Valid`** | Output | Event (1-Scan Pulse) | `BOOL` | Always visible | **New Data Ready Pulse:** Pulses `1` for exactly one scan cycle (`10 ms`) when a valid slave response passes CRC-16 and updates all dynamic pins. |
| **`Error`** (`Err`) | Output | Value (Latched) | `BOOL` | Always visible | **Bus Fault Flag:** Latches `1` on response timeout, CRC-16 error, Modbus exception, or malformed frame. Auto-clears to `0` on the next successful poll. |
| **`ErrCode`** | Output | Value | `UINT16` | Always visible | **Diagnostic Fault Code:** `0` = OK, `1` = Timeout, `2` = CRC-16 Mismatch, `3` = Modbus Exception, `4` = Malformed Frame. |

### 2.2. Dynamic Data Output Pins (`Pins` Collection)

Each row added in the **Dynamic Output Pins** table generates a named output connector below `ErrCode` (up to `4` pins per block) that emits the decoded, scaled value (`BOOL`, `INT32`, or `REAL`) and writes directly to its bound `TargetTag`.

---

## 3. Inspector Parameters & Constraints

Click **`[ Configure Modbus Read... ]`** in the Inspector (or double-click the node) to open the two-column engineering modal dialog:

### 3.1. Bus, Addressing & Timing Parameters

| Parameter | Property Key | Type & Domain | Default | Description & Constraints |
| :--- | :--- | :--- | :---: | :--- |
| **RS-485 Port** | `PortIndex` | `0` (`RS-485 #1`) \| `1` (`RS-485 #2`) | `0` | Selects the physical half-duplex RS-485 transceiver port configured in [RS-485 Config & SCADA Slave Map](/tools/rs485-and-scada-slave). |
| **Slave ID** | `SlaveId` | `Byte` (`1..247`) | `1` | Unicast Modbus RTU station address of the target field sensor. |
| **Function Code** | `FunctionCode` | `FC 01` \| `FC 02` \| `FC 03` \| `FC 04` | `FC 03` | `FC 01`: Read Coils (`0x`) · `FC 02`: Read Discrete Inputs (`1x`) · `FC 03`: Read Holding Registers (`4x`) · `FC 04`: Read Input Registers (`3x`). |
| **Start Address** | `StartAddress` | `UINT16` (`0..65535`) | `0` | Zero-based starting register/coil offset in the external slave PDU (`0x0000..0xFFFF`). |
| **Register Count** | `RegCount` | `UINT16` (`1..125`) | `2` | Number of consecutive 16-bit registers (or coils) fetched in a single telegram. Must span `RegOffset + WordWidth` of all dynamic pins. |
| **Read Mode** | `ReadMode` | `Cyclic` \| `OnTriggerRisingEdge` | `Cyclic` | `Cyclic` polls automatically every `PollIntervalMs` (0 input pins); `OnTriggerRisingEdge` exposes the `Trig` input pin. |
| **Poll Interval** | `PollIntervalMs` | `UINT32` (`50..60000 ms`) | `500` | Periodic polling interval in milliseconds (active only when `ReadMode = Cyclic`). |
| **Response Timeout** | `TimeoutMs` | `UINT32` (`50..5000 ms`) | `300` | Maximum wait window for a complete slave reply before raising `Error = 1` (`ErrCode = 1`). |

### 3.2. Dynamic Output Pin Decoding Contract (`ModbusPinBindingViewModel`)

| Column | Property Key | Options / Range | Default | Engineering Notes |
| :--- | :--- | :--- | :---: | :--- |
| **Pin Label** | `Label` | `String` | `"Val"` | Connector label displayed on the right side of the canvas node (e.g., `Pressure`, `Voltage_Avg`). |
| **Reg Offset** | `RegOffset` | `UINT16` (`0 .. RegCount - 1`) | `0` | Relative word offset from `StartAddress` (`+0`, `+1`, `+2`, ...). |
| **Data Type** | `DataType` | `BOOL`, `INT16`, `UINT16`, `INT32`, `UINT32`, `REAL` (`FLOAT32`) | `INT16` | `BOOL` / `INT16` / `UINT16` consume `1` word; `INT32` / `UINT32` / `REAL` consume `2` consecutive words. |
| **Word/Byte Order** | `Endianness` | `BigEndian_ABCD`, `MidBig_CDAB` *(Word-Swap)*, `ByteSwap_BADC`, `LittleEndian_DCBA` | `BigEndian_ABCD` | Applies to 32-bit types (`INT32`, `UINT32`, `REAL`). Over 70% of industrial meters (Schneider PM2200, INVT, Delta) use **`MidBig_CDAB` (Word-Swap)**. |
| **Scale Multiplier** | `Multiplier` | `Float` (`≠ 0`) | `1.0` | Linear fixed-point scaling (`Value = Raw × Multiplier`), e.g., `0.1` converts raw `253` into `25.3 °C`. |
| **Target Tag** | `TargetTag` | `TagModel?` | `null` | Automatically synchronizes tag type compatibility (`BOOL` ↔ `Bool`, `FLOAT32` ↔ `Real`). |

::: warning Fractional Multiplier Guard on `INT32` Tags
If you assign a fractional `Multiplier` (such as `0.1` or `0.01`) while `TargetTag` is an `INT32` virtual register, SynaptiX IDE highlights the multiplier cell in amber (`⚠ Fractional multiplier will round to integer on INT32 tag`). Either switch `TargetTag` to a **`REAL` (`FLOAT32`)** tag or set `Multiplier = 1`.
:::

---

## 4. Non-Blocking Background Polling Rules

The `MB_READ` block operates in a background communication task, **never stalling local deterministic scan cycles**:
- **Cyclic Mode (`Cyclic`):** Automatically polls the sensor at regular intervals defined by `PollIntervalMs` (default `500 ms`).
- **Triggered Mode (`OnTriggerRisingEdge`):** Only initiates a read transaction on a rising edge at pin `Trig`.
- **Atomic Commit:** When a complete, CRC-valid frame is received, data is decoded and committed to target output pins with a single-scan `Valid = 1` pulse.

---

## 5. Validation, Error Handling & Live Diagnostics

### Standard `ErrCode` Troubleshooting Matrix

| `ErrCode` | Symbolic Fault | Root Cause | Field Corrective Action |
| :---: | :--- | :--- | :--- |
| **`0`** | `ERR_NONE` | Valid response received and decoded. | Normal operation; canvas pill shows **`VALID`** or **`N polls`**. |
| **`1`** | `ERR_TIMEOUT` | No reply received within `TimeoutMs`. | Check 24 VDC sensor power, swapped RS-485 `A(+)` / `B(-)` wires, `SlaveId`, baud rate/parity settings, or raise `TimeoutMs` to `500–1000 ms`. |
| **`2`** | `ERR_CRC` | Frame received with corrupted CRC-16 checksum. | Install `120 Ω` termination resistors at both bus ends, use shielded twisted-pair (STP) cable routed away from VFD motor leads, and ground shield at one end. |
| **`3`** | `ERR_EXCEPTION` | Slave returned exception (`0x01` Illegal Function, `0x02` Illegal Data Address). | Verify `FC 03` vs `FC 04` in the sensor manual, check 0-based vs 1-based register addressing (`±1` offset), and verify `RegCount` does not exceed slave map bounds. |
| **`4`** | `ERR_MALFORMED` | Incomplete byte count or truncated UART frame. | Inspect loose terminal ferrules, baud-rate mismatch, or RS-485 transceiver turnaround contention. |

- **Offline Hex Frame Simulator:** Inside the `MB_READ` modal dialog, the right-hand pane previews the exact outgoing hex telegram (with live CRC-16 calculation) and lets you paste simulated hex responses to verify `CDAB` vs `ABCD` decoding before going onsite.

---

## 6. Practical Industrial Wiring Example

### Three-Phase Energy Meter Monitoring (Schneider PM2200 via `FC 03`, `CDAB` Float32)

**Objective:** Poll average line voltage and total active power every `1000 ms` on `RS-485 #1` from a multifunction meter (`SlaveId = 2`), trigger an under-voltage alarm if voltage drops below `195.0 V` while `Valid` is confirmed, and forward readings to cloud telemetry.

<div class="synx-node-chain" style="margin-bottom: 12px;">
  <div class="synx-canvas-node is-input">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,5 L14,5 M14,5 L10,2 M14,5 L10,8 M14,11 L2,11 M2,11 L6,8 M2,11 L6,14" /></svg>
        <span>MB_READ</span>
      </div>
      <span class="synx-node-status is-live">● RS485 #1</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in" style="visibility: hidden;">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">Slave 2 • FC 03</span>
        <span class="synx-node-alias">PM2200 Meter</span>
        <span class="synx-node-val">Cyclic Poll 1000ms</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">192.4 V</span>
  </div>

  <div class="synx-canvas-node is-comparator">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M3.5,6 L8,9 L3.5,12 M9.5,7 L13,7 M9.5,10.5 L13,10.5" /></svg>
        <span>COMPARATOR</span>
      </div>
      <span class="synx-node-status is-true">● LT 195.0V</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">&lt; 195.0 V</span>
        <span class="synx-node-alias">Under-Voltage Check</span>
        <span class="synx-node-val">Phase Sags Below Limit</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">TRUE</span>
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
        <span class="synx-node-tag">VFLAG0 = 1</span>
        <span class="synx-node-alias">Under-Voltage Latch</span>
        <span class="synx-node-val">Isolate Feeder Contactor</span>
      </div>
      <div class="synx-node-pin is-out" style="visibility: hidden;">Out ●</div>
    </div>
  </div>
</div>

<div class="synx-node-chain" style="margin-top: 12px;">
  <div class="synx-canvas-node is-input">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M2,5 L14,5 M14,5 L10,2 M14,5 L10,8 M14,11 L2,11 M2,11 L6,8 M2,11 L6,14" /></svg>
        <span>MB_READ</span>
      </div>
      <span class="synx-node-status is-live">● OFFSET 2</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in" style="visibility: hidden;">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">VREG_ActiveKw</span>
        <span class="synx-node-alias">Total Active Power</span>
        <span class="synx-node-val">Float32 MidBig CDAB</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge">42.8 kW</span>
  </div>

  <div class="synx-canvas-node is-trigger">
    <div class="synx-node-header">
      <div class="synx-node-header-left">
        <svg class="synx-node-icon" viewBox="0 0 16 16"><path d="M8,14 L8,8 M5,10 A4,4 0 0 1 11,10 M3,7.5 A7,7 0 0 1 13,7.5 M1.5,5 A9.5,9.5 0 0 1 14.5,5" /></svg>
        <span>MQTT_PUB</span>
      </div>
      <span class="synx-node-status is-active">● TELEMETRY</span>
    </div>
    <div class="synx-node-body">
      <div class="synx-node-pin is-in">● In</div>
      <div class="synx-node-content">
        <span class="synx-node-tag">synaptix/power</span>
        <span class="synx-node-alias">Cloud Telemetry</span>
        <span class="synx-node-val">Substation Monitoring</span>
      </div>
      <div class="synx-node-pin is-out">Out ●</div>
    </div>
  </div>

  <div class="synx-node-wire">
    <div class="synx-wire-line"></div>
    <span class="synx-wire-badge is-bool">BUS_OK</span>
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
        <span class="synx-node-tag">DO2 = 0</span>
        <span class="synx-node-alias">Bus Health Lamp</span>
        <span class="synx-node-val">Clear Bus Alarm</span>
      </div>
      <div class="synx-node-pin is-out" style="visibility: hidden;">Out ●</div>
    </div>
  </div>
</div>

**Configuration Steps:**
1. Configure `RS-485 #1` to `9600 bps, 8E1, Master` in [RS-485 Config & SCADA Slave Map](/tools/rs485-and-scada-slave).
2. Add **`MB_READ`** on the canvas and set `PortIndex = RS-485 #1`, `SlaveId = 2`, `FunctionCode = FC 03`, `StartAddress = 3028`, `RegCount = 4`, `ReadMode = Cyclic`, `PollIntervalMs = 1000`, `TimeoutMs = 400`.
3. Add two dynamic output pins:
   - `Voltage_V`: `RegOffset = 0`, `DataType = REAL`, `Endianness = MidBig_CDAB`, `Multiplier = 1.0`, `TargetTag = VREG_GridVolt` (`REAL`).
   - `Power_kW`: `RegOffset = 2`, `DataType = REAL`, `Endianness = MidBig_CDAB`, `Multiplier = 1.0`, `TargetTag = VREG_ActiveKw` (`REAL`).
4. Wire `Voltage_V` to [`COMPARATOR`](/blocks/comparator), `Power_kW` to [`MQTT_PUB`](/blocks/mqtt-pub), and `Error` to an indicator output so bus disconnects are immediately visible to plant operators.
