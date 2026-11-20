# 5. Aplikasi WPF

> Spesifikasi **kondisi target**. Lokasi: `src/EcoKoin.WPF/` (`net10.0-windows`, `UseWPF`), mereferensikan `EcoKoin.Core`. Layout dasar mengikuti slicing Figma yang sudah ada.

## Window & navigasi

`MainWindow`: judul "EcoKoin - Dashboard", 1440×1024 (min 1100×720), tengah layar, latar `CanvasBrush`. Grid dua kolom: **sidebar** + **area konten** yang menampilkan satu `View` sesuai menu terpilih (`ContentControl` + `DataTemplate` per ViewModel).

### Sidebar
- Logo, "EcoKoin", tagline "Small Steps, Big Impact".
- Avatar, nama, dan email/username **pengguna aktif** (dari `CurrentUser`).
- Menu: **Dashboard**, **Misi & Reward**, **Analisis**, **Pengaturan**. Item aktif ber-highlight (latar Misty, teks Sun Orange). Klik memanggil `NavigateCommand`.

## Halaman

### Dashboard
| Baris | Komponen | Sumber data |
|---|---|---|
| Header | "Hai, {Username}!", subjudul, chip kota | `CurrentUser.Username`, `Weather:City` |
| Kartu statistik ×4 | TOTAL ECOKOIN (+ koin minggu ini) · CUACA HARI INI (suhu + kondisi) · KELEMBAPAN UDARA · STATUS DAYA (persen + On Battery/Charging) | `User.TotalEcoKoin`, `WeatherData`, status baterai OS |
| ECOPET | gambar sesuai `PetState`, pesan, label kondisi, Level & `XpInLevel / 500 XP` | `EcoPet`, `User.Level` |
| Rekomendasi Adaptif Cuaca | rekomendasi kecerahan dan rekomendasi sleep idle; tombol *Terapkan* | `GetBrightnessRecommendation`, `DetectIdleState` |
| Analisis Mingguan (ringkas) | Energi dihemat, CO₂ dihindari, Biaya dihemat, grafik 7 hari, "Lihat Selengkapnya" → halaman Analisis | `CarbonFinancialReport` |
| Daily Quest (ringkas) | "N dari 3 misi selesai" + persen, 3 quest dengan tombol *Klaim +N* / status | `User.Quests` |

### Misi & Reward
Daftar lengkap quest hari ini (progres, target, reward, tombol klaim), total EcoKoin, Level/XP, riwayat klaim.

### Analisis
Ringkasan energi/CO₂/Rupiah, grafik harian 7 hari (kWh), insight otomatis (mis. "Penghematan X% lebih baik dari minggu lalu", "Hari paling hemat: Kamis").

### Pengaturan
Kota cuaca, ambang idle, skema daya default, faktor emisi & tarif listrik, status API key cuaca (ada/tidak; nilai tidak ditampilkan).

## Perilaku

- **Binding & command:** semua teks dan angka di-*bind* ke ViewModel; tombol memakai `ICommand`. Tidak ada logika bisnis di code-behind.
- **Status tombol:** *Klaim* aktif hanya bila quest selesai dan belum diklaim; setelah klaim menjadi "Diklaim" (nonaktif).
- **Refresh:** daya tiap 1 menit, cuaca tiap 10 menit, dashboard segera setelah klaim/terapkan.
- **EcoPet:** gambar berganti otomatis mengikuti `PetState` (Segar → `happy.png`, Normal → `netral.png`, Lemas → `sad.png`).
- **Keadaan khusus:** cuaca tidak tersedia → kartu menampilkan "Cuaca tidak tersedia" dan data terakhir dengan penanda usang; DB gagal dibuka → dialog galat + keluar dengan aman; loading → placeholder, UI tidak membeku (pemanggilan I/O `async`).
- Grafik 7 hari adalah data nyata dari `GenerateWeeklyChart()`, bukan ilustrasi.

## Design system

Satu sumber token: `Resources/*.xaml` (di-merge di `App.xaml`); `MainWindow` dan semua View hanya memakai `StaticResource`/`DynamicResource` dari sana, tanpa brush atau hex lokal.

### Warna (`Colors.xaml`)
| Key | Hex | Peran |
|---|---|---|
| `ForestGreen` | `#2C4C3E` | sidebar, teks utama |
| `OceanCyan` | `#B4D4D1` | kartu dalam |
| `Misty` | `#E9F2F1` | kartu utama |
| `Blackberry` | `#222826` | teks gelap |
| `SunOrange` | `#D97746` | aksen, tombol aksi |
| `Canvas` | `#F6F8F7` | latar |
| `Forest70`, `Forest20`, `Misty70` | `#B32C4C3E`, `#332C4C3E`, `#B3E9F2F1` | varian transparan |

Tiap warna tersedia sebagai `Color` dan `SolidColorBrush`.

### Tipografi (`Typography.xaml`)
Heading **Poppins**, body **Inter** — di-*embed* di `Resources/Fonts/` sehingga tidak bergantung pada font terpasang.

| Style | Font | Ukuran | Berat |
|---|---|---|---|
| `H1Style` | Poppins | 48 | SemiBold |
| `H2Style` | Poppins | 32 | SemiBold |
| `H3Style` | Poppins | 24 | SemiBold |
| `H4Style` | Poppins | 18 | SemiBold |
| `H5Style` | Poppins | 16 | SemiBold |
| `BodyLargeStyle` | Inter | 16 | Normal |
| `BodyMediumStyle` | Inter | 14 | Normal |
| `BodySmallStyle` | Inter | 12 | Normal |
| `ButtonTextStyle` | Poppins | 14 | Medium |

### Komponen (`Styles.xaml`)
| Style | Target | Latar | Radius | Padding | Dipakai di |
|---|---|---|---|---|---|
| `DashboardCardStyle` | Border | Misty | 24 | 24 | EcoPet, rekomendasi, analisis, quest |
| `StatCardStyle` | Border | Misty | 24 | 16,20 | kartu statistik |
| `InnerCardStyle` | Border | Ocean Cyan | 20 | 16 | item rekomendasi, misi, grafik |
| `PrimaryButtonStyle` | Button | Sun Orange | 10 | 15,8 | Terapkan, Klaim |
| `SidebarButtonStyle` | Button | transparan; aktif Misty | 10 | — | menu sidebar |

Aksesibilitas: kontras teks Forest/Blackberry di atas Misty/Cyan memenuhi WCAG AA; semua tombol dapat difokus keyboard dan punya `AutomationProperties.Name`.
