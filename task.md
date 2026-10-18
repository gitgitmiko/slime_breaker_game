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

### Putaran 11 — skill Lv 10, power-up tiap 20 level, lifesteal hijau, DEF, joystick bebas, enrage cutscene

- Skill power-up naik sampai **Lv 10** (damage +30%/level, +1 hitungan tiap 2 level, cooldown ×0,92/level, radius +8%/level) dengan bonus MAX per skill; slot HUD menampilkan **MAX** emas.
- Power-up muncul tiap **20 level hero** (bukan 5); berhenti muncul jika semua skill MAX.
- Lifesteal tampil sebagai angka **+N hijau** di atas hero.
- Panel status kiri atas menambah **DEF** dan **TAKEN**.
- Nama upgrade level-up berbahasa Inggris: Power, Swiftness, Vitality, Reach, **Defender**, dst.
- Joystick **bebas (floating)** di setengah kiri layar.
- Boss enrage: cutscene zoom close-up + slow-motion + banner ENRAGE!; skill enrage dengan gelombang kejut api, petir merah, dan guncangan kamera.
- Setup: `SLIMEBREAKER_SETUP_OK`. EditMode 21/21. PlayMode 26/26. APK `SLIMEBREAKER_APK_OK` 0.11.0.
- Unduhan: https://testimony-entire-journalism-residents.trycloudflare.com/

### Putaran 12 — joystick Fixed/Free, deskripsi skill tekan-tahan, Boss Breaker

- Pengaturan punya toggle **Joystick: Fixed / Free**; default **Fixed**.
- Tekan tahan slot power-up di HUD menampilkan deskripsi skill (level, efek, statistik, bonus MAX, status terhadap boss).
- Di stage boss hanya **basic attack** yang melukai boss; skill power-up tidak (muncul **IMMUNE**).
- Gelombang minion boss punya peluang 35% berisi **slime emas**; membunuhnya menjatuhkan **Boss Breaker**: power-up bisa melukai boss selama **60 detik**, hero beraura emas-merah, HUD menampilkan hitung mundur.
- Setup: `SLIMEBREAKER_SETUP_OK`. EditMode 21/21. PlayMode 28/28. APK `SLIMEBREAKER_APK_OK` 0.12.0.
- Unduhan: https://testimony-entire-journalism-residents.trycloudflare.com/

### Putaran 13 — kartu status & deskripsi modern, bar HP boss

- Kartu status kiri atas: panel kaca, header STATUS, grid 2×5 (label redup, nilai tebal, warna per jenis).
- Kartu deskripsi power-up: aksen warna, ikon, pil level/MAX, chip statistik, bonus MAX, status boss.
- Stage boss: bar HP boss di atas layar di bawah bar lantai — pil BOSS/ENRAGE, nama, `10.000 / 10.000` turun mengikuti damage, jejak damage.
- Perbaikan joystick Fixed di resolusi non-1920×1080.
- Setup: `SLIMEBREAKER_SETUP_OK`. EditMode 22/22. PlayMode 28/28. APK `SLIMEBREAKER_APK_OK` 0.13.0.
- Unduhan: https://testimony-entire-journalism-residents.trycloudflare.com/

### Putaran 14 — HUD dan menu bergaya kaca (mengikuti bar HP boss)

- HP/EXP: pil HP/EXP + track gelap + isi berkilap + angka berbayang; Lv jadi pil ungu.
- Lantai: pil LANTAI + `1 / 100`, bar kuota bergaya sama; timer di pil kaca.
- Tombol jeda dan pengaturan: cakram kaca + ikon; dash: cakram kaca dengan cincin cooldown cyan; joystick kaca + knob putih.
- Menu pengaturan: bagian GRAFIS/KONTROL, pilihan aktif ungu solid; jeda/hasil/level-up memakai panel kaca.
- Menu utama: kartu kaca berisi judul, pil Emas/Rekor, tombol Main emas, Lanjutkan & peningkatan kaca, versi.
- Setup: `SLIMEBREAKER_SETUP_OK`. EditMode 22/22. PlayMode 29/29. APK `SLIMEBREAKER_APK_OK` 0.14.0.
- Unduhan: https://testimony-entire-journalism-residents.trycloudflare.com/

### Putaran 15 — aset hero penyihir ungu chibi

- Referensi gaya: *Cat-boy Ashuo* (l4oyu, itch.io) — model 3D chibi berbayar; tidak disalin, hero dibangun ulang orisinal.
- Hero baru: rambut ungu metaball + poni runcing, mata anime dilukis (decal bertekstur), mulut ω + taring, topi penyihir bengkok + pita, gaun berenda, stoking, bot, tongkat bulan sabit.
- Animasi: Idle, Run (lari chibi), Attack (pukulan tongkat, bolt lepas saat hantaman), Dash, Hit, Death (topi lepas), Cast (ambil power-up), Victory (boss kalah), Menu (loop menu utama).
- Menu utama memakai `CHR_Hero_Menu.controller`; hero berayun ±22° menghadap kamera.
- Setup: `SLIMEBREAKER_SETUP_OK`. EditMode 22/22. PlayMode 30/30. APK `SLIMEBREAKER_APK_OK` 0.15.0.
- Unduhan: https://testimony-entire-journalism-residents.trycloudflare.com/

### Putaran 16 — lingkungan, material, dan audio HD

- Tekstur PBR prosedural (albedo/normal/mask) untuk 10 tanah bioma, properti, atlas daun/rumput, penutup bioma.
- 21 properti dibangun ulang HD (pohon berkartu daun, batu displacement, reruntuhan, kaktus, kristal…).
- Shader URP sendiri: Ground (anti-tiling), Lit (angin, translusensi, penutup lumut/salju/pasir/abu), Jelly (slime gel).
- Post-processing ACES + grading per bioma/malam; cuaca dengan sprite hujan/salju, percikan hujan, kabut.
- Audio: 12 musik orkestra (per bioma + boss + menu, crossfade), 11 ambience alam, SFX dan jingle baru; tanpa clipping.
- Setup: `SLIMEBREAKER_SETUP_OK`. EditMode 22/22. PlayMode 31/31. APK `SLIMEBREAKER_APK_OK` 0.16.0.
- Unduhan: https://testimony-entire-journalism-residents.trycloudflare.com/

### Putaran 17 — performa 60 fps dan perbaikan kaktus

- Penyebab drop fps: ratusan kartu rumput = ratusan draw call + pass bayangan, property block mematikan SRP batching, mesh tidak readable sehingga static batching gagal diam-diam, MSAA 4× + shadow map 4096 + PCF high.
- Perbaikan: gabung rumput per sektor, material tint ber-cache, mesh lingkungan readable, preset High lebih ringan, bloom murah, resolusi adaptif FSR.
- Kaktus: ujung kubah tertutup, kuncup merah.
- Setup OK. EditMode 22/22. PlayMode 32/32. APK 0.16.1.

### Putaran 18 — hero cat-boy penyihir, HP boss jutaan, audio tenang, kaktus

- Hero mengikuti referensi cat-boy + gaya penyihir; HP boss 10 juta per 10 lantai; orb EXP lebih tenang; kilau kaktus hilang.
- Setup OK. EditMode 22/22. PlayMode 32/32. APK 0.17.0.

### Putaran 19 — proporsi hero disamakan dengan chibi cat-boy

- Rambut, kepala, badan, lengan, kaki diukur dari referensi dan disamakan. Setup OK. EditMode 22/22. PlayMode 32/32. APK 0.17.1.

### Putaran 20 — bola cahaya putih saat bergerak

- Partikel cuaca melayang di depan kamera; diturunkan ke dekat tanah dan ukurannya dibatasi. APK 0.17.2.

### Putaran 21 — glitch kaktus (NaN) dan cuaca acak

- UV runtuh di ujung kaktus → NaN → bloom; diperbaiki di mesh, shader, kamera, bloom. Hujan bisa siang atau malam. APK 0.17.3.

### Putaran 22 — rarity upgrade, HP boss 10.000

- Normal/Rare/Legend/Ultra (65/20/10/5%, +0,1/0,3/0,5/1%) di semua upgrade; HP boss 10.000 per 10 lantai. APK 0.18.0.

### Putaran 23 — item drop dan inventori

- 5 slot perlengkapan ber-rarity, drop monster/boss, layar inventori, save run. APK 0.19.0.

### Putaran 24 — suara

- Hit peluru musuh dibisukan; slider volume BGM dan suara game di Pengaturan. APK 0.19.1.

### Putaran 26 — dinding steamer dimsum (tahap 1 redesign)

- Lantai/properti tetap; dinding arena = steamer bambu anyaman; properti luar dipindah ke dalam. Boss squishy dumpling (lantai 100 pelangi) dan hero baru menyusul. APK 0.20.1.

### Putaran 27 — collider tak terlihat dan pohon acak

- Collider ring/scatter rebah dan bergeser (rotasi FBX) → diperbaiki dengan `PropShape` + collider anak tegak; pohon tersebar acak. APK 0.20.2.

### Putaran 28 — garis biru, level item

- Cincin rune lantai dihapus; staff Normal/Rare/Legend/Ultra 100/250/500/1000 × level (level = 10 lantai). APK 0.20.3.

### Putaran 29 — boss squishy dumpling (tahap 2 redesign)

- 10 boss jadi dumpling jelly berglitter; boss lantai 100 pelangi. Berikutnya: hero baru. APK 0.21.0.

### Putaran 30 — lampu boss malam hari

- Boss memiliki lampu di atas kepala saat malam. APK 0.21.1.

### Putaran 32 — mode kesulitan

- Easy/Normal/Hard di menu utama; Hard = angka asli. APK 0.22.0.
- Jual item (1/3/6/10 emas per rarity), Auto pilih per rarity + level, kesulitan menjadi dropdown (bawaan Normal). APK 0.23.0.
- Menu: Lanjutkan di atas Main, info kesulitan dan deskripsi dihapus; riwayat boss (lantai 10, 20, ...) yang sudah dilewati berdiri di belakang hero (`SaveData.bossesDefeated`, `MenuTrophies`). APK 0.24.0.
- Arena di menu utama = arena (biome, cuaca, siang/malam) lantai terjauh yang pernah dicapai (`SaveData.highestFloor`, `MenuArena` memakai `ArenaGenerator`); kartu menu dirapatkan (tinggi mengikuti ada tidaknya Lanjutkan); frame inventori diperpanjang dan isinya ditengahkan. APK 0.25.0.
- Tahap 3 redesign: hero penyihir chibi bertopi raksasa (rambut bob pirang-merah muda, capelet, jaket lengan lonceng, rok krem, bot cokelat) mengikuti gambar referensi; tulang dan animasi sama. APK 0.26.0.
- Separuh kanan layar: geser ke samping memutar kamera mengelilingi hero (`CameraDragZone`, `FollowCamera.Yaw`); gerak joystick relatif kamera jadi ikut berputar; tombol HUD tetap di atas zona. APK 0.27.0.
- Orbit kamera dihaluskan (hanya titik fokus yang tertinggal; sudut diredam) dan ditambah sudut atas/bawah (32°–78°); hero dicerahkan (warna lebih jenuh, emisi lembut pada material hero). APK 0.28.0.

## Berikutnya

- Uji pasang di perangkat Android ARM64 (Android 7.1+) dan cek frame rate di perangkat kelas menengah.
- Kalau hero masih kurang "anime", langkah lanjut: tekstur wajah hand-painted dan shader toon ramp (`asset_work/scripts/build_assets.py` → `build_hero`).
