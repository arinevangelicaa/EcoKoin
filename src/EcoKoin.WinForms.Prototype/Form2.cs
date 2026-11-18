using System;
using System.Windows.Forms;

namespace ecokoin7
{
    public partial class Form2 : Form
    {
        public Form2()
        {
            InitializeComponent();

            TampilkanStatistik();
        }

        private void TampilkanStatistik()
        {
            // Data statistik simulasi
            lblEnergi.Text = "Energi: 15 kWh";
            lblCO2.Text = "CO2: 2.3 kg";
            lblHemat.Text = "Hemat: Rp12.500";

            // Progress misi
            progressBar1.Minimum = 0;
            progressBar1.Maximum = 100;
            progressBar1.Value = EcoKoinData.ProgressMisi;

            // Insight
            lblInsight1.Text =
                "Penghematan meningkat: Penghematan energi minggu ini " +
                "15% lebih baik daripada minggu lalu. Tingkatkan!";

            lblInsight2.Text =
                "Hari paling hemat: Kamis, kamu berhasil menghemat " +
                "0.5 kWh pada Kamis minggu ini.";
        }

        // TOMBOL KEMBALI
        private void tbBack_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}