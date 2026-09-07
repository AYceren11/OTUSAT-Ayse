/**
 * @file telemetry_packet.h
 * @brief OTUSAT CubeSat Binary Telemetry Packet Protocol Header
 * @author Ayşe (Communication Subsystem Lead)
 * @note Designed for STM32 OBC (C/C++) and Ground Segment (C# .NET) interoperability.
 * 
 * Packet Size: 39 Bytes
 * Endianness: Little-Endian (Standard ARM Cortex-M & x86/x64)
 * Error Check: CRC-16-CCITT (Polynomial: 0x1021, Init: 0xFFFF)
 */

#ifndef TELEMETRY_PACKET_H
#define TELEMETRY_PACKET_H

#include <stdint.h>
#include <stdbool.h>
#include <stddef.h>

#ifdef __cplusplus
extern "C" {
#endif

/* ========================================================================== */
/* CONSTANTS & DEFINITIONS                                                    */
/* ========================================================================== */

#define TELEMETRY_SYNC_WORD         ((uint16_t)0xABCD)
#define TELEMETRY_PACKET_SIZE       ((size_t)39)
#define TELEMETRY_CRC_DATA_SIZE     ((size_t)37) /* Bytes 0 to 36 covered by CRC */

/* ========================================================================== */
/* ENUMS: SATELLITE MODES & SUBSYSTEM STATUS                                  */
/* ========================================================================== */

/**
 * @brief Satellite operational modes (TM_005)
 */
typedef enum {
    SATELLITE_MODE_BOOT    = 0, /**< Initial startup / power-on mode */
    SATELLITE_MODE_SAFE    = 1, /**< Minimum power, basic comms & telemetry */
    SATELLITE_MODE_NOMINAL = 2  /**< Full operations, payload active */
} SatelliteMode_t;

/**
 * @brief ADCS (Attitude Determination and Control System) status (TM_006)
 */
typedef enum {
    ADCS_STATUS_DISABLED = 0, /**< ADCS inactive / idle */
    ADCS_STATUS_ENABLED  = 1  /**< ADCS active */
} AdcsStatus_t;

/**
 * @brief Payload Camera status (TM_007)
 */
typedef enum {
    CAMERA_STATUS_OFF    = 0, /**< Camera powered off / idle */
    CAMERA_STATUS_ACTIVE = 1  /**< Camera active / capturing / processing */
} CameraStatus_t;

/**
 * @brief Standard Telemetry Parameter Identifiers (WhatsApp specification)
 */
typedef enum {
    TM_ID_TEMPERATURE     = 1, /**< TM_001: Temperature (°C) */
    TM_ID_PRESSURE        = 2, /**< TM_002: Pressure (hPa) */
    TM_ID_BATTERY_VOLTAGE = 3, /**< TM_003: Battery Voltage (V) */
    TM_ID_BATTERY_CURRENT = 4, /**< TM_004: Battery Current (A) */
    TM_ID_MODE            = 5, /**< TM_005: Operational Mode */
    TM_ID_ADCS_STATUS     = 6, /**< TM_006: ADCS Status */
    TM_ID_CAMERA_STATUS   = 7  /**< TM_007: Camera Status */
} TelemetryId_t;

/**
 * @brief Standard Telecommand Identifiers (WhatsApp specification)
 */
typedef enum {
    TC_ID_PING            = 1, /**< TC_001: Ping satellite */
    TC_ID_GET_STATUS      = 2, /**< TC_002: Request system status */
    TC_ID_GET_SENSOR      = 3, /**< TC_003: Request sensor data */
    TC_ID_TAKE_PHOTO      = 4, /**< TC_004: Trigger camera capture */
    TC_ID_SET_MODE        = 5, /**< TC_005: Set satellite mode */
    TC_ID_ADCS_ENABLE     = 6, /**< TC_006: Enable/Disable ADCS */
    TC_ID_RESET           = 7  /**< TC_007: Reset satellite OBC */
} TelecommandId_t;

/* ========================================================================== */
/* TELEMETRY PACKET STRUCTURE (39 BYTES)                                      */
/* ========================================================================== */

#pragma pack(push, 1)

/**
 * @brief Binary Telemetry Packet Structure (Exactly 39 Bytes, 1-byte aligned)
 */
typedef struct {
    /* Header (Offset: 0..5, 6 Bytes) */
    uint16_t sync_word;       /**< [Offset 0]  Sync word, fixed 0xABCD */
    uint32_t timestamp_ms;    /**< [Offset 2]  Uptime in milliseconds since boot */

    /* Sensor & Power Payload (Offset: 6..21, 16 Bytes) */
    float    temperature;     /**< [Offset 6]  Temperature in °C (IEEE 754 float) */
    float    pressure;        /**< [Offset 10] Pressure in hPa (IEEE 754 float) */
    float    battery_voltage; /**< [Offset 14] Battery voltage in Volts (IEEE 754 float) */
    float    battery_current; /**< [Offset 18] Battery current in Amperes (IEEE 754 float) */

    /* Attitude Orientation Payload (Offset: 22..33, 12 Bytes) */
    float    attitude_roll;   /**< [Offset 22] Roll angle in Degrees (IEEE 754 float) */
    float    attitude_pitch;  /**< [Offset 26] Pitch angle in Degrees (IEEE 754 float) */
    float    attitude_yaw;    /**< [Offset 30] Yaw angle in Degrees (IEEE 754 float) */

    /* Subsystem Status (Offset: 34..36, 3 Bytes) */
    uint8_t  mode;            /**< [Offset 34] Satellite mode (0=Boot, 1=Safe, 2=Nominal) */
    uint8_t  adcs_status;     /**< [Offset 35] ADCS status (0=Off, 1=On) */
    uint8_t  camera_status;   /**< [Offset 36] Camera status (0=Off, 1=On) */

    /* Checksum (Offset: 37..38, 2 Bytes) */
    uint16_t checksum;        /**< [Offset 37] CRC-16-CCITT across bytes 0..36 */
} TelemetryPacket_t;

#pragma pack(pop)

/* Compile-time verification of struct size */
typedef char telemetry_packet_size_check[(sizeof(TelemetryPacket_t) == 39) ? 1 : -1];

/* ========================================================================== */
/* FUNCTION PROTOTYPES                                                        */
/* ========================================================================== */

/**
 * @brief Computes CRC-16-CCITT (Polynomial 0x1021, Init 0xFFFF)
 * @param data Pointer to input data buffer
 * @param length Number of bytes to compute CRC over
 * @return 16-bit CRC checksum
 */
uint16_t telemetry_crc16_ccitt(const uint8_t *data, size_t length);

/**
 * @brief Initializes a telemetry packet with default values and correct sync word
 * @param packet Pointer to TelemetryPacket_t struct to initialize
 */
void telemetry_packet_init(TelemetryPacket_t *packet);

/**
 * @brief Serializes a telemetry packet struct into a 39-byte binary buffer
 *        and automatically computes and writes the CRC-16 checksum.
 * @param packet Pointer to source TelemetryPacket_t struct
 * @param buffer Output byte buffer (must be at least 39 bytes)
 * @param buffer_size Size of the output buffer
 * @return 0 on success, negative error code on failure
 */
int telemetry_serialize(TelemetryPacket_t *packet, uint8_t *buffer, size_t buffer_size);

/**
 * @brief Deserializes and validates a 39-byte binary buffer into a struct.
 * @param buffer Input byte buffer
 * @param buffer_size Size of the input buffer (must be >= 39)
 * @param packet Pointer to destination TelemetryPacket_t struct
 * @return 0 on success, negative error code on validation failure
 */
int telemetry_deserialize(const uint8_t *buffer, size_t buffer_size, TelemetryPacket_t *packet);

/**
 * @brief Validates a raw 39-byte telemetry buffer (checks Sync Word and CRC)
 * @param buffer Raw byte buffer
 * @param buffer_size Size of the buffer (must be at least 39 bytes)
 * @return 0 if valid, negative error code if invalid (corrupted/wrong sync)
 */
int telemetry_validate(const uint8_t *buffer, size_t buffer_size);

/**
 * @brief Returns a human-readable string for satellite mode
 * @param mode Satellite mode enum value
 * @return String description
 */
const char* telemetry_mode_to_string(uint8_t mode);

#ifdef __cplusplus
}
#endif

#endif /* TELEMETRY_PACKET_H */
