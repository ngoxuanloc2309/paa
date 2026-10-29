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
 * @brief Gói tin yêu cầu bắt tay từ SynaptiX IDE (88 Bytes)
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
 * @brief Gói tin phản hồi bắt tay từ STM32H5 MCU (96 Bytes)
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

#pragma pack(pop)

#ifdef __cplusplus
}
#endif

#endif /* SPLC_SECURE_WIRE_H */
