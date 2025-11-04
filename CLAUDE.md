# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a C# Windows Forms application (PDF Compressor) that compresses PDF files using Ghostscript. The application provides a simple GUI with file selection and compression capabilities.

## Development Commands

### Build and Run
```bash
# Build the project
dotnet build

# Run the application
dotnet run

# Build for release
dotnet build -c Release

# Clean build artifacts
dotnet clean
```

### Logging
- All operations are logged to `Logs/PDFCompressor_YYYY-MM-DD.log`
- Logs include timestamps and detailed operation information
- Log files are created automatically in the application directory

### Testing
No test framework is currently configured in this project.

## Architecture

### Core Components

- **Program.cs**: Entry point for the Windows Forms application
- **MainForm.cs**: Main application form containing:
  - File selection dialog integration with auto-naming (.compress.pdf suffix)
  - Multi-threaded PDF compression logic using Ghostscript
  - Real-time logging and progress tracking
  - Advanced compression settings for scanned documents
  - UI event handlers with thread-safe operations
- **MainForm.Designer.cs**: Auto-generated Windows Forms UI layout code

### Key Dependencies

- **.NET 9.0 Windows Forms**: Target framework for desktop application
- **Ghostscript**: External dependency for PDF processing (located in `/Ghostscript/` directory)
  - `gswin64c.exe`: Command-line Ghostscript executable
  - `gsdll64.dll`: Ghostscript library

### PDF Compression Logic

The application implements an intelligent compression strategy with automatic document analysis:

1. **Multi-threading**: Compression runs on background threads to prevent UI blocking
2. **Document Analysis**: Automatic detection of document types when files are loaded:
   - **Scanned Documents**: Detected by high image content, large file size, scan-related metadata
   - **Text Documents**: Detected by high text operator count and multiple fonts
   - **Mixed Content**: Documents with both text and images
   - **General Documents**: Default category for other types
3. **Intelligent Compression**: Smart selection of optimal Ghostscript parameters based on document type:
   - **Scanned Documents**: `/screen` setting, aggressive image downsampling (150-300 DPI), JPEG encoding
   - **Text Documents**: `/ebook` setting, preserve image quality (300 DPI), auto-filtering enabled
   - **Mixed Content**: `/printer` setting, balanced compression (200 DPI), optimized for both text and images
   - **General Documents**: `/default` setting with standard parameters
4. **Manual Override Options**:
   - Auto (Best): Uses intelligent analysis for optimal settings
   - Screen/Ebook/Printer/Prepress: Manual profile selection bypassing analysis
   - Image quality slider (10-100%): Fine-tunes JPEG compression
   - Scanned document optimization toggle: Additional optimization for scanned content
5. **File Splitting**: For compressed files exceeding size limit:
   - Automatic detection of large files (>10MB or user-defined threshold)
   - Splits PDF into multiple parts using Ghostscript page range extraction
   - Generates files with naming pattern: filename.part1.pdf, filename.part2.pdf, etc.
6. **Advanced Ghostscript Integration**:
   - Intelligent parameter selection based on document analysis
   - Custom resolution settings for color and grayscale images
   - Advanced filtering (DCTEncode for JPEG compression)
   - Font optimization for screen-quality settings
7. **Progress Tracking**: Real-time progress bar and detailed logging for both compression and splitting
8. **Size Analysis**: Shows before/after file sizes and compression ratio

### File Structure

```
PdfCompressor/
├── Program.cs                    # Application entry point
├── MainForm.cs                  # Main form logic and compression
├── MainForm.Designer.cs         # Auto-generated UI code
├── GhostscriptAPI.cs            # Ghostscript DLL wrapper
├── PdfCompressor.csproj         # Project configuration
├── Ghostscript/                 # Ghostscript binaries
│   ├── gswin64c.exe            # Ghostscript CLI
│   ├── gswin64.exe             # Ghostscript GUI
│   ├── gsdll64.dll             # Ghostscript library
│   └── gsdll64.lib             # Ghostscript import library
├── bin/                        # Build output directory
└── obj/                        # Build artifacts directory
```

## Important Implementation Details

### Ghostscript Integration
- The application expects Ghostscript binaries to be in a `Ghostscript/` subdirectory
- Uses `gswin64c.exe` for command-line processing
- Process execution is configured with `CreateNoWindow = true` and `UseShellExecute = false`

### Error Handling
- Thread-safe error handling with proper exception management
- Detailed logging system for debugging and monitoring
- File validation before processing
- Graceful handling of Ghostscript execution failures
- Progress bar and UI updates handled via thread-safe invokes
- Folder opening functionality with error handling and fallbacks

### UI Design
- **Tabbed Interface**: Two tabs for better organization
  - **Chương trình Tab**: Main compression interface
  - **Thông tin Tab**: Author and application information
- **Vietnamese Language**: Complete interface in Vietnamese for better user experience
- **Main Controls**: File selection and compression buttons with progress indication
- **Log Window**: Real-time logging of compression process with timestamps
- **Settings Panel**:
  - Compression type dropdown (Tự động/Screen/Ebook/Printer/Prepress)
  - Image quality slider (10-100%)
  - Scanned document optimization checkbox
- **File Information & Options Panel**:
  - Document Type display: Shows automatically detected document type (Tài liệu scan/Tài liệu văn bản/Nội dung hỗn hợp/Tài liệu chung)
  - File Splitting checkbox: Splits large files (>10MB) into smaller chunks
  - Split Size input: Configurable maximum file size for splitting (default 5MB)
- **Progress Tracking**: Visual progress bar during compression and splitting
- **Auto-naming**: Automatically suggests output filename with `.compress.pdf` suffix
- **File Information**: Displays file sizes, compression ratios, and detected document type
- **Smart Logging**: Shows document analysis results and compression parameters used
- **Result Dialogs**: Post-compression dialogs with option to open output folder in Windows Explorer
- **About Tab**: Displays author information (Nguyễn Ngọc Tú, tunn1@bidv.com.vn, 0983862402)