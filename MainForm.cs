using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace PdfCompressor;

public partial class MainForm : Form
{
    private string? selectedPdfPath;
    private System.Threading.Thread? compressionThread;
    private bool isCompressionRunning = false;
    private static string ProduceVersion()
    {
        // Lấy file .exe (assembly) đang chạy
        var assembly = Assembly.GetExecutingAssembly();

        // Lấy đối tượng AssemblyVersion (System.Version)
        var version = assembly.GetName().Version;

        // Trả về chuỗi (ví dụ: "1.0.9429.1890")
        return version?.ToString() ?? "unknown";
    }
    public MainForm()
    {
        InitializeComponent();
        InitializeCompressionSettings();
    }

    private void InitializeCompressionSettings()
    {
        comboBoxCompressionType.SelectedIndex = 0;
        trackBarImageQuality.Value = 75;
        UpdateQualityLabel();

        // Initialize advanced options
        checkBoxEnableSplitting.Checked = false;
        textBoxSplitSize.Text = "5";
        textBoxDocumentType.Text = "Chưa phân tích";

        // Initialize logs directory
        InitializeLogsDirectory();
    }

    private void trackBarImageQuality_ValueChanged(object sender, EventArgs e)
    {
        UpdateQualityLabel();
    }

    private void UpdateQualityLabel()
    {
        labelQualityValue.Text = trackBarImageQuality.Value + "%";
    }

    private void checkBoxEnableSplitting_CheckedChanged(object sender, EventArgs e)
    {
        bool enabled = checkBoxEnableSplitting.Checked;
        labelSplitSize.Enabled = enabled;
        textBoxSplitSize.Enabled = enabled;
        labelSplitSizeUnit.Enabled = enabled;
    }

    private void InitializeLogsDirectory()
    {
        try
        {
            var logsDirectory = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            if (!System.IO.Directory.Exists(logsDirectory))
            {
                System.IO.Directory.CreateDirectory(logsDirectory);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error creating logs directory: {ex.Message}");
        }
    }

    private void LogMessage(string message)
    {
        if (logTextBox.InvokeRequired)
        {
            logTextBox.Invoke(new Action<string>(LogMessage), message);
            return;
        }

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var logEntry = $"[{timestamp}] {message}";

        // Add to UI
        logTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\r\n");

        // Save to file
        SaveLogToFile(logEntry);
    }

    private void SaveLogToFile(string logEntry)
    {
        try
        {
            var logsDirectory = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            var logFileName = $"PDFCompressor_{DateTime.Now:yyyy-MM-dd}.log";
            var logFilePath = System.IO.Path.Combine(logsDirectory, logFileName);

            System.IO.File.AppendAllText(logFilePath, logEntry + Environment.NewLine);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving log to file: {ex.Message}");
        }
    }

    private void selectFileButton_Click(object sender, EventArgs e)
    {
        if (openFileDialog.ShowDialog() == DialogResult.OK)
        {
            selectedPdfPath = openFileDialog.FileName;
            statusLabel.Text = $"Selected file: {selectedPdfPath}";
            compressFileButton.Enabled = true;

            // Auto-generate output filename with .compress.pdf suffix
            var directory = System.IO.Path.GetDirectoryName(selectedPdfPath) ?? "";
            var filenameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(selectedPdfPath) ?? "";
            var outputPath = System.IO.Path.Combine(directory, $"{filenameWithoutExt}.compress.pdf");
            saveFileDialog.FileName = outputPath;

            LogMessage($"Đã tải file: {System.IO.Path.GetFileName(selectedPdfPath) ?? "Không xác định"}");
            LogMessage($"Kích thước file: {FormatFileSize(GetFileSize(selectedPdfPath ?? ""))}");

            // Analyze document type
            var documentType = AnalyzeDocumentType(selectedPdfPath ?? "");
            this.Invoke(new Action(() =>
            {
                textBoxDocumentType.Text = documentType;
            }));
            LogMessage($"Loại tài liệu phát hiện: {documentType}");
        }
    }

    private string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    private void compressFileButton_Click(object sender, EventArgs e)
    {
        if (isCompressionRunning)
        {
            MessageBox.Show("Compression is already in progress. Please wait.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (saveFileDialog.ShowDialog() == DialogResult.OK)
        {
            StartCompressionThread(selectedPdfPath ?? "", saveFileDialog.FileName);
        }
    }

    private void StartCompressionThread(string inputPath, string outputPath)
    {
        isCompressionRunning = true;
        compressFileButton.Enabled = false;
        progressBar.Visible = true;
        progressBar.Value = 0;

        // Read all UI values before starting the thread to avoid cross-thread operations
        var compressionSettings = new CompressionThreadSettings
        {
            CompressionType = comboBoxCompressionType.SelectedItem?.ToString() ?? "Tự động (Tốt nhất)",
            ImageQuality = trackBarImageQuality.Value,
            OptimizeForScanned = checkBoxOptimizeForScanned.Checked,
            EnableSplitting = checkBoxEnableSplitting.Checked,
            MaxSplitSizeMB = 5
        };

        if (compressionSettings.EnableSplitting && int.TryParse(textBoxSplitSize.Text, out int splitSize))
        {
            compressionSettings.MaxSplitSizeMB = splitSize;
        }

        // Get document type from analysis
        compressionSettings.DocumentType = textBoxDocumentType.Text;

        compressionThread = new System.Threading.Thread(() => CompressPdfThreaded(inputPath, outputPath, compressionSettings));
        compressionThread.IsBackground = true;
        compressionThread.Start();

        LogMessage("Bắt đầu quá trình nén...");
        statusLabel.Text = "Đang nén...";
    }

    private class CompressionThreadSettings
    {
        public string CompressionType { get; set; } = "";
        public int ImageQuality { get; set; } = 75;
        public bool OptimizeForScanned { get; set; } = false;
        public bool EnableSplitting { get; set; } = false;
        public int MaxSplitSizeMB { get; set; } = 5;
        public string DocumentType { get; set; } = "";
    }

    private async void CompressPdfThreaded(string inputPath, string outputPath, CompressionThreadSettings settings)
    {
        try
        {
            if (string.IsNullOrEmpty(inputPath) || !System.IO.File.Exists(inputPath))
            {
                throw new FileNotFoundException("Input PDF file not found", inputPath);
            }

            var ghostscriptPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Ghostscript", "gswin64c.exe");

            // Use settings from parameter instead of accessing UI controls
            var compressionType = settings.CompressionType;
            var imageQuality = settings.ImageQuality;
            var optimizeForScanned = settings.OptimizeForScanned;
            var enableSplitting = settings.EnableSplitting;
            var maxSplitSizeMB = settings.MaxSplitSizeMB;
            var documentType = settings.DocumentType;

            LogMessage($"Loại tài liệu: {documentType}");
            LogMessage($"Loại nén: {compressionType}");
            LogMessage($"Chất lượng ảnh: {imageQuality}%");
            LogMessage($"Tối ưu cho scan: {optimizeForScanned}");
            if (enableSplitting)
            {
                LogMessage($"Bật chia file (tối đa: {maxSplitSizeMB}MB)");
            }

            UpdateProgress(10);

            string bestSetting;
            CompressionSettings optimalSettings;

            if (compressionType == "Tự động (Tốt nhất)")
            {
                // Use intelligent settings based on document analysis
                optimalSettings = GetOptimalSettings(documentType, imageQuality);
                bestSetting = optimalSettings.PdfSetting;

                LogMessage($"Chế độ tự động phát hiện {documentType}");
                LogMessage($"Sử dụng thiết lập nén thông minh: {bestSetting}");
                LogMessage($"Độ phân giải màu: {optimalSettings.ColorImageResolution} DPI");
                LogMessage($"Độ phân giải xám: {optimalSettings.GrayImageResolution} DPI");
                LogMessage($"Chất lượng JPEG: {optimalSettings.JpegQuality}%");
            }
            else
            {
                bestSetting = GetPdfSettingFromType(compressionType);
                optimalSettings = new CompressionSettings
                {
                    PdfSetting = bestSetting,
                    JpegQuality = imageQuality,
                    ColorImageResolution = 300,
                    GrayImageResolution = 300
                };
                LogMessage($"Sử dụng thiết lập nén: {bestSetting}");
            }

            UpdateProgress(50);

            // Build Ghostscript arguments with optimal compression settings
            var arguments = BuildIntelligentGhostscriptArguments(inputPath, outputPath, optimalSettings, optimizeForScanned);

            LogMessage("Bắt đầu nén với Ghostscript...");
            UpdateProgress(60);

            var process = new System.Diagnostics.Process();
            process.StartInfo.FileName = ghostscriptPath;
            process.StartInfo.Arguments = arguments;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;
            process.Start();
            process.WaitForExit();

            UpdateProgress(90);

            var originalSize = GetFileSize(inputPath);
            var compressedSize = GetFileSize(outputPath);

            // Check if file splitting is needed
            if (enableSplitting && compressedSize > maxSplitSizeMB * 1024 * 1024)
            {
                LogMessage($"Kích thước file ({FormatFileSize(compressedSize)}) vượt quá giới hạn {maxSplitSizeMB}MB. Đang chia nhỏ...");
                await SplitPdfFile(outputPath, maxSplitSizeMB * 1024 * 1024);
            }

            var compressionRatio = (1 - (double)compressedSize / originalSize) * 100;

            LogMessage($"Nén thành công!");
            LogMessage($"Kích thước gốc: {FormatFileSize(originalSize)}");
            LogMessage($"Kích thước sau nén: {FormatFileSize(compressedSize)}");
            LogMessage($"Tỷ lệ nén: {compressionRatio:F1}%");

            UpdateProgress(100);

            // Update UI on main thread
            this.Invoke(new Action(() =>
            {
                statusLabel.Text = "Nén hoàn tất!";

                var result = MessageBox.Show(
                    $"Nén hoàn tất!\n\nLoại tài liệu: {documentType}\nTối ưu hóa: {bestSetting}\n\nKích thước gốc: {FormatFileSize(originalSize)}\nKích thước sau nén: {FormatFileSize(compressedSize)}\nTỷ lệ nén: {compressionRatio:F1}%\n\nBạn có muốn mở thư mục kết quả không?",
                    "Thành công",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (result == DialogResult.Yes)
                {
                    OpenOutputFolder(outputPath);
                }
            }));
        }
        catch (Exception ex)
        {
            LogMessage($"Error during compression: {ex.Message}");
            this.Invoke(new Action(() =>
            {
                statusLabel.Text = "Compression failed!";
                MessageBox.Show($"Compression failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }));
        }
        finally
        {
            this.Invoke(new Action(() =>
            {
                isCompressionRunning = false;
                compressFileButton.Enabled = true;
                progressBar.Visible = false;
            }));
        }
    }

    private void UpdateProgress(int value)
    {
        if (progressBar.InvokeRequired)
        {
            progressBar.Invoke(new Action<int>(UpdateProgress), value);
            return;
        }
        progressBar.Value = value;
    }

    
    private string GetPdfSettingFromType(string type)
    {
        return type switch
        {
            "Screen (Thấp nhất)" => "/screen",
            "Ebook (Thấp)" => "/ebook",
            "Printer (Cao)" => "/printer",
            "Prepress (Cao nhất)" => "/prepress",
            "Mặc định" => "/default",
            _ => "/screen"
        };
    }

    private string FindBestCompressionSetting(string inputPath, string ghostscriptPath, bool optimizeForScanned)
    {
        var pdfSettings = new[] { "/screen", "/ebook", "/printer", "/prepress" };
        var bestSetting = "/screen";
        long smallestSize = -1;

        LogMessage("Testing compression settings to find optimal configuration...");

        for (int i = 0; i < pdfSettings.Length; i++)
        {
            var setting = pdfSettings[i];
            var tempPath = System.IO.Path.GetTempFileName();

            LogMessage($"Testing {setting} setting...");
            UpdateProgress(10 + (i * 10));

            var arguments = BuildGhostscriptArguments(inputPath, tempPath, setting, 75, optimizeForScanned);

            var process = new System.Diagnostics.Process();
            process.StartInfo.FileName = ghostscriptPath;
            process.StartInfo.Arguments = arguments;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;
            process.Start();
            process.WaitForExit();

            var size = GetFileSize(tempPath);
            LogMessage($"Result with {setting}: {FormatFileSize(size)}");

            if (smallestSize == -1 || size < smallestSize)
            {
                smallestSize = size;
                bestSetting = setting;
            }

            System.IO.File.Delete(tempPath);
        }

        return bestSetting;
    }

    private string BuildGhostscriptArguments(string inputPath, string outputPath, string pdfSetting, int imageQuality, bool optimizeForScanned)
    {
        var arguments = $"-sDEVICE=pdfwrite -dCompatibilityLevel=1.4 -dPDFSETTINGS={pdfSetting} -dNOPAUSE -dQUIET -dBATCH";

        // Add image compression settings for scanned documents
        if (optimizeForScanned)
        {
            var jpegQuality = imageQuality * 100; // Convert percentage to 0-10000 range
            arguments += $" -dAutoFilterColorImages=false -dColorImageFilter=/DCTEncode";
            arguments += $" -dAutoFilterGrayImages=false -dGrayImageFilter=/DCTEncode";
            arguments += $" -dColorImageResolution={Math.Max(150, imageQuality * 2)}";
            arguments += $" -dGrayImageResolution={Math.Max(150, imageQuality * 2)}";
            arguments += $" -dJPEGQ={imageQuality}";
        }

        arguments += $" -sOutputFile=\"{outputPath}\" \"{inputPath}\"";

        return arguments;
    }

    private string AnalyzeDocumentType(string filePath)
    {
        try
        {
            if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
                return "Unknown";

            // Read PDF file to analyze content
            var pdfContent = System.IO.File.ReadAllText(filePath, System.Text.Encoding.ASCII);

            // Check for indicators of scanned documents
            if (IsScannedDocument(pdfContent, filePath))
            {
                return "Scanned Document";
            }

            // Check for text-heavy documents
            if (IsTextDocument(pdfContent))
            {
                return "Text Document";
            }

            // Check for mixed content
            if (HasImagesAndText(pdfContent))
            {
                return "Mixed Content";
            }

            return "General Document";
        }
        catch (Exception ex)
        {
            LogMessage($"Error analyzing document: {ex.Message}");
            return "Analysis Failed";
        }
    }

    private bool IsScannedDocument(string pdfContent, string filePath)
    {
        // Check for common indicators of scanned documents
        var fileSize = GetFileSize(filePath);

        // Large file size with limited text content suggests scanned images
        if (fileSize > 5 * 1024 * 1024) // > 5MB
        {
            // Count text operators in PDF
            var textOperatorCount = CountOccurrences(pdfContent, "BT") + CountOccurrences(pdfContent, "Tj");
            var imageOperatorCount = CountOccurrences(pdfContent, "BI") + CountOccurrences(pdfContent, "ID");

            // If more image operators than text operators, likely scanned
            if (imageOperatorCount > textOperatorCount * 2)
            {
                return true;
            }
        }

        // Check for scan-related metadata
        if (pdfContent.Contains("Scanner") || pdfContent.Contains("Scan") ||
            pdfContent.Contains("TWAIN") || pdfContent.Contains("WIA"))
        {
            return true;
        }

        return false;
    }

    private bool IsTextDocument(string pdfContent)
    {
        // Check for high text content
        var textOperatorCount = CountOccurrences(pdfContent, "BT") + CountOccurrences(pdfContent, "Tj");
        var fontCount = CountOccurrences(pdfContent, "Font");

        // If many text operators and fonts, likely text document
        return textOperatorCount > 50 && fontCount > 5;
    }

    private bool HasImagesAndText(string pdfContent)
    {
        var textOperators = CountOccurrences(pdfContent, "BT") + CountOccurrences(pdfContent, "Tj");
        var imageOperators = CountOccurrences(pdfContent, "BI") + CountOccurrences(pdfContent, "ID");

        return textOperators > 10 && imageOperators > 2;
    }

    private int CountOccurrences(string text, string pattern)
    {
        int count = 0;
        int index = 0;

        while ((index = text.IndexOf(pattern, index, StringComparison.OrdinalIgnoreCase)) != -1)
        {
            count++;
            index += pattern.Length;
        }

        return count;
    }

    private CompressionSettings GetOptimalSettings(string documentType, int imageQuality)
    {
        return documentType switch
        {
            "Scanned Document" => new CompressionSettings
            {
                PdfSetting = "/screen",
                ColorImageResolution = Math.Max(150, imageQuality * 2),
                GrayImageResolution = Math.Max(150, imageQuality * 2),
                JpegQuality = imageQuality,
                UseAutoFilter = false,
                UseDCTEncode = true,
                DownsampleColorImages = true,
                DownsampleGrayImages = true
            },
            "Text Document" => new CompressionSettings
            {
                PdfSetting = "/ebook",
                ColorImageResolution = 300,
                GrayImageResolution = 300,
                JpegQuality = Math.Max(80, imageQuality),
                UseAutoFilter = true,
                UseDCTEncode = false,
                DownsampleColorImages = false,
                DownsampleGrayImages = false
            },
            "Mixed Content" => new CompressionSettings
            {
                PdfSetting = "/printer",
                ColorImageResolution = 200,
                GrayImageResolution = 200,
                JpegQuality = imageQuality,
                UseAutoFilter = true,
                UseDCTEncode = true,
                DownsampleColorImages = true,
                DownsampleGrayImages = true
            },
            _ => new CompressionSettings
            {
                PdfSetting = "/default",
                ColorImageResolution = 300,
                GrayImageResolution = 300,
                JpegQuality = 75,
                UseAutoFilter = true,
                UseDCTEncode = false,
                DownsampleColorImages = false,
                DownsampleGrayImages = false
            }
        };
    }

    private class CompressionSettings
    {
        public string PdfSetting { get; set; } = "/default";
        public int ColorImageResolution { get; set; } = 300;
        public int GrayImageResolution { get; set; } = 300;
        public int JpegQuality { get; set; } = 75;
        public bool UseAutoFilter { get; set; } = true;
        public bool UseDCTEncode { get; set; } = false;
        public bool DownsampleColorImages { get; set; } = false;
        public bool DownsampleGrayImages { get; set; } = false;
    }


    private long GetFileSize(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
            return 0;
        return new System.IO.FileInfo(filePath).Length;
    }

    private string BuildIntelligentGhostscriptArguments(string inputPath, string outputPath, CompressionSettings settings, bool optimizeForScanned)
    {
        var arguments = $"-sDEVICE=pdfwrite -dCompatibilityLevel=1.4 -dPDFSETTINGS={settings.PdfSetting} -dNOPAUSE -dQUIET -dBATCH";

        // Add intelligent compression settings based on document analysis
        if (optimizeForScanned || settings.DownsampleColorImages)
        {
            arguments += $" -dColorImageResolution={settings.ColorImageResolution}";
        }

        if (optimizeForScanned || settings.DownsampleGrayImages)
        {
            arguments += $" -dGrayImageResolution={settings.GrayImageResolution}";
        }

        // Configure image filters
        if (settings.UseDCTEncode)
        {
            arguments += " -dAutoFilterColorImages=false -dColorImageFilter=/DCTEncode";
            arguments += " -dAutoFilterGrayImages=false -dGrayImageFilter=/DCTEncode";
        }

        // Set JPEG quality
        arguments += $" -dJPEGQ={settings.JpegQuality}";

        // Additional optimization parameters
        if (settings.PdfSetting == "/screen")
        {
            arguments += " -dSubsetFonts=true -dEmbedAllFonts=false";
        }

        arguments += $" -sOutputFile=\"{outputPath}\" \"{inputPath}\"";

        return arguments;
    }

    private async Task SplitPdfFile(string inputPath, long maxSplitSize)
    {
        try
        {
            // For simplicity, we'll create multiple output files by splitting pages
            // In a real implementation, you might want to use a PDF library like iTextSharp

            var directory = System.IO.Path.GetDirectoryName(inputPath) ?? "";
            var filenameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(inputPath) ?? "";
            var inputSize = GetFileSize(inputPath);

            LogMessage($"Splitting {FormatFileSize(inputSize)} file into chunks of {FormatFileSize(maxSplitSize)}...");

            // For demonstration, we'll create 3-4 split files
            // In reality, you'd need to analyze page count and split accordingly
            int numSplits = (int)Math.Ceiling((double)inputSize / maxSplitSize);

            for (int i = 0; i < numSplits; i++)
            {
                var splitOutputPath = System.IO.Path.Combine(directory, $"{filenameWithoutExt}.part{i + 1}.pdf");

                // This is a simplified approach - in reality you'd need to extract page ranges
                // For now, we'll just copy the file multiple times as placeholders
                LogMessage($"Creating part {i + 1} of {numSplits}: {System.IO.Path.GetFileName(splitOutputPath)}");

                // In a real implementation, you would use Ghostscript or PDF library to extract page ranges
                // Example: gs -sDEVICE=pdfwrite -dNOPAUSE -dBATCH -dFirstPage=1 -dLastPage=10 -sOutputFile=part1.pdf input.pdf

                await Task.Run(() =>
                {
                    var ghostscriptPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Ghostscript", "gswin64c.exe");
                    var startPage = i * 10 + 1; // Rough estimation: 10 pages per split
                    var endPage = (i + 1) * 10;

                    var arguments = $"-sDEVICE=pdfwrite -dNOPAUSE -dBATCH -dFirstPage={startPage} -dLastPage={endPage} -sOutputFile=\"{splitOutputPath}\" \"{inputPath}\"";

                    var process = new System.Diagnostics.Process();
                    process.StartInfo.FileName = ghostscriptPath;
                    process.StartInfo.Arguments = arguments;
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.UseShellExecute = false;
                    process.Start();
                    process.WaitForExit();
                });

                // Update progress for splitting
                UpdateProgress(90 + (i * 10 / numSplits));
            }

            LogMessage("File splitting completed!");

            this.Invoke(new Action(() =>
            {
                var result = MessageBox.Show(
                    $"File đã được chia thành {numSplits} phần.\n\nCác phần được lưu với tên:\n{filenameWithoutExt}.part1.pdf\n{filenameWithoutExt}.part2.pdf\n...\n\nBạn có muốn mở thư mục kết quả không?",
                    "Chia file hoàn tất",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (result == DialogResult.Yes)
                {
                    OpenOutputFolder(directory);
                }
            }));
        }
        catch (Exception ex)
        {
            LogMessage($"Error splitting file: {ex.Message}");
            this.Invoke(new Action(() =>
            {
                MessageBox.Show($"Error splitting file: {ex.Message}", "Split Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }));
        }
    }

    private void OpenOutputFolder(string filePath)
    {
        try
        {
            string? folderPath;

            if (System.IO.File.Exists(filePath))
            {
                // If it's a file, get its directory
                folderPath = System.IO.Path.GetDirectoryName(filePath);
            }
            else if (System.IO.Directory.Exists(filePath))
            {
                // If it's already a directory, use it directly
                folderPath = filePath;
            }
            else
            {
                LogMessage("Cannot open folder: path does not exist");
                return;
            }

            if (!string.IsNullOrEmpty(folderPath))
            {
                // Use Windows Explorer to open the folder and select the file
                var process = new System.Diagnostics.Process();
                process.StartInfo.FileName = "explorer.exe";
                process.StartInfo.Arguments = $"/select,\"{filePath}\"";
                process.StartInfo.UseShellExecute = true;
                process.Start();

                LogMessage($"Opened output folder: {folderPath}");
            }
        }
        catch (Exception ex)
        {
            LogMessage($"Error opening folder: {ex.Message}");
            MessageBox.Show($"Could not open output folder: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void Form1_FormClosing(object sender, FormClosingEventArgs e)
    {
        // Clean up thread if form is closing during compression
        if (isCompressionRunning && compressionThread != null && compressionThread.IsAlive)
        {
            // Thread.Abort() is obsolete, just let the thread complete naturally
            // We set a flag to signal the thread to stop if needed
            isCompressionRunning = false;
        }
    }

    private void MainForm_Load(object sender, EventArgs e)
    {
        var version = ProduceVersion();
        this.Text = $"PDF Compressor - Tối ưu hóa file PDF (v {version})";
    }
}
