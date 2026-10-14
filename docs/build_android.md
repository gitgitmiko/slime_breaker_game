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

## Percobaan yang sudah dijalankan

Perintah di atas dijalankan pada 14 Oktober 2026 dan berhenti dengan kode 198. Log:

```text
No valid Unity Editor license found. Please activate your license.
```

`unity license activate --personal --accept-eula` menolak dengan `NOT_SIGNED_IN`. `unity auth status` melaporkan `loggedIn: false`. Tidak ada file `.ulf` di mesin ini.

APK belum dibuat. Jangan menganggap build Android berhasil sebelum log editor memuat setup OK dan file APK benar-benar ada.

## Mengaktifkan lisensi

Masuk ke akun Unity, lalu aktifkan lisensi Personal:

```bash
unity auth login
unity license activate --personal --accept-eula
```

Akun layanan bisa memakai `unity auth login --client-id ... --secret-from-stdin`. Setelah itu ulangi perintah setup di atas.

## Perintah build yang belum dijalankan

`unity build --help` sudah dicek. Flag yang cocok adalah `--target`, `--output-path`, dan `--android-export-type`. Perintah ini belum dieksekusi:

```bash
unity build --target Android --android-export-type apk \
  --output-path Builds/SlimeBreaker.apk \
  --log-file Logs/android-build.log
```

Target pemain: ARM64, IL2CPP, orientasi Landscape Left, min SDK 25, id `com.gitgitmiko.slimebreaker`. Tidak ada perangkat Android yang terhubung untuk uji pasang, jadi uji di perangkat masih tertunda.
