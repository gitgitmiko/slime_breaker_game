# Art Direction — Slime Breaker

Gaya: anime 3D fantasy. Hero adalah **penyihir cat-boy chibi** yang mengikuti desain referensi *Cat-boy Ashuo* (l4oyu, itch.io): kepala sangat besar, rambut hitam tebal mengembang dengan ujung runcing bergerigi di sisi dan belakang, poni tebal dengan satu helai di tengah, kilau rambut oval kebiruan, telinga kucing hitam berbulu krem di dalam, mata anime besar amber/oranye bergradasi dengan bulu mata hitam tebal, alis hitam tebal, mulut ":3" dengan dua taring, pipi merona, kulit hangat, choker hitam dengan lonceng emas, kaos navy lengan pendek, celana pendek hitam, kaki telanjang. Tambahan gaya penyihir: topi penyihir ungu miring di antara telinga (pita ungu, bulan emas), jubah pendek navy berlapis ungu dengan kancing emas, dan tongkat bulan sabit + kristal. Model dibangun ulang dari nol di `build_assets.py` (`build_hero`) berdasarkan gambar pratinjau; file FBX/tekstur asli (berbayar) tidak dipakai atau disalin.

Semua aset dibuat sendiri di mesin ini (Blender, Python/numpy/Pillow, FluidSynth). Tidak ada karakter, logo, atau desain berhak cipta yang disalin.

## Palet

| Peran | Warna | Hex perkiraan |
|---|---|---|
| Kulit hero | peach hangat | `#FFDBC7` |
| Rambut hero | hitam / kilau biru pucat | `#0B0B0F` / `#99A3DB` |
| Baju hero | kaos navy / celana hitam / jubah navy-ungu | `#1A1C33` / `#09090B` / `#57309A` |
| Topi hero | ungu + pita ungu terang | `#331C66` / `#7A47D1` |
| Topi dan cape luar | indigo | `#29216B` |
| Cape dalam | magenta lembut | `#DB529E` |
| Emas (sabuk, cincin tongkat) | kuning hangat | `#F5C74D` |
| Iris / kristal | violet / biru es | `#7D5CFF` / `#8CE6FF` |
| Slime biasa | mint | `#38D17A` |
| Slime cepat | cyan | `#26C7FF` |
| Slime tank | ungu batu | `#75529E` |
| Slime King | magenta | `#D12E6B` |
| Tanah | lumut, bercak tanah | `#3D7A3A` |
| Skill es / petir / api / arkana | `#8CD9FF` / `#FFF273` / `#FF8033` / `#BF73FF` |

## Proporsi dan kamera

- Hero tinggi 1,80 m sampai ujung topi (puncak rambut 1,33 m), tongkat di tangan kanan dengan tulang `StaffTip` (bulan sabit + kristal) sebagai titik lahir proyektil. Topi punya tulang `Hat` sendiri (bisa terlepas di Death) dan `HatTip` yang bergoyang.
- Slime biasa sekitar 0,85 m. Varian cepat lebih kecil dan pipih. Varian tank lebih besar dengan lempeng batu. Slime King sekitar 2,1 m dengan mahkota.
- Arah hadap sumber di Blender adalah -Y, diekspor FBX (forward -Z, up Y) supaya di Unity menghadap +Z.
- Pivot di telapak kaki. Gerak animasi in-place; perpindahan dunia diatur script.
- Kamera gameplay: offset (0, 8.3, -6.9) dari hero, FOV 50, sedikit mengarah ke arah gerak (`FollowCamera.leadDistance`), goyang pendek saat hero terkena damage.
- Arena bermain r = 11,2 m dibatasi cincin rune emisif dan 18 batu batas; di luar itu tanah naik menjadi bukit berpohon sampai 40 m supaya tepi mesh tidak pernah terlihat.

## Material

- Tanah: shader `SlimeBreaker/Ground` dengan set PBR 2048/1024 px (albedo, normal, mask AO/smoothness/metallic) dan anti-tiling. Properti: `SlimeBreaker/Lit` dengan daun/rumput beralpha yang bergoyang angin dan penutup bioma (lumut, salju, pasir, abu).
- Slime dan Slime King: `SlimeBreaker/Jelly` — gel basah dengan subsurface, rim terang, inti lebih gelap.
- Post-processing ACES + bloom + grading per bioma (gurun hangat, salju dingin, bayangan ungu, gunung api oranye); malam lebih biru dan bloom lebih kuat.
- Slime, permata, iris, kristal, orb XP, dan mesh skill memakai emisi supaya tetap terbaca; musuh yang melambat berpendar biru, yang terbakar berpendar oranye (MaterialPropertyBlock).
- Emas metallic 0,75. Partikel dan trail memakai material aditif tanpa tulis depth.
- Satu-satunya transparansi alpha adalah sinar tipis di atas pickup power-up.

## Rig dan animasi

- Rig generic buatan sendiri, bukan Humanoid Mecanim.
- Hero: Idle (napas, kepala miring, ujung topi bergoyang), Run (lari kecil chibi yang memantul, tangan bebas mengayun), Attack (pukulan tongkat: angkat ke belakang kepala lalu hantam ke depan, bolt lepas saat hantaman ≈0,17 s), Dash (condong jauh, rambut dan cape tertarik), Hit, Death (topi terlempar), Cast (tongkat diangkat lurus dan diputar — saat mengambil power-up/Boss Breaker), Victory (lompat, tongkat ke atas, tangan memegang topi — saat boss kalah), Menu (loop 4 s di menu utama: ayun badan, melambai, memutar tongkat, lompat kecil).
- Slime dan boss: Idle, Move, Hit, Death. Boss menambah Telegraph dan Slam.
- Semua state satu kali (Hit, Dash, Attack, Telegraph, Slam) kembali ke Idle lewat exit time sehingga animator tidak pernah membeku.
- Deformasi slime memakai skala tulang, bukan blendshape.
