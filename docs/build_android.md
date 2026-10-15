# Build Android

Editor yang dipakai: Unity 6000.3.26f1 di `~/Unity/Hub/Editor/6000.3.26f1`.

Modul yang sudah terpasang lewat Unity CLI:

- Android Build Support
- Android SDK & NDK Tools (NDK, CMake, build-tools, platform-tools, platform 34–37, command-line tools)
- OpenJDK, di `Editor/Data/PlaybackEngines/AndroidPlayer/OpenJDK/bin/java`

Template proyek: URP blank lokal `com.unity.template.3d-cross-platform-17.0.14` dengan `com.unity.render-pipelines.universal` 17.0.1. Pipeline tidak diganti.

## Setup scene dan prefab

Setelah lisensi editor aktif:

```bash
cd /home/gitgitmiko/github-repos/slime-breaker
unity-editor -batchmode -nographics -quit \
  -projectPath "$PWD" \
  -executeMethod SlimeBreaker.EditorTools.ProjectSetup.BuildAll \
  -logFile Logs/setup.log
```

Berhasil jika log memuat `SLIMEBREAKER_SETUP_OK`.

## Build APK

CLI `unity build --target Android` pada Unity 6.3 di mesin ini menolak tanpa `--execute-method` atau build profile. Gunakan method di `ProjectSetup`:

```bash
cd /home/gitgitmiko/github-repos/slime-breaker
unity-editor -batchmode -nographics -quit \
  -projectPath "$PWD" \
  -executeMethod SlimeBreaker.EditorTools.ProjectSetup.BuildAndroidApk \
  -logFile Logs/android-build.log
```

Berhasil jika log memuat `SLIMEBREAKER_APK_OK` dan file `Builds/SlimeBreaker.apk` ada.

## Playtest sebelum build

Jalankan setelah `BuildAll`, sebelum `BuildAndroidApk`. Tanpa `-nographics` dan di bawah Xvfb supaya kamera bisa merender screenshot:

```bash
cd /home/gitgitmiko/github-repos/slime-breaker
xvfb-run -a -s "-screen 0 1280x1024x24" unity-editor -batchmode \
  -projectPath "$PWD" -runTests -testPlatform PlayMode \
  -testResults "$PWD/Logs/playmode.xml" -logFile Logs/playmode.log
```

Exit 0 berarti semua 14 test di `Assets/Tests/PlayMode/ArenaPlaytest.cs` lolos: hero dan slime menapak tanah, properti tegak, semua material valid, tombol Main membuka arena, joystick sentuh menggerakkan hero, bot bertahan satu menit, AudioListener + 60 fps terpasang, power-up + portal di level 5 lalu arena berganti, Cincin Es melukai dan memperlambat, bolt mengenai musuh, Nova Api merusak musuh dekat, obstacle memblokir bolt, run-save level/stage utuh, preset grafis Low/High terpasang. Baris `PLAYTEST` di log memuat angka ukurnya. Screenshot ada di `Logs/playtest/` (00_menu … 10_obstacle).

## Hasil 14 Oktober 2026 (putaran 1)

- Setup: `SLIMEBREAKER_SETUP_OK`, exit 0.
- APK: `Builds/SlimeBreaker.apk` (31 MB), paket `com.gitgitmiko.slimebreaker` 0.1.0.

## Hasil 14 Oktober 2026 (putaran 2 — hero anime, skill, audio)

- Setup: `SLIMEBREAKER_SETUP_OK`; PlayMode 10/10 lolos; APK: `SLIMEBREAKER_APK_OK`.
- APK: `Builds/SlimeBreaker.apk` (37 MB), disalin ke `~/Downloads/SlimeBreaker.apk`.
- Paket: `com.gitgitmiko.slimebreaker` 0.2.0 (versionCode 2), min SDK 25, native `arm64-v8a`, landscape, Vulkan dengan fallback GLES3, optimized frame pacing.
- Ditandatangani debug keystore Unity (bukan Play Store). Pasang di atas 0.1.0 berjalan karena keystore sama.
- Tidak ada perangkat ADB terhubung, jadi pasang di HP masih manual.

## Hasil 15 Oktober 2026 (putaran 3 — nova, grafis, obstacle, arena, lanjutkan)

- Setup: `SLIMEBREAKER_SETUP_OK`; PlayMode 14/14 lolos; APK: `SLIMEBREAKER_APK_OK`.
- APK: `Builds/SlimeBreaker.apk` (38 MB), disalin ke `~/Downloads/SlimeBreaker.apk`.
- Paket: `com.gitgitmiko.slimebreaker` 0.3.0 (versionCode 3).
- Unduhan: `https://testimony-entire-journalism-residents.trycloudflare.com/`

## Pasang di HP

1. Salin `SlimeBreaker.apk` ke ponsel (USB, Drive, atau kirim ke diri sendiri).
2. Di Android, izinkan pasang dari sumber tidak dikenal untuk aplikasi yang dipakai transfer.
3. Buka file APK, pasang, lalu jalankan **Slime Breaker**.
4. Putar HP ke landscape. Butuh Android 7.1 atau lebih baru, CPU ARM64.
