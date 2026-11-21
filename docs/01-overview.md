# 1. Ringkasan Produk

## Identitas

| | |
|---|---|
| Nama produk | EcoKoin |
| Jenis | Aplikasi desktop (C#, WPF target utama) |
| Tagline (UI) | *Small Steps, Big Impact* |
| Konteks | Project Junior Project TI — Departemen Teknik Elektro dan Teknologi Informasi, Fakultas Teknik, Universitas Gadjah Mada |
| Tema | Climate Action — efisiensi energi PC |

## Tim

| Peran | Nama | NIM |
|---|---|---|
| Ketua | Adnan Abdul Majid | 24/544058/TK/60471 |
| Anggota 1 | Arin Evangelica Patabang | 24/534030/TK/59182 |
| Anggota 2 | Rida Larasati | 24/539400/TK/59821 |

Nama branch git (`544058`, `534030`, `539400`) mengikuti NIM anggota.

## Masalah yang dipecahkan

Konsumsi daya PC sering boros karena pengguna lupa mengganti skema daya, membiarkan layar terlalu terang, atau meninggalkan PC dalam keadaan menyala. Tool bawaan (Windows Power Options, Razer Cortex) bersifat pasif: tidak ada umpan balik, motivasi, maupun gambaran dampak ke emisi karbon dan biaya listrik.

## Solusi

Aplikasi yang memantau konsumsi daya PC dan membungkusnya dengan elemen permainan serta data cuaca *real-time*, sehingga hemat energi menjadi kebiasaan yang dihargai.

## Fitur utama (rancangan produk)

1. **Gamified Power Saver & Daily Quest** — pengguna mendapat *reward* **EcoKoin** setiap menjalankan skema hemat daya atau mengaktifkan mode *sleep* saat PC ditinggal.
2. **Weather-Adaptive Recommendation (OpenWeatherMap API)** — data cuaca real-time dipakai untuk rekomendasi otomatis (contoh: turunkan kecerahan layar saat mendung).
3. **Interactive Eco-Pet Mascot** — maskot desktop yang berubah kondisi (segar vs lemas) mengikuti konsistensi pengguna mengumpulkan poin hemat energi.
4. **Carbon & Financial Dashboard** — watt yang dihemat dikonversi menjadi estimasi penurunan emisi CO₂ (kg) dan penghematan biaya listrik (Rupiah), ditampilkan sebagai grafik mingguan.

> Dokumen 1–7 menggambarkan produk dalam **kondisi target** (semua fitur di atas berfungsi). Posisi implementasi saat ini dibandingkan dengan target di [Status & Roadmap](08-status-roadmap.md).

## Pengguna & skenario

Pengguna: mahasiswa/pekerja yang memakai PC/laptop Windows berjam-jam. Skenario harian:

1. Membuka aplikasi → dashboard menampilkan saldo EcoKoin, cuaca, status daya, EcoPet, dan 3 Daily Quest.
2. Cuaca berawan → aplikasi menyarankan menurunkan kecerahan; pengguna menekan *Terapkan* → quest "rekomendasi cuaca" selesai.
3. Pengguna meninggalkan PC → idle melewati ambang → aplikasi menyarankan/menerapkan sleep; sesi hemat daya tercatat dengan estimasi watt hemat.
4. Pengguna menekan *Klaim* pada quest selesai → EcoKoin bertambah, EcoPet menjadi lebih segar.
5. Halaman Analisis menunjukkan energi, CO₂, dan Rupiah yang dihemat selama 7 hari.

## Pembeda dari aplikasi sejenis

| Kompetitor | Fokus | Perbedaan EcoKoin |
|---|---|---|
| Forest App | Produktivitas via gamifikasi menanam pohon virtual di HP | EcoKoin berfokus pada penghematan daya fisik PC dan integrasi cuaca |
| Windows Power Options / Razer Cortex | Pengatur profil daya | Pasif; tanpa *reward*, statistik emisi, atau daya tarik visual |
| Desktop Mascot / Tamagotchi apps | Maskot hiburan | Maskot EcoKoin terikat langsung ke logika efisiensi energi dan kondisi PBO aplikasi |

## Identitas visual

Warna dan tipografi diambil dari desain Figma (token ada di `src/EcoKoin.WPF/Resources/`, lihat [Aplikasi WPF](05-wpf-app.md)):

| Token | Hex | Peran |
|---|---|---|
| Forest Green | `#2C4C3E` | Sidebar, teks utama |
| Ocean Cyan | `#B4D4D1` | Kartu bagian dalam |
| Misty | `#E9F2F1` | Kartu utama |
| Blackberry | `#222826` | Teks gelap |
| Sun Orange | `#D97746` | Aksen / tombol aksi |
| Canvas | `#F6F8F7` | Latar dashboard |

Heading memakai **Poppins**, body memakai **Inter**.
