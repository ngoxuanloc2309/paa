# SimplePLC Firmware Interface Control Documents (ICD) Hub

> **Target Standard:** IEC 61131-3, IEC 61508-3 SIL2/3, Modbus Application Protocol V1.1b3  
> **Directory Scope:** Interface Control Documents (ICD), Hardware Register Maps, C Header Definitions, Golden Vectors.

---

## 1. Interface Control Documents (ICD) Master Catalogue

| ID | Specification Code | Document File | Subsystem / Wire Interface | Modbus Address |
|:---:|:---|:---|:---|:---:|
| **00** | `SPLC-SPEC-MCU-000` | [`00-MCU_CONFORMANCE_SPECIFICATION_V2_0.md`](file:///g:/HoaNV/Projects/SimplePLC/docs/03-firmware/00-MCU_CONFORMANCE_SPECIFICATION_V2_0.md) | MCU Conformance & Checklist | Core Architecture |
| **01** | `SPLC-SPEC-SYS-006` | [`01-SPLC_SPEC_SYSTEM_TAGS_006_ICD.md`](file:///g:/HoaNV/Projects/SimplePLC/docs/03-firmware/01-SPLC_SPEC_SYSTEM_TAGS_006_ICD.md) | 8 Real-time System Tags | `0x0820..0x0827` |
| **02** | `SPLC-SPEC-MQTT-001` | [`02-SPLC_SPEC_MQTT_001_TELEMETRY_ICD.md`](file:///g:/HoaNV/Projects/SimplePLC/docs/03-firmware/02-SPLC_SPEC_MQTT_001_TELEMETRY_ICD.md) | MQTT Outbound Telemetry | `0x0C00..0x0D7F` |
| **03** | `SPLC-SPEC-MQTT-002` | [`03-SPLC_SPEC_MQTT_002_SUBSCRIBE_ICD.md`](file:///g:/HoaNV/Projects/SimplePLC/docs/03-firmware/03-SPLC_SPEC_MQTT_002_SUBSCRIBE_ICD.md) | MQTT Inbound Subscription | `0x0D80..0x0DFF` |
| **04** | `SPLC-SPEC-MQTT-003` | [`04-SPLC_SPEC_MQTT_003_TLS_CERTIFICATE_WIRE_ICD.md`](file:///g:/HoaNV/Projects/SimplePLC/docs/03-firmware/04-SPLC_SPEC_MQTT_003_TLS_CERTIFICATE_WIRE_ICD.md) | MQTT TLS/mTLS X.509 Wire | `0x0F00..0x0F46` |
| **05** | `SPLC-SPEC-MQTT-004` | [`05-SPLC_SPEC_MQTT_004_LAST_WILL_ICD.md`](file:///g:/HoaNV/Projects/SimplePLC/docs/03-firmware/05-SPLC_SPEC_MQTT_004_LAST_WILL_ICD.md) | MQTT Last Will & Testament | `0x0F50..0x0F5F` |
| **06** | `SPLC-SPEC-MB-001` | [`06-SPLC_SPEC_MODBUS_MASTER_001_READ_ICD.md`](file:///g:/HoaNV/Projects/SimplePLC/docs/03-firmware/06-SPLC_SPEC_MODBUS_MASTER_001_READ_ICD.md) | Modbus Master Read (MB_READ) | `0x1000..0x10FF` |
| **07** | `SPLC-SPEC-MB-002` | [`07-SPLC_SPEC_MODBUS_MASTER_002_WRITE_ICD.md`](file:///g:/HoaNV/Projects/SimplePLC/docs/03-firmware/07-SPLC_SPEC_MODBUS_MASTER_002_WRITE_ICD.md) | Modbus Master Write (MB_WRITE) | `0x1100..0x11FF` |
| **08** | `SPLC-SPEC-ACT-003` | [`08-SPLC_SPEC_DIRECT_WRITE_003_ICD.md`](file:///g:/HoaNV/Projects/SimplePLC/docs/03-firmware/08-SPLC_SPEC_DIRECT_WRITE_003_ICD.md) | Hardware Pulse Out (PULSE_OUT)| `0x1200..0x12FF` |
| **09** | `SPLC-SPEC-WIFI-004` | [`09-SPLC_SPEC_WIFI_004_ICD.md`](file:///g:/HoaNV/Projects/SimplePLC/docs/03-firmware/09-SPLC_SPEC_WIFI_004_ICD.md) | ST67 Industrial Wi-Fi Module | `0x1300..0x13FF` |
| **10** | `SPLC-SPEC-TCP-005` | [`10-SPLC_SPEC_MODBUS_TCP_005_ICD.md`](file:///g:/HoaNV/Projects/SimplePLC/docs/03-firmware/10-SPLC_SPEC_MODBUS_TCP_005_ICD.md) | Modbus TCP Master/Client | IP Network |
| **11** | `SPLC-SPEC-SLV-001` | [`11-SPLC_SPEC_SLAVE_001_FIXED_MEMORY_MAP_ICD.md`](file:///g:/HoaNV/Projects/SimplePLC/docs/03-firmware/11-SPLC_SPEC_SLAVE_001_FIXED_MEMORY_MAP_ICD.md) | SCADA Fixed Slave Memory Map | Fixed RAM Maps |
| **12** | `SPLC-SPEC-DTU-001` | [`12-SPLC_SPEC_DTU_001_ROUTING_AND_GATEWAY_ICD.md`](file:///g:/HoaNV/Projects/SimplePLC/docs/03-firmware/12-SPLC_SPEC_DTU_001_ROUTING_AND_GATEWAY_ICD.md) | DTU Gateway (Serial/Net Routing) | `0x1600..0x16BF` |
| **13** | `SPLC-SPEC-ZGB-006` | [`13-SPLC_SPEC_ZIGBEE_006_ICD.md`](file:///g:/HoaNV/Projects/SimplePLC/docs/03-firmware/13-SPLC_SPEC_ZIGBEE_006_ICD.md) | Zigbee Wireless Modbus Mesh | `0x1700..0x173F` |
| **14** | `SPLC-SPEC-MATH-007`| [`14-SPLC_SPEC_MATH_007_ICD.md`](file:///g:/HoaNV/Projects/SimplePLC/docs/03-firmware/14-SPLC_SPEC_MATH_007_ICD.md) | Deterministic ALU & Math Blocks| FBD ALU Bytecode |
| **15** | `SPLC-SPEC-SEC-001` | [`15-SPLC_SPEC_SECURITY_001_SECURE_TRANSPORT_ICD.md`](file:///g:/HoaNV/Projects/SimplePLC/docs/03-firmware/15-SPLC_SPEC_SECURITY_001_SECURE_TRANSPORT_ICD.md) | ChaCha20-Poly1305 Security Transport | Wire Security |

---

## 2. Firmware C Headers & Wire Testing

- **C Binary Structs & Packing Contract:** [`98-splc_secure_wire.h`](file:///g:/HoaNV/Projects/SimplePLC/docs/03-firmware/98-splc_secure_wire.h)
- **Golden Test Vectors:** [`99-GOLDEN_VECTORS_V2_0.md`](file:///g:/HoaNV/Projects/SimplePLC/docs/03-firmware/99-GOLDEN_VECTORS_V2_0.md)
