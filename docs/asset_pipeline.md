# Pipeline aset — perintah yang sudah dijalankan

Audit pada mesin ini, 14 Oktober 2026. Tidak ada CLI generasi mesh AI (Meshy, Tripo, Hunyuan, ComfyUI, Stable Diffusion) yang terpasang. Aset 3D dibuat dengan Blender. Gambar dengan ImageMagick. Suara dengan Python, FluidSynth, dan oggenc.

## Versi tool

Semua tool ada di `~/game-toolchain` (tanpa sudo; PATH diatur `~/game-toolchain/env.sh`). Cek semuanya sekaligus dengan `bash asset_work/scripts/check_toolchain.sh` (exit 0 = lengkap). Pasang ulang: `install-portable.sh` (inti) lalu `install-extra.sh` (tambahan, 16 Oktober 2026). Mesin ini **tanpa GPU**. Compiler (`build-essential`, `cmake`, `bzip2`, `patchelf`, `pkg-config`, `libfuse2t64`, `python3-dev`) dipasang dengan sudo pada 16 Oktober 2026, jadi paket yang butuh build (mis. `noise`, `basisu`) sekarang bisa dipasang.

### 3D dan mesh

| Tool | Versi | Fungsi |
|---|---|---|
| Blender | 5.2.2 LTS | Model, rig, animasi, ekspor FBX/glTF, render EEVEE headless |
| assimp | 5.3 | Inspeksi/konversi FBX, glTF, OBJ (`assimp info <file>`) |
| gltf-transform | 4.5.1 | Optimasi glTF (simplify, weld, prune, kompres) |
| gltf-pipeline | 4.3.1 | Konversi + kompresi Draco |
| gltfpack | 1.3 | Simplify/quantize mesh (meshoptimizer) |
| obj2gltf | npm | OBJ → glTF |
| fbx2gltf | 0.9.7 | FBX → glTF/GLB |
| gltf-validator | 2.0 | Validasi glTF (`gltf-validator model.glb`, exit 1 jika ada error) |
| MeshLab | 2025.07 | GUI mesh; untuk skrip pakai `pymeshlab` |
| pymeshlab, trimesh, xatlas, pygltflib, shapely | Python | Remesh/decimate, UV unwrap (`xatlas`), baca-tulis mesh, poligon 2D |

### Tekstur, gambar, UI

| Tool | Versi | Fungsi |
|---|---|---|
| ImageMagick (`magick`) | 7.1.2-33 | Konversi, atlas, filter |
| Inkscape | 1.4.2 | Vektor SVG → PNG |
| Krita | 6.0.4.1 | Lukis tekstur/sprite; ekspor CLI `xvfb-run -a krita --export --export-filename out.png in.kra` |
| KTX-Software (`toktx`, `ktx`) | 4.4.2 | Tekstur KTX2 / Basis (ETC1S, UASTC) |
| basisu | v2.50 (dibangun dari sumber) | Encoder Basis Universal: `basisu -ktx2 -uastc in.png` |
| oxipng, pngquant, optipng, zopflipng | 10.2.1 / 2.18 / 0.7.8 / zopfli | Perkecil PNG (lossless dan 8-bit) |
| cwebp, dwebp, gif2webp, img2webp, gifsicle | libwebp 1.5.0 / 1.94 | WebP dan GIF |
| potrace | 1.16 | Bitmap → SVG |
| Pillow, numpy, scipy, scikit-image, opencv, opensimplex, noise, cairosvg, imageio | Python | Gambar prosedural, noise, filter, SVG → PNG |

### Audio dan musik

| Tool | Versi | Fungsi |
|---|---|---|
| FluidSynth + soundfont FluidR3_GM, default-GM | 2.3.4 | Render MIDI → audio |
| LMMS | 1.2.2 | DAW; render CLI `xvfb-run -a lmms render proyek.mmpz -f wav -o out.wav` |
| SoX | 14.4.2 | Efek, mixing, trim |
| ffmpeg / ffprobe | 7.0.2 | Konversi audio/video, rekam klip |
| oggenc, opusenc, lame, flac | 1.4.2 / 0.2 / 3.100 / 1.4.3 | Encode OGG, Opus, MP3, FLAC |
| rubberband | 3.3.0 | Ubah tempo/pitch tanpa mengubah satunya |
| espeak-ng | 1.51 | Suara sintetis (placeholder voice) |
| abc2midi | 4.88 | Notasi ABC → MIDI |
| MIDIUtil, mido, pretty_midi, music21 | Python | Tulis/analisis MIDI dan teori musik |
| librosa, soundfile, pedalboard, pyloudnorm, pydub, pyfluidsynth | Python | Analisis audio, efek (reverb/compressor), normalisasi loudness, FluidSynth dari Python |

### Build dan umum

| Tool | Versi | Perintah cek |
|---|---|---|
| Unity Editor | 6000.3.26f1 | `~/Unity/Hub/Editor/6000.3.26f1/Editor/Unity` |
| Unity CLI | 1.0.0-beta.13 | `unity --version` |
| Node.js / npm | 24.21.0 / 11.19.0 | `node --version` |
| gcc / g++ / make / cmake / ninja | 13.3 / 13.3 / 4.3 / 4.4 / 1.13 | `gcc --version` (dipasang dengan sudo) |
| 7-Zip, xvfb-run, jq | 23.01 / sistem / 1.7 | `7z i` |

Tidak dipasang: generator AI (Meshy, Tripo, ComfyUI, Stable Diffusion, Ollama; mesin tanpa GPU), GIMP (Krita sudah ada), Aseprite (butuh build Skia besar; game ini 3D), Godot, Docker, Instant Meshes (retopologi sudah ada di `pymeshlab` dan Blender QuadriFlow).

Catatan pemakaian:
- AppImage (Krita, LMMS) diekstrak ke `~/game-toolchain/tools` supaya tidak bergantung pada FUSE; keduanya butuh display, jadi jalankan lewat `xvfb-run -a`.
- `bzip2` sekarang terpasang; skrip lama tetap memakai `7z` untuk arsip `.bz2` supaya jalan di mesin tanpa sudo.
- Paket apt dipasang tanpa root: `apt-get download <paket>` lalu `dpkg-deb -x <deb> ~/game-toolchain/syslibs`.

Ekspor FBX diverifikasi lewat `bpy.ops.export_scene.fbx` (ada) dan `bpy.ops.export_scene.gltf` (ada, tidak dipakai). Unity mengimpor FBX bawaan, jadi FBX yang dipilih.

## Perintah produksi

Dari root repositori, urutannya tekstur → mesh → audio/UI → Unity:

```bash
python3 asset_work/scripts/build_textures.py asset_work/textures

blender --background --python asset_work/scripts/build_assets.py -- \
  asset_work/output asset_work/previews asset_work/textures

python3 asset_work/scripts/build_pbr_textures.py asset_work/textures

blender --background --python asset_work/scripts/build_props_hd.py -- \
  asset_work/output asset_work/previews asset_work/textures

python3 asset_work/scripts/build_audio_ui.py
python3 asset_work/scripts/build_audio_hd.py all
```

### Lingkungan HD (putaran 16)

- `build_pbr_textures.py <out> [nama…]` memakai `pbr.py` (Perlin fbm tileable, Worley, normal + AO dari height). Tiap set: albedo (A = height), `_Normal`, `_Mask` (R AO, G smoothness, B metallic), opsional `_Emit`. Tanah Forest/Snow/Sand 2048, bioma lain 1024; properti (`Bark_HD`, `Rock_HD`, `Stone_Ruin`, `Marble_HD`, `Obsidian`, `Cactus_Skin`, `Mushroom_Cap`, `Ice_HD`), penutup bioma (`Cover_Moss/Snow/Sand/Ash`), atlas daun/rumput beralpha, `TEX_Noise_Macro`, dan sprite cuaca `TEX_FX_*`. Pratinjau ber-lighting ditulis ke `asset_work/previews/textures`.
- `build_props_hd.py` (Blender) membangun ulang 21 properti dengan id lama (pohon daun lebar dan konifer berkartu daun, semak, rumput/bunga, batu displacement, reruntuhan, kaktus, es, kristal, kolom kerajaan, dll). Material `MAT_hd_<permukaan>`; warna verteks R = AO, A = bobot angin. Argumen id terakhir opsional untuk membangun sebagian.
- Shader URP: `SlimeBreaker/Ground` (world-space, sampel kedua diputar 37° anti-tiling, noise makro, emisi berdenyut), `SlimeBreaker/Lit` (alpha clip, angin, translusensi daun, AO verteks, penutup bioma global `_SB_Cover*`), `SlimeBreaker/Jelly` (subsurface wrap, rim fresnel, inti padat, noise mengalir). Keyword global `SB_QUALITY_LOW` memilih jalur murah.
- `PostFx` membuat Volume runtime (ACES, bloom, color adjustments, white balance, split toning, vignette) dengan grading per bioma/malam/cuaca; mati di preset Low.

### Dinding steamer dimsum (putaran 26)

Lantai, properti, dan cuaca arena tetap seperti sebelumnya; hanya tepi arena yang kini berupa dinding wadah dimsum (steamer bambu). Perintah:

```bash
python3 asset_work/scripts/build_pbr_textures.py asset_work/textures TEX_Bamboo_Weave TEX_Steamer_Wood
blender --background --python asset_work/scripts/build_arena_dimsum.py -- asset_work/output asset_work/previews asset_work/textures
```

- Tanpa argumen id, skrip hanya membangun `ENV_Dimsum_Wall` (cincin r 23,0–24,9, tinggi 2,5 m, dasar ditanam 1 m ke tanah): sisi dalam dan luar dilapisi anyaman bambu (`TEX_Bamboo_Weave`, material `MAT_hd_weave`), bibir kayu pucat membulat (`TEX_Steamer_Wood`), dua gelang bambu dan 16 simpul di luar.
- `build_arena_dimsum.py ... all` juga membangun set properti dimsum (dumpling mini jelly berwajah, bambu, pak choi, sumpit, dll.) dan dekor meja; set itu belum dipakai di game (disiapkan untuk tahap boss dumpling).
- `ProjectSetup` memasang dinding di scene arena dan menu; terrain digandakan 1,6× agar tepinya tak terlihat dari balik dinding.

### Audio HD (putaran 16)

`build_audio_hd.py [music|ambience|sfx|all] [nama…]`: komposer prosedural (pretty_midi, progresi akor, voice leading, melodi di-snap ke nada akor) → FluidSynth kering (`-R 0 -C 0`) dengan soundfont GeneralUser GS (`~/game-toolchain/soundfonts/GeneralUser-GS.sf2`) → pedalboard (HP, shelf, kompresor, reverb, tail di-wrap agar loop mulus) → pyloudnorm → soft-knee peak (tanpa make-up gain, puncak ≤ 0,97) → `oggenc`. Hasil: 12 musik (`MUS_Menu`, 10 bioma, `MUS_Boss`, −15 s.d. −19 LUFS), 11 ambience loop 34 s (`AMB_<Bioma>`, `AMB_Night`, sekitar −25 LUFS), 23 SFX/jingle. Tidak ada sampel yang clipping.

Hanya sprite UI dan ikon (audio tidak disentuh): `python3 asset_work/scripts/build_audio_ui.py ui`. Sprite HUD modern: `UI_Card_Glass`, `UI_Pill`, `UI_Bar_Track`, `UI_Bar_Fill` (putih, diwarnai di Unity), `UI_Button_Ghost`, `UI_Circle_Glass`, `UI_Ring`, ikon `UI_Icon_Gear`, `UI_Icon_Pause`, `UI_Icon_Arrow`, dan `UI_Icon_Check`. `UI_Panel`, `UI_Button*`, `UI_Slot`, dan joystick memakai gaya kaca yang sama.

`build_textures.py` membuat `TEX_Forest_Ground` (1024), `TEX_Bark` / `TEX_Rock` (512), plus tanah biome (`TEX_Snow_Ground`, `TEX_Sand_Ground`, `TEX_Thunder_Ground`, `TEX_Swamp_Ground`, `TEX_Shadow_Ground`, `TEX_Volcano_Ground`, `TEX_Storm_Ground`, `TEX_Crystal_Ground`, `TEX_Royal_Ground`) beserta `*_Normal.png` dari value noise tileable (numpy + Pillow). Blender menanamkan tekstur warna di FBX; normal map disalin ke `Assets/Art/Textures` dan dipasang `ProjectSetup` berdasarkan nama material.

Hanya hero (tanpa menyentuh slime, lingkungan, atau tekstur lain):

```bash
blender --background --python asset_work/scripts/build_assets.py -- \
  asset_work/output asset_work/previews asset_work/textures hero
```

Mode ini membangun hero penyihir chibi (tekstur kain `TEX_Hero_Hat/Jacket/Capelet/Hair.png` dilukis numpy; mata = dua elipsoid hitam) lalu menyalin FBX ke `Assets/Art/Characters/Hero`, dan merender pratinjau hero dengan EEVEE (lampu key/fill/rim): `CHR_Hero_Main.png`, `_Face.png`, `_Back.png`, dan frame tiap klip. Set `HERO_DEBUG_VIEWS=1` untuk tambahan tampak depan/samping per frame di `asset_work/previews/debug/` (tidak di-commit). Rambut dibuat dari metaball (radius = radius permukaan; dikali `METABALL_REACH`) lalu di-decimate; mata adalah decal berbentuk mata yang diproyeksikan ke wajah dengan UV mengikuti outline. Pose lengan/tongkat yang miring memakai `aim()` (arah dunia) agar tidak menyilang badan.

Hanya mesh biome baru (tanpa menimpa hero/slime):

```bash
blender --background --python asset_work/scripts/build_assets.py -- \
  asset_work/output asset_work/previews asset_work/textures env
```

`build_assets.py` juga merender preview dan lembar frame animasi hero (`asset_work/previews/CHR_Hero_Main_<Clip>_<persen>.png`) untuk pemeriksaan visual tanpa editor.

`build_audio_ui.py` kini default hanya menulis UI; musik/SFX lama hanya dengan argumen `legacy-audio` (digantikan `build_audio_hd.py`).

Efek suara disintesis numpy 44,1 kHz (osilator, noise berfilter, envelope) ke WAV, lalu `oggenc -q 4`. Ikon dan panel UI dirender Pillow dengan supersampling 4×.

`ProjectSetup.BuildAll` menyalin `asset_work/output` dan `asset_work/textures` ke `Assets/` (mirror: file lama yang tidak ada lagi di output dihapus), jadi jangan menyunting file di `Assets/Art` atau `Assets/Audio` langsung.

## Aset Google Play

`render_store_art.py` (Blender, hanya mengimpor FBX final) merender hero/boss/slime transparan ke `asset_work/store/renders`; `build_store_art.py` (Pillow, font DejaVu Sans Bold) menyusun ikon peluncur `asset_work/output/UI/AppIcon/APP_Icon*.png` (dipasang `ProjectSetup.ConfigureAppIcons`), `asset_work/store/icon_512.png`, dan `feature_graphic_1024x500.png`. Lihat `docs/play_store.md`.

## Konvensi ekspor

- Satuan meter. Karakter menghadap -Y di Blender.
- FBX: `axis_forward='-Z'`, `axis_up='Y'`, `apply_scale_options='FBX_SCALE_UNITS'`.
- Di Unity ini menjadi sumbu Y ke atas dan hadap +Z.
- Rig generic, animasi in-place. Bukan Humanoid.
- Klip hero: Idle, Run, Attack, Dash, Hit, Death, Cast, Victory, Menu (Menu loop, dipakai `CHR_Hero_Menu.controller` di menu utama).
- Klip slime: Idle, Move, Hit, Death.
- Klip boss menambah Telegraph dan Slam.
- Nama clip di FBX berbentuk `NamaArmature|Idle`. Skrip editor memendekkannya saat impor.

## Integrasi Unity

Skrip `SlimeBreaker.EditorTools.ProjectSetup.BuildAll` menyalin aset, mengatur importer (sprite, normal map, klip animasi), mengubah material ke URP Lit dengan tabel look, membuat Animator Controller, prefab (hero, musuh, boss, proyektil, skill, power-up, VFX), ScriptableObject (musuh, upgrade, skill, balance), scene Boot/MainMenu/GameArena, dan menyetel URP Mobile (render scale 1, MSAA 2×, HDR aktif untuk post-processing, bayangan 28 m). Audio: musik `Streaming`, ambience `CompressedInMemory`, SFX `DecompressOnLoad` (Vorbis). Perintahnya ada di `docs/build_android.md`.

## Lisensi aset

- Mesh, tekstur, ikon, dan melodi dibuat sendiri di mesin ini.
- Soundfont GeneralUser GS (S. Christian Collins) mengizinkan pemakaian komersial untuk musik yang dirender; dipakai `build_audio_hd.py`, tidak dimasukkan ke repositori.
- Soundfont `/usr/share/sounds/sf2/FluidR3_GM.sf2` (paket `fluid-soundfont-gm`) berlisensi MIT dan hanya dipakai untuk merender MIDI orisinal. File soundfont tidak dimasukkan ke repositori.
- Tidak ada aset pihak ketiga yang disalin.
