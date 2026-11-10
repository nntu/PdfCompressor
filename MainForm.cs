using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using System.Linq;

namespace PdfCompressor;

public partial class MainForm : Form
{
    private string? selectedPdfPath;
    private Task? compressionTask;
    private CancellationTokenSource? compressionCancellation;
    private volatile bool isCompressionRunning = false;
    private volatile bool isClosing = false;
    private bool? cachedHasColor = null;
    private CancellationTokenSource? fileAnalysisCancellation;

    // External PDF optimization tools (optional; user can provide them)
    private const string CompressionTypeLossless = "Lossless tối ưu (mutool + qpdf)";
    private const string CompressionTypeHybrid = "Hybrid (mutool + Ghostscript + qpdf)";
    private const string CompressionTypeAuto = "Tự động (Tốt nhất)";
    private const string CompressionTypeScanSuper = "Scan - Siêu nén (JBIG2 / Đen trắng)";
    private const string CompressionTypeScanBalanced = "Scan - Cân bằng (Dấu đỏ & chữ ký)";
    private const string CompressionTypeScanHighQuality = "Scan - Chất lượng cao (200 DPI)";

    private const string ScanColorAuto = "Tự động (Auto)";
    private const string ScanColorPreserve = "Giữ màu (Con dấu, chữ ký)";
    private const string ScanColorGray = "Ảnh xám (Grayscale - Tiết kiệm)";
    private const string ScanColorMono = "Đen trắng (Monochrome - Siêu nén)";

    // Merge functionality fields
    private List<string> mergePdfFiles = new List<string>();
    private Task? mergeTask;
    private CancellationTokenSource? mergeCancellation;
    private volatile bool isMergeRunning = false;
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
        comboBoxScanColorMode.SelectedIndex = 0;
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

    private void checkBoxOptimizeForScanned_CheckedChanged(object sender, EventArgs e)
    {
        bool enabled = checkBoxOptimizeForScanned.Checked;
        labelScanColorMode.Enabled = enabled;
        comboBoxScanColorMode.Enabled = enabled;
    }

    private void comboBoxCompressionType_SelectedIndexChanged(object sender, EventArgs e)
    {
        var selected = comboBoxCompressionType.SelectedItem?.ToString();
        if (selected == CompressionTypeScanSuper)
        {
            checkBoxOptimizeForScanned.Checked = true;
            trackBarImageQuality.Value = 60;
            comboBoxScanColorMode.SelectedIndex = 3; // Monochrome
        }
        else if (selected == CompressionTypeScanBalanced)
        {
            checkBoxOptimizeForScanned.Checked = true;
            trackBarImageQuality.Value = 75;
            comboBoxScanColorMode.SelectedIndex = 0; // Auto
        }
        else if (selected == CompressionTypeScanHighQuality)
        {
            checkBoxOptimizeForScanned.Checked = true;
            trackBarImageQuality.Value = 85;
            comboBoxScanColorMode.SelectedIndex = 1; // Preserve Color
        }
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

        InvokeOnUi(() => logTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\r\n"));
    }

    private bool InvokeOnUi(Action action)
    {
        if (isClosing || IsDisposed || !IsHandleCreated)
        {
            return false;
        }

        try
        {
            if (InvokeRequired)
            {
                BeginInvoke(action);
            }
            else
            {
                action();
            }
            return true;
        }
        catch (InvalidOperationException) when (isClosing || IsDisposed || Disposing)
        {
            return false;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    private static bool PathsEqual(string firstPath, string secondPath)
    {
        return string.Equals(
            System.IO.Path.GetFullPath(firstPath),
            System.IO.Path.GetFullPath(secondPath),
            StringComparison.OrdinalIgnoreCase);
    }

    private void selectFileButton_Click(object sender, EventArgs e)
    {
        if (openFileDialog.ShowDialog() == DialogResult.OK)
        {
            selectedPdfPath = openFileDialog.FileName;
            cachedHasColor = null;
            var currentPath = selectedPdfPath;

            statusLabel.Text = $"Đã chọn file: {System.IO.Path.GetFileName(currentPath)}";
            compressFileButton.Enabled = true;

            // Auto-generate output filename with .compress.pdf suffix
            var directory = System.IO.Path.GetDirectoryName(currentPath) ?? "";
            var filenameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(currentPath) ?? "";
            var outputPath = System.IO.Path.Combine(directory, $"{filenameWithoutExt}.compress.pdf");
            saveFileDialog.FileName = outputPath;

            long fileSize = GetFileSize(currentPath);
            LogMessage($"Đã tải file: {System.IO.Path.GetFileName(currentPath)}");
            LogMessage($"Kích thước file: {FormatFileSize(fileSize)}");

            textBoxDocumentType.Text = "Đang phân tích...";

            // Cancel any previous file analysis task
            fileAnalysisCancellation?.Cancel();
            fileAnalysisCancellation?.Dispose();
            fileAnalysisCancellation = new CancellationTokenSource();
            var ct = fileAnalysisCancellation.Token;

            // Run document analysis asynchronously in background so UI never freezes!
            Task.Run(() =>
            {
                try
                {
                    ct.ThrowIfCancellationRequested();
                    var documentType = AnalyzeDocumentType(currentPath);
                    bool hasColor = true;
                    if (string.Equals(documentType, "Scanned Document", StringComparison.OrdinalIgnoreCase))
                    {
                        hasColor = DetectDocumentHasColor(currentPath);
                        cachedHasColor = hasColor;
                    }

                    if (!ct.IsCancellationRequested)
                    {
                        InvokeOnUi(() =>
                        {
                            if (PathsEqual(selectedPdfPath ?? "", currentPath))
                            {
                                textBoxDocumentType.Text = documentType;
                                LogMessage($"Loại tài liệu phát hiện: {documentType}");
                                if (string.Equals(documentType, "Scanned Document", StringComparison.OrdinalIgnoreCase))
                                {
                                    LogMessage(hasColor
                                        ? "Kiểm tra màu scan: Có màu sắc (phát hiện con dấu / chữ ký)."
                                        : "Kiểm tra màu scan: Đen trắng / đơn sắc (không có màu).");
                                }
                            }
                        });
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    Logger.ErrorMainForm($"Lỗi phân tích tài liệu: {ex.Message}");
                }
            }, ct);
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
            var inputPath = selectedPdfPath ?? "";
            if (!string.IsNullOrWhiteSpace(inputPath) && PathsEqual(inputPath, saveFileDialog.FileName))
            {
                MessageBox.Show("File kết quả phải khác file PDF gốc.", "Đường dẫn không hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            StartCompressionThread(inputPath, saveFileDialog.FileName);
        }
    }

    private void StartCompressionThread(string inputPath, string outputPath)
    {
        // Read all UI values before starting the thread to avoid cross-thread operations
        var compressionSettings = new CompressionThreadSettings
        {
            CompressionType = comboBoxCompressionType.SelectedItem?.ToString() ?? "Tự động (Tốt nhất)",
            ScanColorMode = comboBoxScanColorMode.SelectedItem?.ToString() ?? ScanColorAuto,
            ImageQuality = trackBarImageQuality.Value,
            OptimizeForScanned = checkBoxOptimizeForScanned.Checked,
            EnableSplitting = checkBoxEnableSplitting.Checked,
            MaxSplitSizeMB = 5
        };

        if (compressionSettings.EnableSplitting)
        {
            if (!int.TryParse(textBoxSplitSize.Text, out int splitSize) || splitSize <= 0)
            {
                MessageBox.Show("Kích thước mỗi phần phải là số nguyên lớn hơn 0 MB.", "Kích thước không hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            compressionSettings.MaxSplitSizeMB = splitSize;
        }

        // Get document type from analysis
        compressionSettings.DocumentType = textBoxDocumentType.Text;

        isCompressionRunning = true;
        selectFileButton.Enabled = false;
        compressFileButton.Enabled = false;
        comboBoxCompressionType.Enabled = false;
        comboBoxScanColorMode.Enabled = false;
        trackBarImageQuality.Enabled = false;
        progressBar.Visible = true;
        progressBar.Value = 0;

        compressionCancellation?.Dispose();
        compressionCancellation = new CancellationTokenSource();
        var cancellationToken = compressionCancellation.Token;
        compressionTask = Task.Run(() => CompressPdfWorker(inputPath, outputPath, compressionSettings, cancellationToken));

        LogMessage("Bắt đầu quá trình nén...");
        statusLabel.Text = "Đang nén...";
    }

    private class CompressionThreadSettings
    {
        public string CompressionType { get; set; } = "";
        public string ScanColorMode { get; set; } = "";
        public int ImageQuality { get; set; } = 75;
        public bool OptimizeForScanned { get; set; } = false;
        public bool EnableSplitting { get; set; } = false;
        public int MaxSplitSizeMB { get; set; } = 5;
        public string DocumentType { get; set; } = "";
    }

    private void CompressPdfWorker(string inputPath, string outputPath, CompressionThreadSettings settings, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(inputPath) || !System.IO.File.Exists(inputPath))
            {
                throw new FileNotFoundException("Input PDF file not found", inputPath);
            }
            if (PathsEqual(inputPath, outputPath))
            {
                throw new InvalidOperationException("File kết quả phải khác file PDF gốc.");
            }

            var ghostscriptPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Ghostscript", "gswin64c.exe");

            // Use settings from parameter instead of accessing UI controls
            var compressionType = settings.CompressionType;
            var scanColorMode = settings.ScanColorMode;
            var imageQuality = settings.ImageQuality;
            var optimizeForScanned = settings.OptimizeForScanned;
            var enableSplitting = settings.EnableSplitting;
            var maxSplitSizeMB = settings.MaxSplitSizeMB;
            var documentType = settings.DocumentType;
            if (string.Equals(documentType, "Đang phân tích...", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(documentType, "Chưa phân tích", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(documentType))
            {
                documentType = AnalyzeDocumentType(inputPath);
                InvokeOnUi(() => textBoxDocumentType.Text = documentType);
            }

            bool isScanPreset = compressionType == CompressionTypeScanSuper ||
                                compressionType == CompressionTypeScanBalanced ||
                                compressionType == CompressionTypeScanHighQuality;

            bool isScannedDoc = string.Equals(documentType, "Scanned Document", StringComparison.OrdinalIgnoreCase) || optimizeForScanned || isScanPreset;

            LogMessage($"Loại tài liệu: {documentType}");
            LogMessage($"Loại nén: {compressionType}");
            if (isScannedDoc)
            {
                LogMessage($"Chế độ màu scan: {scanColorMode}");
            }
            LogMessage($"Chất lượng ảnh: {imageQuality}%");
            LogMessage($"Tối ưu cho scan: {optimizeForScanned}");
            if (enableSplitting)
            {
                LogMessage($"Bật chia file (tối đa: {maxSplitSizeMB}MB)");
            }

            UpdateProgress(10);

            string bestSetting;
            CompressionSettings optimalSettings = new CompressionSettings();

            bool toolsAvailable = AreExternalToolsAvailable(out string mutoolPath, out string qpdfPath);
            bool isLayered = IsPdfLayeredQuick(inputPath);

            bool hasColor = true;
            if (isScannedDoc)
            {
                hasColor = DetectDocumentHasColor(inputPath);
                LogMessage(hasColor
                    ? "Kiểm tra màu scan: Có màu sắc (con dấu / chữ ký)."
                    : "Kiểm tra màu scan: Đen trắng / đơn sắc.");
            }

            if (isScanPreset)
            {
                optimalSettings = GetOptimalSettings(documentType, imageQuality, compressionType, scanColorMode, hasColor);
                bestSetting = compressionType;
                LogMessage($"Hồ sơ nén scan: {compressionType} (DPI: {optimalSettings.ColorImageResolution}, Bicubic, JPEG-Q: {optimalSettings.JpegQuality}%, Chuyển xám: {optimalSettings.ConvertToGray})");

                if (toolsAvailable)
                {
                    RunScanOptimizedPipeline(mutoolPath, qpdfPath, ghostscriptPath, inputPath, outputPath, optimalSettings, cancellationToken);
                }
                else
                {
                    RunGhostscriptOnly(ghostscriptPath, inputPath, outputPath, optimalSettings, true);
                }

                goto AfterCompression;
            }

            // Auto propose: choose Lossless/Hybrid/Ghostscript-only based on document type, layer detection and tool availability
            if (compressionType == CompressionTypeAuto)
            {
                string proposed;
                if (isLayered)
                {
                    proposed = toolsAvailable ? CompressionTypeLossless : "Ghostscript (không khuyến nghị cho PDF có layer)";
                }
                else if (isScannedDoc)
                {
                    // For scanned documents in Auto mode: choose Scan Super if monochrome or user chose Mono; otherwise Scan Balanced
                    proposed = (!hasColor || scanColorMode == ScanColorMono) ? CompressionTypeScanSuper : CompressionTypeScanBalanced;
                }
                else if (!toolsAvailable)
                {
                    proposed = "Ghostscript (thiếu mutool/qpdf)";
                }
                else if (string.Equals(documentType, "Text Document", StringComparison.OrdinalIgnoreCase))
                {
                    proposed = CompressionTypeLossless;
                }
                else
                {
                    proposed = (imageQuality >= 90) ? CompressionTypeLossless : CompressionTypeHybrid;
                }

                LogMessage($"Đề xuất tự động: {proposed}" +
                           (isLayered ? " (phát hiện PDF có layer)" : "") +
                           (!toolsAvailable ? " (thiếu Tools/mutool.exe hoặc Tools/qpdf.exe)" : ""));

                if (proposed == CompressionTypeScanSuper || proposed == CompressionTypeScanBalanced)
                {
                    optimalSettings = GetOptimalSettings(documentType, imageQuality, proposed, scanColorMode, hasColor);
                    bestSetting = proposed;
                    if (toolsAvailable)
                    {
                        RunScanOptimizedPipeline(mutoolPath, qpdfPath, ghostscriptPath, inputPath, outputPath, optimalSettings, cancellationToken);
                    }
                    else
                    {
                        RunGhostscriptOnly(ghostscriptPath, inputPath, outputPath, optimalSettings, true);
                    }
                    goto AfterCompression;
                }

                if (proposed == CompressionTypeLossless && toolsAvailable)
                {
                    RunLosslessPipeline(mutoolPath, qpdfPath, inputPath, outputPath);
                    bestSetting = CompressionTypeLossless;
                    goto AfterCompression;
                }

                if (proposed == CompressionTypeHybrid && toolsAvailable)
                {
                    optimalSettings = GetOptimalSettings(documentType, imageQuality, proposed, scanColorMode, hasColor);
                    RunHybridPipeline(mutoolPath, qpdfPath, ghostscriptPath, inputPath, outputPath, optimalSettings, optimizeForScanned);
                    bestSetting = CompressionTypeHybrid;
                    goto AfterCompression;
                }

                // fallthrough -> Ghostscript-only flow (keeps existing behavior)
                optimalSettings = GetOptimalSettings(documentType, imageQuality, proposed, scanColorMode, hasColor);
                bestSetting = optimalSettings.PdfSetting;
            }
            else if (compressionType == CompressionTypeLossless || compressionType == CompressionTypeHybrid)
            {
                optimalSettings = GetOptimalSettings(documentType, imageQuality, compressionType, scanColorMode, hasColor);
                bestSetting = compressionType;

                if (!toolsAvailable)
                {
                    throw new FileNotFoundException(
                        "Thiếu công cụ tối ưu PDF.\n\n" +
                        "Vui lòng tạo thư mục 'Tools' cạnh file chạy (.exe) và copy:\n" +
                        "- mutool.exe (MuPDF)\n" +
                        "- qpdf.exe\n\n" +
                        $"Đường dẫn kiểm tra:\n- {mutoolPath}\n- {qpdfPath}");
                }

                if (compressionType == CompressionTypeLossless)
                {
                    RunLosslessPipeline(mutoolPath, qpdfPath, inputPath, outputPath);
                }
                else
                {
                    RunHybridPipeline(mutoolPath, qpdfPath, ghostscriptPath, inputPath, outputPath, optimalSettings, optimizeForScanned);
                }

                goto AfterCompression;
            }
            else
            {
                bestSetting = GetPdfSettingFromType(compressionType);
                optimalSettings = new CompressionSettings
                {
                    PdfSetting = bestSetting,
                    JpegQuality = imageQuality,
                    ColorImageResolution = 300,
                    GrayImageResolution = 300,
                    MonoImageResolution = 300,
                    DownsampleType = "/Bicubic",
                    DownsampleThreshold = 1.0
                };
                LogMessage($"Sử dụng thiết lập nén: {bestSetting}");
            }

            // Ghostscript-only flow
            RunGhostscriptOnly(ghostscriptPath, inputPath, outputPath, optimalSettings, optimizeForScanned);

        AfterCompression:

            cancellationToken.ThrowIfCancellationRequested();

            var originalSize = GetFileSize(inputPath);
            var compressedSize = GetFileSize(outputPath);
            if (originalSize <= 0)
            {
                throw new InvalidDataException("File PDF gốc rỗng hoặc không thể đọc kích thước.");
            }
            if (compressedSize <= 0)
            {
                throw new InvalidOperationException("Công cụ nén không tạo được file kết quả hợp lệ.");
            }

            // Guard: sometimes scanned PDFs become larger after re-encoding.
            // If that happens, retry once with a more aggressive scanned profile, and if still not smaller,
            // warn the user and offer to delete the "compressed" output.
            if (compressedSize >= originalSize)
            {
                bool isScanned = string.Equals(documentType, "Scanned Document", StringComparison.OrdinalIgnoreCase) || optimizeForScanned || isScanPreset;
                if (isScanned)
                {
                    string? retryTempPath = null;
                    try
                    {
                        retryTempPath = System.IO.Path.Combine(
                            System.IO.Path.GetDirectoryName(outputPath) ?? "",
                            $"{System.IO.Path.GetFileNameWithoutExtension(outputPath)}.retry.{Guid.NewGuid():N}.pdf");

                        var aggressive = new CompressionSettings
                        {
                            PdfSetting = "/screen",
                            ColorImageResolution = 120,
                            GrayImageResolution = 120,
                            MonoImageResolution = 300,
                            JpegQuality = Math.Min(55, optimalSettings.JpegQuality),
                            UseAutoFilter = false,
                            UseDCTEncode = true,
                            DownsampleColorImages = true,
                            DownsampleGrayImages = true,
                            DownsampleMonoImages = true,
                            DownsampleType = "/Bicubic",
                            DownsampleThreshold = 1.0,
                            PassThroughJPEGImages = false,
                            ConvertToGray = true,
                            UseJbig2 = true
                        };

                        LogMessage($"Cảnh báo: file sau nén đang lớn hơn hoặc bằng file gốc ({FormatFileSize(compressedSize)} >= {FormatFileSize(originalSize)}). Thử lại cấu hình scan mạnh hơn...");

                        if (toolsAvailable)
                        {
                            RunScanOptimizedPipeline(mutoolPath, qpdfPath, ghostscriptPath, inputPath, retryTempPath, aggressive, cancellationToken);
                        }
                        else
                        {
                            var retryArgs = BuildIntelligentGhostscriptArguments(inputPath, retryTempPath, aggressive, true);
                            RunProcessOrThrow(ghostscriptPath, retryArgs, "Ghostscript retry");
                        }

                        var retrySize = GetFileSize(retryTempPath);
                        if (retrySize > 0 && retrySize < compressedSize && retrySize < originalSize)
                        {
                            try
                            {
                                // Atomically replace the output. If replacement fails, the
                                // existing compressed file remains intact.
                                var backupPath = outputPath + $".backup.{Guid.NewGuid():N}";
                                System.IO.File.Replace(retryTempPath, outputPath, backupPath, true);
                                try { System.IO.File.Delete(backupPath); } catch { }
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
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"Retry scan compression failed: {ex.Message}");
                    }
                    finally
                    {
                        try
                        {
                            if (!string.IsNullOrEmpty(retryTempPath) && System.IO.File.Exists(retryTempPath))
                            {
                                System.IO.File.Delete(retryTempPath);
                            }
                        }
                        catch { }
                    }
                }

                if (compressedSize >= originalSize)
                {
                    LogMessage($"Cảnh báo: file sau nén không nhỏ hơn file gốc ({FormatFileSize(compressedSize)} >= {FormatFileSize(originalSize)}).");

                    DialogResult keepResult = DialogResult.Yes;
                    if (!InvokeOnUi(() =>
                    {
                        keepResult = MessageBox.Show(
                            $"Kết quả nén không hiệu quả (file sau nén không nhỏ hơn file gốc).\n\nGốc: {FormatFileSize(originalSize)}\nSau nén: {FormatFileSize(compressedSize)}\n\nBạn có muốn GIỮ file nén không?\n- Yes: Giữ file nén\n- No: Xóa file nén (giữ file gốc)",
                            "Nén không giảm dung lượng",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Warning);
                    }))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    if (keepResult == DialogResult.No)
                    {
                        try
                        {
                            System.IO.File.Delete(outputPath);
                            LogMessage("Đã xóa file nén vì dung lượng không giảm.");
                            InvokeOnUi(() => statusLabel.Text = "Đã hủy kết quả nén không hiệu quả.");
                            return;
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
                if (!InvokeOnUi(() =>
                {
                    splitResult = MessageBox.Show(
                        $"File sau nén vẫn còn lớn.\n\nKích thước: {FormatFileSize(compressedSize)}\nNgưỡng gợi ý: {suggestThresholdMB}MB\n\nBạn có muốn chia file thành các phần khoảng {splitPartSizeMB}MB không?",
                        "Gợi ý chia file",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);
                }))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                if (splitResult == DialogResult.Yes)
                {
                    LogMessage($"Người dùng chọn chia file. Đang chia nhỏ (tối đa ~{splitPartSizeMB}MB/phần)...");
                    SplitPdfFile(outputPath, splitPartSizeMB * MB, cancellationToken);
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
            InvokeOnUi(() =>
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
            });
        }
        catch (OperationCanceledException)
        {
            LogMessage("Đã hủy quá trình nén.");
        }
        catch (Exception ex)
        {
            LogMessage($"Error during compression: {ex.Message}");
            InvokeOnUi(() =>
            {
                statusLabel.Text = "Compression failed!";
                MessageBox.Show($"Compression failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            });
        }
        finally
        {
            isCompressionRunning = false;
            InvokeOnUi(() =>
            {
                selectFileButton.Enabled = true;
                compressFileButton.Enabled = true;
                comboBoxCompressionType.Enabled = true;
                comboBoxScanColorMode.Enabled = checkBoxOptimizeForScanned.Checked;
                trackBarImageQuality.Enabled = true;
                progressBar.Visible = false;
            });
        }
    }

    private void RunMutoolClean(string mutoolPath, string inputPdf, string outputPdf, CancellationToken cancellationToken = default)
    {
        // mutool clean -g -z "in" "out"
        var args = $"clean -g -z \"{inputPdf}\" \"{outputPdf}\"";
        RunProcessOrThrow(mutoolPath, args, "mutool clean", cancellationToken);
    }

    private void RunQpdfOptimize(string qpdfPath, string inputPdf, string outputPdf, CancellationToken cancellationToken = default)
    {
        // qpdf "in" --compress-streams=y --decode-level=generalized --recompress-flate --compression-level=9 --optimize-images --object-streams=generate "out"
        var args =
            $"\"{inputPdf}\" " +
            "--compress-streams=y " +
            "--decode-level=generalized " +
            "--recompress-flate " +
            "--compression-level=9 " +
            "--optimize-images " +
            "--object-streams=generate " +
            $"\"{outputPdf}\"";
        RunProcessOrThrow(qpdfPath, args, "qpdf optimize", cancellationToken);
    }

    private void RunProcessOrThrow(string exePath, string arguments, string label, CancellationToken cancellationToken = default)
    {
        var process = new System.Diagnostics.Process();
        process.StartInfo.FileName = exePath;
        process.StartInfo.Arguments = arguments;
        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.StandardOutputEncoding = System.Text.Encoding.UTF8;
        process.StartInfo.StandardErrorEncoding = System.Text.Encoding.UTF8;

        LogMessage($"Thực thi {label}: {System.IO.Path.GetFileName(exePath)} {arguments}");

        var stdoutBuffer = new System.Text.StringBuilder();
        var stderrBuffer = new System.Text.StringBuilder();
        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                stdoutBuffer.AppendLine(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                stderrBuffer.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        while (!process.WaitForExit(200))
        {
            if (isClosing || cancellationToken.IsCancellationRequested)
            {
                try { process.Kill(true); } catch { }
                throw new OperationCanceledException($"Đã hủy {label}.");
            }
        }
        process.WaitForExit(1000);

        string stdout = stdoutBuffer.ToString();
        string stderr = stderrBuffer.ToString();

        if (!string.IsNullOrWhiteSpace(stdout) && stdout.Length < 1000)
        {
            LogMessage($"{label} stdout: {stdout.Trim()}");
        }
        if (!string.IsNullOrWhiteSpace(stderr) && stderr.Length < 1000)
        {
            LogMessage($"{label} stderr: {stderr.Trim()}");
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"{label} thất bại (ExitCode={process.ExitCode}).");
        }
    }

    private bool AreExternalToolsAvailable(out string mutoolPath, out string qpdfPath)
    {
        var toolsDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools");
        mutoolPath = System.IO.Path.Combine(toolsDir, "mutool.exe");
        qpdfPath = System.IO.Path.Combine(toolsDir, "qpdf.exe");
        return System.IO.File.Exists(mutoolPath) && System.IO.File.Exists(qpdfPath);
    }

    private void RunLosslessPipeline(string mutoolPath, string qpdfPath, string inputPath, string outputPath)
    {
        string tmpMutoolOut = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{Guid.NewGuid():N}.mutool.pdf");
        try
        {
            LogMessage("Bước 1: mutool clean (lossless)...");
            UpdateProgress(25);
            RunMutoolClean(mutoolPath, inputPath, tmpMutoolOut);

            LogMessage("Bước 2: qpdf optimize (lossless)...");
            UpdateProgress(70);
            RunQpdfOptimize(qpdfPath, tmpMutoolOut, outputPath);
            UpdateProgress(90);
            LogMessage("Hoàn tất tối ưu lossless (mutool + qpdf).");
        }
        finally
        {
            try { if (System.IO.File.Exists(tmpMutoolOut)) System.IO.File.Delete(tmpMutoolOut); } catch { }
        }
    }

    private void RunHybridPipeline(
        string mutoolPath,
        string qpdfPath,
        string ghostscriptPath,
        string inputPath,
        string outputPath,
        CompressionSettings optimalSettings,
        bool optimizeForScanned)
    {
        string tmpMutoolOut = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{Guid.NewGuid():N}.mutool.pdf");
        string tmpGsOut = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{Guid.NewGuid():N}.gs.pdf");

        try
        {
            LogMessage("Bước 1: mutool clean (lossless)...");
            UpdateProgress(25);
            RunMutoolClean(mutoolPath, inputPath, tmpMutoolOut);

            LogMessage($"Bước 2: Ghostscript nén (profile: {optimalSettings.PdfSetting})...");
            UpdateProgress(45);

            // Try GhostscriptAPI first, fallback to process-based
            try
            {
                if (!GhostscriptAPI.IsGhostscriptAvailable())
                {
                    throw new GhostscriptException("Ghostscript DLL not available", -1);
                }

                using (var gsApi = new GhostscriptAPI())
                {
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

                    gsApi.CompressPdf(tmpMutoolOut, tmpGsOut, compressionSettings);
                }
            }
            catch (GhostscriptException gsEx)
            {
                string errorMsg = GhostscriptAPI.GetErrorMessage(gsEx.ErrorCode);
                LogMessage($"Lỗi Ghostscript API: {errorMsg}");
                LogMessage("Fallback: chạy Ghostscript dạng process...");

                var arguments = BuildIntelligentGhostscriptArguments(tmpMutoolOut, tmpGsOut, optimalSettings, optimizeForScanned);
                RunProcessOrThrow(ghostscriptPath, arguments, "Ghostscript (process)");
            }

            LogMessage("Bước 3: qpdf optimize (lossless)...");
            UpdateProgress(75);
            RunQpdfOptimize(qpdfPath, tmpGsOut, outputPath);
            UpdateProgress(90);
            LogMessage("Hoàn tất tối ưu Hybrid (mutool + Ghostscript + qpdf).");
        }
        finally
        {
            try { if (System.IO.File.Exists(tmpMutoolOut)) System.IO.File.Delete(tmpMutoolOut); } catch { }
            try { if (System.IO.File.Exists(tmpGsOut)) System.IO.File.Delete(tmpGsOut); } catch { }
        }
    }

    private void RunScanOptimizedPipeline(
        string mutoolPath,
        string qpdfPath,
        string ghostscriptPath,
        string inputPath,
        string outputPath,
        CompressionSettings optimalSettings,
        CancellationToken cancellationToken)
    {
        string tmpGsOut = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{Guid.NewGuid():N}.gs.pdf");
        string tmpMutoolOut = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{Guid.NewGuid():N}.mutool.pdf");

        try
        {
            string colorDesc = optimalSettings.ConvertToGray ? "Ảnh xám/Đen trắng" : "Giữ màu sRGB";
            LogMessage($"Bước 1: Ghostscript tái cấu trúc scan (DPI: {optimalSettings.ColorImageResolution}, Bicubic, JPEG-Q: {optimalSettings.JpegQuality}%, {colorDesc})...");
            UpdateProgress(35);

            // Execute Ghostscript process directly: isolated, fast, immune to native DLL thread lock
            var arguments = BuildIntelligentGhostscriptArguments(inputPath, tmpGsOut, optimalSettings, true);
            RunProcessOrThrow(ghostscriptPath, arguments, "Ghostscript (process)", cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            string currentIntermediate = tmpGsOut;

            // Step 2: MuTool clean with JBIG2 recompression and stream deduplication
            if (System.IO.File.Exists(mutoolPath))
            {
                LogMessage("Bước 2: mutool khử trùng lặp đối tượng & nén JBIG2 cho trang đen trắng...");
                UpdateProgress(65);

                var mutoolArgs = optimalSettings.UseJbig2
                    ? $"clean -gggg -z -i -Z --bitonal-image-recompress-method=jbig2 \"{currentIntermediate}\" \"{tmpMutoolOut}\""
                    : $"clean -gggg -z -i -Z \"{currentIntermediate}\" \"{tmpMutoolOut}\"";

                try
                {
                    RunProcessOrThrow(mutoolPath, mutoolArgs, "mutool clean + jbig2", cancellationToken);
                    if (System.IO.File.Exists(tmpMutoolOut) && GetFileSize(tmpMutoolOut) > 0)
                    {
                        currentIntermediate = tmpMutoolOut;
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    LogMessage($"Bỏ qua bước mutool (lỗi: {ex.Message}), tiếp tục với kết quả Ghostscript.");
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Step 3: QPDF optimization and object streams
            if (System.IO.File.Exists(qpdfPath))
            {
                LogMessage("Bước 3: qpdf tối ưu hóa luồng đối tượng (Object Streams)...");
                UpdateProgress(85);
                RunQpdfOptimize(qpdfPath, currentIntermediate, outputPath, cancellationToken);
            }
            else
            {
                System.IO.File.Copy(currentIntermediate, outputPath, true);
            }

            UpdateProgress(95);
            LogMessage("Hoàn tất quy trình nén tối ưu cho PDF Scan.");
        }
        finally
        {
            try { if (System.IO.File.Exists(tmpGsOut)) System.IO.File.Delete(tmpGsOut); } catch { }
            try { if (System.IO.File.Exists(tmpMutoolOut)) System.IO.File.Delete(tmpMutoolOut); } catch { }
        }
    }

    private void RunGhostscriptOnly(
        string ghostscriptPath,
        string inputPath,
        string outputPath,
        CompressionSettings optimalSettings,
        bool optimizeForScanned,
        CancellationToken cancellationToken = default)
    {
        UpdateProgress(50);
        LogMessage("Bắt đầu nén với Ghostscript...");

        var arguments = BuildIntelligentGhostscriptArguments(inputPath, outputPath, optimalSettings, optimizeForScanned);
        RunProcessOrThrow(ghostscriptPath, arguments, "Ghostscript", cancellationToken);

        UpdateProgress(90);
        LogMessage("Nén file thành công với Ghostscript!");
    }

    private bool DetectDocumentHasColor(string filePath)
    {
        if (cachedHasColor.HasValue)
        {
            return cachedHasColor.Value;
        }

        try
        {
            var mutoolPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "mutool.exe");
            if (System.IO.File.Exists(mutoolPath))
            {
                using var process = new System.Diagnostics.Process();
                process.StartInfo.FileName = mutoolPath;
                process.StartInfo.Arguments = $"info -I \"{filePath}\"";
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = false; // Prevent pipe deadlock
                process.Start();

                var outputTask = process.StandardOutput.ReadToEndAsync();
                if (process.WaitForExit(3000))
                {
                    string info = outputTask.GetAwaiter().GetResult();
                    if (!string.IsNullOrEmpty(info))
                    {
                        if (info.Contains("DeviceRGB", StringComparison.OrdinalIgnoreCase) ||
                            info.Contains("DeviceCMYK", StringComparison.OrdinalIgnoreCase) ||
                            info.Contains("ICCBased", StringComparison.OrdinalIgnoreCase))
                        {
                            cachedHasColor = true;
                            return true;
                        }
                        if (info.Contains("DeviceGray", StringComparison.OrdinalIgnoreCase) ||
                            info.Contains("1bpc", StringComparison.OrdinalIgnoreCase))
                        {
                            cachedHasColor = false;
                            return false;
                        }
                    }
                }
                else
                {
                    try { process.Kill(true); } catch { }
                }
            }

            // Fallback: check sample of file
            var sample = ReadPdfSample(filePath);
            bool hasColor = sample.Contains("/DeviceRGB", StringComparison.Ordinal) ||
                            sample.Contains("/DeviceCMYK", StringComparison.Ordinal);
            cachedHasColor = hasColor;
            return hasColor;
        }
        catch
        {
            return true;
        }
    }

    private bool IsPdfLayeredQuick(string filePath)
    {
        // Detect common layer/OCG markers: /OCProperties /OCG /OCGs
        // Read a limited chunk to avoid heavy IO.
        try
        {
            const int maxBytes = 4 * 1024 * 1024; // 4MB should be enough for header/xref of many PDFs
            byte[] data;
            using (var fs = new System.IO.FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                int toRead = (int)Math.Min(fs.Length, maxBytes);
                data = new byte[toRead];
                int read = fs.Read(data, 0, toRead);
                if (read <= 0) return false;
                if (read != toRead)
                {
                    Array.Resize(ref data, read);
                }
            }

            // PDFs are mostly ASCII tokens; ISO-8859-1 decoding is safe for byte->char mapping.
            string s = System.Text.Encoding.Latin1.GetString(data);
            return s.Contains("/OCProperties", StringComparison.Ordinal) ||
                   s.Contains("/OCG", StringComparison.Ordinal) ||
                   s.Contains("/OCGs", StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    private void UpdateProgress(int value)
    {
        InvokeOnUi(() => progressBar.Value = Math.Clamp(value, progressBar.Minimum, progressBar.Maximum));
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
        var arguments =
            $"-sDEVICE=pdfwrite -dCompatibilityLevel=1.4 -dPDFSETTINGS={pdfSetting} " +
            "-dNOPAUSE -dQUIET -dBATCH " +
            // Extra optimization flags (inspired by FileOptimizer pipeline)
            "-dSAFER -dDELAYSAFER -dNOPROMPT " +
            "-dDetectDuplicateImages=true -dAutoRotatePages=/None -dOptimize=true " +
            "-dConvertCMYKImagesToRGB=true -dColorConversionStrategy=/sRGB -dPrinted=false";

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

            // PDF streams are binary and can be very large. Sample the beginning and
            // end instead of loading the entire file on the UI thread.
            var pdfContent = ReadPdfSample(filePath);

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

    private static string ReadPdfSample(string filePath)
    {
        const int sampleBytes = 2 * 1024 * 1024;
        using var stream = new System.IO.FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        int firstLength = (int)Math.Min(stream.Length, sampleBytes);
        var first = new byte[firstLength];
        stream.ReadExactly(first);

        if (stream.Length <= sampleBytes * 2L)
        {
            return System.Text.Encoding.Latin1.GetString(first);
        }

        stream.Seek(-sampleBytes, SeekOrigin.End);
        var last = new byte[sampleBytes];
        stream.ReadExactly(last);
        return System.Text.Encoding.Latin1.GetString(first) + System.Text.Encoding.Latin1.GetString(last);
    }

    private bool IsScannedDocument(string pdfContent, string filePath)
    {
        // Check for common indicators of scanned documents
        var fileSize = GetFileSize(filePath);

        // Count text and image indicators
        var textOperatorCount = CountOccurrences(pdfContent, "BT") + CountOccurrences(pdfContent, "Tj");
        var inlineImageCount = CountOccurrences(pdfContent, "BI") + CountOccurrences(pdfContent, "ID");
        var xobjectImageCount = CountOccurrences(pdfContent, "/Subtype /Image") + CountOccurrences(pdfContent, "/Subtype/Image");
        var imageCount = inlineImageCount + xobjectImageCount;

        // If file has images and very little/no text, it's a scanned document
        if (imageCount > 0 && textOperatorCount < 20)
        {
            return true;
        }

        // Large file size with limited text content suggests scanned images
        if (fileSize > 3 * 1024 * 1024 && imageCount > textOperatorCount * 2)
        {
            return true;
        }

        // Check for scan-related metadata
        if (pdfContent.Contains("Scanner", StringComparison.OrdinalIgnoreCase) ||
            pdfContent.Contains("Scan", StringComparison.OrdinalIgnoreCase) ||
            pdfContent.Contains("TWAIN", StringComparison.OrdinalIgnoreCase) ||
            pdfContent.Contains("WIA", StringComparison.OrdinalIgnoreCase))
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

    private CompressionSettings GetOptimalSettings(
        string documentType,
        int imageQuality,
        string compressionType = "",
        string scanColorMode = "",
        bool hasColorImages = true)
    {
        bool isScanSuper = compressionType == CompressionTypeScanSuper;
        bool isScanBalanced = compressionType == CompressionTypeScanBalanced;
        bool isScanHigh = compressionType == CompressionTypeScanHighQuality;
        bool isScanned = isScanSuper || isScanBalanced || isScanHigh || string.Equals(documentType, "Scanned Document", StringComparison.OrdinalIgnoreCase);

        if (isScanned)
        {
            bool convertToGray = false;
            if (scanColorMode == ScanColorMono || isScanSuper)
            {
                convertToGray = true;
            }
            else if (scanColorMode == ScanColorGray)
            {
                convertToGray = true;
            }
            else if (scanColorMode == ScanColorAuto && !hasColorImages)
            {
                convertToGray = true;
            }

            int res = isScanHigh ? 180 : (isScanSuper ? 130 : 150);
            int q = isScanHigh ? Math.Min(80, Math.Max(72, imageQuality)) : (isScanSuper ? Math.Min(60, imageQuality) : Math.Min(75, imageQuality));

            return new CompressionSettings
            {
                PdfSetting = isScanHigh ? "/printer" : "/screen",
                ColorImageResolution = res,
                GrayImageResolution = res,
                MonoImageResolution = 300,
                JpegQuality = q,
                UseAutoFilter = false,
                UseDCTEncode = true,
                DownsampleColorImages = true,
                DownsampleGrayImages = true,
                DownsampleMonoImages = true,
                DownsampleType = "/Bicubic",
                DownsampleThreshold = 1.0,
                PassThroughJPEGImages = false,
                ConvertToGray = convertToGray,
                UseJbig2 = true
            };
        }

        return documentType switch
        {
            "Text Document" => new CompressionSettings
            {
                PdfSetting = "/ebook",
                ColorImageResolution = 300,
                GrayImageResolution = 300,
                MonoImageResolution = 300,
                JpegQuality = Math.Max(80, imageQuality),
                UseAutoFilter = true,
                UseDCTEncode = false,
                DownsampleColorImages = false,
                DownsampleGrayImages = false,
                DownsampleMonoImages = false,
                DownsampleType = "/Bicubic",
                DownsampleThreshold = 1.0,
                PassThroughJPEGImages = true
            },
            "Mixed Content" => new CompressionSettings
            {
                PdfSetting = "/printer",
                ColorImageResolution = 200,
                GrayImageResolution = 200,
                MonoImageResolution = 300,
                JpegQuality = imageQuality,
                UseAutoFilter = true,
                UseDCTEncode = true,
                DownsampleColorImages = true,
                DownsampleGrayImages = true,
                DownsampleMonoImages = true,
                DownsampleType = "/Bicubic",
                DownsampleThreshold = 1.0,
                PassThroughJPEGImages = false
            },
            _ => new CompressionSettings
            {
                PdfSetting = "/default",
                ColorImageResolution = 300,
                GrayImageResolution = 300,
                MonoImageResolution = 300,
                JpegQuality = 75,
                UseAutoFilter = true,
                UseDCTEncode = false,
                DownsampleColorImages = false,
                DownsampleGrayImages = false,
                DownsampleMonoImages = false,
                DownsampleType = "/Bicubic",
                DownsampleThreshold = 1.0,
                PassThroughJPEGImages = true
            }
        };
    }

    private class CompressionSettings
    {
        public string PdfSetting { get; set; } = "/default";
        public int ColorImageResolution { get; set; } = 300;
        public int GrayImageResolution { get; set; } = 300;
        public int MonoImageResolution { get; set; } = 300;
        public int JpegQuality { get; set; } = 75;
        public bool UseAutoFilter { get; set; } = true;
        public bool UseDCTEncode { get; set; } = false;
        public bool DownsampleColorImages { get; set; } = false;
        public bool DownsampleGrayImages { get; set; } = false;
        public bool DownsampleMonoImages { get; set; } = false;
        public string DownsampleType { get; set; } = "/Bicubic";
        public double DownsampleThreshold { get; set; } = 1.0;
        public bool PassThroughJPEGImages { get; set; } = false;
        public string ColorConversionStrategy { get; set; } = "/sRGB";
        public bool ConvertToGray { get; set; } = false;
        public bool UseJbig2 { get; set; } = false;
    }


    private long GetFileSize(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
            return 0;
        return new System.IO.FileInfo(filePath).Length;
    }

    private string BuildIntelligentGhostscriptArguments(string inputPath, string outputPath, CompressionSettings settings, bool optimizeForScanned)
    {
        var arguments =
            $"-sDEVICE=pdfwrite -dCompatibilityLevel=1.5 -dPDFSETTINGS={settings.PdfSetting} " +
            "-dNOPAUSE -dQUIET -dBATCH " +
            "-dSAFER -dDELAYSAFER -dNOPROMPT " +
            "-dDetectDuplicateImages=true -dAutoRotatePages=/None -dOptimize=true -dPrinted=false";

        if (settings.ConvertToGray)
        {
            arguments += " -dColorConversionStrategy=/Gray -dProcessColorModel=/DeviceGray";
        }
        else
        {
            arguments += $" -dConvertCMYKImagesToRGB=true -dColorConversionStrategy={settings.ColorConversionStrategy}";
        }

        if (!settings.PassThroughJPEGImages)
        {
            arguments += " -dPassThroughJPEGImages=false";
        }

        if (optimizeForScanned || settings.DownsampleColorImages)
        {
            arguments += $" -dDownsampleColorImages=true -dColorImageDownsampleType={settings.DownsampleType}";
            arguments += $" -dColorImageResolution={settings.ColorImageResolution}";
            arguments += $" -dColorImageDownsampleThreshold={settings.DownsampleThreshold:F1}";
        }

        if (optimizeForScanned || settings.DownsampleGrayImages)
        {
            arguments += $" -dDownsampleGrayImages=true -dGrayImageDownsampleType={settings.DownsampleType}";
            arguments += $" -dGrayImageResolution={settings.GrayImageResolution}";
            arguments += $" -dGrayImageDownsampleThreshold={settings.DownsampleThreshold:F1}";
        }

        if (optimizeForScanned || settings.DownsampleMonoImages)
        {
            arguments += " -dDownsampleMonoImages=true -dMonoImageDownsampleType=/Subsample";
            arguments += $" -dMonoImageResolution={settings.MonoImageResolution}";
            arguments += $" -dMonoImageDownsampleThreshold={settings.DownsampleThreshold:F1}";
            arguments += " -dMonoImageFilter=/CCITTFaxEncode";
        }

        if (settings.UseDCTEncode)
        {
            arguments += " -dAutoFilterColorImages=false -dColorImageFilter=/DCTEncode";
            arguments += " -dAutoFilterGrayImages=false -dGrayImageFilter=/DCTEncode";
        }

        arguments += " -dEncodeColorImages=true -dEncodeGrayImages=true -dEncodeMonoImages=true";
        arguments += $" -dJPEGQ={settings.JpegQuality}";

        if (settings.PdfSetting == "/screen")
        {
            arguments += " -dSubsetFonts=true -dEmbedAllFonts=false";
        }
        else
        {
            arguments += " -dSubsetFonts=true -dEmbedAllFonts=true";
        }

        arguments += $" -sOutputFile=\"{outputPath}\" \"{inputPath}\"";

        return arguments;
    }

    private void SplitPdfFile(string inputPath, long maxSplitSize, CancellationToken cancellationToken)
    {
        if (maxSplitSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxSplitSize), "Kích thước phần phải lớn hơn 0.");
        }

        var directory = System.IO.Path.GetDirectoryName(inputPath) ?? "";
        var filenameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(inputPath);
        var outputStem = filenameWithoutExt;
        if (System.IO.File.Exists(System.IO.Path.Combine(directory, $"{outputStem}.part1.pdf")))
        {
            outputStem = $"{filenameWithoutExt}.split.{DateTime.Now:yyyyMMddHHmmss}";
        }
        var inputSize = GetFileSize(inputPath);
        if (inputSize <= 0)
        {
            throw new InvalidDataException("File cần chia rỗng hoặc không tồn tại.");
        }

        int pageCount = GetPdfPageCount(inputPath);
        int pagesPerPart = Math.Max(1, (int)Math.Floor((double)pageCount * maxSplitSize / inputSize));
        int startPage = 1;
        int partNumber = 1;
        var createdParts = new List<string>();

        LogMessage($"Đang chia {pageCount} trang thành các phần tối đa khoảng {FormatFileSize(maxSplitSize)}...");

        try
        {
            while (startPage <= pageCount)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int endPage = Math.Min(pageCount, startPage + pagesPerPart - 1);
                var splitOutputPath = System.IO.Path.Combine(directory, $"{outputStem}.part{partNumber}.pdf");
                createdParts.Add(splitOutputPath);

                WritePdfPageRange(inputPath, splitOutputPath, startPage, endPage);
                long partSize = GetFileSize(splitOutputPath);

                // Reduce the page range until the part fits. A single oversized page
                // cannot be split further, so keep it and report that exception.
                while (partSize > maxSplitSize && endPage > startPage)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int currentPages = endPage - startPage + 1;
                    int reducedPages = Math.Max(1, (int)Math.Floor(currentPages * (double)maxSplitSize / partSize));
                    endPage = startPage + Math.Min(currentPages - 1, reducedPages) - 1;
                    WritePdfPageRange(inputPath, splitOutputPath, startPage, endPage);
                    partSize = GetFileSize(splitOutputPath);
                }

                if (partSize <= 0)
                {
                    throw new InvalidOperationException($"Không tạo được phần PDF {partNumber}.");
                }

                if (partSize > maxSplitSize)
                {
                    LogMessage($"Phần {partNumber} chỉ có một trang nhưng vẫn lớn hơn giới hạn ({FormatFileSize(partSize)}).");
                }
                else
                {
                    LogMessage($"Đã tạo phần {partNumber}: trang {startPage}-{endPage}, {FormatFileSize(partSize)}.");
                }

                int pagesCreated = endPage - startPage + 1;
                startPage = endPage + 1;
                partNumber++;

                if (partSize > 0)
                {
                    pagesPerPart = Math.Max(1, (int)Math.Floor(pagesCreated * (double)maxSplitSize / partSize));
                }
                UpdateProgress(Math.Min(99, 90 + (int)(9.0 * (startPage - 1) / pageCount)));
            }
        }
        catch
        {
            foreach (var partPath in createdParts)
            {
                try { System.IO.File.Delete(partPath); } catch { }
            }
            throw;
        }

        LogMessage($"Chia file hoàn tất: {createdParts.Count} phần, đủ {pageCount} trang.");
        InvokeOnUi(() =>
        {
            var result = MessageBox.Show(
                $"File đã được chia thành {createdParts.Count} phần và đủ {pageCount} trang.\n\nBạn có muốn mở thư mục kết quả không?",
                "Chia file hoàn tất",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);

            if (result == DialogResult.Yes)
            {
                OpenOutputFolder(directory);
            }
        });
    }

    private int GetPdfPageCount(string inputPath)
    {
        var qpdfPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "qpdf.exe");
        if (System.IO.File.Exists(qpdfPath))
        {
            using var process = new System.Diagnostics.Process();
            process.StartInfo.FileName = qpdfPath;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.ArgumentList.Add("--show-npages");
            process.StartInfo.ArgumentList.Add(inputPath);
            process.Start();
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            while (!process.WaitForExit(250))
            {
                if (isClosing)
                {
                    try { process.Kill(true); } catch { }
                    throw new OperationCanceledException("Đã hủy thao tác đếm trang PDF.");
                }
            }
            string output = outputTask.GetAwaiter().GetResult();
            string error = errorTask.GetAwaiter().GetResult();
            if (process.ExitCode == 0 && int.TryParse(output.Trim(), out int pageCount) && pageCount > 0)
            {
                return pageCount;
            }
            LogMessage($"qpdf không đếm được số trang: {error.Trim()}");
        }

        using var gsApi = new GhostscriptAPI();
        int fallbackPageCount = gsApi.GetPdfPageCount(inputPath);
        if (fallbackPageCount <= 0)
        {
            throw new InvalidDataException("Không xác định được số trang PDF.");
        }
        return fallbackPageCount;
    }

    private void WritePdfPageRange(string inputPath, string outputPath, int startPage, int endPage)
    {
        try { if (System.IO.File.Exists(outputPath)) System.IO.File.Delete(outputPath); } catch { }

        try
        {
            using var gsApi = new GhostscriptAPI();
            gsApi.SplitPdfByPageRange(inputPath, outputPath, startPage, endPage);
        }
        catch (GhostscriptException gsEx)
        {
            LogMessage($"Ghostscript API không chia được trang {startPage}-{endPage}: {gsEx.Message}. Dùng process fallback...");
            var ghostscriptPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Ghostscript", "gswin64c.exe");
            var arguments = $"-sDEVICE=pdfwrite -dNOPAUSE -dBATCH -dFirstPage={startPage} -dLastPage={endPage} -sOutputFile=\"{outputPath}\" \"{inputPath}\"";
            RunProcessOrThrow(ghostscriptPath, arguments, $"Ghostscript split {startPage}-{endPage}");
        }

        if (GetFileSize(outputPath) <= 0)
        {
            throw new InvalidOperationException($"Không tạo được PDF cho trang {startPage}-{endPage}.");
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
        isClosing = true;
        compressionCancellation?.Cancel();
        mergeCancellation?.Cancel();
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

        InvokeOnUi(() => mergeLogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\r\n"));
    }

    private void UpdateMergeProgress(int value)
    {
        InvokeOnUi(() => mergeProgressBar.Value = Math.Clamp(value, mergeProgressBar.Minimum, mergeProgressBar.Maximum));
    }

    private void UpdateMergeStatus(string status)
    {
        InvokeOnUi(() => mergeStatusLabel.Text = status);
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
            if (mergePdfFiles.Any(inputPath => PathsEqual(inputPath, saveMergeFileDialog.FileName)))
            {
                MessageBox.Show("File kết quả phải khác tất cả file PDF đầu vào.", "Đường dẫn không hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
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

        mergeCancellation?.Dispose();
        mergeCancellation = new CancellationTokenSource();
        var cancellationToken = mergeCancellation.Token;
        mergeTask = Task.Run(() => MergePdfWorker(filesToMerge, outputPath, cancellationToken));

        LogMergeMessage("Bắt đầu gộp file PDF...");
        UpdateMergeStatus("Đang gộp...");
    }

    private void MergePdfWorker(List<string> inputPaths, string outputPath, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (inputPaths.Count < 2)
            {
                throw new ArgumentException("At least 2 PDF files are required for merging");
            }
            if (inputPaths.Any(inputPath => PathsEqual(inputPath, outputPath)))
            {
                throw new InvalidOperationException("File kết quả phải khác tất cả file PDF đầu vào.");
            }

            LogMergeMessage($"Số lượng file cần gộp: {inputPaths.Count}");
            UpdateMergeProgress(10);

            // Validate all input files exist
            foreach (string filePath in inputPaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
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
                    if (GetFileSize(outputPath) <= 0)
                    {
                        throw new InvalidOperationException("Output file was not created or is empty");
                    }

                    var outputSize = GetFileSize(outputPath);
                    var totalInputSize = inputPaths.Sum(GetFileSize);

                    LogMergeMessage($"File kết quả: {System.IO.Path.GetFileName(outputPath)}");
                    LogMergeMessage($"Kích thước tổng các file gốc: {FormatFileSize(totalInputSize)}");
                    LogMergeMessage($"Kích thước file sau khi gộp: {FormatFileSize(outputSize)}");

                    UpdateMergeProgress(100);

                    // Update UI on main thread
                    InvokeOnUi(() =>
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
                    });
                }
                catch (GhostscriptException gsEx)
                {
                    string errorMsg = GhostscriptAPI.GetErrorMessage(gsEx.ErrorCode);
                    LogMergeMessage($"Lỗi Ghostscript API: {errorMsg}");
                    LogMergeMessage($"Chi tiết: {gsEx.Message}");

                    // Fallback to process-based approach
                    LogMergeMessage("Thử phương pháp thay thế...");
                    MergePdfWithProcess(inputPaths, outputPath, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            LogMergeMessage("Đã hủy quá trình gộp PDF.");
        }
        catch (Exception ex)
        {
            LogMergeMessage($"Lỗi khi gộp file: {ex.Message}");
            InvokeOnUi(() =>
            {
                UpdateMergeStatus("Gộp thất bại!");
                MessageBox.Show($"Merge failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            });
        }
        finally
        {
            isMergeRunning = false;
            InvokeOnUi(() =>
            {
                UpdateMergeButtonState();
                mergeProgressBar.Visible = false;
            });
        }
    }

    private void MergePdfWithProcess(List<string> inputPaths, string outputPath, CancellationToken cancellationToken)
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
            while (!process.WaitForExit(250))
            {
                if (isClosing || cancellationToken.IsCancellationRequested)
                {
                    try { process.Kill(true); } catch { }
                    throw new OperationCanceledException("Đã hủy quá trình gộp PDF.");
                }
            }

            UpdateMergeProgress(90);

            if (process.ExitCode == 0 && GetFileSize(outputPath) > 0)
            {
                LogMergeMessage("Gộp file thành công với phương pháp thay thế!");

                var outputSize = GetFileSize(outputPath);
                var totalInputSize = inputPaths.Sum(GetFileSize);

                LogMergeMessage($"File kết quả: {System.IO.Path.GetFileName(outputPath)}");
                LogMergeMessage($"Kích thước file sau khi gộp: {FormatFileSize(outputSize)}");

                UpdateMergeProgress(100);

                InvokeOnUi(() =>
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
                });
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
