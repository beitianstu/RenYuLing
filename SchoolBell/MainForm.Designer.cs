namespace SchoolBell
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        private System.Windows.Forms.TextBox txtStartBell;
        private System.Windows.Forms.TextBox txtEndBell;
        private System.Windows.Forms.Button btnChooseStartBell;
        private System.Windows.Forms.Button btnChooseEndBell;

        private System.Windows.Forms.ComboBox comboDevice;
        private System.Windows.Forms.Button btnStart;
        private System.Windows.Forms.Button btnStop;
        private System.Windows.Forms.Label lblNextBell;
        private System.Windows.Forms.Label lblCountdown;
        private System.Windows.Forms.Timer remainTimer;

        private System.Windows.Forms.Panel cardSchedule;
        private System.Windows.Forms.Panel cardBell;
        private System.Windows.Forms.Panel cardControl;
        private System.Windows.Forms.Panel cardStatus;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Label lblScheduleTitle;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            txtStartBell = new System.Windows.Forms.TextBox();
            txtEndBell = new System.Windows.Forms.TextBox();
            btnChooseStartBell = new System.Windows.Forms.Button();
            btnChooseEndBell = new System.Windows.Forms.Button();
            comboDevice = new System.Windows.Forms.ComboBox();
            btnStart = new System.Windows.Forms.Button();
            btnStop = new System.Windows.Forms.Button();
            lblTime = new System.Windows.Forms.Label();
            btnTestStartBell = new System.Windows.Forms.Button();
            btnTestEndBell = new System.Windows.Forms.Button();
            lblNextBell = new System.Windows.Forms.Label();
            lblCountdown = new System.Windows.Forms.Label();
            lblScheduleStatus = new System.Windows.Forms.Label();
            lblRunStatus = new System.Windows.Forms.Label();
            Pause = new System.Windows.Forms.Button();
            Resume = new System.Windows.Forms.Button();
            remainTimer = new System.Windows.Forms.Timer(components);
            lblMicStatus = new System.Windows.Forms.Label();
            pictureBox1 = new System.Windows.Forms.PictureBox();
            txtboxClassTable = new System.Windows.Forms.TextBox();
            lblTitle = new System.Windows.Forms.Label();
            cardSchedule = new SchoolBell.MainForm.RoundedPanel();
            lblScheduleTitle = new System.Windows.Forms.Label();
            cardBell = new SchoolBell.MainForm.RoundedPanel();
            cardControl = new SchoolBell.MainForm.RoundedPanel();
            cardStatus = new SchoolBell.MainForm.RoundedPanel();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).BeginInit();
            cardSchedule.SuspendLayout();
            cardBell.SuspendLayout();
            cardControl.SuspendLayout();
            cardStatus.SuspendLayout();
            SuspendLayout();
            // 
            // txtStartBell
            // 
            txtStartBell.BackColor = System.Drawing.Color.FromArgb(((int)((byte)36)), ((int)((byte)36)), ((int)((byte)36)));
            txtStartBell.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            txtStartBell.Font = new System.Drawing.Font("Segoe UI Variable Text", 8F);
            txtStartBell.ForeColor = System.Drawing.Color.FromArgb(((int)((byte)200)), ((int)((byte)200)), ((int)((byte)200)));
            txtStartBell.Location = new System.Drawing.Point(10, 12);
            txtStartBell.Name = "txtStartBell";
            txtStartBell.ReadOnly = true;
            txtStartBell.Size = new System.Drawing.Size(166, 22);
            txtStartBell.TabIndex = 0;
            txtStartBell.TabStop = false;
            txtStartBell.Text = "bells中放入start.mp3作为默认上课铃";
            // 
            // txtEndBell
            // 
            txtEndBell.BackColor = System.Drawing.Color.FromArgb(((int)((byte)36)), ((int)((byte)36)), ((int)((byte)36)));
            txtEndBell.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            txtEndBell.Font = new System.Drawing.Font("Segoe UI Variable Text", 8F);
            txtEndBell.ForeColor = System.Drawing.Color.FromArgb(((int)((byte)200)), ((int)((byte)200)), ((int)((byte)200)));
            txtEndBell.Location = new System.Drawing.Point(10, 48);
            txtEndBell.Name = "txtEndBell";
            txtEndBell.ReadOnly = true;
            txtEndBell.Size = new System.Drawing.Size(166, 22);
            txtEndBell.TabIndex = 2;
            txtEndBell.TabStop = false;
            txtEndBell.Text = "bells中放入end.mp3作为默认下课铃";
            // 
            // btnChooseStartBell
            // 
            btnChooseStartBell.Location = new System.Drawing.Point(184, 10);
            btnChooseStartBell.Name = "btnChooseStartBell";
            btnChooseStartBell.Size = new System.Drawing.Size(66, 26);
            btnChooseStartBell.TabIndex = 1;
            btnChooseStartBell.Text = "选择";
            // 
            // btnChooseEndBell
            // 
            btnChooseEndBell.Location = new System.Drawing.Point(184, 46);
            btnChooseEndBell.Name = "btnChooseEndBell";
            btnChooseEndBell.Size = new System.Drawing.Size(66, 26);
            btnChooseEndBell.TabIndex = 3;
            btnChooseEndBell.Text = "选择";
            // 
            // comboDevice
            // 
            comboDevice.BackColor = System.Drawing.Color.FromArgb(((int)((byte)36)), ((int)((byte)36)), ((int)((byte)36)));
            comboDevice.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            comboDevice.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            comboDevice.Font = new System.Drawing.Font("Segoe UI Variable Text", 9F);
            comboDevice.ForeColor = System.Drawing.Color.FromArgb(((int)((byte)250)), ((int)((byte)250)), ((int)((byte)250)));
            comboDevice.Location = new System.Drawing.Point(10, 84);
            comboDevice.Name = "comboDevice";
            comboDevice.Size = new System.Drawing.Size(240, 24);
            comboDevice.TabIndex = 4;
            // 
            // btnStart
            // 
            btnStart.BackColor = System.Drawing.Color.FromArgb(((int)((byte)0)), ((int)((byte)120)), ((int)((byte)215)));
            btnStart.Cursor = System.Windows.Forms.Cursors.Hand;
            btnStart.FlatAppearance.BorderSize = 0;
            btnStart.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btnStart.Font = new System.Drawing.Font("Segoe UI Variable Text", 9F, System.Drawing.FontStyle.Bold);
            btnStart.ForeColor = System.Drawing.Color.White;
            btnStart.Location = new System.Drawing.Point(10, 10);
            btnStart.Name = "btnStart";
            btnStart.Size = new System.Drawing.Size(164, 30);
            btnStart.TabIndex = 0;
            btnStart.TabStop = false;
            btnStart.Text = "启动";
            btnStart.UseVisualStyleBackColor = false;
            // 
            // btnStop
            // 
            btnStop.Location = new System.Drawing.Point(10, 48);
            btnStop.Name = "btnStop";
            btnStop.Size = new System.Drawing.Size(164, 30);
            btnStop.TabIndex = 1;
            btnStop.Text = "停止";
            // 
            // lblTime
            // 
            lblTime.Font = new System.Drawing.Font("Segoe UI Variable Display", 15F, System.Drawing.FontStyle.Bold);
            lblTime.ForeColor = System.Drawing.Color.FromArgb(((int)((byte)0)), ((int)((byte)120)), ((int)((byte)215)));
            lblTime.Location = new System.Drawing.Point(10, 10);
            lblTime.Name = "lblTime";
            lblTime.Size = new System.Drawing.Size(164, 28);
            lblTime.TabIndex = 0;
            lblTime.Text = "--:--:--";
            // 
            // btnTestStartBell
            // 
            btnTestStartBell.Location = new System.Drawing.Point(10, 118);
            btnTestStartBell.Name = "btnTestStartBell";
            btnTestStartBell.Size = new System.Drawing.Size(118, 26);
            btnTestStartBell.TabIndex = 5;
            btnTestStartBell.Text = "上课铃测试";
            // 
            // btnTestEndBell
            // 
            btnTestEndBell.Location = new System.Drawing.Point(132, 118);
            btnTestEndBell.Name = "btnTestEndBell";
            btnTestEndBell.Size = new System.Drawing.Size(118, 26);
            btnTestEndBell.TabIndex = 6;
            btnTestEndBell.Text = "下课铃测试";
            // 
            // lblNextBell
            // 
            lblNextBell.Font = new System.Drawing.Font("Segoe UI Variable Text", 8F);
            lblNextBell.ForeColor = System.Drawing.Color.FromArgb(((int)((byte)200)), ((int)((byte)200)), ((int)((byte)200)));
            lblNextBell.Location = new System.Drawing.Point(10, 44);
            lblNextBell.Name = "lblNextBell";
            lblNextBell.Size = new System.Drawing.Size(166, 18);
            lblNextBell.TabIndex = 1;
            lblNextBell.Text = "下一次：未加载";
            // 
            // lblCountdown
            // 
            lblCountdown.Font = new System.Drawing.Font("Segoe UI Variable Text", 8F);
            lblCountdown.ForeColor = System.Drawing.Color.FromArgb(((int)((byte)200)), ((int)((byte)200)), ((int)((byte)200)));
            lblCountdown.Location = new System.Drawing.Point(10, 64);
            lblCountdown.Name = "lblCountdown";
            lblCountdown.Size = new System.Drawing.Size(166, 18);
            lblCountdown.TabIndex = 2;
            lblCountdown.Text = "倒计时 --:--:--";
            // 
            // lblScheduleStatus
            // 
            lblScheduleStatus.Font = new System.Drawing.Font("Segoe UI Variable Text", 8F);
            lblScheduleStatus.ForeColor = System.Drawing.Color.FromArgb(((int)((byte)140)), ((int)((byte)140)), ((int)((byte)140)));
            lblScheduleStatus.Location = new System.Drawing.Point(10, 88);
            lblScheduleStatus.Name = "lblScheduleStatus";
            lblScheduleStatus.Size = new System.Drawing.Size(166, 16);
            lblScheduleStatus.TabIndex = 3;
            lblScheduleStatus.Text = "课表：未加载";
            // 
            // lblRunStatus
            // 
            lblRunStatus.Font = new System.Drawing.Font("Segoe UI Variable Text", 8F);
            lblRunStatus.ForeColor = System.Drawing.Color.FromArgb(((int)((byte)140)), ((int)((byte)140)), ((int)((byte)140)));
            lblRunStatus.Location = new System.Drawing.Point(10, 106);
            lblRunStatus.Name = "lblRunStatus";
            lblRunStatus.Size = new System.Drawing.Size(166, 16);
            lblRunStatus.TabIndex = 4;
            lblRunStatus.Text = "状态：NOT RUNNING";
            // 
            // Pause
            // 
            Pause.Location = new System.Drawing.Point(10, 86);
            Pause.Name = "Pause";
            Pause.Size = new System.Drawing.Size(79, 30);
            Pause.TabIndex = 2;
            Pause.Text = "打铃课表";
            // 
            // Resume
            // 
            Resume.Location = new System.Drawing.Point(95, 86);
            Resume.Name = "Resume";
            Resume.Size = new System.Drawing.Size(79, 30);
            Resume.TabIndex = 3;
            Resume.Text = "静默课表";
            // 
            // remainTimer
            // 
            remainTimer.Enabled = true;
            remainTimer.Interval = 200;
            // 
            // lblMicStatus
            // 
            lblMicStatus.Font = new System.Drawing.Font("Segoe UI Variable Text", 8F);
            lblMicStatus.ForeColor = System.Drawing.Color.FromArgb(((int)((byte)140)), ((int)((byte)140)), ((int)((byte)140)));
            lblMicStatus.Location = new System.Drawing.Point(10, 124);
            lblMicStatus.Name = "lblMicStatus";
            lblMicStatus.Size = new System.Drawing.Size(166, 16);
            lblMicStatus.TabIndex = 5;
            lblMicStatus.Text = "麦克风：未知";
            // 
            // pictureBox1
            // 
            pictureBox1.BackColor = System.Drawing.Color.Transparent;
            pictureBox1.Image = ((System.Drawing.Image)resources.GetObject("pictureBox1.Image"));
            pictureBox1.Location = new System.Drawing.Point(300, 10);
            pictureBox1.Name = "pictureBox1";
            pictureBox1.Size = new System.Drawing.Size(184, 76);
            pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            pictureBox1.TabIndex = 1;
            pictureBox1.TabStop = false;
            pictureBox1.Click += pictureBox1_Click;
            // 
            // txtboxClassTable
            // 
            txtboxClassTable.BackColor = System.Drawing.Color.FromArgb(((int)((byte)46)), ((int)((byte)46)), ((int)((byte)46)));
            txtboxClassTable.BorderStyle = System.Windows.Forms.BorderStyle.None;
            txtboxClassTable.Font = new System.Drawing.Font("Segoe UI Variable Text", 9F);
            txtboxClassTable.ForeColor = System.Drawing.Color.FromArgb(((int)((byte)250)), ((int)((byte)250)), ((int)((byte)250)));
            txtboxClassTable.Location = new System.Drawing.Point(10, 32);
            txtboxClassTable.Multiline = true;
            txtboxClassTable.Name = "txtboxClassTable";
            txtboxClassTable.ReadOnly = true;
            txtboxClassTable.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            txtboxClassTable.Size = new System.Drawing.Size(240, 128);
            txtboxClassTable.TabIndex = 1;
            txtboxClassTable.TabStop = false;
            txtboxClassTable.Text = "课表未加载";
            // 
            // lblTitle
            // 
            lblTitle.BackColor = System.Drawing.Color.Transparent;
            lblTitle.Font = new System.Drawing.Font("Segoe UI Variable Display", 14F, System.Drawing.FontStyle.Bold);
            lblTitle.ForeColor = System.Drawing.Color.FromArgb(((int)((byte)250)), ((int)((byte)250)), ((int)((byte)250)));
            lblTitle.Location = new System.Drawing.Point(16, 14);
            lblTitle.Name = "lblTitle";
            lblTitle.Size = new System.Drawing.Size(200, 28);
            lblTitle.TabIndex = 0;
            lblTitle.Text = "任禹铃";
            // 
            // cardSchedule
            // 
            cardSchedule.BackColor = System.Drawing.Color.FromArgb(((int)((byte)46)), ((int)((byte)46)), ((int)((byte)46)));
            cardSchedule.Controls.Add(lblScheduleTitle);
            cardSchedule.Controls.Add(txtboxClassTable);
            cardSchedule.Location = new System.Drawing.Point(16, 50);
            cardSchedule.Name = "cardSchedule";
            cardSchedule.Size = new System.Drawing.Size(260, 170);
            cardSchedule.TabIndex = 2;
            // 
            // lblScheduleTitle
            // 
            lblScheduleTitle.BackColor = System.Drawing.Color.Transparent;
            lblScheduleTitle.Font = new System.Drawing.Font("Segoe UI Variable Text", 9F, System.Drawing.FontStyle.Bold);
            lblScheduleTitle.ForeColor = System.Drawing.Color.FromArgb(((int)((byte)250)), ((int)((byte)250)), ((int)((byte)250)));
            lblScheduleTitle.Location = new System.Drawing.Point(10, 8);
            lblScheduleTitle.Name = "lblScheduleTitle";
            lblScheduleTitle.Size = new System.Drawing.Size(240, 20);
            lblScheduleTitle.TabIndex = 0;
            lblScheduleTitle.Text = "今日课表";
            // 
            // cardBell
            // 
            cardBell.BackColor = System.Drawing.Color.FromArgb(((int)((byte)46)), ((int)((byte)46)), ((int)((byte)46)));
            cardBell.Controls.Add(txtStartBell);
            cardBell.Controls.Add(btnChooseStartBell);
            cardBell.Controls.Add(txtEndBell);
            cardBell.Controls.Add(btnChooseEndBell);
            cardBell.Controls.Add(comboDevice);
            cardBell.Controls.Add(btnTestStartBell);
            cardBell.Controls.Add(btnTestEndBell);
            cardBell.Location = new System.Drawing.Point(16, 232);
            cardBell.Name = "cardBell";
            cardBell.Size = new System.Drawing.Size(260, 150);
            cardBell.TabIndex = 3;
            // 
            // cardControl
            // 
            cardControl.BackColor = System.Drawing.Color.FromArgb(((int)((byte)46)), ((int)((byte)46)), ((int)((byte)46)));
            cardControl.Controls.Add(btnStart);
            cardControl.Controls.Add(btnStop);
            cardControl.Controls.Add(Pause);
            cardControl.Controls.Add(Resume);
            cardControl.Location = new System.Drawing.Point(300, 92);
            cardControl.Name = "cardControl";
            cardControl.Size = new System.Drawing.Size(184, 128);
            cardControl.TabIndex = 4;
            // 
            // cardStatus
            // 
            cardStatus.BackColor = System.Drawing.Color.FromArgb(((int)((byte)46)), ((int)((byte)46)), ((int)((byte)46)));
            cardStatus.Controls.Add(lblTime);
            cardStatus.Controls.Add(lblNextBell);
            cardStatus.Controls.Add(lblCountdown);
            cardStatus.Controls.Add(lblScheduleStatus);
            cardStatus.Controls.Add(lblRunStatus);
            cardStatus.Controls.Add(lblMicStatus);
            cardStatus.Location = new System.Drawing.Point(300, 232);
            cardStatus.Name = "cardStatus";
            cardStatus.Size = new System.Drawing.Size(184, 150);
            cardStatus.TabIndex = 5;
            // 
            // MainForm
            // 
            BackColor = System.Drawing.Color.FromArgb(((int)((byte)24)), ((int)((byte)24)), ((int)((byte)24)));
            ClientSize = new System.Drawing.Size(500, 398);
            Controls.Add(lblTitle);
            Controls.Add(pictureBox1);
            Controls.Add(cardSchedule);
            Controls.Add(cardBell);
            Controls.Add(cardControl);
            Controls.Add(cardStatus);
            Font = new System.Drawing.Font("Segoe UI Variable Text", 9F);
            Icon = ((System.Drawing.Icon)resources.GetObject("$this.Icon"));
            Text = "任禹铃(TM)";
            ((System.ComponentModel.ISupportInitialize)pictureBox1).EndInit();
            cardSchedule.ResumeLayout(false);
            cardSchedule.PerformLayout();
            cardBell.ResumeLayout(false);
            cardBell.PerformLayout();
            cardControl.ResumeLayout(false);
            cardStatus.ResumeLayout(false);
            ResumeLayout(false);
        }

        private static void StyleGhostButton(System.Windows.Forms.Button b, System.Drawing.Font font, System.Drawing.Color fill)
        {
            b.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            b.BackColor = fill;
            b.ForeColor = System.Drawing.Color.FromArgb(200, 200, 200);
            b.Font = font;
            b.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(66, 66, 66);
            b.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(56, 56, 56);
            b.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(40, 40, 40);
            b.Cursor = System.Windows.Forms.Cursors.Hand;
            b.TabStop = false;
        }

        // Fluent 圆角卡片
        private sealed class RoundedPanel : System.Windows.Forms.Panel
        {
            private const int Radius = 8;

            public RoundedPanel()
            {
                DoubleBuffered = true;
            }

            protected override void OnHandleCreated(System.EventArgs e)
            {
                base.OnHandleCreated(e);
                UpdateRegion();
            }

            protected override void OnResize(System.EventArgs e)
            {
                base.OnResize(e);
                UpdateRegion();
            }

            private void UpdateRegion()
            {
                if (Width <= 0 || Height <= 0) return;
                using var path = CreateRoundPath(new System.Drawing.Rectangle(0, 0, Width, Height), Radius * 2);
                Region?.Dispose();
                Region = new System.Drawing.Region(path);
            }

            protected override void OnPaint(System.Windows.Forms.PaintEventArgs e)
            {
                base.OnPaint(e);
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using var path = CreateRoundPath(new System.Drawing.Rectangle(0, 0, Width - 1, Height - 1), Radius * 2);
                using var pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(66, 66, 66), 1f);
                e.Graphics.DrawPath(pen, path);
            }

            private static System.Drawing.Drawing2D.GraphicsPath CreateRoundPath(System.Drawing.Rectangle rect, int diameter)
            {
                var path = new System.Drawing.Drawing2D.GraphicsPath();
                path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
                path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
                path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
                path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
                path.CloseFigure();
                return path;
            }
        }

        private System.Windows.Forms.TextBox txtboxClassTable;
        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.Label lblMicStatus;
        private System.Windows.Forms.Button Resume;
        private System.Windows.Forms.Button Pause;
        private System.Windows.Forms.Label lblRunStatus;
        private System.Windows.Forms.Label lblScheduleStatus;
        private System.Windows.Forms.Button btnTestStartBell;
        private System.Windows.Forms.Button btnTestEndBell;
        private System.Windows.Forms.Label lblTime;
    }
}
