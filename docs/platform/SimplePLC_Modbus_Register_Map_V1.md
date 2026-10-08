# SimplePLC Platform V1 — Modbus Register Map Specification

**Status:** FROZEN  
**Platform Release:** SimplePLC Platform V1.9  
**Transport:** Modbus RTU over Serial / USB-CDC  
**Data Word Format:** 16-bit registers, Big-Endian  
**Supported Function Codes:** FC03 (Read Holding Registers), FC06 (Write Single Register), FC16 (Write Multiple Registers)

---

## 1. Register Address Space Overview

The Modbus 16-bit register map is organized into two primary address spaces:
1. **Core & Runtime Space (`0x0000 - 0x0A02`)**: Device identity, self-describing resource profile, active rule execution table, health diagnostics, live tag monitoring, and maintenance commands.
2. **Staging & Commit Space (`0x9000 - 0xA001`)**: Atomic staging buffer, CRC verification, and transactional commit handshaking for dynamic rule deployments.

---

## 2. Core & Runtime Address Space (`0x0000 - 0x0A02`)

| Address Range | Length | Name | Access | Function Code | Description |
|---|---|---|---|---|---|
| `0x0000..0x0009` | 10 | **Device Descriptor** | RO | FC03 | `device_class`, `device_variant`, HW version (maj, min, patch), FW version (maj, min, patch), protocol version (2), rule format (7). |
| `0x0010` | 1 | **Rule Table Info** | RO | FC03 | Active rule count currently executing on device. |
| `0x0020..0x0029` | 10 | **Device Resource Info** | RO | FC03 | Self-describing profile: `wire_profile` (2), `max_rules`, `runtime_tag_count`, `di_count`, `do_count`, `ai_count`, `vflag_count`, `vreg_count`, `vreg_retain_count`, `counter_count`. |
| `0x0100..0x073F` | 1600 | **Active Rule Table** | RO | FC03 | Currently active rules. 100 rules maximum × 16 registers (32 bytes) per rule. |
| `0x0800..0x0809` | 10 | **Device Health** | RO | FC03 | `uptime_s` (u32), `reset_reason` (u16), `health_flags` (u16), `cpu_load` (u16), `ram_usage` (u16), `scan_time_ms` (u32), `max_scan_time_ms` (u32). |
| `0x0810..0x0813` | 4 | **RTC Clock** | RW | FC03 / FC16 | Real-Time Clock: `epoch_utc_s` (u32), `tz_offset_min` (i16), `rtc_flags` (u16: SYNCED 0x1, HW_PRESENT 0x2, BATTERY_LOW 0x4). Read-Before-Write protection. |
| `0x0900..0x09FF` | 256 | **Runtime Tag Values** | RW | FC03 / FC16 | Live 32-bit values of up to 128 runtime tags (`0x0900 + TagIndex * 2`). High Word first. Direct writes protected by Diag Safety Interlock. |
| `0x0A00` | 1 | **System Command** | WO | FC06 / FC16 | System command request (`1: REBOOT`, `2: CLEAR_FAULTS`, `3: FACTORY_RESET`). |
| `0x0A01..0x0A02` | 2 | **System Command Result**| RO | FC03 | Execution result: `[0]=CommandStatus` (0=IDLE, 1=ACCEPTED, 2=BUSY, 3=DONE, 4=ERROR), `[1]=ErrorCode`. |
| `0x0A20..0x0A24` | 5 | **Diagnostic Control Block**| RW | FC03 / FC16 | Diagnostic subsystem (V2): `[0]=DIAG_COMMAND` (1=ENTER, 2=HEARTBEAT, 3=EXIT, 4=COMMIT_RETAIN, 5=DISCARD), `[1]=STATE`, `[2]=FLAGS`, `[3]=LEASE_MS` (3000ms), `[4]=ERROR`. |
| `0x0B00..0x0B3F` | 64 | **Dedicated FB Timers** | RW | FC03 / FC16 | 8 dedicated IEC 61131-3 Timers (TON, TOF, TP), 8 registers each (Mode, PresetMs, ElapsedMs, IN, Reset, Running, Q). |
| `0x0B40..0x0B7F` | 64 | **Dedicated FB Counters** | RW | FC03 / FC16 | 8 dedicated IEC 61131-3 Counters (CTU, CTD, CTUD), 8 registers each (Mode, PV, CV, CU, CD, Reset, Load, QU, QD). |

---

## 3. Staging & Commit Address Space (`0x9000 - 0xA001`)

| Address Range | Length | Name | Access | Function Code | Description |
|---|---|---|---|---|---|
| `0x9000` | 1 | **Config Status** | RO | FC03 | Staging handshake state (`0=IDLE, 1=RECEIVING, 2=VERIFYING, 3=READY, 4=ERROR`). |
| `0x9001` | 1 | **Config Error Code** | RO | FC03 | Staging error code (`SPLC_ErrorCode_t`). |
| `0x9002` | 1 | **Rule Count Staged** | RW | FC16 / FC03 | Declared count of rules to stage (1..100). |
| `0x9003` | 1 | **Expected CRC16** | RW | FC16 / FC03 | Expected Rule Table Payload CRC16 over staged rule byte array. |
| `0x9004` | 1 | **Active Rule Count** | RO | FC03 | Mirrored count of currently active committed rules. |
| `0x9005` | 1 | **Active Rule CRC16** | RO | FC03 | Mirrored CRC16 of currently active committed rules. |
| `0x9006..0x900F` | 10 | **Staging Reserved** | — | — | Reserved for future staging handshake extensions. |
| `0x9010..0x964F` | 1600 | **Staging Rule Buffer** | WO | FC16 | Staging buffer memory for newly uploaded rules (written in chunks, typically 120 registers per frame). |
| `0xA000` | 1 | **Commit Command** | WO | FC06 / FC16 | Trigger atomic verification & pointer swap by writing `0xA5A5` (`SPLC_COMMIT_MAGIC`). |
| `0xA001` | 1 | **Active Rule Version** | RO | FC03 | Increments monotonically by 1 upon each successful atomic commit. |

---

## 4. Runtime Tag Address Formulas & Dynamic Layout

### 4.1 Modbus Register Address Formula
For any tag with index `TagIndex` (0..127):
```text
ModbusAddress = 0x0900 + TagIndex * 2
High Word = Memory[ModbusAddress]
Low Word  = Memory[ModbusAddress + 1]
Value     = (int32)((High Word << 16) | Low Word)
```

### 4.2 Dynamic Tag Index Layout (`TagLayoutMap`)
Starting with Platform V2.0, tag indices are no longer fixed to 8-slot boundaries. Tag Base Indices are calculated dynamically by packing declared resources consecutively based on `DeviceResourceInfo` (0x0020):

```text
DiBase         = 0
DoBase         = di_count
AiBase         = di_count + do_count
VflagBase      = di_count + do_count + ai_count
VregBase       = di_count + do_count + ai_count + vflag_count
VregRetainBase = di_count + do_count + ai_count + vflag_count + vreg_count
CounterBase    = di_count + do_count + ai_count + vflag_count + vreg_count + vreg_retain_count
TotalTags      = di_count + do_count + ai_count + vflag_count + vreg_count + vreg_retain_count + counter_count
```

#### Example 1: Full Standard Board (8 DI, 8 DO, 4 AI, 32 VFLAG, 32 VREG, 32 VREG_R, 8 COUNTER)
- `DI0..DI7`: TagIndex `0..7` → `0x0900..0x090F`
- `DO0..DO7`: TagIndex `8..15` → `0x0910..0x091F`
- `AI0..AI3`: TagIndex `16..19` → `0x0920..0x0927`
- `VFLAG0..31`: TagIndex `20..51` → `0x0928..0x0967`
- `VREG0..31`: TagIndex `52..83` → `0x0968..0x09A7`
- `VREG_RETAIN0..31`: TagIndex `84..115` → `0x09A8..0x09E7`
- `COUNTER0..7`: TagIndex `116..123` → `0x09E8..0x09F7`

#### Example 2: Compact Controller Board (4 DI, 4 DO, 2 AI)
- `DI0..DI3`: TagIndex `0..3` → `0x0900..0x0907`
- `DO0..DO3`: TagIndex `4..7` → `0x0908..0x090F` *(Seamlessly follows DI without unused gaps)*
- `AI0..AI1`: TagIndex `8..9` → `0x0910..0x0913`
- `VFLAG0..31`: TagIndex `10..41` → `0x0914..0x0953`
- `VREG0..31`: TagIndex `42..73` → `0x0954..0x0993`
- `VREG_RETAIN0..31`: TagIndex `74..105` → `0x0994..0x09D3`
- `COUNTER0..7`: TagIndex `106..113` → `0x09D4..0x09E3`

