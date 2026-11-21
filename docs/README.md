# Dokumentasi EcoKoin

Dokumentasi lengkap EcoKoin dalam **kondisi target** (produk selesai: semua fitur berfungsi, satu solusi terpadu, SQLite, MVVM). Dokumen 1–7 adalah spesifikasi target. Posisi repositori saat ini (branch `544058`, setelah merge PR #9) dan selisihnya terhadap target dicatat di [Status & Roadmap](08-status-roadmap.md).

## Daftar isi

| No | Dokumen | Isi |
|---|---|---|
| 1 | [Ringkasan Produk](01-overview.md) | Latar belakang, masalah, solusi, fitur, kompetitor, tim |
| 2 | [Arsitektur & Struktur Repositori](02-architecture.md) | Layout folder, komponen, alur data, hubungan antar bagian |
| 3 | [Model Domain](03-domain-model.md) | Referensi class `Models/`, `Services/`, `ViewModels/` + class diagram |
| 4 | [Basis Data](04-database.md) | Skema SQLite, DDL, cara membuat ulang, pemakaian dari kode |
| 5 | [Aplikasi WPF](05-wpf-app.md) | Struktur UI dashboard, design token, status implementasi |
| 6 | [Prototipe WinForms (arsip)](06-winforms-prototype.md) | Prototipe PBO (inheritance, polymorphism), koneksi PostgreSQL |
| 7 | [Setup & Menjalankan](07-setup.md) | Prasyarat, build, run, konfigurasi |
| 8 | [Status & Roadmap](08-status-roadmap.md) | Target vs kondisi repo saat ini, roadmap, asumsi yang perlu dikonfirmasi |

Dokumen yang sudah ada sebelumnya:

- [ERD.md](ERD.md) — Entity Relationship Diagram (Mermaid) dan penjelasan relasi
- [DATABASE_PROOF.md](DATABASE_PROOF.md) — bukti pembuatan database + sample data
- [erd-hw.pdf](erd-hw.pdf) — sumber ERD (Modul 4, Perancangan Basis Data)

## Gambaran satu paragraf

EcoKoin adalah aplikasi desktop C# (WPF, MVVM) yang menggabungkan pemantauan daya PC, gamifikasi (EcoKoin, Daily Quest, EcoPet), dan data cuaca OpenWeatherMap untuk mendorong penghematan energi, dengan data tersimpan di SQLite lokal. Logika domain berada di `EcoKoin.Core`, UI di `EcoKoin.WPF`; prototipe WinForms disimpan sebagai arsip PBO.
