# PRD 1 --- Slime Breaker Game Development

**Versi:** 1.0\
**Platform:** Android\
**Engine:** Unity (gunakan versi yang sudah terpasang di server)\
**Genre:** 3D Anime Action Roguelite\
**Mode:** Single-player, offline\
**Dokumen pasangan:** `PRD_ASSET_PIPELINE.md`

------------------------------------------------------------------------

## 1. Tujuan Dokumen

Dokumen ini khusus mengatur pengembangan game: arsitektur Unity,
gameplay, input, UI, progression, penyimpanan, pengujian, build Android,
dan workflow AI coding agent.

Pembuatan aset 3D dibahas hanya dari sisi kebutuhan integrasi. Detail
produksi aset, prompt visual, format output, dan pipeline CLI diatur
oleh `PRD_ASSET_PIPELINE.md`.

## 2. Ringkasan Produk

Slime Breaker adalah game action roguelite 3D bergaya anime/chibi untuk
Android. Pemain mengendalikan satu hero di arena kecil, bergerak
menghindari monster, menyerang musuh terdekat secara otomatis,
mengumpulkan XP, dan memilih upgrade acak saat naik level. Di akhir run,
pemain menghadapi boss dan menerima hadiah untuk upgrade permanen.

Target pengalaman: - Mudah dipahami dalam kurang dari satu menit. - Satu
run berlangsung sekitar 5--8 menit. - Kontrol layar sentuh nyaman. -
Gameplay inti dapat dimainkan offline. - Scope cukup kecil untuk
dikembangkan solo dengan bantuan AI coding agent.

## 3. Prinsip Desain

1.  **Playable first:** buat gameplay yang bisa dimainkan sebelum
    memoles visual.
2.  **Small scope:** satu hero, satu arena, tiga varian slime, satu boss
    untuk MVP.
3.  **Modular:** sistem gameplay terpisah dan mudah diuji.
4.  **Configurable:** statistik dan balancing tidak tersebar di banyak
    script.
5.  **Offline-first:** tidak membutuhkan backend atau akun.
6.  **Verified automation:** agent wajib melaporkan pengujian yang
    benar-benar dilakukan.

## 4. Tujuan dan Non-Tujuan

### Tujuan MVP

-   Project Unity valid dan dapat dibuild untuk Android.
-   Satu run lengkap dari menu sampai hasil.
-   Movement, dash, auto-attack, enemy AI, XP, level-up, upgrade, boss,
    victory/defeat.
-   Save lokal untuk gold dan upgrade permanen.
-   UI dan kontrol Android yang layak.
-   Workflow build dan pengujian terdokumentasi.

### Bukan tujuan MVP

-   Multiplayer, PvP, login, cloud save, backend, leaderboard online.
-   Gacha, iklan, pembelian dalam aplikasi.
-   Open world atau procedural world kompleks.
-   Crafting, inventory, quest, dan dialog panjang.
-   Banyak hero atau banyak arena.
-   Mengoptimalkan semua perangkat Android tanpa menetapkan perangkat
    uji.

## 5. Core Loop

1.  Main Menu → Play.
2.  Masuk arena.
3.  Bergerak dan menghindari serangan.
4.  Auto-attack menyerang musuh terdekat.
5.  Musuh menjatuhkan XP.
6.  Level-up menampilkan tiga pilihan upgrade.
7.  Gelombang musuh bertambah menantang.
8.  Boss muncul di akhir run.
9.  Victory atau defeat.
10. Hadiah diberikan dan progres permanen disimpan.
11. Pemain memulai run berikutnya.

Timer, spawn rate, HP, damage, cooldown, dan reward harus mudah
dikonfigurasi.

## 6. Fitur MVP

### P0 --- wajib

-   Main Menu, Play, Pause, Resume, Restart.
-   Game states: Menu, Playing, LevelUp, Paused, Victory, Defeat.
-   Player movement relatif terhadap kamera.
-   Virtual joystick Android.
-   Dash dengan cooldown dan invulnerability window singkat.
-   Auto-targeting dan auto-attack.
-   HP, damage, death, dan feedback serangan.
-   Tiga tipe musuh: slime normal, cepat, dan tank.
-   Enemy chase AI sederhana.
-   XP orb, XP bar, level-up.
-   Tiga pilihan upgrade acak dan penerapan efek.
-   Spawn musuh berdasarkan waktu.
-   Satu boss dengan minimal dua pola serangan dan telegraph.
-   HUD: HP, XP, level, timer, dash cooldown.
-   Victory/Defeat screen dengan reward dan retry.
-   Gold dan upgrade permanen sederhana.
-   Save/load lokal dengan `Application.persistentDataPath`.
-   Build APK dan pengujian pada perangkat Android.

### P1 --- setelah P0 stabil

-   Hero kedua.
-   Senjata atau skill tambahan.
-   15--20 upgrade dengan sinergi.
-   Arena atau boss tambahan.
-   Pengaturan grafis.
-   Audio lebih lengkap dan tutorial interaktif.

## 7. Kontrol dan UX

-   Rekomendasi orientasi awal: landscape; validasi dengan pengujian
    perangkat.
-   Kiri bawah: virtual joystick.
-   Kanan bawah: tombol dash.
-   Serangan dasar otomatis.
-   Bagian atas: HP, XP, level, timer.
-   Level-up benar-benar menjeda simulasi gameplay.
-   UI memperhitungkan safe area/notch.
-   Tombol memiliki ukuran sentuh yang nyaman.
-   Feedback damage, XP, cooldown, dan level-up mudah dibaca.

## 8. Arsitektur Unity

Gunakan C# dan sistem scene/prefab Unity. Gunakan versi Unity dan
package yang sudah tersedia kecuali ada alasan kuat untuk mengubahnya.

### Modul utama

  -----------------------------------------------------------------------
  Modul                               Tanggung jawab
  ----------------------------------- -----------------------------------
  `GameFlowController`                Menu, gameplay, level-up, pause,
                                      victory, defeat

  `PlayerController`                  Movement dan rotasi

  `PlayerHealth`                      HP, damage, death, invulnerability

  `DashController`                    Dash dan cooldown

  `AutoAttackController`              Targeting, range, attack cooldown

  `EnemyHealth`                       HP, damage, death, reward

  `EnemyChaseAI`                      Pergerakan musuh

  `EnemySpawner`                      Spawn dan peningkatan gelombang

  `XPManager`                         XP dan level-up

  `UpgradeManager`                    Pilihan upgrade dan penerapan efek

  `RunManager`                        Timer, boss trigger, hasil run

  `BossController`                    State dan pola serangan boss

  `SaveService`                       Penyimpanan progres permanen

  `UIController`                      HUD, pause, upgrade selection,
                                      result

  `AudioService`                      Audio jika diperlukan
  -----------------------------------------------------------------------

### Prinsip implementasi

-   Gunakan ScriptableObject untuk enemy stats, upgrade data, dan
    balancing.
-   Gunakan events untuk komunikasi antarsistem jika sesuai.
-   Hindari satu manager yang menangani semua logika.
-   Hindari dependency eksternal yang tidak diperlukan.
-   Jangan menambahkan object pooling sebelum kebutuhan terukur atau
    jelas.
-   Pisahkan state sementara run dari progres permanen.
-   Hindari angka balancing yang tersebar dalam banyak file.

## 9. Struktur Folder

``` text
Assets/
├── Art/                 # Aset hasil pipeline PRD aset
├── Audio/
├── Prefabs/
│   ├── Player/
│   ├── Enemies/
│   ├── Bosses/
│   ├── Pickups/
│   └── UI/
├── Scenes/
│   ├── Boot.unity
│   ├── MainMenu.unity
│   └── GameArena.unity
├── Scripts/
│   ├── Core/
│   ├── Player/
│   ├── Combat/
│   ├── Enemies/
│   ├── Boss/
│   ├── Progression/
│   ├── Run/
│   ├── Save/
│   └── UI/
├── ScriptableObjects/
│   ├── Enemies/
│   ├── Upgrades/
│   └── Balance/
└── Tests/
docs/
├── game_design.md
└── build_android.md
PRD_GAME_DEVELOPMENT.md
PRD_ASSET_PIPELINE.md
README.md
task.md
AGENTS.md
```

Struktur adalah panduan. Jangan membuat semua folder/file kosong pada
awal proyek; tambahkan sesuai kebutuhan. Jangan commit folder Unity
cache seperti `Library/`.

## 10. Save dan Data

Save minimum: - `schemaVersion` - `gold` - `permanentUpgrades` -
`bestRunTime` jika fitur ini digunakan

Ketentuan: - Gunakan `Application.persistentDataPath`. - Data baru,
hilang, atau rusak harus ditangani dengan nilai default dan fallback
yang aman. - Simpan pada checkpoint penting, bukan setiap frame. -
Jangan menyimpan state sementara run sebagai progres permanen. - Tidak
ada token atau credential karena game tidak memakai backend.

## 11. Performa dan Build Android

-   Target awal: 30 FPS stabil pada perangkat Android kelas menengah
    yang ditentukan untuk pengujian.
-   Pilih render pipeline berdasarkan versi Unity yang ada dan kebutuhan
    aset; jangan berganti pipeline setelah produksi dimulai tanpa
    alasan.
-   Batasi musuh aktif, partikel, transparansi, dan material berdasarkan
    profiling.
-   Uji pada perangkat nyata; performa Editor saja tidak cukup.
-   Periksa versi Unity, Android Build Support, SDK, NDK, dan JDK yang
    tersedia.
-   Verifikasi opsi Unity CLI/batch mode dari versi yang terpasang
    sebelum membuat command otomatis.
-   Jangan mengarang command build atau mengklaim APK berhasil tanpa
    output yang bisa diverifikasi.

## 12. Testing dan Acceptance Criteria

### Gameplay

-   Movement joystick berjalan dengan benar.
-   Kamera mengikuti pemain dengan stabil.
-   Auto-attack hanya memilih target valid dan mematuhi cooldown.
-   Dash memiliki cooldown dan invulnerability sesuai aturan.
-   Musuh mengejar dan memberi damage secara terkendali.
-   XP memicu level-up.
-   Tiga pilihan upgrade valid muncul dan dapat dipilih.
-   Level-up menghentikan gameplay.
-   Boss memiliki telegraph yang terbaca dan kondisi menang berfungsi.
-   Restart menginisialisasi run baru tanpa state sementara yang bocor.

### Data dan UI

-   Save permanen bertahan setelah aplikasi ditutup.
-   Save rusak tidak membuat game gagal dibuka.
-   UI tidak terpotong oleh notch atau rasio layar.
-   Pause/resume dan victory/defeat berfungsi.
-   Game inti dapat dimainkan offline.

### Build

-   Project dapat dibuka pada versi Unity yang terdokumentasi.
-   Tidak ada compile error.
-   Tidak ada runtime error berulang dalam run normal.
-   APK berhasil dipasang dan dibuka pada perangkat uji.
-   Hasil FPS, perangkat, dan masalah yang ditemukan dicatat.

## 13. Roadmap Implementasi

### Tahap 0 --- Audit

-   Periksa repository, Unity version, packages, scene, Git status, dan
    toolchain Android.
-   Baca instruksi agent.
-   Buat `task.md` berdasarkan tahap di bawah.
-   Jangan reset atau membuat ulang project valid yang sudah ada.

### Tahap 1 --- Greybox

-   Arena menggunakan primitive.
-   Player movement, kamera, dan input editor.
-   Virtual joystick Android.
-   Validasi di perangkat jika tersedia.

### Tahap 2 --- Combat

-   Auto-targeting, auto-attack, HP, damage, death.
-   Satu enemy chase AI.
-   Dash dan cooldown.

### Tahap 3 --- Roguelite loop

-   XP, level-up, upgrade.
-   Spawn waves dan timer.
-   Boss, victory/defeat, restart.

### Tahap 4 --- Integrasi aset

-   Ikuti `PRD_ASSET_PIPELINE.md`.
-   Integrasikan satu hero, satu slime, dan arena terlebih dahulu.
-   Validasi rig, animasi, skala, material, dan collider.

### Tahap 5 --- UI dan save

-   HUD dan menu.
-   Gold dan upgrade permanen.
-   Save/load lokal.

### Tahap 6 --- Android QA

-   Build APK.
-   Tes kontrol, lifecycle aplikasi, save/load, resolusi, performa.
-   Perbaiki bug P0 dan dokumentasikan hasil.

## 14. Workflow AI Coding Agent

1.  Audit project sebelum mengedit.
2.  Baca PRD ini, `task.md`, dan `AGENTS.md` jika ada.
3.  Implementasikan satu task per iterasi.
4.  Jangan membuat semua fitur sekaligus.
5.  Pertahankan project tetap dapat dibuka dan diuji setelah setiap
    tahap.
6.  Periksa diff sebelum menyelesaikan task.
7.  Jangan menimpa aset pengguna tanpa izin.
8.  Jangan menebak command CLI atau lokasi executable.
9.  Jalankan validasi relevan dan laporkan hasil aktual.
10. Jangan melakukan merge ke branch utama tanpa instruksi.
11. Laporkan file berubah, validasi, hasil, blocker, dan langkah
    berikutnya.

### Format laporan task

-   Tujuan.
-   File dibuat/diubah.
-   Implementasi.
-   Command validasi yang benar-benar dijalankan.
-   Hasil validasi.
-   Bug atau blocker.
-   Langkah berikutnya.

## 15. Definisi Selesai

MVP selesai jika pemain dapat membuka game, menyelesaikan satu run
lengkap secara offline, memperoleh reward, meningkatkan karakter,
menutup aplikasi, dan kembali dengan progres permanen tersimpan. Project
harus dapat dibuild ke Android dan diuji pada setidaknya satu perangkat
nyata.

## 16. Instruksi Task Pertama

Audit project Unity yang tersedia. Identifikasi versi Unity, struktur
project, packages, status Git, toolchain Android, dan CLI aset yang
tersedia. Jangan mengarang nama CLI atau argumennya.

Laporkan temuan dan rencana, lalu implementasikan **hanya greybox arena
dan player movement**. Jangan membuat combat atau aset final sebelum
movement berhasil divalidasi.
