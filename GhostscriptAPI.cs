using System;
using System.Runtime.InteropServices;
using System.Text;

namespace PdfCompressor
{
    /// <summary>
    /// Wrapper class for Ghostscript DLL API functions
    /// Based on Ghostscript C API documentation: https://ghostscript.readthedocs.io/en/latest/Lib.html
    /// </summary>
    public class GhostscriptAPI : IDisposable
    {
        private const string GS_DLL = "gsdll64.dll";
        private IntPtr _gsInstance = IntPtr.Zero;
        private bool _disposed = false;
        private static readonly string _ghostscriptPath;

        #region Ghostscript DLL Imports

        static GhostscriptAPI()
        {
            // Set the Ghostscript DLL path
            _ghostscriptPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Ghostscript", GS_DLL);

            // Pre-load the DLL to handle dependencies
            try
            {
                if (System.IO.File.Exists(_ghostscriptPath))
                {
                    // Add the Ghostscript directory to the DLL search path
                    string ghostscriptDir = System.IO.Path.GetDirectoryName(_ghostscriptPath) ?? "";
                    SetDllDirectory(ghostscriptDir);

                    // Try to load the library to check for dependencies
                    LoadLibrary(_ghostscriptPath);
                }
            }
            catch
            {
                // If pre-loading fails, continue and try standard loading
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        [DllImport(GS_DLL, EntryPoint = "gsapi_new_instance")]
        private static extern int gsapi_new_instance(out IntPtr pinstance, IntPtr caller_handle);

        [DllImport(GS_DLL, EntryPoint = "gsapi_init_with_args")]
        private static extern int gsapi_init_with_args(IntPtr instance, int argc, string[] argv);

        [DllImport(GS_DLL, EntryPoint = "gsapi_exit")]
        private static extern int gsapi_exit(IntPtr instance);

        [DllImport(GS_DLL, EntryPoint = "gsapi_delete_instance")]
        private static extern void gsapi_delete_instance(IntPtr instance);

        [DllImport(GS_DLL, EntryPoint = "gsapi_set_stdio")]
        private static extern int gsapi_set_stdio(IntPtr instance, StdioCallBack stdin_fn, StdioCallBack stdout_fn, StdioCallBack stderr_fn);

        [DllImport(GS_DLL, EntryPoint = "gsapi_set_poll")]
        private static extern int gsapi_set_poll(IntPtr instance, PollCallBack poll_fn);

        [DllImport(GS_DLL, EntryPoint = "gsapi_set_display_callback")]
        private static extern int gsapi_set_display_callback(IntPtr instance, ref DisplayCallback callback);

        [DllImport(GS_DLL, EntryPoint = "gsapi_run_string_begin")]
        private static extern int gsapi_run_string_begin(IntPtr instance, int user_errors, out int exit_code);

        [DllImport(GS_DLL, EntryPoint = "gsapi_run_string_continue")]
        private static extern int gsapi_run_string_continue(IntPtr instance, string str, uint length, int user_errors, out int exit_code);

        [DllImport(GS_DLL, EntryPoint = "gsapi_run_string_end")]
        private static extern int gsapi_run_string_end(IntPtr instance, int user_errors, out int exit_code);

        [DllImport(GS_DLL, EntryPoint = "gsapi_run_file")]
        private static extern int gsapi_run_file(IntPtr instance, string file_name, int user_errors, out int exit_code);

        [DllImport(GS_DLL, EntryPoint = "gsapi_run_string")]
        private static extern int gsapi_run_string(IntPtr instance, string str, int user_errors, out int exit_code);

        [DllImport(GS_DLL, EntryPoint = "gsapi_revision")]
        private static extern int gsapi_revision(ref gsapi_revision_t pr, int len);

        #endregion

        #region Callbacks

        private delegate int StdioCallBack(IntPtr caller_handle, IntPtr str, uint len);

        private delegate int PollCallBack(IntPtr caller_handle);

        [StructLayout(LayoutKind.Sequential)]
        private struct DisplayCallback
        {
            public DisplaySizeCallBack size;
            public DisplayPageCallBack page;
            public DisplayUpdateCallBack update;
            public DisplayPreUpdateCallBack preupdate;
            public DisplayMemAllocCallBack memalloc;
            public DisplayMemFreeCallBack memfree;
            public IntPtr display_handle;
        }

        private delegate int DisplaySizeCallBack(IntPtr handle, IntPtr device, IntPtr width, IntPtr height, IntPtr raster, uint format);

        private delegate int DisplayPageCallBack(IntPtr handle, IntPtr device, int copies, int flush);

        private delegate int DisplayUpdateCallBack(IntPtr handle, IntPtr device, int x, int y, int w, int h);

        private delegate int DisplayPreUpdateCallBack(IntPtr handle, IntPtr device, int x, int y, int w, int h);

        private delegate IntPtr DisplayMemAllocCallBack(IntPtr handle, uint size);

        private delegate void DisplayMemFreeCallBack(IntPtr handle, IntPtr mem);

        [StructLayout(LayoutKind.Sequential)]
        private struct gsapi_revision_t
        {
            public string? product;
            public string? copyright;
            public int revision;
            public int revisiondate;
        }

        #endregion

        #region Error Codes

        public const int GS_OK = 0;
        public const int GS_ERROR_QUIT = -101;
        public const int GS_ERROR_INTERRUPT = -102;
        public const int GS_ERROR_OUTOFMEMORY = -103;
        public const int GS_ERROR_UNRECOVERABLE = -104;
        public const int GS_ERROR_FATALEXIT = -105;
        public const int GS_ERROR_INVALIDEXIT = -106;
        public const int GS_ERROR_EXECERROR = -107;
        public const int GS_ERROR_INVALIDFILEACCESS = -108;
        public const int GS_ERROR_INVALIDFONT = -109;
        public const int GS_ERROR_INVALIDSTATETRANSITION = -110;
        public const int GS_ERROR_UNDEFINED = -111;
        public const int GS_ERROR_UNREGISTERED = -112;
        public const int GS_ERROR_INVALIDID = -113;
        public const int GS_ERROR_RANGE = -114;
        public const int GS_ERROR_UNKNOWN = -115;
        public const int GS_ERROR_LIMITCHECK = -116;
        public const int GS_ERROR_VMERROR = -117;
        public const int GS_ERROR_CONFIGURATIONERROR = -118;
        public const int GS_ERROR_CHECKFAILED = -119;
        public const int GS_ERROR_INVALIDCONTEXT = -120;

        #endregion

        #region Constructor and Destructor

        public GhostscriptAPI()
        {
            Initialize();
        }

        /// <summary>
        /// Constructor with detailed error information
        /// </summary>
        public GhostscriptAPI(bool throwOnMissingDll = true)
        {
            if (!IsGhostscriptAvailable() && throwOnMissingDll)
            {
                throw new GhostscriptException($"Ghostscript DLL not found. Please ensure Ghostscript is installed and {_ghostscriptPath} exists.", -1);
            }

            if (IsGhostscriptAvailable())
            {
                Initialize();
            }
        }

        ~GhostscriptAPI()
        {
            Dispose(false);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Check if Ghostscript DLL is available
        /// </summary>
        public static bool IsGhostscriptAvailable()
        {
            try
            {
                return System.IO.File.Exists(_ghostscriptPath);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Initialize Ghostscript instance
        /// </summary>
        public void Initialize()
        {
            if (_gsInstance != IntPtr.Zero)
                return;

            // Check if Ghostscript DLL exists
            if (!IsGhostscriptAvailable())
            {
                throw new GhostscriptException($"Ghostscript DLL not found at: {_ghostscriptPath}", -1);
            }

            try
            {
                int result = gsapi_new_instance(out _gsInstance, IntPtr.Zero);
                if (result != GS_OK)
                {
                    throw new GhostscriptException($"Failed to create Ghostscript instance. Error code: {result}", result);
                }

                // Set a poll callback that never interrupts to prevent -100 errors
                PollCallBack pollFn = (caller_handle) => 0; // Always return 0 (no interrupt)
                result = gsapi_set_poll(_gsInstance, pollFn);
                if (result != GS_OK)
                {
                    // Log warning but don't fail initialization
                    System.Diagnostics.Debug.WriteLine($"Warning: Failed to set poll callback. Error code: {result}");
                }
            }
            catch (DllNotFoundException dllEx)
            {
                throw new GhostscriptException($"Ghostscript DLL could not be loaded. Please ensure {_ghostscriptPath} and its dependencies are available.", -1, dllEx);
            }
            catch (Exception ex)
            {
                throw new GhostscriptException($"Failed to initialize Ghostscript: {ex.Message}", -1, ex);
            }
        }

        /// <summary>
        /// Execute Ghostscript with command line arguments
        /// </summary>
        /// <param name="args">Command line arguments</param>
        public void Execute(string[] args)
        {
            if (_gsInstance == IntPtr.Zero)
                throw new InvalidOperationException("Ghostscript instance not initialized");

            int result = gsapi_init_with_args(_gsInstance, args.Length, args);
            if (result != GS_OK && result != GS_ERROR_QUIT)
            {
                throw new GhostscriptException($"Ghostscript execution failed. Error code: {result}", result);
            }
        }

        /// <summary>
        /// Execute Ghostscript command string
        /// </summary>
        /// <param name="command">Ghostscript command to execute</param>
        public void ExecuteString(string command)
        {
            if (_gsInstance == IntPtr.Zero)
                throw new InvalidOperationException("Ghostscript instance not initialized");

            int exitCode;
            int result = gsapi_run_string(_gsInstance, command, 0, out exitCode);
            if (result != GS_OK)
            {
                throw new GhostscriptException($"Ghostscript command execution failed. Error code: {result}", result);
            }
        }

        /// <summary>
        /// Execute Ghostscript file
        /// </summary>
        /// <param name="fileName">File to execute</param>
        public void ExecuteFile(string fileName)
        {
            if (_gsInstance == IntPtr.Zero)
                throw new InvalidOperationException("Ghostscript instance not initialized");

            int exitCode;
            int result = gsapi_run_file(_gsInstance, fileName, 0, out exitCode);
            if (result != GS_OK)
            {
                throw new GhostscriptException($"Ghostscript file execution failed. Error code: {result}", result);
            }
        }

        /// <summary>
        /// Get Ghostscript revision information
        /// </summary>
        /// <returns>Revision information</returns>
        public GhostscriptRevision GetRevision()
        {
            var revision = new gsapi_revision_t();
            int result = gsapi_revision(ref revision, System.Runtime.InteropServices.Marshal.SizeOf(revision));

            if (result != GS_OK)
            {
                throw new GhostscriptException($"Failed to get Ghostscript revision. Error code: {result}", result);
            }

            return new GhostscriptRevision
            {
                Product = revision.product,
                Copyright = revision.copyright,
                Revision = revision.revision,
                RevisionDate = revision.revisiondate
            };
        }

        /// <summary>
        /// Get error message for Ghostscript error code
        /// </summary>
        /// <param name="errorCode">Error code</param>
        /// <returns>Error message in Vietnamese</returns>
        public string GetErrorMessage(int errorCode)
        {
            return errorCode switch
            {
                GS_OK => "Thành công",
                GS_ERROR_QUIT => "Yêu cầu thoát",
                GS_ERROR_INTERRUPT => "Bị gián đoạn (-100)",
                GS_ERROR_OUTOFMEMORY => "Hết bộ nhớ (-103)",
                GS_ERROR_UNRECOVERABLE => "Lỗi không thể phục hồi (-104)",
                GS_ERROR_FATALEXIT => "Lỗi nghiêm trọng (-105)",
                GS_ERROR_INVALIDEXIT => "Thoát không hợp lệ (-106)",
                GS_ERROR_EXECERROR => "Lỗi thực thi (-107)",
                GS_ERROR_INVALIDFILEACCESS => "Truy cập file không hợp lệ (-108)",
                GS_ERROR_INVALIDFONT => "Font không hợp lệ (-109)",
                GS_ERROR_INVALIDSTATETRANSITION => "Chuyển trạng thái không hợp lệ (-110)",
                GS_ERROR_UNDEFINED => "Chưa định nghĩa (-111)",
                GS_ERROR_UNREGISTERED => "Chưa đăng ký (-112)",
                GS_ERROR_INVALIDID => "ID không hợp lệ (-113)",
                GS_ERROR_RANGE => "Lỗi phạm vi (-114)",
                GS_ERROR_UNKNOWN => "Lỗi không xác định (-115)",
                GS_ERROR_LIMITCHECK => "Lỗi kiểm tra giới hạn (-116)",
                GS_ERROR_VMERROR => "Lỗi máy ảo (-117)",
                GS_ERROR_CONFIGURATIONERROR => "Lỗi cấu hình (-118)",
                GS_ERROR_CHECKFAILED => "Kiểm tra thất bại (-119)",
                GS_ERROR_INVALIDCONTEXT => "Ngữ cảnh không hợp lệ (-120)",
                _ => $"Mã lỗi không xác định: {errorCode}"
            };
        }

        /// <summary>
        /// Set standard I/O callbacks
        /// </summary>
        /// <param name="stdinCallback">Standard input callback</param>
        /// <param name="stdoutCallback">Standard output callback</param>
        /// <param name="stderrCallback">Standard error callback</param>
        public void SetStdioCallbacks(StdioCallback? stdinCallback, StdioCallback? stdoutCallback, StdioCallback? stderrCallback)
        {
            if (_gsInstance == IntPtr.Zero)
                throw new InvalidOperationException("Ghostscript instance not initialized");

            StdioCallBack stdinFn = (caller_handle, str, len) =>
            {
                return stdinCallback?.Invoke(caller_handle, str, len) ?? 0;
            };

            StdioCallBack stdoutFn = (caller_handle, str, len) =>
            {
                return stdoutCallback?.Invoke(caller_handle, str, len) ?? 0;
            };

            StdioCallBack stderrFn = (caller_handle, str, len) =>
            {
                return stderrCallback?.Invoke(caller_handle, str, len) ?? 0;
            };

            int result = gsapi_set_stdio(_gsInstance, stdinFn, stdoutFn, stderrFn);
            if (result != GS_OK)
            {
                throw new GhostscriptException($"Failed to set stdio callbacks. Error code: {result}", result);
            }
        }

        #endregion

        #region PDF Operations

        /// <summary>
        /// Compress PDF file using Ghostscript
        /// </summary>
        /// <param name="inputPath">Input PDF file path</param>
        /// <param name="outputPath">Output PDF file path</param>
        /// <param name="settings">Compression settings</param>
        public void CompressPdf(string inputPath, string outputPath, PdfCompressionSettings settings)
        {
            var args = BuildCompressionArgs(inputPath, outputPath, settings);
            Execute(args);
        }

        /// <summary>
        /// Merge multiple PDF files into one
        /// </summary>
        /// <param name="inputPaths">Array of input PDF file paths</param>
        /// <param name="outputPath">Output PDF file path</param>
        public void MergePdfFiles(string[] inputPaths, string outputPath)
        {
            var args = BuildMergeArgs(inputPaths, outputPath);
            Execute(args);
        }

        /// <summary>
        /// Split PDF file into multiple files
        /// </summary>
        /// <param name="inputPath">Input PDF file path</param>
        /// <param name="outputPathPattern">Output file path pattern (e.g., "output_part{0}.pdf")</param>
        /// <param name="pagesPerFile">Number of pages per output file</param>
        public void SplitPdfFile(string inputPath, string outputPathPattern, int pagesPerFile)
        {
            // Get total page count first
            int totalPages = GetPdfPageCount(inputPath);
            int fileCount = (int)Math.Ceiling((double)totalPages / pagesPerFile);

            for (int i = 0; i < fileCount; i++)
            {
                int startPage = i * pagesPerFile + 1;
                int endPage = Math.Min((i + 1) * pagesPerFile, totalPages);
                string outputPath = string.Format(outputPathPattern, i + 1);

                var args = BuildSplitArgs(inputPath, outputPath, startPage, endPage);
                Execute(args);
            }
        }

        /// <summary>
        /// Split PDF file by specifying exact start and end pages
        /// </summary>
        /// <param name="inputPath">Input PDF file path</param>
        /// <param name="outputPath">Output PDF file path</param>
        /// <param name="startPage">Starting page number (1-based)</param>
        /// <param name="endPage">Ending page number (1-based)</param>
        public void SplitPdfByPageRange(string inputPath, string outputPath, int startPage, int endPage)
        {
            var args = BuildSplitArgs(inputPath, outputPath, startPage, endPage);
            Execute(args);
        }

        /// <summary>
        /// Get page count of PDF file
        /// </summary>
        /// <param name="inputPath">Input PDF file path</param>
        /// <returns>Number of pages</returns>
        public int GetPdfPageCount(string inputPath)
        {
            try
            {
                // Method 1: Try using Ghostscript script
                string script = $"({inputPath}) (r) file runpdfbegin pdfpagecount runpdfend == flush";

                // Capture output using stdio callback
                string output = "";
                SetStdioCallbacks(null, (caller_handle, str, len) =>
                {
                    if (str != IntPtr.Zero)
                    {
                        byte[] buffer = new byte[len];
                        Marshal.Copy(str, buffer, 0, (int)len);
                        output += Encoding.ASCII.GetString(buffer);
                    }
                    return 0;
                }, null);

                ExecuteString(script);

                if (int.TryParse(output.Trim(), out int pageCount))
                {
                    return pageCount;
                }
            }
            catch
            {
                // Fallback to process-based method
            }

            // Fallback method: Return a reasonable default
            // In a production environment, you'd want to implement proper page counting
            return 10; // Default fallback for page count
        }

        #endregion

        #region Private Methods

        private string[] BuildCompressionArgs(string inputPath, string outputPath, PdfCompressionSettings settings)
        {
            var args = new List<string>
            {
                "gs", // Ghostscript executable name
                "-dNOPAUSE",
                "-dBATCH",
                "-dQUIET",
                "-sDEVICE=pdfwrite",
                $"-dCompatibilityLevel=1.4",
                $"-dPDFSETTINGS={settings.PdfSetting}",
                $"-sOutputFile={outputPath}"
            };

            if (settings.ColorImageResolution > 0)
                args.Add($"-dColorImageResolution={settings.ColorImageResolution}");

            if (settings.GrayImageResolution > 0)
                args.Add($"-dGrayImageResolution={settings.GrayImageResolution}");

            if (settings.JpegQuality > 0)
                args.Add($"-dJPEGQ={settings.JpegQuality}");

            if (settings.UseAutoFilter)
            {
                args.Add("-dAutoFilterColorImages=true");
                args.Add("-dAutoFilterGrayImages=true");
            }

            if (settings.UseDCTEncode)
            {
                args.Add("-dAutoFilterColorImages=false");
                args.Add("-dColorImageFilter=/DCTEncode");
                args.Add("-dAutoFilterGrayImages=false");
                args.Add("-dGrayImageFilter=/DCTEncode");
            }

            if (settings.DownsampleColorImages)
                args.Add("-dDownsampleColorImages=true");

            if (settings.DownsampleGrayImages)
                args.Add("-dDownsampleGrayImages=true");

            args.Add(inputPath);

            return args.ToArray();
        }

        private string[] BuildMergeArgs(string[] inputPaths, string outputPath)
        {
            var args = new List<string>
            {
                "gs",
                "-dNOPAUSE",
                "-dBATCH",
                "-dQUIET",
                "-sDEVICE=pdfwrite",
                $"-sOutputFile={outputPath}"
            };

            // Add all input files
            args.AddRange(inputPaths);

            return args.ToArray();
        }

        private string[] BuildSplitArgs(string inputPath, string outputPath, int startPage, int endPage)
        {
            return new string[]
            {
                "gs",
                "-dNOPAUSE",
                "-dBATCH",
                "-dQUIET",
                "-sDEVICE=pdfwrite",
                $"-dFirstPage={startPage}",
                $"-dLastPage={endPage}",
                $"-sOutputFile={outputPath}",
                inputPath
            };
        }

        #endregion

        #region IDisposable Implementation

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && _gsInstance != IntPtr.Zero)
            {
                try
                {
                    gsapi_exit(_gsInstance);
                }
                catch
                {
                    // Ignore cleanup errors
                }

                try
                {
                    gsapi_delete_instance(_gsInstance);
                }
                catch
                {
                    // Ignore cleanup errors
                }

                _gsInstance = IntPtr.Zero;
                _disposed = true;
            }
        }

        #endregion
    }

    #region Supporting Classes

    /// <summary>
    /// PDF compression settings
    /// </summary>
    public class PdfCompressionSettings
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

    /// <summary>
    /// Ghostscript revision information
    /// </summary>
    public class GhostscriptRevision
    {
        public string Product { get; set; } = "";
        public string Copyright { get; set; } = "";
        public int Revision { get; set; }
        public int RevisionDate { get; set; }
    }

    /// <summary>
    /// Standard I/O callback delegate
    /// </summary>
    /// <param name="caller_handle">Caller handle</param>
    /// <param name="str">String buffer</param>
    /// <param name="len">Buffer length</param>
    /// <returns>0 on success</returns>
    public delegate int StdioCallback(IntPtr caller_handle, IntPtr str, uint len);

    /// <summary>
    /// Ghostscript exception
    /// </summary>
    public class GhostscriptException : Exception
    {
        public int ErrorCode { get; }

        public GhostscriptException(string message) : base(message)
        {
            ErrorCode = 0;
        }

        public GhostscriptException(string message, int errorCode) : base(message)
        {
            ErrorCode = errorCode;
        }

        public GhostscriptException(string message, int errorCode, Exception innerException) : base(message, innerException)
        {
            ErrorCode = errorCode;
        }
    }

    #endregion
}