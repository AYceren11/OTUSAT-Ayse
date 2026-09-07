/**
 * @file telemetry_packet.c
 * @brief OTUSAT CubeSat Binary Telemetry Packet Protocol Implementation
 * @author Ayşe (Communication Subsystem Lead)
 */

#include "../include/telemetry_packet.h"
#include <string.h>
#include <stdio.h>

/* ========================================================================== */
/* CRC-16-CCITT IMPLEMENTATION (Polynomial 0x1021, Initial 0xFFFF)            */
/* ========================================================================== */

uint16_t telemetry_crc16_ccitt(const uint8_t *data, size_t length) {
    if (data == NULL || length == 0) {
        return 0;
    }

    uint16_t crc = 0xFFFF;

    for (size_t i = 0; i < length; i++) {
        crc ^= (uint16_t)((uint16_t)data[i] << 8);
        for (int bit = 0; bit < 8; bit++) {
            if (crc & 0x8000) {
                crc = (uint16_t)((crc << 1) ^ 0x1021);
            } else {
                crc = (uint16_t)(crc << 1);
            }
        }
    }

    return crc;
}

/* ========================================================================== */
/* INITIALIZATION & SERIALIZATION                                             */
/* ========================================================================== */

void telemetry_packet_init(TelemetryPacket_t *packet) {
    if (packet == NULL) return;
    
    memset(packet, 0, sizeof(TelemetryPacket_t));
    packet->sync_word = TELEMETRY_SYNC_WORD;
    packet->mode = SATELLITE_MODE_BOOT;
    packet->adcs_status = ADCS_STATUS_DISABLED;
    packet->camera_status = CAMERA_STATUS_OFF;
}

int telemetry_serialize(TelemetryPacket_t *packet, uint8_t *buffer, size_t buffer_size) {
    if (packet == NULL || buffer == NULL) {
        return -1; /* Invalid null pointer */
    }
    if (buffer_size < TELEMETRY_PACKET_SIZE) {
        return -2; /* Buffer too small */
    }

    /* Ensure Sync Word is correct */
    packet->sync_word = TELEMETRY_SYNC_WORD;

    /* Copy struct memory to buffer (excluding checksum first for calculation) */
    memcpy(buffer, packet, TELEMETRY_CRC_DATA_SIZE);

    /* Calculate CRC-16 over the first 37 bytes (Offset 0..36) */
    uint16_t computed_crc = telemetry_crc16_ccitt(buffer, TELEMETRY_CRC_DATA_SIZE);
    packet->checksum = computed_crc;

    /* Write checksum in Little-Endian byte order at Offset 37 & 38 */
    buffer[37] = (uint8_t)(computed_crc & 0xFF);
    buffer[38] = (uint8_t)((computed_crc >> 8) & 0xFF);

    return 0; /* Success */
}

int telemetry_validate(const uint8_t *buffer, size_t buffer_size) {
    if (buffer == NULL) {
        return -1; /* Null pointer */
    }
    if (buffer_size < TELEMETRY_PACKET_SIZE) {
        return -2; /* Incomplete packet size */
    }

    /* Check Sync Word (0xABCD -> Little-Endian: buffer[0] = 0xCD, buffer[1] = 0xAB) */
    uint16_t received_sync = (uint16_t)(buffer[0] | (buffer[1] << 8));
    if (received_sync != TELEMETRY_SYNC_WORD) {
        return -3; /* Invalid sync word */
    }

    /* Calculate CRC over first 37 bytes */
    uint16_t computed_crc = telemetry_crc16_ccitt(buffer, TELEMETRY_CRC_DATA_SIZE);

    /* Extract received CRC from offset 37..38 (Little-Endian) */
    uint16_t received_crc = (uint16_t)(buffer[37] | (buffer[38] << 8));

    if (computed_crc != received_crc) {
        return -4; /* CRC mismatch / packet corrupted */
    }

    return 0; /* Valid */
}

int telemetry_deserialize(const uint8_t *buffer, size_t buffer_size, TelemetryPacket_t *packet) {
    if (buffer == NULL || packet == NULL) {
        return -1;
    }

    /* First validate raw bytes */
    int val_res = telemetry_validate(buffer, buffer_size);
    if (val_res != 0) {
        return val_res;
    }

    /* Copy validated bytes directly into packed struct */
    memcpy(packet, buffer, TELEMETRY_PACKET_SIZE);

    return 0; /* Success */
}

const char* telemetry_mode_to_string(uint8_t mode) {
    switch (mode) {
        case SATELLITE_MODE_BOOT:    return "BOOT";
        case SATELLITE_MODE_SAFE:    return "SAFE";
        case SATELLITE_MODE_NOMINAL: return "NOMINAL";
        default:                     return "UNKNOWN";
    }
}
