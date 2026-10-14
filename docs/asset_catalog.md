# Katalog aset

Sumber geometri: Blender 5.2.2, skrip `asset_work/scripts/build_assets.py`. Tekstur: `asset_work/scripts/build_textures.py` (numpy + Pillow). Audio dan UI: `asset_work/scripts/build_audio_ui.py`. Angka poligon dihitung di Blender sebelum ekspor dan tersimpan di `asset_work/output/catalog.json`. `ProjectSetup.BuildAll` menyalin `asset_work/output` ke `Assets/Art` dan `Assets/Audio`.

## Karakter

| ID | File | Jenis | Tris | Rig / klip |
|---|---|---|---|---|
| CHR_Hero_Main | Assets/Art/Characters/Hero/CHR_Hero_Main.fbx | penyihir anime: topi, jubah, cape dua lapis, twin tail, mata besar, tongkat kristal | 20914 | generic 24 tulang (Hair.L/R ×2, HatTip, Cape, Staff, StaffTip); Idle 48f, Run 20f, Attack 14f (cast), Dash 10f, Hit 10f, Death 30f |
| ENM_Slime_Normal | Assets/Art/Enemies/Slime/ENM_Slime_Normal.fbx | slime mint, wajah di permukaan | 3668 | Body/Top; Idle Move Hit Death |
| ENM_Slime_Fast | Assets/Art/Enemies/Slime/ENM_Slime_Fast.fbx | kecil, sirip kuning | 3722 | klip yang sama |
| ENM_Slime_Tank | Assets/Art/Enemies/Slime/ENM_Slime_Tank.fbx | besar, lempeng batu bertekstur | 3852 | klip yang sama |
| BOSS_SlimeKing | Assets/Art/Enemies/SlimeKing/BOSS_SlimeKing.fbx | boss magenta, mahkota, cape | 4584 | ditambah Telegraph, Slam |

## Arena

| ID | Tris | Catatan |
|---|---|---|
| ENV_Forest_Ground | 7200 | grid 80 m, tepi naik jadi bukit di r > 13 m; tekstur 1024 + normal map |
| ENV_Forest_Tree_A / B | 948 | kulit kayu bertekstur + normal map |
| ENV_Forest_Rock_A / B | 80 | tekstur batu + normal map |
| ENV_Forest_Mushroom | 916 | |
| ENV_Forest_Bush | 720 | |
| ENV_Forest_RuinPillar | 420 | |
| ENV_Forest_Grass | 70 | ditabur 54× di arena |
| ENV_Forest_Flower | 740 | ditabur 20× |
| ENV_Forest_BoundaryStone | 100 | 18 batu di r = 12 m menghadap pusat |
| ENV_Arena_RuneRing | 1536 | cincin rune emisif r ≈ 11,4 m, batas arena |

## VFX mesh

| ID | Tris | Dipakai oleh |
|---|---|---|
| VFX_Boss_Ring | 768 | telegraph boss |
| VFX_XP_Orb | 80 | `XPOrb` |
| VFX_Magic_Bolt | 20 | serangan dasar hero (`HomingProjectile`) |
| VFX_Ice_Shard | 20 | skill Cincin Es |
| VFX_Arcane_Missile | 20 | skill Misil Arkana |
| VFX_Fire_Ring | 960 | skill Nova Api (`RingPulse`) |
| VFX_PowerUp_Crystal | 660 | pickup power-up (`PowerUpPickup`) |

Partikel (hit, dash, levelup, slam, ice, zap, fire, burn, arcane, powerup, cast, death, pickup, playerhit) dibuat skrip editor dengan material aditif `MAT_Particle_Additive`.

## UI dan audio

- Ikon 256 px: 8 upgrade (`ICON_Upgrade_*`) dan 4 skill (`ICON_Skill_Ice`, `_Lightning`, `_Fire`, `_Arcane`).
- Panel: `UI_Panel`, `UI_Button`, `UI_Button_Danger`, `UI_Button_Gold`, `UI_Slot`, `UI_Bar_Back`, `UI_Joystick_Base`, `UI_Joystick_Knob`, `UI_Circle`.
- SFX 20 file (`Assets/Audio/SFX`): Cast, Hit, BoltHit, EnemyDeath, PlayerHit, Dash, Pickup, LevelUp, PowerUpSpawn, PowerUp, Ice, Freeze, Lightning, Fire, Arcane, BossWarn, BossSlam, UI, Victory, Defeat. Semua disintesis numpy.
- Musik: `MUS_Arena.ogg` (40 s loop) dan `MUS_Menu.ogg` (26 s loop), MIDI orisinal dirender FluidSynth.

Preview Blender ada di `asset_work/previews/` (termasuk lembar frame animasi hero). Material berwarna ikut di dalam FBX dan dikonversi ke URP Lit oleh `ProjectSetup` memakai tabel nama material (smoothness, metallic, emisi, normal map).

Collider gameplay tidak memakai mesh. Pemain memakai CharacterController. Musuh dan proyektil memakai jarak. Tanah memakai BoxCollider 80 m yang dipasang skrip editor.
