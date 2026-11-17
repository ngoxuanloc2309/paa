# SimplePLC Specification: System Tags & Clock Registers ICD
**Document ID:** `SPLC_SPEC_SYSTEM_TAGS_006_ICD`  
**Version:** 1.0.0  
**Target Architecture:** SimplePLC Firmware V2+ & SynaptiX IDE  
**Compliance Standards:** IEC 61131-3, IEC 61508-3 SIL2/3, Modbus Application Protocol V1.1b3  

---

## 1. Overview & Architectural Motivation

In modern industrial control platforms (Siemens TIA Portal, Rockwell Studio 5000, Beckhoff TwinCAT, CODESYS), PLC runtime engines expose standardized system memory flags and deterministic clock pulses. These **System Tags** eliminate the need to allocate user timer blocks or auxiliary virtual flags for common operational requirements such as:
1. First scan initialization routines (`FirstScan` / `SYS_FIRST_SCAN`).
2. Permanent logical True/False references (`AlwaysTRUE` / `AlwaysFALSE` / `SYS_ON` / `SYS_OFF`).
3. Standardized blinkers and flasher clocks for indicator LEDs and tower lights (`Clock_1Hz` / `SYS_CLOCK_1S`, `Clock_10Hz` / `SYS_CLOCK_100MS`).
4. Real-time scheduling hooks without polling overhead (`SYS_RTC_HOUR`, `SYS_RTC_MINUTE`).
5. Hardware communication and link health supervision (`SYS_COMM_OK`).

---

## 2. Modbus Register Map & Memory Layout

System Tags occupy a dedicated, non-overlapping read-only partition within the SimplePLC memory map:
- **Base Modbus Address:** `42081 (0x0820)` (Standard 1-based Holding Register address: 40001 + 2080).
- **Partition Length:** 8 registers (16 bytes).
- **Tag Index Range:** `1000..1007` (Fixed architectural index transmitted to MCU firmware in the 32-byte binary Rule struct as `ushort TriggerTag`, `GuardTag`, `ActionTag`. This ensures universal portability across different hardware models without colliding with user runtime tag channels `0..127`).

| Register Address | Tag Index | Tag Name | Data Type | Access | Cycle Update Rule / Semantics |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `42081 (0x0820)` | `1000` | `SYS_FIRST_SCAN` | `BOOL` | RO | Active `1` during cycle #1; returns `0` on scan #2 and beyond. |
| `42082 (0x0821)` | `1001` | `SYS_ON` | `BOOL` | RO | Constant `1` (Logical TRUE). |
| `42083 (0x0822)` | `1002` | `SYS_OFF` | `BOOL` | RO | Constant `0` (Logical FALSE). |
| `42084 (0x0823)` | `1003` | `SYS_CLOCK_1S` | `BOOL` | RO | Square wave: 500ms ON / 500ms OFF (1.0 Hz, 50% duty cycle). |
| `42085 (0x0824)` | `1004` | `SYS_CLOCK_100MS`| `BOOL` | RO | Square wave: 50ms ON / 50ms OFF (10.0 Hz, 50% duty cycle). |
| `42086 (0x0825)` | `1005` | `SYS_RTC_HOUR` | `INT16` | RO | Real-time hour component (`0..23`) synchronized with RTC (1 register). |
| `42087 (0x0826)` | `1006` | `SYS_RTC_MINUTE` | `INT16` | RO | Real-time minute component (`0..59`) synchronized with RTC (1 register). |
| `42088 (0x0827)` | `1007` | `SYS_COMM_OK` | `BOOL` | RO | Communication link integrity (`1` = healthy, `0` = timeout/fault). |

---

## 3. Compiler & Graph Grammar Enforcement

### 3.1. Direct Connection Grammar (`Input -> Action` & `Input -> Guard`)
In `GraphGrammarV1`, direct edge transitions from `Input` nodes to `Action` and `Guard` nodes are strictly permitted. When an `Input` node represents a digital tag or System Tag, `RuleCompiler` automatically synthesizes an `OnRise` trigger:
$$\text{TriggerKind} = \begin{cases} \text{OnRise}, & \text{if direct Input connection without Trigger node} \\ \text{TriggerNode.Type}, & \text{if explicit Trigger node present} \end{cases}$$

### 3.2. Read-Only Target Safety Guard (`SPLC-COMP-ACT-RO`)
To prevent unintended runtime corruption or undefined firmware behavior:
- Any Action node attempting to write to a tag marked `IsReadOnly = true` (including `TagKind.System`, `DiscreteInput`, `AnalogInput`) is intercepted at compile time.
- Diagnostic emitted: `SPLC-COMP-ACT-RO`: `"Action node '{0}' cannot write to read-only tag '{1}'."`

---

## 4. Deterministic Concurrency & Timing

1. **Scan Cycle Synchronization:** System tags are calculated once at the very start of every scan pass before any rule evaluations or macro block updates.
2. **Zero Allocation:** Update routines execute with zero heap allocations using stack or bitwise modular arithmetic:
   ```csharp
   sysClock1s = (tickMs / 500) % 2 == 0 ? 1 : 0;
   sysClock100ms = (tickMs / 50) % 2 == 0 ? 1 : 0;
   ```
3. **Monotonic Clocks:** Time is measured using monotonic clocks (`ISimulatedClock` / `System.TimeProvider`), completely avoiding non-deterministic `Thread.Sleep()`.

---

## 5. Communication Supervision & Two-Tier Diagnostics (`SYS_COMM_OK`)

### 5.1. Runtime Truth Table & Watchdog Semantics
`SYS_COMM_OK` (Tag 1007, Register `42088 / 0x0827`) provides deterministic link integrity supervision. Rather than relying on simple Layer-2/3 physical link detection (e.g., merely having a Wi-Fi IP address), `SYS_COMM_OK` operates strictly at the application layer:

$$\mathbf{SYS\_COMM\_OK} = \mathbf{Modbus\_Active}(N \le 3000\text{ms}) \;\lor\; \mathbf{MQTT\_Active}$$

1. **Modbus Active:** A valid Modbus frame (matching Slave ID, correct CRC, supported function code) has been received on any physical port (USB CDC, RS-485, or Modbus TCP) within the last $N = 3000\text{ms}$ (`SPLC_COMM_WATCHDOG_DEFAULT_TIMEOUT_MS`).
2. **MQTT Active:** If the MQTT subsystem is enabled, the TCP connection to the broker is established, `CONNACK` is received, and the ping/keepalive loop is healthy.
3. **Standalone / Offline Mode:** If a controller runs autonomously in an electrical cabinet without active fieldbus queries or network telemetry, `SYS_COMM_OK` is `0` (Physical Truth). User safety rules for autonomous machines must NOT use `SYS_COMM_OK` as an operational run permissive.

### 5.2. Two-Tier Diagnostics Architecture (Decoupled Protection & Forensics)
- **Tier 1 (Fast Trip - Runtime Scan 10ms):**
  - Bit-level interlock consumed by user rules.
  - Typical usage: Fail-Safe trip (`Trigger: Periodic 500ms -> Guard: SYS_COMM_OK == 0 -> Action: Safe State`).
- **Tier 2 (Deep Forensics - Studio & Maintenance):**
  - When `SYS_COMM_OK == 0`, SynaptiX Studio and maintenance personnel investigate root cause by querying dedicated diagnostic registers:
    - **Wi-Fi Subsystem (`0x1339` `conn_status`):** `4` = `AuthFailed` (Bad password), `5` = `SsidNotFound` (Router off / Out of range).
    - **Wi-Fi RSSI (`0x133C` `rssi_dbm`):** Weak signal if $< -80\text{dBm}$.
    - **MQTT Subsystem (`0x0E5A` `last_error_code`):** `1` = `DNS_FAILED` (No internet), `2` = `TCP_FAILED`, `3` = `TLS_FAILED`, `4` = `AUTH_REJECTED`, `6` = `TIMEOUT`.
    - **Modbus Silence:** `LastModbusRxElapsedMs > 3000ms` (Cable disconnected or Master software stopped).
