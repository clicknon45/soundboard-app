using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace SoundboardApp.Services
{
    /// <summary>
    /// เอนจินเสียงหลัก ทำหน้าที่ 2 อย่างพร้อมกัน (แบบเดียวกับ Soundpad):
    ///  1) อัดเสียงจากไมค์จริง (input device) แบบเรียลไทม์
    ///  2) เล่นไฟล์เสียงจากปุ่ม soundboard
    /// แล้ว "มิกซ์" ทั้งสองอย่างรวมกัน ส่งออกไปยัง output device เดียว (เช่น CABLE Input)
    /// ทำให้ปลายทาง (Discord) ได้ยินทั้งเสียงพูดจริง + เสียง soundboard พร้อมกันในสตรีมเดียว
    /// </summary>
    public class AudioEngine : IDisposable
    {
        // รูปแบบเสียงกลางที่ใช้มิกซ์ทุกอย่างให้ตรงกัน (44.1kHz, stereo, float)
        // WASAPI shared mode จะ resample ให้อัตโนมัติหากอุปกรณ์จริงใช้ sample rate อื่น
        private static readonly WaveFormat MixFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);

        private WasapiOut? _output;
        private MixingSampleProvider? _mixer;
        private MMDevice? _outputDevice;

        private WasapiCapture? _capture;
        private BufferedWaveProvider? _micBuffer;
        private VolumeSampleProvider? _micVolumeProvider;
        private MMDevice? _inputDevice;
        private bool _micEnabled;
        private double _micVolume = 1.0;

        private readonly List<PlaybackSession> _active = new();

        public event Action<string>? PlaybackStarted; // ส่ง buttonId
        public event Action<string>? PlaybackStopped; // ส่ง buttonId

        // ---------- Device enumeration ----------

        public static List<MMDevice> GetOutputDevices()
        {
            using var enumerator = new MMDeviceEnumerator();
            return enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).ToList();
        }

        public static List<MMDevice> GetInputDevices()
        {
            using var enumerator = new MMDeviceEnumerator();
            return enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).ToList();
        }

        public string? CurrentOutputDeviceId => _outputDevice?.ID;
        public string? CurrentInputDeviceId => _inputDevice?.ID;
        public bool IsMicEnabled => _micEnabled;

        // ---------- Output ----------

        /// <summary>ตั้ง/สลับอุปกรณ์เสียงขาออก (จุดที่มิกซ์เสียงทั้งหมดถูกส่งไป เช่น CABLE Input)</summary>
        public void SetOutputDevice(MMDevice device)
        {
            // หยุดของเก่าทั้งหมดก่อน (เสียงที่กำลังเล่นค้างอยู่จะถูกตัดเมื่อสลับอุปกรณ์)
            StopAll();
            _output?.Stop();
            _output?.Dispose();

            _outputDevice = device;
            _mixer = new MixingSampleProvider(MixFormat) { ReadFully = true };

            _output = new WasapiOut(device, AudioClientShareMode.Shared, true, 50);
            _output.Init(_mixer);
            _output.Play();

            // ถ้าไมค์เปิดอยู่ก่อนสลับ output ให้ต่อไมค์เข้ามิกเซอร์ใหม่
            if (_micEnabled && _inputDevice != null)
            {
                AttachMicToMixer();
            }
        }

        // ---------- Microphone (live capture + mix) ----------

        /// <summary>ตั้งอุปกรณ์ไมค์จริงที่จะอัดเสียงเข้ามาผสม</summary>
        public void SetInputDevice(MMDevice device)
        {
            bool wasEnabled = _micEnabled;
            DetachMic();
            _inputDevice = device;
            if (wasEnabled) SetMicEnabled(true);
        }

        /// <summary>เปิด/ปิดการผสมเสียงไมค์จริงเข้าไปในสตรีมที่ส่งออก</summary>
        public void SetMicEnabled(bool enabled)
        {
            _micEnabled = enabled;
            if (enabled) AttachMicToMixer();
            else DetachMic();
        }

        /// <summary>ระดับเสียงไมค์ 0.0 - 2.0 (1.0 = ระดับปกติ)</summary>
        public void SetMicVolume(double volume)
        {
            _micVolume = volume;
            if (_micVolumeProvider != null) _micVolumeProvider.Volume = (float)volume;
        }

        private void AttachMicToMixer()
        {
            if (_inputDevice == null || _mixer == null) return;
            DetachMic(keepEnabledFlag: true);

            _capture = new WasapiCapture(_inputDevice);
            _micBuffer = new BufferedWaveProvider(_capture.WaveFormat)
            {
                DiscardOnBufferOverflow = true,
                BufferDuration = TimeSpan.FromSeconds(2)
            };
            _capture.DataAvailable += (s, e) => _micBuffer.AddSamples(e.Buffer, 0, e.BytesRecorded);
            _capture.RecordingStopped += (s, e) => { /* เงียบไว้ ไม่ต้อง crash ถ้าอุปกรณ์หลุด */ };

            ISampleProvider chain = _micBuffer.ToSampleProvider();
            chain = MatchChannels(chain, _mixer.WaveFormat.Channels);
            chain = MatchSampleRate(chain, _mixer.WaveFormat.SampleRate);

            _micVolumeProvider = new VolumeSampleProvider(chain) { Volume = (float)_micVolume };
            _mixer.AddMixerInput(_micVolumeProvider);

            _capture.StartRecording();
        }

        private void DetachMic(bool keepEnabledFlag = false)
        {
            if (_capture != null)
            {
                try { _capture.StopRecording(); } catch { /* ignore */ }
                _capture.Dispose();
                _capture = null;
            }
            if (_micVolumeProvider != null && _mixer != null)
            {
                _mixer.RemoveMixerInput(_micVolumeProvider);
                _micVolumeProvider = null;
            }
            _micBuffer = null;
            if (!keepEnabledFlag) _micEnabled = false;
        }

        // ---------- Soundboard playback ----------

        /// <summary>เล่นไฟล์เสียง 1 ไฟล์ ผสมเข้าไปในสตรีมที่กำลังส่งออกอยู่ (ไม่ตัดเสียงไมค์หรือเสียงอื่นที่กำลังเล่น)</summary>
        public void Play(string buttonId, string filePath, double volume)
        {
            if (_mixer == null)
                throw new InvalidOperationException("ยังไม่ได้เลือกอุปกรณ์เสียงขาออก (Output Device)");
            if (!File.Exists(filePath))
                throw new FileNotFoundException("ไม่พบไฟล์เสียง", filePath);

            var reader = new AudioFileReader(filePath) { Volume = (float)Math.Clamp(volume, 0, 1) };

            ISampleProvider chain = reader;
            chain = MatchChannels(chain, _mixer.WaveFormat.Channels);
            chain = MatchSampleRate(chain, _mixer.WaveFormat.SampleRate);

            var notifying = new NotifyingSampleProvider(chain);
            var session = new PlaybackSession(buttonId, reader, notifying);

            notifying.Finished += () =>
            {
                lock (_active) { _active.Remove(session); }
                _mixer.RemoveMixerInput(notifying);
                reader.Dispose();
                PlaybackStopped?.Invoke(buttonId);
            };

            lock (_active) { _active.Add(session); }
            _mixer.AddMixerInput(notifying);
            PlaybackStarted?.Invoke(buttonId);
        }

        /// <summary>หยุดเสียงทั้งหมดที่กำลังเล่นของปุ่มนี้</summary>
        public void Stop(string buttonId)
        {
            List<PlaybackSession> toStop;
            lock (_active) { toStop = _active.Where(s => s.ButtonId == buttonId).ToList(); }

            foreach (var s in toStop)
            {
                _mixer?.RemoveMixerInput(s.Provider);
                s.Reader.Dispose();
                lock (_active) { _active.Remove(s); }
                PlaybackStopped?.Invoke(buttonId);
            }
        }

        /// <summary>หยุดเสียง soundboard ทั้งหมด (ไม่กระทบไมค์)</summary>
        public void StopAll()
        {
            List<PlaybackSession> toStop;
            lock (_active) { toStop = _active.ToList(); }
            foreach (var s in toStop) Stop(s.ButtonId);
        }

        public bool IsPlaying(string buttonId)
        {
            lock (_active) { return _active.Any(s => s.ButtonId == buttonId); }
        }

        // ---------- Format helpers ----------

        private static ISampleProvider MatchChannels(ISampleProvider input, int targetChannels)
        {
            if (input.WaveFormat.Channels == targetChannels) return input;
            if (input.WaveFormat.Channels == 1 && targetChannels == 2) return new MonoToStereoSampleProvider(input);
            if (input.WaveFormat.Channels == 2 && targetChannels == 1) return new StereoToMonoSampleProvider(input);
            return input; // กรณีแปลก ๆ (เช่น 5.1) ปล่อยผ่านไปก่อน
        }

        private static ISampleProvider MatchSampleRate(ISampleProvider input, int targetRate)
        {
            return input.WaveFormat.SampleRate == targetRate
                ? input
                : new WdlResamplingSampleProvider(input, targetRate);
        }

        public void Dispose()
        {
            StopAll();
            DetachMic();
            _output?.Stop();
            _output?.Dispose();
            _outputDevice?.Dispose();
            _inputDevice?.Dispose();
        }

        private class PlaybackSession
        {
            public string ButtonId { get; }
            public AudioFileReader Reader { get; }
            public NotifyingSampleProvider Provider { get; }

            public PlaybackSession(string buttonId, AudioFileReader reader, NotifyingSampleProvider provider)
            {
                ButtonId = buttonId;
                Reader = reader;
                Provider = provider;
            }
        }

        /// <summary>ห่อ ISampleProvider เพื่อรู้ว่าเล่นจบเมื่อไหร่ (Read คืนค่า 0) จะได้เอาออกจากมิกเซอร์อัตโนมัติ</summary>
        private class NotifyingSampleProvider : ISampleProvider
        {
            private readonly ISampleProvider _source;
            private bool _finishedRaised;

            public event Action? Finished;

            public NotifyingSampleProvider(ISampleProvider source) => _source = source;

            public WaveFormat WaveFormat => _source.WaveFormat;

            public int Read(float[] buffer, int offset, int count)
            {
                int read = _source.Read(buffer, offset, count);
                if (read == 0 && !_finishedRaised)
                {
                    _finishedRaised = true;
                    Finished?.Invoke();
                }
                return read;
            }
        }
    }
}
