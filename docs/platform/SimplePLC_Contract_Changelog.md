# SimplePLC Platform Contract Changelog

## [2.2.6] - 2026-10-08 (COUNTER STORAGE BINDING, DYNAMIC TAG MAP & SAVE/LOAD FIX)

### Fixed
- **Counter Storage Register (CV) Binding & Telemetry**:
  - Broadened `RetainTagIndex` binding in `RuleCompiler` to accept any valid storage register (`VREG`, `VREG_RETAIN`, `VFLAG`, `COUNTER`) instead of strictly requiring `VREG_RETAIN`.
  - Firmware now correctly resolves the storage tag index and populates `CurrentValue` in FB Counter telemetry (`0x0B40..0x0B7F`), allowing Canvas Counter nodes to reflect real-time count progression.
  - Automatic fallback to dedicated `COUNTER{i}` tag index when CV is unassigned.
- **Project File Save / Load Graph Preservation**:
  - Fixed issue where saved projects opened as blank canvas by ensuring `RuleTable` loads first and nodes are automatically restored from the project graph or compiled rules.
- **Device Info Dialog Card Ordering**:
  - Corrected order to [1] Device Identification -> [2] I/O & Memory Resource Map -> [3] Physical Communication Parameters.

### Changed
- **Dynamic Tag Index Packing (`TagLayoutMap`)**:
  - Consecutive tag layout dynamically packed from MCU `DeviceResourceInfo` (`0x0020`), removing all hardcoded static offsets.
- **Simulator & Hardware Test Automation**:
  - All 748 unit tests passing 100%.
  - Verified 7/7 physical hardware test steps and 5/5 diagnostic scenarios over UART COM7 <-> COM9.

## [2.1.0] - 2026-10-08 (DYNAMIC TAG LAYOUT ARCHITECTURE)

### Changed
- **Dynamic Tag Index Packing (`TagLayoutMap`)**:
  - Removed static hardcoded Base Indices (`DiBaseIndex=0`, `DoBaseIndex=8`, `AiBaseIndex=16`...).
  - Introduced `TagLayoutMap` struct in `SimplePLC.Protocol.Models` and `ModbusRegisterMap.ComputeLayout(...)`.
  - Tag base offsets are now packed consecutively according to the exact number of hardware pins declared by MCU in `DeviceResourceInfo` (`0x0020`):
    - `DiBase = 0`
    - `DoBase = DiCount` (e.g. 4 DI board has `DO0` at index 4 instead of index 8, eliminating empty dummy slots).
    - `AiBase = DiCount + DoCount`
    - `VflagBase = DiCount + DoCount + AiCount`, etc.
  - Studio `TagCatalogViewModel` dynamically rebuilds Tag Catalog and channel assignments upon receiving device profile via `RebuildFromLayout(TagLayoutMap)`.
  - Updated all simulators, emulators, and test suites across all 5 test projects (747/747 passed).

## [2.0.0] - 2026-10-07 (PLATFORM V2.0 RELEASE)

### Added
- **Diagnostic & Commissioning Subsystem (Wire Profile V2)**:
  - Specified `wire_profile = 2` as a strict superset of Wire Profile V1.
  - Added dedicated `Diagnostic Control Block` at `0x0A20..0x0A24` (5 registers) separated from one-shot system commands:
    - `0x0A20`: `DIAG_COMMAND` (WO command mailbox: `ENTER_DIAG=1`, `HEARTBEAT=2`, `EXIT_DIAG=3`, `COMMIT_RETAIN=4`, `DISCARD_RETAIN=5`).
    - `0x0A21`: `DIAG_STATE` (RO observable state: `ENGINE_RUNNING=1`, `DIAG_CONTROL=2`, `TRANSITIONING=3`, `FAULT=4`).
    - `0x0A22`: `DIAG_FLAGS` (RO bitmask: `RETAIN_DIRTY=bit0`, `LEASE_ACTIVE=bit1`).
    - `0x0A23`: `DIAG_LEASE_REMAINING_MS` (RO countdown in ms, default 3000ms watchdog lease).
    - `0x0A24`: `DIAG_ERROR_CODE` (RO diagnostic error code: `NONE=0`, `DENIED_FAULT=1`, `LEASE_EXPIRED=2`, `FLASH_CRC_MISMATCH=3`, `INVALID_COMMAND=4`, `RETAIN_DIRTY=5`).
  - Defined mutual-exclusive Tag Store ownership: `ENGINE_RUNNING` (Rule Engine owns tags; Modbus writes to `0x0900` locked by safety interlock) vs `DIAG_CONTROL` (Studio owns tags; FC16 writes enabled).
- **Dedicated Function Blocks Subsystem (`0x0B00..0x0B7F`)**:
  - `0x0B00..0x0B3F`: 8 Dedicated Timers (TON, TOF, TP), 8 registers each (16 bytes).
  - `0x0B40..0x0B7F`: 8 Dedicated Counters (CTU, CTD, CTUD), 8 registers each (16 bytes).
- **Real-Time Clock Subsystem (RTC Clock `0x0810..0x0813`)**:
  - `epoch_utc_s` (u32), `tz_offset_min` (i16), `rtc_flags` (u16: `SYNCED 0x0001`, `HW_PRESENT 0x0002`, `BATTERY_LOW 0x0004`).
  - Protocol Read-Before-Write: Studio reads `0x0810..0x0813` prior to synchronization, preserving hardware RTC flags and battery status.
- **Hardware Integration Test Plan V2.0**:
  - Expanded test plan with 14 full hardware test cases (`HIT-001` through `HIT-014`).

---

## [1.9.0] - 2026-09-16 (FROZEN RELEASE)

### Added
- **Self-Describing Device Profile (Contract V1.9)**:
  - Added `DeviceResourceInfo` holding register region at `0x0020..0x0029` (10 registers / 20 bytes, FC03).
  - Added `DeviceResourceInfoDto` wire DTO in `SimplePLC.Protocol`.
  - Added `RegisterCodec.EncodeDeviceResourceInfo` and `RegisterCodec.DecodeDeviceResourceInfo`.
  - Added `DeviceProfileMapper` in `SimplePLC.Application` as a pure data adapter.
  - Added dynamic `ProductDefinition` construction in `SimplePLC.Domain.Builders.DeviceProfileBuilder`.
  - Added runtime capability derivation: `HasRuleEngine` derived from `MaxRules > 0`, `HasRetentiveMemory` derived from `RetentiveRegisters > 0`.
- **Formal Contract Freeze Tests (Phase E)**:
  - Added `WireContractV1_9FreezeTests.cs`: Literal value assertions freezing all register addresses, block lengths, base indices, and binary offsets.
  - Added `ProtocolNumericContractTests.cs`: Literal value assertions freezing all protocol enum integers (`SPLC_DeviceClass`, `SPLC_RemoteIoVariant`, `SPLC_ResetReason`, `SPLC_HealthFlags`, `SPLC_SystemCommand`, `SPLC_CommandStatus`, `SPLC_ConfigStatus`, `SPLC_ErrorCode`, `SPLC_TriggerType`, `SPLC_CompareOp`, `SPLC_ActionType`, `ModbusExceptionCode`).
  - Added `DomainNumericContractAndMappingFreezeTests.cs`: Literal assertions for `TagKind`, `TriggerKind`, `CompareOperator`, `ActionKind` and 1:1 direct cast roundtrip guarantees.
  - Added `PlatformArchitectureRegressionTests.cs`: Behavioral tests verifying mapper adapter purity, builder gatekeeper strictness, and un-hardcoded unknown variant support.
  - Added `GoldenVectorV1_9Tests.cs` and `golden_vectors_v1_9.json`: 9 canonical test vectors (GV-001..GV-009) with mathematically verified Modbus CRC-16 frames and payload checksums.
- **Explicit Enum Definition**:
  - Formalized `SPLC_ConfigStatus` (`0=IDLE, 1=RECEIVING, 2=VERIFYING, 3=READY, 4=ERROR`) in `DeviceEnums.cs` and `simpleplc_protocol_v1_7.h`.
- **Documentation Freeze**:
  - Centralized platform standards in `docs/platform/SimplePLC_Wire_Contract_V1_9.md` and `docs/platform/SimplePLC_Modbus_Register_Map_V1.md`.

### Changed
- Decoupled product configuration from static C# code: Studio connects to any microcontroller conforming to Wire Profile V1 without code changes or redeployments.
- Standardized scan time field in `DeviceHealth`: confirmed millisecond unit (`scan_time_ms`, nominal 10 ms).
- Clarified CRC scopes: explicitly separated Rule Table Payload CRC16 (staged byte stream) from Modbus RTU Frame CRC16 (transport frame checksum).

---

## [1.7.0] - 2026-09-15

### Added
- Standardized 32-byte `RuleRecord` wire format (16 Modbus registers).
- Atomic staging and commit handshake mechanism via registers `0x9000..0xA001`.
- Commit magic word `0xA5A5` for atomic pointer swap.
- Retentive virtual registers (`VREG_RETAIN0..31` at TagIndex 84..115).
- System maintenance commands (`REBOOT`, `FACTORY_RESET`, `CLEAR_RULES`, `CLEAR_RETAIN` at `0x0A00`).
- Initial C header specification `simpleplc_protocol_v1_7.h`.

---

## [1.0.0] - 2026-09-01

### Added
- Initial SimplePLC Remote I/O V1 specification (8 DI, 8 DO, 4 AI).
- Modbus RTU communication over USB-CDC virtual COM port.
- Basic runtime tag monitoring at `0x0900`.
