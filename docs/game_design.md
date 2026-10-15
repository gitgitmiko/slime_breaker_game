# Desain gameplay

Slime Breaker adalah action roguelite 3D offline untuk Android, landscape, target 60 fps.

Satu run berlangsung di **satu arena besar** (radius 22 m) selama **100 lantai**. Level hero terpisah, maksimum **999**. Setiap 10 lantai muncul slime boss unik. Mengalahkan **Royal Slime** di lantai 100 menamatkan permainan.

## Loop

Menu → satu arena besar → gerak dan dash → penyihir otomatis menembakkan bolt sihir pelacak ke musuh terdekat yang terlihat (jangkauan 4,4 m; batu/pohon memblokir tembakan) → orb XP → level-up menjeda permainan dan menawarkan 3 upgrade (hero max Lv 999) → setiap kelipatan 5 **level hero** satu kristal power-up muncul di map yang sama → clear kuota slime untuk naik lantai → setiap kelipatan 10 **lantai** slime biasa lenyap, peringatan **BOSS LANTAI N**, kamera zoom ke bos, lalu pertarungan → Royal Slime di lantai 100 = tamat → emas disimpan → upgrade permanen di menu. Keluar di tengah run menyimpan progres ke `slimebreaker-run.json`; tombol **Lanjutkan** di menu memulihkan lantai, level, skill, HP, dan XP.

HUD menampilkan **Lantai**, **Lv**, **ATK** (damage output hero), timer, HP, dan XP.

## Kontrol

- Kiri bawah: joystick. Di editor, WASD atau tombol panah juga berjalan.
- Kanan bawah: dash. Di editor, spasi.
- Dash punya cooldown dan jendela kebal singkat. Terkena damage juga memberi kebal 0,35 s.
- Jeda: tombol II atau Escape. Dari jeda atau menu, ⚙ / Pengaturan memilih grafis Low / Medium / High (default High: bayangan 4096, soft, 2 cascade).

## Musuh

- Biasa: pelan, nyawa sedang.
- Cepat: kecil, lebih laju, nyawa rendah.
- Tank: besar, lambat, nyawa tinggi, menjatuhkan 2 orb.
- Jarak: menjaga jarak dan menembakkan bolt (`HostileProjectile`) yang terhalang obstacle.
- Semua slime menampilkan bar darah world-space di atas kepala.
- Lantai naik setelah kuota slime di lantai itu habis (bukan setelah level hero naik).

## Boss (setiap 10 lantai)

Saat lantai mencapai kelipatan 10, arena masuk mode boss: minion lenyap, spawner berhenti, banner **BOSS LANTAI N** muncul, kamera zoom ke bos selama ~2,5 detik, lalu pertarungan dimulai.

HP boss = lantai × 1000 (lantai 10 = 10.000, lantai 20 = 20.000, … lantai 100 = 100.000). Ketika HP boss turun di bawah 30%, ia **enrage** dan memakai N skill damage besar (N = lantai / 10).

| Lantai | Nama | HP | Skill enrage | Keunikan |
|---|---|---|---|---|
| 10 | Rock Slime | 10.000 | 1 | Kulit batu (damage masuk berkurang), lemparan batu, slam lebih lebar |
| 20 | Flame Slime | 20.000 | 2 | Ledakan api dan genangan bakar |
| 30 | Frost Slime | 30.000 | 3 | Aura dingin + chill yang memperlambat hero |
| 40 | Volt Slime | 40.000 | 4 | Blink lalu petir |
| 50 | Toxic Slime | 50.000 | 5 | Ludah racun dan genangan |
| 60 | Shadow Slime | 60.000 | 6 | Blink dan selubung gelap |
| 70 | Magma Slime | 70.000 | 7 | Erupsi tertunda + jejak lava saat charge |
| 80 | Storm Slime | 80.000 | 8 | Dorongan angin + sambaran |
| 90 | Crystal Slime | 90.000 | 9 | Cincin duri + perisai yang menahan bolt |
| 100 | Royal Slime | 100.000 | 10 | Campuran zap/lemparan + memanggil minion. Kalahkan untuk **Tamat!** |

## Upgrade run (level-up)

Serangan, laju, nyawa, **Attack Speed**, dash, jangkauan, XP, pengurangan damage, dan **Lifesteal** (pulih sebagian dari damage yang diberikan, bukan tick bakar). Damage output hero tampil di HUD sebagai **ATK**.

## Skill power-up (tiap 5 level hero)

`StageManager` menjatuhkan kristal di arena yang sama (tanpa ganti peta). Menyentuh kristal memberi skill baru; jika semua sudah dimiliki, level skill naik (maks 4). Skill dijalankan `SkillController`. Nova Api adalah cincin api yang membesar dari hero (`FireNova`).

| Skill | Efek | Naik level |
|---|---|---|
| Cincin Es | 3 pecahan es mengorbit hero (r 1,5 m). Musuh yang tersentuh terkena 7 damage per 0,5 s dan melambat 55% selama 1,6 s. | +1 pecahan, radius +12%, damage +35% |
| Petir Berantai | Tiap 2,6 s menyambar musuh terdekat (≤ 8 m) lalu melompat ke ≤ 3 musuh lain dalam 3,8 m. | +1 lompatan, cooldown -15% |
| Nova Api | Tiap 4,2 s cincin api meledak keluar dari hero (radius 2,6 m). Musuh terkena saat tepi cincin menjangkau mereka, lalu terbakar 3 tick. Obstacle memblokir LOS. | radius +12%, damage +35% |
| Misil Arkana | Tiap 1,9 s menembakkan 2 misil pelacak ke musuh acak dalam 9,5 m. | +1 misil, cooldown -15% |

## Progres permanen

Emas membeli peringkat damage, nyawa, dan laju. Pilihan grafis ikut tersimpan. Meta-save: `Application.persistentDataPath/slimebreaker-save.json`. Progres run: `slimebreaker-run.json` (dihapus saat menang/kalah/Main baru).

## Audio

`AudioService` memutar musik loop dan efek lewat pool 10 AudioSource dengan variasi pitch dan interval minimum per id supaya kerumunan tidak menumpuk jadi derau. Kamera utama membawa `AudioListener` (dipasang `ProjectSetup` dan dijaga `UrpSceneBootstrap`).
