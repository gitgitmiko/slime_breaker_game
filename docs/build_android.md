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

Exit 0 berarti semua test di `Assets/Tests/PlayMode/ArenaPlaytest.cs` lolos: hero dan slime menapak tanah, properti tegak, semua material valid, tombol Main membuka arena, joystick sentuh menggerakkan hero, bot bertahan satu menit, AudioListener + 60 fps terpasang, power-up tiap 5 level hero di map yang sama (bukan boss), Cincin Es melukai dan memperlambat, bolt mengenai musuh saat diam (tidak saat jalan), Nova Api merusak musuh dekat, obstacle memblokir bolt, run-save level/lantai utuh, preset grafis Low/High terpasang, bar darah musuh, lifesteal, slime jarak menembak, Rock Slime + warning di lantai 10 (HP 10.000, damage basic naik tiap lantai, enrage <30%, 10 minion, skyfall), kuota lantai N×10 membuka pintu lalu Enter naik lantai (malam + layout baru di lantai 2, tanpa pintu langsung), HUD Lantai/progres bunuh/ATK/EXP + wayfinder + upgrade crit, collider obstacle, angka damage/CRIT, dan biome gurun/salju/petir di lantai 11/21/31. Baris `PLAYTEST` di log memuat angka ukurnya. Screenshot ada di `Logs/playtest/` (00_menu … 16_damage_numbers).

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

## Hasil 15 Oktober 2026 (putaran 4 — bar darah, boss, satu map, ranged, lifesteal)

- Setup: `SLIMEBREAKER_SETUP_OK`; PlayMode 18/18 lolos; APK: `SLIMEBREAKER_APK_OK`.
- APK: `Builds/SlimeBreaker.apk` (38 MB), disalin ke `~/Downloads/SlimeBreaker.apk`.
- Paket: `com.gitgitmiko.slimebreaker` 0.4.0 (versionCode 4).
- Unduhan: `https://testimony-entire-journalism-residents.trycloudflare.com/`

## Hasil 15 Oktober 2026 (putaran 5 — lantai, hero 999, boss enrage)

- Setup: `SLIMEBREAKER_SETUP_OK`; EditMode 9/9; PlayMode 20/20 lolos; APK: `SLIMEBREAKER_APK_OK`.
- APK: `Builds/SlimeBreaker.apk` (38 MB), disalin ke `~/Downloads/SlimeBreaker.apk`.
- Paket: `com.gitgitmiko.slimebreaker` 0.5.0 (versionCode 5).
- Unduhan: `https://testimony-entire-journalism-residents.trycloudflare.com/`

## Hasil 15 Oktober 2026 (putaran 6 — damage, crit, pintu, skill elemental)

- Setup: `SLIMEBREAKER_SETUP_OK`; EditMode 12/12; PlayMode 22/22 lolos; APK: `SLIMEBREAKER_APK_OK`.
- APK: `Builds/SlimeBreaker.apk` (39 MB), disalin ke `~/Downloads/SlimeBreaker.apk`.
- Paket: `com.gitgitmiko.slimebreaker` 0.6.0 (versionCode 6).
- Unduhan: `https://testimony-entire-journalism-residents.trycloudflare.com/`

## Hasil 15 Oktober 2026 (putaran 7 — biome, cuaca, damage boss)

- Setup: `SLIMEBREAKER_SETUP_OK`; EditMode 15/15; PlayMode 23/23 lolos; APK: `SLIMEBREAKER_APK_OK`.
- APK: `Builds/SlimeBreaker.apk` (42 MB), disalin ke `~/Downloads/SlimeBreaker.apk`.
- Paket: `com.gitgitmiko.slimebreaker` 0.7.0 (versionCode 7).
- PlayMode menambah tes biome gurun/salju/petir tiap 10 lantai, cuaca/malam di lantai 2, dan damage basic boss 50.

## Hasil 15 Oktober 2026 (putaran 8 — bayangan, serangan sambil jalan, HUD status, palet boss)

- Setup: `SLIMEBREAKER_SETUP_OK`; EditMode 17/17; PlayMode 24/24 lolos; APK: `SLIMEBREAKER_APK_OK`.
- APK: `Builds/SlimeBreaker.apk` (42 MB), disalin ke `~/Downloads/SlimeBreaker.apk`.
- Paket: `com.gitgitmiko.slimebreaker` 0.8.0 (versionCode 8).
- PlayMode menambah tes bolt mengenai musuh saat hero berjalan, HUD status (DMG/CRIT/CRIT RATE/ATK SPD/MOVE/LS), bayangan skyfall umbra/penumbra, dan jubah merah + mahkota emas pada Rock Slime.
- Unduhan: `https://testimony-entire-journalism-residents.trycloudflare.com/`

## Hasil 15 Oktober 2026 (putaran 9 — diam untuk serang, kuota ×10, scaling)

- Setup: `SLIMEBREAKER_SETUP_OK`; EditMode 19/19; PlayMode 24/24 lolos; APK: `SLIMEBREAKER_APK_OK`.
- APK: `Builds/SlimeBreaker.apk` (42 MB), disalin ke `~/Downloads/SlimeBreaker.apk`.
- Paket: `com.gitgitmiko.slimebreaker` 0.9.0 (versionCode 9).
- PlayMode menambah tes bolt tidak tembak saat jalan, kuota 10/20 tanpa pintu langsung, dan HUD progres `0 / 10`.
- Unduhan: `https://testimony-entire-journalism-residents.trycloudflare.com/`

## Hasil 15 Oktober 2026 (putaran 10 — RANGE/EXP, dash, HP slime)

- Setup: `SLIMEBREAKER_SETUP_OK`; EditMode 20/20; PlayMode 25/25 lolos; APK: `SLIMEBREAKER_APK_OK`.
- APK: `Builds/SlimeBreaker.apk` (42 MB), disalin ke `~/Downloads/SlimeBreaker.apk`.
- Paket: `com.gitgitmiko.slimebreaker` 0.10.0 (versionCode 10).
- PlayMode menambah tes dash tidak mengantri saat cooldown, HUD RANGE + EXP bonus, dan HP slime naik tiap lantai.
- Unduhan: `https://testimony-entire-journalism-residents.trycloudflare.com/`

## Pasang di HP

1. Salin `SlimeBreaker.apk` ke ponsel (USB, Drive, atau kirim ke diri sendiri).
2. Di Android, izinkan pasang dari sumber tidak dikenal untuk aplikasi yang dipakai transfer.
3. Buka file APK, pasang, lalu jalankan **Slime Breaker**.
4. Putar HP ke landscape. Butuh Android 7.1 atau lebih baru, CPU ARM64.
