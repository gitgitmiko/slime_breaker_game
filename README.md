# Slime Breaker

Game action roguelite 3D untuk Android, 60 fps. Seorang penyihir ungu chibi melawan gerombolan slime selama 100 lantai. Setiap lantai cuaca dan siang/malam berganti; setiap 10 lantai biome map berganti (hutan, gurun, salju, petir, rawa, bayangan, magma, badai, kristal, kerajaan). Level hero maksimum 999; tiap naik level serangan dasar +10. Panel status HUD menampilkan DMG, CRIT, CRIT RATE, ATK SPD, RANGE, MOVE, EXP bonus, LS, dan DEF, plus progres bunuh slime per lantai (lantai N = N×10). Tiap 20 level hero ada power-up (skill maks Lv 10, tanda MAX); lifesteal tampil sebagai angka hijau di atas hero; joystick Fixed/Free di Pengaturan (default Fixed); tekan tahan slot skill untuk kartu deskripsi; HUD dan menu bergaya kaca seragam (bar HP/EXP/lantai, dash, joystick, jeda, pengaturan, menu utama); bar HP boss di atas layar; tiap 10 lantai muncul slime boss unik (Rock Slime … Royal Slime) dengan HP 10.000 × nomor bos, jubah merah + mahkota emas, skill elemental, damage yang naik tiap lantai, minion tiap 20 detik, cutscene zoom saat enrage, kebal skill power-up (hanya basic attack), dan drop Boss Breaker dari slime emas yang membuat power-up melukai boss 60 detik. Critical hanya aktif setelah upgrade Crit Rate atau Critical. Kuota lantai membuka pintu; panah menunjuk pintu dan power-up. Bolt dasar hanya tembak saat hero diam. Batu dan pohon memblokir gerak dan serangan. Progres run bisa dilanjutkan. Offline, tanpa akun.

## Isi

- `PRD_ASSET_PIPELINE.md` dan `PRD_GAME_DEVELOPMENT.md` — spesifikasi.
- `asset_work/scripts/` — generator tekstur (`build_textures.py`), mesh + animasi Blender (`build_assets.py`), ikon/panel/SFX/musik (`build_audio_ui.py`).
- `asset_work/output/`, `asset_work/textures/` — hasil generator; `ProjectSetup.BuildAll` menyalinnya ke `Assets/Art` dan `Assets/Audio`.
- `Assets/Scripts` — gameplay (`Skills/` berisi sistem power-up). Scene, prefab, dan ScriptableObject dibuat oleh `ProjectSetup.BuildAll`.
- `Assets/Tests/PlayMode` — playtest otomatis.
- `docs/` — arah seni, pipeline, katalog, desain, dan build Android.

## Membuat ulang aset

```bash
python3 asset_work/scripts/build_textures.py asset_work/textures
blender --background --python asset_work/scripts/build_assets.py -- \
  asset_work/output asset_work/previews asset_work/textures
python3 asset_work/scripts/build_audio_ui.py
```

## Membuka proyek

Unity 6000.3.26f1. Render pipeline: URP 17.0.1. APK ARM64 ada di `Builds/SlimeBreaker.apk` setelah build. Langkah setup, test, build, dan pasang di HP ada di `docs/build_android.md`.
