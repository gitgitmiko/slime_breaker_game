# Task

## Selesai di mesin ini

- Folder proyek `github-repos/slime-breaker`.
- Audit CLI aset. Tidak ada generator mesh AI. Blender, Python (numpy, Pillow), FluidSynth, dan oggenc yang dipakai.
- Unity Personal aktif setelah `unity auth login` (sjatmiko30@gmail.com).

### Putaran 2 — hero anime, aset HD, skill, audio

- Hero diganti penyihir anime (topi, jubah, cape, twin tail, mata besar, tongkat kristal) dengan rig 24 tulang dan klip Idle/Run/Attack(cast)/Dash/Hit/Death yang dibuat per frame; preview lembar animasi di `asset_work/previews/`.
- Semua aset diregenerasi lebih padat: slime dengan wajah di permukaan, boss, prop arena bertekstur + normal map, tanah 80 m, rumput, bunga, batu batas, cincin rune, mesh VFX skill.
- Audio: sebelumnya tidak ada `AudioListener` sehingga game bisu. Sekarang kamera membawa listener, `AudioService` memakai pool suara, 20 SFX baru dan 2 musik.
- Serangan dasar menjadi bolt sihir pelacak dari `StaffTip`. Animator semua state satu kali kembali ke Idle (dulu Hit/Dash membeku).
- Power-up tiap 5 level: kristal spawn acak, 4 skill (Cincin Es slow, Petir Berantai, Nova Api bakar, Misil Arkana), slot skill di HUD, toast.
- HUD baru: bar HP/XP bersprite, joystick bulat, tombol dash dengan sapuan cooldown, panel level-up dengan ikon. Menu menampilkan hero berputar + musik menu.
- 60 fps: `Application.targetFrameRate = 60`, vSync mati, URP Mobile render scale 1, MSAA 2×, HDR mati, bayangan 28 m, optimized frame pacing, static batching prop.
- PlayMode test 10/10 lolos (termasuk power-up level 5/10, es melukai + memperlambat, bolt mengenai musuh, AudioListener + 60 fps).
- APK ARM64 `Builds/SlimeBreaker.apk`, `SLIMEBREAKER_APK_OK`; disalin ke `~/Downloads/SlimeBreaker.apk`.

### Putaran 3 — nova api, grafis, obstacle, arena acak, lanjutkan

- Nova Api: cincin api annulus yang membesar dari hero (`FireNova`), damage saat tepi cincin menjangkau musuh.
- Grafis default High (bayangan 4096, soft, 2 cascade) plus panel Pengaturan Low/Medium/High di menu dan jeda.
- Obstacle padat (batu, pohon, pilar) di layer `Obstacle`; bolt, petir, nova, dan gerak musuh terhalang.
- Tiap level 5/10/15… slime lenyap, power-up + portal keluar; arena berikutnya di-roll `ArenaTheme` (bioma × cuaca × waktu).
- Fitur Lanjutkan: `RunSaveService` menulis `slimebreaker-run.json` saat jeda/keluar/naik level/ganti arena.

## Berikutnya

- Uji pasang di perangkat Android ARM64 (Android 7.1+) dan cek frame rate di perangkat kelas menengah.
- Kalau hero masih kurang "anime", langkah lanjut: tekstur wajah hand-painted dan shader toon ramp (`asset_work/scripts/build_assets.py` → `build_hero`).
