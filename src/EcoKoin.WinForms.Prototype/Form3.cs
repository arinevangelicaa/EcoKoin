using System;
using System.Windows.Forms;

namespace ecokoin7
{
    public partial class Form3 : Form
    {
        public Form3()
        {
            InitializeComponent();

            // Hubungkan tombol yang belum memiliki event
            tbAktifkan.Click += tbAktifkan_Click;
            tbQuest1.Click += tbQuest1_Click;
            tbBack.Click += tbBack_Click;

            TampilkanMisi();
        }

        private void TampilkanMisi()
        {
            // Saldo EcoKoin
            lblTotKoin.Text = "Total EcoKoin: " + EcoKoinData.TotalKoin;

            // Progress misi
            progressBarMisi.Minimum = 0;
            progressBarMisi.Maximum = 100;
            progressBarMisi.Value = EcoKoinData.ProgressMisi;

            // Status rekomendasi cuaca
            if (EcoKoinData.MisiKecerahanSelesai)
            {
                tbAktifkan.Text = "Selesai";
                tbAktifkan.Enabled = false;
            }
            else
            {
                tbAktifkan.Text = "Terapkan";
                tbAktifkan.Enabled = true;
            }

            // Status Daily Quest
            if (EcoKoinData.MisiPowerSaverSelesai)
            {
                tbQuest1.Text = "Selesai";
                tbQuest1.Enabled = false;
            }
            else
            {
                tbQuest1.Text = "+20";
                tbQuest1.Enabled = true;
            }
        }

        // MISI REKOMENDASI CUACA
        private void tbAktifkan_Click(object sender, EventArgs e)
        {
            bool berhasil = EcoKoinData.SelesaikanMisiKecerahan();

            if (berhasil)
            {
                MessageBox.Show(
                    "Rekomendasi cuaca berhasil diterapkan (simulasi)!\n" +
                    "Kamu mendapatkan +15 EcoKoin.",
                    "Misi Selesai",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }

            TampilkanMisi();
        }

        // MISI POWER SAVER
        private void tbQuest1_Click(object sender, EventArgs e)
        {
            bool berhasil = EcoKoinData.SelesaikanMisiPowerSaver();

            if (berhasil)
            {
                MessageBox.Show(
                    "Misi Power Saver berhasil diselesaikan (simulasi)!\n" +
                    "Kamu mendapatkan +20 EcoKoin.",
                    "Misi Selesai",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }

            TampilkanMisi();
        }

        // TOMBOL KEMBALI
        private void tbBack_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        // EVENT LABEL YANG SUDAH ADA DI DESIGNER
        private void lblProgresMisi_Click(object sender, EventArgs e)
        {
        }
    }
}