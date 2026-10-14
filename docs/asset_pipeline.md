# Pipeline aset — perintah yang sudah dijalankan

Audit pada mesin ini, 14 Oktober 2026. Tidak ada CLI generasi mesh AI (Meshy, Tripo, Hunyuan, ComfyUI, Stable Diffusion) yang terpasang. Aset 3D dibuat dengan Blender. Gambar dengan ImageMagick. Suara dengan Python, FluidSynth, dan oggenc.

## Versi tool

| Tool | Versi | Perintah cek |
|---|---|---|
| Blender | 5.2.2 LTS | `blender --version` |
| ImageMagick | 7.1.2-33 | `magick -version` |
| FluidSynth | 2.3.4 | `fluidsynth --version` |
| oggenc | vorbis-tools 1.4.2 | `oggenc --version` |
| assimp | terpasang di PATH | `assimp info <file.fbx>` |
| Unity Editor | 6000.3.26f1 | `~/Unity/Hub/Editor/6000.3.26f1/Editor/Unity` |
| Unity CLI | 1.0.0-beta.13 | `unity --version` |

Ekspor FBX diverifikasi lewat `bpy.ops.export_scene.fbx` (ada) dan `bpy.ops.export_scene.gltf` (ada, tidak dipakai). Unity mengimpor FBX bawaan, jadi FBX yang dipilih.

## Perintah produksi

Dari root repositori, urutannya tekstur → mesh → audio/UI → Unity:

```bash
python3 asset_work/scripts/build_textures.py asset_work/textures

blender --background --python asset_work/scripts/build_assets.py -- \
  asset_work/output asset_work/previews asset_work/textures

python3 asset_work/scripts/build_audio_ui.py
```

`build_textures.py` membuat `TEX_Forest_Ground` (1024) dan `TEX_Bark`, `TEX_Rock` (512) beserta `*_Normal.png` dari value noise tileable (numpy + Pillow). Blender menanamkan tekstur warna di FBX; normal map disalin ke `Assets/Art/Textures` dan dipasang `ProjectSetup` berdasarkan nama material.

`build_assets.py` juga merender preview dan lembar frame animasi hero (`asset_work/previews/CHR_Hero_Main_<Clip>_<persen>.png`) untuk pemeriksaan visual tanpa editor.

Musik ditulis sebagai MIDI oleh `build_audio_ui.py` lalu:

```bash
fluidsynth -ni -g 0.8 -F /tmp/MUS_Arena.wav -r 44100 \
  /usr/share/sounds/sf2/FluidR3_GM.sf2 /tmp/MUS_Arena.mid
oggenc -q 5 /tmp/MUS_Arena.wav -o asset_work/output/Audio/Music/MUS_Arena.ogg
```

Efek suara disintesis numpy 44,1 kHz (osilator, noise berfilter, envelope) ke WAV, lalu `oggenc -q 4`. Ikon dan panel UI dirender Pillow dengan supersampling 4×.

`ProjectSetup.BuildAll` menyalin `asset_work/output` dan `asset_work/textures` ke `Assets/` (mirror: file lama yang tidak ada lagi di output dihapus), jadi jangan menyunting file di `Assets/Art` atau `Assets/Audio` langsung.

## Konvensi ekspor

- Satuan meter. Karakter menghadap -Y di Blender.
- FBX: `axis_forward='-Z'`, `axis_up='Y'`, `apply_scale_options='FBX_SCALE_UNITS'`.
- Di Unity ini menjadi sumbu Y ke atas dan hadap +Z.
- Rig generic, animasi in-place. Bukan Humanoid.
- Klip hero: Idle, Run, Attack, Dash, Hit, Death.
- Klip slime: Idle, Move, Hit, Death.
- Klip boss menambah Telegraph dan Slam.
- Nama clip di FBX berbentuk `NamaArmature|Idle`. Skrip editor memendekkannya saat impor.

## Integrasi Unity

Skrip `SlimeBreaker.EditorTools.ProjectSetup.BuildAll` menyalin aset, mengatur importer (sprite, normal map, klip animasi), mengubah material ke URP Lit dengan tabel look, membuat Animator Controller, prefab (hero, musuh, boss, proyektil, skill, power-up, VFX), ScriptableObject (musuh, upgrade, skill, balance), scene Boot/MainMenu/GameArena, dan menyetel URP Mobile (render scale 1, MSAA 2×, HDR mati, bayangan 28 m). Perintahnya ada di `docs/build_android.md`.

## Lisensi aset

- Mesh, tekstur, ikon, dan melodi dibuat sendiri di mesin ini.
- Soundfont `/usr/share/sounds/sf2/FluidR3_GM.sf2` (paket `fluid-soundfont-gm`) berlisensi MIT dan hanya dipakai untuk merender MIDI orisinal. File soundfont tidak dimasukkan ke repositori.
- Tidak ada aset pihak ketiga yang disalin.
