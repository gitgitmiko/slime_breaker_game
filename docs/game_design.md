# Desain gameplay

Slime Breaker adalah action roguelite 3D offline untuk Android, landscape, target 60 fps.

Satu run berlangsung di arena radius 22 m selama **100 lantai**. Level hero terpisah, maksimum **999**. Setiap lantai, cuaca dan siang/malam berganti. Setiap 10 lantai biome map berganti. Setiap 10 lantai muncul slime boss unik. Mengalahkan **Royal Slime** di lantai 100 menamatkan permainan.

## Loop

Menu → arena lantai 1 (hutan siang cerah) → gerak dan dash → penyihir otomatis menembakkan bolt sihir pelacak **hanya saat diam** ke musuh terdekat yang terlihat (jangkauan 4,4 m; batu/pohon memblokir tembakan) → orb EXP → setiap naik **level hero**, serangan dasar **+10**, lalu permainan jeda dan menawarkan 3 upgrade (hero max Lv 999) → setiap kelipatan 5 **level hero** satu kristal power-up muncul di map yang sama → lantai N butuh **N×10** slime (lantai 1 = 10, lantai 2 = 20, …) sebelum **pintu** muncul; hero masuk dulu baru naik lantai → setiap kelipatan 10 **lantai** slime biasa lenyap, peringatan **BOSS LANTAI N**, kamera zoom ke bos, lalu pertarungan (boss memanggil 10 slime kecil tiap 20 detik) → Royal Slime di lantai 100 = tamat → emas disimpan → upgrade permanen di menu. Keluar di tengah run menyimpan progres ke `slimebreaker-run.json`; tombol **Lanjutkan** di menu memulihkan lantai, level, skill, HP, dan EXP.

Hero adalah penyihir chibi bertopi raksasa dengan rambut bob pirang-merah muda (lihat `docs/art_direction.md`). Serangan dasar dianimasikan sebagai pukulan tongkat; bolt sihir lepas tepat saat tongkat menghantam. Mengambil power-up atau Boss Breaker memutar animasi Cast; mengalahkan boss memutar Victory. Menu utama menampilkan hero dengan animasi Menu (melambai, memutar tongkat) yang berayun pelan menghadap kamera.

Seluruh HUD dan menu memakai gaya yang sama dengan bar HP boss: kaca gelap, garis tepi tipis, pil berwarna untuk label (HP merah, EXP hijau, LANTAI biru, Lv ungu), track gelap dengan isi berkilap, dan teks tebal berbayang. Tombol utama berwarna solid (emas = aksi utama, ungu = pilihan aktif, merah = keluar/tutup), tombol lain kaca transparan; tombol bulat (jeda, pengaturan, dash) berupa cakram kaca dengan ikon. Dash menampilkan cincin cyan yang berputar saat cooldown terisi. HUD menampilkan **Lantai**, progres bunuh slime lantai itu (misalnya `3 / 10`), **Lv**, timer, HP, **EXP**, dan **kartu status** modern di kiri atas di bawah bar HP (panel kaca gelap, header STATUS, grid 2×5 sel label redup + nilai tebal; LS hijau, DEF/TAKEN biru, CRIT emas saat aktif, nilai 0 diredupkan): **DMG**, **CRIT**, **CRIT RATE**, **ATK SPD**, **RANGE**, **MOVE**, **EXP** (bonus), **LS**, dan **DEF** (pengurangan damage dari Defender) + **TAKEN** (persen damage yang masih masuk). Panah wayfinder menunjuk pintu dan power-up yang aktif. Angka damage mengambang di atas musuh (krem), critical (**CRIT** emas lebih besar), damage yang diterima hero (merah), dan heal lifesteal (**+N hijau** di atas kepala hero, digabung tiap 0,25 s). Bolt sihir dasar hanya tembak saat hero diam; jalan atau dash menahan serangan dasar. Skill power-up tetap jalan sambil bergerak.

## Kontrol

- Kiri bawah: joystick. Default **Fixed**: hanya sentuhan di dasar joystick yang menggerakkan, dasar diam di tempat. Pengaturan → **Joystick: Fixed / Free** (tersimpan di meta-save). Mode **Free**: sentuh di mana saja pada setengah kiri layar; dasar pindah ke titik sentuh, ikut terseret jika jempol melewati tepi, kembali ke posisi asal saat dilepas. Di editor, WASD atau tombol panah juga berjalan.
- Kanan bawah: dash. Di editor, spasi.
- Dash punya cooldown dan jendela kebal singkat. Tombol dash dan spasi diabaikan selama cooldown — tekan beruntun tidak mengantri dash berikutnya. Terkena damage juga memberi kebal 0,35 s.
- Jeda: tombol II atau Escape. Dari jeda atau menu, ⚙ / Pengaturan memilih grafis Low / Medium / High (default High: bayangan 4096, soft, 2 cascade).

## Musuh

- Biasa: pelan, nyawa sedang.
- Cepat: kecil, lebih laju, nyawa rendah.
- Tank: besar, lambat, nyawa tinggi, menjatuhkan 2 orb.
- Jarak: menjaga jarak dan menembakkan bolt (`HostileProjectile`) yang terhalang obstacle.
- Semua slime menampilkan bar darah world-space di atas kepala.
- Kuota slime di lantai N adalah **N×10** (lantai 1 = 10, lantai 2 = 20, dst.). HUD di atas menampilkan progres itu. Lantai naik setelah hero **masuk pintu**, bukan otomatis. Slime sisa dihapus saat masuk pintu supaya kuota lantai berikutnya mulai dari nol.
- Setiap naik lantai, damage kontak slime **+1** dan HP slime **+10%** dari nyawa dasar. Boss menambah damage basic/skill/enrage **+3** per lantai.

## Boss (setiap 10 lantai)

Saat lantai mencapai kelipatan 10, arena masuk mode boss: minion lenyap, spawner berhenti, banner **BOSS LANTAI N** muncul, kamera zoom ke bos selama ~2,5 detik, lalu pertarungan dimulai. Boss memanggil **10 slime kecil setiap 20 detik** (maks ~14 hidup) supaya hero tetap bisa naik level dan mengambil power-up tiap 20 level. Setelah boss kalah, pintu muncul; masuk pintu baru ke lantai berikutnya.

HP boss = lantai × 1000 (lantai 10 = 10.000, lantai 20 = 20.000, … lantai 100 = 100.000). Damage boss naik tiap lantai: **basic 50 + 3×(lantai−1)**, **skill 100 + 3×(lantai−1)**, **skill enrage 200 + 3×(lantai−1)** (lantai 10 = 77 / 127 / 227). Ketika HP boss turun di bawah 30%, ia **enrage** dan memakai N skill elemental (N = lantai / 10). Masuk enrage memutar **cutscene** 2,2 s: pertarungan beku, slow-motion, kamera zoom close-up ke boss, banner **ENRAGE!**, tubuh berdenyut merah-putih, gelombang kejut cincin api + guncangan kamera. Selama enrage tubuh boss terus berdenyut merah dan memercikkan bara; setiap skill enrage ditambah gelombang kejut cincin api, petir merah menyambar di sekitar boss, ledakan di target, dan guncangan kamera. Warna jatuhan/hujan skill condong ke merah.

**HP boss** tampil di atas layar di bawah bar lantai selama boss hidup: pil **BOSS** (berubah **ENRAGE** berdenyut saat enrage), nama boss, bar merah (oranye saat enrage) dengan jejak damage kuning redup yang menyusul, dan angka `7.250 / 10.000` (pemisah ribuan titik). Bar lantai di stage boss hanya berlabel BOSS.

**Boss kebal power-up.** Hanya serangan dasar (bolt) yang melukai boss. Skill power-up (Cincin Es, Petir Berantai, Nova Api, Misil Arkana) tidak membidik boss dan sentuhan ke boss memunculkan **IMMUNE** abu-abu; skill tetap melukai minion. Setiap gelombang minion punya peluang **35%** berisi satu **slime emas pembawa** (berkilau emas). Bunuh slime itu untuk menjatuhkan kristal emas **Boss Breaker** (ditunjuk panah power-up). Mengambilnya membuat semua power-up yang dimiliki bisa melukai boss selama **60 detik**. Selama aktif hero memakai aura emas-merah (cincin cahaya di kaki, api naik, percikan, bara tiap 0,45 s) dan HUD menampilkan hitung mundur **BOSS BREAKER Ns**. Hanya satu Boss Breaker beredar sekaligus (pembawa/kristal/efek aktif), dan pembawa hanya muncul jika hero sudah punya minimal satu skill. Skill memakai telegraf sesuai elemen: batu/es/racun/magma jatuh dari langit dengan **bayangan lunak** yang membesar dan menggelap saat batu mendekat tanah, api menembak bolt api, petir menyambar. Model boss memakai palet per-bagian: tubuh sesuai elemen, **jubah merah**, **mahkota emas**, permata mengikuti elemen.

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
| 10 | Rock Dumpling | 10.000 | 1 | Kulit batu, lempar batu ke atas lalu jatuh ke bayangan di tanah, slam lebih lebar |
| 20 | Flame Dumpling | 20.000 | 2 | Tembakan api (`HostileProjectile`) + genangan bakar |
| 30 | Frost Dumpling | 30.000 | 3 | Hujan es + aura dingin yang memperlambat hero |
| 40 | Volt Dumpling | 40.000 | 4 | Sambaran petir + jatuhan kilat |
| 50 | Toxic Dumpling | 50.000 | 5 | Hujan racun dan genangan |
| 60 | Shadow Dumpling | 60.000 | 6 | Blink, hujan bayangan, selubung gelap |
| 70 | Magma Dumpling | 70.000 | 7 | Hujan magma + jejak lava saat charge |
| 80 | Storm Dumpling | 80.000 | 8 | Dorongan angin + jatuhan petir |
| 90 | Crystal Dumpling | 90.000 | 9 | Cincin duri + perisai yang menahan bolt |
| 100 | Royal Dumpling | 100.000 | 10 | Campuran tembakan api, hujan, dan zap. Kalahkan untuk **Tamat!** |

Pengaturan memuat dua slider volume (disimpan di save meta, `musicLevel`/`sfxLevel`): **Musik (BGM)** mengatur musik, **Suara game** mengatur efek suara dan ambience (hujan, angin, bed biome). Peluru musuh (slime ranged dan boss) tidak punya suara saat mengenai hero; yang terdengar hanya bunyi hit milik hero (`playerhit`).

## Kesulitan (Easy / Normal / Hard)

Dipilih di menu utama (dropdown KESULITAN, bawaan Normal, tersimpan di save meta) untuk run baru; run yang disimpan (Lanjutkan) tetap memakai kesulitan saat run dimulai. **Hard** adalah keseimbangan asli; Normal dan Easy mengubah angka berikut (semuanya di `Difficulty.For`):

| | Easy | Normal | Hard |
|---|---|---|---|
| Damage monster (slime, peluru ranged) | ×0,5 | ×0,75 | ×1 |
| HP monster | ×0,7 | ×0,85 | ×1 |
| Jeda spawn monster | ×1,35 (lebih jarang) | ×1,15 | ×1 |
| HP boss | ×0,5 (lantai 10 = 5.000) | ×0,75 (7.500) | ×1 (10.000) |
| Damage boss (kontak + skill) | ×0,5 | ×0,75 | ×1 |
| HP awal hero | ×1,5 (150) | ×1,25 (125) | ×1 (100) |
| Bonus kartu level-up | ×3 (Ultra 3,0%) | ×2 (2,0%) | ×1 (1,0%) |
| Peluang slime menjatuhkan item | 15% | 8% | 5% |
| EXP | ×1,5 | ×1,25 | ×1 |

Alasan: kematian utama berasal dari damage kontak ke hero 100 HP, jadi damage monster dan HP hero yang paling menurunkan kesulitan; kartu level-up dan drop item hanya menambah kekuatan pelan-pelan (kartu hanya 0,1–1%), jadi dipakai sebagai pelengkap. Peluang rarity kartu dan item (65/20/10/5%) dan stat item per level tidak berubah di semua kesulitan. Install baru mulai di Normal; save lama tanpa pilihan juga Normal, run lama yang tersimpan dianggap Hard.

## Item dan inventori

Monster menjatuhkan perlengkapan (5% per slime; boss menjatuhkan **4 item, salah satunya pasti Ultra**). Rarity memakai chance yang sama dengan kartu upgrade (Normal 65%, Rare 20%, Legend 10%, Ultra 5%). Stat dikali **band lantai** tempat item jatuh (lantai 1–10 = ×1, 11–20 = ×2, … 91–100 = ×10).

| Slot | Stat | Normal | Rare | Legend | Ultra |
|---|---|---|---|---|---|
| Staff | ATK serangan dasar | +100 | +250 | +500 | +1000 |
| Topi | HP | +20 | +50 | +120 | +250 |
| Baju | HP + DEF | +30, 1% | +75, 2% | +180, 3,5% | +360, 5% |
| Celana | HP | +15 | +40 | +100 | +200 |
| Sepatu | Move speed | +2% | +4% | +6% | +9% |

(Nilai di atas untuk level item 1 = lantai 1–10 (band 1); level 2 = lantai 11–20 dst. Staff terbaik level 1 = +1000, level 2 = +2000, level 3 = +3000. ATK dan HP dikali band; DEF +0,25% dan move +0,3% per band. Total DEF dibatasi 75%.)

Item diambil dengan berjalan ke permata berwarna rarity (ikon item melayang di atasnya; makin langka makin tinggi pilar cahayanya). Slot kosong langsung terpakai; item lain masuk tas (40 slot). Tombol tas di HUD membuka **Inventori** (game dijeda): kolom perlengkapan kiri, grid tas kanan, panel detail dengan perbandingan dan tombol **Pakai/Lepas** dan **Buang**. Item yang tertinggal saat masuk pintu/naik lantai otomatis diambil. Tas dan perlengkapan ikut tersimpan di save run (Lanjutkan).

## Upgrade run (level-up)

Setiap kartu level-up mendapat **rarity** acak (berlaku untuk semua jenis upgrade):

| Rarity | Warna | Chance | Kenaikan |
|---|---|---|---|
| Normal | hijau | 65% | +0,1% |
| Rare | biru | 20% | +0,3% |
| Legend | ungu | 10% | +0,5% |
| Ultra | oranye | 5% | +1% |

Kenaikan berlaku sebagai persen stat: DMG, HP, MOVE, RANGE dikali (1 + p); cooldown serangan/dash dikali (1 − p); EXP, DEF, LS, CRIT RATE, CRIT ditambah p poin persen. DMG dan HP menyimpan pecahan (`damageCarry`/`healthCarry`) agar kartu kecil tetap terakumulasi. Status HUD menampilkan persen dengan 0,1% presisi.


Setiap naik level, serangan dasar otomatis **+10**, lalu pilihan 3 upgrade (nama berbahasa Inggris): **Power** (+20% damage), **Swiftness** (+12% gerak), **Vitality** (+25 HP), **Attack Speed**, **Dash**, **Reach** (+20% jangkauan), **EXP**, **Defender** (-8% damage masuk, cap 60%), **Lifesteal**, **Crit Rate** (+8% peluang, cap 70%), dan **Critical** (+30% pengali crit, cap 3,5×). Hero mulai hanya dengan damage dasar — **tidak ada critical** sampai mengambil Crit Rate atau Critical. Crit Rate membuka peluang 8% dengan pengali 1,5×; Critical sendiri membuka crit (peluang 8%) dan menaikkan pengali. Serangan yang crit menampilkan angka **CRIT** emas.

## Skill power-up (tiap 20 level hero)

`StageManager` menjatuhkan kristal di arena yang sama (tanpa ganti peta). Menyentuh kristal memberi skill baru; jika semua sudah dimiliki, level skill naik (maks **10**). Slot skill di HUD menampilkan `Lv N`, dan **MAX** emas saat level 10. Jika keempat skill sudah MAX, kristal tidak muncul lagi. **Tekan tahan** slot skill (~0,45 s) untuk membuka kartu deskripsi: garis aksen warna skill, ikon, nama, pil level (`Lv 2/10`, emas `MAX`), deskripsi, tiga chip statistik (DMG + pecahan/lompatan/misil/radius + CD), baris bonus MAX (★ saat aktif), dan titik status boss; lepas untuk menutup.

Skala per level (1 → 10): damage +30%/level (×3,7 di level 10), +1 pecahan/lompatan/misil tiap 2 level, cooldown ×0,92/level (×0,47), radius +8%/level (×1,72). Level 10 membuka bonus MAX per skill (lihat kolom terakhir). Skill dijalankan `SkillController`. Nova Api adalah cincin api yang membesar dari hero (`FireNova`).

| Skill | Efek | Naik level | Bonus MAX (Lv 10) |
|---|---|---|---|
| Cincin Es | 3 pecahan es mengorbit hero (r 1,5 m). Musuh yang tersentuh terkena 7 damage per 0,5 s dan melambat 55% selama 1,6 s. | pecahan, radius, damage, putaran lebih cepat | slow 40% lebih kuat (hampir membeku) |
| Petir Berantai | Tiap 2,6 s menyambar musuh terdekat (≤ 8 m) lalu melompat ke ≤ 3 musuh lain dalam 3,8 m. | lompatan, damage, cooldown | lompatan tanpa penurunan damage (100%) |
| Nova Api | Tiap 4,2 s cincin api meledak keluar dari hero (radius 2,6 m). Musuh terkena saat tepi cincin menjangkau mereka, lalu terbakar 3 tick. Obstacle memblokir LOS. | radius, damage, cooldown | bakar 2× lebih kuat |
| Misil Arkana | Tiap 1,9 s menembakkan 2 misil pelacak ke musuh acak dalam 9,5 m. | misil, damage, cooldown | +2 misil tambahan |

## Progres permanen

Emas membeli peringkat damage, nyawa, dan laju. Pilihan grafis ikut tersimpan. Meta-save: `Application.persistentDataPath/slimebreaker-save.json`. Progres run: `slimebreaker-run.json` (dihapus saat menang/kalah/Main baru).

## Audio

Orb EXP memakai bunyi "bloop" lembut bernada rendah; pemutaran beruntun pada id yang sama (pickup, kematian slime, hit) otomatis dilembutkan (`crowdFalloff`), jadi panen puluhan orb terdengar tenang. `AudioService` memutar musik per bioma (10 track + `MUS_Boss` saat boss, crossfade equal-power 1,6 s), ambience alam per bioma (`AMB_Night` untuk hutan/kerajaan malam) ditambah lapisan cuaca (hujan, angin), dan efek lewat pool 10 AudioSource dengan variasi pitch dan interval minimum per id supaya kerumunan tidak menumpuk jadi derau. Kamera utama membawa `AudioListener` (dipasang `ProjectSetup` dan dijaga `UrpSceneBootstrap`).

## Jual item

Di Inventori, tombol **Mode jual** membuat ketukan pada item menandainya (bukan memilihnya). Harga jual per rarity: Normal (hijau) 1 emas, Rare (biru) 3, Legend (ungu) 6, Ultra (oranye) 10. Tombol **rarity** dan **level** memilih filter, **Auto pilih** menandai semua item yang cocok (level "Semua" = semua level), **Jual** menjual semua yang ditandai dan emas langsung masuk ke save. Item yang sedang dipakai tidak pernah ikut terjual.

## Riwayat boss di menu

Save menyimpan `bossesDefeated` (rekor terbanyak boss lantai 10, 20, ... yang sudah dilewati; berdiri di lantai 11 berarti boss 10 terlewati, menang = 10). Dicatat saat jeda, keluar ke menu, tutup aplikasi, dan akhir run. Di menu utama, tiap boss itu berdiri sebagai dumpling kecil berwarna bosnya di belakang hero (boss 100 pelangi).

Latar menu utama memakai generator arena yang sama dengan game: biome, cuaca, dan siang/malam lantai terjauh yang pernah dicapai (`highestFloor`). Save baru menampilkan hutan siang (lantai 1). Area sekitar hero dan boss pajangan dikosongkan dari pohon/properti besar.

## Kamera

Geser jari di separuh kanan layar (di luar tombol) memutar kamera mengelilingi hero: ke samping = putar kiri/kanan (0,16° per piksel), ke atas/bawah = sudut kamera (dibatasi 32°–78°). Pengaturan > Kamera: **Normal** (bawaan; geser kanan memutar pandangan ke kanan, geser atas menurunkan kamera = melihat ke atas) atau **Inverted** (dunia mengikuti jari: kedua arah dibalik). Pilihan tersimpan di save (`invertCamera`). Sudut menuju target dengan peredaman, dan fokus kamera saja yang tertinggal dari hero, sehingga putaran tidak mengayun. Joystick bergerak relatif kamera, jadi arah jalan mengikuti sudut baru. Tidak aktif saat jeda atau cutscene boss.
