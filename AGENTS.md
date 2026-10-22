# Catatan untuk agent

- Jangan menebak executable. Tool yang terbukti ada dicatat di `docs/asset_pipeline.md`.
- Jangan menimpa FBX final tanpa menjalankan ulang `asset_work/scripts/build_assets.py` atau menyimpan salinan.
- Scene dan prefab gameplay dihasilkan `Assets/Scripts/Editor/ProjectSetup.cs`, bukan ditulis tangan sebagai YAML.
- Jangan mengklaim APK atau compile editor berhasil sebelum log Unity memuat `SLIMEBREAKER_SETUP_OK` dan, untuk APK, file keluarannya ada.
- Lisensi Unity Personal butuh `unity auth login` sebelum batch mode.
- `Library/`, `Logs/`, `Temp/`, dan `UserSettings/` tidak di-commit.
- Upload key Play ada di `~/.slimebreaker/` (di luar repo). Jangan commit keystore/kata sandi; `BuildAndroidAab` mengosongkannya lagi dari `ProjectSettings.asset`.
