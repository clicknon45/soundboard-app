## วิธีใช้งาน

โปรแกรมนี้ **มิกซ์เสียงไมค์จริง + เสียง soundboard ให้เองในตัว** (แบบเดียวกับ Soundpad)
ไม่ต้องไปตั้งค่า "Listen to this device" ใน Windows Sound Settings อีกต่อไป

1. เปิดโปรแกรม → แถวบนสุดเลือก **Output Device** เป็น **"CABLE Input"**
   (โปรแกรมจะพยายามเลือกให้อัตโนมัติถ้าเจอ)
2. แถวที่สอง ✔ ติ๊ก **"🎙 ผสมเสียงไมค์จริงเข้าไปด้วย"** แล้วเลือกไมค์จริงของคุณในช่อง Input Device
   ที่อยู่ข้าง ๆ (โปรแกรมจะเลี่ยงเลือกอุปกรณ์ชื่อ CABLE ให้อัตโนมัติ เพราะนั่นไม่ใช่ไมค์จริง)
3. ปรับ **ระดับเสียงไมค์** ด้วย slider ข้าง ๆ ได้ตามต้องการ (ค่าเริ่มต้น 1.0 = ระดับปกติ)
4. กด **"➕ เพิ่มเสียง"** เพื่อเลือกไฟล์เสียง (.mp3 / .wav / .m4a ฯลฯ) — จะขึ้นเป็นการ์ดใหม่
5. กด **"⌨ ตั้ง Hotkey"** ที่การ์ดนั้น แล้วกดคีย์ผสมที่ต้องการ (เช่น Ctrl+Alt+F1) — ระบบจะจับอัตโนมัติ
6. กด **"▶ เล่น"** บนการ์ด หรือกด Hotkey ที่ตั้งไว้ (ใช้งานได้แม้โฟกัสอยู่ที่ Discord/เกม)
   เสียงจะถูก**มิกซ์รวมกับเสียงไมค์แบบเรียลไทม์**แล้วส่งออกไปที่ output device เดียวกัน
7. ไปที่ Discord → Settings → Voice & Video → Input Device → เลือก **"CABLE Output"**
   → Discord จะได้ยินทั้งเสียงพูดของคุณและเสียง soundboard พร้อมกันในสตรีมเดียว

### ถ้าอยากปิดไมค์ชั่วคราว (เล่นเสียงคนเดียวไม่ต้องพูด)
แค่เอาเครื่องหมายถูกออกจาก **"🎙 ผสมเสียงไมค์จริงเข้าไปด้วย"** — เสียง soundboard ยังเล่นได้ปกติ
แค่ไม่มีเสียงไมค์ปนไปด้วย

### หมายเหตุเรื่อง delay/latency
เพราะโปรแกรมอัดเสียงไมค์ผ่าน WASAPI แล้วมิกซ์ใหม่ก่อนส่งออก อาจมี latency เล็กน้อย (ปกติ 20-50ms
ซึ่งแทบไม่รู้สึก) ถ้ารู้สึกดีเลย์เยอะเกินไป ลองปรับค่า `50` (ms, latency ของ WasapiOut) ใน
`AudioEngine.cs` ตรง `new WasapiOut(device, AudioClientShareMode.Shared, true, 50)` ให้สูงขึ้น
เพื่อความเสถียร หรือต่ำลงเพื่อลด delay (แลกกับเสี่ยงเสียงสะดุดถ้าเครื่องช้า)

## โครงสร้างโปรเจกต์

```
SoundboardApp/
├── SoundboardApp.csproj      โปรเจกต์ .NET 8 WPF
├── app.manifest               ตั้งค่าสิทธิ์การรัน (asInvoker)
├── App.xaml / App.xaml.cs     จุดเริ่มโปรแกรม + สี theme
├── MainWindow.xaml/.cs        หน้าต่างหลัก (UI + logic)
├── RenameDialog.xaml/.cs      ป็อปอัพแก้ไขชื่อปุ่มเสียง
├── Models/
│   ├── SoundButtonModel.cs    ข้อมูลปุ่มเสียง (runtime, INotifyPropertyChanged)
│   └── AppConfig.cs           รูปแบบข้อมูลที่บันทึกลง config.json
└── Services/
    ├── AudioEngine.cs         เอนจินเสียงหลัก: อัดไมค์จริง + เล่นไฟล์เสียง แล้วมิกซ์ส่งออก
    │                          ไปยัง device เดียว (ใช้ NAudio WASAPI + MixingSampleProvider)
    ├── GlobalKeyboardHook.cs  ดัก keyboard ระดับ OS สำหรับ Hotkey
    └── ConfigService.cs       บันทึก/โหลด config.json ที่ %AppData%\SoundboardApp
```

ตั้งค่าที่บันทึกไว้ (รายการเสียง + hotkey + อุปกรณ์ที่เลือก) จะอยู่ที่:
`%AppData%\SoundboardApp\config.json` — เปิดโปรแกรมครั้งต่อไปจะโหลดอัตโนมัติ

## ปัญหาที่พบบ่อย

- **Hotkey ไม่ทำงานตอนอยู่ในเกม/Discord ที่รันแบบ Admin**: เปิด `app.manifest` เปลี่ยน
  `level="asInvoker"` เป็น `level="requireAdministrator"` แล้ว build ใหม่ จากนั้นต้องรัน
  Soundboard แบบ Run as Administrator ด้วย
- **ไม่มีเสียงใน Discord**: เช็คว่า Output Device ในโปรแกรมเป็น "CABLE Input" และ Input Device
  ใน Discord เป็น "CABLE Output" ตรงกัน (สลับกันบ่อย)
- **เสียงหาย/แตกเป็นช่วง ๆ**: ลองปิดโปรแกรมอื่นที่ใช้เสียงพร้อมกันเยอะ ๆ หรือปรับค่า latency
  `50` (ms) ใน `AudioEngine.cs` ตรง `new WasapiOut(...)` ให้สูงขึ้นเพื่อความเสถียร
- **ได้ยินเสียงตัวเองซ้อน/ก้อง (echo)**: มักเกิดถ้าคุณเปิดทั้งการผสมไมค์ในแอพนี้ *และ*
  ยังเปิด "Listen to this device" ของ CABLE Output ทิ้งไว้ใน Windows Sound Settings จากที่เคยตั้งไว้
  ก่อนหน้า — ให้เข้าไปปิดอันนั้นทิ้ง เพราะตอนนี้แอพมิกซ์ให้เองแล้ว ไม่ต้องใช้ trick นั้นอีก
