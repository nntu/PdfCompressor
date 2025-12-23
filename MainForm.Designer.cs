namespace PdfCompressor;

partial class MainForm
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
        System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
        selectFileButton = new Button();
        compressFileButton = new Button();
        statusLabel = new Label();
        openFileDialog = new OpenFileDialog();
        saveFileDialog = new SaveFileDialog();
        tabControl = new TabControl();
        tabProgram = new TabPage();
        logTextBox = new TextBox();
        groupBoxCompressionSettings = new GroupBox();
        checkBoxOptimizeForScanned = new CheckBox();
        labelQualityValue = new Label();
        trackBarImageQuality = new TrackBar();
        labelImageQuality = new Label();
        labelCompressionType = new Label();
        comboBoxCompressionType = new ComboBox();
        groupBoxSplitOptions = new GroupBox();
        textBoxDocumentType = new TextBox();
        labelDocumentType = new Label();
        labelSplitSizeUnit = new Label();
        textBoxSplitSize = new TextBox();
        labelSplitSize = new Label();
        checkBoxEnableSplitting = new CheckBox();
        progressBar = new ProgressBar();
        tabMerge = new TabPage();
        groupBoxMergeFiles = new GroupBox();
        mergePdfListBox = new ListBox();
        groupBoxMergeControls = new GroupBox();
        addPdfButton = new Button();
        removePdfButton = new Button();
        moveUpButton = new Button();
        moveDownButton = new Button();
        mergeButton = new Button();
        mergeProgressBar = new ProgressBar();
        mergeStatusLabel = new Label();
        groupBoxMergeLog = new GroupBox();
        mergeLogTextBox = new TextBox();
        tabAbout = new TabPage();
        labelAuthorInfo = new Label();
        labelVersion = new Label();
        labelPhone = new Label();
        labelEmail = new Label();
        labelAppName = new Label();
        saveMergeFileDialog = new SaveFileDialog();
        openPdfFileDialog = new OpenFileDialog();
        tabControl.SuspendLayout();
        tabProgram.SuspendLayout();
        groupBoxCompressionSettings.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)trackBarImageQuality).BeginInit();
        groupBoxSplitOptions.SuspendLayout();
        tabMerge.SuspendLayout();
        groupBoxMergeFiles.SuspendLayout();
        groupBoxMergeControls.SuspendLayout();
        groupBoxMergeLog.SuspendLayout();
        tabAbout.SuspendLayout();
        SuspendLayout();
        // 
        // selectFileButton
        // 
        selectFileButton.Location = new Point(12, 12);
        selectFileButton.Name = "selectFileButton";
        selectFileButton.Size = new Size(120, 30);
        selectFileButton.TabIndex = 0;
        selectFileButton.Text = "Chọn file PDF";
        selectFileButton.UseVisualStyleBackColor = true;
        selectFileButton.Click += selectFileButton_Click;
        // 
        // compressFileButton
        // 
        compressFileButton.Enabled = false;
        compressFileButton.Location = new Point(142, 12);
        compressFileButton.Name = "compressFileButton";
        compressFileButton.Size = new Size(120, 30);
        compressFileButton.TabIndex = 1;
        compressFileButton.Text = "Nén PDF";
        compressFileButton.UseVisualStyleBackColor = true;
        compressFileButton.Click += compressFileButton_Click;
        // 
        // statusLabel
        // 
        statusLabel.AutoSize = true;
        statusLabel.Location = new Point(12, 410);
        statusLabel.Name = "statusLabel";
        statusLabel.Size = new Size(113, 15);
        statusLabel.TabIndex = 2;
        statusLabel.Text = "Trạng thái: Sẵn sàng";
        // 
        // openFileDialog
        // 
        openFileDialog.Filter = "PDF Files|*.pdf";
        openFileDialog.Title = "Chọn file PDF";
        // 
        // saveFileDialog
        // 
        saveFileDialog.Filter = "PDF Files|*.pdf";
        saveFileDialog.Title = "Lưu file PDF đã nén";
        // 
        // tabControl
        // 
        tabControl.Controls.Add(tabProgram);
        tabControl.Controls.Add(tabMerge);
        tabControl.Controls.Add(tabAbout);
        tabControl.Dock = DockStyle.Fill;
        tabControl.Location = new Point(0, 0);
        tabControl.Name = "tabControl";
        tabControl.SelectedIndex = 0;
        tabControl.Size = new Size(784, 461);
        tabControl.TabIndex = 0;
        // 
        // tabProgram
        // 
        tabProgram.Controls.Add(selectFileButton);
        tabProgram.Controls.Add(compressFileButton);
        tabProgram.Controls.Add(logTextBox);
        tabProgram.Controls.Add(groupBoxCompressionSettings);
        tabProgram.Controls.Add(groupBoxSplitOptions);
        tabProgram.Controls.Add(progressBar);
        tabProgram.Controls.Add(statusLabel);
        tabProgram.Location = new Point(4, 24);
        tabProgram.Name = "tabProgram";
        tabProgram.Padding = new Padding(3);
        tabProgram.Size = new Size(776, 433);
        tabProgram.TabIndex = 0;
        tabProgram.Text = "Chương trình";
        tabProgram.UseVisualStyleBackColor = true;
        // 
        // logTextBox
        // 
        logTextBox.Location = new Point(12, 60);
        logTextBox.Multiline = true;
        logTextBox.Name = "logTextBox";
        logTextBox.ReadOnly = true;
        logTextBox.ScrollBars = ScrollBars.Vertical;
        logTextBox.Size = new Size(380, 200);
        logTextBox.TabIndex = 3;
        logTextBox.Text = "Nhật ký:\r\n";
        // 
        // groupBoxCompressionSettings
        // 
        groupBoxCompressionSettings.Controls.Add(checkBoxOptimizeForScanned);
        groupBoxCompressionSettings.Controls.Add(labelQualityValue);
        groupBoxCompressionSettings.Controls.Add(trackBarImageQuality);
        groupBoxCompressionSettings.Controls.Add(labelImageQuality);
        groupBoxCompressionSettings.Controls.Add(labelCompressionType);
        groupBoxCompressionSettings.Controls.Add(comboBoxCompressionType);
        groupBoxCompressionSettings.Location = new Point(410, 12);
        groupBoxCompressionSettings.Name = "groupBoxCompressionSettings";
        groupBoxCompressionSettings.Size = new Size(360, 200);
        groupBoxCompressionSettings.TabIndex = 4;
        groupBoxCompressionSettings.TabStop = false;
        groupBoxCompressionSettings.Text = "Cài đặt nén";
        // 
        // checkBoxOptimizeForScanned
        // 
        checkBoxOptimizeForScanned.AutoSize = true;
        checkBoxOptimizeForScanned.Checked = true;
        checkBoxOptimizeForScanned.CheckState = CheckState.Checked;
        checkBoxOptimizeForScanned.Location = new Point(15, 150);
        checkBoxOptimizeForScanned.Name = "checkBoxOptimizeForScanned";
        checkBoxOptimizeForScanned.Size = new Size(148, 19);
        checkBoxOptimizeForScanned.TabIndex = 5;
        checkBoxOptimizeForScanned.Text = "Tối ưu cho tài liệu scan";
        checkBoxOptimizeForScanned.UseVisualStyleBackColor = true;
        // 
        // labelQualityValue
        // 
        labelQualityValue.AutoSize = true;
        labelQualityValue.Location = new Point(220, 108);
        labelQualityValue.Name = "labelQualityValue";
        labelQualityValue.Size = new Size(29, 15);
        labelQualityValue.TabIndex = 4;
        labelQualityValue.Text = "75%";
        // 
        // trackBarImageQuality
        // 
        trackBarImageQuality.Location = new Point(15, 100);
        trackBarImageQuality.Maximum = 100;
        trackBarImageQuality.Minimum = 10;
        trackBarImageQuality.Name = "trackBarImageQuality";
        trackBarImageQuality.Size = new Size(200, 45);
        trackBarImageQuality.TabIndex = 3;
        trackBarImageQuality.Value = 75;
        trackBarImageQuality.ValueChanged += trackBarImageQuality_ValueChanged;
        // 
        // labelImageQuality
        // 
        labelImageQuality.AutoSize = true;
        labelImageQuality.Location = new Point(15, 80);
        labelImageQuality.Name = "labelImageQuality";
        labelImageQuality.Size = new Size(92, 15);
        labelImageQuality.TabIndex = 2;
        labelImageQuality.Text = "Chất lượng ảnh:";
        // 
        // labelCompressionType
        // 
        labelCompressionType.AutoSize = true;
        labelCompressionType.Location = new Point(15, 25);
        labelCompressionType.Name = "labelCompressionType";
        labelCompressionType.Size = new Size(55, 15);
        labelCompressionType.TabIndex = 0;
        labelCompressionType.Text = "Loại nén:";
        // 
        // comboBoxCompressionType
        // 
        comboBoxCompressionType.DropDownStyle = ComboBoxStyle.DropDownList;
        comboBoxCompressionType.FormattingEnabled = true;
        comboBoxCompressionType.Items.AddRange(new object[] { "Tự động (Tốt nhất)", "Lossless tối ưu (mutool + qpdf)", "Hybrid (mutool + Ghostscript + qpdf)", "Screen (Thấp nhất)", "Ebook (Thấp)", "Printer (Cao)", "Prepress (Cao nhất)", "Mặc định" });
        comboBoxCompressionType.Location = new Point(15, 45);
        comboBoxCompressionType.Name = "comboBoxCompressionType";
        comboBoxCompressionType.Size = new Size(200, 23);
        comboBoxCompressionType.TabIndex = 1;
        // 
        // groupBoxSplitOptions
        // 
        groupBoxSplitOptions.Controls.Add(textBoxDocumentType);
        groupBoxSplitOptions.Controls.Add(labelDocumentType);
        groupBoxSplitOptions.Controls.Add(labelSplitSizeUnit);
        groupBoxSplitOptions.Controls.Add(textBoxSplitSize);
        groupBoxSplitOptions.Controls.Add(labelSplitSize);
        groupBoxSplitOptions.Controls.Add(checkBoxEnableSplitting);
        groupBoxSplitOptions.Location = new Point(410, 230);
        groupBoxSplitOptions.Name = "groupBoxSplitOptions";
        groupBoxSplitOptions.Size = new Size(360, 120);
        groupBoxSplitOptions.TabIndex = 6;
        groupBoxSplitOptions.TabStop = false;
        groupBoxSplitOptions.Text = "Thông tin & Tùy chọn";
        // 
        // textBoxDocumentType
        // 
        textBoxDocumentType.Location = new Point(105, 22);
        textBoxDocumentType.Name = "textBoxDocumentType";
        textBoxDocumentType.ReadOnly = true;
        textBoxDocumentType.Size = new Size(150, 23);
        textBoxDocumentType.TabIndex = 1;
        textBoxDocumentType.Text = "Chưa phân tích";
        // 
        // labelDocumentType
        // 
        labelDocumentType.AutoSize = true;
        labelDocumentType.Location = new Point(15, 25);
        labelDocumentType.Name = "labelDocumentType";
        labelDocumentType.Size = new Size(70, 15);
        labelDocumentType.TabIndex = 0;
        labelDocumentType.Text = "Loại tài liệu:";
        // 
        // labelSplitSizeUnit
        // 
        labelSplitSizeUnit.AutoSize = true;
        labelSplitSizeUnit.Enabled = false;
        labelSplitSizeUnit.Location = new Point(205, 80);
        labelSplitSizeUnit.Name = "labelSplitSizeUnit";
        labelSplitSizeUnit.Size = new Size(25, 15);
        labelSplitSizeUnit.TabIndex = 5;
        labelSplitSizeUnit.Text = "MB";
        // 
        // textBoxSplitSize
        // 
        textBoxSplitSize.Enabled = false;
        textBoxSplitSize.Location = new Point(140, 77);
        textBoxSplitSize.Name = "textBoxSplitSize";
        textBoxSplitSize.Size = new Size(60, 23);
        textBoxSplitSize.TabIndex = 4;
        textBoxSplitSize.Text = "5";
        // 
        // labelSplitSize
        // 
        labelSplitSize.AutoSize = true;
        labelSplitSize.Enabled = false;
        labelSplitSize.Location = new Point(30, 80);
        labelSplitSize.Name = "labelSplitSize";
        labelSplitSize.Size = new Size(100, 15);
        labelSplitSize.TabIndex = 3;
        labelSplitSize.Text = "Kích thước tối đa:";
        // 
        // checkBoxEnableSplitting
        // 
        checkBoxEnableSplitting.AutoSize = true;
        checkBoxEnableSplitting.Location = new Point(15, 50);
        checkBoxEnableSplitting.Name = "checkBoxEnableSplitting";
        checkBoxEnableSplitting.Size = new Size(225, 19);
        checkBoxEnableSplitting.TabIndex = 2;
        checkBoxEnableSplitting.Text = "Chia nhỏ file lớn (>10MB sau khi nén)";
        checkBoxEnableSplitting.UseVisualStyleBackColor = true;
        checkBoxEnableSplitting.CheckedChanged += checkBoxEnableSplitting_CheckedChanged;
        // 
        // progressBar
        // 
        progressBar.Location = new Point(12, 340);
        progressBar.Name = "progressBar";
        progressBar.Size = new Size(380, 23);
        progressBar.TabIndex = 5;
        progressBar.Visible = false;
        // 
        // tabMerge
        // 
        tabMerge.Controls.Add(groupBoxMergeFiles);
        tabMerge.Controls.Add(groupBoxMergeControls);
        tabMerge.Controls.Add(groupBoxMergeLog);
        tabMerge.Location = new Point(4, 24);
        tabMerge.Name = "tabMerge";
        tabMerge.Padding = new Padding(3);
        tabMerge.Size = new Size(776, 433);
        tabMerge.TabIndex = 1;
        tabMerge.Text = "Gộp PDF";
        tabMerge.UseVisualStyleBackColor = true;
        // 
        // groupBoxMergeFiles
        // 
        groupBoxMergeFiles.Controls.Add(mergePdfListBox);
        groupBoxMergeFiles.Location = new Point(12, 12);
        groupBoxMergeFiles.Name = "groupBoxMergeFiles";
        groupBoxMergeFiles.Size = new Size(400, 300);
        groupBoxMergeFiles.TabIndex = 0;
        groupBoxMergeFiles.TabStop = false;
        groupBoxMergeFiles.Text = "Danh sách file PDF";
        // 
        // mergePdfListBox
        // 
        mergePdfListBox.FormattingEnabled = true;
        mergePdfListBox.Location = new Point(15, 25);
        mergePdfListBox.Name = "mergePdfListBox";
        mergePdfListBox.SelectionMode = SelectionMode.MultiExtended;
        mergePdfListBox.Size = new Size(370, 259);
        mergePdfListBox.TabIndex = 0;
        mergePdfListBox.SelectedIndexChanged += mergePdfListBox_SelectedIndexChanged;
        // 
        // groupBoxMergeControls
        // 
        groupBoxMergeControls.Controls.Add(addPdfButton);
        groupBoxMergeControls.Controls.Add(removePdfButton);
        groupBoxMergeControls.Controls.Add(moveUpButton);
        groupBoxMergeControls.Controls.Add(moveDownButton);
        groupBoxMergeControls.Controls.Add(mergeButton);
        groupBoxMergeControls.Controls.Add(mergeProgressBar);
        groupBoxMergeControls.Controls.Add(mergeStatusLabel);
        groupBoxMergeControls.Location = new Point(420, 12);
        groupBoxMergeControls.Name = "groupBoxMergeControls";
        groupBoxMergeControls.Size = new Size(340, 300);
        groupBoxMergeControls.TabIndex = 1;
        groupBoxMergeControls.TabStop = false;
        groupBoxMergeControls.Text = "Điều khiển";
        // 
        // addPdfButton
        // 
        addPdfButton.Location = new Point(15, 25);
        addPdfButton.Name = "addPdfButton";
        addPdfButton.Size = new Size(100, 30);
        addPdfButton.TabIndex = 0;
        addPdfButton.Text = "Thêm file";
        addPdfButton.UseVisualStyleBackColor = true;
        addPdfButton.Click += addPdfButton_Click;
        // 
        // removePdfButton
        // 
        removePdfButton.Location = new Point(125, 25);
        removePdfButton.Name = "removePdfButton";
        removePdfButton.Size = new Size(100, 30);
        removePdfButton.TabIndex = 1;
        removePdfButton.Text = "Xóa file";
        removePdfButton.UseVisualStyleBackColor = true;
        removePdfButton.Click += removePdfButton_Click;
        // 
        // moveUpButton
        // 
        moveUpButton.Location = new Point(235, 25);
        moveUpButton.Name = "moveUpButton";
        moveUpButton.Size = new Size(90, 30);
        moveUpButton.TabIndex = 2;
        moveUpButton.Text = "Lên trên";
        moveUpButton.UseVisualStyleBackColor = true;
        moveUpButton.Click += moveUpButton_Click;
        // 
        // moveDownButton
        // 
        moveDownButton.Location = new Point(15, 65);
        moveDownButton.Name = "moveDownButton";
        moveDownButton.Size = new Size(90, 30);
        moveDownButton.TabIndex = 3;
        moveDownButton.Text = "Xuống dưới";
        moveDownButton.UseVisualStyleBackColor = true;
        moveDownButton.Click += moveDownButton_Click;
        // 
        // mergeButton
        // 
        mergeButton.Enabled = false;
        mergeButton.Location = new Point(125, 65);
        mergeButton.Name = "mergeButton";
        mergeButton.Size = new Size(100, 30);
        mergeButton.TabIndex = 4;
        mergeButton.Text = "Gộp PDF";
        mergeButton.UseVisualStyleBackColor = true;
        mergeButton.Click += mergeButton_Click;
        // 
        // mergeProgressBar
        // 
        mergeProgressBar.Location = new Point(15, 105);
        mergeProgressBar.Name = "mergeProgressBar";
        mergeProgressBar.Size = new Size(310, 23);
        mergeProgressBar.TabIndex = 5;
        mergeProgressBar.Visible = false;
        // 
        // mergeStatusLabel
        // 
        mergeStatusLabel.AutoSize = true;
        mergeStatusLabel.Location = new Point(15, 140);
        mergeStatusLabel.Name = "mergeStatusLabel";
        mergeStatusLabel.Size = new Size(113, 15);
        mergeStatusLabel.TabIndex = 6;
        mergeStatusLabel.Text = "Trạng thái: Sẵn sàng";
        // 
        // groupBoxMergeLog
        // 
        groupBoxMergeLog.Controls.Add(mergeLogTextBox);
        groupBoxMergeLog.Location = new Point(12, 325);
        groupBoxMergeLog.Name = "groupBoxMergeLog";
        groupBoxMergeLog.Size = new Size(748, 95);
        groupBoxMergeLog.TabIndex = 2;
        groupBoxMergeLog.TabStop = false;
        groupBoxMergeLog.Text = "Nhật ký gộp file";
        // 
        // mergeLogTextBox
        // 
        mergeLogTextBox.Location = new Point(10, 20);
        mergeLogTextBox.Multiline = true;
        mergeLogTextBox.Name = "mergeLogTextBox";
        mergeLogTextBox.ReadOnly = true;
        mergeLogTextBox.ScrollBars = ScrollBars.Vertical;
        mergeLogTextBox.Size = new Size(728, 65);
        mergeLogTextBox.TabIndex = 0;
        mergeLogTextBox.Text = "Nhật ký gộp file:\r\n";
        // 
        // tabAbout
        // 
        tabAbout.Controls.Add(labelAuthorInfo);
        tabAbout.Controls.Add(labelVersion);
        tabAbout.Controls.Add(labelPhone);
        tabAbout.Controls.Add(labelEmail);
        tabAbout.Controls.Add(labelAppName);
        tabAbout.Location = new Point(4, 24);
        tabAbout.Name = "tabAbout";
        tabAbout.Padding = new Padding(3);
        tabAbout.Size = new Size(776, 433);
        tabAbout.TabIndex = 2;
        tabAbout.Text = "Thông tin";
        tabAbout.UseVisualStyleBackColor = true;
        // 
        // labelAuthorInfo
        // 
        labelAuthorInfo.AutoSize = true;
        labelAuthorInfo.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Bold, GraphicsUnit.Point, 0);
        labelAuthorInfo.Location = new Point(209, 102);
        labelAuthorInfo.Name = "labelAuthorInfo";
        labelAuthorInfo.Size = new Size(208, 20);
        labelAuthorInfo.TabIndex = 3;
        labelAuthorInfo.Text = "Tác giả: Nguyễn Ngọc Tú";
        // 
        // labelVersion
        // 
        labelVersion.AutoSize = true;
        labelVersion.Location = new Point(309, 62);
        labelVersion.Name = "labelVersion";
        labelVersion.Size = new Size(90, 15);
        labelVersion.TabIndex = 2;
        labelVersion.Text = "Phiên bản: 1.0.0";
        // 
        // labelPhone
        // 
        labelPhone.AutoSize = true;
        labelPhone.Location = new Point(238, 148);
        labelPhone.Name = "labelPhone";
        labelPhone.Size = new Size(127, 15);
        labelPhone.TabIndex = 5;
        labelPhone.Text = "Điện thoại: 0983862402";
        // 
        // labelEmail
        // 
        labelEmail.AutoSize = true;
        labelEmail.Location = new Point(238, 133);
        labelEmail.Name = "labelEmail";
        labelEmail.Size = new Size(150, 15);
        labelEmail.TabIndex = 4;
        labelEmail.Text = "Email: tunn1@bidv.com.vn";
        // 
        // labelAppName
        // 
        labelAppName.AutoSize = true;
        labelAppName.Font = new Font("Microsoft Sans Serif", 18F, FontStyle.Bold, GraphicsUnit.Point, 0);
        labelAppName.Location = new Point(259, 22);
        labelAppName.Name = "labelAppName";
        labelAppName.Size = new Size(214, 29);
        labelAppName.TabIndex = 1;
        labelAppName.Text = "PDF Compressor";
        // 
        // saveMergeFileDialog
        // 
        saveMergeFileDialog.Filter = "PDF Files|*.pdf";
        saveMergeFileDialog.Title = "Lưu file PDF đã gộp";
        // 
        // openPdfFileDialog
        // 
        openPdfFileDialog.Filter = "PDF Files|*.pdf";
        openPdfFileDialog.Multiselect = true;
        openPdfFileDialog.Title = "Chọn file PDF để gộp";
        // 
        // MainForm
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(784, 461);
        Controls.Add(tabControl);
        Icon = (Icon)resources.GetObject("$this.Icon");
        Name = "MainForm";
        Text = "PDF Compressor - Tối ưu hóa file PDF";
        FormClosing += MainForm_FormClosing;
        Load += MainForm_Load;
        tabControl.ResumeLayout(false);
        tabProgram.ResumeLayout(false);
        tabProgram.PerformLayout();
        groupBoxCompressionSettings.ResumeLayout(false);
        groupBoxCompressionSettings.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)trackBarImageQuality).EndInit();
        groupBoxSplitOptions.ResumeLayout(false);
        groupBoxSplitOptions.PerformLayout();
        tabMerge.ResumeLayout(false);
        groupBoxMergeFiles.ResumeLayout(false);
        groupBoxMergeControls.ResumeLayout(false);
        groupBoxMergeControls.PerformLayout();
        groupBoxMergeLog.ResumeLayout(false);
        groupBoxMergeLog.PerformLayout();
        tabAbout.ResumeLayout(false);
        tabAbout.PerformLayout();
        ResumeLayout(false);
    }

    #endregion

    private System.Windows.Forms.TabControl tabControl;
    private System.Windows.Forms.TabPage tabProgram;
    private System.Windows.Forms.TabPage tabAbout;
    private System.Windows.Forms.Button selectFileButton;
    private System.Windows.Forms.Button compressFileButton;
    private System.Windows.Forms.Label statusLabel;
    private System.Windows.Forms.OpenFileDialog openFileDialog;
    private System.Windows.Forms.SaveFileDialog saveFileDialog;
    private System.Windows.Forms.TextBox logTextBox;
    private System.Windows.Forms.GroupBox groupBoxCompressionSettings;
    private System.Windows.Forms.Label labelCompressionType;
    private System.Windows.Forms.ComboBox comboBoxCompressionType;
    private System.Windows.Forms.Label labelImageQuality;
    private System.Windows.Forms.TrackBar trackBarImageQuality;
    private System.Windows.Forms.Label labelQualityValue;
    private System.Windows.Forms.CheckBox checkBoxOptimizeForScanned;
    private System.Windows.Forms.ProgressBar progressBar;
    private System.Windows.Forms.GroupBox groupBoxSplitOptions;
    private System.Windows.Forms.CheckBox checkBoxEnableSplitting;
    private System.Windows.Forms.Label labelSplitSize;
    private System.Windows.Forms.TextBox textBoxSplitSize;
    private System.Windows.Forms.Label labelSplitSizeUnit;
    private System.Windows.Forms.Label labelDocumentType;
    private System.Windows.Forms.TextBox textBoxDocumentType;
    private System.Windows.Forms.Label labelAuthorInfo;
    private System.Windows.Forms.Label labelAppName;
    private System.Windows.Forms.Label labelVersion;
    private System.Windows.Forms.Label labelEmail;
    private System.Windows.Forms.Label labelPhone;
    private System.Windows.Forms.TabPage tabMerge;
    private System.Windows.Forms.ListBox mergePdfListBox;
    private System.Windows.Forms.Button addPdfButton;
    private System.Windows.Forms.Button removePdfButton;
    private System.Windows.Forms.Button moveUpButton;
    private System.Windows.Forms.Button moveDownButton;
    private System.Windows.Forms.Button mergeButton;
    private System.Windows.Forms.ProgressBar mergeProgressBar;
    private System.Windows.Forms.Label mergeStatusLabel;
    private System.Windows.Forms.SaveFileDialog saveMergeFileDialog;
    private System.Windows.Forms.OpenFileDialog openPdfFileDialog;
    private System.Windows.Forms.GroupBox groupBoxMergeFiles;
    private System.Windows.Forms.GroupBox groupBoxMergeControls;
    private System.Windows.Forms.TextBox mergeLogTextBox;
    private System.Windows.Forms.GroupBox groupBoxMergeLog;
}
