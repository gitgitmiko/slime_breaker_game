# Art Direction — Slime Breaker

Gaya: anime 3D fantasy. Hero adalah penyihir perempuan bergaya anime (topi runcing, jubah, cape, twin tail, mata besar dengan highlight), dibaca jelas dari kamera third-person agak tinggi.

Semua aset dibuat sendiri di mesin ini (Blender, Python/numpy/Pillow, FluidSynth). Tidak ada karakter, logo, atau desain berhak cipta yang disalin.

## Palet

| Peran | Warna | Hex perkiraan |
|---|---|---|
| Kulit hero | peach hangat | `#FFDBC7` |
| Rambut hero | lavender pucat / ungu | `#D6C7FF` / `#9E8ADB` |
| Jubah hero | ungu tua | `#3B2E8F` |
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

- Hero tinggi 1,87 m sampai ujung topi (badan sekitar 1,3 m), kepala besar, tongkat di tangan kanan dengan tulang `StaffTip` sebagai titik lahir proyektil.
- Slime biasa sekitar 0,85 m. Varian cepat lebih kecil dan pipih. Varian tank lebih besar dengan lempeng batu. Slime King sekitar 2,1 m dengan mahkota.
- Arah hadap sumber di Blender adalah -Y, diekspor FBX (forward -Z, up Y) supaya di Unity menghadap +Z.
- Pivot di telapak kaki. Gerak animasi in-place; perpindahan dunia diatur script.
- Kamera gameplay: offset (0, 8.3, -6.9) dari hero, FOV 50, sedikit mengarah ke arah gerak (`FollowCamera.leadDistance`), goyang pendek saat hero terkena damage.
- Arena bermain r = 11,2 m dibatasi cincin rune emisif dan 18 batu batas; di luar itu tanah naik menjadi bukit berpohon sampai 40 m supaya tepi mesh tidak pernah terlihat.

## Material

- URP Lit. Tanah, kulit kayu, dan batu memakai tekstur 1024/512 px + normal map prosedural.
- Slime, permata, iris, kristal, orb XP, dan mesh skill memakai emisi supaya tetap terbaca; musuh yang melambat berpendar biru, yang terbakar berpendar oranye (MaterialPropertyBlock).
- Emas metallic 0,75. Partikel dan trail memakai material aditif tanpa tulis depth.
- Satu-satunya transparansi alpha adalah sinar tipis di atas pickup power-up.

## Rig dan animasi

- Rig generic buatan sendiri, bukan Humanoid Mecanim.
- Hero: Idle (napas, rambut dan cape bergoyang), Run (langkah dan ayun tongkat), Attack (dorongan tongkat ke depan, cast), Dash, Hit, Death.
- Slime dan boss: Idle, Move, Hit, Death. Boss menambah Telegraph dan Slam.
- Semua state satu kali (Hit, Dash, Attack, Telegraph, Slam) kembali ke Idle lewat exit time sehingga animator tidak pernah membeku.
- Deformasi slime memakai skala tulang, bukan blendshape.
