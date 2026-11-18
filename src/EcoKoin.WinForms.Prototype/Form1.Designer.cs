namespace ecokoin7
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            lblJudul = new Label();
            lblPet = new Label();
            pictureBox1 = new PictureBox();
            lblWeather = new Label();
            lblKoin = new Label();
            lblStatus = new Label();
            tbAktifkan = new Button();
            tbStatistik = new Button();
            lblUser = new Label();
            lblLembap = new Label();
            lblDaya = new Label();
            lblAnalisis = new Label();
            lblRekomendasi = new Label();
            lblQuest = new Label();
            lblQuest1 = new Label();
            tbQuest1 = new Button();
            tbDashboard = new Button();
            tbMisiReward = new Button();
            tbAnalisis = new Button();
            lblCO2 = new Label();
            lblRupiah = new Label();
            lblEnergi = new Label();
            lblInsight = new Label();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).BeginInit();
            SuspendLayout();
            // 
            // lblJudul
            // 
            lblJudul.AutoSize = true;
            lblJudul.Location = new Point(27, 32);
            lblJudul.Margin = new Padding(2, 0, 2, 0);
            lblJudul.Name = "lblJudul";
            lblJudul.Size = new Size(63, 20);
            lblJudul.TabIndex = 0;
            lblJudul.Text = "EcoKoin";
            // 
            // lblPet
            // 
            lblPet.AutoSize = true;
            lblPet.Location = new Point(189, 100);
            lblPet.Margin = new Padding(2, 0, 2, 0);
            lblPet.Name = "lblPet";
            lblPet.Size = new Size(56, 20);
            lblPet.TabIndex = 1;
            lblPet.Text = "EcoPet:";
            // 
            // pictureBox1
            // 
            pictureBox1.Location = new Point(255, 100);
            pictureBox1.Margin = new Padding(2, 2, 2, 2);
            pictureBox1.Name = "pictureBox1";
            pictureBox1.Size = new Size(102, 87);
            pictureBox1.TabIndex = 2;
            pictureBox1.TabStop = false;
            // 
            // lblWeather
            // 
            lblWeather.AutoSize = true;
            lblWeather.Location = new Point(352, 32);
            lblWeather.Margin = new Padding(2, 0, 2, 0);
            lblWeather.Name = "lblWeather";
            lblWeather.Size = new Size(163, 20);
            lblWeather.TabIndex = 3;
            lblWeather.Text = "Weather: 25C Mendung";
            lblWeather.Click += lblWeather_Click;
            // 
            // lblKoin
            // 
            lblKoin.AutoSize = true;
            lblKoin.Location = new Point(182, 32);
            lblKoin.Margin = new Padding(2, 0, 2, 0);
            lblKoin.Name = "lblKoin";
            lblKoin.Size = new Size(94, 20);
            lblKoin.TabIndex = 4;
            lblKoin.Text = "EcoKoin: 120";
            // 
            // lblStatus
            // 
            lblStatus.AutoSize = true;
            lblStatus.Location = new Point(577, 147);
            lblStatus.Margin = new Padding(2, 0, 2, 0);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(343, 40);
            lblStatus.TabIndex = 5;
            lblStatus.Text = "Turunkan kecerahan ke 40%\r\n(Cuaca berawan, laya tidak perlu terang maksimal)";
            lblStatus.Click += lblStatus_Click;
            // 
            // tbAktifkan
            // 
            tbAktifkan.BackColor = SystemColors.ScrollBar;
            tbAktifkan.Location = new Point(906, 143);
            tbAktifkan.Margin = new Padding(2, 2, 2, 2);
            tbAktifkan.Name = "tbAktifkan";
            tbAktifkan.Size = new Size(89, 27);
            tbAktifkan.TabIndex = 6;
            tbAktifkan.Text = "Terapkan";
            tbAktifkan.UseVisualStyleBackColor = false;
            tbAktifkan.Click += tbAktifkan_Click;
            // 
            // tbStatistik
            // 
            tbStatistik.BackColor = SystemColors.ScrollBar;
            tbStatistik.Location = new Point(401, 414);
            tbStatistik.Margin = new Padding(2, 2, 2, 2);
            tbStatistik.Name = "tbStatistik";
            tbStatistik.Size = new Size(124, 27);
            tbStatistik.TabIndex = 7;
            tbStatistik.Text = "Lihat Semua";
            tbStatistik.UseVisualStyleBackColor = false;
            tbStatistik.Click += tbStatistik_Click;
            // 
            // lblUser
            // 
            lblUser.AutoSize = true;
            lblUser.Location = new Point(27, 77);
            lblUser.Margin = new Padding(2, 0, 2, 0);
            lblUser.Name = "lblUser";
            lblUser.Size = new Size(75, 20);
            lblUser.TabIndex = 8;
            lblUser.Text = "Hermione";
            lblUser.Click += label1_Click;
            // 
            // lblLembap
            // 
            lblLembap.AutoSize = true;
            lblLembap.Location = new Point(586, 32);
            lblLembap.Margin = new Padding(2, 0, 2, 0);
            lblLembap.Name = "lblLembap";
            lblLembap.Size = new Size(186, 20);
            lblLembap.TabIndex = 9;
            lblLembap.Text = "Kelembapan: 65% Lembap";
            // 
            // lblDaya
            // 
            lblDaya.AutoSize = true;
            lblDaya.Location = new Point(828, 32);
            lblDaya.Margin = new Padding(2, 0, 2, 0);
            lblDaya.Name = "lblDaya";
            lblDaya.Size = new Size(196, 20);
            lblDaya.TabIndex = 10;
            lblDaya.Text = "Status Daya: 75% On Battery";
            // 
            // lblAnalisis
            // 
            lblAnalisis.AutoSize = true;
            lblAnalisis.Location = new Point(189, 252);
            lblAnalisis.Margin = new Padding(2, 0, 2, 0);
            lblAnalisis.Name = "lblAnalisis";
            lblAnalisis.Size = new Size(62, 20);
            lblAnalisis.TabIndex = 11;
            lblAnalisis.Text = "Analisis:";
            lblAnalisis.Click += label1_Click_1;
            // 
            // lblRekomendasi
            // 
            lblRekomendasi.AutoSize = true;
            lblRekomendasi.Location = new Point(577, 111);
            lblRekomendasi.Margin = new Padding(2, 0, 2, 0);
            lblRekomendasi.Name = "lblRekomendasi";
            lblRekomendasi.Size = new Size(199, 20);
            lblRekomendasi.TabIndex = 12;
            lblRekomendasi.Text = "Rekomendasi Adaptif Cuaca:";
            // 
            // lblQuest
            // 
            lblQuest.AutoSize = true;
            lblQuest.Location = new Point(577, 254);
            lblQuest.Margin = new Padding(2, 0, 2, 0);
            lblQuest.Name = "lblQuest";
            lblQuest.Size = new Size(88, 20);
            lblQuest.TabIndex = 13;
            lblQuest.Text = "Daily Quest:";
            // 
            // lblQuest1
            // 
            lblQuest1.AutoSize = true;
            lblQuest1.Location = new Point(578, 282);
            lblQuest1.Margin = new Padding(2, 0, 2, 0);
            lblQuest1.Name = "lblQuest1";
            lblQuest1.Size = new Size(183, 20);
            lblQuest1.TabIndex = 14;
            lblQuest1.Text = "Power saver selama 4  jam";
            // 
            // tbQuest1
            // 
            tbQuest1.BackColor = SystemColors.ScrollBar;
            tbQuest1.Location = new Point(906, 278);
            tbQuest1.Margin = new Padding(2, 2, 2, 2);
            tbQuest1.Name = "tbQuest1";
            tbQuest1.Size = new Size(67, 27);
            tbQuest1.TabIndex = 15;
            tbQuest1.Text = "+20";
            tbQuest1.UseVisualStyleBackColor = false;
            // 
            // tbDashboard
            // 
            tbDashboard.BackColor = SystemColors.ScrollBar;
            tbDashboard.Location = new Point(27, 132);
            tbDashboard.Margin = new Padding(2, 2, 2, 2);
            tbDashboard.Name = "tbDashboard";
            tbDashboard.Size = new Size(104, 27);
            tbDashboard.TabIndex = 16;
            tbDashboard.Text = "Dashboard";
            tbDashboard.UseVisualStyleBackColor = false;
            // 
            // tbMisiReward
            // 
            tbMisiReward.BackColor = SystemColors.ScrollBar;
            tbMisiReward.Location = new Point(27, 235);
            tbMisiReward.Margin = new Padding(2, 2, 2, 2);
            tbMisiReward.Name = "tbMisiReward";
            tbMisiReward.Size = new Size(104, 27);
            tbMisiReward.TabIndex = 17;
            tbMisiReward.Text = "Misi & Reward";
            tbMisiReward.UseVisualStyleBackColor = false;
            // 
            // tbAnalisis
            // 
            tbAnalisis.BackColor = SystemColors.ScrollBar;
            tbAnalisis.Location = new Point(27, 182);
            tbAnalisis.Margin = new Padding(2, 2, 2, 2);
            tbAnalisis.Name = "tbAnalisis";
            tbAnalisis.Size = new Size(104, 27);
            tbAnalisis.TabIndex = 18;
            tbAnalisis.Text = "Analisis";
            tbAnalisis.UseVisualStyleBackColor = false;
            // 
            // lblCO2
            // 
            lblCO2.AutoSize = true;
            lblCO2.Location = new Point(206, 282);
            lblCO2.Margin = new Padding(2, 0, 2, 0);
            lblCO2.Name = "lblCO2";
            lblCO2.Size = new Size(144, 20);
            lblCO2.TabIndex = 19;
            lblCO2.Text = "CO2 Dihemat: 1.2 kg";
            // 
            // lblRupiah
            // 
            lblRupiah.AutoSize = true;
            lblRupiah.Location = new Point(206, 312);
            lblRupiah.Margin = new Padding(2, 0, 2, 0);
            lblRupiah.Name = "lblRupiah";
            lblRupiah.Size = new Size(189, 20);
            lblRupiah.TabIndex = 20;
            lblRupiah.Text = "Tagihan Dihemat: Rp12.500";
            // 
            // lblEnergi
            // 
            lblEnergi.AutoSize = true;
            lblEnergi.Location = new Point(206, 344);
            lblEnergi.Margin = new Padding(2, 0, 2, 0);
            lblEnergi.Name = "lblEnergi";
            lblEnergi.Size = new Size(171, 20);
            lblEnergi.TabIndex = 21;
            lblEnergi.Text = "Energi Dihemat: 2.5 kWh";
            // 
            // lblInsight
            // 
            lblInsight.AutoSize = true;
            lblInsight.Location = new Point(206, 374);
            lblInsight.Margin = new Padding(2, 0, 2, 0);
            lblInsight.Name = "lblInsight";
            lblInsight.Size = new Size(421, 20);
            lblInsight.TabIndex = 22;
            lblInsight.Text = "Penghematan kamu 15% lebih baik dari minggu lalu. Teruskan!";
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1063, 470);
            Controls.Add(lblInsight);
            Controls.Add(lblEnergi);
            Controls.Add(lblRupiah);
            Controls.Add(lblCO2);
            Controls.Add(tbAnalisis);
            Controls.Add(tbMisiReward);
            Controls.Add(tbDashboard);
            Controls.Add(tbQuest1);
            Controls.Add(lblQuest1);
            Controls.Add(lblQuest);
            Controls.Add(lblRekomendasi);
            Controls.Add(lblAnalisis);
            Controls.Add(lblDaya);
            Controls.Add(lblLembap);
            Controls.Add(lblUser);
            Controls.Add(tbStatistik);
            Controls.Add(tbAktifkan);
            Controls.Add(lblStatus);
            Controls.Add(lblKoin);
            Controls.Add(lblWeather);
            Controls.Add(pictureBox1);
            Controls.Add(lblPet);
            Controls.Add(lblJudul);
            Margin = new Padding(2, 2, 2, 2);
            Name = "Form1";
            Text = "Form1";
            Load += Form1_Load;
            ((System.ComponentModel.ISupportInitialize)pictureBox1).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label lblJudul;
        private Label lblPet;
        private PictureBox pictureBox1;
        private Label lblWeather;
        private Label lblKoin;
        private Label lblStatus;
        private Button tbAktifkan;
        private Button tbStatistik;
        private Label lblUser;
        private Label lblLembap;
        private Label lblDaya;
        private Label lblAnalisis;
        private Label lblRekomendasi;
        private Label lblQuest;
        private Label lblQuest1;
        private Button tbQuest1;
        private Button tbDashboard;
        private Button tbMisiReward;
        private Button tbAnalisis;
        private Label lblCO2;
        private Label lblRupiah;
        private Label lblEnergi;
        private Label lblInsight;
    }
}
