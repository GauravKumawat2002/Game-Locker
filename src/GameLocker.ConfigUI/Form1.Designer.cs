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
        this.grpExtensions = new System.Windows.Forms.GroupBox();
        
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
        this.lblSchedulePreview = new System.Windows.Forms.Label();
        
        // Folder controls
        this.lstFolders = new System.Windows.Forms.ListBox();
        this.btnAddFolder = new System.Windows.Forms.Button();
        this.btnRemoveFolder = new System.Windows.Forms.Button();
        this.btnTestLock = new System.Windows.Forms.Button();
        
        // Extension controls (NEW)
        this.clbExtensions = new System.Windows.Forms.CheckedListBox();
        this.lblExtensionInfo = new System.Windows.Forms.Label();
        this.btnSelectSafe = new System.Windows.Forms.Button();
        this.btnSelectNone = new System.Windows.Forms.Button();
        this.btnRescan = new System.Windows.Forms.Button();
        
        // Settings controls
        this.chkNotifications = new System.Windows.Forms.CheckBox();
        this.chkVault = new System.Windows.Forms.CheckBox();
        
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
        this.grpExtensions.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)(this.nudDuration)).BeginInit();
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
        this.grpDays.Size = new System.Drawing.Size(360, 85);
        this.grpDays.TabIndex = 0;
        this.grpDays.TabStop = false;
        this.grpDays.Text = "Allowed Gaming Days";

        // Day checkboxes
        this.chkSunday.AutoSize = true;
        this.chkSunday.Location = new System.Drawing.Point(15, 25);
        this.chkSunday.Name = "chkSunday";
        this.chkSunday.Size = new System.Drawing.Size(66, 19);
        this.chkSunday.TabIndex = 0;
        this.chkSunday.Text = "Sunday";

        this.chkMonday.AutoSize = true;
        this.chkMonday.Location = new System.Drawing.Point(95, 25);
        this.chkMonday.Name = "chkMonday";
        this.chkMonday.Size = new System.Drawing.Size(70, 19);
        this.chkMonday.TabIndex = 1;
        this.chkMonday.Text = "Monday";

        this.chkTuesday.AutoSize = true;
        this.chkTuesday.Location = new System.Drawing.Point(175, 25);
        this.chkTuesday.Name = "chkTuesday";
        this.chkTuesday.Size = new System.Drawing.Size(68, 19);
        this.chkTuesday.TabIndex = 2;
        this.chkTuesday.Text = "Tuesday";

        this.chkWednesday.AutoSize = true;
        this.chkWednesday.Location = new System.Drawing.Point(255, 25);
        this.chkWednesday.Name = "chkWednesday";
        this.chkWednesday.Size = new System.Drawing.Size(86, 19);
        this.chkWednesday.TabIndex = 3;
        this.chkWednesday.Text = "Wednesday";

        this.chkThursday.AutoSize = true;
        this.chkThursday.Location = new System.Drawing.Point(15, 50);
        this.chkThursday.Name = "chkThursday";
        this.chkThursday.Size = new System.Drawing.Size(74, 19);
        this.chkThursday.TabIndex = 4;
        this.chkThursday.Text = "Thursday";

        this.chkFriday.AutoSize = true;
        this.chkFriday.Location = new System.Drawing.Point(95, 50);
        this.chkFriday.Name = "chkFriday";
        this.chkFriday.Size = new System.Drawing.Size(58, 19);
        this.chkFriday.TabIndex = 5;
        this.chkFriday.Text = "Friday";

        this.chkSaturday.AutoSize = true;
        this.chkSaturday.Location = new System.Drawing.Point(175, 50);
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
        this.grpTime.Controls.Add(this.lblSchedulePreview);
        this.grpTime.Location = new System.Drawing.Point(12, 105);
        this.grpTime.Name = "grpTime";
        this.grpTime.Size = new System.Drawing.Size(360, 83);
        this.grpTime.TabIndex = 1;
        this.grpTime.TabStop = false;
        this.grpTime.Text = "Gaming Time Window";

        this.lblStartTime.AutoSize = true;
        this.lblStartTime.Location = new System.Drawing.Point(15, 25);
        this.lblStartTime.Name = "lblStartTime";
        this.lblStartTime.Size = new System.Drawing.Size(63, 15);
        this.lblStartTime.Text = "Start Time:";

        this.dtpStartTime.Format = System.Windows.Forms.DateTimePickerFormat.Time;
        this.dtpStartTime.Location = new System.Drawing.Point(85, 22);
        this.dtpStartTime.Name = "dtpStartTime";
        this.dtpStartTime.ShowUpDown = true;
        this.dtpStartTime.Size = new System.Drawing.Size(90, 23);
        this.dtpStartTime.TabIndex = 0;

        this.lblDuration.AutoSize = true;
        this.lblDuration.Location = new System.Drawing.Point(195, 25);
        this.lblDuration.Name = "lblDuration";
        this.lblDuration.Size = new System.Drawing.Size(56, 15);
        this.lblDuration.Text = "Duration:";

        this.nudDuration.Location = new System.Drawing.Point(260, 22);
        this.nudDuration.Maximum = new decimal(new int[] { 24, 0, 0, 0 });
        this.nudDuration.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
        this.nudDuration.Name = "nudDuration";
        this.nudDuration.Size = new System.Drawing.Size(50, 23);
        this.nudDuration.TabIndex = 1;
        this.nudDuration.Value = new decimal(new int[] { 2, 0, 0, 0 });

        this.lblDurationHours.AutoSize = true;
        this.lblDurationHours.Location = new System.Drawing.Point(315, 25);
        this.lblDurationHours.Name = "lblDurationHours";
        this.lblDurationHours.Size = new System.Drawing.Size(36, 15);
        this.lblDurationHours.Text = "hours";

        this.lblSchedulePreview.Location = new System.Drawing.Point(15, 50);
        this.lblSchedulePreview.Name = "lblSchedulePreview";
        this.lblSchedulePreview.Size = new System.Drawing.Size(332, 30);

        // 
        // grpFolders
        // 
        this.grpFolders.Controls.Add(this.lstFolders);
        this.grpFolders.Controls.Add(this.btnAddFolder);
        this.grpFolders.Controls.Add(this.btnRemoveFolder);
        this.grpFolders.Controls.Add(this.btnTestLock);
        this.grpFolders.Location = new System.Drawing.Point(12, 196);
        this.grpFolders.Name = "grpFolders";
        this.grpFolders.Size = new System.Drawing.Size(360, 145);
        this.grpFolders.TabIndex = 2;
        this.grpFolders.TabStop = false;
        this.grpFolders.Text = "Game Folders to Lock (click folder to see file types)";

        this.lstFolders.FormattingEnabled = true;
        this.lstFolders.ItemHeight = 15;
        this.lstFolders.Location = new System.Drawing.Point(15, 22);
        this.lstFolders.Name = "lstFolders";
        this.lstFolders.Size = new System.Drawing.Size(240, 109);
        this.lstFolders.TabIndex = 0;
        this.lstFolders.SelectedIndexChanged += new System.EventHandler(this.lstFolders_SelectedIndexChanged);

        this.btnAddFolder.Location = new System.Drawing.Point(265, 22);
        this.btnAddFolder.Name = "btnAddFolder";
        this.btnAddFolder.Size = new System.Drawing.Size(85, 28);
        this.btnAddFolder.TabIndex = 1;
        this.btnAddFolder.Text = "Add Folder...";
        this.btnAddFolder.Click += new System.EventHandler(this.btnAddFolder_Click);

        this.btnRemoveFolder.Location = new System.Drawing.Point(265, 58);
        this.btnRemoveFolder.Name = "btnRemoveFolder";
        this.btnRemoveFolder.Size = new System.Drawing.Size(85, 28);
        this.btnRemoveFolder.TabIndex = 2;
        this.btnRemoveFolder.Text = "Remove";
        this.btnRemoveFolder.Click += new System.EventHandler(this.btnRemoveFolder_Click);

        this.btnTestLock.Location = new System.Drawing.Point(265, 103);
        this.btnTestLock.Name = "btnTestLock";
        this.btnTestLock.Size = new System.Drawing.Size(85, 28);
        this.btnTestLock.TabIndex = 3;
        this.btnTestLock.Text = "Test Lock";
        this.btnTestLock.Click += new System.EventHandler(this.btnTestLock_Click);

        // 
        // grpExtensions (NEW - File Type Selection)
        // 
        this.grpExtensions.Controls.Add(this.clbExtensions);
        this.grpExtensions.Controls.Add(this.lblExtensionInfo);
        this.grpExtensions.Controls.Add(this.btnSelectSafe);
        this.grpExtensions.Controls.Add(this.btnSelectNone);
        this.grpExtensions.Controls.Add(this.btnRescan);
        this.grpExtensions.Location = new System.Drawing.Point(380, 12);
        this.grpExtensions.Name = "grpExtensions";
        this.grpExtensions.Size = new System.Drawing.Size(300, 300);
        this.grpExtensions.TabIndex = 3;
        this.grpExtensions.TabStop = false;
        this.grpExtensions.Text = "Vault file types";

        this.lblExtensionInfo.AutoSize = false;
        this.lblExtensionInfo.Location = new System.Drawing.Point(10, 22);
        this.lblExtensionInfo.Name = "lblExtensionInfo";
        this.lblExtensionInfo.Size = new System.Drawing.Size(280, 30);
        this.lblExtensionInfo.Text = "Select a folder to view file types";

        this.clbExtensions.FormattingEnabled = true;
        this.clbExtensions.AccessibleName = "File types to encrypt";
        this.clbExtensions.Location = new System.Drawing.Point(10, 55);
        this.clbExtensions.Name = "clbExtensions";
        this.clbExtensions.Size = new System.Drawing.Size(280, 202);
        this.clbExtensions.TabIndex = 0;
        this.clbExtensions.CheckOnClick = true;
        this.clbExtensions.ItemCheck += new System.Windows.Forms.ItemCheckEventHandler(this.clbExtensions_ItemCheck);

        this.btnSelectSafe.Location = new System.Drawing.Point(10, 263);
        this.btnSelectSafe.Name = "btnSelectSafe";
        this.btnSelectSafe.Size = new System.Drawing.Size(85, 28);
        this.btnSelectSafe.TabIndex = 1;
        this.btnSelectSafe.Text = "Save files";
        this.btnSelectSafe.Click += new System.EventHandler(this.btnSelectSafe_Click);

        this.btnSelectNone.Location = new System.Drawing.Point(105, 263);
        this.btnSelectNone.Name = "btnSelectNone";
        this.btnSelectNone.Size = new System.Drawing.Size(85, 28);
        this.btnSelectNone.TabIndex = 2;
        this.btnSelectNone.Text = "Clear selection";
        this.btnSelectNone.Click += new System.EventHandler(this.btnSelectNone_Click);

        this.btnRescan.Location = new System.Drawing.Point(200, 263);
        this.btnRescan.Name = "btnRescan";
        this.btnRescan.Size = new System.Drawing.Size(90, 28);
        this.btnRescan.TabIndex = 3;
        this.btnRescan.Text = "Scan file types";
        this.btnRescan.Click += new System.EventHandler(this.btnRescan_Click);

        // 
        // grpSettings
        // 
        this.grpSettings.Controls.Add(this.chkNotifications);
        this.grpSettings.Controls.Add(this.chkVault);
        this.grpSettings.Location = new System.Drawing.Point(380, 318);
        this.grpSettings.Name = "grpSettings";
        this.grpSettings.Size = new System.Drawing.Size(300, 70);
        this.grpSettings.TabIndex = 4;
        this.grpSettings.TabStop = false;
        this.grpSettings.Text = "Settings";

        this.chkNotifications.AutoSize = true;
        this.chkNotifications.Checked = true;
        this.chkNotifications.CheckState = System.Windows.Forms.CheckState.Checked;
        this.chkNotifications.Location = new System.Drawing.Point(15, 25);
        this.chkNotifications.Name = "chkNotifications";
        this.chkNotifications.Size = new System.Drawing.Size(130, 19);
        this.chkNotifications.TabIndex = 0;
        this.chkNotifications.Text = "Enable Notifications";

        this.chkVault.AutoSize = true;
        this.chkVault.Location = new System.Drawing.Point(15, 46);
        this.chkVault.Name = "chkVault";
        this.chkVault.TabIndex = 1;
        this.chkVault.Text = "Encrypt selected file types when locked";
        this.chkVault.CheckedChanged += new System.EventHandler(this.chkVault_CheckedChanged);

        // 
        // btnSave
        // 
        this.btnSave.BackColor = System.Drawing.Color.FromArgb(0, 120, 215);
        this.btnSave.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        this.btnSave.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
        this.btnSave.ForeColor = System.Drawing.Color.White;
        this.btnSave.Location = new System.Drawing.Point(12, 348);
        this.btnSave.Name = "btnSave";
        this.btnSave.Size = new System.Drawing.Size(170, 35);
        this.btnSave.TabIndex = 5;
        this.btnSave.Text = "Save configuration";
        this.btnSave.UseVisualStyleBackColor = false;
        this.btnSave.Click += new System.EventHandler(this.btnSave_Click);

        // 
        // btnClear
        // 
        this.btnClear.Location = new System.Drawing.Point(195, 348);
        this.btnClear.Name = "btnClear";
        this.btnClear.Size = new System.Drawing.Size(170, 35);
        this.btnClear.TabIndex = 6;
        this.btnClear.Text = "Clear configuration";
        this.btnClear.Click += new System.EventHandler(this.btnClear_Click);

        // 
        // lblStatus
        // 
        this.lblStatus.AutoSize = false;
        this.lblStatus.Location = new System.Drawing.Point(12, 396);
        this.lblStatus.Name = "lblStatus";
        this.lblStatus.Size = new System.Drawing.Size(668, 36);
        this.lblStatus.Text = "Configure your gaming schedule and folders.";

        // 
        // Form1
        // 
        this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(694, 439);
        this.Controls.Add(this.grpDays);
        this.Controls.Add(this.grpTime);
        this.Controls.Add(this.grpFolders);
        this.Controls.Add(this.grpExtensions);
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
        this.grpExtensions.ResumeLayout(false);
        this.grpSettings.ResumeLayout(false);
        this.grpSettings.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)(this.nudDuration)).EndInit();
        this.ResumeLayout(false);
        this.PerformLayout();
    }

    #endregion

    private System.Windows.Forms.GroupBox grpDays;
    private System.Windows.Forms.GroupBox grpTime;
    private System.Windows.Forms.GroupBox grpFolders;
    private System.Windows.Forms.GroupBox grpSettings;
    private System.Windows.Forms.GroupBox grpExtensions;
    
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
    private System.Windows.Forms.Label lblSchedulePreview;
    
    private System.Windows.Forms.ListBox lstFolders;
    private System.Windows.Forms.Button btnAddFolder;
    private System.Windows.Forms.Button btnRemoveFolder;
    private System.Windows.Forms.Button btnTestLock;
    
    // Extension selection controls (NEW)
    private System.Windows.Forms.CheckedListBox clbExtensions;
    private System.Windows.Forms.Label lblExtensionInfo;
    private System.Windows.Forms.Button btnSelectSafe;
    private System.Windows.Forms.Button btnSelectNone;
    private System.Windows.Forms.Button btnRescan;
    
    private System.Windows.Forms.CheckBox chkNotifications;
    private System.Windows.Forms.CheckBox chkVault;
    
    private System.Windows.Forms.Button btnSave;
    private System.Windows.Forms.Button btnClear;
    private System.Windows.Forms.Label lblStatus;
}
