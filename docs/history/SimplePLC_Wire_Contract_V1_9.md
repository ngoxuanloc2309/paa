# SimplePLC Platform V1.9 — Frozen Wire Contract Specification

**Status:** FROZEN  
**Platform Release:** SimplePLC Platform V1.9  
**Protocol Version:** 1  
**Rule Format Version:** 7 (32-byte records)  
**Wire Profile:** 1 (Self-Describing V1)

---

## 1. Overview & Architectural Principles

SimplePLC Platform V1.9 freezes the binary wire protocol between SimplePLC Studio (Host App) and Embedded Microcontrollers (MCU: STM32, ESP32, RP2040).

### Key Architectural Invariants
1. **MCU is the Source of Truth for Capabilities**: The device reports its physical and virtual resources via `DeviceResourceInfo` (`0x0020`). Studio dynamically builds the `ProductDefinition` without requiring product-specific C# hardcoded factories.
2. **Fixed Base Index Mapping**: Regardless of whether a device implements 2 DI or 8 DI, `DI0` is always at `TagIndex 0`, `DO0` is always at `TagIndex 8`, `AI0` at `16`, `VFLAG0` at `20`, `VREG0` at `52`, `VREG_RETAIN0` at `84`, and `COUNTER0` at `116`. Unused intermediate slots do not shift the base addresses of other resource kinds.
3. **Dual-Layer CRC Distinction**:
   - **Rule Table Payload CRC16**: Modbus CRC-16 (Polynomial `0xA001`, Init `0xFFFF`) computed strictly over the serialized byte stream of staged `RuleRecord[]` (`ruleCount * 32` bytes).
   - **Modbus RTU Frame CRC16**: Serial transport frame checksum appended to the last 2 bytes of every RTU communication frame (`[SlaveID, FC, ..., CRC_Lo, CRC_Hi]`).

### Authority Hierarchy (Source of Truth)

In case of any discrepancy or conflict across artifacts:

```text
SOURCE OF TRUTH (Primary Authority):
  1. Platform Contract V1.9 Docs (docs/platform/)
  2. C# Protocol Freeze Tests + Canonical Golden Vectors (tests/SimplePLC.Protocol.Tests/)
  3. C# Protocol Implementation (src/SimplePLC.Protocol/)

REFERENCE ARTIFACT (Secondary / Non-Authoritative):
  4. C Header (src/SimplePLC.Protocol/Firmware/simpleplc_protocol_reference_v1_9.h)
```

> [!IMPORTANT]
> The C header `simpleplc_protocol_reference_v1_9.h` is provided as an embedded integration aid. If the C header deviates from the Golden Vectors or Platform Contract docs, **the Platform Contract and Golden Vectors win unconditionally**.

---

## 2. Platform Constants & Capacities

| Identifier | Value | Description |
|---|---|---|
| `SPLC_PROTOCOL_VERSION` | `1` | Wire protocol version |
| `SPLC_RULE_FORMAT_VERSION` | `7` | 32-byte RuleRecord wire layout version |
| `SPLC_WIRE_PROFILE_V1` | `1` | Self-describing device wire profile identifier |
| `SPLC_REGISTERS_PER_RULE` | `16` | Registers per rule record (32 bytes) |
| `SPLC_MAX_RULES` | `100` | Maximum active / staging rule count |
| `SPLC_MAX_RUNTIME_TAGS` | `128` | Maximum wire tag slots in V1 |
| `SPLC_REGISTERS_PER_TAG` | `2` | 2 registers per tag value (32-bit Big-Endian) |
| `SPLC_COMMIT_MAGIC` | `0xA5A5` | Magic command word written to `0xA000` |
| `SPLC_GUARD_NEGATE_MASK` | `0x8000` | Bit 15 of `guard_tag` indicates negated evaluation |
| `SPLC_GUARD_TAG_MASK` | `0x7FFF` | Bit 0..14 of `guard_tag` contains the tag index |
| `SPLC_GUARD_TAG_NONE` | `0x7FFF` | Sentinel value in bit 0..14 indicating NO GUARD configured |

---

## 3. Wire Profile V1 Resource Capacities & Base Indices

> [!NOTE]
> **Archived Contract Note (Platform V2.1+)**: The fixed base indices below apply strictly to legacy Wire Profile V1 devices. Modern SimplePLC devices (V2.1+) utilize **Dynamic Tag Index Packing** (`TagLayoutMap`), where `DO0` immediately succeeds `DI(N-1)` without empty slots. See [`SimplePLC_Modbus_Register_Map_V1.md`](SimplePLC_Modbus_Register_Map_V1.md) Section 4.2 for the dynamic formula.

| Resource | Maximum Capacity | Fixed Base Index (V1 Legacy) | TagIndex Range | Modbus Address Range |
|---|---|---|---|---|
| Digital Inputs (`DI`) | 8 | `0` | `0..7` | `0x0900..0x090F` |
| Digital Outputs (`DO`) | 8 | `8` | `8..15` | `0x0910..0x091F` |
| Analog Inputs (`AI`) | 4 | `16` | `16..19` | `0x0920..0x0927` |
| Virtual Flags (`VFLAG`) | 32 | `20` | `20..51` | `0x0928..0x0967` |
| Virtual Registers (`VREG`) | 32 | `52` | `52..83` | `0x0968..0x09A7` |
| Retentive Registers (`VREG_RETAIN`)| 32 | `84` | `84..115` | `0x09A8..0x09E7` |
| Counters (`COUNTER`) | 8 | `116` | `116..123` | `0x09E8..0x09F7` |
| Reserved Slots | 4 | `124` | `124..127` | `0x09F8..0x09FF` |
| **Total Wire Capacity** | **128 Tags** | — | `0..127` | `0x0900..0x09FF` |


---

## 4. Binary Wire Data Structures

### 4.1 Device Descriptor (0x0000, 10 registers / 20 bytes, RO)
Big-Endian 16-bit word order:
1. `[0]` `device_class`: `SPLC_DeviceClass_t` (0=UNKNOWN, 1=REMOTE_IO, 2=DATALOGGER, 3=GATEWAY, 4=CONTROLLER)
2. `[1]` `device_variant`: uint16 product variant identifier
3. `[2]` `hw_version_major`: uint16
4. `[3]` `hw_version_minor`: uint16
5. `[4]` `hw_version_patch`: uint16
6. `[5]` `fw_version_major`: uint16
7. `[6]` `fw_version_minor`: uint16
8. `[7]` `fw_version_patch`: uint16
9. `[8]` `protocol_version`: uint16 (Must be 1)
10. `[9]` `rule_format_version`: uint16 (Must be 7)

### 4.2 Device Resource Info (0x0020, 10 registers / 20 bytes, RO)
1. `[0]` `wire_profile`: uint16 (Must be 1)
2. `[1]` `max_rules`: uint16 (0..100)
3. `[2]` `runtime_tag_count`: uint16 (Must match sum of declared tags)
4. `[3]` `di_count`: uint16 (0..8)
5. `[4]` `do_count`: uint16 (0..8)
6. `[5]` `ai_count`: uint16 (0..4)
7. `[6]` `vflag_count`: uint16 (0..32)
8. `[7]` `vreg_count`: uint16 (0..32)
9. `[8]` `vreg_retain_count`: uint16 (0..32)
10. `[9]` `counter_count`: uint16 (0..8)

### 4.3 Device Health (0x0800, 10 registers / 20 bytes, RO)
1. `[0..1]` `uptime_s`: uint32 (Seconds since boot, High Word first)
2. `[2]` `reset_reason`: uint16 (0=UNKNOWN, 1=POWER_ON, 2=SOFTWARE, 3=WATCHDOG, 4=BROWNOUT, 5=EXTERNAL)
3. `[3]` `health_flags`: uint16 bitmask (0x0001=CPU_HIGH, 0x0002=RAM_HIGH, 0x0004=SCAN_OVERRUN)
4. `[4]` `cpu_load_percent`: uint16 (0..100 %)
5. `[5]` `ram_usage_percent`: uint16 (0..100 %)
6. `[6..7]` `scan_time_ms`: uint32 (Scan cycle duration in ms, nominal 10 ms)
7. `[8..9]` `max_scan_time_ms`: uint32 (Peak scan cycle duration in ms)

### 4.4 Rule Record V1.7 (16 registers / 32 bytes, R/W)
1. `[0..1]` `threshold_lo`: int32 (High Word first)
2. `[2..3]` `threshold_hi`: int32 (High Word first)
3. `[4..5]` `for_ms`: uint32 (High Word first)
4. `[6..7]` `action_param`: int32 (High Word first)
5. `[8]` `trigger_tag`: uint16 (0..127)
6. `[9]` `action_tag`: uint16 (0..127)
7. `[10]` `guard_tag`: uint16 (bit 15: NEGATE, bit 0..14: tag index 0..127, or 0x7FFF for NO GUARD)
8. `[11]` High byte: `enabled` (0 or 1), Low byte: `trigger_type` (0..4)
9. `[12]` High byte: `compare_op` (0..7), Low byte: `action_type` (0..7)
10. `[13..15]` `reserved[6]`: 3 registers (Sender writes 0x0000; receiver ignores)

---

## 5. Frozen Numeric Enums

### 5.1 SPLC_ConfigStatus (0x9000)
- `0`: `IDLE` — Staging buffer inactive
- `1`: `RECEIVING` — Staging payload chunk received
- `2`: `VERIFYING` — Validating rule completeness & CRC-16
- `3`: `READY` — Atomic commit successful, active version incremented
- `4`: `ERROR` — Commit or payload transfer failed

### 5.2 SPLC_CommandStatus (0x0A01)
- `0`: `IDLE`
- `1`: `ACCEPTED`
- `2`: `BUSY`
- `3`: `DONE`
- `4`: `ERROR`

### 5.3 SPLC_ErrorCode (0x9001 / 0x0A02)
- `0`: `NONE`
- `1`: `INVALID_COMMAND`
- `2`: `INVALID_PARAMETER`
- `3`: `BUSY`
- `4`: `CRC_MISMATCH`
- `5`: `UNSUPPORTED`
- `6`: `FLASH`

### 5.4 SPLC_TriggerType (RuleRecord[11] Low Byte)
- `0`: `ON_CHANGE`
- `1`: `ON_RISE`
- `2`: `ON_FALL`
- `3`: `TIME_WINDOW`
- `4`: `INTERVAL`

### 5.5 SPLC_CompareOp (RuleRecord[12] High Byte)
- `0`: `NONE`
- `1`: `EQ`
- `2`: `NEQ`
- `3`: `GT`
- `4`: `LT`
- `5`: `GTE`
- `6`: `LTE`
- `7`: `BETWEEN`

### 5.6 SPLC_ActionType (RuleRecord[12] Low Byte)
- `0`: `SET_TAG`
- `1`: `TOGGLE_TAG`
- `2`: `INC_COUNTER`
- `3`: `WRITE_REMOTE`
- `4`: `LOG_EVENT`
- `5`: `SEND_ALARM`
- `6`: `ADD_TAG`
- `7`: `SCALE_TAG`

### 5.7 TagKind (Domain & Contract Specification Mục 3.2)
- `0`: `None`
- `1`: `DiscreteInput`
- `2`: `DiscreteOutput`
- `3`: `AnalogInput`
- `4`: `VirtualFlag`
- `5`: `VirtualRegister`
- `6`: `ModbusCoil`
- `7`: `ModbusHolding`
- `8`: `VirtualRegisterRetain`
- `9`: `Counter`
