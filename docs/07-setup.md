# 7. Setup & Menjalankan

> Panduan **kondisi target** (setelah `EcoKoin.Core` dan solusi terpadu tersedia, lihat [dokumen 2](02-architecture.md)). Untuk menjalankan kondisi repo saat ini, lihat bagian terakhir.

## Prasyarat

| Kebutuhan | Keterangan |
|---|---|
| Windows 10/11 | WPF dan API daya hanya tersedia di Windows |
| .NET SDK 10 | build semua proyek |
| Akun OpenWeatherMap + API key | fitur cuaca (gratis) |
| Visual Studio 2022+ / Rider | opsional |
| Server PostgreSQL 15+ | **wajib**; database remote (Supabase, Neon, Railway, server kampus, dll.). Aplikasi tidak memakai database lokal |
| `psql` CLI | opsional, untuk memeriksa/membuat ulang tabel |
| Podman/Docker | opsional, untuk PostgreSQL lokal saat pengembangan/tes |

## Mengambil kode

```powershell
git clone https://github.com/arinevangelicaa/EcoKoin.git
cd EcoKoin
```

Branch kerja per anggota (`544058`, `534030`, `539400`); perubahan masuk ke `main` lewat pull request.

## Konfigurasi

1. **Koneksi database** (wajib, jangan dikomit). Format connection string Npgsql; ganti placeholder dengan data dari penyedia hosting:
   ```powershell
   # hanya untuk sesi PowerShell ini
   $env:ECOKOIN_DB_CONNECTION = "Host=<host>;Port=5432;Database=<db>;Username=<user>;Password=<password>;SSL Mode=Require"

   # permanen untuk akun Windows ini (berlaku di jendela terminal baru)
   setx ECOKOIN_DB_CONNECTION "Host=<host>;Port=5432;Database=<db>;Username=<user>;Password=<password>;SSL Mode=Require"
   ```
   Pengguna database harus boleh `CREATE TABLE` di schema-nya pada **run pertama** (aplikasi membuat tabel dan data awal otomatis; berikutnya cukup hak baca/tulis). Bagikan connection string ke anggota tim lewat kanal privat, bukan lewat repo/issue/chat publik. Bila variabel belum diatur atau server tidak terjangkau, aplikasi menampilkan pesan kesalahan lalu keluar.
2. **API key cuaca** (jangan dikomit):
   ```powershell
   $env:ECOKOIN_WEATHER_API_KEY = "<api-key>"
   # atau
   dotnet user-secrets set "Weather:ApiKey" "<api-key>" --project src\EcoKoin.WPF
   ```
3. **Pengaturan lain** di `src/EcoKoin.WPF/appsettings.json` ([dokumen 2](02-architecture.md#konfigurasi-appsettingsjson)):

   | Kunci | Default |
   |---|---|
   | `Weather:City` | `Yogyakarta` |
   | `Weather:CacheMinutes` | `10` |
   | `Power:IdleThresholdMinutes` | `5` |
   | `Power:BaselineWatt` | `65` |
   | `Report:EmissionFactorKgPerKwh` | `0.87` |
   | `Report:TariffRupiahPerKwh` | `1444.70` |

## Build, tes, jalankan

```powershell
dotnet build EcoKoin.sln
dotnet test  tests\EcoKoin.Core.Tests
dotnet run --project src\EcoKoin.WPF
```

Saat pertama dijalankan, aplikasi terhubung ke PostgreSQL, menjalankan `schema.sql` dan `seed.sql` (idempoten), lalu membuat 3 quest harian per pengguna. Buat akun lewat halaman registrasi.

Tanpa API key, kartu cuaca menampilkan "Cuaca tidak tersedia". Tanpa internet/akses ke server database, login dan registrasi tidak dapat dilakukan.

## Database manual

```bash
psql "host=<host> port=5432 dbname=<db> user=<user> sslmode=require" \
     -f src/EcoKoin.Core/Database/schema.sql \
     -f src/EcoKoin.Core/Database/seed.sql
psql "host=<host> dbname=<db> user=<user> sslmode=require" -c "\dt"
```

Hasil: `daily_quests`, `eco_pets`, `power_schemes`, `power_sessions`, `users`. Rincian dan query: [dokumen 4](04-database.md).

### PostgreSQL lokal untuk pengembangan dan tes (opsional)

Hanya untuk laptop pengembang/CI, bukan untuk pengguna akhir. Contoh dengan Podman (Docker sama, ganti `podman` dengan `docker`):

```bash
podman run --rm -d --name ecokoin-pg -e POSTGRES_USER=ecokoin -e POSTGRES_PASSWORD=<password-sementara> \
  -e POSTGRES_DB=ecokoin_test -p 5432:5432 docker.io/library/postgres:17
export ECOKOIN_TEST_PG_CONNECTION="Host=localhost;Port=5432;Database=ecokoin_test;Username=ecokoin;Password=<password-sementara>"
dotnet test EcoKoin.sln
podman stop ecokoin-pg
```

`ECOKOIN_TEST_PG_CONNECTION` mengaktifkan tes kontrak PostgreSQL (`PostgresRepositoryContractTests`); tanpa variabel itu tes tersebut dilewati dan hanya tes SQLite/unit yang jalan. Gunakan database khusus tes, bukan database bersama tim. Di GitHub Actions, job `core` menyalakan container `postgres:17` dan mengisi variabel ini secara otomatis (password sekali pakai milik container sementara).

## Cakupan tes

- `User`: `AddEcoKoin` (validasi), `Login` (hash), `Level`/`XpInLevel`.
- `DailyQuest` + turunan: `UpdateProgress` dibatasi target, `Claim` idempoten.
- `EcoPet.UpdateState`: batas 30 / 10 koin.
- `CarbonFinancialReport`: rumus CO₂/Rupiah, 7 titik grafik.
- `WeatherService.GetBrightnessRecommendation`: batas 60 / 30 persen awan.
- `IAppRepository`: suite kontrak yang sama dijalankan pada SQLite sementara (tes) dan PostgreSQL asli (bila `ECOKOIN_TEST_PG_CONNECTION` diatur): round-trip, FK, `CHECK`, `DuplicateUsernameException`.
- `MainViewModel` / `AsyncRelayCommand`: alur async, galat command, dan state dashboard.

## Pelepasan

`dotnet publish src\EcoKoin.WPF -c Release -r win-x64 --self-contained` menghasilkan paket yang dapat dijalankan tanpa .NET terpasang.

## Dokumentasi web

`_config.yml` memakai tema `jekyll-theme-architect` (GitHub Pages). Diagram Mermaid dirender di tampilan repo GitHub; tema Pages tidak merendernya bawaan.

## Menjalankan kondisi repo saat ini

`EcoKoin.sln` (Core + Tests) sudah ada dan dapat dibangun serta diuji di Linux/macOS/Windows. `EcoKoin.WPF` mereferensikan Core; build dan jalankan di Windows (belum masuk `EcoKoin.sln` agar solusi tetap bisa dibangun di Linux):

```powershell
$env:ECOKOIN_DB_CONNECTION = "Host=<host>;Port=5432;Database=<db>;Username=<user>;Password=<password>;SSL Mode=Require"
dotnet test EcoKoin.sln
dotnet run --project src\EcoKoin.WPF
cd src\EcoKoin.WinForms.Prototype;   dotnet run   # prototipe PBO (arsip; butuh PostgreSQL + ECOKOIN_DB_PASSWORD agar koin tersimpan)
```

Rincian prototipe: [dokumen 6](06-winforms-prototype.md).
