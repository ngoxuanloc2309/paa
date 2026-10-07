# SimplePLC C# Engineering Rules

## 1. Purpose

Quy định các nguyên tắc engineering bắt buộc cho codebase C# SimplePLC nhằm giữ code có thể test, đo hiệu năng, review và thay đổi an toàn.

---

## 2. Core Rules

### R1. Code and test together
Mỗi feature production phải đi kèm test trong cùng thay đổi. Không defer test sang phase sau.

### R2. Respect layer boundaries
Không bypass architecture để làm nhanh. Nếu thiếu boundary, bổ sung interface đúng layer.

### R3. Protocol constants have one source of truth
Register address, enum numeric value, commit magic, record size và CRC constants chỉ định nghĩa tại `SimplePLC.Protocol`.

### R4. No raw struct casting for wire data
Không dùng direct memory cast/marshal để serialize/deserialize Modbus wire data. Phải đi qua codec explicit.

### R5. Explicit cancellation on I/O
Mọi public async API có I/O phải nhận `CancellationToken`.

### R6. Timeout belongs at I/O boundary
Infrastructure quản lý timeout/retry; Application không poll vô hạn.

### R7. Errors are explicit
Không swallow exception. `SPLC_ErrorCode` phải được map rõ sang error model Application.

### R8. Reserved wire bytes/registers are deterministic
Sender luôn ghi reserved = 0; receiver ignore reserved ở V1.

### R9. RuleIndex is the V1 rule identity
V1 không có persistent `rule_id`; `RuleIndex` là vị trí zero-based trong Rule Table.

### R10. Active Rule Table is read-only from App
Deploy bắt buộc: TransferInfo -> Staging -> CRC verify -> Commit -> Active.

---

## 3. Naming Rules

- Types/public members: `PascalCase`
- Private fields: `_camelCase`
- Local variables/parameters: `camelCase`
- Interfaces: `I` + `PascalCase`
- Async method: hậu tố `Async`

---

## 4. DTO vs Domain Rules

DTO biểu diễn transport/wire data; Domain biểu diễn nghiệp vụ. Không dùng một class cho cả hai vai trò.

```text
RuleRecordDto != Rule
```

Mapping phải explicit và test được.

---

## 5. Async Rules

- Không dùng `.Result` / `.Wait()` trong production flow.
- Không dùng `async void` ngoài UI event handler bắt buộc.
- Truyền `CancellationToken` xuống toàn chain I/O.
- Runtime polling phải có owner, cancellation, stop/dispose, interval policy và error policy.

---

## 6. Allocation and Performance Rules

Performance-sensitive V1:
- Rule encode/decode.
- CRC16 toàn Rule Table.
- Register chunk planning.
- Runtime tag polling/decode.
- DTO <-> Domain mapping.

Guideline:
- Ưu tiên `Span<T>` / `ReadOnlySpan<T>` trong codec nội bộ khi hợp lý.
- Tránh array trung gian lặp lại trong hot loop.
- LINQ được phép ở non-hot path; benchmark trước khi loại bỏ.
- Không optimize trước khi có measurement.

---

## 7. Benchmark Rules

Benchmark dùng project riêng `SimplePLC.Benchmarks`.

Baseline V1 tối thiểu:

```text
Encode 100 RuleRecord
Decode 100 RuleRecord
CRC16 3.2 KB
Decode 128 runtime tags
Chunk planning for 100 rules
```

Unit test không assert thời gian tuyệt đối do CI jitter.

---

## 8. Unit Test Rules

Tên test ưu tiên:

```text
Method_Scenario_ExpectedResult
```

Ví dụ:

```text
DecodeRuleRecord_ValidRegisters_ReturnsExpectedDto
DeployRules_CrcMismatch_ReturnsTransferError
ReadDescriptor_Cancelled_StopsRead
```

Test phải deterministic; không phụ thuộc COM thật/MCU thật/random không seed.

---

## 9. Test Coverage by Feature

Mỗi feature cần xét tối thiểu:
- Happy path.
- Empty/minimum input.
- Maximum supported input nếu hợp lý.
- Invalid contract data.
- Device error.
- Cancellation/timeout nếu có I/O.

Deploy Rules cần tối thiểu: 0/1/100 rules, CRC match/mismatch, CONFIG_STATUS error, cancel during staging write.

---

## 10. Fake Device Rules

Fake device mô phỏng contract, không mô phỏng implementation nội bộ MCU.

Fake cần hỗ trợ register read/write, Active/Staging Rule Table, Config status, CRC result, commit success/failure, DeviceHealth và RuntimeTagValues.

---

## 11. Logging Rules

Log các boundary có giá trị chẩn đoán: connect/disconnect, Modbus failure, deploy start/end, CRC mismatch, MCU error code, reboot/factory reset.

Không spam từng register hoặc polling success ở mức Info mặc định.

---

## 12. Source Control Rules

Contract change phải cập nhật đồng thời:

```text
Protocol code + tests + ICD/data-contract documentation
```

Tránh trộn refactor lớn, feature mới và formatting toàn solution trong cùng change nếu không cần.

---

## 13. Definition of Done

- [ ] Đúng project/layer.
- [ ] Build sạch.
- [ ] Unit tests mới có và pass.
- [ ] Existing tests pass.
- [ ] Error/cancellation path đã xét.
- [ ] Không hard-code protocol literals ngoài Protocol.
- [ ] Không leak transport DTO lên UI.
- [ ] Benchmark cập nhật nếu chạm hot path.
- [ ] Documentation cập nhật nếu thay contract/architecture.
