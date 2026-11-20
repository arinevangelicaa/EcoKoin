# 8. Status & Roadmap

Dokumen 1–7 menjelaskan **kondisi target**. Dokumen ini satu-satunya yang mencatat **posisi repo saat ini** dan jalan menuju target.

Snapshot: 2026-11-19, branch `544058`, HEAD `27c54bb` (merge PR #9).

> **Update 2026-11-20:** langkah roadmap 1-8 untuk backend sudah dikerjakan di `src/EcoKoin.Core` (`EcoKoin.sln`: Core + Tests, 156 tes lulus). `EcoKoin.WPF` mereferensikan Core. Tabel di bawah masih menggambarkan snapshot sebelum update ini; sisa pekerjaan: langkah 9-11 (UI binding, halaman, CI). WPF belum dimasukkan ke `EcoKoin.sln` agar solusi tetap bisa dibangun di Linux.

## Riwayat

| Tahap | Isi | Referensi |
|---|---|---|
| Inisiasi | README, tim, `fileindex.md`, Jekyll | PR #2–#4 |
| Desain class | class diagram, kerangka `Models/`, `Services/`, `ViewModels/` | PR #5 |
| Database | ERD v1, `schema.sql`, `ecokoin.db` | PR #6 |
| Prototipe PBO | WinForms Form1–3, hierarki quest, Npgsql | PR #7 |
| UI produk | WPF dashboard (slicing Figma) + token | PR #8, #9 |

## Target vs saat ini

Legenda: ✅ sesuai target · ◐ sebagian · ⛔ stub (`NotImplementedException`) · ✗ belum ada.

| Area | Target | Saat ini | Status |
|---|---|---|---|
| Solusi & proyek | `EcoKoin.sln`: `Core`, `WPF`, `Tests` | Dua solusi `.slnx` terpisah (WPF, WinForms); `Models/`, `Services/`, `ViewModels/` di root, **tidak dikompilasi** oleh proyek mana pun | ✗ |
| Paket data | `Microsoft.Data.Sqlite` terpasang | belum direferensikan; prototipe memakai Npgsql | ✗ |
| Skema DB | v2 (password, `quest_type`, reward, klaim, indeks, CHECK) | v1 (ERD Modul 4); `ecokoin.db` berisi sample data | ◐ |
| `SqliteRepository` | CRUD lengkap, FK per koneksi | hanya `InitializeDatabase()`; sisanya ⛔; `schema.sql` belum disalin ke output | ◐ |
| `User` | validasi, `Login` hash, Level/XP | `AddEcoKoin`, `GetBalance` ✅; `Login` ⛔; tanpa `PasswordHash`/Level | ◐ |
| `DailyQuest` | abstrak + 3 turunan, `Claim`, reward | konkret; `UpdateProgress`, `CheckCompletion` ✅; tanpa reward/klaim/turunan | ◐ |
| `EcoPet` | `UpdateState`, `Animate` | keduanya ⛔ | ⛔ |
| `PowerScheme` / `PowerSession` | `Activate` via powercfg; sesi dengan `UserId`/`SchemeId`, `EndTime` nullable | `Activate`, `SaveToDatabase` ⛔; properti kurang, `EndTime` non-nullable | ⛔ |
| `PowerMonitorService` | idle (Win32), terapkan skema, estimasi watt | semua ⛔ | ⛔ |
| `WeatherService` | OpenWeatherMap + cache + rekomendasi | semua ⛔ | ⛔ |
| `CarbonFinancialReport` | rumus CO₂/Rupiah, 7 titik grafik | semua method ⛔ | ⛔ |
| `MainViewModel` | `INotifyPropertyChanged`, command, timer | properti saja; `Initialize`, `RefreshDashboard` ⛔ | ⛔ |
| WPF dashboard | data bind, 4 halaman, token global, font ter-embed | UI statis 1 halaman; brush/style lokal di `MainWindow.xaml`, font Segoe UI; sidebar tanpa aksi; data contoh ("Hermione") | ◐ |
| Token desain | satu sumber `Resources/*.xaml` | ada & di-merge, tetapi tidak dipakai window; nilai hex sedikit berbeda (`#2B4B3E` vs `#2C4C3E`) | ◐ |
| Prototipe WinForms | arsip referensi | berjalan, PostgreSQL, data simulasi; angka CO₂ tidak konsisten antar-form (1.2 vs 2.3 kg) | ✅ sebagai arsip |
| Tes & CI | xUnit + build Windows di CI | tidak ada | ✗ |
| Lisensi | ditentukan | tidak ada | ✗ |

## Roadmap menuju target

Urutan dari fondasi ke fitur; tiap langkah dapat menjadi satu PR.

1. **Satukan solusi.** Buat `src/EcoKoin.Core` (pindahkan `Models/`, `Services/`, `ViewModels/`), `EcoKoin.sln`, referensi dari WPF, tambah `Microsoft.Data.Sqlite`, salin `schema.sql` ke output.
2. **Skema v2.** Perbarui `schema.sql` + `seed.sql`, ERD.md, DATABASE_PROOF.md.
3. **Selaraskan model.** Tambah `UserId`/`SchemeId`/`EndTime?`, `PasswordHash`, `Humidity`; jadikan `DailyQuest` abstrak + tiga turunan; pemetaan enum `PetState`.
4. **`SqliteRepository` penuh** + tes round-trip in-memory.
5. **Logika murni:** `User`, `DailyQuest.Claim`, `EcoPet.UpdateState`, `CarbonFinancialReport` + unit test.
6. **`PowerMonitorService` & `PowerScheme.Activate`** (Win32 idle, `powercfg`).
7. **`WeatherService`** (HttpClient, cache, API key dari env/user-secrets).
8. **`MainViewModel` MVVM** (`INotifyPropertyChanged`, `ICommand`, timer) dan `DataContext` WPF.
9. **Rapikan WPF:** hapus brush lokal, pakai token & style global, embed Poppins/Inter, bind semua data, ganti ilustrasi grafik dengan data nyata.
10. **Halaman Misi & Reward, Analisis, Pengaturan** + navigasi.
11. **Kualitas:** GitHub Actions (Windows runner: build + test), pilih lisensi, tandai prototipe sebagai arsip.

## Asumsi desain yang perlu dikonfirmasi tim

Nilai berikut dipilih untuk melengkapi spesifikasi dan belum datang dari dokumen tugas:

- Faktor emisi `0.87` kg CO₂/kWh dan tarif `Rp1.444,70`/kWh (nilai default, dapat diubah di konfigurasi; perlu diverifikasi sumbernya).
- Ambang EcoPet (≥ 30 koin Segar, 10–29 Normal), Level tiap 500 koin.
- Model estimasi watt hemat (`BaselineWatt` 65 W) dan ambang awan rekomendasi (60% / 30%).
- Target/reward tiga quest contoh (30 / 20 / 15 koin).
- Perubahan skema v2 menyimpang dari ERD Modul 4 (kolom tambahan saja; relasi tetap).
