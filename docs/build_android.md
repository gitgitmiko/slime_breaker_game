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

Exit 0 berarti semua test di `Assets/Tests/PlayMode/ArenaPlaytest.cs` lolos: hero dan slime menapak tanah, properti tegak, semua material valid, tombol Main membuka arena, joystick sentuh Fixed (default) menggerakkan hero dan toggle Pengaturan ke Free (dasar pindah ke titik sentuh), tekan tahan slot skill menampilkan deskripsi, bot bertahan satu menit, AudioListener + 60 fps terpasang, power-up tiap 20 level hero di map yang sama (bukan boss), skill naik sampai Lv 10 dengan tanda MAX, Cincin Es melukai dan memperlambat, bolt mengenai musuh saat diam (tidak saat jalan), Nova Api merusak musuh dekat, obstacle memblokir bolt, run-save level/lantai utuh, preset grafis Low/High terpasang, bar darah musuh, lifesteal + angka hijau di atas hero, slime jarak menembak, Rock Slime + warning di lantai 10 (HP 10.000, bar HP boss `10.000 / 10.000` yang ikut turun dan tag ENRAGE, damage basic naik tiap lantai, enrage <30% + cutscene zoom ke boss + skill enrage heboh, 10 minion, skyfall), boss kebal power-up kecuali Boss Breaker dari slime emas (60 dtk, aura hero, timer HUD), basic attack tetap melukai boss,, kuota lantai N×10 membuka pintu lalu Enter naik lantai (malam + layout baru di lantai 2, tanpa pintu langsung), HUD Lantai/progres bunuh/ATK/EXP/DEF (kartu status 10 sel) + wayfinder + upgrade crit + nama upgrade Inggris (Defender), collider obstacle, angka damage/CRIT, dan biome gurun/salju/petir di lantai 11/21/31. Baris `PLAYTEST` di log memuat angka ukurnya. Screenshot ada di `Logs/playtest/` (00_menu … 29_hero_victory).

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

## Hasil 16 Oktober 2026 (putaran 11 — skill Lv 10, power-up tiap 20 level, enrage cutscene)

- Setup: `SLIMEBREAKER_SETUP_OK`; EditMode 21/21; PlayMode 26/26 lolos; APK: `SLIMEBREAKER_APK_OK`.
- APK: `Builds/SlimeBreaker.apk` (42 MB), disalin ke `~/Downloads/SlimeBreaker.apk`.
- Paket: `com.gitgitmiko.slimebreaker` 0.11.0 (versionCode 11).
- PlayMode menambah tes skill Lv 10 + MAX, angka lifesteal hijau, joystick bebas, DEF di HUD, nama Defender, cutscene enrage (kamera 4,7 m dari boss) dan flourish skill enrage.
- Unduhan: `https://testimony-entire-journalism-residents.trycloudflare.com/`

## Hasil 16 Oktober 2026 (putaran 12 — joystick Fixed/Free, deskripsi skill, Boss Breaker)

- Setup: `SLIMEBREAKER_SETUP_OK`; EditMode 21/21; PlayMode 28/28 lolos; APK: `SLIMEBREAKER_APK_OK`.
- APK: `Builds/SlimeBreaker.apk` (42 MB), disalin ke `~/Downloads/SlimeBreaker.apk`.
- Paket: `com.gitgitmiko.slimebreaker` 0.12.0 (versionCode 12).
- PlayMode menambah tes tekan-tahan deskripsi skill dan boss kebal power-up + Boss Breaker; tes joystick kini memeriksa default Fixed dan toggle Free.
- Unduhan: `https://testimony-entire-journalism-residents.trycloudflare.com/`

## Hasil 16 Oktober 2026 (putaran 13 — kartu status & deskripsi modern, bar HP boss)

- Setup: `SLIMEBREAKER_SETUP_OK`; EditMode 22/22; PlayMode 28/28 lolos; APK: `SLIMEBREAKER_APK_OK`.
- APK: `Builds/SlimeBreaker.apk` (42 MB), disalin ke `~/Downloads/SlimeBreaker.apk`.
- Paket: `com.gitgitmiko.slimebreaker` 0.13.0 (versionCode 13).
- Perbaikan: posisi istirahat joystick disimpan sebagai `anchoredPosition` (sebelumnya `localPosition` di `Awake`, basi di resolusi selain 1920×1080 sehingga mode Fixed bisa menolak sentuhan).
- Unduhan: `https://testimony-entire-journalism-residents.trycloudflare.com/`

## Hasil 16 Oktober 2026 (putaran 14 — HUD dan menu bergaya kaca)

- Setup: `SLIMEBREAKER_SETUP_OK`; EditMode 22/22; PlayMode 29/29 lolos; APK: `SLIMEBREAKER_APK_OK`.
- APK: `Builds/SlimeBreaker.apk` (42 MB), disalin ke `~/Downloads/SlimeBreaker.apk`.
- Paket: `com.gitgitmiko.slimebreaker` 0.14.0 (versionCode 14).
- PlayMode menambah tes gaya HUD (track/fill bar, cincin dash, tombol kaca, sorotan pilihan pengaturan) dan pemeriksaan tombol menu utama.
- Unduhan: `https://testimony-entire-journalism-residents.trycloudflare.com/`

## Hasil 16 Oktober 2026 (putaran 15 — hero penyihir ungu chibi)

- Aset: `blender … hero` → `CHR_Hero_Main.fbx` 26.964 tris, 9 klip (Idle, Run, Attack, Dash, Hit, Death, Cast, Victory, Menu).
- Setup: `SLIMEBREAKER_SETUP_OK`; EditMode 22/22; PlayMode 30/30 lolos; APK: `SLIMEBREAKER_APK_OK`.
- APK: `Builds/SlimeBreaker.apk` (42 MB), disalin ke `~/Downloads/SlimeBreaker.apk`.
- Paket: `com.gitgitmiko.slimebreaker` 0.15.0 (versionCode 15).
- PlayMode menambah tes klip hero + tekstur mata + emote Cast/Victory, dan menu utama memutar loop Menu 4 s.
- Unduhan: `https://testimony-entire-journalism-residents.trycloudflare.com/`

## Hasil 17 Oktober 2026 (putaran 16 — lingkungan dan audio HD)

- Setup: `SLIMEBREAKER_SETUP_OK`; EditMode 22/22; PlayMode 31/31 lolos; APK: `SLIMEBREAKER_APK_OK`.
- APK: `Builds/SlimeBreaker.apk` (98 MB — tekstur PBR 2048 dan audio orkestra), disalin ke `~/Downloads/SlimeBreaker-0.16.0.apk`.
- Paket: `com.gitgitmiko.slimebreaker` 0.16.0 (versionCode 16).
- PlayMode menambah tes lingkungan HD di 10 bioma (PostFx, shader Ground, properti HD) dan audio per bioma + boss.
- Unduhan: `https://testimony-entire-journalism-residents.trycloudflare.com/`

## Hasil 17 Oktober 2026 (putaran 17 — performa 60 fps, kaktus)

- Rumput/bunga/alang-alang digabung per material ke 8 sektor (tanpa bayangan); tint bioma memakai salinan material, bukan property block, jadi SRP batcher + static batching bekerja (mesh lingkungan kini readable). Renderer arena: hutan 581 → 141, kerajaan 361 → 84, rawa 311 → 93.
- Preset High: MSAA 2×, shadow map 2048, PCF medium, jarak bayangan 32 m, HDR 32-bit; bloom dual-filter seperempat resolusi.
- `AdaptiveResolution`: render scale turun bertahap (min 0,7) bila frame > 18 ms dan naik lagi saat longgar, di-upscale FSR 1; resolusi internal dibatasi ~1080p di layar sangat padat. Nonaktif di editor.
- Kaktus: ujung batang/lengan kini kubah tertutup (tutup datar berbentuk bintang dulu salah triangulasi → kilau kuning/putih), kuncup putih diganti buah merah kecil.
- Setup: `SLIMEBREAKER_SETUP_OK`; EditMode 22/22; PlayMode 32/32 (tes baru `RenderBudgetPerBiome`); APK `SLIMEBREAKER_APK_OK` 0.16.1 (versionCode 17).
- Unduhan: `https://testimony-entire-journalism-residents.trycloudflare.com/`

## Hasil 17 Oktober 2026 (putaran 18 — hero cat-boy, HP boss, audio tenang)

- Hero dibangun ulang mengikuti referensi cat-boy (rambut hitam bergerigi, telinga kucing, mata amber, :3 + taring, choker lonceng, kaos navy, celana hitam) + topi, jubah, tongkat penyihir. `CHR_Hero_Main.fbx` 28.546 tris, rig dan klip sama.
- HP boss 10.000.000 × tier (lantai 100 = 100.000.000).
- SFX pickup diganti "bloop" lembut; volume pickup/kematian/hit turun + pelembutan otomatis saat beruntun.
- Kaktus: duri/areola diredam dan smoothness rendah, tak ada lagi kilau putih saat kamera bergerak.
- Setup: `SLIMEBREAKER_SETUP_OK`; EditMode 22/22; PlayMode 32/32; APK `SLIMEBREAKER_APK_OK` 0.17.0 (versionCode 18).

## Hasil 17 Oktober 2026 (putaran 19 — proporsi hero mengikuti chibi cat-boy)

- Proporsi diukur dari gambar referensi lalu disamakan: rambut lebih ramping (±0,27 H) dengan tonjolan bergelombang di sisi/belakang dan ujung tidak turun di bawah dagu, kaki lebih tebal dan rapat (tulang kaki digeser ke x ±0,075–0,085), lengan/tangan lebih tebal, kaos lebih lebar, telinga di sudut atas rambut. `CHR_Hero_Main.fbx` 28.578 tris.
- Setup: `SLIMEBREAKER_SETUP_OK`; EditMode 22/22; PlayMode 32/32; APK `SLIMEBREAKER_APK_OK` 0.17.1 (versionCode 19).

## Hasil 17 Oktober 2026 (putaran 20 — bola cahaya putih)

- Penyebab "cahaya putih" di dekat kaktus saat hero bergerak: kunang-kunang malam dipancarkan dari bola radius 10 m yang berpusat 10 m di atas hero, jadi sebagian lewat tepat di depan lensa kamera (kamera ±8 m di atas hero) dan tampil sebagai cakram putih raksasa + bloom.
- Perbaikan: kunang-kunang, debu, spora, dan percikan kini di lapisan rendah (1,2–1,5 m) di sekitar hero; semua partikel cuaca dibatasi `maxParticleSize` 0,02 layar. Tes `RenderBudgetPerBiome` memeriksa keduanya.
- Setup: `SLIMEBREAKER_SETUP_OK`; EditMode 22/22; PlayMode 32/32; APK `SLIMEBREAKER_APK_OK` 0.17.2 (versionCode 20).

## Hasil 17 Oktober 2026 (putaran 21 — glitch kaktus NaN, cuaca acak)

- Akar masalah cahaya putih / segi delapan hitam: ujung kubah kaktus (2 face di puncak batang, 1 di tiap ujung lengan) punya UV runtuh ke satu titik → tangent nol dari importer → `normalize(0)` = NaN di `SlimeBreaker/Lit` → bloom melebarkan satu piksel NaN/Inf jadi cakram putih atau hitam. Muncul saat bergerak karena tergantung sudut kamera.
- Perbaikan berlapis: `fix_degenerate_uvs` di `build_props_hd.py` (proyeksi planar untuk face ber-UV nol; hanya kaktus yang terkena), TBN aman (`SafeNormalize` + tangent cadangan) dan clamp output 0–32 di ketiga shader, `stopNaN` di kamera, `bloom.clamp` 24.
- Cuaca dan siang/malam kini diacak terpisah per lantai dari seed run (60% siang); cuaca tidak berulang di dua lantai berturut bioma sama. Sebelumnya cuaca dan waktu sama-sama berotasi ganjil/genap, sehingga hujan selalu malam. Tes EditMode `RainFallsByDayAndByNight`.
- Setup: `SLIMEBREAKER_SETUP_OK`; EditMode 23/23; PlayMode 32/32; APK `SLIMEBREAKER_APK_OK` 0.17.3 (versionCode 21).

## Hasil 17 Oktober 2026 (putaran 22 — rarity upgrade, HP boss 10.000)

- Kartu level-up ber-rarity: Normal hijau 65% +0,1%, Rare biru 20% +0,3%, Legend ungu 10% +0,5%, Ultra oranye 5% +1% (sisa 5% dari 95% ditambahkan ke Normal sesuai pilihan). Kartu memakai pita + warna rarity; status HUD 0,1% presisi.
- HP boss kembali 10.000 × tier (lantai 100 = 100.000).
- Setup: `SLIMEBREAKER_SETUP_OK`; EditMode 25/25; PlayMode 32/32; APK `SLIMEBREAKER_APK_OK` 0.18.0 (versionCode 22).

## Hasil 17 Oktober 2026 (putaran 23 — item drop dan inventori)

- Item Staff/Topi/Baju/Celana/Sepatu ber-rarity dengan stat per band lantai (Legend staff +1000 di lantai 1–10, +2000 di 11–20, …); drop 5% per slime, boss 4 item dengan 1 Ultra pasti.
- Inventori 40 slot, layar perlengkapan + tas + detail (Pakai/Lepas/Buang), tersimpan di save run; stat gear dihitung terpisah (`RunStats.gear*`, `Total*`).
- Setup: `SLIMEBREAKER_SETUP_OK`; EditMode 27/27; PlayMode 33/33; APK `SLIMEBREAKER_APK_OK` 0.19.0 (versionCode 23).

## Hasil 18 Oktober 2026 (putaran 24 — pengaturan suara)

- Peluru musuh tidak lagi memutar `bolthit` saat kena hero (suara kerumunan slime ranged menutupi BGM); `playerhit` milik hero tetap berbunyi.
- Pengaturan: slider Musik (BGM) dan Suara game, persen tampil di sisi kanan, tersimpan ke save meta (ditulis ±0,4 dtk setelah digeser).
- Setup: `SLIMEBREAKER_SETUP_OK`; EditMode 28/28; PlayMode 34/34; APK `SLIMEBREAKER_APK_OK` 0.19.1 (versionCode 24).

## Hasil 18 Oktober 2026 (putaran 26 — dinding wadah dimsum)

- Koreksi arah: lantai dan properti kembali seperti semula; hanya dinding arena yang menjadi steamer dimsum berlapis anyaman bambu, dan semua properti yang tadinya di luar batas (pohon) dipindah ke dalam.
- Tes baru `SteamerWallEnclosesTheArenaAndEveryPropIsInside` (dinding terlihat di tepi, tak ada properti di luar dinding di 4 lantai/bioma).
- Setup: `SLIMEBREAKER_SETUP_OK`; EditMode 28/28; PlayMode 35/35; APK `SLIMEBREAKER_APK_OK` 0.20.1 (versionCode 26).

## Hasil 18 Oktober 2026 (putaran 27 — collider tak terlihat, pohon acak)

- Akar masalah hero "terhalang sesuatu": properti ring/scatter (pohon, batu, tiang, kaktus, dll.) memasang collider langsung pada akar model FBX yang membawa rotasi sumbu (-90° X). Capsule/box-nya jadi **rebah ke samping** dan **bergeser 1–3,6 m** dari propnya (capsule pohon selebar 5–6,6 m, tinggi 1,4 m). Setelah pohon dipindah ke dalam arena, collider itu mulai menghalangi.
- Perbaikan: collider kini dipasang di anak `SolidCollider` yang tegak dan terkompensasi skala, ukurannya dari `PropShape` (ukuran bebas-yaw, diukur saat prefab dibuat): pohon = batang saja (r 0,3–0,5), batu = bola ±0,85× setengah lebar, tiang/lengkungan = kotak tipis mengikuti bentuk. Prefab `OBS_*` juga diketatkan (sebelumnya bola ES 2,0 m untuk paku es setinggi 0,9 m, kotak persegi untuk lengkungan tipis, dst.).
- Pohon besar kini tersebar acak di seluruh arena (termasuk tengah), ditempatkan terakhir menghindari properti lain, jalur kardinal, dan titik spawn.
- Tes baru: `ObstacleCollidersHugTheirVisibleShape` (collider tidak lebih lebar dari bentuk yang terlihat, tidak rebah, tidak keluar dari bentuk visual di 10 bioma) dan `BigTreesAreScatteredAcrossTheArenaIncludingTheMiddle`.
- Setup: `SLIMEBREAKER_SETUP_OK`; EditMode 28/28; PlayMode 37/37; APK `SLIMEBREAKER_APK_OK` 0.20.2 (versionCode 27).

## Hasil 18 Oktober 2026 (putaran 28 — garis biru, level item)

- Cincin rune biru bercahaya di lantai dekat dinding dihapus (dinding steamer sudah jadi batas arena; cincin hanya tersisa di pintu portal dan di bawah hero di menu).
- Level item mengikuti lantai: 1–10 = level 1, 11–20 = level 2, dst. Staff terbaik (Ultra) level 1 = +1000, level 2 = +2000, level 3 = +3000. Staff: Normal 100, Rare 250, Legend 500, Ultra 1000 × level. Level tampil di notifikasi ambil item, slot, dan kotak tas ("L2").
- Tes: `ItemStatsFollowSlotRarityAndFloorBand` (tak ada item level 1 > +1000, lantai 15 = level 2) dan drop nyata di lantai 15 pada `ItemsDropEquipAndPersist`.
- Setup: `SLIMEBREAKER_SETUP_OK`; EditMode 28/28; PlayMode 37/37; APK `SLIMEBREAKER_APK_OK` 0.20.3 (versionCode 28).

## Hasil 18 Oktober 2026 (putaran 29 — boss squishy dumpling)

- Boss jadi dumpling jelly berglitter berwajah kawaii + bintang emas; nama "X Dumpling"; jubah dan mahkota dihapus; Royal (lantai 100) pelangi.
- Shader Jelly: `_Glitter`, `_GlitterScale`, `_Rainbow`; `EnemyHealth` mengirim `_Rainbow` untuk Royal dan menjaga cahaya sendiri bintang/pipi saat tint.
- Tes: `EveryBossSpawnsWithItsOwnLookAndHp` (jenis, HP, potret) dan `AssertDumplingLook`.
- Setup: `SLIMEBREAKER_SETUP_OK`; EditMode 28/28; PlayMode 38/38; APK `SLIMEBREAKER_APK_OK` 0.21.0 (versionCode 29).

## Hasil 18 Oktober 2026 (putaran 30 — cahaya boss malam hari)

- Saat lantai malam, tiap boss membawa lampu titik 2,9 m di atas kepalanya (range 9, intensitas 2,6), berwarna versi terang dari warna boss (Royal pelangi: putih hangat), seperti lampu hero. Siang tanpa lampu. Tes `EveryBossSpawnsWithItsOwnLookAndHp` memeriksa keduanya.
- Setup: `SLIMEBREAKER_SETUP_OK`; EditMode 28/28; PlayMode 38/38; APK `SLIMEBREAKER_APK_OK` 0.21.1 (versionCode 30).

## Hasil 18 Oktober 2026 (putaran 31 — tata letak pohon)

- Pohon besar kini ditempatkan lebih dulu (sebelum properti ring), jadi benar-benar tersebar acak termasuk tengah arena (±37% berada <13 m dari pusat; sebelumnya ±15%); properti ring melewati titik yang sudah terisi. Skala pohon 0,85–1,15. Tes `BigTreesAreScatteredAcrossTheArenaIncludingTheMiddle` dikencangkan (stabil di 3 run).
- Setup: `SLIMEBREAKER_SETUP_OK`; EditMode 28/28; PlayMode 38/38; APK `SLIMEBREAKER_APK_OK` 0.21.2 (versionCode 31).

## Hasil 18 Oktober 2026 (putaran 32 — mode kesulitan)

- `Difficulty` (Easy/Normal/Hard) dengan profil pengali; dipilih di menu utama, disimpan di `SaveData.difficulty` (baru: Normal) dan `RunSave.difficulty` (lama: Hard). Diterapkan ke HP/damage monster, jeda spawn, HP/damage boss, HP awal hero, bonus kartu, peluang drop item, dan EXP.
- Perbaikan sampingan: bar HP hero kini memakai HP awal sebenarnya (rank HP permanen dan kesulitan) — sebelumnya `PlayerHealth` bisa terbangun sebelum stat run dan tertahan di 100. Menu juga membaca ulang save sebelum membeli upgrade/memilih kesulitan supaya slider suara tak tertimpa.
- Tes: `DifficultyLevelsScaleMonstersBossesUpgradesAndDrops`, `DifficultyIsSavedWithTheRunAndOldSavesStayHard`, `MenuDifficultyButtonsDecideTheNextRun`; semua tes arena memakai Hard.
- Setup: `SLIMEBREAKER_SETUP_OK`; EditMode 30/30; PlayMode 39/39; APK `SLIMEBREAKER_APK_OK` 0.22.0 (versionCode 32).
- Setup: `SLIMEBREAKER_SETUP_OK`; EditMode 31/31; PlayMode 41/41; APK 0.23.0 (versionCode 33): jual item + auto pilih, dropdown kesulitan.
- Setup: `SLIMEBREAKER_SETUP_OK`; EditMode 31/31; PlayMode 42/42; APK 0.24.0 (versionCode 34): menu (Lanjutkan di atas Main, tanpa deskripsi), boss yang sudah dikalahkan berdiri di belakang hero.
- Setup: `SLIMEBREAKER_SETUP_OK`; EditMode 31/31; PlayMode 43/43; APK 0.25.0 (versionCode 35): arena menu mengikuti lantai terjauh (`MenuArena`), kartu menu dan inventori dirapikan.
- Setup: `SLIMEBREAKER_SETUP_OK`; EditMode 31/31; PlayMode 43/43; APK 0.26.0 (versionCode 36): hero baru penyihir chibi (tahap 3 redesign).
- Setup: `SLIMEBREAKER_SETUP_OK`; EditMode 31/31; PlayMode 44/44; APK 0.27.0 (versionCode 37): geser di separuh kanan layar memutar kamera.
- Setup: `SLIMEBREAKER_SETUP_OK`; EditMode 31/31; PlayMode 44/44; APK 0.28.0 (versionCode 38): orbit kamera dihaluskan + bisa atas/bawah, hero lebih cerah (warna lebih jenuh + emisi lembut).
- Setup: `SLIMEBREAKER_SETUP_OK`; EditMode 31/31; PlayMode 45/45; APK 0.29.0 (versionCode 39): pengaturan arah rotasi kamera Normal (bawaan) / Inverted.
- Setup: `SLIMEBREAKER_SETUP_OK`; EditMode 32/32; PlayMode 48/48; APK `SLIMEBREAKER_APK_OK` 0.30.0 (versionCode 40): 3 nyawa per run, panel Nyawa Habis (hitung mundur 5 detik, Game Baru / Lanjutkan), iklan layar penuh (house ad `AdService`) sebelum lanjut. Screenshot `30_life_lost`, `31_continue_countdown`, `32_ad_fullscreen`, `33_ad_close`.
- Persiapan Google Play, 0.30.1 (versionCode 41): ikon aplikasi (adaptive + legacy), target API 36, `BuildAndroidAab` → `Builds/SlimeBreaker.aab` 104 MB ditandatangani upload key (`jarsigner -verify` lolos), semua `.so` arm64 rata 16 KB (`0x4000`). Panduan lengkap: `docs/play_store.md`.
- Setup: `SLIMEBREAKER_SETUP_OK`; EditMode 32/32; PlayMode 49/49; APK + AAB 0.31.0 (versionCode 42): AdMob Rewarded (Google Mobile Ads 11.5.0 + UMP). Manifest berisi App ID AdMob + izin `AD_ID`; APK memakai unit uji Google (`SB_ADMOB_TEST`), AAB memakai unit asli `…/8407574176`; iklan ditutup sebelum reward → kembali ke panel Nyawa Habis.

## Pasang di HP

1. Salin `SlimeBreaker.apk` ke ponsel (USB, Drive, atau kirim ke diri sendiri).
2. Di Android, izinkan pasang dari sumber tidak dikenal untuk aplikasi yang dipakai transfer.
3. Buka file APK, pasang, lalu jalankan **Slime Breaker**.
4. Putar HP ke landscape. Butuh Android 7.1 atau lebih baru, CPU ARM64.
