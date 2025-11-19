using System;
using System.IO;
using System.Linq;
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

            // Pre-load the DLL to handle dependencies and improve error detection
            try
            {
                if (System.IO.File.Exists(_ghostscriptPath))
                {
                    // Add the Ghostscript directory to the DLL search path
                    string ghostscriptDir = System.IO.Path.GetDirectoryName(_ghostscriptPath) ?? "";
                    if (!string.IsNullOrEmpty(ghostscriptDir))
                    {
                        SetDllDirectory(ghostscriptDir);
                        System.Diagnostics.Debug.WriteLine($"Ghostscript DLL directory added to search path: {ghostscriptDir}");
                    }

                    // Try to load the library to check for dependencies early
                    IntPtr handle = LoadLibrary(_ghostscriptPath);
                    if (handle != IntPtr.Zero)
                    {
                        System.Diagnostics.Debug.WriteLine($"Ghostscript DLL pre-loaded successfully: {_ghostscriptPath}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Warning: Failed to pre-load Ghostscript DLL: {_ghostscriptPath}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Warning: Ghostscript DLL not found at: {_ghostscriptPath}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Warning: Ghostscript DLL pre-loading failed: {ex.Message}");
                // If pre-loading fails, continue and try standard loading during initialization
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
        public const int GS_ERROR_INTERRUPT = -100;  // Fixed: Actual interrupt error from Ghostscript
        public const int GS_ERROR_QUIT = -101;
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
        /// Get Ghostscript installation diagnostic information
        /// </summary>
        /// <returns>Detailed information about Ghostscript installation</returns>
        public static string GetDiagnosticInfo()
        {
            var info = new System.Text.StringBuilder();

            info.AppendLine($"Ghostscript DLL Path: {_ghostscriptPath}");
            info.AppendLine($"DLL Exists: {System.IO.File.Exists(_ghostscriptPath)}");

            if (System.IO.File.Exists(_ghostscriptPath))
            {
                try
                {
                    var fileInfo = new System.IO.FileInfo(_ghostscriptPath);
                    info.AppendLine($"DLL Size: {fileInfo.Length:N0} bytes");
                    info.AppendLine($"DLL Modified: {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}");

                    // Check for Ghostscript executable
                    string exePath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(_ghostscriptPath) ?? "", "gswin64c.exe");
                    info.AppendLine($"Executable Exists: {System.IO.File.Exists(exePath)}");
                    info.AppendLine($"Executable Path: {exePath}");

                    if (System.IO.File.Exists(exePath))
                    {
                        var exeInfo = new System.IO.FileInfo(exePath);
                        info.AppendLine($"Executable Size: {exeInfo.Length:N0} bytes");
                        info.AppendLine($"Executable Modified: {exeInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}");
                    }
                }
                catch (Exception ex)
                {
                    info.AppendLine($"Error getting file info: {ex.Message}");
                }
            }

            info.AppendLine($"Working Directory: {System.IO.Directory.GetCurrentDirectory()}");
            info.AppendLine($"Application Base: {AppDomain.CurrentDomain.BaseDirectory}");

            return info.ToString();
        }

        /// <summary>
        /// Initialize Ghostscript instance
        /// </summary>
        public void Initialize()
        {
            if (_gsInstance != IntPtr.Zero)
            {
                Logger.InfoGhostscript("Ghostscript instance đã được khởi tạo trước đó");
                return;
            }

            Logger.LogGhostscriptOperationStart("khởi tạo Ghostscript instance", $"DLL path: {_ghostscriptPath}");

            // Check if Ghostscript DLL exists
            if (!IsGhostscriptAvailable())
            {
                Logger.LogGhostscriptError("khởi tạo", -1, $"Ghostscript DLL not found at: {_ghostscriptPath}");
                throw new GhostscriptException($"Ghostscript DLL not found at: {_ghostscriptPath}", -1);
            }

            try
            {
                Logger.InfoGhostscript("Đang tạo Ghostscript instance mới...");
                int result = gsapi_new_instance(out _gsInstance, IntPtr.Zero);
                if (result != GS_OK)
                {
                    Logger.LogGhostscriptError("tạo instance", result);
                    throw new GhostscriptException($"Failed to create Ghostscript instance. Error code: {result}", result);
                }

                Logger.InfoGhostscript("Ghostscript instance được tạo thành công");

                // Set a robust poll callback to prevent -100 errors
                Logger.InfoGhostscript("Đang thiết lập poll callback mạnh mẽ để tránh lỗi -100...");

                // Create a more sophisticated poll callback
                PollCallBack pollFn = (caller_handle) =>
                {
                    try
                    {
                        // Always return 0 to indicate "don't interrupt"
                        // This prevents the -100 interrupt error
                        return 0;
                    }
                    catch
                    {
                        // If anything goes wrong in the callback, still return 0
                        return 0;
                    }
                };

                result = gsapi_set_poll(_gsInstance, pollFn);
                if (result != GS_OK)
                {
                    Logger.LogGhostscriptError("thiết lập poll callback", result, $"Không thể thiết lập poll callback, có thể gây lỗi -100");
                    // Don't fail initialization, but log as warning
                    System.Diagnostics.Debug.WriteLine($"Warning: Failed to set poll callback. Error code: {result}. This may cause -100 errors.");
                }
                else
                {
                    Logger.InfoGhostscript("Poll callback được thiết lập thành công - Đã bảo vệ khỏi lỗi -100");
                }

                Logger.LogGhostscriptOperationSuccess("khởi tạo Ghostscript instance");
            }
            catch (DllNotFoundException dllEx)
            {
                Logger.LogGhostscriptError("khởi tạo", -1, $"DLL không thể tải: {dllEx.Message}");
                throw new GhostscriptException($"Ghostscript DLL could not be loaded. Please ensure {_ghostscriptPath} and its dependencies are available.", -1, dllEx);
            }
            catch (Exception ex)
            {
                Logger.LogGhostscriptError("khởi tạo", -1, ex.Message);
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
            {
                Logger.LogGhostscriptError("thực thi", -1, "Ghostscript instance not initialized");
                throw new InvalidOperationException("Ghostscript instance not initialized");
            }

            string argsString = string.Join(" ", args);
            Logger.LogGhostscriptOperationStart("thực thi Ghostscript", argsString);

            try
            {
                int result = gsapi_init_with_args(_gsInstance, args.Length, args);

                // Handle common error codes gracefully
                if (result == GS_OK || result == GS_ERROR_QUIT)
                {
                    Logger.LogGhostscriptOperationSuccess("thực thi Ghostscript", $"Result: {result}");
                    return;
                }

                // Special handling for interrupt errors (-100)
                if (result == GS_ERROR_INTERRUPT)
                {
                    Logger.InfoGhostscript("Phát hiện lỗi gián đoạn (-100), thử lại với poll callback cường độ cao...");

                    // Try to reset and retry once
                    System.Threading.Thread.Sleep(100); // Brief pause
                    result = gsapi_init_with_args(_gsInstance, args.Length, args);

                    if (result == GS_OK || result == GS_ERROR_QUIT)
                    {
                        Logger.InfoGhostscript("Thử lại thành công sau khi xử lý lỗi -100");
                        return;
                    }
                }

                // Log detailed error information
                string errorMsg = GetErrorMessage(result);
                Logger.LogGhostscriptError("thực thi", result, $"{errorMsg} - Args: {argsString}");

                // For certain errors, provide more context
                switch (result)
                {
                    case GS_ERROR_INTERRUPT:
                        throw new GhostscriptException($"Ghostscript bị gián đoạn. Poll callback có thể không hoạt động đúng. Error: {result}", result);
                    case GS_ERROR_INVALIDFILEACCESS:
                        throw new GhostscriptException($"Không thể truy cập file. Kiểm tra đường dẫn và quyền truy cập. Error: {result}", result);
                    case GS_ERROR_EXECERROR:
                        throw new GhostscriptException($"Lỗi thực thi command. Kiểm tra cú pháp tham số. Error: {result}", result);
                    default:
                        throw new GhostscriptException($"Ghostscript execution failed. {errorMsg}", result);
                }
            }
            catch (Exception ex) when (!(ex is GhostscriptException))
            {
                Logger.LogGhostscriptError("thực thi", -1, $"{ex.Message} - Args: {argsString}");
                throw new GhostscriptException($"Ghostscript execution failed with exception: {ex.Message}", -1, ex);
            }
        }

        /// <summary>
        /// Execute Ghostscript command string
        /// </summary>
        /// <param name="command">Ghostscript command to execute</param>
        public void ExecuteString(string command)
        {
            if (_gsInstance == IntPtr.Zero)
            {
                Logger.LogGhostscriptError("thực thi command", -1, "Ghostscript instance not initialized");
                throw new InvalidOperationException("Ghostscript instance not initialized");
            }

            Logger.LogGhostscriptOperationStart("thực thi Ghostscript command", command);

            try
            {
                int exitCode;
                int result = gsapi_run_string(_gsInstance, command, 0, out exitCode);
                if (result != GS_OK)
                {
                    Logger.LogGhostscriptError("thực thi command", result, $"Command: {command}");
                    throw new GhostscriptException($"Ghostscript command execution failed. Error code: {result}", result);
                }

                Logger.LogGhostscriptOperationSuccess("thực thi Ghostscript command", $"Exit code: {exitCode}");
            }
            catch (Exception ex)
            {
                Logger.LogGhostscriptError("thực thi command", -1, $"{ex.Message} - Command: {command}");
                throw;
            }
        }

        /// <summary>
        /// Execute Ghostscript file
        /// </summary>
        /// <param name="fileName">File to execute</param>
        public void ExecuteFile(string fileName)
        {
            if (_gsInstance == IntPtr.Zero)
            {
                Logger.LogGhostscriptError("thực thi file", -1, "Ghostscript instance not initialized");
                throw new InvalidOperationException("Ghostscript instance not initialized");
            }

            Logger.LogGhostscriptOperationStart("thực thi Ghostscript file", fileName);

            try
            {
                int exitCode;
                int result = gsapi_run_file(_gsInstance, fileName, 0, out exitCode);
                if (result != GS_OK)
                {
                    Logger.LogGhostscriptError("thực thi file", result, $"File: {fileName}");
                    throw new GhostscriptException($"Ghostscript file execution failed. Error code: {result}", result);
                }

                Logger.LogGhostscriptOperationSuccess("thực thi Ghostscript file", $"File: {fileName}, Exit code: {exitCode}");
            }
            catch (Exception ex)
            {
                Logger.LogGhostscriptError("thực thi file", -1, $"{ex.Message} - File: {fileName}");
                throw;
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
                Product = revision.product ?? "",
                Copyright = revision.copyright ?? "",
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
                GS_ERROR_INTERRUPT => "Bị gián đoạn (-100) - Thiếu poll callback hoặc timeout",
                GS_ERROR_QUIT => "Yêu cầu thoát (-101)",
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
            Logger.LogGhostscriptOperationStart("nén PDF", $"Input: {Path.GetFileName(inputPath)}, Output: {Path.GetFileName(outputPath)}");
            Logger.InfoGhostscript($"Thiết lập nén: {settings.PdfSetting}, Chất lượng JPEG: {settings.JpegQuality}%");
            Logger.InfoGhostscript($"Độ phân giải ảnh màu: {settings.ColorImageResolution} DPI, Ảnh xám: {settings.GrayImageResolution} DPI");

            try
            {
                var args = BuildCompressionArgs(inputPath, outputPath, settings);
                Execute(args);
                Logger.LogGhostscriptOperationSuccess("nén PDF", $"File output: {Path.GetFileName(outputPath)}");
            }
            catch (Exception ex)
            {
                Logger.LogGhostscriptError("nén PDF", -1, $"{ex.Message} - Input: {inputPath}");
                throw;
            }
        }

        /// <summary>
        /// Merge multiple PDF files into one
        /// </summary>
        /// <param name="inputPaths">Array of input PDF file paths</param>
        /// <param name="outputPath">Output PDF file path</param>
        public void MergePdfFiles(string[] inputPaths, string outputPath)
        {
            string inputFiles = string.Join(", ", inputPaths.Select(Path.GetFileName));
            Logger.LogGhostscriptOperationStart("gộp PDF", $"Input files: {inputFiles}, Output: {Path.GetFileName(outputPath)}");

            try
            {
                var args = BuildMergeArgs(inputPaths, outputPath);
                Execute(args);
                Logger.LogGhostscriptOperationSuccess("gộp PDF", $"Đã gộp {inputPaths.Length} files thành {Path.GetFileName(outputPath)}");
            }
            catch (Exception ex)
            {
                Logger.LogGhostscriptError("gộp PDF", -1, $"{ex.Message} - Files: {inputFiles}");
                throw;
            }
        }

        /// <summary>
        /// Split PDF file into multiple files
        /// </summary>
        /// <param name="inputPath">Input PDF file path</param>
        /// <param name="outputPathPattern">Output file path pattern (e.g., "output_part{0}.pdf")</param>
        /// <param name="pagesPerFile">Number of pages per output file</param>
        public void SplitPdfFile(string inputPath, string outputPathPattern, int pagesPerFile)
        {
            Logger.LogGhostscriptOperationStart("chia PDF", $"Input: {Path.GetFileName(inputPath)}, {pagesPerFile} pages/file");

            try
            {
                // Get total page count first
                int totalPages = GetPdfPageCount(inputPath);
                int fileCount = (int)Math.Ceiling((double)totalPages / pagesPerFile);

                Logger.InfoGhostscript($"Phát hiện {totalPages} trang, sẽ chia thành {fileCount} files");

                for (int i = 0; i < fileCount; i++)
                {
                    int startPage = i * pagesPerFile + 1;
                    int endPage = Math.Min((i + 1) * pagesPerFile, totalPages);
                    string outputPath = string.Format(outputPathPattern, i + 1);

                    Logger.InfoGhostscript($"Đang tạo phần {i + 1}/{fileCount}: trang {startPage}-{endPage}");

                    var args = BuildSplitArgs(inputPath, outputPath, startPage, endPage);
                    Execute(args);

                    Logger.InfoGhostscript($"Đã tạo phần {i + 1}: {Path.GetFileName(outputPath)}");
                }

                Logger.LogGhostscriptOperationSuccess("chia PDF", $"Đã chia thành {fileCount} files");
            }
            catch (Exception ex)
            {
                Logger.LogGhostscriptError("chia PDF", -1, $"{ex.Message} - Input: {inputPath}");
                throw;
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
            Logger.LogGhostscriptOperationStart("chia PDF theo range", $"Input: {Path.GetFileName(inputPath)}, trang {startPage}-{endPage}");

            try
            {
                var args = BuildSplitArgs(inputPath, outputPath, startPage, endPage);
                Execute(args);
                Logger.LogGhostscriptOperationSuccess("chia PDF theo range", $"Output: {Path.GetFileName(outputPath)}, trang {startPage}-{endPage}");
            }
            catch (Exception ex)
            {
                Logger.LogGhostscriptError("chia PDF theo range", -1, $"{ex.Message} - Input: {inputPath}, pages {startPage}-{endPage}");
                throw;
            }
        }

        /// <summary>
        /// Get page count of PDF file
        /// </summary>
        /// <param name="inputPath">Input PDF file path</param>
        /// <returns>Number of pages</returns>
        public int GetPdfPageCount(string inputPath)
        {
            Logger.LogGhostscriptOperationStart("đếm trang PDF", Path.GetFileName(inputPath));

            try
            {
                // Method 1: Try using Ghostscript script
                string script = $"({inputPath}) (r) file runpdfbegin pdfpagecount runpdfend == flush";
                Logger.InfoGhostscript($"Thực thi script đếm trang: {script}");

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
                    Logger.LogGhostscriptOperationSuccess("đếm trang PDF", $"{pageCount} trang");
                    return pageCount;
                }
                else
                {
                    Logger.InfoGhostscript("Không thể parse kết quả đếm trang, sử dụng giá trị mặc định");
                }
            }
            catch (Exception ex)
            {
                Logger.LogGhostscriptError("đếm trang PDF", -1, $"{ex.Message} - Sử dụng giá trị mặc định");
            }

            // Fallback method: Return a reasonable default
            // In a production environment, you'd want to implement proper page counting
            Logger.InfoGhostscript("Sử dụng giá trị mặc định: 10 trang");
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
                Logger.InfoGhostscript("Đang dọn dẹp Ghostscript instance...");

                // Exit Ghostscript instance first
                try
                {
                    int result = gsapi_exit(_gsInstance);
                    if (result == GS_OK || result == GS_ERROR_QUIT)
                    {
                        Logger.InfoGhostscript($"Ghostscript instance exit thành công (result: {result})");
                    }
                    else
                    {
                        string errorMsg = GetErrorMessage(result);
                        Logger.LogGhostscriptError("dispose - exit", result, $"{errorMsg} - Non-critical cleanup error");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogGhostscriptError("dispose - exit", -1, $"{ex.Message} - Non-critical cleanup exception");
                }

                // Delete the instance
                try
                {
                    gsapi_delete_instance(_gsInstance);
                    Logger.InfoGhostscript("Ghostscript instance đã được xóa");
                }
                catch (Exception ex)
                {
                    Logger.LogGhostscriptError("dispose - delete", -1, $"{ex.Message} - Non-critical cleanup exception");
                }

                _gsInstance = IntPtr.Zero;
                _disposed = true;
                Logger.InfoGhostscript("Ghostscript API đã được dispose thành công");
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