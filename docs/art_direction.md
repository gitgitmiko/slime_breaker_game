# Art Direction — Slime Breaker

Gaya: anime 3D chibi fantasy, dibaca jelas dari kamera third-person agak tinggi.

Semua aset di bawah ini dibuat sendiri di mesin ini (Blender, ImageMagick, Python). Tidak ada karakter, logo, atau desain berhak cipta yang disalin.

## Palet

| Peran | Warna | Hex perkiraan |
|---|---|---|
| Kulit hero | peach hangat | `#F5C6A8` |
| Rambut hero | karang | `#E65938` |
| Jubah / aksen hero | teal | `#1F8C9E` |
| Emas | kuning hangat | `#F2C14E` |
| Slime biasa | mint | `#38D17A` |
| Slime cepat | cyan | `#26C7FF` |
| Slime tank | ungu batu | `#6B4C8C` |
| Slime King | magenta | `#D12E6B` |
| Tanah | lumut | `#3D7A3A` |
| Kayu | coklat | `#614028` |
| Daun | hijau daun | `#2E8C47` |
| Jamur | merah lembut | `#DB594F` |
| Batu | abu biru | `#737A84` |

## Proporsi dan kamera

- Hero chibi, tinggi sekitar 1,25 m, kepala besar, senjata tongkat pendek di tangan kanan.
- Slime biasa sekitar 0,9 m. Varian cepat lebih kecil dan pipih. Varian tank lebih besar dengan lempeng batu. Slime King jelas lebih besar dan memakai mahkota.
- Arah hadap sumber di Blender adalah -Y, diekspor FBX (forward -Z, up Y) supaya di Unity menghadap +Z.
- Pivot di telapak kaki. Gerak animasi in-place; perpindahan dunia diatur script.
- Kamera gameplay kira-kira di belakang dan di atas pemain, melihat ke bawah sedikit, supaya siluet slime dan telegraph boss tetap terbaca.

## Material

- Material opak, roughness sedang, tanpa transparansi (lebih aman untuk Android kelas menengah).
- Slime, permata, dan orb XP memakai emisi ringan supaya tetap terbaca, bukan shader toon terpisah.
- Satu tekstur tanah 512 px. Selebihnya warna material polos.

## Rig

- Rig generic buatan sendiri, bukan Humanoid Mecanim, karena proporsi chibi tidak cocok dengan skeleton manusia standar.
- Hero: idle, run, attack, dash, hit, death.
- Slime dan boss: idle, move, hit, death. Boss menambah telegraph dan slam.
- Deformasi slime memakai skala tulang, bukan blendshape.
