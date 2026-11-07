# Error Codes & Commissioning Troubleshooting

<div class="doc-meta-bar">
  <span><strong>Document ID:</strong> SYNX-DOC-APPX-003</span>
  <span><strong>Scope:</strong> Modbus Exceptions &bull; MQTT/TLS/Wi-Fi/HTTP/TCP Codes &bull; Compiler Diagnostics</span>
  <span><strong>Audience:</strong> Field Commissioning &amp; Maintenance Engineers</span>
  <span><strong>Standard:</strong> IEC/IEEE 82079-1 Structured Fault Isolation</span>
</div>

This appendix consolidates protocol exception codes, wireless/cloud diagnostic states, pre-flight compiler validation errors, and a step-by-step field commissioning fault tree to help engineers isolate and resolve hardware or configuration issues rapidly.

---

## 1. Modbus Protocol Exception Codes & Master Transport Errors

### 1.1 Standard Modbus PDU Exception Responses (`0x01 .. 0x0B`)
When a Modbus Slave (either a SynaptiX unit serving SCADA or a third-party RS-485 instrument polled by [`MB_READ`](/blocks/mb-read) / [`MB_WRITE`](/blocks/mb-write)) rejects a valid-CRC frame, it sets the MSB of the Function Code (`FC | 0x80`, e.g., `0x83` for `FC03`) followed by a 1-byte exception code:

| Hex Code | Standard Exception Name | Root Cause in SynaptiX / Fieldbus Context | Corrective Action |
| :--- | :--- | :--- | :--- |
| `0x01` | `Illegal Function` | Target slave does not support the requested Function Code (e.g., sending `FC06` single-register write to a sensor that only accepts `FC16`, or attempting write on `FC02`/`FC04`). | Switch `MB_WRITE` from `FC06` to `FC16` (required for 32-bit `INT32`/`REAL` registers), or check the instrument datasheet. |
| `0x02` | `Illegal Data Address` | Requested 0-based offset + register quantity falls outside mapped memory or crosses two distinct partitions in the [Fixed SCADA Slave Map](/tools/rs485-and-scada-slave). | Check for `+1` (`40001` vs `0x0000`) addressing offset errors; split cross-partition SCADA reads into separate blocks. |
| `0x03` | `Illegal Data Value` | Attempting to write to a hardware-locked **Read-Only (`RO`)** zone (`DI`, `AI0..AI3`, `SYS_DIAG`), or requesting `0` / `> 125` registers in a single PDU. | Ensure SCADA/Master writes target `RW` zones only (`DO`, `VFLAG`, `VREG`, `VREG_RETAIN`, `COUNTER`). |
| `0x04` | `Server Device Failure` | Unrecoverable internal fault on the downstream slave instrument (e.g., disconnected PT100 sensor element or EEPROM fault). | Inspect the physical field instrument's local display and power supply. |
| `0x06` | `Server Device Busy` | Target device is currently executing an exclusive flash commit or held by another commissioning lease. | Retry after `500..1000 ms` or verify no second IDE instance holds an active write session. |
| `0x0A` / `0x0B` | `Gateway Path Unavailable` / `Target Failed to Respond` | Upstream Modbus TCP-to-RTU gateway reached over IP, but the downstream RS-485 Slave ID did not answer on the serial trunk. | Verify RS-485 Slave ID, baud rate, and A/B wiring behind the gateway. |

### 1.2 RS-485 Master Transport Faults (`MB_READ` / `MB_WRITE` / Wire Inspector)

| Transport Diagnostic | Observed Symptom | Primary Physical Causes | Immediate Fix |
| :--- | :--- | :--- | :--- |
| **`ERR_TIMEOUT`** *(No RX bytes)* | `ResponseLatency = —`, tag turns **`[STALE]`** after `RetryCount`. | Swapped `A(+)` / `B(-)` wires, wrong Slave ID, mismatched Baud Rate, powered-off sensor, or `ResponseTimeoutMs` too short. | Swap `D+/A` and `D-/B` at one end; verify Slave ID and Baud Rate with the **Moxa Wire Inspector (`Read Once`)** in `Ctrl+Shift+D`. |
| **`ERR_CRC_MISMATCH`** | Partial/corrupted hex bytes received; CRC-16 check fails. | Missing `120 Ohm` termination on long cable, VFD EMI coupling on unshielded wire, or duplicate Slave IDs talking simultaneously! | Enable `120 Ohm` termination at both bus ends, ground STP shield at cabinet end only, and verify unique Slave IDs. |
| **`ERR_FRAMING` / `OVERRUN`** | Garbled start/stop bits or truncated tail bytes. | Parity mismatch (`8N1` vs `8E1`), or slave `DE/RE` turnaround slower than master inter-frame gap. | Match exact framing (`8E1` vs `8N1`) and increase `InterFrameDelayMs` from `20 ms` to `35..50 ms`. |

---

## 2. Wi-Fi, MQTT/TLS, HTTP REST & TCP Diagnostic Codes

| Subsystem | Status / Error Code | Meaning | Resolution |
| :--- | :--- | :--- | :--- |
| **Wi-Fi (`Ctrl+Shift+W`)** | `AUTH_FAILED` (`4`) | WPA2/WPA3 Pre-Shared Key rejected by Access Point. | Toggle password eye icon to verify exact case-sensitive Wi-Fi password; confirm AP supports `2.4 GHz` `802.11b/g/n` (`5 GHz`-only SSIDs are invisible to 2.4 GHz industrial radios). |
| **Wi-Fi (`Ctrl+Shift+W`)** | `SSID_NOT_FOUND` (`5`) | Configured SSID not detected in RF scan. | Run **Live AP Scanner** inside the cabinet; attach external SMA antenna if `RSSI <= -78 dBm`. |
| **MQTT / TLS (`Ctrl+Shift+M`)** | `TLS_CERT_INVALID` / `AUTH_EX` | X.509 chain verification failed, expired certificate, or SAN hostname mismatch. | Load the matching Root CA `.pem`/`.crt`, check system/RTC clock year (`>= 2026`), or enable `IgnoreHostname` when connecting to LAN broker by raw IP. |
| **MQTT Broker** | `CONNACK 0x04 / 0x05` | Bad Username/Password or Not Authorized ACL on topic. | Verify broker credentials and ACL publish/subscribe permissions for `ClientId` and topic path. |
| **`MQTT_SUB` Block** | `Err` Pulse / `PARSE_ERR` | Inbound JSON malformed, missing bound `JsonKey`, or non-numeric string sent to `INT32`/`REAL` pin. | Inspect `LastReceivedPayload`; **Atomic All-or-Nothing** protection safely discarded the bad packet. Fix publisher JSON schema. |
| **`HTTP_CLIENT` Block** | `StatusCode 400 / 401 / 403` | Webhook rejected JSON body (`400`) or missing/expired `Authorization` token (`401`/`403`). | Check `PreviewPayloadText` JSON validity and verify `CustomHeaders` (`<= 56 chars`). |
| **`HTTP_CLIENT` Block** | `StatusCode 408 / 5xx / 0` | DNS failure, firewall block, or remote REST API timeout (`> TimeoutSec`). | Confirm Static IP **Gateway** and **DNS (`8.8.8.8`)** are populated when not using DHCP! |
| **`TCP_CLIENT` Block** | `SOCKET_REFUSED / RESET` | Remote server port closed or NAT idle timeout dropped silent socket. | Verify server listener port and enable `HeartbeatIntervalSec = 30 s` (`PING`). |

---

## 3. Pre-Flight Compiler Validation Diagnostics (`F7`)

Pressing `F7` (or `Ctrl+F5`) runs static graph verification before generating bytecode. Common diagnostics include:

| Diagnostic Category | Triggering Graph Condition | How to Resolve on Logic Graph (`Ctrl+2`) |
| :--- | :--- | :--- |
| **Action Missing Input** (`ERR_ACTION_NO_INCOMING`) | An [`ACTION`](/blocks/action) block has no upstream wire connected to its `Exec` input pin. | Connect an `INPUT`, `TRIGGER`, `COMPARATOR`, `GUARD`, or Timer/Counter output pin to `ACTION`, or delete the unused block. |
| **Unconnected Trigger / Guard** (`ERR_TRIGGER_NO_INPUT` / `ERR_GUARD_NO_TRIGGER`) | A `TRIGGER` lacks an `INPUT` source, or a `GUARD` gate has no incoming signal path. | Wire a valid source tag into `TRIGGER`, and place `GUARD` inline between upstream logic and `ACTION`. |
| **Non-Boolean Guard Tag** (`ERR_GUARD_NOT_BOOLEAN`) | A `GUARD` interlock references an `INT32` or `REAL` register instead of a `BOOL` tag. | Bind `GUARD` strictly to a `BOOL` tag (`DI`, `DO`, `VFLAG`), or insert a `COMPARATOR` first to produce a boolean result. |
| **Read-Only Target Violation** (`ERR_ACTION_READONLY_TARGET`) | An `ACTION`, `SCALE`, `MB_READ`, or `MQTT_SUB` attempts to write into a hardware input (`DI`, `AI`, `SYS_*`). | Change the destination tag to a writable `DO`, `VFLAG`, `VREG`, or `VREG_RETAIN` slot. |
| **Conflicting Multi-Writers** (`ERR_CONFLICTING_WRITERS`) | Two unconditional blocks/rules write conflicting values to the same tag on every scan, or two `DIRECT_WRITE` blocks claim the same `DO` pin. | Use mutually exclusive `Set` / `Reset` conditions, consolidate into a single writer, or assign distinct `DO` pins. |
| **Circular Combinational Loop** (`ERR_CIRCULAR_DEPENDENCY`) | Combinational nodes form a zero-delay feedback cycle (`Tag A -> Tag B -> Tag A`) with no stateful boundary. | Break combinational loops through an explicit stateful block (`TON`, `CTU`) or edge `TRIGGER`. |
| **Master Block on Slave Port** (`ERR_MB_SLAVE_PORT_CONFLICT`) | [`MB_READ`](/blocks/mb-read) or [`MB_WRITE`](/blocks/mb-write) is assigned to an RS-485 port configured as **Modbus RTU Slave**. | Route the Master block to a second RS-485 port (`RS-485 #2`) or switch the port role back to **Master** in `Ctrl+Shift+B`. |
| **Resource Capacity Exceeded** (`ERR_CAPACITY_EXCEEDED`) | Graph exceeds preset limits (`MaxRules`, `8` Timers, `8` Counters, `4` `MQTT_PUB`, `2` `MQTT_SUB`, or `4` `MB_READ`). | Consolidate single-variable blocks into multi-pin/multi-field batch blocks, or upgrade to a higher [Device Preset](/appendices/macro-tokens-and-limits). |

---

## 4. Step-by-Step Field Commissioning Fault Tree

| Fault Symptom | Root Cause | 3-Step Field Resolution Playbook |
| :--- | :--- | :--- |
| **USB CDC Port Missing** | Power-only USB cable (lacks D+/D- lines) or another serial monitor/terminal process has locked COM exclusive access. | 1. Swap to a verified shielded data USB cable.<br/>2. Check Windows Device Manager under Ports (COM & LPT).<br/>3. Close PuTTY/ModScan and press **F4** to rescan. |
| **Modbus TCP (:502) Connection Refused** | IP subnet mismatch, duplicate static IP address, or plant VLAN firewall blocking port 502. | 1. Run `ping <IP>` from command prompt to verify layer-3 reachability.<br/>2. Ensure laptop NIC shares the same subnet (e.g. `192.168.1.x /24`).<br/>3. Whitelist inbound TCP port 502 on industrial switches. |
| **Fieldbus Tags Flagged [STALE]** | Sensor 24V supply off, reversed RS-485 polarity, baud/parity mismatch, or off-by-one register addressing. | 1. Press **Ctrl+Shift+D** Wire Inspector and click *Read Once*.<br/>2. If 0 RX bytes: swap RS-485 A(+) and B(-) lines.<br/>3. If CRC error: match parity (8N1/8E1) and enable 120Ω bus termination. |

### Quick Resolution Playbooks

1. **`USB CDC` Virtual COM Port Not Detected in SynaptiX IDE:**
   - **Charge-Only Cable Trap:** Nearly 60% of field USB issues stem from power-only USB cables lacking `D+/D-` data lines. Swap to a verified data cable and inspect **Windows Device Manager -> Ports (COM & LPT)**.
   - **Exclusive Port Lock:** Windows allows only one process to open a `COM` port at a time. Close PuTTY, ModScan, Arduino Serial Monitor, or any second IDE window before pressing `F4`.
2. **`Modbus TCP` Port `502` Connection Refused / Timed Out:**
   - Confirm your workstation NIC is on the same IPv4 subnet as the SynaptiX unit (e.g., `192.168.1.x / 24`) and run `ping <device-ip>` in PowerShell.
   - Verify no corporate firewall or managed switch ACL blocks inbound TCP port `502`. Always assign a **Static IPv4** (`Ctrl+Shift+W` or Device tab) for SCADA/TCP targets.
3. **Fieldbus Tags Flagged `[STALE]` During Live Debug (`F8`):**
   - Open `Ctrl+Shift+D`, select the suspect channel, expand the **Moxa Wire Frame Inspector**, and click **Read Once**:
     - **Zero RX bytes:** Check 24V sensor supply, confirm Slave ID, and test swapping `RS-485 A(+)` and `B(-)`.
     - **Exception `0x02`:** Reduce multi-register span count or subtract/add `40001` base offset.

---

## Related Documentation

- [RS-485 Config & SCADA Slave Map (`Ctrl+Shift+B`)](/tools/rs485-and-scada-slave)
- [MQTT TLS & Wi-Fi Provisioning (`Ctrl+Shift+M` / `W`)](/tools/mqtt-and-wifi-config)
- [Simulator (`F5`) & Online Live Debug (`F8`)](/tools/simulator-and-live-debug)
