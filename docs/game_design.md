# Desain gameplay

Slime Breaker adalah action roguelite 3D offline untuk Android, landscape, target 60 fps.

Satu run berlangsung di arena radius 22 m selama **100 lantai**. Level hero terpisah, maksimum **999**. Setiap lantai, cuaca dan siang/malam berganti. Setiap 10 lantai biome map berganti. Setiap 10 lantai muncul slime boss unik. Mengalahkan **Royal Slime** di lantai 100 menamatkan permainan.

## Loop

Menu → arena lantai 1 (hutan siang cerah) → gerak dan dash → penyihir otomatis menembakkan bolt sihir pelacak ke musuh terdekat yang terlihat (jangkauan 4,4 m; batu/pohon memblokir tembakan) → orb EXP → level-up menjeda permainan dan menawarkan 3 upgrade (hero max Lv 999) → setiap kelipatan 5 **level hero** satu kristal power-up muncul di map yang sama → clear kuota slime membuka **pintu**; hero masuk dulu baru naik lantai → setiap kelipatan 10 **lantai** slime biasa lenyap, peringatan **BOSS LANTAI N**, kamera zoom ke bos, lalu pertarungan (boss memanggil 10 slime kecil tiap 20 detik) → Royal Slime di lantai 100 = tamat → emas disimpan → upgrade permanen di menu. Keluar di tengah run menyimpan progres ke `slimebreaker-run.json`; tombol **Lanjutkan** di menu memulihkan lantai, level, skill, HP, dan EXP.

HUD menampilkan **Lantai**, **Lv**, **ATK** (damage output hero), timer, HP, dan **EXP**. Panah wayfinder menunjuk pintu dan power-up yang aktif. Angka damage mengambang di atas musuh (krem), critical (**CRIT** emas lebih besar), dan damage yang diterima hero (merah).

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
- Kuota slime di lantai itu membuka pintu keluar. Lantai naik setelah hero **masuk pintu**, bukan otomatis.

## Boss (setiap 10 lantai)

Saat lantai mencapai kelipatan 10, arena masuk mode boss: minion lenyap, spawner berhenti, banner **BOSS LANTAI N** muncul, kamera zoom ke bos selama ~2,5 detik, lalu pertarungan dimulai. Boss memanggil **10 slime kecil setiap 20 detik** (maks ~14 hidup) supaya hero tetap bisa naik level dan mengambil power-up tiap 5 level. Setelah boss kalah, pintu muncul; masuk pintu baru ke lantai berikutnya.

HP boss = lantai × 1000 (lantai 10 = 10.000, lantai 20 = 20.000, … lantai 100 = 100.000). Damage boss tetap: **basic 50**, **skill 100**, **skill enrage 200**. Ketika HP boss turun di bawah 30%, ia **enrage** dan memakai N skill elemental (N = lantai / 10). Skill memakai telegraf sesuai elemen: batu/es/racun/magma jatuh dari langit dengan **bayangan di tanah**, api menembak bolt api, petir menyambar.

## Map dan cuaca

Masuk pintu meregenerasi arena (fade). Cuaca dan siang/malam berganti setiap lantai. Biome berganti setiap 10 lantai:

| Lantai | Biome | Nuansa |
|---|---|---|
| 1–10 | Hutan Rune | rumput, pohon, jamur |
| 11–20 | Reruntuhan Gurun | pasir, kaktus |
| 21–30 | Lembah Salju | es, duri es |
| 31–40 | Padang Petir | spire kilat |
| 41–50 | Rawa Beracun | akar, reed, spora |
| 51–60 | Lembah Bayangan | obelisk gelap |
| 61–70 | Kaldera Magma | batu lava, hujan abu |
| 71–80 | Puncak Badai | gerbang batu, hujan |
| 81–90 | Gua Kristal | spire kristal |
| 91–100 | Taman Kerajaan | kolom marmer emas |

| Lantai | Nama | HP | Skill enrage | Keunikan |
|---|---|---|---|---|
| 10 | Rock Slime | 10.000 | 1 | Kulit batu, lempar batu ke atas lalu jatuh ke bayangan di tanah, slam lebih lebar |
| 20 | Flame Slime | 20.000 | 2 | Tembakan api (`HostileProjectile`) + genangan bakar |
| 30 | Frost Slime | 30.000 | 3 | Hujan es + aura dingin yang memperlambat hero |
| 40 | Volt Slime | 40.000 | 4 | Sambaran petir + jatuhan kilat |
| 50 | Toxic Slime | 50.000 | 5 | Hujan racun dan genangan |
| 60 | Shadow Slime | 60.000 | 6 | Blink, hujan bayangan, selubung gelap |
| 70 | Magma Slime | 70.000 | 7 | Hujan magma + jejak lava saat charge |
| 80 | Storm Slime | 80.000 | 8 | Dorongan angin + jatuhan petir |
| 90 | Crystal Slime | 90.000 | 9 | Cincin duri + perisai yang menahan bolt |
| 100 | Royal Slime | 100.000 | 10 | Campuran tembakan api, hujan, dan zap. Kalahkan untuk **Tamat!** |

## Upgrade run (level-up)

Serangan, laju, nyawa, **Attack Speed**, dash, jangkauan, **EXP**, pengurangan damage, **Lifesteal**, **Crit Rate** (+8% peluang, cap 70%), dan **Critical** (+30% pengali crit, dasar 1,5×, cap 3,5×). Damage output hero tampil di HUD sebagai **ATK**. Serangan yang crit menampilkan angka **CRIT** emas.

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
