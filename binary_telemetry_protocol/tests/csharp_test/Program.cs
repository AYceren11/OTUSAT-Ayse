using System;
using System.Runtime.InteropServices;
using Otusat.GroundSegment.Protocol;

namespace Otusat.GroundSegment.Tests
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=====================================================");
            Console.WriteLine("  OTUSAT Ground Segment C# Telemetry Protocol Tests  ");
            Console.WriteLine("=====================================================");

            // 1. Struct Boyutu Testi
            Console.Write("[TEST 1] Struct Boyutu Kontrolü (39 Bayt)... ");
            int structSize = Marshal.SizeOf<TelemetryPacket>();
            if (structSize != 39 || TelemetryPacket.PacketSize != 39)
            {
                throw new Exception($"HATA: Struct boyutu {structSize} bayt, 39 olmalı!");
            }
            Console.WriteLine($"BAŞARILI! (Boyut = {structSize} bayt)");

            // 2. C'den Gelen Ham Bayt Dizisinin C# Tarafında Çözümlenmesi (Çapraz Uyumluluk)
            // Ham hex: CD AB E8 03 00 00 00 00 C8 41 00 00 80 44 9A 99 01 41 CD CC CC 3E 00 00 B4 42 00 00 34 42 00 00 F0 42 02 01 00 BC A7
            Console.Write("[TEST 2] C Tarafında Üretilen Ham Baytların Çözümlenmesi (C -> C#)... ");
            byte[] cGeneratedBytes = new byte[] {
                0xCD, 0xAB,                               // Sync Word: 0xABCD
                0xE8, 0x03, 0x00, 0x00,                   // Timestamp: 1000 ms
                0x00, 0x00, 0xC8, 0x41,                   // Temp: 25.0 °C
                0x00, 0x00, 0x80, 0x44,                   // Pressure: 1024.0 hPa
                0x9A, 0x99, 0x01, 0x41,                   // Battery Voltage: 8.1 V
                0xCD, 0xCC, 0xCC, 0x3E,                   // Battery Current: 0.400 A
                0x00, 0x00, 0xB4, 0x42,                   // Roll: 90.0 °
                0x00, 0x00, 0x34, 0x42,                   // Pitch: 45.0 °
                0x00, 0x00, 0xF0, 0x42,                   // Yaw: 120.0 °
                0x02,                                     // Mode: Nominal (2)
                0x01,                                     // ADCS: Enabled (1)
                0x00,                                     // Camera: Off (0)
                0xA7, 0xA1                                // CRC-16: 0xA1A7
            };

            bool parseSuccess = TelemetryPacket.TryParse(cGeneratedBytes, out TelemetryPacket rxPacket, out string error);
            if (!parseSuccess)
            {
                throw new Exception($"Parse başarısız oldu: {error}");
            }

            if (rxPacket.SyncWord != 0xABCD ||
                rxPacket.TimestampMs != 1000 ||
                Math.Abs(rxPacket.Temperature - 25.0f) > 0.001f ||
                Math.Abs(rxPacket.Pressure - 1024.0f) > 0.001f ||
                Math.Abs(rxPacket.BatteryVoltage - 8.1f) > 0.001f ||
                Math.Abs(rxPacket.BatteryCurrent - 0.400f) > 0.001f ||
                Math.Abs(rxPacket.AttitudeRoll - 90.0f) > 0.001f ||
                Math.Abs(rxPacket.AttitudePitch - 45.0f) > 0.001f ||
                Math.Abs(rxPacket.AttitudeYaw - 120.0f) > 0.001f ||
                rxPacket.Mode != SatelliteMode.Nominal ||
                rxPacket.AdcsStatus != AdcsStatus.Enabled ||
                rxPacket.CameraStatus != CameraStatus.Off ||
                rxPacket.Checksum != 0xA1A7)
            {
                throw new Exception("Çözümlenen değerler beklenen değerlerle eşleşmedi!");
            }
            Console.WriteLine("BAŞARILI!");

            // 3. C# Tarafında Serileştirme ve CRC Eşleşmesi (C# -> C Uyumluluğu)
            Console.Write("[TEST 3] C# Serileştirme ve Otomatik CRC Hesabı... ");
            TelemetryPacket txPacket = new TelemetryPacket
            {
                TimestampMs = 1000,
                Temperature = 25.0f,
                Pressure = 1024.0f,
                BatteryVoltage = 8.1f,
                BatteryCurrent = 0.400f,
                AttitudeRoll = 90.0f,
                AttitudePitch = 45.0f,
                AttitudeYaw = 120.0f,
                Mode = SatelliteMode.Nominal,
                AdcsStatus = AdcsStatus.Enabled,
                CameraStatus = CameraStatus.Off
            };

            byte[] csharpSerializedBytes = txPacket.Serialize();
            if (csharpSerializedBytes.Length != 39)
            {
                throw new Exception($"Serileştirilen bayt boyutu {csharpSerializedBytes.Length} bayt, 39 olmalı!");
            }

            for (int i = 0; i < 39; i++)
            {
                if (csharpSerializedBytes[i] != cGeneratedBytes[i])
                {
                    throw new Exception($"Bayt uyuşmazlığı tespit edildi! Offset {i}: C#=0x{csharpSerializedBytes[i]:X2}, C=0x{cGeneratedBytes[i]:X2}");
                }
            }
            Console.WriteLine("BAŞARILI! (C ve C# çıktıları bayt bayt %100 özdeş)");

            // 4. Bozuk Paket / CRC Hata Tespiti
            Console.Write("[TEST 4] Bozuk Paket ve CRC Hata Yakalama... ");
            byte[] corruptedBytes = (byte[])cGeneratedBytes.Clone();
            corruptedBytes[10] ^= 0x55; // Basınç verisini boz
            bool corruptResult = TelemetryPacket.TryParse(corruptedBytes, out _, out string corruptMsg);
            if (corruptResult)
            {
                throw new Exception("HATA: Bozuk paketin kabul edilmemesi gerekiyordu!");
            }
            Console.WriteLine($"BAŞARILI! (Hata mesajı: {corruptMsg})");

            // 5. Konsol ve JSON Formatı Dökümü
            Console.WriteLine("\n[TEST 5] Formatlanmış Telemetri Görünümü:");
            Console.WriteLine(rxPacket.ToFormattedString());

            Console.WriteLine("[TEST 6] JSON Formatı:");
            Console.WriteLine(rxPacket.ToJson());

            Console.WriteLine("\n>>> TÜM C# TESTLERİ BAŞARIYLA TAMAMLANDI! <<<");
        }
    }
}
