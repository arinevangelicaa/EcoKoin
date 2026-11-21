# 7. Setup & Menjalankan

> Panduan **kondisi target** (setelah `EcoKoin.Core` dan solusi terpadu tersedia, lihat [dokumen 2](02-architecture.md)). Untuk menjalankan kondisi repo saat ini, lihat bagian terakhir.

## Prasyarat

| Kebutuhan | Keterangan |
|---|---|
| Windows 10/11 | WPF dan API daya hanya tersedia di Windows |
| .NET SDK 10 | build semua proyek |
| Akun OpenWeatherMap + API key | fitur cuaca (gratis) |
| Visual Studio 2022+ / Rider | opsional |
| `sqlite3` CLI | opsional, untuk memeriksa/membuat ulang DB |

## Mengambil kode

```powershell
git clone https://github.com/arinevangelicaa/EcoKoin.git
cd EcoKoin
```

Branch kerja per anggota (`544058`, `534030`, `539400`); perubahan masuk ke `main` lewat pull request.

## Konfigurasi

1. **API key cuaca** (jangan dikomit):
   ```powershell
   $env:ECOKOIN_WEATHER_API_KEY = "<api-key>"
   # atau
   dotnet user-secrets set "Weather:ApiKey" "<api-key>" --project src\EcoKoin.WPF
   ```
2. **Pengaturan lain** di `src/EcoKoin.WPF/appsettings.json` ([dokumen 2](02-architecture.md#konfigurasi-appsettingsjson)):

   | Kunci | Default |
   |---|---|
   | `Database:Path` | `%LOCALAPPDATA%\EcoKoin\ecokoin.db` |
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

Saat pertama dijalankan, aplikasi membuat file DB, menjalankan `schema.sql` dan `seed.sql`, lalu membuat 3 quest harian. Akun demo disediakan oleh `seed.sql`.

Tanpa API key atau tanpa internet, aplikasi tetap berjalan; kartu cuaca menampilkan "Cuaca tidak tersedia".

## Database manual

```bash
rm -f Database/ecokoin.db
sqlite3 Database/ecokoin.db < Database/schema.sql
sqlite3 Database/ecokoin.db < Database/seed.sql
sqlite3 Database/ecokoin.db ".tables"
```

Hasil: `DAILY_QUESTS  ECO_PETS  POWER_SCHEMES  POWER_SESSIONS  USERS`.

## Cakupan tes

- `User`: `AddEcoKoin` (validasi), `Login` (hash), `Level`/`XpInLevel`.
- `DailyQuest` + turunan: `UpdateProgress` dibatasi target, `Claim` idempoten.
- `EcoPet.UpdateState`: batas 30 / 10 koin.
- `CarbonFinancialReport`: rumus CO₂/Rupiah, 7 titik grafik.
- `WeatherService.GetBrightnessRecommendation`: batas 60 / 30 persen awan.
- `SqliteRepository`: round-trip pada DB SQLite in-memory, FK aktif.

## Pelepasan

`dotnet publish src\EcoKoin.WPF -c Release -r win-x64 --self-contained` menghasilkan paket yang dapat dijalankan tanpa .NET terpasang.

## Dokumentasi web

`_config.yml` memakai tema `jekyll-theme-architect` (GitHub Pages). Diagram Mermaid dirender di tampilan repo GitHub; tema Pages tidak merendernya bawaan.

## Menjalankan kondisi repo saat ini

Belum ada `EcoKoin.sln` dan `EcoKoin.Core`. Yang bisa dijalankan sekarang (Windows + .NET SDK 10):

```powershell
cd src\EcoKoin.WPF;                   dotnet run   # dashboard statis
cd src\EcoKoin.WinForms.Prototype;    dotnet run   # prototipe PBO (butuh PostgreSQL + ECOKOIN_DB_PASSWORD agar koin tersimpan)
```

Rincian prototipe: [dokumen 6](06-winforms-prototype.md).
