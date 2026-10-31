# Rilis ke Google Play

## Yang sudah disiapkan di proyek

| Syarat Play | Status |
|---|---|
| Nama dan paket | **Dumpling Witch**, paket `com.gitgitmiko.dumplingwitch` (sejak 0.32.0; paket tidak bisa diganti setelah upload pertama). Nama kode proyek/repo tetap Slime Breaker. |
| Format Android App Bundle (`.aab`) | `ProjectSetup.BuildAndroidAab` → `Builds/SlimeBreaker.aab` (log `SLIMEBREAKER_AAB_OK`) |
| Ditandatangani upload key | `~/.slimebreaker/upload.keystore` (alias `upload`, RSA 4096, PKCS12), kata sandi di `~/.slimebreaker/upload-keystore.properties` (chmod 600). **Di luar repo, jangan di-commit.** Path lain lewat env `SLIMEBREAKER_KEYSTORE_PROPERTIES`. Setelah build, password dikosongkan lagi dari `ProjectSettings.asset`. |
| Target API terbaru | `targetSdkVersion` = API 36 (Android 16), `minSdk` 25 |
| 64-bit | ARM64 saja (IL2CPP) |
| Ikon aplikasi | Adaptive (background + foreground), round, legacy dari `asset_work/output/UI/AppIcon/` |
| Tanpa izin sensitif | Tidak ada izin lokasi/kamera/mikrofon/kontak; Unity Analytics/Cloud mati. SDK AdMob menambah `INTERNET`, `ACCESS_NETWORK_STATE`, dan `AD_ID` |
| Ikon 512, feature graphic, screenshot | `asset_work/store/icon_512.png`, `feature_graphic_1024x500.png`, `screenshots/*.png` (1280×720) |
| Situs developer | **https://gitgitdev.my.id** (repo `~/github-repos/gitgitdev-site`). Halaman game: `https://gitgitdev.my.id/dumpling-witch/` |
| Kebijakan privasi | **https://gitgitdev.my.id/dumpling-witch/privacy/** (ID + EN; sumber di repo situs, `docs/store/privacy-policy.html` hanya salinan lama) |
| AdMob | Google Mobile Ads 11.5.0 (OpenUPM) + UMP consent. App ID `ca-app-pub-6808667639334235~3733831999`, unit Rewarded `ca-app-pub-6808667639334235/8407574176` (`Continue_Revive`). AAB memakai unit asli; APK sideload dibangun dengan `SB_ADMOB_TEST` dan hanya meminta iklan uji Google. |
| app-ads.txt | **https://gitgitdev.my.id/app-ads.txt** (aktif). Isi `https://gitgitdev.my.id` sebagai *Website* di Play Console |

Bangun ulang aset store:

```bash
blender --background --python asset_work/scripts/render_store_art.py -- "$PWD" asset_work/store/renders
python3 asset_work/scripts/build_store_art.py
```

Build AAB (setelah `BuildAll`):

```bash
unity-editor -batchmode -nographics -quit -projectPath "$PWD" \
  -executeMethod SlimeBreaker.EditorTools.ProjectSetup.BuildAndroidAab \
  -logFile Logs/android-aab.log
```

Naikkan `AppVersion` dan `bundleVersionCode` di `ProjectSetup` untuk setiap upload; Play menolak versionCode yang sama.

### Upload key

Simpan cadangan `~/.slimebreaker/upload.keystore` **dan** `upload-keystore.properties` di tempat aman (mis. password manager + drive pribadi). Dengan Play App Signing, Google memegang kunci rilis; kalau upload key hilang, reset bisa diminta lewat Play Console, tapi prosesnya lama.

## Langkah di Play Console (dikerjakan pemilik akun)

1. **Akun developer** di play.google.com/console — biaya sekali USD 25, verifikasi identitas (KTP) dan nomor HP.
2. **Create app**: nama *Dumpling Witch*, bahasa default Indonesia, *Game*, *Free*, centang deklarasi.
3. **Kebijakan privasi**: isi `https://gitgitdev.my.id/dumpling-witch/privacy/` di *App content → Privacy policy*; *Store settings → Website*: `https://gitgitdev.my.id`.
4. **App content**:
   - *Ads*: **Ya, berisi iklan** (iklan reward AdMob saat pemain memilih Lanjutkan, dan interstitial saat masuk lantai boss).
   - *Advertising ID*: **Ya**, dipakai untuk *Advertising or marketing*.
   - *App access*: semua fitur bisa dipakai tanpa login.
   - *Content rating* (kuesioner IARC): kategori Game; kekerasan fantasi/kartun ringan terhadap makhluk fantasi (slime), tanpa darah, tanpa interaksi pengguna, tanpa pembelian, tanpa judi. Hasil biasanya PEGI 7 / ESRB Everyone 10+ / IARC 7+.
   - *Target audience*: **13+** (13–15, 16–17, 18+). Jangan pilih di bawah 13 kecuali siap memenuhi *Families Policy* (SDK iklan bersertifikat keluarga, tanpa iklan personal).
   - *Data safety* (AdMob sudah terpasang sejak 0.31.0): mengumpulkan *Device or other IDs* (advertising ID), *App info and performance* (crash/diagnostik) dan *App interactions*, dibagikan ke Google untuk *Advertising or marketing* dan *Fraud prevention*; data dienkripsi saat transit; pengguna tidak bisa meminta penghapusan lewat aplikasi (tidak ada akun).
   - *Government app*, *Financial features*, *Health*: Tidak.
5. **Store listing**: teks di bawah, ikon 512, feature graphic, minimal 2 screenshot ponsel (sudah ada 8). Kategori *Action* atau *Role Playing*; email kontak `support@gitgitdev.my.id` (Cloudflare Email Routing → Gmail).
6. **Testing → Closed testing** (wajib untuk akun pribadi yang dibuat setelah November 2023): buat track, upload `SlimeBreaker.aab`, undang **minimal 12 penguji** (email Google) yang tetap ikut **14 hari berturut-turut**. Setelah itu ajukan *Production access*.
7. **Production**: buat rilis dari AAB yang sama atau yang lebih baru, isi catatan rilis, kirim untuk review (biasanya beberapa hari).

## Menghubungkan AdMob

Aplikasi AdMob *Dumpling Witch* sudah dibuat tanpa tautan store. Alur di game: Lanjutkan → iklan reward AdMob; reward didapat → hero bangkit; iklan ditutup sebelum reward → kembali ke panel Nyawa Habis dengan sisa hitung mundur; iklan belum termuat/offline → house ad. Formulir persetujuan UMP muncul saat start untuk pengguna EEA/UK — buat pesan GDPR di AdMob → *Privacy & messaging*. Setelah game tayang di Play, buka aplikasi di AdMob → *App settings* → tautkan ke Play Store, lalu tunggu review aplikasi AdMob. Sebelum itu iklan nyata terbatas; selama pengembangan pakai iklan uji. Pasang `app-ads.txt` di domain situs developer yang diisi di Play Console.

## Teks store listing

**Nama aplikasi** (≤30): `Dumpling Witch`

**Deskripsi singkat** (≤80): `Penyihir chibi vs ribuan slime! Tembus 100 lantai dan kalahkan 10 boss dumpling.`

**Deskripsi lengkap:**

```
Jadilah penyihir chibi bertopi raksasa dan hancurkan gelombang slime di arena 3D yang terus berubah!

• 100 LANTAI – Setiap lantai butuh lebih banyak slime untuk dikalahkan. Masuk portal untuk naik ke lantai berikutnya.
• 10 BOSS DUMPLING – Tiap 10 lantai, boss jelly raksasa muncul. Saat HP-nya di bawah 30% ia mengamuk (ENRAGE) dengan skill elemen yang heboh. Bisakah kamu mengalahkan Royal Dumpling pelangi di lantai 100?
• SKILL SIHIR – Kumpulkan Cincin Es, Nova Api, Petir Berantai, dan Misil Arkana, lalu naikkan sampai Lv 10.
• NAIK LEVEL – Pilih 1 dari 3 kartu upgrade setiap naik level: damage, kecepatan, critical, lifesteal, pertahanan, dan lainnya.
• ITEM & INVENTORI – Staff, topi, baju, celana, dan sepatu dengan rarity Normal, Rare, Legend, sampai Ultra.
• 10 BIOMA – Hutan, salju, gurun, petir, rawa, bayangan, gunung api, badai, kristal, dan istana kerajaan, dengan cuaca serta siang/malam.
• 3 NYAWA – Nyawa habis? Lanjutkan perjalananmu atau mulai petualangan baru.
• Kontrol mudah satu jari: joystick, dash, dan putar kamera dengan geser layar.
• Tiga tingkat kesulitan: Easy, Normal, Hard.
• Main offline, tanpa akun.
```

**English (optional translation):**

- Short: `A chibi witch vs. endless slimes! Climb 100 floors and beat 10 dumpling bosses.`
- Full:

```
Become a chibi witch with a giant hat and blast waves of slimes in an ever-changing 3D arena!

• 100 FLOORS – Each floor needs more slimes cleared. Step into the portal to climb higher.
• 10 DUMPLING BOSSES – Every 10 floors a giant jelly boss appears and goes into a wild ENRAGE below 30% HP. Can you beat the rainbow Royal Dumpling on floor 100?
• MAGIC SKILLS – Collect Ice Ring, Fire Nova, Chain Lightning and Arcane Missiles and level them up to Lv 10.
• LEVEL UP – Pick 1 of 3 upgrade cards each level: damage, speed, critical, lifesteal, defense and more.
• ITEMS & INVENTORY – Staffs, hats, armor, pants and boots in Normal, Rare, Legend and Ultra rarity.
• 10 BIOMES – Forest, snow, desert, thunder, swamp, shadow, volcano, storm, crystal and royal palace, with weather and day/night.
• 3 LIVES – Out of lives? Continue your run or start a new adventure.
• Easy one-thumb controls: joystick, dash, and swipe to rotate the camera.
• Three difficulties: Easy, Normal, Hard.
• Play offline, no account needed.
```
