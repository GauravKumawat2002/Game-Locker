namespace GameLocker.ConfigUI;

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
        this.components = new System.ComponentModel.Container();
        
        // Main layout panels
        this.grpDays = new System.Windows.Forms.GroupBox();
        this.grpTime = new System.Windows.Forms.GroupBox();
        this.grpFolders = new System.Windows.Forms.GroupBox();
        this.grpSettings = new System.Windows.Forms.GroupBox();
        
        // Day checkboxes
        this.chkSunday = new System.Windows.Forms.CheckBox();
        this.chkMonday = new System.Windows.Forms.CheckBox();
        this.chkTuesday = new System.Windows.Forms.CheckBox();
        this.chkWednesday = new System.Windows.Forms.CheckBox();
        this.chkThursday = new System.Windows.Forms.CheckBox();
        this.chkFriday = new System.Windows.Forms.CheckBox();
        this.chkSaturday = new System.Windows.Forms.CheckBox();
        
        // Time controls
        this.lblStartTime = new System.Windows.Forms.Label();
        this.dtpStartTime = new System.Windows.Forms.DateTimePicker();
        this.lblDuration = new System.Windows.Forms.Label();
        this.nudDuration = new System.Windows.Forms.NumericUpDown();
        this.lblDurationHours = new System.Windows.Forms.Label();
        
        // Folder controls
        this.lstFolders = new System.Windows.Forms.ListBox();
        this.btnAddFolder = new System.Windows.Forms.Button();
        this.btnRemoveFolder = new System.Windows.Forms.Button();
        this.btnTestLock = new System.Windows.Forms.Button();
        
        // Settings controls
        this.chkNotifications = new System.Windows.Forms.CheckBox();
        this.lblPollingInterval = new System.Windows.Forms.Label();
        this.nudPollingInterval = new System.Windows.Forms.NumericUpDown();
        this.lblPollingMinutes = new System.Windows.Forms.Label();
        
        // Action buttons
        this.btnSave = new System.Windows.Forms.Button();
        this.btnClear = new System.Windows.Forms.Button();
        
        // Status
        this.lblStatus = new System.Windows.Forms.Label();

        // Suspend layout
        this.grpDays.SuspendLayout();
        this.grpTime.SuspendLayout();
        this.grpFolders.SuspendLayout();
        this.grpSettings.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)(this.nudDuration)).BeginInit();
        ((System.ComponentModel.ISupportInitialize)(this.nudPollingInterval)).BeginInit();
        this.SuspendLayout();

        // 
        // grpDays
        // 
        this.grpDays.Controls.Add(this.chkSunday);
        this.grpDays.Controls.Add(this.chkMonday);
        this.grpDays.Controls.Add(this.chkTuesday);
        this.grpDays.Controls.Add(this.chkWednesday);
        this.grpDays.Controls.Add(this.chkThursday);
        this.grpDays.Controls.Add(this.chkFriday);
        this.grpDays.Controls.Add(this.chkSaturday);
        this.grpDays.Location = new System.Drawing.Point(12, 12);
        this.grpDays.Name = "grpDays";
        this.grpDays.Size = new System.Drawing.Size(360, 100);
        this.grpDays.TabIndex = 0;
        this.grpDays.TabStop = false;
        this.grpDays.Text = "Allowed Gaming Days";

        // Day checkboxes
        this.chkSunday.AutoSize = true;
        this.chkSunday.Location = new System.Drawing.Point(15, 30);
        this.chkSunday.Name = "chkSunday";
        this.chkSunday.Size = new System.Drawing.Size(66, 19);
        this.chkSunday.TabIndex = 0;
        this.chkSunday.Text = "Sunday";

        this.chkMonday.AutoSize = true;
        this.chkMonday.Location = new System.Drawing.Point(95, 30);
        this.chkMonday.Name = "chkMonday";
        this.chkMonday.Size = new System.Drawing.Size(70, 19);
        this.chkMonday.TabIndex = 1;
        this.chkMonday.Text = "Monday";

        this.chkTuesday.AutoSize = true;
        this.chkTuesday.Location = new System.Drawing.Point(175, 30);
        this.chkTuesday.Name = "chkTuesday";
        this.chkTuesday.Size = new System.Drawing.Size(68, 19);
        this.chkTuesday.TabIndex = 2;
        this.chkTuesday.Text = "Tuesday";

        this.chkWednesday.AutoSize = true;
        this.chkWednesday.Location = new System.Drawing.Point(255, 30);
        this.chkWednesday.Name = "chkWednesday";
        this.chkWednesday.Size = new System.Drawing.Size(86, 19);
        this.chkWednesday.TabIndex = 3;
        this.chkWednesday.Text = "Wednesday";

        this.chkThursday.AutoSize = true;
        this.chkThursday.Location = new System.Drawing.Point(15, 60);
        this.chkThursday.Name = "chkThursday";
        this.chkThursday.Size = new System.Drawing.Size(74, 19);
        this.chkThursday.TabIndex = 4;
        this.chkThursday.Text = "Thursday";

        this.chkFriday.AutoSize = true;
        this.chkFriday.Location = new System.Drawing.Point(95, 60);
        this.chkFriday.Name = "chkFriday";
        this.chkFriday.Size = new System.Drawing.Size(58, 19);
        this.chkFriday.TabIndex = 5;
        this.chkFriday.Text = "Friday";

        this.chkSaturday.AutoSize = true;
        this.chkSaturday.Location = new System.Drawing.Point(175, 60);
        this.chkSaturday.Name = "chkSaturday";
        this.chkSaturday.Size = new System.Drawing.Size(72, 19);
        this.chkSaturday.TabIndex = 6;
        this.chkSaturday.Text = "Saturday";

        // 
        // grpTime
        // 
        this.grpTime.Controls.Add(this.lblStartTime);
        this.grpTime.Controls.Add(this.dtpStartTime);
        this.grpTime.Controls.Add(this.lblDuration);
        this.grpTime.Controls.Add(this.nudDuration);
        this.grpTime.Controls.Add(this.lblDurationHours);
        this.grpTime.Location = new System.Drawing.Point(12, 120);
        this.grpTime.Name = "grpTime";
        this.grpTime.Size = new System.Drawing.Size(360, 80);
        this.grpTime.TabIndex = 1;
        this.grpTime.TabStop = false;
        this.grpTime.Text = "Gaming Time Window";

        this.lblStartTime.AutoSize = true;
        this.lblStartTime.Location = new System.Drawing.Point(15, 35);
        this.lblStartTime.Name = "lblStartTime";
        this.lblStartTime.Size = new System.Drawing.Size(63, 15);
        this.lblStartTime.Text = "Start Time:";

        this.dtpStartTime.Format = System.Windows.Forms.DateTimePickerFormat.Time;
        this.dtpStartTime.Location = new System.Drawing.Point(85, 32);
        this.dtpStartTime.Name = "dtpStartTime";
        this.dtpStartTime.ShowUpDown = true;
        this.dtpStartTime.Size = new System.Drawing.Size(90, 23);
        this.dtpStartTime.TabIndex = 0;

        this.lblDuration.AutoSize = true;
        this.lblDuration.Location = new System.Drawing.Point(195, 35);
        this.lblDuration.Name = "lblDuration";
        this.lblDuration.Size = new System.Drawing.Size(56, 15);
        this.lblDuration.Text = "Duration:";

        this.nudDuration.Location = new System.Drawing.Point(260, 32);
        this.nudDuration.Maximum = new decimal(new int[] { 24, 0, 0, 0 });
        this.nudDuration.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
        this.nudDuration.Name = "nudDuration";
        this.nudDuration.Size = new System.Drawing.Size(50, 23);
        this.nudDuration.TabIndex = 1;
        this.nudDuration.Value = new decimal(new int[] { 2, 0, 0, 0 });

        this.lblDurationHours.AutoSize = true;
        this.lblDurationHours.Location = new System.Drawing.Point(315, 35);
        this.lblDurationHours.Name = "lblDurationHours";
        this.lblDurationHours.Size = new System.Drawing.Size(36, 15);
        this.lblDurationHours.Text = "hours";

        // 
        // grpFolders
        // 
        this.grpFolders.Controls.Add(this.lstFolders);
        this.grpFolders.Controls.Add(this.btnAddFolder);
        this.grpFolders.Controls.Add(this.btnRemoveFolder);
        this.grpFolders.Controls.Add(this.btnTestLock);
        this.grpFolders.Location = new System.Drawing.Point(12, 210);
        this.grpFolders.Name = "grpFolders";
        this.grpFolders.Size = new System.Drawing.Size(560, 160);
        this.grpFolders.TabIndex = 2;
        this.grpFolders.TabStop = false;
        this.grpFolders.Text = "Game Folders to Lock";

        this.lstFolders.FormattingEnabled = true;
        this.lstFolders.ItemHeight = 15;
        this.lstFolders.Location = new System.Drawing.Point(15, 25);
        this.lstFolders.Name = "lstFolders";
        this.lstFolders.Size = new System.Drawing.Size(430, 124);
        this.lstFolders.TabIndex = 0;

        this.btnAddFolder.Location = new System.Drawing.Point(455, 25);
        this.btnAddFolder.Name = "btnAddFolder";
        this.btnAddFolder.Size = new System.Drawing.Size(95, 30);
        this.btnAddFolder.TabIndex = 1;
        this.btnAddFolder.Text = "Add Folder...";
        this.btnAddFolder.Click += new System.EventHandler(this.btnAddFolder_Click);

        this.btnRemoveFolder.Location = new System.Drawing.Point(455, 65);
        this.btnRemoveFolder.Name = "btnRemoveFolder";
        this.btnRemoveFolder.Size = new System.Drawing.Size(95, 30);
        this.btnRemoveFolder.TabIndex = 2;
        this.btnRemoveFolder.Text = "Remove";
        this.btnRemoveFolder.Click += new System.EventHandler(this.btnRemoveFolder_Click);

        this.btnTestLock.Location = new System.Drawing.Point(455, 115);
        this.btnTestLock.Name = "btnTestLock";
        this.btnTestLock.Size = new System.Drawing.Size(95, 30);
        this.btnTestLock.TabIndex = 3;
        this.btnTestLock.Text = "Test Lock";
        this.btnTestLock.Click += new System.EventHandler(this.btnTestLock_Click);

        // 
        // grpSettings
        // 
        this.grpSettings.Controls.Add(this.chkNotifications);
        this.grpSettings.Controls.Add(this.lblPollingInterval);
        this.grpSettings.Controls.Add(this.nudPollingInterval);
        this.grpSettings.Controls.Add(this.lblPollingMinutes);
        this.grpSettings.Location = new System.Drawing.Point(380, 12);
        this.grpSettings.Name = "grpSettings";
        this.grpSettings.Size = new System.Drawing.Size(192, 100);
        this.grpSettings.TabIndex = 3;
        this.grpSettings.TabStop = false;
        this.grpSettings.Text = "Settings";

        this.chkNotifications.AutoSize = true;
        this.chkNotifications.Checked = true;
        this.chkNotifications.CheckState = System.Windows.Forms.CheckState.Checked;
        this.chkNotifications.Location = new System.Drawing.Point(15, 30);
        this.chkNotifications.Name = "chkNotifications";
        this.chkNotifications.Size = new System.Drawing.Size(130, 19);
        this.chkNotifications.TabIndex = 0;
        this.chkNotifications.Text = "Enable Notifications";

        this.lblPollingInterval.AutoSize = true;
        this.lblPollingInterval.Location = new System.Drawing.Point(15, 60);
        this.lblPollingInterval.Name = "lblPollingInterval";
        this.lblPollingInterval.Size = new System.Drawing.Size(73, 15);
        this.lblPollingInterval.Text = "Check every:";

        this.nudPollingInterval.Location = new System.Drawing.Point(95, 57);
        this.nudPollingInterval.Maximum = new decimal(new int[] { 60, 0, 0, 0 });
        this.nudPollingInterval.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
        this.nudPollingInterval.Name = "nudPollingInterval";
        this.nudPollingInterval.Size = new System.Drawing.Size(45, 23);
        this.nudPollingInterval.TabIndex = 1;
        this.nudPollingInterval.Value = new decimal(new int[] { 1, 0, 0, 0 });

        this.lblPollingMinutes.AutoSize = true;
        this.lblPollingMinutes.Location = new System.Drawing.Point(145, 60);
        this.lblPollingMinutes.Name = "lblPollingMinutes";
        this.lblPollingMinutes.Size = new System.Drawing.Size(28, 15);
        this.lblPollingMinutes.Text = "min";

        // 
        // btnSave
        // 
        this.btnSave.BackColor = System.Drawing.Color.FromArgb(0, 120, 215);
        this.btnSave.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        this.btnSave.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
        this.btnSave.ForeColor = System.Drawing.Color.White;
        this.btnSave.Location = new System.Drawing.Point(380, 120);
        this.btnSave.Name = "btnSave";
        this.btnSave.Size = new System.Drawing.Size(95, 35);
        this.btnSave.TabIndex = 4;
        this.btnSave.Text = "Save Config";
        this.btnSave.UseVisualStyleBackColor = false;
        this.btnSave.Click += new System.EventHandler(this.btnSave_Click);

        // 
        // btnClear
        // 
        this.btnClear.Location = new System.Drawing.Point(485, 120);
        this.btnClear.Name = "btnClear";
        this.btnClear.Size = new System.Drawing.Size(85, 35);
        this.btnClear.TabIndex = 5;
        this.btnClear.Text = "Clear";
        this.btnClear.Click += new System.EventHandler(this.btnClear_Click);

        // 
        // lblStatus
        // 
        this.lblStatus.AutoSize = true;
        this.lblStatus.Location = new System.Drawing.Point(12, 380);
        this.lblStatus.Name = "lblStatus";
        this.lblStatus.Size = new System.Drawing.Size(230, 15);
        this.lblStatus.Text = "Configure your gaming schedule and folders.";

        // 
        // Form1
        // 
        this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(584, 411);
        this.Controls.Add(this.grpDays);
        this.Controls.Add(this.grpTime);
        this.Controls.Add(this.grpFolders);
        this.Controls.Add(this.grpSettings);
        this.Controls.Add(this.btnSave);
        this.Controls.Add(this.btnClear);
        this.Controls.Add(this.lblStatus);
        this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
        this.MaximizeBox = false;
        this.Name = "Form1";
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
        this.Text = "GameLocker Configuration";
        this.Load += new System.EventHandler(this.Form1_Load);
        
        this.grpDays.ResumeLayout(false);
        this.grpDays.PerformLayout();
        this.grpTime.ResumeLayout(false);
        this.grpTime.PerformLayout();
        this.grpFolders.ResumeLayout(false);
        this.grpSettings.ResumeLayout(false);
        this.grpSettings.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)(this.nudDuration)).EndInit();
        ((System.ComponentModel.ISupportInitialize)(this.nudPollingInterval)).EndInit();
        this.ResumeLayout(false);
        this.PerformLayout();
    }

    #endregion

    private System.Windows.Forms.GroupBox grpDays;
    private System.Windows.Forms.GroupBox grpTime;
    private System.Windows.Forms.GroupBox grpFolders;
    private System.Windows.Forms.GroupBox grpSettings;
    
    private System.Windows.Forms.CheckBox chkSunday;
    private System.Windows.Forms.CheckBox chkMonday;
    private System.Windows.Forms.CheckBox chkTuesday;
    private System.Windows.Forms.CheckBox chkWednesday;
    private System.Windows.Forms.CheckBox chkThursday;
    private System.Windows.Forms.CheckBox chkFriday;
    private System.Windows.Forms.CheckBox chkSaturday;
    
    private System.Windows.Forms.Label lblStartTime;
    private System.Windows.Forms.DateTimePicker dtpStartTime;
    private System.Windows.Forms.Label lblDuration;
    private System.Windows.Forms.NumericUpDown nudDuration;
    private System.Windows.Forms.Label lblDurationHours;
    
    private System.Windows.Forms.ListBox lstFolders;
    private System.Windows.Forms.Button btnAddFolder;
    private System.Windows.Forms.Button btnRemoveFolder;
    private System.Windows.Forms.Button btnTestLock;
    
    private System.Windows.Forms.CheckBox chkNotifications;
    private System.Windows.Forms.Label lblPollingInterval;
    private System.Windows.Forms.NumericUpDown nudPollingInterval;
    private System.Windows.Forms.Label lblPollingMinutes;
    
    private System.Windows.Forms.Button btnSave;
    private System.Windows.Forms.Button btnClear;
    private System.Windows.Forms.Label lblStatus;
}
