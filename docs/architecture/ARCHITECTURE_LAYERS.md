# SimplePLC C# Architecture Layers

## 1. Purpose

Tài liệu này chốt kiến trúc layer cho ứng dụng C# SimplePLC trước khi bắt đầu tạo solution, project skeleton và implementation.

Mục tiêu:
- Cho phép phát triển C# độc lập với implementation nội bộ của MCU.
- Giữ dependency rõ ràng giữa protocol, domain, infrastructure, application và UI.
- Mỗi feature được triển khai cùng test tương ứng trong cùng thay đổi.
- Các path quan trọng về hiệu năng có benchmark riêng và được theo dõi từ sớm.
- Không để chi tiết Modbus/USB rò rỉ lên Domain hoặc Presentation.

---

## 2. Architecture Principles

1. **Contract-first**
   - App và MCU chỉ phụ thuộc vào Data Contract + Modbus Register Map đã chốt.
   - C# không phụ thuộc cách MCU tổ chức task, RAM, Flash hay Rule Engine nội bộ.

2. **Dependency direction is one-way**
   - Layer cấp cao chỉ dùng abstraction của layer thấp hơn.
   - Không tạo dependency ngược lên UI/Application.

3. **Domain is transport-agnostic**
   - Domain không biết Modbus, USB, register address hoặc DTO wire.

4. **Protocol is business-agnostic**
   - Protocol chỉ mô tả wire contract, enum, register map, codec và constants.
   - Không chứa business logic hoặc UI logic.

5. **UI never talks Modbus directly**
   - Presentation chỉ gọi Application use cases/services.

6. **Code and tests are delivered together**
   - Mỗi feature production phải có automated tests trong cùng change.

7. **Performance-critical paths are benchmarked**
   - Encode/decode Rule, CRC, mapping, chunking và runtime polling phải có benchmark hoặc performance test phù hợp.

---

## 3. Solution Structure

```text
SimplePLC.sln
│
├── src/
│   ├── SimplePLC.Protocol/
│   ├── SimplePLC.Domain/
│   ├── SimplePLC.Infrastructure/
│   ├── SimplePLC.Application/
│   └── SimplePLC.Presentation/
│
├── tests/
│   ├── SimplePLC.Protocol.Tests/
│   ├── SimplePLC.Domain.Tests/
│   ├── SimplePLC.Infrastructure.Tests/
│   ├── SimplePLC.Application.Tests/
│   └── SimplePLC.Presentation.Tests/
│
└── benchmarks/
    └── SimplePLC.Benchmarks/
```

Nguyên tắc: test project tách theo production project để ownership rõ ràng và dễ chạy chọn lọc trong CI.

---

## 4. Layer Responsibilities

### 4.1 `SimplePLC.Protocol`

**Vai trò:** biểu diễn chính xác contract App <-> MCU.

Chứa:
- Modbus register map/constants.
- Wire DTO.
- Enum numeric contract.
- Register encoding/decoding helpers.
- RuleRecord register layout.
- CRC-16/MODBUS helper.
- Address calculation helpers.

**Không được chứa:** Rule business object, ViewModel/UI state, serial/USB implementation cụ thể, workflow deploy/load.

**Test ownership:** enum values, address calculation, word order, byte packing, RuleRecord 16-register format, CRC vectors.

### 4.2 `SimplePLC.Domain`

**Vai trò:** mô hình nghiệp vụ sạch của SimplePLC.

Chứa Rule, Trigger, Condition, Guard, Action, TagReference, ProductDefinition và domain validation.

**Không được biết:** Modbus, USB/VCP, register address, FC03/FC16, wire layout.

**Test ownership:** domain validation, invariants và behavior.

### 4.3 `SimplePLC.Infrastructure`

**Vai trò:** hiện thực kết nối thiết bị và giao tiếp Modbus thực tế.

Chứa:
- `IModbusClient` implementation.
- USB/VCP discovery/open/close.
- Modbus RTU transport.
- Device readers/writers.
- FC03/FC16 chunking.
- Timeout/retry.
- Staging/commit interaction.

**Test ownership:** fake Modbus transport, read/write sequence, chunking, timeout/retry, CRC mismatch, commit paths.

### 4.4 `SimplePLC.Application`

**Vai trò:** orchestration use case.

Use case chính:
- Connect Device.
- Load Rules.
- Deploy Rules.
- Read Device Health.
- Read Runtime Tags.
- Execute System Command.
- Start/Stop Monitoring.

Application không biết chi tiết FC03/FC16.

**Test ownership:** happy path, error propagation, cancellation và state transition.

### 4.5 `SimplePLC.Presentation`

**Vai trò:** UI và ViewModel.

**Không được:** biết register, tính CRC, serialize RuleRecord hay gọi FC03/FC16.

**Test ownership:** ViewModel state, command enable/disable, loading/error state.

### 4.6 Test Projects

Mỗi production project có test project tương ứng. Feature production và test phải xuất hiện trong cùng PR/change.

### 4.7 `SimplePLC.Benchmarks`

**Vai trò:** theo dõi hiệu năng các path có tần suất cao hoặc ảnh hưởng UX.

Benchmark ban đầu:
- Decode 1/100 RuleRecord.
- Encode 100 RuleRecord.
- CRC16 trên 3.2 KB.
- DTO -> Domain mapping 100 rules.
- FC03/FC16 chunk planning.
- Runtime tag decode 128 tags.

Ngưỡng performance chỉ freeze sau khi có baseline ổn định.

---

## 5. Dependency Rules

```text
Presentation
    |
    v
Application
    |
    v
Domain

Infrastructure ---> Protocol
      |
      +-----------> Domain
```

`SimplePLC.Protocol` và `SimplePLC.Domain` là dependency roots và phải framework-agnostic tối đa.

---

## 6. Allowed Dependencies

| From | To | Allowed |
|---|---|---|
| Presentation | Application | Yes |
| Application | Domain | Yes |
| Application | Application abstractions | Yes |
| Infrastructure | Protocol | Yes |
| Infrastructure | Domain | Yes, khi mapping cần domain model |
| Tests | Production project under test | Yes |
| Benchmarks | Protocol / Domain / selected Infrastructure | Yes |

---

## 7. Forbidden Dependencies

```text
Presentation -> Infrastructure concrete implementation
Presentation -> Protocol register map
Domain -> Protocol
Domain -> Infrastructure
Protocol -> Domain
Protocol -> UI framework
Application -> SerialPort/NanoModbus concrete classes
```

Không hợp lệ:

```csharp
await _modbus.WriteSingleRegisterAsync(0x9003, crc);
```

Đúng boundary:

```csharp
await _deployRules.ExecuteAsync(rules, cancellationToken);
```

---

## 8. Interface Boundaries

Boundary đầu tiên cần freeze trước skeleton:

```text
IModbusClient
IDeviceDescriptorReader
IRuleTableReader
IRuleTableWriter
IDeviceHealthReader
IRuntimeTagReader
ISystemCommandClient
```

Application nên phụ thuộc interface cấp use case/gateway thay vì operation Modbus cụ thể.

---

## 9. Test Strategy by Layer

### Protocol
Pure unit tests; deterministic byte/register output; không cần hardware.

### Domain
Pure unit tests; behavior/invariants.

### Infrastructure
Fake `IModbusClient` cho phần lớn test; hardware test để riêng.

### Application
Mock/fake abstraction; orchestration, cancellation và error propagation.

### Presentation
ViewModel tests; không phụ thuộc thiết bị thật.

### Hardware-in-the-loop
Để suite/tag riêng `Category: Hardware`, không blocking mọi local unit-test run.

---

## 10. Performance Responsibility

| Area | Owner | Measure |
|---|---|---|
| Register encode/decode | Protocol | latency + allocation |
| Rule encode/decode | Protocol | latency + allocation |
| CRC16 | Protocol | throughput |
| Modbus chunking | Infrastructure | request count + allocation |
| Load/Deploy Rule | Application + Infrastructure | end-to-end latency |
| Runtime polling | Infrastructure/Application | cycle time + missed polls |
| UI rendering | Presentation | responsiveness |

Quy tắc: đo trước khi tối ưu; hot-path regression phải có baseline/benchmark phát hiện.

---

## 11. Example Feature Flow

### Load Rules

```text
Presentation
    -> LoadRulesUseCase
    -> IRuleTableReader / DeviceGateway
    -> Infrastructure Modbus reader
    -> Protocol codec + register map
    -> MCU
```

Data chiều về:

```text
MCU registers -> Protocol DTO -> Domain Rule -> Application result -> ViewModel/UI
```

### Deploy Rules

```text
Domain Rule[]
    -> Protocol RuleRecord[]
    -> CRC16
    -> RuleTransferInfo
    -> Staging writes
    -> Commit
    -> Status/Error
```

Presentation không nhìn thấy register, CRC hoặc chunking.

---

## 12. Definition of Done

- [ ] Đúng layer ownership.
- [ ] Không vi phạm dependency rules.
- [ ] Có production code.
- [ ] Có automated tests trong cùng change.
- [ ] Có happy-path test.
- [ ] Có error/boundary test phù hợp.
- [ ] Có cancellation/timeout nếu feature thực hiện I/O.
- [ ] Không leak raw Modbus detail lên Domain/UI.
- [ ] Performance-critical path có benchmark/baseline phù hợp.
- [ ] Tất cả test liên quan pass.
- [ ] Contract change được cập nhật đồng thời vào ICD/Protocol docs.

---

## 13. Architecture Freeze for Phase 1

Thứ tự implementation:

```text
1. Protocol project skeleton + tests
2. RegisterCodec + tests
3. ModbusRegisterMap + address tests
4. IModbusClient abstraction + FakeModbusClient
5. DeviceDescriptorReader + tests
6. Device connect use case + tests
7. RuleTableReader/Writer + tests
8. Benchmark baseline
9. Domain rule model + mapping
10. Presentation integration
```
