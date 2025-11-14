using NLog;
using NLog.Config;
using System;
using System.IO;

namespace PdfCompressor
{
    /// <summary>
    /// Centralized logger using NLog for shared folder scenarios
    /// </summary>
    public static class Logger
    {
        private static readonly NLog.Logger _logger = LogManager.GetCurrentClassLogger();
        private static readonly NLog.Logger _mainFormLogger = LogManager.GetLogger("MainForm");
        private static readonly NLog.Logger _ghostscriptApiLogger = LogManager.GetLogger("GhostscriptAPI");

        /// <summary>
        /// Initialize NLog configuration
        /// </summary>
        public static void Initialize()
        {
            try
            {
                // Try to load config file first
                var configFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NLog.config");
                if (File.Exists(configFile))
                {
                    LogManager.Configuration = new XmlLoggingConfiguration(configFile);
                    _mainFormLogger.Info("NLog configuration loaded from file");
                }
                else
                {
                    // Fallback to programmatic configuration
                    ConfigureProgrammatically();
                    _mainFormLogger.Info("NLog configured programmatically");
                }

                _mainFormLogger.Info($"PDFCompressor started - User: {Environment.UserName}, Computer: {Environment.MachineName}");
                _mainFormLogger.Info($"Log directory: {Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs")}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize NLog: {ex.Message}");
                // Fallback to basic console logging
                ConfigureMinimal();
            }
        }

        /// <summary>
        /// Configure NLog programmatically (fallback when config file is missing)
        /// </summary>
        private static void ConfigureProgrammatically()
        {
            var config = new LoggingConfiguration();

            // File target with user-specific naming for shared folder safety
            var fileTarget = new NLog.Targets.FileTarget("fileTarget")
            {
                FileName = "${basedir}/Logs/PDFCompressor_" + Environment.UserName + "_" + Environment.MachineName + "_${shortdate}.log",
                Layout = "${longdate} [${level:uppercase=true}] [${logger}] ${message} ${exception:format=tostring}",
                ArchiveFileName = "${basedir}/Logs/archive/PDFCompressor_" + Environment.UserName + "_" + Environment.MachineName + "_{#}.log",
                ArchiveSuffixFormat = "{#}",
                ArchiveEvery = NLog.Targets.FileArchivePeriod.Day,
                MaxArchiveFiles = 7,
                KeepFileOpen = false,
                Encoding = System.Text.Encoding.UTF8,
                CreateDirs = true
            };

            // Async wrapper for better performance and lock prevention
            var asyncFileTarget = new NLog.Targets.Wrappers.AsyncTargetWrapper(fileTarget, 1000, NLog.Targets.Wrappers.AsyncTargetWrapperOverflowAction.Discard);

            // Console target
            var consoleTarget = new NLog.Targets.ConsoleTarget("consoleTarget")
            {
                Layout = "${time} [${level:uppercase=true}] [${logger}] ${message}"
            };

            // Add targets
            config.AddTarget(asyncFileTarget);
            config.AddTarget(consoleTarget);

            // Add rules
            config.AddRule(LogLevel.Info, LogLevel.Fatal, asyncFileTarget);
            config.AddRule(LogLevel.Debug, LogLevel.Fatal, consoleTarget);

            LogManager.Configuration = config;
        }

        /// <summary>
        /// Minimal configuration as fallback
        /// </summary>
        private static void ConfigureMinimal()
        {
            var config = new LoggingConfiguration();
            var consoleTarget = new NLog.Targets.ConsoleTarget("console")
            {
                Layout = "${time} [${level:uppercase=true}] ${message}"
            };
            config.AddTarget(consoleTarget);
            config.AddRule(LogLevel.Info, LogLevel.Fatal, consoleTarget);
            LogManager.Configuration = config;
        }

        #region MainForm Logging Methods

        /// <summary>
        /// Log message from MainForm
        /// </summary>
        public static void LogMainForm(string message, LogLevel level)
        {
            try
            {
                _mainFormLogger.Log(level, message);
            }
            catch
            {
                // Fallback to console
                Console.WriteLine($"[MainForm] [{level}] {message}");
            }
        }

        /// <summary>
        /// Log info message from MainForm (overload)
        /// </summary>
        public static void LogMainForm(string message)
        {
            LogMainForm(message, LogLevel.Info);
        }

        /// <summary>
        /// Log debug message from MainForm
        /// </summary>
        public static void DebugMainForm(string message)
        {
            LogMainForm(message, LogLevel.Debug);
        }

        /// <summary>
        /// Log info message from MainForm
        /// </summary>
        public static void InfoMainForm(string message)
        {
            LogMainForm(message, LogLevel.Info);
        }

        /// <summary>
        /// Log warning message from MainForm
        /// </summary>
        public static void WarnMainForm(string message)
        {
            LogMainForm(message, LogLevel.Warn);
        }

        /// <summary>
        /// Log error message from MainForm
        /// </summary>
        public static void ErrorMainForm(string message, Exception? ex = null)
        {
            if (ex != null)
                _mainFormLogger.Error(ex, message);
            else
                _mainFormLogger.Error(message);
        }

        #endregion

        #region GhostscriptAPI Logging Methods

        /// <summary>
        /// Log message from GhostscriptAPI
        /// </summary>
        public static void LogGhostscriptAPI(string message, LogLevel level)
        {
            try
            {
                _ghostscriptApiLogger.Log(level, message);
            }
            catch
            {
                // Fallback to console
                Console.WriteLine($"[GhostscriptAPI] [{level}] {message}");
            }
        }

        /// <summary>
        /// Log info message from GhostscriptAPI (overload)
        /// </summary>
        public static void LogGhostscriptAPI(string message)
        {
            LogGhostscriptAPI(message, LogLevel.Info);
        }

        /// <summary>
        /// Log debug message from GhostscriptAPI
        /// </summary>
        public static void DebugGhostscript(string message)
        {
            LogGhostscriptAPI(message, LogLevel.Debug);
        }

        /// <summary>
        /// Log info message from GhostscriptAPI
        /// </summary>
        public static void InfoGhostscript(string message)
        {
            LogGhostscriptAPI(message, LogLevel.Info);
        }

        /// <summary>
        /// Log warning message from GhostscriptAPI
        /// </summary>
        public static void WarnGhostscript(string message)
        {
            LogGhostscriptAPI(message, LogLevel.Warn);
        }

        /// <summary>
        /// Log error message from GhostscriptAPI
        /// </summary>
        public static void ErrorGhostscript(string message, Exception? ex = null)
        {
            if (ex != null)
                _ghostscriptApiLogger.Error(ex, message);
            else
                _ghostscriptApiLogger.Error(message);
        }

        /// <summary>
        /// Log Ghostscript operation start
        /// </summary>
        public static void LogGhostscriptOperationStart(string operation, string? details = null)
        {
            string message = $"Bắt đầu {operation}";
            if (!string.IsNullOrEmpty(details))
                message += $": {details}";
            InfoGhostscript(message);
        }

        /// <summary>
        /// Log Ghostscript operation success
        /// </summary>
        public static void LogGhostscriptOperationSuccess(string operation, string? details = null)
        {
            string message = $"Hoàn thành {operation} thành công";
            if (!string.IsNullOrEmpty(details))
                message += $": {details}";
            InfoGhostscript(message);
        }

        /// <summary>
        /// Log Ghostscript error with Vietnamese error message
        /// </summary>
        public static void LogGhostscriptError(string operation, int errorCode, string? additionalInfo = null)
        {
            try
            {
                var gsApi = new GhostscriptAPI(false);
                string errorMessage = gsApi.GetErrorMessage(errorCode);

                string message = $"Lỗi Ghostscript trong '{operation}': {errorMessage} (Mã: {errorCode})";
                if (!string.IsNullOrEmpty(additionalInfo))
                    message += $" - Chi tiết: {additionalInfo}";

                ErrorGhostscript(message);
            }
            catch
            {
                ErrorGhostscript($"Lỗi Ghostscript trong '{operation}': Mã {errorCode}");
            }
        }

        #endregion

        /// <summary>
        /// Flush all pending log messages
        /// </summary>
        public static void Flush()
        {
            try
            {
                LogManager.Flush();
            }
            catch
            {
                // Ignore flush errors
            }
        }

        /// <summary>
        /// Shutdown NLog
        /// </summary>
        public static void Shutdown()
        {
            try
            {
                _mainFormLogger.Info("PDFCompressor shutting down");
                LogManager.Shutdown();
            }
            catch
            {
                // Ignore shutdown errors
            }
        }
    }
}