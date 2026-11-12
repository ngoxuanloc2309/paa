namespace SimplePLC.Domain.Enums;

/// <summary>
/// Device class: family cấp cao của sản phẩm SimplePLC theo mô hình thiết bị phần cứng.
/// </summary>
public enum SPLC_DeviceClass : ushort
{
    UNKNOWN     = 0,
    REMOTE_IO   = 1,
    DATALOGGER  = 2,
    GATEWAY     = 3,
    CONTROLLER  = 4,
    DTU         = 5
}

/// <summary>
/// Remote I/O variant: chỉ dùng khi device_class = REMOTE_IO.
/// Định danh chuẩn hóa 1:1 theo mã SKU thương mại SynaptiX.
/// </summary>
public enum SPLC_RemoteIoVariant : ushort
{
    UNKNOWN            = 0,
    VARIANT_RIO_884    = 1,
    VARIANT_RIO_440_WF = 2,
    VARIANT_RIO_440    = 3,
    VARIANT_RIO_880    = 4,
    VARIANT_RIO_440_ZB = 5
}

/// <summary>
/// Datalogger variant: chỉ dùng khi device_class = DATALOGGER (2).
/// Định danh chuẩn hóa 1:1 theo mã SKU thương mại SynaptiX.
/// </summary>
public enum SPLC_DataloggerVariant : ushort
{
    UNKNOWN        = 0,

    /// <summary>SYNX-DL-424: 4 Digital In, 2 Digital Out, 4 Analog In (Data Logger)</summary>
    VARIANT_DL_424 = 1
}

/// <summary>
/// Gateway variant: chỉ dùng khi device_class = GATEWAY (3).
/// Định danh chuẩn hóa 1:1 theo mã SKU thương mại SynaptiX.
/// </summary>
public enum SPLC_GatewayVariant : ushort
{
    UNKNOWN        = 0,

    /// <summary>SYNX-GW-220: 2 Digital In, 2 Digital Out (Gateway)</summary>
    VARIANT_GW_220 = 1
}

/// <summary>
/// DTU variant: chỉ dùng khi device_class = DTU (5).
/// Định danh chuẩn hóa 1:1 theo mã SKU thương mại SynaptiX.
/// </summary>
public enum SPLC_DtuVariant : ushort
{
    UNKNOWN         = 0,

    /// <summary>SYNX-DTU-485: 4x RS-485 serial ports to Net/Cloud (Industrial DTU)</summary>
    VARIANT_DTU_485 = 1
}
