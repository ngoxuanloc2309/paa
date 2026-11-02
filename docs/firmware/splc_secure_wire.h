/**
 * @file splc_secure_wire.h
 * @brief SynaptiX Secure Transport Protocol Wire Contract ICD
 * @standard SPLC-SPEC-SECURITY-001
 * 
 * Copyright (c) SynaptiX Automation. All rights reserved.
 */

#ifndef SPLC_SECURE_WIRE_H
#define SPLC_SECURE_WIRE_H

#include <stdint.h>
#include <stdbool.h>

#ifdef __cplusplus
extern "C" {
#endif

/* --- Magic Start of Frame (SOF) Markers --- */
#define SPLC_SOF_HANDSHAKE_REQ   0x4853  /**< 'S', 'H' (Little-Endian in memory: 0x53, 0x48) */
#define SPLC_SOF_HANDSHAKE_RESP  0x5353  /**< 'S', 'S' (Little-Endian in memory: 0x53, 0x53) */
#define SPLC_SOF_SECURE_DATA     0x5853  /**< 'S', 'X' (Little-Endian in memory: 0x53, 0x58) */

/* --- Protocol Constants --- */
#define SPLC_SECURE_PROTO_V1     0x01
#define SPLC_MAX_PAYLOAD_SIZE    1024
#define SPLC_AUTH_TAG_SIZE       16
#define SPLC_NONCE_SIZE          12
#define SPLC_PUBKEY_COORD_SIZE   32
#define SPLC_PUBKEY_XY_SIZE      64
#define SPLC_SALT_SIZE           16
#define SPLC_VENDOR_TOKEN_SIZE   8
#define SPLC_DEVICE_PROOF_SIZE   12

/* --- Status Codes --- */
#define SPLC_STATUS_OK                 0x00
#define SPLC_STATUS_ERR_INVALID_TOKEN  0x01
#define SPLC_STATUS_ERR_CRYPTO_BUSY    0x02
#define SPLC_STATUS_ERR_UNAUTHORIZED   0x03
#define SPLC_STATUS_ERR_BAD_LENGTH     0x04

#pragma pack(push, 1)

/**
 * @brief Gói tin yêu cầu bắt tay từ SynaptiX IDE (Đúng chuẩn 92 Bytes = 2+1+1+64+16+8)
 */
typedef struct {
    uint16_t magic_sof;          /**< 0x4853 ('S', 'H') */
    uint8_t  protocol_version;   /**< 0x01 */
    uint8_t  reserved;           /**< 0x00 */
    uint8_t  app_pubkey_xy[64];  /**< NIST P-256 Ephemeral Public Key (X: 32B, Y: 32B) */
    uint8_t  app_salt[16];       /**< Random Salt do App sinh chống Replay */
    uint8_t  vendor_token[8];    /**< HMAC xác thực App chính hãng SynaptiX */
} SPLC_HandshakeReq_t;

/**
 * @brief Gói tin phản hồi bắt tay từ STM32H5 MCU (Đúng chuẩn 96 Bytes = 2+1+1+64+16+12)
 */
typedef struct {
    uint16_t magic_sof;          /**< 0x5353 ('S', 'S') */
    uint8_t  status_code;        /**< 0x00: Thành công, Mã lỗi khác 0 */
    uint8_t  mcu_hw_revision;    /**< Phiên bản bo mạch phần cứng */
    uint8_t  mcu_pubkey_xy[64];  /**< NIST P-256 Ephemeral Public Key của MCU */
    uint8_t  mcu_salt[16];       /**< Random Salt do TRNG của STM32 sinh ra */
    uint8_t  device_proof[12];   /**< Bằng chứng xác thực danh tính gắn với 96-bit UID */
} SPLC_HandshakeResp_t;

/**
 * @brief Tiêu đề Khung vận chuyển dữ liệu mã hóa (16 Bytes Header)
 */
typedef struct {
    uint16_t magic_sof;          /**< 0x5853 ('S', 'X') */
    uint16_t payload_len;        /**< Số byte của ciphertext (1..1024) */
    uint32_t session_id;         /**< ID phiên làm việc ngẫu nhiên */
    uint64_t packet_counter;     /**< Bộ đếm gói tin tăng dần đơn điệu */
} SPLC_SecureHeader_t;

/**
 * @brief Cấu trúc phong bì mã hóa hoàn chỉnh trên đường truyền (Envelope)
 */
typedef struct {
    SPLC_SecureHeader_t header;
    uint8_t             ciphertext_and_tag[SPLC_MAX_PAYLOAD_SIZE + SPLC_AUTH_TAG_SIZE];
} SPLC_SecureFrame_t;

/* ========================================================================= */
/* --- DTU Gateway Subsystem Wire Contract (0x1600 - 0x16BF) ---             */
/* Standard: SPLC-SPEC-DTU-001                                               */
/* ========================================================================= */

/**
 * @brief 0x1600 - 0x163F: Cấu hình HTTP REST Client của DTU (Đúng chuẩn 128 Bytes / 64 Regs)
 */
typedef struct {
    uint8_t  enabled;           /**< 0 = Tắt, 1 = Bật */
    uint8_t  method;            /**< 0 = POST, 1 = PUT */
    uint16_t timeout_sec;       /**< Thời gian chờ phản hồi HTTP (mặc định 10s) */
    uint16_t interval_sec;      /**< Chu kỳ gửi định kỳ khi trigger_mode = 1 (s) */
    uint8_t  trigger_mode;      /**< 0 = OnDataReceived (Sự kiện), 1 = Periodic (Chu kỳ) */
    uint8_t  reserved0;         /**< Đệm căn lề */
    char     url[64];           /**< Endpoint URL kết thúc bằng ký tự NULL */
    char     headers[56];       /**< Custom HTTP Headers (VD: Authorization: Bearer ...) */
} splc_dtu_http_config_t;

/**
 * @brief 0x1640 - 0x167F: Cấu hình TCP Socket Client / Server (Đúng chuẩn 128 Bytes / 64 Regs)
 */
typedef struct {
    uint8_t  enabled;                 /**< 0 = Tắt, 1 = Bật */
    uint8_t  socket_mode;             /**< 0 = Client, 1 = Server */
    uint16_t remote_port;             /**< Cổng máy chủ từ xa khi Mode = Client */
    uint16_t local_port;              /**< Cổng lắng nghe cục bộ khi Mode = Server */
    uint16_t reconnect_delay_sec;     /**< Thời gian thử lại kết nối khi đứt socket (s) */
    uint16_t heartbeat_interval_sec;  /**< Chu kỳ gửi gói tin giữ phiên PING (s) */
    uint8_t  registration_type;       /**< 0 = None, 1 = OnConnect, 2 = PrependToEveryPacket */
    uint8_t  reserved0;               /**< Đệm căn lề */
    char     remote_host[48];         /**< IP hoặc Domain máy chủ từ xa */
    char     heartbeat_payload[16];   /**< Chuỗi ký tự Heartbeat (mặc định "PING") */
    char     registration_payload[52];/**< Chuỗi định danh báo danh trạm (VD: "ID:STATION_01") */
} splc_dtu_tcp_config_t;

/**
 * @brief 0x1680 - 0x16BF: Cấu hình Định Tuyến & Ngắt Khung RS-485 (Đúng chuẩn 128 Bytes / 64 Regs)
 */
typedef struct {
    uint8_t  enabled;           /**< 0 = Tắt, 1 = Bật chế độ DTU */
    uint8_t  port_index;        /**< Cổng RS-485 áp dụng (0 = Port 1, 1 = Port 2...) */
    uint8_t  serial_mode;       /**< 0 = Transference, 1 = ModbusMaster, 2 = ModbusSlave */
    uint8_t  uplink_mode;       /**< 0 = MQTT, 1 = HTTP, 2 = TCP Client, 3 = TCP Server */
    uint16_t frame_timeout_ms;  /**< Delimiter Timeout t3.5 (mặc định 20ms) */
    uint16_t max_packet_size;   /**< Kích thước tối đa bộ đệm gói tin (512..2048) */
    uint8_t  payload_mode;      /**< 0 = Standard IEC, 1 = Custom, 2 = Raw Passthrough */
    uint8_t  payload_flags;     /**< Bitmask cờ trường: DevId, Time, Port, Seq, Hex, Len */
    uint8_t  reserved[118];     /**< Đệm căn chỉnh 128 bytes */
} splc_dtu_routing_config_t;

#pragma pack(pop)

/* --- Static Assertions for Wire Contract Integrity (C11 / C++11) --- */
#if defined(__STDC_VERSION__) && __STDC_VERSION__ >= 201112L
_Static_assert(sizeof(SPLC_HandshakeReq_t) == 92, "SPLC_HandshakeReq_t must be exactly 92 bytes");
_Static_assert(sizeof(SPLC_HandshakeResp_t) == 96, "SPLC_HandshakeResp_t must be exactly 96 bytes");
_Static_assert(sizeof(SPLC_SecureHeader_t) == 16, "SPLC_SecureHeader_t must be exactly 16 bytes");
_Static_assert(sizeof(splc_dtu_http_config_t) == 128, "splc_dtu_http_config_t must be exactly 128 bytes");
_Static_assert(sizeof(splc_dtu_tcp_config_t) == 128, "splc_dtu_tcp_config_t must be exactly 128 bytes");
_Static_assert(sizeof(splc_dtu_routing_config_t) == 128, "splc_dtu_routing_config_t must be exactly 128 bytes");
#elif defined(__cplusplus) && __cplusplus >= 201103L
static_assert(sizeof(SPLC_HandshakeReq_t) == 92, "SPLC_HandshakeReq_t must be exactly 92 bytes");
static_assert(sizeof(SPLC_HandshakeResp_t) == 96, "SPLC_HandshakeResp_t must be exactly 96 bytes");
static_assert(sizeof(SPLC_SecureHeader_t) == 16, "SPLC_SecureHeader_t must be exactly 16 bytes");
static_assert(sizeof(splc_dtu_http_config_t) == 128, "splc_dtu_http_config_t must be exactly 128 bytes");
static_assert(sizeof(splc_dtu_tcp_config_t) == 128, "splc_dtu_tcp_config_t must be exactly 128 bytes");
static_assert(sizeof(splc_dtu_routing_config_t) == 128, "splc_dtu_routing_config_t must be exactly 128 bytes");
#endif

#ifdef __cplusplus
}
#endif

#endif /* SPLC_SECURE_WIRE_H */
