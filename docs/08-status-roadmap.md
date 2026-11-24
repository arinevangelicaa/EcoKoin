# 8. Status & Roadmap

Dokumen 1–7 menjelaskan **kondisi target**. Dokumen ini satu-satunya yang mencatat **posisi repo saat ini** dan jalan menuju target.

Snapshot: 2026-11-24, branch `544058`. Persyaratan tugas **tidak mengizinkan database lokal**; seluruh penyimpanan kini diarahkan ke PostgreSQL remote.

Update 2026-11-24 (sore): `origin/main` (PR #29–#36 dari tim UI: cuaca, quest Power Saver, Ingat saya, dashboard dengan data nyata) sudah digabung ke `544058`. Method repository baru dari tim UI dipindahkan ke `IAppRepository` (async), `PostgresRepository`, dan suite kontrak; seluruh pemanggil di `EcoKoin.WPF` memakai API async. Suite tes (283 tes) lulus di SQLite dan PostgreSQL. UI belum diuji pada Windows.

## Riwayat

| Tahap | Isi | Referensi |
|---|---|---|
| Inisiasi | README, tim, `fileindex.md`, Jekyll | PR #2–#4 |
| Desain class | class diagram, kerangka `Models/`, `Services/`, `ViewModels/` | PR #5 |
| Database | ERD v1, `schema.sql`, `ecokoin.db` | PR #6 |
| Prototipe PBO | WinForms Form1–3, hierarki quest, Npgsql | PR #7 |
| UI produk | WPF dashboard (slicing Figma) + token | PR #8, #9 |
| Backend Core | `EcoKoin.Core` + 156 tes, solusi `EcoKoin.sln` | branch `544058` |
| Integrasi WPF | login/registrasi UI terhubung ke Core | PR #24–#28 |
| Database remote | persyaratan tugas melarang database lokal: `IAppRepository` async + `PostgresRepository` (Npgsql), SQLite hanya untuk tes | pekerjaan saat ini |

## Target vs saat ini

Legenda: ✅ sesuai target · ◐ sebagian · ✗ belum ada.

| Area | Target | Saat ini | Status |
|---|---|---|---|
| Solusi & proyek | `EcoKoin.sln`: `Core`, `WPF`, `Tests` | `EcoKoin.sln` berisi Core + Tests; WPF mereferensikan Core tetapi belum masuk `.sln` (agar bisa dibangun di Linux; build WPF di CI Windows) | ◐ |
| Penyimpanan | PostgreSQL remote, tanpa database lokal | `PostgresRepository` (Npgsql) di Core; SQLite hanya `SqliteTestRepository` di proyek tes | ✅ |
| Antarmuka data | `IAppRepository` async + `DuplicateUsernameException` | ada; `MainViewModel` dan WPF memakainya | ✅ |
| Skema DB | v2 (PostgreSQL) | `src/EcoKoin.Core/Database/schema.sql` + `seed.sql` (embedded resource) | ✅ |
| Hosting database | server PostgreSQL yang dipakai tim/pengguna | **belum diputuskan** (Supabase/Neon/Railway/server kampus); connection string lewat `ECOKOIN_DB_CONNECTION` | ✗ |
| `User`, `DailyQuest` + 3 turunan, `EcoPet`, `PowerSession`, `CarbonFinancialReport` | logika domain lengkap | terimplementasi + dites | ✅ |
| `PowerMonitorService` / `PowerScheme.Activate` | idle Win32, `powercfg` | terimplementasi dengan fake di tes; **belum diuji di Windows asli** | ◐ |
| `WeatherService` | OpenWeatherMap + cache + rekomendasi | terimplementasi + dites dengan fake HTTP; butuh `ECOKOIN_WEATHER_API_KEY` | ✅ |
| `MainViewModel` | MVVM async, command, timer | terimplementasi (`InitializeAsync`, `RefreshDashboardAsync`, `OnTickAsync`, `AsyncRelayCommand`); **belum dipakai UI** (dashboard WPF masih memakai `DashboardViewModel` sendiri); timer belum dipasang | ◐ |
| Login & registrasi WPF | akun tersimpan di DB | `AccountService` → `IAppRepository`; aplikasi keluar dengan pesan jelas bila database tidak terjangkau | ✅ |
| WPF dashboard & halaman | data bind ke `MainViewModel`, 4 halaman | UI dan sebagian data dari tim UI; belum terikat ke `MainViewModel` | ◐ |
| Konfigurasi `appsettings.json` | opsi cuaca/daya/laporan dibaca dari file | belum dibaca (nilai default di kode) | ✗ |
| Prototipe WinForms | arsip referensi | berjalan, PostgreSQL, data simulasi | ✅ sebagai arsip |
| Tes | xUnit, kontrak repository untuk semua implementasi | 171+ tes; kontrak berjalan di SQLite dan PostgreSQL (bila `ECOKOIN_TEST_PG_CONNECTION` diatur) | ✅ |
| CI/CD | build + tes di CI, rilis Windows | `.github/workflows/ci.yml` (Ubuntu + service `postgres:17`; build WPF di Windows) dan `release.yml` (tag `v*`) | ✅ |
| Lisensi | ditentukan | tidak ada | ✗ |

## Roadmap menuju target

1. ~~Satukan solusi, skema, model, logika murni, service, `MainViewModel`~~ (selesai di `EcoKoin.Core`).
2. ~~Pindah ke PostgreSQL remote~~ (`IAppRepository` + `PostgresRepository`; SQLite hanya untuk tes).
3. **Putuskan hosting PostgreSQL** (Supabase/Neon/Railway/server kampus), buat pengguna database dengan hak minimum, bagikan connection string secara privat, dan aktifkan `SSL Mode=Require`.
4. **Hubungkan `MainViewModel` ke WPF:** `DataContext`, timer yang memanggil `OnTickAsync`, `RecordEventAsync(SleepEntered)` saat PC sleep/idle, binding dashboard.
5. **Uji bagian khusus Windows:** `WindowsIdleTimeProvider` dan `powercfg` pada PC nyata (cadangkan skema aktif sebelum menguji).
6. Rapikan WPF: pakai token & style global, embed font, ganti data contoh dengan data nyata.
7. Halaman Misi & Reward, Analisis, Pengaturan + navigasi.
8. Baca `appsettings.json`, tentukan lisensi, tandai prototipe sebagai arsip.

## Asumsi desain yang perlu dikonfirmasi tim

Nilai berikut dipilih untuk melengkapi spesifikasi dan belum datang dari dokumen tugas:

- Faktor emisi `0.87` kg CO₂/kWh dan tarif `Rp1.444,70`/kWh (nilai default, dapat diubah di konfigurasi; perlu diverifikasi sumbernya).
- Ambang EcoPet (≥ 30 koin Segar, 10–29 Normal), Level tiap 500 koin.
- Model estimasi watt hemat (`BaselineWatt` 65 W) dan ambang awan rekomendasi (60% / 30%).
- Target/reward tiga quest contoh (30 / 20 / 15 koin).
- Perubahan skema v2 menyimpang dari ERD Modul 4 (kolom tambahan saja; relasi tetap).
