namespace SoundboardApp.Models
{
    /// <summary>
    /// ข้อมูลตั้งค่าทั้งหมดของแอพ ใช้บันทึก/โหลดจาก config.json
    /// </summary>
    public class AppConfig
    {
        public string? OutputDeviceId { get; set; }
        public string? InputDeviceId { get; set; }
        public bool MicEnabled { get; set; }
        public double MicVolume { get; set; } = 1.0;
        public List<SoundButtonData> Buttons { get; set; } = new();
    }

    /// <summary>
    /// รูปแบบข้อมูลของปุ่มเสียงที่ใช้ตอน serialize/deserialize (plain data, ไม่มี INotifyPropertyChanged)
    /// </summary>
    public class SoundButtonData
    {
        public string Name { get; set; } = "New Sound";
        public string FilePath { get; set; } = string.Empty;
        public bool Ctrl { get; set; }
        public bool Alt { get; set; }
        public bool Shift { get; set; }
        public string Key { get; set; } = string.Empty;
        public double Volume { get; set; } = 1.0;
    }
}
