# OTUSAT — CubeSat Binary Telemetry Protocol

[![Language: C](https://img.shields.io/badge/Language-C99%20%2F%20C%2B%2B-blue.svg)](#)
[![Language: C#](https://img.shields.io/badge/Language-C%23%20%2F%20.NET%209-purple.svg)](#)
[![Protocol Size](https://img.shields.io/badge/Packet%20Size-39%20Bytes-success.svg)](#)
[![Error Detection](https://img.shields.io/badge/Checksum-CRC--16--CCITT-orange.svg)](#)

A deterministic, lightweight, and cross-platform **Binary Telemetry Serialization & Deserialization Protocol** designed for CubeSat On-Board Computers (OBC) and Ground Segment Control Software.

---

## 📌 Overview

In small satellite and LoRa RF communications, bandwidth and power are strictly constrained. This protocol provides a fixed **39-byte compact frame format** to transmit critical spacecraft telemetry with minimal overhead and reliable error detection.

Both embedded **C/C++** (for STM32/ARM Cortex-M flight software) and **C# (.NET)** (for Ground Station mission control) implementations are provided with 100% byte-for-byte interoperability.

---

## 🚀 Key Features

* **39-Byte Fixed-Length Frame:** Optimized for low-bandwidth LoRa/FSK RF downlinks.
* **CRC-16-CCITT Error Detection:** Polynomial `0x1021` (`0xFFFF` init) guarantees packet integrity over noisy channels.
* **Byte-Aligned Architecture:** Strict 1-byte struct alignment (`pack=1`) eliminating compiler padding discrepancies.
* **Little-Endian Standard:** Native alignment on both ARM Cortex-M and modern x86/x64 systems.
* **Zero-Allocation Parser:** Fast `Span<byte>` and `BinaryPrimitives` in C# for high-throughput receiver loops.

---

## 📁 Repository Structure

```
├── binary_telemetry_protocol/
│   ├── include/
│   │   └── telemetry_packet.h        # C/C++ Header file (STM32 / Flight Software)
│   ├── src/
│   │   └── telemetry_packet.c        # C/C++ Implementation & CRC engine
│   ├── csharp/
│   │   └── TelemetryPacket.cs        # C# .NET Ground Segment Library
│   └── tests/
│       ├── test_telemetry.c          # C verification & unit test suite
│       └── csharp_test/              # .NET cross-compatibility test project
│           ├── csharp_test.csproj
│           └── Program.cs
└── README.md
```

---

## 📊 Telemetry Frame Structure (39 Bytes)

| Offset | Field Name | Data Type | Units / Description |
|---|---|---|---|
| `0..1` | **Sync Word** | `uint16_t` (2B) | Frame preamble (`0xABCD`) |
| `2..5` | **Timestamp** | `uint32_t` (4B) | Spacecraft uptime in milliseconds |
| `6..9` | **Temperature** | `float` (4B) | On-board temperature (°C) |
| `10..13` | **Pressure** | `float` (4B) | Ambient pressure (hPa) |
| `14..17` | **Battery Voltage** | `float` (4B) | Main power bus voltage (V) |
| `18..21` | **Battery Current** | `float` (4B) | Total current draw (A) |
| `22..25` | **Attitude Roll** | `float` (4B) | Orientation roll angle (°) |
| `26..29` | **Attitude Pitch** | `float` (4B) | Orientation pitch angle (°) |
| `30..33` | **Attitude Yaw** | `float` (4B) | Orientation yaw angle (°) |
| `34` | **Mode** | `uint8_t` (1B) | Satellite mode (`0=Boot`, `1=Safe`, `2=Nominal`) |
| `35` | **ADCS Status** | `uint8_t` (1B) | Attitude system state (`0=Off`, `1=On`) |
| `36` | **Camera Status** | `uint8_t` (1B) | Payload state (`0=Off`, `1=Active`) |
| `37..38` | **CRC Checksum** | `uint16_t` (2B) | CRC-16-CCITT across bytes `0..36` |

---

## 💻 Quick Start

### 1. Embedded C / STM32 (Flight Software)

```c
#include "telemetry_packet.h"

TelemetryPacket_t packet;
telemetry_packet_init(&packet);

packet.timestamp_ms    = HAL_GetTick();
packet.temperature     = 24.5f;
packet.pressure        = 1013.25f;
packet.battery_voltage = 8.2f;
packet.battery_current = 0.35f;
packet.mode            = SATELLITE_MODE_NOMINAL;

uint8_t tx_buffer[TELEMETRY_PACKET_SIZE];
if (telemetry_serialize(&packet, tx_buffer, sizeof(tx_buffer)) == 0) {
    LoRa_Transmit(tx_buffer, TELEMETRY_PACKET_SIZE);
}
```

### 2. C# .NET (Ground Segment)

```csharp
using Otusat.GroundSegment.Protocol;

byte[] receivedBuffer = serialPort.ReadExistingBytes();

if (TelemetryPacket.TryParse(receivedBuffer, out TelemetryPacket packet, out string error))
{
    Console.WriteLine($"[TELEMETRY] Temp: {packet.Temperature:F2} °C, Battery: {packet.BatteryVoltage:F2} V");
    Console.WriteLine(packet.ToFormattedString());
}
else
{
    Console.WriteLine($"[INVALID PACKET] {error}");
}
```

---

## 🧪 Verification & Testing

Both C and C# test suites validate packet serialization, round-trip deserialization, and CRC rejection on bit-flip corruption.

```bash
# Run C Unit Test
gcc -Wall -Wextra -I"binary_telemetry_protocol/include" binary_telemetry_protocol/src/telemetry_packet.c binary_telemetry_protocol/tests/test_telemetry.c -o test_c && ./test_c

# Run C# .NET Test
dotnet run --project binary_telemetry_protocol/tests/csharp_test/csharp_test.csproj
```
