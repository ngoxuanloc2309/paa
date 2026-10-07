# SimplePLC Platform Contract Changelog

## [2.0.0-draft] - 2026-09-23 (DRAFT SPECIFICATION)

### Added
- **Diagnostic & Commissioning Subsystem (Wire Profile V2 Draft)**:
  - Specified `wire_profile = 2` as a strict superset of Wire Profile V1.
  - Added dedicated `Diagnostic Control Block` at `0x0A20..0x0A24` (5 registers) separated from one-shot system commands:
    - `0x0A20`: `DIAG_COMMAND` (WO command mailbox: `ENTER_DIAG=1`, `HEARTBEAT=2`, `EXIT_DIAG=3`, `COMMIT_RETAIN=4`, `RESTORE_SAFE_OUTPUTS=5`).
    - `0x0A21`: `DIAG_STATE` (RO observable state: `ENGINE_RUNNING=1`, `DIAG_CONTROL=2`, `TRANSITIONING=3`, `FAULT=4`).
    - `0x0A22`: `DIAG_FLAGS` (RO bitmask: `RETAIN_DIRTY=bit0`, `LEASE_ACTIVE=bit1`).
    - `0x0A23`: `DIAG_LEASE_REMAINING_MS` (RO countdown in ms).
    - `0x0A24`: `DIAG_ERROR_CODE` (RO diagnostic error code).
  - Defined mutual-exclusive Tag Store ownership: `ENGINE_RUNNING` (Rule Engine owns tags; Modbus writes to `0x0900` locked) vs `DIAG_CONTROL` (Studio owns tags; FC16 writes to `0x0900` enabled).
  - Specified leased session lifecycle with target 1000ms heartbeat, 3000ms lease duration, and baseline safe fallback (restore logic ownership and execute fresh scan).
  - Enforced strict 32-bit FC16 atomic write contract on runtime tags (`Quantity = 2 * N`), forbidding FC06 single-register writes.
  - Specified RAM shadow for `VREG_RETAIN` during diagnostics with atomic ping-pong Flash commit verification to eliminate Flash wear.
  - Detailed in `docs/platform/SimplePLC_Wire_Contract_V2_Draft.md`.

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
