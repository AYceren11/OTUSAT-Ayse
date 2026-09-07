/**
 * @file test_telemetry.c
 * @brief Unit tests for C Binary Telemetry Packet Protocol
 */

#include "../include/telemetry_packet.h"
#include <stdio.h>
#include <assert.h>
#include <math.h>

static void print_hex(const uint8_t *buf, size_t len) {
    for (size_t i = 0; i < len; i++) {
        printf("%02X ", buf[i]);
    }
    printf("\n");
}

int main(void) {
    printf("=====================================================\n");
    printf("   OTUSAT CubeSat Telemetry Packet C Unit Tests     \n");
    printf("=====================================================\n");

    /* 1. Size Verification */
    printf("[TEST 1] Verifying Struct Size == 39 Bytes... ");
    assert(sizeof(TelemetryPacket_t) == 39);
    assert(TELEMETRY_PACKET_SIZE == 39);
    printf("PASSED! (Size = %zu bytes)\n", sizeof(TelemetryPacket_t));

    /* 2. Initialization and Value Assignment */
    printf("[TEST 2] Initializing and serializing packet... ");
    TelemetryPacket_t tx_pkt;
    telemetry_packet_init(&tx_pkt);

    tx_pkt.timestamp_ms    = 1000;      /* 1.0s */
    tx_pkt.temperature     = 25.0f;     /* 25.0 °C */
    tx_pkt.pressure        = 1024.0f;   /* 1024.0 hPa */
    tx_pkt.battery_voltage = 8.1f;      /* 8.1 V */
    tx_pkt.battery_current = 0.400f;    /* 0.4 A */
    tx_pkt.attitude_roll   = 90.0f;     /* 90 deg */
    tx_pkt.attitude_pitch  = 45.0f;     /* 45 deg */
    tx_pkt.attitude_yaw    = 120.0f;    /* 120 deg */
    tx_pkt.mode            = SATELLITE_MODE_NOMINAL;
    tx_pkt.adcs_status     = ADCS_STATUS_ENABLED;
    tx_pkt.camera_status   = CAMERA_STATUS_OFF;

    uint8_t raw_buffer[TELEMETRY_PACKET_SIZE];
    int res = telemetry_serialize(&tx_pkt, raw_buffer, sizeof(raw_buffer));
    assert(res == 0);
    printf("PASSED!\n");

    printf("Serialized Hex Stream (39 Bytes):\n  ");
    print_hex(raw_buffer, TELEMETRY_PACKET_SIZE);

    /* 3. Validation Check */
    printf("[TEST 3] Validating raw buffer... ");
    assert(telemetry_validate(raw_buffer, sizeof(raw_buffer)) == 0);
    printf("PASSED! (Sync Word & CRC OK)\n");

    /* 4. CRC Error Detection on Corrupted Buffer */
    printf("[TEST 4] Testing CRC error detection on byte corruption... ");
    uint8_t corrupted_buffer[TELEMETRY_PACKET_SIZE];
    for (size_t i = 0; i < TELEMETRY_PACKET_SIZE; i++) corrupted_buffer[i] = raw_buffer[i];
    corrupted_buffer[6] ^= 0xFF; /* Corrupt temperature byte */
    int corrupt_res = telemetry_validate(corrupted_buffer, sizeof(corrupted_buffer));
    assert(corrupt_res == -4); /* Must fail with CRC mismatch */
    printf("PASSED! (Corrupted packet correctly rejected)\n");

    /* 5. Deserialization & Round-Trip Check */
    printf("[TEST 5] Deserializing and asserting round-trip values... ");
    TelemetryPacket_t rx_pkt;
    res = telemetry_deserialize(raw_buffer, sizeof(raw_buffer), &rx_pkt);
    assert(res == 0);
    assert(rx_pkt.sync_word == TELEMETRY_SYNC_WORD);
    assert(rx_pkt.timestamp_ms == 1000);
    assert(fabs(rx_pkt.temperature - 25.0f) < 0.0001f);
    assert(fabs(rx_pkt.pressure - 1024.0f) < 0.0001f);
    assert(fabs(rx_pkt.battery_voltage - 8.1f) < 0.0001f);
    assert(fabs(rx_pkt.battery_current - 0.400f) < 0.0001f);
    assert(fabs(rx_pkt.attitude_roll - 90.0f) < 0.0001f);
    assert(fabs(rx_pkt.attitude_pitch - 45.0f) < 0.0001f);
    assert(fabs(rx_pkt.attitude_yaw - 120.0f) < 0.0001f);
    assert(rx_pkt.mode == SATELLITE_MODE_NOMINAL);
    assert(rx_pkt.adcs_status == ADCS_STATUS_ENABLED);
    assert(rx_pkt.camera_status == CAMERA_STATUS_OFF);
    assert(rx_pkt.checksum == tx_pkt.checksum);
    printf("PASSED!\n");

    printf("\n>>> ALL C TESTS COMPLETED SUCCESSFULLY! <<<\n");
    return 0;
}
