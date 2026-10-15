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

### Putaran 4 — bar darah, boss 10/20/…/100, satu map, ranged, lifesteal

- Bar darah world-space di atas setiap slime dan boss.
- Run tamat di level 100. Satu arena besar (radius 22 m); portal ganti-peta dihapus. Power-up tetap tiap 5 level di map yang sama.
- Boss unik tiap 10 level (Rock … Royal Slime), banner STAGE BOSS, zoom kamera 2,5 d, skill sesuai keunikan.
- Upgrade baru Lifesteal; Ritme diganti **Attack Speed**.
- Slime jarak menembak `HostileProjectile`.

### Putaran 5 — lantai 1–100, hero 999, boss enrage

- Progres run jadi **lantai 1–100** (kuota slime per lantai). Level hero terpisah, cap **999**.
- Power-up tetap tiap 5 **level hero**. Boss tiap 10 **lantai**.
- Boss lantai 10 HP 10.000 + 1 skill enrage di bawah 30% HP; lantai 20 HP 20.000 + 2 skill; … lantai 100 HP 100.000 + 10 skill.
- HUD menampilkan Lantai, Lv, dan ATK.
- Setup: `SLIMEBREAKER_SETUP_OK`. EditMode 9/9. PlayMode 20/20. APK `SLIMEBREAKER_APK_OK` 0.5.0.

### Putaran 6 — damage number, crit, pintu, wayfinder, skill elemental

- Angka damage mengambang: musuh (krem), hero (merah), critical (**CRIT** emas).
- Upgrade level-up baru **Crit Rate** dan **Critical**; judul pengalaman diganti **EXP**.
- Naik lantai lewat **pintu** (masuk dulu); panah wayfinder menunjuk pintu dan power-up.
- Boss memanggil 10 slime kecil tiap 20 detik; skill sesuai elemen (batu jatuh + bayangan, api menembak, dst.).
- Prop pohon/batu/pilar/boulder yang sebelumnya tembus kini punya collider di layer Obstacle.
- Setup: `SLIMEBREAKER_SETUP_OK`. EditMode 12/12. PlayMode 22/22. APK `SLIMEBREAKER_APK_OK` 0.6.0.

### Putaran 7 — biome per 10 lantai, cuaca tiap lantai, damage boss

- Tiap naik lantai arena di-roll ulang: cuaca + siang/malam berganti, seed layout baru.
- Tiap 10 lantai biome baru: hutan (1–10), gurun (11–20), salju (21–30), petir, rawa, bayangan, magma, badai, kristal, kerajaan.
- Aset baru: 7 tekstur tanah + 11 mesh biome (kaktus, duri es, spire, akar rawa, obelisk, lava, gerbang, kolom).
- Damage boss: basic 50, skill 100, skill enrage 200.
- Setup: `SLIMEBREAKER_SETUP_OK`. EditMode 15/15. PlayMode 23/23. APK `SLIMEBREAKER_APK_OK` 0.7.0.

### Putaran 8 — bayangan batu, serangan sambil jalan, HUD status, palet boss, crit terkunci

- Bayangan skill jatuhan batu: umbra + penumbra lunak yang mengikuti ketinggian batu (kecil/pudar di udara, besar/gelap dekat tanah) plus cincin dampak, jatuhan berakselerasi.
- Auto-attack tidak memutar hero atau memutus animasi lari; bolt tetap homing saat hero berjalan. Proyektil yang spawn di dalam obstacle tidak langsung hilang.
- HUD status di kiri atas di bawah HP/EXP: DMG, CRIT, CRIT RATE, ATK SPD, MOVE, LS.
- Pewarnaan boss per bahan: tubuh elemen, jubah merah, mahkota emas, permata elemen.
- Critical hanya keluar setelah upgrade Crit Rate atau Critical (awal crit chance 0).
- Setup: `SLIMEBREAKER_SETUP_OK`. EditMode 17/17. PlayMode 24/24. APK `SLIMEBREAKER_APK_OK` 0.8.0.
- Unduhan: https://testimony-entire-journalism-residents.trycloudflare.com/

### Putaran 9 — serangan diam, kuota ×10, progres lantai, scaling damage

- Basic attack hanya saat hero diam; jalan/dash menahan bolt.
- Bug pintu beruntun (lantai 8→9 langsung 10) diperbaiki: slime sisa tidak dihitung ke lantai berikutnya.
- Kuota bunuh: lantai N = N×10 slime. HUD atas menampilkan progres `3 / 10`.
- Setiap naik level hero, ATK dasar +10. Setiap lantai, damage slime +1 dan damage boss +3.
- Setup: `SLIMEBREAKER_SETUP_OK`. EditMode 19/19. PlayMode 24/24. APK `SLIMEBREAKER_APK_OK` 0.9.0.
- Unduhan: https://testimony-entire-journalism-residents.trycloudflare.com/

### Putaran 10 — RANGE/EXP HUD, dash tanpa antrian, HP slime per lantai

- Panel status kiri atas menambah **RANGE** dan **EXP +%**.
- Dash tidak bisa diantrikan: tekan saat cooldown diabaikan, tombol nonaktif sampai cooldown habis.
- HP slime +10% dari dasar setiap naik lantai.
- Setup: `SLIMEBREAKER_SETUP_OK`. EditMode 20/20. PlayMode 25/25. APK `SLIMEBREAKER_APK_OK` 0.10.0.
- Unduhan: https://testimony-entire-journalism-residents.trycloudflare.com/

## Berikutnya

- Uji pasang di perangkat Android ARM64 (Android 7.1+) dan cek frame rate di perangkat kelas menengah.
- Kalau hero masih kurang "anime", langkah lanjut: tekstur wajah hand-painted dan shader toon ramp (`asset_work/scripts/build_assets.py` → `build_hero`).
