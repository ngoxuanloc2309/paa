namespace SimplePLC.Application.Models;

using SimplePLC.Application.Enums;
using SimplePLC.Domain.Enums;

/// <summary>
/// Ảnh chụp trạng thái bất biến tại một thời điểm của một Tag trong bộ nhớ Runtime State Store.
/// Giữ nguyên giá trị raw int32 nhận được từ đường truyền Modbus RTU.
/// </summary>
public sealed record RuntimeTagSnapshot(
    ushort TagIndex,
    string Name,
    string? Alias,
    TagKind Kind,
    TagDataType DataType,
    int RawValue,
    TagQuality Quality,
    DateTimeOffset LastSuccessfulUpdateAt
);
