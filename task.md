# Task

## Selesai di mesin ini

- Folder proyek `github-repos/slime-breaker`.
- Audit CLI aset. Tidak ada generator mesh AI. Blender, ImageMagick, FluidSynth, dan oggenc yang dipakai.
- Aset P0: hero, 3 slime, boss, arena, ikon, panel, SFX, musik.
- Skrip gameplay MVP dan `ProjectSetup` untuk merakit scene.

## Terblokir

- Impor Unity dan build APK. Editor 6000.3.26f1 menolak batch mode karena tidak ada lisensi (`No valid Unity Editor license found`, exit 198).
- Uji di perangkat Android belum dilakukan.

## Berikutnya, setelah `unity auth login`

1. Jalankan `ProjectSetup.BuildAll` seperti di `docs/build_android.md`.
2. Baca `Logs/setup.log`. Perbaiki compile error jika ada.
3. Cek skala FBX dari baris `bounds` di log. `FitHeight` menyesuaikan jika skala 100x atau 0,01x.
4. Build APK ARM64 dan catat hasil pasang di satu perangkat.
