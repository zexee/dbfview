# dbfview

Visual FoxPro (DBF 2000) 文件查看/编辑器，无外部依赖。

A lightweight Windows DBF viewer/editor for Visual FoxPro (DBF 2000) files, with no external dependencies.

## 功能 / Features

- 打开和编辑 Visual FoxPro 签名的 DBF 文件 (0x30) / Open and edit DBF files with Visual FoxPro signature (0x30)
- GBK / UTF-8 编码自动检测 / Auto-detect GBK and UTF-8 encoding
- 直接在表格中编辑字段 / Edit cell values directly in the grid
- 切换删除标记（**Ctrl+Delete**）/ Toggle delete marks with **Ctrl+Delete**
- 行过滤，支持友好语法（字符串自动加引号）/ Row filter with user-friendly syntax (auto-quote string values)
- 列宽适配：适配内容 / 适配窗口 / Column resize: fit to content / fit to window
- 保存时使用正确的语言驱动 (0x86 = Chinese) / Save with correct language driver (0x86 = Chinese)

## 快捷键 / Keyboard Shortcuts

| 按键 / Key | 操作 / Action |
|------------|---------------|
| `Ctrl+O` | 打开文件 / Open file |
| `Ctrl+S` | 保存文件 / Save file |
| `Ctrl+F` | 聚焦过滤框 / Focus filter box |
| `Ctrl+Delete` | 切换删除标记 / Toggle delete mark |

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
| `src/dbfview/DbfHelper.cs` | DBF 2000 二进制读写 / DBF 2000 binary read/write |
| `src/dbfview/FilterHelper.cs` | 过滤表达式增强 / Filter expression enhancement |

## 许可证 / License

MIT
