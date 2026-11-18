namespace ecokoin7
{
    partial class Form3
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            lblJudulMisiReward = new Label();
            lblTotKoin = new Label();
            lblInsightKoin = new Label();
            lblDaftarMisi = new Label();
            lblMisi1 = new Label();
            lblMIsi2 = new Label();
            lblProgresMisi = new Label();
            progressBarMisi = new ProgressBar();
            tbAktifkan = new Button();
            tbQuest1 = new Button();
            tbBack = new Button();
            SuspendLayout();
            // 
            // lblJudulMisiReward
            // 
            lblJudulMisiReward.AutoSize = true;
            lblJudulMisiReward.Location = new Point(63, 35);
            lblJudulMisiReward.Name = "lblJudulMisiReward";
            lblJudulMisiReward.Size = new Size(112, 25);
            lblJudulMisiReward.TabIndex = 0;
            lblJudulMisiReward.Text = "Misi & Reward";
            // 
            // lblTotKoin
            // 
            lblTotKoin.AutoSize = true;
            lblTotKoin.Location = new Point(63, 80);
            lblTotKoin.Name = "lblTotKoin";
            lblTotKoin.Size = new Size(156, 25);
            lblTotKoin.TabIndex = 1;
            lblTotKoin.Text = "Total EcoKoin: 800";
            // 
            // lblInsightKoin
            // 
            lblInsightKoin.AutoSize = true;
            lblInsightKoin.Location = new Point(81, 116);
            lblInsightKoin.Name = "lblInsightKoin";
            lblInsightKoin.Size = new Size(651, 25);
            lblInsightKoin.TabIndex = 2;
            lblInsightKoin.Text = "Selesaikan rekomendais adaptif cuaca dan daily quest untuk menambah koinmu!";
            // 
            // lblDaftarMisi
            // 
            lblDaftarMisi.AutoSize = true;
            lblDaftarMisi.Location = new Point(63, 158);
            lblDaftarMisi.Name = "lblDaftarMisi";
            lblDaftarMisi.Size = new Size(102, 25);
            lblDaftarMisi.TabIndex = 3;
            lblDaftarMisi.Text = "Daftar Misi:";
            // 
            // lblMisi1
            // 
            lblMisi1.AutoSize = true;
            lblMisi1.Location = new Point(81, 196);
            lblMisi1.Name = "lblMisi1";
            lblMisi1.Size = new Size(412, 50);
            lblMisi1.TabIndex = 4;
            lblMisi1.Text = "Turunkan kecerahan ke 40%\r\n(Cuaca berawan, layar tidak perlu terang maksimal)\r\n";
            // 
            // lblMIsi2
            // 
            lblMIsi2.AutoSize = true;
            lblMIsi2.Location = new Point(81, 273);
            lblMIsi2.Name = "lblMIsi2";
            lblMIsi2.Size = new Size(220, 25);
            lblMIsi2.TabIndex = 5;
            lblMIsi2.Text = "Power saver selama 4  jam";
            // 
            // lblProgresMisi
            // 
            lblProgresMisi.AutoSize = true;
            lblProgresMisi.Location = new Point(63, 330);
            lblProgresMisi.Name = "lblProgresMisi";
            lblProgresMisi.Size = new Size(122, 25);
            lblProgresMisi.TabIndex = 6;
            lblProgresMisi.Text = "Progress Misi:";
            lblProgresMisi.Click += lblProgresMisi_Click;
            // 
            // progressBarMisi
            // 
            progressBarMisi.Location = new Point(197, 325);
            progressBarMisi.Name = "progressBarMisi";
            progressBarMisi.Size = new Size(150, 34);
            progressBarMisi.TabIndex = 7;
            progressBarMisi.Value = 40;
            // 
            // tbAktifkan
            // 
            tbAktifkan.BackColor = SystemColors.ScrollBar;
            tbAktifkan.Location = new Point(573, 196);
            tbAktifkan.Name = "tbAktifkan";
            tbAktifkan.Size = new Size(111, 34);
            tbAktifkan.TabIndex = 8;
            tbAktifkan.Text = "Terapkan";
            tbAktifkan.UseVisualStyleBackColor = false;
            // 
            // tbQuest1
            // 
            tbQuest1.BackColor = SystemColors.ScrollBar;
            tbQuest1.Location = new Point(585, 268);
            tbQuest1.Name = "tbQuest1";
            tbQuest1.Size = new Size(84, 34);
            tbQuest1.TabIndex = 16;
            tbQuest1.Text = "+20";
            tbQuest1.UseVisualStyleBackColor = false;
            // 
            // tbBack
            // 
            tbBack.BackColor = SystemColors.ScrollBar;
            tbBack.Location = new Point(63, 454);
            tbBack.Name = "tbBack";
            tbBack.Size = new Size(112, 34);
            tbBack.TabIndex = 17;
            tbBack.Text = "Kembali";
            tbBack.UseVisualStyleBackColor = false;
            // 
            // Form3
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(900, 580);
            Controls.Add(tbBack);
            Controls.Add(tbQuest1);
            Controls.Add(tbAktifkan);
            Controls.Add(progressBarMisi);
            Controls.Add(lblProgresMisi);
            Controls.Add(lblMIsi2);
            Controls.Add(lblMisi1);
            Controls.Add(lblDaftarMisi);
            Controls.Add(lblInsightKoin);
            Controls.Add(lblTotKoin);
            Controls.Add(lblJudulMisiReward);
            Name = "Form3";
            Text = "Form3";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label lblJudulMisiReward;
        private Label lblTotKoin;
        private Label lblInsightKoin;
        private Label lblDaftarMisi;
        private Label lblMisi1;
        private Label lblMIsi2;
        private Label lblProgresMisi;
        private ProgressBar progressBarMisi;
        private Button tbAktifkan;
        private Button tbQuest1;
        private Button tbBack;
    }
}