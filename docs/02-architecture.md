# 2. Arsitektur & Struktur Repositori

> Dokumen ini menggambarkan **kondisi target** (produk selesai). Selisih terhadap isi repositori saat ini ada di [Status & Roadmap](08-status-roadmap.md).

## Struktur solusi

```
EcoKoin/
├── EcoKoin.sln
├── src/
│   ├── EcoKoin.Core/                 Class library: domain, service, repository, ViewModel
│   │   ├── Models/                   User, DailyQuest (+ turunan), EcoPet, PetState, PowerScheme,
│   │   │                             PowerSession, WeatherData, CarbonFinancialReport
│   │   ├── Services/                 PowerMonitorService, WeatherService, IAppRepository, PostgresRepository
│   │   ├── ViewModels/               MainViewModel (+ ViewModel halaman lain)
│   │   ├── Database/schema.sql, seed.sql  Skema PostgreSQL v2 (embedded resource)
│   │   └── EcoKoin.Core.csproj
│   ├── EcoKoin.WPF/                  Aplikasi desktop (View)
│   │   ├── Assets/Images/            logo, happy, netral, sad
│   │   ├── Resources/                Colors.xaml, Typography.xaml, Styles.xaml, Fonts/
│   │   ├── Views/                    DashboardView, MissionView, AnalysisView, SettingsView
│   │   ├── App.xaml, MainWindow.xaml
│   │   └── appsettings.json
│   └── EcoKoin.WinForms.Prototype/   Arsip prototipe PBO (bukan bagian rilis)
├── tests/EcoKoin.Core.Tests/         Unit test domain & service
├── Database/                         arsip bukti ERD v1 (ecokoin.db SQLite, tidak dipakai aplikasi)
├── docs/                             Dokumentasi
├── README.md, fileindex.md, _config.yml
```

## Lapisan

| Lapisan | Proyek | Tanggung jawab | Ketergantungan |
|---|---|---|---|
| View | `EcoKoin.WPF` | XAML, binding, navigasi, tema | `EcoKoin.Core` |
| ViewModel | `EcoKoin.Core/ViewModels` | State layar, `ICommand`, timer refresh | Services, Models |
| Service | `EcoKoin.Core/Services` | Daya OS, HTTP cuaca, akses PostgreSQL (`IAppRepository`) | OS API, HttpClient, `Npgsql` |
| Model | `EcoKoin.Core/Models` | Aturan domain (quest, pet, laporan) | — (tanpa I/O) |

Pola: **MVVM** + **Repository**. Model murni (tanpa akses OS/DB) agar mudah diuji; semua I/O lewat service.

## Diagram komponen

```
┌────────────────────────── WPF View ──────────────────────────┐
│ MainWindow ─ Dashboard │ Misi & Reward │ Analisis │ Pengaturan│
└─────────────────────────────┬─────────────────────────────────┘
                              │ DataContext / binding / ICommand
                       ┌──────▼───────┐
                       │ MainViewModel │  timer: daya 1 mnt · cuaca 10 mnt
                       └──┬─────┬───┬──┘
          ┌───────────────┘     │   └────────────────┐
┌─────────▼────────┐  ┌─────────▼────────┐  ┌────────▼─────────┐
│PowerMonitorService│  │  WeatherService  │  │PostgresRepository│
│ idle · skema · W  │  │ OpenWeatherMap   │  │ (IAppRepository) │
└─────────┬────────┘  └─────────┬────────┘  └────────┬─────────┘
   Win32/powercfg           HTTPS (HttpClient)     TCP/TLS (Npgsql)
                                                       │
                                              PostgreSQL (server remote)
```

## Alur data

1. **Startup.** `App` membaca connection string dari environment variable `ECOKOIN_DB_CONNECTION` (`DatabaseConfig.CreateFromEnvironment()` → `PostgresRepository`) → `InitializeDatabaseAsync()` (membuat tabel bila belum ada, idempoten). Jika variabel belum diatur atau server tidak terjangkau, aplikasi menampilkan pesan dan keluar. Setelah login, `MainViewModel.InitializeAsync(username)` memuat `User`, quest hari ini, `EcoPet`, skema daya.
2. **Pemantauan daya (tiap menit).** `PowerMonitorService.DetectIdleState()` membandingkan waktu input terakhir dengan `IdleThresholdMinutes`. Saat idle lebih lama dari ambang, rekomendasi sleep muncul; saat skema hemat aktif, sesi dibuka (`PowerSession.StartTime`).
3. **Menutup sesi.** Saat skema diganti / PC aktif lagi: `EndTime` diisi, `CalculateWattSaved()` menghitung penghematan, `SavePowerSessionAsync` menyimpan, progres quest terkait naik.
4. **Cuaca (tiap 10 menit).** `WeatherService.FetchCurrentWeather()` → `WeatherData` → `GetBrightnessRecommendation()` → kartu "Rekomendasi Adaptif Cuaca". Jika gagal (offline), data terakhir dipakai dan ditandai usang.
5. **Quest & reward.** Event (sesi selesai, rekomendasi diterapkan, sleep) memanggil `DailyQuest.Evaluate(...)`. Quest selesai bisa di-*claim*: `User.AddEcoKoin(reward)`, `SaveQuestProgressAsync`, `UpdateUserProfileAsync`, lalu `EcoPet.UpdateState(koinHariIni)`.
6. **Laporan.** `GetWeeklyReportDataAsync()` → `CarbonFinancialReport` menghitung kWh, CO₂, Rupiah, dan 7 titik grafik harian.

## Konfigurasi

Koneksi database **tidak** disimpan di file mana pun di repo. Atur lewat environment variable:

```powershell
$env:ECOKOIN_DB_CONNECTION = "Host=<host>;Port=5432;Database=<db>;Username=<user>;Password=<password>;SSL Mode=Require"
```

Pengaturan lain (`appsettings.json`):

```json
{
  "Weather":   { "City": "Yogyakarta", "CacheMinutes": 10 },
  "Power":     { "IdleThresholdMinutes": 5, "BaselineWatt": 65 },
  "Report":    { "EmissionFactorKgPerKwh": 0.87, "TariffRupiahPerKwh": 1444.70 }
}
```

API key OpenWeatherMap **tidak** disimpan di file ini: dibaca dari environment variable `ECOKOIN_WEATHER_API_KEY` atau .NET user-secrets.

## Teknologi

| Aspek | Pilihan |
|---|---|
| Bahasa / runtime | C#, .NET `net10.0-windows` (nullable & implicit usings aktif) |
| UI | WPF (MVVM) |
| Penyimpanan | PostgreSQL remote via `Npgsql` (`IAppRepository` async); SQLite hanya di proyek tes |
| Cuaca | OpenWeatherMap Current Weather API (`HttpClient`, `System.Text.Json`) |
| Daya | Win32 `GetLastInputInfo`, `powercfg` |
| Tes | xUnit |
| Desain | Figma → token XAML |
| Dokumentasi web | Jekyll `jekyll-theme-architect` (GitHub Pages) |

## Keputusan desain

- **PostgreSQL remote, bukan database lokal.** Persyaratan tugas melarang database lokal. Akses data lewat antarmuka `IAppRepository` (async) sehingga penyedia data dapat diganti; kredensial hanya dari environment variable. SQLite (`SqliteTestRepository`) hanya ada di proyek tes agar tes tetap cepat dan offline.
- **`WeatherData` & `CarbonFinancialReport` bukan tabel.** Cuaca sementara; laporan dihitung dari `POWER_SESSIONS`.
- **Estimasi, bukan pengukuran.** Watt hemat adalah estimasi model (lihat [dokumen 3](03-domain-model.md)); UI menyebutnya "estimasi".
- **Butuh jaringan.** Tanpa koneksi ke database aplikasi tidak dapat login; tanpa API key/internet hanya kartu cuaca yang tidak tersedia.
