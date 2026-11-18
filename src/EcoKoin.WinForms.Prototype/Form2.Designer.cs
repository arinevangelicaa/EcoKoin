namespace ecokoin7
{
    partial class Form2
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
            components = new System.ComponentModel.Container();
            lblJudulAnalisis = new Label();
            lblCO2 = new Label();
            lblHemat = new Label();
            lblProgres = new Label();
            progressBar1 = new ProgressBar();
            tbBack = new Button();
            bindingSource1 = new BindingSource(components);
            lblInsight = new Label();
            lblInsight1 = new Label();
            lblInsight2 = new Label();
            lblEnergi = new Label();
            ((System.ComponentModel.ISupportInitialize)bindingSource1).BeginInit();
            SuspendLayout();
            // 
            // lblJudulAnalisis
            // 
            lblJudulAnalisis.AutoSize = true;
            lblJudulAnalisis.Location = new Point(59, 38);
            lblJudulAnalisis.Name = "lblJudulAnalisis";
            lblJudulAnalisis.Size = new Size(71, 25);
            lblJudulAnalisis.TabIndex = 0;
            lblJudulAnalisis.Text = "Analisis";
            // 
            // lblCO2
            // 
            lblCO2.AutoSize = true;
            lblCO2.Location = new Point(59, 134);
            lblCO2.Name = "lblCO2";
            lblCO2.Size = new Size(105, 25);
            lblCO2.TabIndex = 2;
            lblCO2.Text = "CO2: 2.3 kg";
            // 
            // lblHemat
            // 
            lblHemat.AutoSize = true;
            lblHemat.Location = new Point(59, 180);
            lblHemat.Name = "lblHemat";
            lblHemat.Size = new Size(150, 25);
            lblHemat.TabIndex = 3;
            lblHemat.Text = "Hemat: Rp12.500";
            lblHemat.TextAlign = ContentAlignment.TopCenter;
            // 
            // lblProgres
            // 
            lblProgres.AutoSize = true;
            lblProgres.Location = new Point(59, 230);
            lblProgres.Name = "lblProgres";
            lblProgres.Size = new Size(85, 25);
            lblProgres.TabIndex = 4;
            lblProgres.Text = "Progress:";
            // 
            // progressBar1
            // 
            progressBar1.Location = new Point(152, 228);
            progressBar1.Name = "progressBar1";
            progressBar1.Size = new Size(150, 34);
            progressBar1.TabIndex = 5;
            progressBar1.Value = 50;
            // 
            // tbBack
            // 
            tbBack.BackColor = SystemColors.ScrollBar;
            tbBack.Location = new Point(52, 429);
            tbBack.Name = "tbBack";
            tbBack.Size = new Size(112, 34);
            tbBack.TabIndex = 6;
            tbBack.Text = "Kembali";
            tbBack.UseVisualStyleBackColor = false;
            tbBack.Click += tbBack_Click;
            // 
            // lblEnergi
            // 
            lblEnergi.AutoSize = true;
            lblEnergi.Location = new Point(59, 84);
            lblEnergi.Name = "lblEnergi";
            lblEnergi.Size = new Size(131, 25);
            lblEnergi.TabIndex = 1;
            lblEnergi.Text = "Energi: 15 kWh";
            // 
            // lblInsight
            // 
            lblInsight.AutoSize = true;
            lblInsight.Location = new Point(59, 290);
            lblInsight.Name = "lblInsight";
            lblInsight.Size = new Size(70, 25);
            lblInsight.TabIndex = 7;
            lblInsight.Text = "Insight:";
            // 
            // lblInsight1
            // 
            lblInsight1.AutoSize = true;
            lblInsight1.Location = new Point(74, 325);
            lblInsight1.Name = "lblInsight1";
            lblInsight1.Size = new Size(865, 25);
            lblInsight1.TabIndex = 8;
            lblInsight1.Text = "Penghematan meningkat: Penghematan energi minggu ini 15% lebih baik daripada minggu lalu. Tingkatkan!";
            // 
            // lblInsight2
            // 
            lblInsight2.AutoSize = true;
            lblInsight2.Location = new Point(74, 361);
            lblInsight2.Name = "lblInsight2";
            lblInsight2.Size = new Size(690, 25);
            lblInsight2.TabIndex = 9;
            lblInsight2.Text = "Hari paling hemat: Kamis, kamu berhasil menghemat 0.5 kWh pada Kamis minggu ini.";
            // 
            // Form2
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1005, 542);
            Controls.Add(lblInsight2);
            Controls.Add(lblInsight1);
            Controls.Add(lblInsight);
            Controls.Add(tbBack);
            Controls.Add(progressBar1);
            Controls.Add(lblProgres);
            Controls.Add(lblHemat);
            Controls.Add(lblCO2);
            Controls.Add(lblEnergi);
            Controls.Add(lblJudulAnalisis);
            Name = "Form2";
            Text = "Form2";
            ((System.ComponentModel.ISupportInitialize)bindingSource1).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label lblJudulAnalisis;
        private Label lblEnergi;
        private Label lblCO2;
        private Label lblHemat;
        private Label lblProgres;
        private ProgressBar progressBar1;
        private Button tbBack;
        private BindingSource bindingSource1;
        private Label lblInsight;
        private Label lblInsight1;
        private Label lblInsight2;
    }
}