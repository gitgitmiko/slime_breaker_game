# Desain gameplay

Slime Breaker adalah action roguelite 3D offline untuk Android, landscape, target 60 fps.

Satu run sekitar 5 menit, lalu Slime King muncul. Angka ada di `BalanceConfig` (`Assets/ScriptableObjects/Balance/Balance.asset` setelah setup editor).

## Loop

Menu → arena → gerak dan dash → penyihir otomatis menembakkan bolt sihir pelacak ke musuh terdekat (jangkauan 4,4 m) → orb XP → level-up menjeda permainan dan menawarkan 3 upgrade → setiap kelipatan 5 level satu kristal power-up muncul acak di arena → gelombang makin rapat → boss → menang atau kalah → emas disimpan → upgrade permanen di menu.

## Kontrol

- Kiri bawah: joystick. Di editor, WASD atau tombol panah juga berjalan.
- Kanan bawah: dash. Di editor, spasi.
- Dash punya cooldown dan jendela kebal singkat. Terkena damage juga memberi kebal 0,35 s.
- Jeda: tombol II atau Escape.

## Tiga slime dan boss

- Biasa: pelan, nyawa sedang.
- Cepat: kecil, lebih laju, nyawa rendah.
- Tank: besar, lambat, nyawa tinggi, menjatuhkan 2 orb.
- Boss: telegraph lingkaran lalu hantam, lalu menerjang satu kali. Boss hanya setengah terpengaruh efek slow.

## Upgrade run (level-up)

Serangan, laju, nyawa, ritme serangan, dash, jangkauan, XP, dan pengurangan damage.

## Skill power-up (tiap 5 level)

`PowerUpSpawner` menjatuhkan satu kristal di r 3–8,5 m, minimal 3,5 m dari hero. Menyentuhnya memberi skill baru; jika semua sudah dimiliki, kristal menaikkan level skill (maks 4). Skill dijalankan `SkillController` di hero, HUD menampilkannya di slot kanan atas.

| Skill | Efek | Naik level |
|---|---|---|
| Cincin Es | 3 pecahan es mengorbit hero (r 1,5 m). Musuh yang tersentuh terkena 7 damage per 0,5 s dan melambat 55% selama 1,6 s. | +1 pecahan, radius +12%, damage +35% |
| Petir Berantai | Tiap 2,6 s menyambar musuh terdekat (≤ 8 m) lalu melompat ke ≤ 3 musuh lain dalam 3,8 m. | +1 lompatan, cooldown -15% |
| Nova Api | Tiap 4,2 s gelombang api radius 2,6 m dari hero; musuh terkena damage lalu terbakar 3 tick. | radius +12%, damage +35% |
| Misil Arkana | Tiap 1,9 s menembakkan 2 misil pelacak ke musuh acak dalam 9,5 m. | +1 misil, cooldown -15% |

## Progres permanen

Emas membeli peringkat damage, nyawa, dan laju. Disimpan di `Application.persistentDataPath/slimebreaker-save.json`.

## Audio

`AudioService` memutar musik loop dan efek lewat pool 10 AudioSource dengan variasi pitch dan interval minimum per id supaya kerumunan tidak menumpuk jadi derau. Kamera utama membawa `AudioListener` (dipasang `ProjectSetup` dan dijaga `UrpSceneBootstrap`).
