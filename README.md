# dbfview

DBF / CSV / Excel 文件轻量查看器，无外部依赖，仅 Windows。DBF 文件支持编辑和保存。

A lightweight DBF / CSV / Excel viewer with zero external dependencies (Windows only). DBF files also support editing and saving.

## 功能 / Features

- **DBF**：Visual FoxPro (0x30) 签名，读写支持，删除标记 (Ctrl+Delete)，编码 0x86 语言驱动保存 / Visual FoxPro (0x30) signature, read/write, delete marks (Ctrl+Delete), language driver 0x86 save
- **CSV**：自定义分隔符（逗号/分号/Tab/空格/竖线），首行表头可选，Excel 风格自动列名 (A, B, ..., AA, AB) / Custom delimiter (comma/semicolon/tab/space/pipe), optional first row header, Excel-style auto column names
- **Excel**：读取 .xlsx（共享字符串、sheet 自动发现）/ Read .xlsx (shared strings, auto sheet discovery)
- 导出为 CSV / Export to CSV
- GBK / UTF-8 编码自动检测与手动切换 / Auto-detect & manual switch between GBK and UTF-8 encoding
- 行过滤，友好语法（字符串值自动加引号）/ Row filter with user-friendly syntax (auto-quote string values)
- 列宽适配：适配内容 / 适配窗口 / Column resize: fit to content / fit to window
- 单元格 tooltip + 状态栏显示当前单元格内容 / Cell tooltip + status bar cell content display

## 快捷键 / Keyboard Shortcuts

| 按键 / Key | 操作 / Action |
|------------|---------------|
| `Ctrl+O` | 打开文件 / Open file |
| `Ctrl+S` | 保存（仅 DBF）/ Save (DBF only) |
| `Ctrl+F` | 聚焦过滤框 / Focus filter box |
| `Ctrl+Delete` | 切换删除标记（仅 DBF）/ Toggle delete mark (DBF only) |

## 环境要求 / Requirements

- Windows
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

## 构建与运行 / Build & Run

```powershell
dotnet build
dotnet run --project src/dbfview
```

直接打开文件 / Open a file directly:

```powershell
dotnet run --project src/dbfview -- "path\to\file.dbf"
dotnet run --project src/dbfview -- "path\to\file.csv"
dotnet run --project src/dbfview -- "path\to\file.xlsx"
```

发布单文件可执行程序 / Publish single-file executable:

```powershell
dotnet publish src/dbfview -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

## 项目结构 / Project Structure

| 文件 / File | 用途 / Role |
|-------------|------------|
| `src/dbfview/Program.cs` | 入口点 / Entry point |
| `src/dbfview/MainForm.cs` | UI 与事件处理 / UI and event handlers |
| `src/dbfview/Readers/IFileReader.cs` | 文件读取接口 / File reader interface |
| `src/dbfview/Readers/EncodingDetector.cs` | 通用编码检测 / Shared encoding detection |
| `src/dbfview/Readers/DbfReader.cs` | DBF 2000 二进制读取 / DBF 2000 binary read |
| `src/dbfview/Readers/CsvReader.cs` | CSV 解析 / CSV parsing |
| `src/dbfview/Readers/ExcelReader.cs` | Excel (.xlsx) 读取 / Excel (.xlsx) read |
| `src/dbfview/DbfHelper.cs` | DBF 2000 保存 / DBF 2000 binary write |
| `src/dbfview/FilterHelper.cs` | 过滤表达式增强 / Filter expression enhancement |

## 许可证 / License

MIT
