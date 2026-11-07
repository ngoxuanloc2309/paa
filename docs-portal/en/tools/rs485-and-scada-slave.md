# RS-485 Bus Configuration & SCADA Slave Register Map

<div class="doc-meta-bar">
  <span><strong>Document ID:</strong> SYNX-DOC-TOOL-001</span>
  <span><strong>Shortcut:</strong> <code>Ctrl+Shift+B</code></span>
  <span><strong>Protocols:</strong> Modbus RTU (TIA/EIA-485-A) &bull; Modbus TCP (Port 502)</span>
  <span><strong>Standard:</strong> Modbus Application Protocol V1.1b3 &bull; IEC 61131-3</span>
</div>

Every **SynaptiX Edge Controller**, **Remote I/O Unit**, and **DTU Gateway** equipped with physical TIA/EIA-485 transceivers supports independent per-port configuration as either a **Modbus RTU Master (Client)** or a **Modbus RTU Slave (Server)**. Furthermore, network-capable models simultaneously expose the same deterministic **Public Fixed SCADA Slave Register Map** over **Modbus TCP (Port 502)** for zero-latency integration with enterprise SCADA, DCS, and industrial HMI platforms.

---

## Part 1: RS-485 Bus Configuration Dialog (`Ctrl+Shift+B`)

Press `Ctrl+Shift+B` from any workspace (or click **Device -> RS-485 Port Configuration...**) to open the **RS-485 Modbus Ports Configuration** dialog. Depending on your selected hardware preset, SynaptiX IDE exposes `RS-485 #1` through `RS-485 #4` (`PortIndex 0..3`) in the left port selector list.

### Quick 3-Step RS-485 Port Setup

1. **Step 1 — Open the Dialog:** Press `Ctrl+Shift+B` on your keyboard.
2. **Step 2 — Assign the Port Role:**
   - Select **Modbus RTU Master**: Use this role when the controller needs to actively poll energy meters, VFDs, or sensors using [`MB_READ`](/blocks/mb-read) and [`MB_WRITE`](/blocks/mb-write) blocks.
   - Select **Modbus RTU Slave**: Use this role when serving data up to an on-panel touch HMI or plant SCADA system via a dedicated Slave ID (`1..247`).
3. **Step 3 — Set Baudrate & Save:** Choose your serial baudrate (`9600`, `19200`, or `115200`), Parity (`None`, `Even`, `Odd`), and click **Save Configuration**.

### 1.1 Per-Port Operating Role: Master vs. Slave

Each physical RS-485 transceiver is half-duplex and must operate in one deterministic role at a time:

| Operating Role | Bus Behavior | Allowed Function Blocks | Typical Use Case |
| :--- | :--- | :--- | :--- |
| **Modbus RTU Master** | Initiates cyclic polling (`FC01..FC04`) and event/COV actuation writes (`FC05, FC06, FC15, FC16`) to downstream field slaves. | [`MB_READ`](/blocks/mb-read), [`MB_WRITE`](/blocks/mb-write) | Polling energy meters, environmental sensors, load cells, and commanding VFDs or valve manifolds. |
| **Modbus RTU Slave (Server)** | Passively listens for incoming requests addressed to its **Slave ID (`1..247`)** and serves the Fixed SCADA Register Map directly from RAM in `O(1)` time. | None on this port (guarded by compiler) | Serving local panel HMIs (Weintek, Pro-face, Siemens Comfort) or upstream supervisory masters. |

::: danger Compiler Safety Gate — Master/Slave Port Conflict Prevention
When an RS-485 port (for example, `RS-485 #1`) is assigned the **Modbus RTU Slave** role, the SynaptiX compiler enforces a strict hardware-exclusivity lock. Any [`MB_READ`](/blocks/mb-read) or [`MB_WRITE`](/blocks/mb-write) block targeting that same port triggers a pre-flight compilation error (`ERR_MB_SLAVE_PORT_CONFLICT`), preventing multi-master bus collisions on the physical transceiver.
:::

### 1.2 Serial Framing & Timing Parameters

All nodes sharing the same twisted-pair RS-485 trunk must match identical serial framing parameters:

| Parameter | Supported Values | Default | Engineering Notes |
| :--- | :--- | :--- | :--- |
| **Port Enabled** | `Active` / `Disabled` | `Active` (`#1`, `#2`) | Disables UART transceiver interrupts on unused ports to conserve scan budget. |
| **Baud Rate** | `1200`, `2400`, `4800`, `9600`, `19200`, `38400`, `57600`, `115200` bps | `9600` (`#1`), `19200` (`#2`) | Use `9600` or `19200` bps for cable runs exceeding `300 m`; `38400..115200` bps for short cabinet backplanes. |
| **Data Bits / Parity / Stop Bits** | `8N1`, `8E1`, `8O1`, `8N2` (plus `7E1`/`7O1` legacy) | `8N1` (`#1`), `8E1` (`#2`) | Modbus RTU specification recommends `8E1` (Even parity, 1 stop bit) or `8N2` (No parity, 2 stop bits); `8N1` is widely used by modern field instruments. |
| **Slave ID** *(Slave Role)* | `1 .. 247` | `1` | Must be unique across every device on the RS-485 daisy-chain. Address `0` is reserved for broadcast writes. |
| **Response Timeout** *(Master Role)* | `50 .. 5000 ms` | `200 ms` | Maximum wait window before declaring a downstream slave transaction timed out and flagging `[STALE]` if retries are exhausted. |
| **Inter-Frame / Turnaround Delay** | `5 .. 1000 ms` | `20 ms` | Silent turnaround gap enforced between consecutive frames (always exceeding the Modbus RTU `3.5`-character silent interval `t3.5`) so slower RS-485 transceivers release their `DE/RE` driver lines cleanly. |
| **Retry Count** *(Master Role)* | `0 .. 5` | `1` | Automatic re-transmissions upon CRC mismatch or timeout before incrementing port fault counters. |

### 1.3 Master Channel I/O Table & Type-Safe Tag Filtering

When a port is set to **Modbus RTU Master**, the dialog displays an interactive **Master Channel Table** featuring:
- **Live Hex Frame Preview:** Automatically computes the exact 8-byte outbound RTU request frame including standard **CRC-16/MODBUS** (`[ Slave ] [ FC ] [ OffsetHi OffsetLo ] [ QtyHi QtyLo ] [ CRCLo CRCHi ]`) and expected response footprint.
- **Physical Type & Read-Only Guarding:** Selecting a read function (`FC01..FC04`) automatically hides hardware-sampled read-only tags (`DI`, `AI`, `SYS_*`) so external fieldbus reads never fight local ADC/GPIO hardware sampling. Bit operations (`BOOL`) filter strictly to digital flags (`VFLAG`, `DO`), while `INT16`/`INT32` and `REAL` (`Float32`) filter to compatible `VREG` and `VREG_RETAIN` registers.
- **Cycle Time Estimator:** Continuously estimates total round-robin bus scan duration (`EstimatedScanCycleMs`) from channel count, baud rate, and turnaround delay.

### 1.4 Physical RS-485 Wiring, A/B Polarity & Termination Best Practices

```text
[Master / Supervisory Port]                          [Field Slave #1]              [Field Slave #N]
   +---------------+                                 +---------------+             +---------------+
   |  D+ / A (+)   |---------------------------------|  D+ / A (+)   |------ ... --|  D+ / A (+)   |
   |  D- / B (-)   |---------------------------------|  D- / B (-)   |------ ... --|  D- / B (-)   |
   |  SG / GND     |---------------------------------|  SG / GND     |------ ... --|  SG / GND     |
   +---------------+                                 +---------------+             +---------------+
      [120 Ohm ON]                                      [120 Ohm OFF]                 [120 Ohm ON]
```

::: tip TIA/EIA-485 Field Wiring Checklist
1. **Daisy-Chain Topology Only:** Wire devices strictly in a linear bus (daisy-chain). Avoid star, ring, or long T-stub drops (`> 1 m`), which cause impedance reflections at `19200+ bps`.
2. **`120 Ohm` Line Termination:** Enable `120 Ohm` termination resistors **only at the two physical ends** of the trunk cable (First Node and Last Node). Never enable termination on intermediate nodes, which overloads transceiver drivers.
3. **Shielded Twisted Pair & Single-Point Drain:** Use industrial `AWG 22..24` (`120 Ohm` characteristic impedance) shielded twisted pair (STP). Connect the cable shield drain wire to protective earth (`PE`) at **one end only** (typically the main control cabinet) to prevent ground-loop currents.
4. **Signal Reference (`GND` / `SG`):** Always connect the isolated RS-485 signal reference (`GND`/`SG`) across transceivers to keep common-mode voltage within `-7 V .. +12 V`.
:::

---

## Part 2: Public SCADA / HMI Fixed Modbus Slave Register Map

When a SynaptiX device operates as a **Modbus RTU Slave** (on an RS-485 port) or serves **Modbus TCP on Port `502`** (Unit ID `1`, or any Unit ID `1..255` when TCP gateway bridging is disabled), external SCADA systems access an immutable **Factory Fixed Memory Map** directly backed by deterministic runtime RAM.

### 2.1 Memory Partition & Boundary Protection

To satisfy industrial robustness requirements (IEC 61508-3), the slave listener enforces hardware-level access and partition boundary checks on every incoming PDU:

| Protection Rule | Enforced Invariant | Exception Returned on Violation |
| :--- | :--- | :--- |
| **Read-Only Hardware Protection** | Physical inputs (`DI`, `DI_WORD`, `AI0..AI3`) and system diagnostics (`SYS_DIAG`) are hard-locked against remote writes. | `0x03` (`Illegal Data Value` / Access Denied) or `0x01` (`Illegal Function` on `FC02`/`FC04`) |
| **Unmapped Address Guard** | Any query targeting reserved or unmapped address gaps outside the public SCADA map is rejected immediately. | `0x02` (`Illegal Data Address`) |
| **Cross-Partition Boundary Guard** | A single multi-register read/write (`FC03` / `FC16`) must stay within one logical table partition (e.g., cannot span across `VREG` and `VREG_RETAIN` in one PDU). | `0x02` (`Illegal Data Address`) |
| **32-Bit Atomic Word Alignment** | Writes to 32-bit tags (`VREG`, `VREG_RETAIN`, `COUNTER`) commit atomically via `FC16` (`2` registers per variable) so the logic scan never observes a torn 16-bit half-word. | `0x02` / `0x03` on unaligned partial writes |

---

### 2.2 Bit-Level Tables: Discrete Inputs (`FC02`) & Coils (`FC01`, `FC05`, `FC15`)

SCADA/HMI drivers can read physical inputs and command individual outputs or handshake flags at single-bit granularity:

| Zone & Function Codes | 1-Based Modbus Address | 0-Based Wire Offset (Hex) | SynaptiX Tag Range | Data Type | Access | Description & Preset Scaling |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **Discrete Inputs** (`FC02`) | `10001 .. 10008` *(up to `10016`)* | `0x0000 .. 0x0007` *(`.. 0x000F`)* | `DI0 .. DI7` *(up to `DI15`)* | `BOOL` (`1-bit`) | `RO` | Real-time state of opto-isolated digital input terminals (`0 = OFF`, `1 = ON`). `DI0..DI7` active on standard 8-DI presets. |
| **Physical Output Coils** (`FC01, FC05, FC15`) | `00001 .. 00008` *(up to `00016`)* | `0x0000 .. 0x0007` *(`.. 0x000F`)* | `DO0 .. DO7` *(up to `DO15`)* | `BOOL` (`1-bit`) | `RW` | Read state or directly energize/de-energize physical relay / transistor outputs (`DO0..DO7` on 8-DO models). |
| **Virtual Flag Coils** (`FC01, FC05, FC15`) | `00101 .. 00132` | `0x0064 .. 0x0083` | `VFLAG0 .. VFLAG31` | `BOOL` (`1-bit`) | `RW` | 32 internal boolean flags ideal for SCADA `Start` / `Stop` / `Reset` pushbuttons and permissive interlocks. |

---

### 2.3 Input Registers (`FC04` — `3xxxx`) & Packed/Diagnostic Registers

Analog inputs and system telemetry are readable via **Input Registers (`FC04`, `3xxxx`)** as well as mirrored in the **Holding Register (`FC03`, `4xxxx`)** space so HMIs capable of single-block `FC03` polling can batch-read status words alongside process variables:

| 1-Based Address (`3xxxx` / `4xxxx`) | 0-Based Wire Offset (Hex) | SynaptiX Tag / Block | Data Type | Regs | Access | Detailed Layout & Engineering Units |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `40001` | `0x0000` | `DI_WORD` | `UINT16` | `1` | `RO` | Packed 16-bit bitmask of `DI0..DI15` (`Bit 0 = DI0`, `Bit 7 = DI7`). Ideal for single-word HMI status indicators. |
| `40002` | `0x0001` | `DO_WORD` | `UINT16` | `1` | `RW` | Packed 16-bit bitmask of `DO0..DO15` (`Bit 0 = DO0`, `Bit 7 = DO7`). Supports simultaneous multi-output writes. |
| `40003 .. 40004` | `0x0002 .. 0x0003` | `VFLAG_DWORD` | `UINT32` | `2` | `RW` | Packed 32-bit bitmask of `VFLAG0..VFLAG31` (High Word First). |
| `30017 .. 30024` / `40017 .. 40024` | `0x0010 .. 0x0017` | `AI0 .. AI3` | `FLOAT32` (IEEE-754) | `8` (`2/ch`) | `RO` | 4 analog input channels (`0..10 V` / `4..20 mA`), Big-Endian `ABCD`: `AI0` = `0x0010` (`30017`/`40017`), `AI1` = `0x0012` (`30019`/`40019`), `AI2` = `0x0014` (`30021`/`40021`), `AI3` = `0x0016` (`30023`/`40023`). |
| `30241 .. 30242` / `40241 .. 40242` | `0x00F0 .. 0x00F1` | `SYS_UPTIME` | `UINT32` | `2` | `RO` | Monotonic system uptime counter in seconds since power-on (`ABCD`). |
| `30243` / `40243` | `0x00F2` | `SYS_SCANTIME` | `UINT16` | `1` | `RO` | Actual deterministic logic scan execution time in milliseconds (`ms`). |
| `30244` / `40244` | `0x00F3` | `SYS_STATUS` | `UINT16` | `1` | `RO` | Runtime health & communication status bitmask (`Bit 0 = Run OK`, `Bit 1 = Bus Fault`, `Bit 2 = Cloud Online`). |
| `30245 .. 30250` / `40245 .. 40250` | `0x00F4 .. 0x00F9` | `SYS_RTC` | `UINT16[6]` | `6` | `RO` | Real-Time Clock components: `[Year, Month, Day, Hour, Minute, Second]`. |

---

### 2.4 Holding Registers (`FC03`, `FC06`, `FC16` — `4xxxx`)

All 32-bit process registers, retentive setpoints, and high-speed counters reside in contiguous 2-register slots within the Holding Register space:

| Partition | 1-Based Modbus Range | 0-Based Wire Offset (Hex) | Address Formula (`Index` = `0..N-1`) | Data Type | Regs / Item | Access | Description |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **`VREG0 .. VREG31`** | `40033 .. 40096` | `0x0020 .. 0x005F` | `40033 + (Index * 2)` *(Hex: `0x0020 + Index * 2`)* | `INT32` or `FLOAT32` | `2` (`32-bit`) | `RW` | 32 volatile general-purpose calculation, scaled sensor, and Modbus/MQTT bridge registers. Matches the Tag Catalog data type (`INT32` or `REAL`). |
| **`VREG_RETAIN0 .. VREG_RETAIN31`** | `40097 .. 40160` | `0x0060 .. 0x009F` | `40097 + (Index * 2)` *(Hex: `0x0060 + Index * 2`)* | `INT32` or `FLOAT32` | `2` (`32-bit`) | `RW` | 32 non-volatile retentive registers preserved across power cycles (ideal for alarm thresholds, calibration offsets, and batch recipes). |
| **`COUNTER0 .. COUNTER7`** | `40161 .. 40176` | `0x00A0 .. 0x00AF` | `40161 + (Index * 2)` *(Hex: `0x00A0 + Index * 2`)* | `INT32` | `2` (`32-bit`) | `RW` | 8 event/pulse accumulator registers (`CV`) driven by [`CTU`](/blocks/ctu) / [`CTD`](/blocks/ctd) blocks. Write `0` via `FC16` to remotely reset a production counter. |

::: info Exporting the SCADA Tag Sheet (`CSV` / `Markdown`)
Inside the **RS-485 Configuration Dialog (`Ctrl+Shift+B`)**, switch a port to **Slave** mode, filter by Moxa-style memory tabs (**All**, **Holding Registers 4xxxx**, **Coils 0xxxx**, **Discrete Inputs 1xxxx**), and click **Export Datasheet**. SynaptiX IDE generates a UTF-8 BOM `.csv` spreadsheet or `.md` specification table ready for direct import into SCADA engineering suites.
:::

---

### 2.5 32-Bit Word-Order Conventions (`ABCD` Big-Endian) for Ignition, WinCC & Kepware

All 32-bit integers (`INT32`, `UINT32`) and IEEE-754 single-precision floats (`FLOAT32` / `REAL`) in the SynaptiX Fixed Slave Map use **Network Big-Endian — High Word First, High Byte First (`ABCD`)**:
- **Register `N` (`Offset + 0`):** Bits `31..16` (`Byte A` = MSB, `Byte B`)
- **Register `N + 1` (`Offset + 1`):** Bits `15..0` (`Byte C`, `Byte D` = LSB)

For example, `FLOAT32` value `25.5` (`0x41CC0000`) stored in `VREG0` (`40033..40034`, wire offset `0x0020..0x0021`) transmits `0x41CC` at `40033` and `0x0000` at `40034`.

| SCADA / OPC-UA Platform | Addressing Mode | 32-Bit Word / Byte Swap Setting | Example Tag Syntax for `VREG0` (`REAL` at `40033`) |
| :--- | :--- | :--- | :--- |
| **Inductive Automation Ignition** | 1-Based (`OneBasedAddressing = true`) | `ReverseWordOrder = false` (`ABCD` High Word First) | `[SynaptiX_Unit1]1.HRF33` (`HRF` = Holding Register Float, `HRI` = Int32) |
| **PTC Kepware (KEPServerEX)** | 1-Based (`Modbus` decimal) | **First Word Low = Disabled (`0`)** *(High Word First)* | `400033@FLOAT` or `400033@LONG` (6-digit) / `40033` (5-digit) |
| **Siemens WinCC Unified / TIA Portal** | 1-Based Standard Modbus TCP/RTU | Big-Endian Standard (No Word Swap required) | `%MD` / `4x40033` (`Real` or `DInt` data type) |
| **Weintek EasyBuilder Pro** | 1-Based (`4x` / `0x` / `1x`) | `32-bit Float` / `32-bit Signed` — Conversion `ABCD` (Default) | Device `MODBUS RTU / TCP` -> `4x-40033` (`32-bit Float`) |

::: warning Preventing Off-by-One (`+1`) Addressing Errors
If your SCADA driver uses **0-based hexadecimal/decimal PDU offsets** (such as `pymodbus`, Node-RED Modbus nodes, or Ignition with `Zero-Based Addressing` enabled), subtract `40001` from Holding Register numbers — e.g., poll offset `32` (`0x0020`) for `VREG0`, **not** `33`. Polling offset `33` (`0x0021`) reads the low word of `VREG0` concatenated with the high word of `VREG1`, producing garbled floating-point values!
:::

---

## Related Documentation

- [MB_READ — Modbus RTU Master Poller](/blocks/mb-read)
- [MB_WRITE — Modbus RTU Master Writer](/blocks/mb-write)
- [DTU Gateway Studio (`Ctrl+Shift+D`)](/tools/dtu-gateway-studio)
- [Error Codes & Commissioning Troubleshooting](/appendices/error-codes-and-troubleshooting)
