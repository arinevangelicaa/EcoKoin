using System;
using System.Windows.Forms;

namespace ecokoin7
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();

            // Tombol yang belum memiliki event di Designer
            tbDashboard.Click += tbDashboard_Click;
            tbAnalisis.Click += tbAnalisis_Click;
            tbMisiReward.Click += tbMisiReward_Click;
            tbQuest1.Click += tbQuest1_Click;

            TampilkanDashboard();
        }

        private void TampilkanDashboard()
        {
            // Informasi utama
            lblKoin.Text = "EcoKoin: " + EcoKoinData.TotalKoin;
            lblWeather.Text = "Weather: 25°C Mendung";
            lblLembap.Text = "Kelembapan: 65% Lembap";
            lblDaya.Text = "Status Daya: 75% On Battery";

            // Ringkasan analisis
            lblCO2.Text = "CO2 Dihemat: 1.2 kg";
            lblRupiah.Text = "Tagihan Dihemat: Rp12.500";
            lblEnergi.Text = "Energi Dihemat: 2.5 kWh";

            lblInsight.Text =
                "Penghematan kamu 15% lebih baik dari minggu lalu. Teruskan!";

            // Status EcoPet
            if (EcoKoinData.MisiSelesai == 2)
            {
                lblPet.Text = "EcoPet: Bahagia!";
            }
            else if (EcoKoinData.MisiSelesai == 1)
            {
                lblPet.Text = "EcoPet: Semangat!";
            }
            else
            {
                lblPet.Text = "EcoPet: " + (!string.IsNullOrEmpty(EcoKoinData.PetState) ? EcoKoinData.PetState : "Normal");
            }

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

        // EVENT YANG SUDAH TERHUBUNG DI DESIGNER

        private void Form1_Load(object sender, EventArgs e)
        {
            EcoKoinData.MuatDataDariDatabase();
            TampilkanDashboard();
        }

        private void lblStatus_Click(object sender, EventArgs e)
        {
        }

        private void lblWeather_Click(object sender, EventArgs e)
        {
        }

        private void label1_Click(object sender, EventArgs e)
        {
        }

        private void label1_Click_1(object sender, EventArgs e)
        {
        }

        // TOMBOL TERAPKAN REKOMENDASI CUACA
        private void tbAktifkan_Click(object sender, EventArgs e)
        {
            bool berhasil = EcoKoinData.SelesaikanMisiKecerahan();

            if (berhasil)
            {
                MessageBox.Show(
                    "Rekomendasi cuaca berhasil diterapkan (simulasi)!\n" +
                    "Kamu mendapatkan +15 EcoKoin.",
                    "EcoKoin",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }

            TampilkanDashboard();
        }

        // TOMBOL LIHAT SEMUA
        private void tbStatistik_Click(object sender, EventArgs e)
        {
            BukaMisiReward();
        }

        // TOMBOL DASHBOARD
        private void tbDashboard_Click(object sender, EventArgs e)
        {
            TampilkanDashboard();
        }

        // TOMBOL ANALISIS
        private void tbAnalisis_Click(object sender, EventArgs e)
        {
            using (Form2 formAnalisis = new Form2())
            {
                this.Hide();
                formAnalisis.ShowDialog();
                this.Show();
            }

            TampilkanDashboard();
        }

        // TOMBOL MISI & REWARD
        private void tbMisiReward_Click(object sender, EventArgs e)
        {
            BukaMisiReward();
        }

        private void BukaMisiReward()
        {
            using (Form3 formMisi = new Form3())
            {
                this.Hide();
                formMisi.ShowDialog();
                this.Show();
            }

            TampilkanDashboard();
        }

        // TOMBOL DAILY QUEST +20
        private void tbQuest1_Click(object sender, EventArgs e)
        {
            bool berhasil = EcoKoinData.SelesaikanMisiPowerSaver();

            if (berhasil)
            {
                MessageBox.Show(
                    "Daily Quest berhasil diselesaikan (simulasi)!\n" +
                    "Kamu mendapatkan +20 EcoKoin.",
                    "EcoKoin",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }

            TampilkanDashboard();
        }
    }
}