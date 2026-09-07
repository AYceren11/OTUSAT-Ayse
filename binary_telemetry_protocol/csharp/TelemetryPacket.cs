using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Otusat.GroundSegment.Protocol
{
    /// <summary>
    /// Uydunun mevcut çalışma modu (TM_005)
    /// </summary>
    public enum SatelliteMode : byte
    {
        Boot = 0,
        Safe = 1,
        Nominal = 2
    }

    /// <summary>
    /// ADCS (Yönelim Belirleme ve Kontrol Sistemi) durumu (TM_006)
    /// </summary>
    public enum AdcsStatus : byte
    {
        Disabled = 0,
        Enabled = 1
    }

    /// <summary>
    /// Faydalı Yük Kamera durumu (TM_007)
    /// </summary>
    public enum CameraStatus : byte
    {
        Off = 0,
        Active = 1
    }

    /// <summary>
    /// Standart Telemetri Parametre Numaraları
    /// </summary>
    public enum TelemetryId : byte
    {
        Temperature = 1,     // TM_001
        Pressure = 2,        // TM_002
        BatteryVoltage = 3,  // TM_003
        BatteryCurrent = 4,  // TM_004
        Mode = 5,            // TM_005
        AdcsStatus = 6,      // TM_006
        CameraStatus = 7     // TM_007
    }

    /// <summary>
    /// Standart Telekomut Numaraları
    /// </summary>
    public enum TelecommandId : byte
    {
        Ping = 1,          // TC_001
        GetStatus = 2,     // TC_002
        GetSensor = 3,     // TC_003
        TakePhoto = 4,     // TC_004
        SetMode = 5,       // TC_005
        AdcsEnable = 6,    // TC_006
        Reset = 7          // TC_007
    }

    /// <summary>
    /// OTUSAT CubeSat İkili (Binary) Telemetri Paketi (39 Bayt)
    /// STM32 Uçuş Bilgisayarı ve C# Yer İstasyonu arasında ortak kullanılan standart protokol.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TelemetryPacket
    {
        public const ushort ExpectedSyncWord = 0xABCD;
        public const int PacketSize = 39;
        public const int CrcDataSize = 37;

        // --- Başlık (Header: Offset 0..5, 6 Bayt) ---
        public ushort SyncWord { get; set; }           // Offset 0: 0xABCD
        public uint TimestampMs { get; set; }          // Offset 2: Başlangıçtan beri geçen süre (ms)

        // --- Sensör ve Güç Verileri (Payload: Offset 6..21, 16 Bayt) ---
        public float Temperature { get; set; }         // Offset 6: Sıcaklık (°C) - TM_001
        public float Pressure { get; set; }            // Offset 10: Basınç (hPa) - TM_002
        public float BatteryVoltage { get; set; }      // Offset 14: Batarya Gerilimi (V) - TM_003
        public float BatteryCurrent { get; set; }      // Offset 18: Batarya Akımı (A) - TM_004

        // --- Yönelim Verileri (Attitude: Offset 22..33, 12 Bayt) ---
        public float AttitudeRoll { get; set; }        // Offset 22: Roll Açısı (°)
        public float AttitudePitch { get; set; }       // Offset 26: Pitch Açısı (°)
        public float AttitudeYaw { get; set; }         // Offset 30: Yaw Açısı (°)

        // --- Alt Sistem Durumları (Offset 34..36, 3 Bayt) ---
        public SatelliteMode Mode { get; set; }        // Offset 34: 0=Boot, 1=Safe, 2=Nominal - TM_005
        public AdcsStatus AdcsStatus { get; set; }     // Offset 35: 0=Kapalı, 1=Açık - TM_006
        public CameraStatus CameraStatus { get; set; } // Offset 36: 0=Kapalı, 1=Açık - TM_007

        // --- Kontrol Bloğu (Offset 37..38, 2 Bayt) ---
        public ushort Checksum { get; set; }           // Offset 37: CRC-16-CCITT (0x1021)

        /// <summary>
        /// Uptime süresini TimeSpan nesnesi olarak döner.
        /// </summary>
        public TimeSpan Uptime => TimeSpan.FromMilliseconds(TimestampMs);

        /// <summary>
        /// Yeni bir telemetri paketi örneği oluşturur.
        /// </summary>
        public static TelemetryPacket CreateDefault()
        {
            return new TelemetryPacket
            {
                SyncWord = ExpectedSyncWord,
                TimestampMs = 0,
                Temperature = 25.0f,
                Pressure = 1013.25f,
                BatteryVoltage = 8.4f,
                BatteryCurrent = 0.35f,
                AttitudeRoll = 0.0f,
                AttitudePitch = 0.0f,
                AttitudeYaw = 0.0f,
                Mode = SatelliteMode.Nominal,
                AdcsStatus = AdcsStatus.Enabled,
                CameraStatus = CameraStatus.Off,
                Checksum = 0
            };
        }

        /// <summary>
        /// CRC-16-CCITT (Polinom: 0x1021, Başlangıç Değeri: 0xFFFF) hesaplar.
        /// STM32 C kodu ile %100 birebir aynı algoritmadır.
        /// </summary>
        public static ushort CalculateCrc16(ReadOnlySpan<byte> data)
        {
            ushort crc = 0xFFFF;
            for (int i = 0; i < data.Length; i++)
            {
                crc ^= (ushort)(data[i] << 8);
                for (int bit = 0; bit < 8; bit++)
                {
                    if ((crc & 0x8000) != 0)
                    {
                        crc = (ushort)((crc << 1) ^ 0x1021);
                    }
                    else
                    {
                        crc = (ushort)(crc << 1);
                    }
                }
            }
            return crc;
        }

        /// <summary>
        /// Paketi 39 baytlık ikili (binary) diziye dönüştürür (Serialize) ve CRC'sini otomatik hesaplayıp ekler.
        /// </summary>
        public byte[] Serialize()
        {
            byte[] buffer = new byte[PacketSize];
            SerializeTo(buffer);
            return buffer;
        }

        /// <summary>
        /// Paketi verilen Span hedefine serileştirir.
        /// </summary>
        public void SerializeTo(Span<byte> destination)
        {
            if (destination.Length < PacketSize)
            {
                throw new ArgumentException($"Hedef dizi en az {PacketSize} bayt olmalıdır.", nameof(destination));
            }

            // Little-Endian formatında alanları yerleştir
            BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(0, 2), ExpectedSyncWord);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(2, 4), TimestampMs);

            BinaryPrimitives.WriteSingleLittleEndian(destination.Slice(6, 4), Temperature);
            BinaryPrimitives.WriteSingleLittleEndian(destination.Slice(10, 4), Pressure);
            BinaryPrimitives.WriteSingleLittleEndian(destination.Slice(14, 4), BatteryVoltage);
            BinaryPrimitives.WriteSingleLittleEndian(destination.Slice(18, 4), BatteryCurrent);

            BinaryPrimitives.WriteSingleLittleEndian(destination.Slice(22, 4), AttitudeRoll);
            BinaryPrimitives.WriteSingleLittleEndian(destination.Slice(26, 4), AttitudePitch);
            BinaryPrimitives.WriteSingleLittleEndian(destination.Slice(30, 4), AttitudeYaw);

            destination[34] = (byte)Mode;
            destination[35] = (byte)AdcsStatus;
            destination[36] = (byte)CameraStatus;

            // İlk 37 bayt üzerinden CRC hesapla ve son 2 bayta Little-Endian yaz
            ushort computedCrc = CalculateCrc16(destination.Slice(0, CrcDataSize));
            BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(37, 2), computedCrc);
        }

        /// <summary>
        /// 39 baytlık ikili telemetri paketini çözümler (Deserialize) ve doğrular.
        /// </summary>
        public static TelemetryPacket Deserialize(ReadOnlySpan<byte> buffer)
        {
            if (!TryParse(buffer, out TelemetryPacket packet, out string error))
            {
                throw new InvalidDataException($"Telemetri paketi çözümlenemedi: {error}");
            }
            return packet;
        }

        /// <summary>
        /// Ham bayt akışından güvenli, istisna fırlatmayan telemetri çözümleme metodu (Yer İstasyonu için ideal).
        /// </summary>
        public static bool TryParse(ReadOnlySpan<byte> buffer, out TelemetryPacket packet, out string errorMessage)
        {
            packet = default;
            errorMessage = string.Empty;

            if (buffer.Length < PacketSize)
            {
                errorMessage = $"Eksik veri boyutu: Beklenen {PacketSize} bayt, gelen {buffer.Length} bayt.";
                return false;
            }

            // 1. Sync Word Kontrolü
            ushort sync = BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(0, 2));
            if (sync != ExpectedSyncWord)
            {
                errorMessage = $"Geçersiz Sync Word: Beklenen 0x{ExpectedSyncWord:X4}, gelen 0x{sync:X4}.";
                return false;
            }

            // 2. CRC Kontrolü
            ushort receivedCrc = BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(37, 2));
            ushort computedCrc = CalculateCrc16(buffer.Slice(0, CrcDataSize));
            if (receivedCrc != computedCrc)
            {
                errorMessage = $"CRC Hatası: Hesaplanan 0x{computedCrc:X4}, Paketteki 0x{receivedCrc:X4}. Paket bozulmuş!";
                return false;
            }

            // 3. Verileri Ayrıştır
            packet.SyncWord = sync;
            packet.TimestampMs = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(2, 4));

            packet.Temperature = BinaryPrimitives.ReadSingleLittleEndian(buffer.Slice(6, 4));
            packet.Pressure = BinaryPrimitives.ReadSingleLittleEndian(buffer.Slice(10, 4));
            packet.BatteryVoltage = BinaryPrimitives.ReadSingleLittleEndian(buffer.Slice(14, 4));
            packet.BatteryCurrent = BinaryPrimitives.ReadSingleLittleEndian(buffer.Slice(18, 4));

            packet.AttitudeRoll = BinaryPrimitives.ReadSingleLittleEndian(buffer.Slice(22, 4));
            packet.AttitudePitch = BinaryPrimitives.ReadSingleLittleEndian(buffer.Slice(26, 4));
            packet.AttitudeYaw = BinaryPrimitives.ReadSingleLittleEndian(buffer.Slice(30, 4));

            packet.Mode = (SatelliteMode)buffer[34];
            packet.AdcsStatus = (AdcsStatus)buffer[35];
            packet.CameraStatus = (CameraStatus)buffer[36];
            packet.Checksum = receivedCrc;

            return true;
        }

        /// <summary>
        /// Yer istasyonu konsolunda veya loglarda okunabilir formatlanmış metin çıktısı.
        /// </summary>
        public string ToFormattedString()
        {
            var sb = new StringBuilder();
            sb.AppendLine("================ OTUSAT TELEMETRİ VERİSİ ================");
            sb.AppendLine($"[0x00] Sync Word       : 0x{SyncWord:X4} (OK)");
            sb.AppendLine($"[0x02] Uptime          : {TimestampMs} ms ({Uptime.TotalSeconds:F2} sn)");
            sb.AppendLine($"[0x06] Sıcaklık (TM_001): {Temperature:F2} °C");
            sb.AppendLine($"[0x0A] Basınç   (TM_002): {Pressure:F2} hPa");
            sb.AppendLine($"[0x0E] Gerilim  (TM_003): {BatteryVoltage:F2} V");
            sb.AppendLine($"[0x12] Akım     (TM_004): {BatteryCurrent:F3} A");
            sb.AppendLine($"[0x16] Roll Açısı      : {AttitudeRoll:F2} °");
            sb.AppendLine($"[0x1A] Pitch Açısı     : {AttitudePitch:F2} °");
            sb.AppendLine($"[0x1E] Yaw Açısı       : {AttitudeYaw:F2} °");
            sb.AppendLine($"[0x22] Mod      (TM_005): {Mode} ({(byte)Mode})");
            sb.AppendLine($"[0x23] ADCS     (TM_006): {AdcsStatus} ({(byte)AdcsStatus})");
            sb.AppendLine($"[0x24] Kamera   (TM_007): {CameraStatus} ({(byte)CameraStatus})");
            sb.AppendLine($"[0x25] Checksum (CRC)  : 0x{Checksum:X4}");
            sb.AppendLine("=========================================================");
            return sb.ToString();
        }

        /// <summary>
        /// Veritabanına veya arayüze kolay aktarım için JSON benzeri temsil
        /// </summary>
        public string ToJson()
        {
            return $"{{\"sync\":\"0x{SyncWord:X4}\",\"timestamp_ms\":{TimestampMs},\"temperature\":{Temperature:F2},\"pressure\":{Pressure:F2},\"battery_voltage\":{BatteryVoltage:F2},\"battery_current\":{BatteryCurrent:F3},\"roll\":{AttitudeRoll:F2},\"pitch\":{AttitudePitch:F2},\"yaw\":{AttitudeYaw:F2},\"mode\":\"{Mode}\",\"adcs\":\"{AdcsStatus}\",\"camera\":\"{CameraStatus}\",\"crc\":\"0x{Checksum:X4}\"}}";
        }
    }
}
