# Art Direction — Slime Breaker

Gaya: anime 3D fantasy. Hero adalah **penyihir chibi** (tahap 3 konsep "Squishy Dumpling Co.", mengikuti gambar referensi dari pengguna): topi penyihir ungu raksasa berbahan felt dengan tepi bergerigi sobek berpita emas, dua jahitan X emas dan ujung yang menekuk ke samping; wajah krem bulat dengan dua mata oval hitam saja (tanpa hidung dan mulut); rambut bob pirang yang memudar ke merah muda di ujung dengan poni dan helai samping yang mencuat; capelet dua tingkat ungu bertepi emas-cokelat dengan kancing toggle di kerah; jaket lilac berlengan lonceng dengan tepi emas-cokelat dan kancing kodok; rok krem mengembang; stoking lilac dan sepatu bot cokelat berlipat; sarung tangan krem bulat. Tongkat bulan sabit + kristal tetap di tangan. Model dibangun dari nol di `build_assets.py` (`build_hero`, tekstur kain dilukis numpy: `TEX_Hero_Hat/Jacket/Capelet/Hair`); tulang dan klip animasi tidak berubah.

Semua aset dibuat sendiri di mesin ini (Blender, Python/numpy/Pillow, FluidSynth). Tidak ada karakter, logo, atau desain berhak cipta yang disalin.

## Palet

| Peran | Warna | Hex perkiraan |
|---|---|---|
| Kulit hero | krem hangat | `#FFE6C2` |
| Rambut hero | pirang krem → merah muda di ujung | `#FCE6AD` / `#F794A8` |
| Baju hero | jaket lilac / capelet ungu / rok krem | `#A88ACC` / `#614788` / `#F2EBDB` |
| Topi hero | ungu felt + tepi emas | `#4D3370` / `#FACC1A` |
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

## Boss: squishy dumpling

Kesepuluh boss adalah dumpling jelly berglitter (mesh `BOSS_SlimeKing.fbx`, armature dan klip lama): bao bening berlipatan dengan pucuk berpilin, wajah kawaii (mata besar mengilap bertitik pantul, pipi merona, mulut terbuka dengan lidah) dan bintang emas swa-cahaya di dahi. Warna tubuh tiap boss tetap berbeda (tint per jenis); shader `SlimeBreaker/Jelly` kini punya serpihan glitter yang berkelip (`_Glitter`) dan pusaran pelangi (`_Rainbow`). Boss lantai 100 (Royal Dumpling) pelangi: warna jelly berputar melalui semua hue.

## Arena: dinding wadah dimsum

Konsep "Squishy Dumpling Co.": arena berada di dalam steamer dimsum. Lantai (rumput, pasir, salju, …), properti, dan pencahayaan per bioma tetap; yang berubah adalah tepinya: dinding setinggi 2,5 m berlapis anyaman bambu (twill) dengan bibir kayu pucat membulat, gelang bambu dan simpul di luar. Semua properti yang dulu berdiri di luar batas (pohon di bukit) kini berdiri di dalam, dalam sabuk 2–4 m di sepanjang dinding. Dinding rendah supaya kamera tinggi tidak menutupi hero di tepi. Tahap berikutnya: boss jadi squishy dumpling (lantai 100 pelangi), lalu hero.

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
