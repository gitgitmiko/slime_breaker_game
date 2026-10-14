# Desain gameplay MVP

Slime Breaker adalah action roguelite 3D offline untuk Android, landscape.

Satu run sekitar 5 menit, lalu Slime King muncul. Angka ada di `BalanceConfig` (`Assets/ScriptableObjects/Balance/Balance.asset` setelah setup editor).

## Loop

Menu → arena → gerak dan dash → serangan otomatis ke musuh terdekat → orb XP → level-up menjeda permainan dan menawarkan 3 upgrade → gelombang makin rapat → boss → menang atau kalah → emas disimpan → upgrade permanen di menu.

## Kontrol

- Kiri bawah: joystick. Di editor, WASD atau tombol panah juga berjalan.
- Kanan bawah: dash. Di editor, spasi.
- Dash punya cooldown dan jendela kebal singkat.
- Jeda: tombol II atau Escape.

## Tiga slime dan boss

- Biasa: pelan, nyawa sedang.
- Cepat: kecil, lebih laju, nyawa rendah.
- Tank: besar, lambat, nyawa tinggi.
- Boss: telegraph lingkaran lalu hantam, lalu menerjang satu kali.

## Upgrade run

Serangan, laju, nyawa, ritme serangan, dash, jangkauan, XP, dan pengurangan damage.

## Progres permanen

Emas membeli peringkat damage, nyawa, dan laju. Disimpan di `Application.persistentDataPath/slimebreaker-save.json`.
