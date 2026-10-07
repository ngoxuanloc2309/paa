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
| `0x0000..0x0009` | 10 | **Device Descriptor** | RO | FC03 | `device_class`, `device_variant`, HW version (maj, min, patch), FW version (maj, min, patch), protocol version (1), rule format (7). |
| `0x0010` | 1 | **Rule Table Info** | RO | FC03 | Active rule count currently executing on device. |
| `0x0020..0x0029` | 10 | **Device Resource Info** | RO | FC03 | Self-describing profile: `wire_profile`, `max_rules`, `runtime_tag_count`, `di_count`, `do_count`, `ai_count`, `vflag_count`, `vreg_count`, `vreg_retain_count`, `counter_count`. |
| `0x0100..0x073F` | 1600 | **Active Rule Table** | RO | FC03 | Currently active rules. 100 rules maximum × 16 registers (32 bytes) per rule. |
| `0x0800..0x0809` | 10 | **Device Health** | RO | FC03 | `uptime_s` (u32), `reset_reason` (u16), `health_flags` (u16), `cpu_load` (u16), `ram_usage` (u16), `scan_time_ms` (u32), `max_scan_time_ms` (u32). |
| `0x0900..0x09FF` | 256 | **Runtime Tag Values** | RW | FC03 / FC16 | Live 32-bit values of up to 128 runtime tags (`0x0900 + TagIndex * 2`). High Word first. |
| `0x0A00` | 1 | **System Command** | WO | FC06 / FC16 | System command request (`1: REBOOT`, `2: FACTORY_RESET`, `3: CLEAR_RULES`, `4: CLEAR_RETAIN`). |
| `0x0A01..0x0A02` | 2 | **System Command Result**| RO | FC03 | Execution result: `[0]=CommandStatus` (0=IDLE, 1=ACCEPTED, 2=BUSY, 3=DONE, 4=ERROR), `[1]=ErrorCode`. |

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

## 4. Runtime Tag Address Formulas

For any tag with index `TagIndex` (0..127):
```text
ModbusAddress = 0x0900 + TagIndex * 2
High Word = Memory[ModbusAddress]
Low Word  = Memory[ModbusAddress + 1]
Value     = (int32)((High Word << 16) | Low Word)
```

Example Addresses:
- `DI0` (TagIndex 0): `0x0900..0x0901`
- `DO0` (TagIndex 8): `0x0910..0x0911`
- `AI0` (TagIndex 16): `0x0920..0x0921`
- `VFLAG0` (TagIndex 20): `0x0928..0x0929`
- `VREG0` (TagIndex 52): `0x0968..0x0969`
- `VREG_RETAIN0` (TagIndex 84): `0x09A8..0x09A9`
- `COUNTER0` (TagIndex 116): `0x09E8..0x09E9`
- `RESERVED` (TagIndex 127): `0x09FE..0x09FF`
