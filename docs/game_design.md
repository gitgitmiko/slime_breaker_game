# Desain gameplay

Slime Breaker adalah action roguelite 3D offline untuk Android, landscape, target 60 fps.

Satu run sekitar 5 menit, lalu Slime King muncul. Angka ada di `BalanceConfig` (`Assets/ScriptableObjects/Balance/Balance.asset` setelah setup editor).

## Loop

Menu → arena → gerak dan dash → penyihir otomatis menembakkan bolt sihir pelacak ke musuh terdekat yang terlihat (jangkauan 4,4 m; batu/pohon memblokir tembakan) → orb XP → level-up menjeda permainan dan menawarkan 3 upgrade → setiap kelipatan 5 level semua slime lenyap, satu kristal power-up dan portal keluar muncul → masuk portal memuat arena acak baru (hutan / salju / gurun × cuaca × siang/malam) → gelombang makin rapat, HP musuh naik per arena → boss → menang atau kalah → emas disimpan → upgrade permanen di menu. Keluar di tengah run menyimpan progres ke `slimebreaker-run.json`; tombol **Lanjutkan** di menu memulihkan level, arena, skill, HP, dan XP.

## Kontrol

- Kiri bawah: joystick. Di editor, WASD atau tombol panah juga berjalan.
- Kanan bawah: dash. Di editor, spasi.
- Dash punya cooldown dan jendela kebal singkat. Terkena damage juga memberi kebal 0,35 s.
- Jeda: tombol II atau Escape. Dari jeda atau menu, ⚙ / Pengaturan memilih grafis Low / Medium / High (default High: bayangan 4096, soft, 2 cascade).

## Tiga slime dan boss

- Biasa: pelan, nyawa sedang.
- Cepat: kecil, lebih laju, nyawa rendah.
- Tank: besar, lambat, nyawa tinggi, menjatuhkan 2 orb.
- Boss: telegraph lingkaran lalu hantam, lalu menerjang satu kali. Boss hanya setengah terpengaruh efek slow.

## Upgrade run (level-up)

Serangan, laju, nyawa, ritme serangan, dash, jangkauan, XP, dan pengurangan damage.

## Skill power-up (tiap 5 level)

`StageManager` memicu intermisi tiap 5 level: musuh di-`Vanish` (tanpa XP), `PowerUpSpawner` menjatuhkan satu kristal di r 3–8,5 m (menjauh dari obstacle), lalu portal keluar muncul. Menyentuh kristal memberi skill baru; jika semua sudah dimiliki, level skill naik (maks 4). Skill dijalankan `SkillController`. Nova Api sekarang cincin api yang membesar dari hero (`FireNova`), bukan mesh cincin yang di-scale aneh.

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
