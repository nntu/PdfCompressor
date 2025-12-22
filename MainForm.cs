using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using System.Linq;

namespace PdfCompressor;

public partial class MainForm : Form
{
    private string? selectedPdfPath;
    private System.Threading.Thread? compressionThread;
    private bool isCompressionRunning = false;

    // Merge functionality fields
    private List<string> mergePdfFiles = new List<string>();
    private System.Threading.Thread? mergeThread;
    private bool isMergeRunning = false;
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
        // Log to NLog (file, console, debug)
        Logger.InfoMainForm(message);

        // Also update UI if needed
        if (logTextBox.InvokeRequired)
        {
            logTextBox.Invoke(new Action<string>(LogMessage), message);
            return;
        }

        // Add to UI
        logTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\r\n");
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

            // Use GhostscriptAPI wrapper for compression
            LogMessage("Sử dụng Ghostscript API để nén file...");
            UpdateProgress(60);

            try
            {
                if (!GhostscriptAPI.IsGhostscriptAvailable())
                {
                    LogMessage("Ghostscript API không sẵn có, sử dụng phương pháp thay thế...");
                    throw new GhostscriptException("Ghostscript DLL not available", -1);
                }

                using (var gsApi = new GhostscriptAPI())
                {
                    // Create compression settings based on optimal settings
                    var compressionSettings = new PdfCompressionSettings
                    {
                        PdfSetting = optimalSettings.PdfSetting,
                        ColorImageResolution = optimalSettings.ColorImageResolution,
                        GrayImageResolution = optimalSettings.GrayImageResolution,
                        JpegQuality = optimalSettings.JpegQuality,
                        UseAutoFilter = optimalSettings.UseAutoFilter,
                        UseDCTEncode = optimalSettings.UseDCTEncode,
                        DownsampleColorImages = optimalSettings.DownsampleColorImages,
                        DownsampleGrayImages = optimalSettings.DownsampleGrayImages
                    };

                    gsApi.CompressPdf(inputPath, outputPath, compressionSettings);
                    UpdateProgress(90);

                    LogMessage("Nén file thành công với Ghostscript API!");
                }
            }
            catch (GhostscriptException gsEx)
            {
                string errorMsg = GhostscriptAPI.GetErrorMessage(gsEx.ErrorCode);
                LogMessage($"Lỗi Ghostscript API: {errorMsg}");
                LogMessage($"Chi tiết: {gsEx.Message}");

                // Fallback to process-based approach
                LogMessage("Thử phương pháp thay thế (process-based)...");

                // Build Ghostscript arguments with optimal compression settings
                var arguments = BuildIntelligentGhostscriptArguments(inputPath, outputPath, optimalSettings, optimizeForScanned);

                LogMessage("Bắt đầu nén với Ghostscript process...");
                UpdateProgress(70);

                var process = new System.Diagnostics.Process();
                process.StartInfo.FileName = ghostscriptPath;
                process.StartInfo.Arguments = arguments;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.UseShellExecute = false;
                process.Start();
                process.WaitForExit();

                UpdateProgress(90);

                LogMessage("Nén file thành công với phương pháp thay thế!");
            }

            var originalSize = GetFileSize(inputPath);
            var compressedSize = GetFileSize(outputPath);

            // If compression is "technically smaller" but negligible (e.g. 0.0% in logs),
            // offer a one-time aggressive retry for scanned PDFs.
            {
                bool isScanned = string.Equals(documentType, "Scanned Document", StringComparison.OrdinalIgnoreCase);
                if (isScanned && originalSize > 0)
                {
                    var savedBytes = originalSize - compressedSize;
                    var savedPct = ((double)savedBytes / originalSize) * 100.0;

                    // Threshold: less than 1% saved is usually perceived as "no compression"
                    if (savedPct < 1.0)
                    {
                        DialogResult retryResult = DialogResult.No;
                        this.Invoke(new Action(() =>
                        {
                            retryResult = MessageBox.Show(
                                $"Nén gần như không giảm dung lượng.\n\nGốc: {FormatFileSize(originalSize)}\nSau nén: {FormatFileSize(compressedSize)}\nGiảm: {savedPct:F1}%\n\nBạn có muốn thử lại chế độ nén scan mạnh hơn không?",
                                "Nén scan không hiệu quả",
                                MessageBoxButtons.YesNo,
                                MessageBoxIcon.Question);
                        }));

                        if (retryResult == DialogResult.Yes)
                        {
                            try
                            {
                                var retryTempPath = System.IO.Path.Combine(
                                    System.IO.Path.GetDirectoryName(outputPath) ?? "",
                                    $"{System.IO.Path.GetFileNameWithoutExtension(outputPath)}.retry.pdf");

                                var aggressive = new CompressionSettings
                                {
                                    PdfSetting = "/screen",
                                    ColorImageResolution = 120,
                                    GrayImageResolution = 120,
                                    JpegQuality = Math.Min(60, optimalSettings.JpegQuality),
                                    UseAutoFilter = false,
                                    UseDCTEncode = true,
                                    DownsampleColorImages = true,
                                    DownsampleGrayImages = true
                                };

                                LogMessage("Thử lại nén scan mạnh hơn theo yêu cầu người dùng...");

                                var retryArgs = BuildIntelligentGhostscriptArguments(inputPath, retryTempPath, aggressive, true);
                                var retryProcess = new System.Diagnostics.Process();
                                retryProcess.StartInfo.FileName = ghostscriptPath;
                                retryProcess.StartInfo.Arguments = retryArgs;
                                retryProcess.StartInfo.CreateNoWindow = true;
                                retryProcess.StartInfo.UseShellExecute = false;
                                retryProcess.Start();
                                retryProcess.WaitForExit();

                                var retrySize = GetFileSize(retryTempPath);
                                if (retrySize > 0 && retrySize < compressedSize)
                                {
                                    try
                                    {
                                        System.IO.File.Delete(outputPath);
                                        System.IO.File.Move(retryTempPath, outputPath);
                                        compressedSize = retrySize;
                                        LogMessage($"Thử lại thành công: kích thước sau nén giảm còn {FormatFileSize(compressedSize)}");
                                    }
                                    catch
                                    {
                                        try { System.IO.File.Delete(retryTempPath); } catch { }
                                    }
                                }
                                else
                                {
                                    try { System.IO.File.Delete(retryTempPath); } catch { }
                                    LogMessage("Thử lại không cải thiện dung lượng.");
                                }
                            }
                            catch (Exception ex)
                            {
                                LogMessage($"Retry scan compression failed: {ex.Message}");
                            }
                        }
                    }
                }
            }

            // Guard: sometimes scanned PDFs become larger after re-encoding.
            // If that happens, retry once with a more aggressive scanned profile, and if still not smaller,
            // warn the user and offer to delete the "compressed" output.
            if (compressedSize >= originalSize)
            {
                bool isScanned = string.Equals(documentType, "Scanned Document", StringComparison.OrdinalIgnoreCase);
                if (isScanned)
                {
                    try
                    {
                        var retryTempPath = System.IO.Path.Combine(
                            System.IO.Path.GetDirectoryName(outputPath) ?? "",
                            $"{System.IO.Path.GetFileNameWithoutExtension(outputPath)}.retry.pdf");

                        var aggressive = new CompressionSettings
                        {
                            PdfSetting = "/screen",
                            ColorImageResolution = 120,
                            GrayImageResolution = 120,
                            JpegQuality = Math.Min(60, optimalSettings.JpegQuality),
                            UseAutoFilter = false,
                            UseDCTEncode = true,
                            DownsampleColorImages = true,
                            DownsampleGrayImages = true
                        };

                        LogMessage($"Cảnh báo: file sau nén đang lớn hơn hoặc bằng file gốc ({FormatFileSize(compressedSize)} >= {FormatFileSize(originalSize)}). Thử lại cấu hình scan mạnh hơn...");

                        var retryArgs = BuildIntelligentGhostscriptArguments(inputPath, retryTempPath, aggressive, true);
                        var retryProcess = new System.Diagnostics.Process();
                        retryProcess.StartInfo.FileName = ghostscriptPath;
                        retryProcess.StartInfo.Arguments = retryArgs;
                        retryProcess.StartInfo.CreateNoWindow = true;
                        retryProcess.StartInfo.UseShellExecute = false;
                        retryProcess.Start();
                        retryProcess.WaitForExit();

                        var retrySize = GetFileSize(retryTempPath);
                        if (retrySize > 0 && retrySize < compressedSize && retrySize < originalSize)
                        {
                            try
                            {
                                // Replace output with better retry result
                                System.IO.File.Delete(outputPath);
                                System.IO.File.Move(retryTempPath, outputPath);
                                compressedSize = retrySize;
                                LogMessage($"Thử lại thành công: kích thước sau nén giảm còn {FormatFileSize(compressedSize)}");
                            }
                            catch
                            {
                                // If replace fails, keep original output and clean up retry file
                                try { System.IO.File.Delete(retryTempPath); } catch { }
                            }
                        }
                        else
                        {
                            try { System.IO.File.Delete(retryTempPath); } catch { }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"Retry scan compression failed: {ex.Message}");
                    }
                }

                if (compressedSize >= originalSize)
                {
                    LogMessage($"Cảnh báo: file sau nén không nhỏ hơn file gốc ({FormatFileSize(compressedSize)} >= {FormatFileSize(originalSize)}).");

                    DialogResult keepResult = DialogResult.Yes;
                    this.Invoke(new Action(() =>
                    {
                        keepResult = MessageBox.Show(
                            $"Kết quả nén không hiệu quả (file sau nén không nhỏ hơn file gốc).\n\nGốc: {FormatFileSize(originalSize)}\nSau nén: {FormatFileSize(compressedSize)}\n\nBạn có muốn GIỮ file nén không?\n- Yes: Giữ file nén\n- No: Xóa file nén (giữ file gốc)",
                            "Nén không giảm dung lượng",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Warning);
                    }));

                    if (keepResult == DialogResult.No)
                    {
                        try
                        {
                            System.IO.File.Delete(outputPath);
                            compressedSize = originalSize;
                            LogMessage("Đã xóa file nén vì dung lượng không giảm.");
                        }
                        catch (Exception ex)
                        {
                            LogMessage($"Không thể xóa file nén: {ex.Message}");
                        }
                    }
                }
            }

            // After compression: if file is still large, suggest splitting (instead of auto-splitting)
            bool didSplit = false;
            const long MB = 1024L * 1024L;

            // "Cao" threshold:
            // - If user enabled splitting, use their limit as the threshold
            // - Otherwise use a sensible default threshold (20MB)
            int suggestThresholdMB = enableSplitting ? maxSplitSizeMB : 20;

            // Split part size:
            // - If user enabled splitting, split into that size
            // - Otherwise default to 5MB parts
            int splitPartSizeMB = enableSplitting ? maxSplitSizeMB : 5;

            if (compressedSize > suggestThresholdMB * MB)
            {
                LogMessage($"File sau nén vẫn lớn ({FormatFileSize(compressedSize)}). Gợi ý chia file thành các phần ~{splitPartSizeMB}MB.");

                DialogResult splitResult = DialogResult.No;
                this.Invoke(new Action(() =>
                {
                    splitResult = MessageBox.Show(
                        $"File sau nén vẫn còn lớn.\n\nKích thước: {FormatFileSize(compressedSize)}\nNgưỡng gợi ý: {suggestThresholdMB}MB\n\nBạn có muốn chia file thành các phần khoảng {splitPartSizeMB}MB không?",
                        "Gợi ý chia file",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);
                }));

                if (splitResult == DialogResult.Yes)
                {
                    LogMessage($"Người dùng chọn chia file. Đang chia nhỏ (tối đa ~{splitPartSizeMB}MB/phần)...");
                    await SplitPdfFile(outputPath, splitPartSizeMB * MB);
                    didSplit = true;
                }
            }

            var compressionRatio = (1 - (double)compressedSize / originalSize) * 100;

            LogMessage($"Nén thành công!");
            LogMessage($"File kết quả: {outputPath}");
            LogMessage($"Kích thước gốc: {FormatFileSize(originalSize)}");
            LogMessage($"Kích thước sau nén: {FormatFileSize(compressedSize)}");
            LogMessage($"Tỷ lệ nén: {compressionRatio:F1}%");

            UpdateProgress(100);

            // Update UI on main thread
            this.Invoke(new Action(() =>
            {
                statusLabel.Text = "Nén hoàn tất!";

                // If we already ran splitting, SplitPdfFile() has its own completion dialog.
                // Avoid showing multiple popups.
                if (didSplit)
                {
                    return;
                }

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
        LogMessage($"Ghostscript arguments: {arguments}");
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
            // IMPORTANT: Ghostscript only downsamples if the corresponding Downsample flag is enabled.
            arguments += " -dDownsampleColorImages=true -dColorImageDownsampleType=/Average";
            arguments += $" -dColorImageResolution={settings.ColorImageResolution}";
        }

        if (optimizeForScanned || settings.DownsampleGrayImages)
        {
            arguments += " -dDownsampleGrayImages=true -dGrayImageDownsampleType=/Average";
            arguments += $" -dGrayImageResolution={settings.GrayImageResolution}";
        }

        // Scanned PDFs often contain 1-bit (mono) images; ensure we don't accidentally bloat them.
        // (Keep mono at a sane default; user doesn't control this in UI yet.)
        if (optimizeForScanned)
        {
            arguments += " -dDownsampleMonoImages=true -dMonoImageDownsampleType=/Subsample -dMonoImageResolution=300";
            // Keep lossless-ish mono compression (commonly supported).
            arguments += " -dMonoImageFilter=/CCITTFaxEncode";
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

                // Use GhostscriptAPI to split file by page ranges
                await Task.Run(() =>
                {
                    try
                    {
                        using (var gsApi = new GhostscriptAPI())
                        {
                            var startPage = i * 10 + 1; // Rough estimation: 10 pages per split
                            var endPage = (i + 1) * 10;

                            gsApi.SplitPdfByPageRange(inputPath, splitOutputPath, startPage, endPage);
                            LogMessage($"Đã tạo phần {i + 1}: {System.IO.Path.GetFileName(splitOutputPath)}");
                        }
                    }
                    catch (GhostscriptException gsEx)
                    {
                        LogMessage($"Lỗi Ghostscript API khi chia file: {gsEx.Message} (Error Code: {gsEx.ErrorCode})");

                        // Fallback to process-based approach
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

                        LogMessage($"Đã tạo phần {i + 1} với phương pháp thay thế: {System.IO.Path.GetFileName(splitOutputPath)}");
                    }
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

    private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
    {
        // Clean up threads if form is closing during operations
        if (isCompressionRunning && compressionThread != null && compressionThread.IsAlive)
        {
            // Thread.Abort() is obsolete, just let the thread complete naturally
            // We set a flag to signal the thread to stop if needed
            isCompressionRunning = false;
        }

        if (isMergeRunning && mergeThread != null && mergeThread.IsAlive)
        {
            // Set flag to signal the merge thread to stop
            isMergeRunning = false;
        }
    }

    private void MainForm_Load(object sender, EventArgs e)
    {
        var version = ProduceVersion();
        this.Text = $"PDF Compressor - Tối ưu hóa file PDF (v {version})";
        LogMessage($"Ứng dụng khởi động. Phiên bản: {version}");
        // Check Ghostscript availability on startup
        try
        {
            if (GhostscriptAPI.IsGhostscriptAvailable())
            {
                LogMessage("Ghostscript API: Sẵn sàng");
                LogMessage($"Đường dẫn DLL: {System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Ghostscript", "gsdll64.dll")}");
            }
            else
            {
                LogMessage("Cảnh báo: Ghostscript API không sẵn có, sẽ sử dụng phương pháp thay thế");
                LogMessage("Vui lòng kiểm tra file gsdll64.dll trong thư mục Ghostscript");
            }
        }
        catch (Exception ex)
        {
            LogMessage($"Lỗi khi kiểm tra Ghostscript: {ex.Message}");
        }
    }

    #region PDF Merge Functionality

    private void LogMergeMessage(string message)
    {
        // Log to NLog with [MERGE] prefix
        Logger.InfoMainForm($"[MERGE] {message}");

        // Also update UI if needed
        if (mergeLogTextBox.InvokeRequired)
        {
            mergeLogTextBox.Invoke(new Action<string>(LogMergeMessage), message);
            return;
        }

        // Add to UI
        mergeLogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\r\n");
    }

    private void UpdateMergeProgress(int value)
    {
        if (mergeProgressBar.InvokeRequired)
        {
            mergeProgressBar.Invoke(new Action<int>(UpdateMergeProgress), value);
            return;
        }
        mergeProgressBar.Value = value;
    }

    private void UpdateMergeStatus(string status)
    {
        if (mergeStatusLabel.InvokeRequired)
        {
            mergeStatusLabel.Invoke(new Action<string>(UpdateMergeStatus), status);
            return;
        }
        mergeStatusLabel.Text = status;
    }

    private void addPdfButton_Click(object sender, EventArgs e)
    {
        if (openPdfFileDialog.ShowDialog() == DialogResult.OK)
        {
            foreach (string fileName in openPdfFileDialog.FileNames)
            {
                if (!mergePdfFiles.Contains(fileName))
                {
                    mergePdfFiles.Add(fileName);
                    mergePdfListBox.Items.Add(System.IO.Path.GetFileName(fileName));
                    LogMergeMessage($"Đã thêm file: {System.IO.Path.GetFileName(fileName)} ({FormatFileSize(GetFileSize(fileName))})");
                }
                else
                {
                    LogMergeMessage($"File đã tồn tại: {System.IO.Path.GetFileName(fileName)}");
                }
            }

            UpdateMergeButtonState();
            LogMergeMessage($"Tổng số file: {mergePdfFiles.Count}");
        }
    }

    private void removePdfButton_Click(object sender, EventArgs e)
    {
        var selectedIndices = mergePdfListBox.SelectedIndices.Cast<int>().OrderByDescending(i => i).ToList();

        foreach (int index in selectedIndices)
        {
            if (index >= 0 && index < mergePdfFiles.Count && index < mergePdfListBox.Items.Count)
            {
                try
                {
                    string fileName = mergePdfFiles[index];
                    mergePdfFiles.RemoveAt(index);
                    mergePdfListBox.Items.RemoveAt(index);
                    LogMergeMessage($"Đã xóa file: {System.IO.Path.GetFileName(fileName)}");
                }
                catch (Exception ex)
                {
                    LogMergeMessage($"Lỗi khi xóa file tại vị trí {index}: {ex.Message}");
                }
            }
        }

        UpdateMergeButtonState();
        LogMergeMessage($"Tổng số file: {mergePdfFiles.Count}");
    }

    private void moveUpButton_Click(object sender, EventArgs e)
    {
        int selectedIndex = mergePdfListBox.SelectedIndex;
        if (selectedIndex > 0 && selectedIndex < mergePdfFiles.Count && selectedIndex - 1 >= 0)
        {
            try
            {
                // Swap in the list
                string temp = mergePdfFiles[selectedIndex];
                mergePdfFiles[selectedIndex] = mergePdfFiles[selectedIndex - 1];
                mergePdfFiles[selectedIndex - 1] = temp;

                // Swap in the ListBox
                if (selectedIndex < mergePdfListBox.Items.Count && selectedIndex - 1 >= 0)
                {
                    object tempItem = mergePdfListBox.Items[selectedIndex];
                    mergePdfListBox.Items[selectedIndex] = mergePdfListBox.Items[selectedIndex - 1];
                    mergePdfListBox.Items[selectedIndex - 1] = tempItem;

                    mergePdfListBox.SelectedIndex = selectedIndex - 1;
                    LogMergeMessage($"Đã di chuyển file lên trên: {System.IO.Path.GetFileName(mergePdfFiles[selectedIndex])}");
                }
            }
            catch (Exception ex)
            {
                LogMergeMessage($"Lỗi khi di chuyển file lên: {ex.Message}");
            }
        }
    }

    private void moveDownButton_Click(object sender, EventArgs e)
    {
        int selectedIndex = mergePdfListBox.SelectedIndex;
        if (selectedIndex >= 0 && selectedIndex < mergePdfFiles.Count - 1 && selectedIndex + 1 < mergePdfFiles.Count)
        {
            try
            {
                // Swap in the list
                string temp = mergePdfFiles[selectedIndex];
                mergePdfFiles[selectedIndex] = mergePdfFiles[selectedIndex + 1];
                mergePdfFiles[selectedIndex + 1] = temp;

                // Swap in the ListBox
                if (selectedIndex < mergePdfListBox.Items.Count && selectedIndex + 1 < mergePdfListBox.Items.Count)
                {
                    object tempItem = mergePdfListBox.Items[selectedIndex];
                    mergePdfListBox.Items[selectedIndex] = mergePdfListBox.Items[selectedIndex + 1];
                    mergePdfListBox.Items[selectedIndex + 1] = tempItem;

                    mergePdfListBox.SelectedIndex = selectedIndex + 1;
                    LogMergeMessage($"Đã di chuyển file xuống dưới: {System.IO.Path.GetFileName(mergePdfFiles[selectedIndex])}");
                }
            }
            catch (Exception ex)
            {
                LogMergeMessage($"Lỗi khi di chuyển file xuống: {ex.Message}");
            }
        }
    }

    private void UpdateMergeButtonState()
    {
        mergeButton.Enabled = mergePdfFiles.Count >= 2 && !isMergeRunning;
        moveUpButton.Enabled = mergePdfListBox.SelectedIndex > 0 &&
                               mergePdfListBox.SelectedIndex < mergePdfFiles.Count &&
                               !isMergeRunning;
        moveDownButton.Enabled = mergePdfListBox.SelectedIndex >= 0 &&
                                mergePdfListBox.SelectedIndex < mergePdfFiles.Count - 1 &&
                                !isMergeRunning;
        removePdfButton.Enabled = mergePdfListBox.SelectedIndices.Count > 0 && !isMergeRunning;
        addPdfButton.Enabled = !isMergeRunning;
    }

    private void mergeButton_Click(object sender, EventArgs e)
    {
        if (isMergeRunning)
        {
            MessageBox.Show("Merge operation is already in progress. Please wait.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (mergePdfFiles.Count < 2)
        {
            MessageBox.Show("Please add at least 2 PDF files to merge.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        saveMergeFileDialog.FileName = "merged.pdf";
        if (saveMergeFileDialog.ShowDialog() == DialogResult.OK)
        {
            StartMergeThread(saveMergeFileDialog.FileName);
        }
    }

    private void StartMergeThread(string outputPath)
    {
        isMergeRunning = true;
        UpdateMergeButtonState();
        mergeProgressBar.Visible = true;
        mergeProgressBar.Value = 0;

        // Create a copy of the file list to avoid modification during merge
        var filesToMerge = new List<string>(mergePdfFiles);

        mergeThread = new System.Threading.Thread(() => MergePdfThreaded(filesToMerge, outputPath));
        mergeThread.IsBackground = true;
        mergeThread.Start();

        LogMergeMessage("Bắt đầu gộp file PDF...");
        UpdateMergeStatus("Đang gộp...");
    }

    private async void MergePdfThreaded(List<string> inputPaths, string outputPath)
    {
        try
        {
            if (inputPaths.Count < 2)
            {
                throw new ArgumentException("At least 2 PDF files are required for merging");
            }

            LogMergeMessage($"Số lượng file cần gộp: {inputPaths.Count}");
            UpdateMergeProgress(10);

            // Validate all input files exist
            foreach (string filePath in inputPaths)
            {
                if (!System.IO.File.Exists(filePath))
                {
                    throw new FileNotFoundException($"Input PDF file not found: {System.IO.Path.GetFileName(filePath)}", filePath);
                }
                LogMergeMessage($"File: {System.IO.Path.GetFileName(filePath)} ({FormatFileSize(GetFileSize(filePath))})");
            }

            UpdateMergeProgress(20);

            // Use GhostscriptAPI to merge PDFs
            using (var gsApi = new GhostscriptAPI())
            {
                LogMergeMessage("Sử dụng Ghostscript API để gộp file...");
                UpdateMergeProgress(30);

                try
                {
                    if (!GhostscriptAPI.IsGhostscriptAvailable())
                    {
                        LogMergeMessage("Ghostscript API không sẵn có, sử dụng phương pháp thay thế...");
                        throw new GhostscriptException("Ghostscript DLL not available", -1);
                    }

                    gsApi.MergePdfFiles(inputPaths.ToArray(), outputPath);
                    UpdateMergeProgress(80);

                    LogMergeMessage("Gộp file thành công với Ghostscript API!");

                    // Verify output file was created
                    if (!System.IO.File.Exists(outputPath))
                    {
                        throw new InvalidOperationException("Output file was not created");
                    }

                    var outputSize = GetFileSize(outputPath);
                    var totalInputSize = inputPaths.Sum(GetFileSize);

                    LogMergeMessage($"File kết quả: {System.IO.Path.GetFileName(outputPath)}");
                    LogMergeMessage($"Kích thước tổng các file gốc: {FormatFileSize(totalInputSize)}");
                    LogMergeMessage($"Kích thước file sau khi gộp: {FormatFileSize(outputSize)}");

                    UpdateMergeProgress(100);

                    // Update UI on main thread
                    this.Invoke(new Action(() =>
                    {
                        UpdateMergeStatus("Gộp hoàn tất!");

                        var result = MessageBox.Show(
                            $"Gộp file hoàn tất!\n\nSố lượng file đã gộp: {inputPaths.Count}\nKích thước tổng: {FormatFileSize(totalInputSize)}\nKích thước file kết quả: {FormatFileSize(outputSize)}\n\nBạn có muốn mở thư mục kết quả không?",
                            "Thành công",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Information);

                        if (result == DialogResult.Yes)
                        {
                            OpenOutputFolder(outputPath);
                        }
                    }));
                }
                catch (GhostscriptException gsEx)
                {
                    string errorMsg = GhostscriptAPI.GetErrorMessage(gsEx.ErrorCode);
                    LogMergeMessage($"Lỗi Ghostscript API: {errorMsg}");
                    LogMergeMessage($"Chi tiết: {gsEx.Message}");

                    // Fallback to process-based approach
                    LogMergeMessage("Thử phương pháp thay thế...");
                    await MergePdfWithProcess(inputPaths, outputPath);
                }
            }
        }
        catch (Exception ex)
        {
            LogMergeMessage($"Lỗi khi gộp file: {ex.Message}");
            this.Invoke(new Action(() =>
            {
                UpdateMergeStatus("Gộp thất bại!");
                MessageBox.Show($"Merge failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }));
        }
        finally
        {
            this.Invoke(new Action(() =>
            {
                isMergeRunning = false;
                UpdateMergeButtonState();
                mergeProgressBar.Visible = false;
            }));
        }
    }

    private async Task MergePdfWithProcess(List<string> inputPaths, string outputPath)
    {
        try
        {
            LogMergeMessage("Sử dụng phương pháp Ghostscript process...");
            UpdateMergeProgress(40);

            var ghostscriptPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Ghostscript", "gswin64c.exe");

            // Build Ghostscript arguments for merging
            var args = new List<string>
            {
                "-sDEVICE=pdfwrite",
                "-dNOPAUSE",
                "-dBATCH",
                "-dQUIET",
                $"-sOutputFile=\"{outputPath}\""
            };

            args.AddRange(inputPaths.Select(path => $"\"{path}\""));

            var arguments = string.Join(" ", args);
            LogMergeMessage($"Thực thi: {arguments}");

            UpdateMergeProgress(60);

            var process = new System.Diagnostics.Process();
            process.StartInfo.FileName = ghostscriptPath;
            process.StartInfo.Arguments = arguments;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;
            process.Start();
            await Task.Run(() => process.WaitForExit());

            UpdateMergeProgress(90);

            if (process.ExitCode == 0 && System.IO.File.Exists(outputPath))
            {
                LogMergeMessage("Gộp file thành công với phương pháp thay thế!");

                var outputSize = GetFileSize(outputPath);
                var totalInputSize = inputPaths.Sum(GetFileSize);

                LogMergeMessage($"File kết quả: {System.IO.Path.GetFileName(outputPath)}");
                LogMergeMessage($"Kích thước file sau khi gộp: {FormatFileSize(outputSize)}");

                UpdateMergeProgress(100);

                this.Invoke(new Action(() =>
                {
                    UpdateMergeStatus("Gộp hoànất!");

                    var result = MessageBox.Show(
                        $"Gộp file hoàn tất!\n\nSố lượng file đã gộp: {inputPaths.Count}\nKích thước file kết quả: {FormatFileSize(outputSize)}\n\nBạn có muốn mở thư mục kết quả không?",
                        "Thành công",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Information);

                    if (result == DialogResult.Yes)
                    {
                        OpenOutputFolder(outputPath);
                    }
                }));
            }
            else
            {
                throw new InvalidOperationException($"Ghostscript process failed with exit code: {process.ExitCode}");
            }
        }
        catch (Exception ex)
        {
            LogMergeMessage($"Phương pháp thay thế cũng thất bại: {ex.Message}");
            throw;
        }
    }

    private void mergePdfListBox_SelectedIndexChanged(object sender, EventArgs e)
    {
        try
        {
            UpdateMergeButtonState();
        }
        catch (Exception ex)
        {
            LogMergeMessage($"Lỗi khi cập nhật trạng thái nút: {ex.Message}");
        }
    }

    #endregion
}
