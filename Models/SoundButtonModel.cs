using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SoundboardApp.Models
{
    /// <summary>
    /// ข้อมูลของปุ่มเสียง 1 ปุ่มในบอร์ด
    /// </summary>
    public class SoundButtonModel : INotifyPropertyChanged
    {
        private string _name = "New Sound";
        private string _filePath = string.Empty;
        private bool _ctrl;
        private bool _alt;
        private bool _shift;
        private string _key = string.Empty; // เช่น "F1", "D1", "A"
        private double _volume = 1.0;
        private bool _isPlaying;

        public string Name
        {
            get => _name;
            set { _name = value; OnChanged(); }
        }

        public string FilePath
        {
            get => _filePath;
            set { _filePath = value; OnChanged(); }
        }

        public bool Ctrl { get => _ctrl; set { _ctrl = value; OnChanged(); OnChanged(nameof(HotkeyDisplay)); } }
        public bool Alt { get => _alt; set { _alt = value; OnChanged(); OnChanged(nameof(HotkeyDisplay)); } }
        public bool Shift { get => _shift; set { _shift = value; OnChanged(); OnChanged(nameof(HotkeyDisplay)); } }

        public string Key
        {
            get => _key;
            set { _key = value; OnChanged(); OnChanged(nameof(HotkeyDisplay)); }
        }

        public double Volume
        {
            get => _volume;
            set { _volume = value; OnChanged(); }
        }

        public bool IsPlaying
        {
            get => _isPlaying;
            set { _isPlaying = value; OnChanged(); }
        }

        /// <summary>ข้อความแสดง Hotkey เช่น "Ctrl+Alt+F1"</summary>
        public string HotkeyDisplay
        {
            get
            {
                if (string.IsNullOrEmpty(Key)) return "(ไม่ได้ตั้ง)";
                var parts = new List<string>();
                if (Ctrl) parts.Add("Ctrl");
                if (Alt) parts.Add("Alt");
                if (Shift) parts.Add("Shift");
                parts.Add(Key);
                return string.Join("+", parts);
            }
        }

        public bool HasHotkey => !string.IsNullOrEmpty(Key);

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
