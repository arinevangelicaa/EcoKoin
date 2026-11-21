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
│   │   ├── Services/                 PowerMonitorService, WeatherService, SqliteRepository
│   │   ├── ViewModels/               MainViewModel (+ ViewModel halaman lain)
│   │   ├── Database/schema.sql       Skema SQLite (embedded / disalin ke output)
│   │   └── EcoKoin.Core.csproj
│   ├── EcoKoin.WPF/                  Aplikasi desktop (View)
│   │   ├── Assets/Images/            logo, happy, netral, sad
│   │   ├── Resources/                Colors.xaml, Typography.xaml, Styles.xaml, Fonts/
│   │   ├── Views/                    DashboardView, MissionView, AnalysisView, SettingsView
│   │   ├── App.xaml, MainWindow.xaml
│   │   └── appsettings.json
│   └── EcoKoin.WinForms.Prototype/   Arsip prototipe PBO (bukan bagian rilis)
├── tests/EcoKoin.Core.Tests/         Unit test domain & service
├── Database/                         schema.sql (sumber), ecokoin.db (contoh), seed.sql
├── docs/                             Dokumentasi
├── README.md, fileindex.md, _config.yml
```

## Lapisan

| Lapisan | Proyek | Tanggung jawab | Ketergantungan |
|---|---|---|---|
| View | `EcoKoin.WPF` | XAML, binding, navigasi, tema | `EcoKoin.Core` |
| ViewModel | `EcoKoin.Core/ViewModels` | State layar, `ICommand`, timer refresh | Services, Models |
| Service | `EcoKoin.Core/Services` | Daya OS, HTTP cuaca, akses SQLite | OS API, HttpClient, `Microsoft.Data.Sqlite` |
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
│PowerMonitorService│  │  WeatherService  │  │ SqliteRepository │
│ idle · skema · W  │  │ OpenWeatherMap   │  │ ecokoin.db       │
└─────────┬────────┘  └─────────┬────────┘  └────────┬─────────┘
   Win32/powercfg           HTTPS (HttpClient)     SQLite file
```

## Alur data

1. **Startup.** `App` membuat `SqliteRepository` → `InitializeDatabase()` (membuat file DB bila belum ada) → `MainViewModel.Initialize()` memuat `User`, quest hari ini, `EcoPet`, skema daya.
2. **Pemantauan daya (tiap menit).** `PowerMonitorService.DetectIdleState()` membandingkan waktu input terakhir dengan `IdleThresholdMinutes`. Saat idle lebih lama dari ambang, rekomendasi sleep muncul; saat skema hemat aktif, sesi dibuka (`PowerSession.StartTime`).
3. **Menutup sesi.** Saat skema diganti / PC aktif lagi: `EndTime` diisi, `CalculateWattSaved()` menghitung penghematan, `SavePowerSession` menyimpan, progres quest terkait naik.
4. **Cuaca (tiap 10 menit).** `WeatherService.FetchCurrentWeather()` → `WeatherData` → `GetBrightnessRecommendation()` → kartu "Rekomendasi Adaptif Cuaca". Jika gagal (offline), data terakhir dipakai dan ditandai usang.
5. **Quest & reward.** Event (sesi selesai, rekomendasi diterapkan, sleep) memanggil `DailyQuest.Evaluate(...)`. Quest selesai bisa di-*claim*: `User.AddEcoKoin(reward)`, `SaveQuestProgress`, `UpdateUserProfile`, lalu `EcoPet.UpdateState(koinHariIni)`.
6. **Laporan.** `GetWeeklyReportData()` → `CarbonFinancialReport` menghitung kWh, CO₂, Rupiah, dan 7 titik grafik harian.

## Konfigurasi (`appsettings.json`)

```json
{
  "Database":  { "Path": "%LOCALAPPDATA%\\EcoKoin\\ecokoin.db" },
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
| Penyimpanan | SQLite via `Microsoft.Data.Sqlite`, FK aktif per koneksi |
| Cuaca | OpenWeatherMap Current Weather API (`HttpClient`, `System.Text.Json`) |
| Daya | Win32 `GetLastInputInfo`, `powercfg` |
| Tes | xUnit |
| Desain | Figma → token XAML |
| Dokumentasi web | Jekyll `jekyll-theme-architect` (GitHub Pages) |

## Keputusan desain

- **SQLite, satu file lokal.** Tanpa server; cocok untuk aplikasi desktop per pengguna. Prototipe PostgreSQL tidak dilanjutkan.
- **`WeatherData` & `CarbonFinancialReport` bukan tabel.** Cuaca sementara; laporan dihitung dari `POWER_SESSIONS`.
- **Estimasi, bukan pengukuran.** Watt hemat adalah estimasi model (lihat [dokumen 3](03-domain-model.md)); UI menyebutnya "estimasi".
- **Offline-first.** Seluruh fitur kecuali cuaca bekerja tanpa jaringan.
