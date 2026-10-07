namespace SimplePLC.Protocol.Enums;

/// <summary>
/// Các mã ngoại lệ chuẩn Modbus theo đặc tả Modbus Application Protocol V1.1b3.
/// Khi MCU gặp lỗi xử lý, frame phản hồi sẽ có function_code | 0x80 kèm exception_code.
/// </summary>
public enum ModbusExceptionCode : byte
{
    None = 0x00,

    /// <summary>
    /// Function code không được hỗ trợ bởi thiết bị (0x01).
    /// </summary>
    IllegalFunction = 0x01,

    /// <summary>
    /// Địa chỉ thanh ghi yêu cầu nằm ngoài phạm vi cho phép (0x02).
    /// </summary>
    IllegalDataAddress = 0x02,

    /// <summary>
    /// Giá trị ghi vào thanh ghi không hợp lệ hoặc số lượng thanh ghi vượt quá giới hạn (0x03).
    /// </summary>
    IllegalDataValue = 0x03,

    /// <summary>
    /// Lỗi phần cứng hoặc lỗi nghiêm trọng khi xử lý yêu cầu trên thiết bị (0x04).
    /// </summary>
    SlaveDeviceFailure = 0x04,

    /// <summary>
    /// Thiết bị đã nhận yêu cầu và đang xử lý tác vụ dài (0x05).
    /// </summary>
    Acknowledge = 0x05,

    /// <summary>
    /// Thiết bị đang bận xử lý tác vụ trước đó, yêu cầu thử lại sau (0x06).
    /// </summary>
    SlaveDeviceBusy = 0x06,

    /// <summary>
    /// Thiết bị từ chối thực hiện yêu cầu (0x07).
    /// </summary>
    NegativeAcknowledge = 0x07,

    /// <summary>
    /// Lỗi chẵn lẻ bộ nhớ khi đọc vùng nhớ lưu trữ (0x08).
    /// </summary>
    MemoryParityError = 0x08,

    /// <summary>
    /// Gateway không tìm thấy đường dẫn đến thiết bị đích (0x0A).
    /// </summary>
    GatewayPathUnavailable = 0x0A,

    /// <summary>
    /// Thiết bị đích phía sau gateway không phản hồi (0x0B).
    /// </summary>
    GatewayTargetDeviceFailedToRespond = 0x0B
}
