# PRD 2 --- Slime Breaker Asset Production Pipeline

**Versi:** 1.0\
**Produk:** Slime Breaker\
**Target engine:** Unity\
**Platform:** Android\
**Gaya visual:** Anime 3D chibi fantasy\
**Dokumen pasangan:** `PRD_GAME_DEVELOPMENT.md`

------------------------------------------------------------------------

## 1. Tujuan Dokumen

Dokumen ini khusus mengatur produksi, validasi, pengelolaan, dan
integrasi aset visual untuk Slime Breaker menggunakan CLI pembuatan aset
yang telah tersedia di server.

Dokumen ini **tidak mengasumsikan nama tool, command, parameter, API,
atau format output tertentu**. Agent wajib mengaudit tool yang
benar-benar terpasang dan membaca help/dokumentasi yang tersedia sebelum
menjalankan command.

Implementasi gameplay, UI logic, save system, dan build game diatur oleh
`PRD_GAME_DEVELOPMENT.md`.

## 2. Tujuan Pipeline Aset

-   Membuat aset 3D yang konsisten secara visual dan kompatibel dengan
    Unity.
-   Memprioritaskan aset yang diperlukan MVP.
-   Memastikan model, material, rig, animasi, dan skala tervalidasi
    sebelum produksi massal.
-   Mengurangi pekerjaan ulang akibat format atau skeleton yang tidak
    kompatibel.
-   Menyimpan prompt, metadata, versi, dan catatan integrasi aset jika
    tool mendukungnya.
-   Menggunakan aset secara efisien untuk Android kelas menengah.
-   Menjaga seluruh aset dan hak penggunaannya terdokumentasi.

## 3. Prinsip Utama

1.  **Audit before generation:** kenali tool dan kemampuan nyata sebelum
    membuat aset.
2.  **One asset first:** buat dan integrasikan satu hero serta satu
    enemy sebelum memperbanyak aset.
3.  **Consistent art direction:** semua aset harus berasal dari style
    guide yang sama.
4.  **Unity-ready:** aset dinilai dari hasil integrasi di Unity, bukan
    hanya gambar preview.
5.  **Mobile-friendly:** geometri, material, tekstur, rig, dan efek
    disesuaikan untuk Android.
6.  **Reversible workflow:** jangan menimpa aset final tanpa backup atau
    Git commit.
7.  **Traceable:** simpan metadata generasi dan lisensi sejauh tersedia.

## 4. Art Direction

### Gaya

-   Anime 3D chibi fantasy.
-   Siluet yang mudah dibaca dari kamera gameplay third-person dengan
    sudut sedikit tinggi.
-   Proporsi hero imut dengan kepala relatif besar, bentuk sederhana,
    dan ekspresi jelas.
-   Warna hero harus kontras terhadap slime dan lingkungan.
-   Fantasy ringan: hutan, batu, jamur, reruntuhan kecil, energi sihir.
-   Efek serangan berwarna dan menarik, tetapi tidak boleh menutupi
    telegraph boss.
-   Hindari tekstur fotorealistis yang tidak sesuai dengan gaya
    keseluruhan.

### Konsistensi

-   Tentukan satu palet warna utama dan warna aksen.
-   Pertahankan proporsi, bentuk mata, material, tingkat detail, dan
    pencahayaan yang serupa.
-   Semua varian karakter harus terlihat berasal dari game yang sama.
-   Gunakan referensi visual yang sah dan jelas; jangan menyalin
    karakter berhak cipta yang dikenal.
-   Jangan menggunakan ikon atau logo generik hasil AI jika tidak sesuai
    identitas game.

## 5. Daftar Aset dan Prioritas

### P0 --- aset untuk MVP

  -----------------------------------------------------------------------
  ID                Aset              Kebutuhan minimum Prioritas
  ----------------- ----------------- ----------------- -----------------
  A01               Hero utama        Model 3D,         P0
                                      material, rig     
                                      kompatibel,       
                                      animasi inti      

  A02               Slime dasar       Model sederhana,  P0
                                      material,         
                                      gerak/deformasi   
                                      atau animasi      
                                      sederhana         

  A03               Slime cepat       Bisa memakai base P0
                                      mesh dengan       
                                      variasi yang      
                                      jelas             

  A04               Slime tank        Bisa memakai base P0
                                      mesh dengan       
                                      variasi           
                                      ukuran/material   

  A05               Slime King        Model boss yang   P0
                                      jelas lebih besar 
                                      dan berbeda       

  A06               Arena hutan       Ground, pohon,    P0
                                      batu, dekorasi    
                                      dasar             

  A07               VFX dasar         Hit, dash, XP     P0
                                      orb, telegraph    
                                      dan serangan boss 

  A08               Ikon upgrade      Ikon yang terbaca P0
                                      pada layar kecil  

  A09               UI visual         Panel, tombol,    P0
                                      frame, elemen HUD 
                                      bila dibutuhkan   

  A10               Audio             Hit, dash,        P0
                                      level-up, boss,   
                                      UI; bisa memakai  
                                      sumber legal      
  -----------------------------------------------------------------------

### P1 --- setelah gameplay stabil

-   Hero kedua.
-   Variasi senjata.
-   Environment props tambahan.
-   Skin atau variasi slime.
-   Efek skill tambahan.
-   Arena dan boss tambahan.

Jangan membuat aset P1 sebelum aset P0 terbukti dapat diimpor dan
digunakan.

## 6. Spesifikasi Aset

Spesifikasi berikut adalah panduan awal. Sesuaikan berdasarkan kemampuan
tool, kebutuhan rig, dan hasil profiling.

### 6.1 Hero

-   Model humanoid 3D dengan siluet anime chibi.
-   Pivot dan orientasi konsisten.
-   Skala masuk akal terhadap collider dan arena.
-   Jika menggunakan animasi skeletal, skeleton harus konsisten untuk
    semua klip.
-   Animasi yang dibutuhkan: idle, run, attack, dash, hit, death.
-   Animasi attack tidak wajib mengendalikan gameplay timing; gunakan
    event atau state logic yang stabil.
-   Material sederhana dan mudah dipelihara.
-   Periksa clipping pakaian/senjata dan deformasi rig.

### 6.2 Musuh slime

-   Bentuk sederhana dan mudah dibaca.
-   Animasi idle, move, hit, death jika pipeline mendukungnya.
-   Varian cepat dan tank dapat menggunakan variasi material, ukuran,
    atau detail agar produksi efisien.
-   Collider gameplay dibuat terpisah atau dikonfigurasi secara
    eksplisit di Unity.
-   Jangan mengandalkan mesh visual sebagai collider kompleks tanpa
    kebutuhan.

### 6.3 Boss

-   Siluet lebih besar dari musuh biasa.
-   Animasi idle, move, attack, wind-up/telegraph, hit, death jika
    tersedia.
-   Serangan area harus punya visual telegraph yang jelas.
-   Efek tidak boleh mengaburkan area aman dan berbahaya.

### 6.4 Arena

-   Satu arena hutan kecil dengan batas jelas.
-   Props dekoratif tidak boleh menghalangi kamera atau kontrol pemain.
-   Sediakan area navigasi yang cukup luas untuk menghindari serangan.
-   Pisahkan mesh dekorasi dan collider gameplay.
-   Hindari terlalu banyak material unik dan transparansi.

### 6.5 VFX

-   Hit spark, dash trail, XP pickup, upgrade/level-up, boss warning dan
    boss attack.
-   Efek singkat, jelas, dan hemat partikel.
-   Hindari bloom/flash berlebihan yang menutupi musuh.
-   Perhatikan visibilitas pada layar ponsel dan aksesibilitas pemain.

### 6.6 UI dan ikon

-   Kontras tinggi dan mudah dibaca pada resolusi ponsel.
-   Bentuk ikon konsisten dan tetap terbaca dalam ukuran kecil.
-   Sediakan sumber resolusi memadai dan ukuran impor sesuai penggunaan.
-   UI gameplay dibuat responsif terhadap safe area.

## 7. Audit CLI Aset

Sebelum menghasilkan aset, agent wajib: 1. Menemukan tool/CLI yang
tersedia dari lingkungan server atau dokumentasi lokal. 2. Memeriksa
versi dan perintah help yang didukung. 3. Mengidentifikasi jenis aset
yang dapat dibuat: mesh, texture, material, rig, animasi, VFX, audio,
atau hanya gambar. 4. Mengidentifikasi format ekspor, lokasi output,
batasan resolusi/poligon, dan kebutuhan lisensi. 5. Menjalankan satu uji
kecil yang tidak menimpa aset yang ada. 6. Memeriksa apakah hasil bisa
diimpor ke versi Unity proyek. 7. Mendokumentasikan command yang
benar-benar berhasil di `docs/asset_pipeline.md`.

**Larangan:** jangan menebak nama executable, parameter, endpoint, atau
lokasi file. Jangan mengklaim tool mampu membuat rig/animasi jika belum
diverifikasi. Jangan mengirim credential atau data sensitif ke layanan
eksternal.

## 8. Pipeline Produksi Aset

### Tahap 1 --- Style guide

-   Tetapkan deskripsi visual hero, slime, boss, arena, material, dan
    palet warna.
-   Tentukan sudut kamera gameplay untuk menilai keterbacaan aset.
-   Simpan keputusan visual dan referensi yang diizinkan.

**Output:** `docs/art_direction.md`.

### Tahap 2 --- Proof of pipeline

-   Buat satu hero placeholder/final awal dan satu slime.
-   Impor keduanya ke project Unity.
-   Uji skala, pivot, material, rig, animasi, dan performa.
-   Perbaiki pipeline sebelum membuat variasi.

**Output:** aset uji yang berhasil tampil di scene Unity.

### Tahap 3 --- Hero final

-   Iterasi siluet dan material.
-   Validasi semua animasi yang diperlukan.
-   Pastikan orientasi karakter sesuai sistem movement.
-   Uji dengan kamera gameplay dan lighting arena.

**Output:** hero yang siap digunakan.

### Tahap 4 --- Musuh dan boss

-   Buat slime dasar.
-   Gunakan ulang base mesh atau material untuk varian bila masuk akal.
-   Buat boss dengan siluet serta animasi telegraph yang jelas.
-   Validasi skala terhadap hero.

**Output:** tiga tipe musuh dan satu boss.

### Tahap 5 --- Arena

-   Buat ground dan props modular.
-   Impor ke Unity dan susun satu arena.
-   Periksa navigasi, kamera, collider, dan visibility.
-   Pastikan dekorasi tidak menghalangi gameplay.

**Output:** arena MVP siap dimainkan.

### Tahap 6 --- VFX/UI/audio

-   Buat efek hit, dash, XP orb, level-up, dan boss attack.
-   Integrasikan ikon upgrade.
-   Tambahkan audio yang legal dan sesuai style.
-   Uji agar efek tidak mengurangi keterbacaan atau performa.

**Output:** aset feedback gameplay lengkap untuk MVP.

### Tahap 7 --- Optimasi dan katalog

-   Periksa ukuran file, tekstur, material, geometri, dan animasi.
-   Kurangi detail jika performa atau ukuran build bermasalah.
-   Catat aset final dan dependency-nya.
-   Simpan preview atau thumbnail bila tersedia.

**Output:** katalog aset MVP dan catatan optimasi.

## 9. Struktur Folder Aset

Gunakan struktur berikut di dalam Unity project:

``` text
Assets/
├── Art/
│   ├── Characters/
│   │   └── Hero/
│   ├── Enemies/
│   │   ├── Slime/
│   │   └── SlimeKing/
│   ├── Environment/
│   │   └── ForestArena/
│   ├── Materials/
│   ├── Textures/
│   ├── Animations/
│   ├── VFX/
│   └── UI/
│       ├── Icons/
│       └── Panels/
├── Audio/
│   ├── SFX/
│   └── Music/
└── Prefabs/
    ├── Player/
    ├── Enemies/
    ├── Bosses/
    ├── Environment/
    └── VFX/
```

Folder kerja sementara sebaiknya berada di luar folder impor Unity,
misalnya folder `asset_work/` di root repository atau lokasi kerja yang
disepakati. Jangan memasukkan file intermediate besar ke `Assets/` jika
tidak digunakan runtime.

## 10. Naming Convention dan Versioning

Gunakan nama yang konsisten dan deskriptif: - `CHR_Hero_Main` -
`ENM_Slime_Normal` - `ENM_Slime_Fast` - `ENM_Slime_Tank` -
`BOSS_SlimeKing` - `ENV_Forest_Ground` - `VFX_Hit_Small` -
`VFX_Dash_Trail` - `ICON_Upgrade_Damage`

Sesuaikan prefix bila konvensi project yang sudah ada lebih baik. Jangan
mengganti nama aset yang sudah direferensikan scene/prefab tanpa
memperbarui referensinya.

Untuk setiap aset, catat bila memungkinkan: - ID dan nama aset. - Jenis
dan tujuan. - Tool/CLI dan versi. - Prompt atau parameter yang
digunakan. - Format output dan sumber file. - Status
rig/animasi/material. - Lisensi dan sumber. - Lokasi file. - Status:
draft, integrated, approved, deprecated.

## 11. Validasi Integrasi Unity

Aset tidak dianggap selesai hanya karena berhasil dihasilkan.

### Model dan material

-   File terbuka dan dapat diimpor.
-   Skala, pivot, dan orientasi benar.
-   Material dan tekstur tidak hilang.
-   Tidak ada permukaan penting yang terbalik atau rusak.
-   Jumlah material dan kompleksitas mesh wajar.

### Rig dan animasi

-   Rig cocok dengan model.
-   Animasi berjalan tanpa deformasi besar.
-   Tidak ada foot sliding atau clipping yang mengganggu.
-   Root motion dan in-place movement ditentukan secara konsisten.
-   Jika tool tidak menyediakan animasi, dokumentasikan kebutuhan
    animasi alternatif.

### Gameplay integration

-   Collider ditetapkan terpisah sesuai kebutuhan.
-   Ukuran model sesuai target, jangkauan serangan, dan kamera.
-   Efek tidak menutupi telegraph atau karakter.
-   Model dapat digunakan pada prefab yang dituju.
-   Aset diuji dalam build/perangkat target ketika memungkinkan.

## 12. Batasan Android

-   Hindari detail mesh yang tidak terlihat dari kamera.
-   Gunakan jumlah material seperlunya.
-   Atur ukuran tekstur sesuai penggunaan aktual.
-   Batasi shader mahal, transparansi, dan partikel berlebih.
-   Gunakan LOD atau simplifikasi bila profiling menunjukkan manfaat.
-   Uji pada perangkat Android target, bukan hanya di Editor.
-   Jangan menetapkan batas poligon/tekstur universal sebelum mengetahui
    tool dan perangkat target; dokumentasikan angka aktual setelah
    pengujian.

## 13. Hak Penggunaan dan Keamanan

-   Gunakan aset yang dibuat sendiri atau yang memiliki lisensi sesuai
    tujuan distribusi.
-   Catat lisensi aset eksternal.
-   Jangan menyalin karakter, logo, atau desain terlindungi yang dikenal
    tanpa izin.
-   Jangan memasukkan credential atau secret ke prompt/metadata aset.
-   Jangan mengunggah aset proprietary ke layanan eksternal tanpa izin.
-   Jangan menghapus aset sumber atau menimpa hasil final tanpa
    backup/commit.

## 14. Acceptance Criteria Pipeline Aset

Pipeline aset MVP dinyatakan berhasil jika: - Tool dan command yang
digunakan sudah diverifikasi. - Satu hero dan satu slime berhasil
diimpor dan ditampilkan di Unity. - Skala, material, pivot, dan
rig/animasi tervalidasi sesuai kebutuhan. - Tiga varian slime dan satu
boss dapat digunakan dalam prefab gameplay. - Arena dapat dipakai
bermain tanpa collider atau kamera yang mengganggu. - VFX utama terbaca
dan tidak mengganggu gameplay. - Aset yang diperlukan MVP tercatat di
katalog. - Sumber, versi tool, dan lisensi terdokumentasi sejauh
tersedia. - Performa dan ukuran aset telah diperiksa pada target yang
tersedia. - Tidak ada aset final yang ditimpa tanpa izin.

## 15. Workflow AI Asset Agent

1.  Baca `PRD_ASSET_PIPELINE.md` dan `PRD_GAME_DEVELOPMENT.md`.
2.  Audit tool/CLI dan dokumentasinya.
3.  Laporkan kemampuan yang terverifikasi dan keterbatasan yang
    ditemukan.
4.  Buat `docs/art_direction.md` dan `docs/asset_pipeline.md`.
5.  Hasilkan satu aset uji.
6.  Integrasikan dan validasi di Unity.
7.  Baru lanjutkan produksi aset P0.
8.  Periksa output dan file changes sebelum menyelesaikan task.
9.  Simpan prompt/metadata bila tersedia.
10. Laporkan hasil aktual, bukan asumsi.

### Format laporan aset

-   Aset yang diminta.
-   Tool dan versi yang digunakan.
-   Command yang benar-benar dijalankan.
-   File output dan format.
-   Validasi Unity yang dilakukan.
-   Status rig, animasi, material, skala, dan lisensi.
-   Kendala serta langkah berikutnya.

## 16. Roadmap Produksi

1.  **Audit CLI:** identifikasi tool, versi, format, dan batasan.
2.  **Style guide:** tentukan karakter, proporsi, warna, dan material.
3.  **Proof of pipeline:** satu hero dan satu slime di Unity.
4.  **Hero:** finalisasi model dan animasi inti.
5.  **Enemy/boss:** buat tiga varian slime dan boss.
6.  **Arena:** ground dan props modular.
7.  **VFX/UI/audio:** feedback combat dan ikon upgrade.
8.  **Optimization:** uji performa, ukuran, dan keterbacaan.
9.  **Asset catalog:** dokumentasikan output final dan sumbernya.

Urutan ini boleh tumpang tindih dengan development, tetapi jangan
menghambat tahap greybox dan validasi gameplay.

## 17. Instruksi Task Pertama

Audit CLI pembuat aset yang sudah tersedia pada server. Jangan berasumsi
tentang nama tool atau kemampuannya. Verifikasi command dan output
format dari dokumentasi/help yang benar-benar tersedia.

Kemudian: 1. Buat `docs/art_direction.md` dengan style guide awal. 2.
Buat `docs/asset_pipeline.md` dengan command yang sudah diverifikasi. 3.
Hasilkan satu aset uji yang paling sederhana dan relevan. 4.
Integrasikan aset tersebut ke project Unity tanpa mengubah sistem
gameplay yang belum diminta. 5. Laporkan output, keterbatasan, validasi,
dan langkah selanjutnya.

**Prioritas tertinggi:** pipeline yang terbukti bekerja dan aset yang
benar-benar terintegrasi ke Unity, bukan jumlah aset yang dihasilkan.
