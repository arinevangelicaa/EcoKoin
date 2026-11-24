using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using EcoKoin.Models;

namespace EcoKoin.WPF.ViewModels
{
    // EcoPet block (dashboard + Misi & Reward header) and the sleep recommendation card.
    public sealed partial class DashboardViewModel
    {
        private const int PetSegarKoin = 30;
        private static readonly Dictionary<string, ImageSource> PetImageCache = new(StringComparer.OrdinalIgnoreCase);

        // Neutral placeholder until the first database read, so the pet slot is never empty.
        private ImageSource? _petImageSource = LoadPetImage("netral.png");
        private string _petStateDisplay = "Memuat EcoPet…";
        private string _petMessageDisplay = "Memuat data EcoPet…";
        private string _petHeadlineDisplay = "EcoPet kamu";
        private string _petHintDisplay = "";
        private bool _canApplySleepRecommendation;

        public ImageSource? PetImageSource { get => _petImageSource; private set => SetProperty(ref _petImageSource, value); }
        public string PetStateDisplay { get => _petStateDisplay; private set => SetProperty(ref _petStateDisplay, value); }
        public string PetMessageDisplay { get => _petMessageDisplay; private set => SetProperty(ref _petMessageDisplay, value); }
        public string PetHeadlineDisplay { get => _petHeadlineDisplay; private set => SetProperty(ref _petHeadlineDisplay, value); }
        public string PetHintDisplay { get => _petHintDisplay; private set => SetProperty(ref _petHintDisplay, value); }

        public bool CanApplySleepRecommendation
        {
            get => _canApplySleepRecommendation;
            private set => SetProperty(ref _canApplySleepRecommendation, value);
        }

        /// <summary>Shows the pet for <paramref name="state"/>; <paramref name="koinToday"/> is the koin claimed today.</summary>
        internal void ShowPet(PetState state, string imageFile, int koinToday)
        {
            PetImageSource = LoadPetImage(imageFile) ?? PetImageSource;
            switch (state)
            {
                case PetState.Segar:
                    PetStateDisplay = "Segar & Sehat";
                    PetMessageDisplay = $"Yeay! Kamu sudah mengumpulkan {koinToday} koin hari ini.";
                    PetHeadlineDisplay = "Langkah kecilmu bikin aku tumbuh!";
                    PetHintDisplay = "Pertahankan kebiasaan hematmu supaya EcoPet tetap sehat.";
                    break;
                case PetState.Normal:
                    PetStateDisplay = "Cukup Semangat";
                    PetMessageDisplay = $"Lumayan! Kamu baru mengumpulkan {koinToday} koin hari ini.";
                    PetHeadlineDisplay = "Sedikit lagi aku segar!";
                    PetHintDisplay = $"Kumpulkan {Math.Max(1, PetSegarKoin - koinToday)} koin lagi hari ini supaya EcoPet kembali segar.";
                    break;
                default:
                    PetStateDisplay = "Butuh Hemat Energi";
                    PetMessageDisplay = koinToday > 0
                        ? $"Aku masih lemas, kamu baru mengumpulkan {koinToday} koin hari ini."
                        : "Aku lemas, kamu belum mengklaim koin hari ini.";
                    PetHeadlineDisplay = "Ayo hemat energi hari ini!";
                    PetHintDisplay = "Selesaikan lalu klaim misi harian untuk memulihkan EcoPet.";
                    break;
            }
        }

        internal void ShowPetUnavailable()
        {
            PetStateDisplay = "Data belum tersedia";
            PetMessageDisplay = "Data EcoPet belum bisa dibaca.";
            PetHeadlineDisplay = "EcoPet kamu";
            PetHintDisplay = "";
        }

        internal void ShowSleepRecommendation(string title, string reason, bool canApply)
        {
            RekomendasiSleepDisplay = title;
            AlasanRekomendasiSleepDisplay = reason;
            CanApplySleepRecommendation = canApply;
        }

        private static ImageSource? LoadPetImage(string fileName)
        {
            if (PetImageCache.TryGetValue(fileName, out var cached))
                return cached;
            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.UriSource = new Uri("pack://application:,,,/Assets/Images/" + Uri.EscapeDataString(fileName), UriKind.Absolute);
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.EndInit();
                image.Freeze();
                PetImageCache[fileName] = image;
                return image;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
