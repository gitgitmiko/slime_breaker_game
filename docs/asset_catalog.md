# Katalog aset MVP

Sumber geometri: Blender 5.2.2, skrip `asset_work/scripts/build_assets.py`. Angka poligon dihitung di Blender sebelum ekspor. File yang dipakai game ada di `Assets/`.

| ID | File | Jenis | Poligon | Rig / klip | Status |
|---|---|---|---|---|---|
| A01 CHR_Hero_Main | Assets/Art/Characters/Hero/CHR_Hero_Main.fbx | hero chibi | 2391 | generic; Idle Run Attack Dash Hit Death | dibuat, belum diimpor editor |
| A02 ENM_Slime_Normal | Assets/Art/Enemies/Slime/ENM_Slime_Normal.fbx | slime | 336 | generic; Idle Move Hit Death | dibuat |
| A03 ENM_Slime_Fast | Assets/Art/Enemies/Slime/ENM_Slime_Fast.fbx | varian kecil + sirip | 354 | klip yang sama | dibuat |
| A04 ENM_Slime_Tank | Assets/Art/Enemies/Slime/ENM_Slime_Tank.fbx | varian besar + batu | 354 | klip yang sama | dibuat |
| A05 BOSS_SlimeKing | Assets/Art/Enemies/SlimeKing/BOSS_SlimeKing.fbx | boss + mahkota | 747 | ditambah Telegraph, Slam | dibuat |
| A06 arena | Assets/Art/Environment/ForestArena/ | tanah, 2 pohon, 2 batu, jamur, semak, pilar | 16–400 tiap prop | statis | dibuat |
| A07 VFX | Assets/Art/VFX/VFX_Boss_Ring.fbx, VFX_XP_Orb.fbx | cincin telegraph, orb | 224 dan 20 | statis | mesh dibuat; partikel hit/dash/level/slam dibuat skrip editor |
| A08 ikon | Assets/Art/UI/Icons/ | 8 ikon upgrade 256 px | — | — | dibuat ImageMagick |
| A09 UI | Assets/Art/UI/Panels/ | panel dan dua tombol | — | — | dibuat ImageMagick |
| A10 audio | Assets/Audio/SFX, Assets/Audio/Music/MUS_Arena.ogg | 11 SFX + 1 musik | — | — | disintesis, bukan rekaman pihak ketiga |

Preview workbench (tanpa warna material) ada di `asset_work/previews/`. Material berwarna ikut di dalam FBX dan dikonversi ke URP Lit oleh `ProjectSetup`.

Collider gameplay tidak memakai mesh. Pemain memakai CharacterController. Musuh memakai jarak sentuh. Tanah memakai BoxCollider yang dipasang skrip editor.
