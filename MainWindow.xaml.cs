using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using NAudio.CoreAudioApi;
using SoundboardApp.Models;
using SoundboardApp.Services;

namespace SoundboardApp
{
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<SoundButtonModel> _buttons = new();
        private readonly AudioEngine _audioEngine = new();
        private readonly GlobalKeyboardHook _hook = new();
        private readonly ConfigService _configService = new();

        // ใช้ระหว่างโหมด "กำลังรอรับ Hotkey ใหม่"
        private SoundButtonModel? _waitingForHotkey;

        public MainWindow()
        {
            InitializeComponent();
            SoundItemsControl.ItemsSource = _buttons;

            LoadDevices();
            LoadConfig();

            _audioEngine.PlaybackStarted += id => Dispatcher.Invoke(() => SetPlayingState(id, true));
            _audioEngine.PlaybackStopped += id => Dispatcher.Invoke(() => SetPlayingState(id, false));

            _hook.KeyCombinationPressed += Hook_KeyCombinationPressed;
            _hook.Start();

            Closing += (s, e) => SaveConfig();
        }

        // ---------- Device handling ----------

        private void LoadDevices()
        {
            var outputs = AudioEngine.GetOutputDevices();
            DeviceComboBox.ItemsSource = outputs;
            if (outputs.Count > 0)
            {
                // พยายามเลือก CABLE Input อัตโนมัติถ้ามี ไม่งั้นเลือกตัวแรก
                var cable = outputs.FirstOrDefault(d => d.FriendlyName.Contains("CABLE Input", StringComparison.OrdinalIgnoreCase));
                DeviceComboBox.SelectedItem = cable ?? outputs[0];
            }

            var inputs = AudioEngine.GetInputDevices();
            InputDeviceComboBox.ItemsSource = inputs;
            if (inputs.Count > 0)
            {
                // เลี่ยงเลือกอุปกรณ์ที่ชื่อมี CABLE (เพราะนั่นคือเสียงที่แอพส่งออกเอง ไม่ใช่ไมค์จริง)
                var realMic = inputs.FirstOrDefault(d => !d.FriendlyName.Contains("CABLE", StringComparison.OrdinalIgnoreCase));
                InputDeviceComboBox.SelectedItem = realMic ?? inputs[0];
            }
        }

        private void DeviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DeviceComboBox.SelectedItem is MMDevice device)
            {
                _audioEngine.SetOutputDevice(device);
                StatusText.Text = $"กำลังส่งเสียงออกไปที่: {device.FriendlyName}";
            }
        }

        private void InputDeviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (InputDeviceComboBox.SelectedItem is MMDevice device)
            {
                _audioEngine.SetInputDevice(device);
                if (_audioEngine.IsMicEnabled)
                    StatusText.Text = $"เปลี่ยนไมค์เป็น: {device.FriendlyName}";
            }
        }

        private void MicEnabled_Changed(object sender, RoutedEventArgs e)
        {
            bool enabled = MicEnabledCheckBox.IsChecked == true;
            _audioEngine.SetMicEnabled(enabled);
            StatusText.Text = enabled
                ? "เปิดผสมไมค์จริงแล้ว — เสียงพูด + soundboard จะถูกส่งออกพร้อมกัน"
                : "ปิดผสมไมค์จริงแล้ว — ส่งออกเฉพาะเสียง soundboard";
        }

        private void MicVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _audioEngine.SetMicVolume(e.NewValue);
        }

        // ---------- Config load/save ----------

        private void LoadConfig()
        {
            var config = _configService.Load();

            if (!string.IsNullOrEmpty(config.OutputDeviceId))
            {
                var devices = (DeviceComboBox.ItemsSource as IEnumerable<MMDevice>)?.ToList();
                var match = devices?.FirstOrDefault(d => d.ID == config.OutputDeviceId);
                if (match != null) DeviceComboBox.SelectedItem = match;
            }

            if (!string.IsNullOrEmpty(config.InputDeviceId))
            {
                var inputs = (InputDeviceComboBox.ItemsSource as IEnumerable<MMDevice>)?.ToList();
                var match = inputs?.FirstOrDefault(d => d.ID == config.InputDeviceId);
                if (match != null) InputDeviceComboBox.SelectedItem = match;
            }

            MicVolumeSlider.Value = config.MicVolume;
            _audioEngine.SetMicVolume(config.MicVolume);

            // ตั้งค่า checkbox ก่อน (นี่จะ trigger MicEnabled_Changed ซึ่งเปิด/ปิดไมค์ให้ตรงตาม config)
            MicEnabledCheckBox.IsChecked = config.MicEnabled;

            foreach (var b in config.Buttons)
            {
                _buttons.Add(new SoundButtonModel
                {
                    Name = b.Name,
                    FilePath = b.FilePath,
                    Ctrl = b.Ctrl,
                    Alt = b.Alt,
                    Shift = b.Shift,
                    Key = b.Key,
                    Volume = b.Volume
                });
            }
        }

        private void SaveConfig()
        {
            var config = new AppConfig
            {
                OutputDeviceId = _audioEngine.CurrentOutputDeviceId,
                InputDeviceId = _audioEngine.CurrentInputDeviceId,
                MicEnabled = _audioEngine.IsMicEnabled,
                MicVolume = MicVolumeSlider.Value,
                Buttons = _buttons.Select(b => new SoundButtonData
                {
                    Name = b.Name,
                    FilePath = b.FilePath,
                    Ctrl = b.Ctrl,
                    Alt = b.Alt,
                    Shift = b.Shift,
                    Key = b.Key,
                    Volume = b.Volume
                }).ToList()
            };
            _configService.Save(config);
        }

        // ---------- Sound button CRUD ----------

        private void AddSound_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "ไฟล์เสียง (*.mp3;*.wav;*.wma;*.aac;*.m4a)|*.mp3;*.wav;*.wma;*.aac;*.m4a|ทุกไฟล์|*.*",
                Title = "เลือกไฟล์เสียง"
            };
            if (dialog.ShowDialog() != true) return;

            var name = Path.GetFileNameWithoutExtension(dialog.FileName);
            _buttons.Add(new SoundButtonModel { Name = name, FilePath = dialog.FileName });
            StatusText.Text = $"เพิ่มเสียง '{name}' แล้ว — คลิก 'ตั้ง Hotkey' เพื่อกำหนดปุ่มลัด";
            SaveConfig();
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is not SoundButtonModel model) return;

            var dialog = new OpenFileDialog
            {
                Filter = "ไฟล์เสียง (*.mp3;*.wav;*.wma;*.aac;*.m4a)|*.mp3;*.wav;*.wma;*.aac;*.m4a|ทุกไฟล์|*.*",
                Title = "เลือกไฟล์เสียงใหม่ (หรือกด Cancel เพื่อแค่แก้ชื่อ)"
            };

            var renameDialog = new RenameDialog(model.Name);
            if (renameDialog.ShowDialog() == true)
            {
                model.Name = renameDialog.ResultName;
            }

            if (dialog.ShowDialog() == true)
            {
                model.FilePath = dialog.FileName;
            }
            SaveConfig();
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is not SoundButtonModel model) return;
            _audioEngine.Stop(GetButtonId(model));
            _buttons.Remove(model);
            SaveConfig();
        }

        // ---------- Playback ----------

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is not SoundButtonModel model) return;
            TriggerPlayback(model);
        }

        private void TriggerPlayback(SoundButtonModel model)
        {
            var id = GetButtonId(model);
            try
            {
                if (_audioEngine.IsPlaying(id))
                {
                    _audioEngine.Stop(id);
                }
                else
                {
                    if (string.IsNullOrEmpty(model.FilePath))
                    {
                        StatusText.Text = $"ปุ่ม '{model.Name}' ยังไม่ได้เลือกไฟล์เสียง";
                        return;
                    }
                    _audioEngine.Play(id, model.FilePath, model.Volume);
                    StatusText.Text = $"กำลังเล่น: {model.Name}";
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"เล่นเสียงไม่สำเร็จ: {ex.Message}";
            }
        }

        private void SetPlayingState(string buttonId, bool isPlaying)
        {
            var model = _buttons.FirstOrDefault(b => GetButtonId(b) == buttonId);
            if (model != null) model.IsPlaying = isPlaying;
        }

        private void StopAll_Click(object sender, RoutedEventArgs e)
        {
            _audioEngine.StopAll();
            StatusText.Text = "หยุดเสียงทั้งหมดแล้ว";
        }

        // ใช้ FilePath+Name เป็น id ง่าย ๆ (ไม่ต้องเพิ่ม field ใหม่ในโมเดล)
        private static string GetButtonId(SoundButtonModel model) => $"{model.Name}|{model.FilePath}";

        // ---------- Hotkey assignment ----------

        private void SetHotkey_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is not SoundButtonModel model) return;
            _waitingForHotkey = model;
            StatusText.Text = $"กำลังรอ... กดคีย์ผสมที่ต้องการสำหรับ '{model.Name}' (เช่น Ctrl+Alt+F1)";
        }

        private void Hook_KeyCombinationPressed(bool ctrl, bool alt, bool shift, string keyName)
        {
            // โหมดกำลังตั้ง Hotkey ใหม่ให้ปุ่มที่เลือกไว้
            if (_waitingForHotkey != null)
            {
                var target = _waitingForHotkey;
                Dispatcher.Invoke(() =>
                {
                    target.Ctrl = ctrl;
                    target.Alt = alt;
                    target.Shift = shift;
                    target.Key = keyName;
                    StatusText.Text = $"ตั้ง Hotkey '{target.HotkeyDisplay}' ให้ '{target.Name}' แล้ว";
                    SaveConfig();
                });
                _waitingForHotkey = null;
                return;
            }

            // โหมดปกติ: เช็คว่ามีปุ่มไหนตรงกับคีย์ที่กดหรือไม่ แล้วสั่งเล่น/หยุด
            var match = _buttons.FirstOrDefault(b =>
                b.HasHotkey && b.Ctrl == ctrl && b.Alt == alt && b.Shift == shift && b.Key == keyName);

            if (match != null)
            {
                Dispatcher.Invoke(() => TriggerPlayback(match));
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _hook.Dispose();
            _audioEngine.Dispose();
            base.OnClosed(e);
        }
    }
}
