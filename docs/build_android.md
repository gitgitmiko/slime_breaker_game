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

## Hasil 14 Oktober 2026

- Setup: `SLIMEBREAKER_SETUP_OK`, exit 0.
- APK: `Builds/SlimeBreaker.apk` (31 MB), juga disalin ke `~/Downloads/SlimeBreaker.apk`.
- Paket: `com.gitgitmiko.slimebreaker` 0.1.0, min SDK 25, target 36, native `arm64-v8a`, landscape.
- Ditandatangani debug keystore Unity (bukan Play Store).
- Tidak ada perangkat ADB terhubung, jadi pasang di HP masih manual.

## Pasang di HP

1. Salin `SlimeBreaker.apk` ke ponsel (USB, Drive, atau kirim ke diri sendiri).
2. Di Android, izinkan pasang dari sumber tidak dikenal untuk aplikasi yang dipakai transfer.
3. Buka file APK, pasang, lalu jalankan **Slime Breaker**.
4. Putar HP ke landscape. Butuh Android 7.1 atau lebih baru, CPU ARM64.
