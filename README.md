# PDF Compressor - Tối ưu hóa file PDF

Ứng dụng Windows Forms giúp nén và tối ưu hóa file PDF sử dụng Ghostscript với giao diện tiếng Việt thân thiện.

## 📋 Mục lục

- [Tính năng](#tính-năng)
- [Yêu cầu hệ thống](#yêu-cầu-hệ-thống)
- [Cài đặt](#cài-đặt)
- [Sử dụng](#sử-dụng)
- [Tính năng chi tiết](#tính-năng-chi-tiết)
- [Troubleshooting](#troubleshooting)
- [Thông tin tác giả](#thông-tin-tác-giả)

## ✨ Tính năng

- 🧠 **Phân tích tài liệu thông minh**: Tự động nhận dạng tài liệu scan, văn bản, hoặc nội dung hỗn hợp
- 🎯 **Tối ưu hóa thông minh**: Chọn tham số nén phù hợp nhất dựa trên loại tài liệu
- 📁 **Chia nhỏ file lớn**: Tự động chia file >10MB thành các phần nhỏ hơn
- 🌐 **Giao diện tiếng Việt**: Hoàn toàn bằng tiếng Việt, dễ sử dụng
- 📊 **Logging nâng cao với NLog**: Hệ thống logging chuyên nghiệp, hỗ trợ multi-user
- 👥 **Multi-user Support**: Hoạt động tốt trên thư mục share cho nhiều người dùng
- 🔄 **Async Logging**: Không bị lock file, performance cao
- 📂 **Mở thư mục kết quả**: Hỏi người dùng có muốn mở thư mục chứa file kết quả
- 🎛️ **Tab interface**: Giao diện chuyên nghiệp với 2 tabs

## 💻 Yêu cầu hệ thống

- **Hệ điều hành**: Windows 10/11 (x64)
- **.NET Runtime**: .NET 9.0 Runtime
- **Ghostscript**: Phiên bản 9.x hoặc mới hơn (đã bao gồm)
- **RAM**: Tối thiểu 4GB
- **Disk space**: 100MB cho ứng dụng + không gian cho file PDF

## 🚀 Cài đặt

### Cách 1: Download bản đã build

1. Download file ZIP từ releases
2. Giải nén vào thư mục mong muốn
3. **Quan trọng**: Đảm bảo quyền ghi vào thư mục để tạo log files
4. Chạy `PDFCompressor.exe`
5. **Lần đầu chạy**: Tự động tạo thư mục `Logs/` và file `NLog.config`

### Cách 2: Build từ source code

```bash
# Clone repository
git clone <repository-url>
cd PdfCompressor

# Build ứng dụng
dotnet build --configuration Release

# Chạy ứng dụng
dotnet run --configuration Release
```

### Cách 3: Deploy trên Shared Folder (Multi-user)

1. Copy toàn bộ thư mục vào shared folder
2. **Không cần** cài đặt trên từng máy
3. Mỗi user sẽ có log file riêng: `PDFCompressor_USERNAME_COMPUTERNAME_YYYY-MM-DD.log`
4. **Auto-detect**: NLog tự động tạo user-specific log files
5. **No conflicts**: Nhiều user có thể chạy đồng thời mà không bị lock file

⚠️ **Yêu cầu**: Shared folder phải có quyền read/write cho tất cả users

## 📖 Sử dụng

### Cơ bản

1. **Chọn file PDF**: Nhấn nút "Chọn file PDF" và chọn file cần nén
2. **Phân tích tự động**: Ứng dụng tự động phân tích loại tài liệu
3. **Tùy chỉnh cài đặt** (nếu cần):
   - Loại nén: Chọn "Tự động (Tốt nhất)" để tối ưu tự động
   - Chất lượng ảnh: Kéo thanh trượt từ 10-100%
   - Tối ưu cho tài liệu scan: Tick nếu là file scan
4. **Nén file**: Nhấn nút "Nén PDF"
5. **Xem kết quả**: Dialog hiển thị thông tin chi tiết và hỏi mở thư mục

### Nâng cao

#### Phân loại tài liệu tự động

- **Tài liệu scan**: File có nhiều hình ảnh, ít văn bản → Sử dụng nén mạnh
- **Tài liệu văn bản**: Nhiều text, nhiều font → Giữ chất lượng cao
- **Nội dung hỗn hợp**: Cả text và hình ảnh → Cân bằng chất lượng/size
- **Tài liệu chung**: Các loại khác → Sử dụng cài đặt mặc định

#### Chia nhỏ file lớn

1. Tick "Chia nhỏ file lớn (>10MB sau khi nén)"
2. Nhập kích thước tối đa cho mỗi phần (mặc định: 5MB)
3. Nén file bình thường
4. Nếu file kết quả > giới hạn → Tự động chia thành nhiều parts

#### Thông số nén theo loại tài liệu

| Loại tài liệu | PDF Setting | Resolution | JPEG Quality |
|--------------|-------------|-------------|--------------|
| Scan | /screen | 150-300 DPI | 10-100% |
| Văn bản | /ebook | 300 DPI | 80-100% |
| Hỗn hợp | /printer | 200 DPI | 10-100% |
| Chung | /default | 300 DPI | 75% |

## 🔧 Tính năng chi tiết

### Phân tích tài liệu thông minh

Ứng dụng phân tích PDF để xác định loại tài liệu:

```csharp
// Scan document indicators
- File size > 5MB
- More image operators than text operators
- Scan-related metadata (Scanner, TWAIN, WIA)

// Text document indicators
- Text operators > 50
- Fonts > 5

// Mixed content indicators
- Text operators > 10 AND Image operators > 2
```

### Tham số Ghostscript tối ưu

**Tài liệu scan:**
```
-sDEVICE=pdfwrite -dPDFSETTINGS=/screen
-dColorImageResolution=150 -dGrayImageResolution=150
-dAutoFilterColorImages=false -dColorImageFilter=/DCTEncode
-dJPEGQ=75 -dSubsetFonts=true -dEmbedAllFonts=false
```

**Tài liệu văn bản:**
```
-sDEVICE=pdfwrite -dPDFSETTINGS=/ebook
-dColorImageResolution=300 -dGrayImageResolution=300
-dJPEGQ=80 -dAutoFilterColorImages=true
```

### Logging system (NLog v6.0)

- **Multi-user Support**: Tên file theo user và computer: `PDFCompressor_USERNAME_COMPUTERNAME_YYYY-MM-DD.log`
- **Location**: `./Logs/PDFComplier_USERNAME_COMPUTERNAME_YYYY-MM-DD.log`
- **Format**: `2025-11-14 10:30:15 [INFO] [MainForm] Đã tải file: document.pdf`
- **Async Logging**: Không bị lock file, performance cao
- **Rotation**: Tự động archive sau 7 ngày
- **Multi-target**: File + Console + Debug output
- **Shared Folder Safe**: User-specific filenames prevent conflicts
- **Internal Logging**: `internal-nlog.txt` cho NLog diagnostics

### File splitting algorithm

```csharp
// Calculate number of parts needed
numSplits = Math.Ceiling(fileSize / maxSplitSize)

// Split using Ghostscript page ranges
gs -sDEVICE=pdfwrite -dFirstPage=1 -dLastPage=10 -sOutputFile=part1.pdf input.pdf
gs -sDEVICE=pdfwrite -dFirstPage=11 -dLastPage=20 -sOutputFile=part2.pdf input.pdf
```

## 🐛 Troubleshooting

### Các vấn đề thường gặp

#### 1. "Không tìm thấy Ghostscript"
- **Nguyên nhân**: File `gswin64c.exe` bị thiếu trong thư mục Ghostscript
- **Giải pháp**: Download lại ứng dụng đầy đủ hoặc copy Ghostscript từ bản khác

#### 2. "Không thể nén file"
- **Nguyên nhân**: File PDF bị lỗi hoặc protected
- **Giải pháp**: Thử mở file bằng PDF viewer khác, kiểm tra file không bị password protected

#### 3. "File kết quả lớn hơn file gốc"
- **Nguyên nhân**: File gốc đã được nén tối ưu hoặc là file text đơn thuần
- **Giải pháp**: Thử chất lượng thấp hơn hoặc dùng cài đặt "Screen"

#### 4. "Không thể ghi log"
- **Nguyên nhân**: Không có quyền ghi vào thư mục Logs
- **Giải pháp**: Chạy ứng dụng với quyền Administrator

#### 5. "Lỗi Ghostscript API: Mã lỗi không xác định: -100"
- **Nguyên nhân**: Ghostscript API yêu cầu poll callback nhưng không được thiết lập
- **Giải pháp**: Đã được khắc phục trong phiên bản mới (v1.0.1+) bằng cách thêm poll callback vào GhostscriptAPI.cs
- **Fallback**: Tự động chuyển sang sử dụng process-based Ghostscript nếu API thất bại

### Debug với logs

Kiểm tra file log để troubleshooting:

```bash
# Mở file log ngày hiện tại
notepad ./Logs/PDFCompressor_2025-11-04.log

# Tìm kiếm lỗi
findstr /i "error" ./Logs/PDFCompressor_*.log
```

### Performance tips

1. **File lớn (>100MB)**: Sử dụng chất lượng thấp hơn
2. **Nhiều file**: Chạy lần lượt, không mở nhiều instance
3. **Scan documents**: Bật "Tối ưu cho tài liệu scan"
4. **Text documents**: Giữ chất lượng cao để không bị mờ

## 📊 Technical Details

### Architecture

```
PdfCompressor/
├── MainForm.cs              # Main form logic and compression
├── MainForm.Designer.cs     # UI layout (Vietnamese)
├── GhostscriptAPI.cs        # Ghostscript DLL wrapper
├── Program.cs              # Application entry point
├── PdfCompressor.csproj    # Project configuration
├── Ghostscript/            # Ghostscript binaries
│   ├── gswin64c.exe       # Command-line executable
│   ├── gswin64.exe        # GUI executable
│   ├── gsdll64.dll        # Ghostscript library
│   └── gsdll64.lib        # Import library
├── bin/                   # Build output
├── obj/                   # Build artifacts
└── Logs/                  # Log files directory
    └── PDFCompressor_YYYY-MM-DD.log
```

### Dependencies

- **.NET 9.0 Windows Forms**: UI framework
- **Ghostscript 10.06.0**: PDF processing engine with DLL API integration
- **System.IO**: File operations
- **System.Diagnostics**: Process management
- **System.Threading.Tasks**: Async operations

### Ghostscript API Integration

- **DLL Wrapper**: `GhostscriptAPI.cs` provides direct API calls with poll callback to prevent -100 errors
- **Fallback System**: Automatic switch to process-based execution if API fails
- **Error Handling**: Comprehensive error codes and Vietnamese error messages
- **Performance**: Direct API calls provide better performance than process spawning
- **Stability**: Poll callback implementation prevents interruption errors (v1.0.1+)

### Code Organization

- **Document Analysis**: `AnalyzeDocumentType()`, `IsScannedDocument()`
- **Compression Logic**: `CompressPdfThreaded()`, `BuildIntelligentGhostscriptArguments()`
- **File Operations**: `SplitPdfFile()`, `OpenOutputFolder()`
- **Logging**: `LogMessage()`, `SaveLogToFile()`
- **UI Management**: `UpdateProgress()`, `InvokeRequired` patterns

## 📞 Thông tin tác giả

**Nguyễn Ngọc Tú**

- 📧 **Email**: ngoctuct@gmail.com
- 📱 **Điện thoại**: 0983862402


## 📜 License

This project is licensed under the MIT License - see the details below:

```
MIT License

Copyright (c) 2025 Nguyễn Ngọc Tú

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

### Third-party Dependencies

This project includes the following third-party components:

- **Ghostscript**: Licensed under the AGPL (Affero General Public License) v3+
  - Copyright © 2025 Artifex Software, Inc. All rights reserved.
  - More information: https://www.ghostscript.com/licensing/index.html

By using this software, you agree to comply with both the MIT License for this project and the AGPL v3+ license for Ghostscript.

 

## 🔒 Security Note

Ứng dụng xử lý file PDF cục bộ, không upload data lên server. Mọi thông tin tài liệu được giữ nguyên trên máy người dùng.

---

**Phiên bản**: 1.1
**Cập nhật lần cuối**: 14/11/2025
**Framework**: .NET 9.0 Windows Forms

### Version History

- **v1.1** (14/11/2025):
  - 🚀 **Migrated to NLog v6.0** for professional logging system
  - 👥 **Multi-user Support**: User-specific log filenames for shared folder environments
  - 🔄 **Async Logging**: Non-blocking file operations, no more lock issues
  - 📁 **Safe Shared Folder**: Each user gets separate log file with username and computer name
  - 🗂️ **Log Rotation**: Automatic archive after 7 days, organized in archive folder
  - 🔧 **Centralized Logger**: Unified logging interface for MainForm and GhostscriptAPI
  - 🎯 **Improved Debugging**: Enhanced log format with timestamps, log levels, and component names
  - 📊 **Better Performance**: Async queue-based logging system

- **v1.0.1** (4/11/2025):
  - Fixed Ghostscript API -100 error by implementing poll callback
  - Enhanced stability of Ghostscript DLL integration
  - Updated error handling and logging

- **v1.0.0** (4/11/2025):
  - Initial release with intelligent PDF compression
  - Document analysis and automatic optimization
  - File splitting for large documents
  - Vietnamese language interface