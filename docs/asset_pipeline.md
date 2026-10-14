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

Dari root repositori:

```bash
blender --background --python asset_work/scripts/build_assets.py -- \
  asset_work/output asset_work/previews asset_work/textures

python3 asset_work/scripts/build_audio_ui.py
```

`build_assets.py` sendiri memanggil:

```bash
magick -size 512x512 plasma:#2f6b32-#c4d98a -blur 0x1.2 -modulate 95,55,98 \
  asset_work/textures/TEX_Forest_Ground.png
```

Musik:

```bash
fluidsynth -ni -F asset_work/intermediate/MUS_Arena.wav -r 22050 \
  /usr/share/sounds/sf2/FluidR3_GM.sf2 asset_work/intermediate/MUS_Arena.mid
oggenc -Q asset_work/intermediate/MUS_Arena.wav -o Assets/Audio/Music/MUS_Arena.ogg
```

Efek suara ditulis sebagai WAV oleh `build_audio_ui.py`, lalu `oggenc -Q`.

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

Skrip `SlimeBreaker.EditorTools.ProjectSetup.BuildAll` mengubah material ke URP Lit, membuat Animator Controller, prefab, scene, dan VFX partikel. Perintahnya ada di `docs/build_android.md`. Editor belum bisa menjalankannya karena lisensi Unity Personal belum aktif.

## Lisensi aset

- Mesh, tekstur, ikon, dan melodi dibuat sendiri di mesin ini.
- Soundfont `/usr/share/sounds/sf2/FluidR3_GM.sf2` (paket `fluid-soundfont-gm`) berlisensi MIT dan hanya dipakai untuk merender MIDI orisinal. File soundfont tidak dimasukkan ke repositori.
- Tidak ada aset pihak ketiga yang disalin.
