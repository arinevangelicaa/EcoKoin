# EcoKoin

EcoKoin adalah aplikasi desktop berbasis C# (WPF) yang dirancang untuk mendukung aksi iklim (Climate Action) melalui pemantauan dan efisiensi energi PC secara interaktif.

> *Small Steps, Big Impact*

## EcoKoin Team
Ketua Kelompok: Adnan Abdul Majid - 24/544058/TK/60471
- Anggota 1: Arin Evangelica Patabang - 24/534030/TK/59182
- Anggota 2: Rida Larasati - 24/539400/TK/59821

Project Junior Project TI — Departemen Teknik Elektro dan Teknologi Informasi, Fakultas Teknik, Universitas Gadjah Mada.

## Fitur Utama

1. **Gamified Power Saver & Daily Quest** — dapatkan EcoKoin setiap menjalankan skema hemat daya atau mengaktifkan mode *sleep* saat PC ditinggal.
2. **Weather-Adaptive Recommendation** — data cuaca real-time (OpenWeatherMap) menjadi dasar rekomendasi, misalnya menurunkan kecerahan layar saat mendung.
3. **Interactive Eco-Pet Mascot** — maskot yang segar atau lemas tergantung konsistensi hemat energi.
4. **Carbon & Financial Dashboard** — watt yang dihemat dikonversi menjadi estimasi CO₂ (kg) dan penghematan biaya listrik (Rupiah) dalam grafik mingguan.

## Arsitektur

Aplikasi WPF dengan pola MVVM. Logika domain, service, dan akses data berada di library `EcoKoin.Core`; UI di `EcoKoin.WPF`.

| Lapisan | Isi |
|---|---|
| Model | `User`, `DailyQuest` (+ `SleepQuest`, `PowerSaverQuest`, `WeatherAdaptiveQuest`), `EcoPet`, `PowerScheme`, `PowerSession`, `WeatherData`, `CarbonFinancialReport` |
| Service | `PowerMonitorService` (idle + skema daya), `WeatherService` (OpenWeatherMap), `SqliteRepository` |
| ViewModel / View | `MainViewModel`; Dashboard, Misi & Reward, Analisis, Pengaturan |
| Data | SQLite lokal, 5 tabel (`USERS`, `DAILY_QUESTS`, `ECO_PETS`, `POWER_SCHEMES`, `POWER_SESSIONS`) |

## Struktur Repositori (target)

```
EcoKoin/
├── src/
│   ├── EcoKoin.Core/                   Models, Services, ViewModels, schema.sql
│   ├── EcoKoin.WPF/                    Aplikasi desktop
│   └── EcoKoin.WinForms.Prototype/     Arsip prototipe PBO
├── tests/EcoKoin.Core.Tests/
├── Database/                           schema.sql, seed.sql, ecokoin.db
└── docs/                               Dokumentasi lengkap
```

## Status Saat Ini

Dokumentasi di `docs/` menjelaskan produk dalam kondisi target. Posisi repositori sekarang:

- `EcoKoin.Core` (`src/EcoKoin.Core`) sudah berisi logika domain, `SqliteRepository` (skema v2), `PowerMonitorService`, `WeatherService`, dan `MainViewModel`, dengan 156 unit test (`tests/EcoKoin.Core.Tests`). Build/test dapat dijalankan di Linux: `dotnet test EcoKoin.sln`.
- Dashboard WPF berupa UI statis hasil slicing Figma.
- Prototipe WinForms berjalan sebagai demo PBO dengan PostgreSQL.

Perbandingan target vs saat ini dan roadmap: [docs/08-status-roadmap.md](docs/08-status-roadmap.md).

## Menjalankan

Butuh Windows dan .NET SDK 10.

```powershell
dotnet build EcoKoin.sln
dotnet run --project src\EcoKoin.WPF
```

Set `ECOKOIN_WEATHER_API_KEY` untuk fitur cuaca. Panduan lengkap (dan cara menjalankan kondisi repo saat ini): [docs/07-setup.md](docs/07-setup.md).

## Dokumentasi

| Dokumen | Isi |
|---|---|
| [Ringkasan Produk](docs/01-overview.md) | Latar belakang, fitur, kompetitor |
| [Arsitektur](docs/02-architecture.md) | Struktur repo, komponen, alur data |
| [Model Domain](docs/03-domain-model.md) | Referensi class + class diagram |
| [Basis Data](docs/04-database.md) | Skema SQLite, ERD, cara build ulang |
| [Aplikasi WPF](docs/05-wpf-app.md) | Layout dashboard, design token |
| [Prototipe WinForms](docs/06-winforms-prototype.md) | Konsep PBO, akses PostgreSQL |
| [Setup & Menjalankan](docs/07-setup.md) | Prasyarat, build, konfigurasi |
| [Status & Roadmap](docs/08-status-roadmap.md) | Matriks fitur, masalah, langkah berikutnya |

Indeks lengkap: [docs/README.md](docs/README.md).

## Class Diagram
<img width="1115" height="738" alt="image" src="https://github.com/user-attachments/assets/ce5e2815-8b6a-4683-a0b4-2d74fe245b8a" />
