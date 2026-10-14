# Slime Breaker

Game action roguelite 3D untuk Android. Satu hero chibi, arena hutan, tiga slime, dan satu boss. Offline, tanpa akun.

## Isi

- `PRD_ASSET_PIPELINE.md` dan `PRD_GAME_DEVELOPMENT.md` — spesifikasi.
- `asset_work/scripts/` — generator aset Blender, ikon, dan audio.
- `Assets/Art`, `Assets/Audio` — hasil yang dipakai Unity.
- `Assets/Scripts` — gameplay. Scene, prefab, dan ScriptableObject dibuat oleh `ProjectSetup.BuildAll` saat editor bisa jalan.
- `docs/` — arah seni, pipeline, katalog, desain, dan build Android.

## Membuat ulang aset

```bash
blender --background --python asset_work/scripts/build_assets.py -- \
  asset_work/output asset_work/previews asset_work/textures
python3 asset_work/scripts/build_audio_ui.py
```

## Membuka proyek

Unity 6000.3.26f1. Render pipeline: URP 17.0.1. APK ARM64 ada di `Builds/SlimeBreaker.apk`. Langkah setup, build, dan pasang di HP ada di `docs/build_android.md`.
