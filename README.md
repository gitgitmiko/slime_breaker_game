# Slime Breaker

Game action roguelite 3D untuk Android, 60 fps. Seorang penyihir anime melawan gerombolan slime selama 100 lantai. Setiap lantai cuaca dan siang/malam berganti; setiap 10 lantai biome map berganti (hutan, gurun, salju, petir, rawa, bayangan, magma, badai, kristal, kerajaan). Level hero maksimum 999; panel status HUD menampilkan DMG, CRIT, CRIT RATE, ATK SPD, MOVE, dan LS. Tiap 5 level hero ada power-up; tiap 10 lantai muncul slime boss unik (Rock Slime … Royal Slime) dengan HP 10.000 × nomor bos, jubah merah + mahkota emas, skill elemental (basic 50 / skill 100 / enrage 200), dan minion tiap 20 detik. Critical hanya aktif setelah upgrade Crit Rate atau Critical. Kuota lantai membuka pintu; panah menunjuk pintu dan power-up. Bolt tetap mengenai musuh saat hero berjalan. Batu dan pohon memblokir gerak dan serangan. Progres run bisa dilanjutkan. Offline, tanpa akun.

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
