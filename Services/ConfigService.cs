using System.IO;
using System.Text.Json;
using SoundboardApp.Models;

namespace SoundboardApp.Services
{
    /// <summary>
    /// บันทึก/โหลดการตั้งค่าโปรแกรม (รายการปุ่มเสียง + อุปกรณ์ที่เลือก)
    /// เก็บไว้ที่ %AppData%\SoundboardApp\config.json
    /// </summary>
    public class ConfigService
    {
        private readonly string _folderPath;
        private readonly string _filePath;

        public ConfigService()
        {
            _folderPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SoundboardApp");
            _filePath = Path.Combine(_folderPath, "config.json");
        }

        public AppConfig Load()
        {
            try
            {
                if (!File.Exists(_filePath)) return new AppConfig();
                var json = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
            }
            catch
            {
                // ถ้าไฟล์ config เสีย ให้เริ่มต้นใหม่แทนที่จะ crash
                return new AppConfig();
            }
        }

        public void Save(AppConfig config)
        {
            Directory.CreateDirectory(_folderPath);
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }
    }
}
