# AGENTS.md

## Setup

- Visual Studio 2022 Community with ".NET 桌面开发" workload
- .NET 9 SDK (`dotnet --list-sdks`)

## Dev commands

```powershell
# Build
dotnet build

# Run (optional: pass a .dbf file path as argument)
dotnet run --project src/dbfview
dotnet run --project src/dbfview -- "path\to\file.dbf"

# Publish single-file executable
dotnet publish src/dbfview -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

## Architecture

- Single WinForms project at `src/dbfview/`
- No external DBF library — custom DBF 2000 (Visual FoxPro, signature 0x30) parser in `DbfHelper.cs`
- **Encoding**: requires `System.Text.Encoding.CodePages` NuGet package + `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)` in `Program.cs` — without this GBK silently fails and the window won't open
- Data model: `DataTable` → `DataView` → `DataGridView`
- DataTable columns: `_deleted` (bool), `_row` (int), then field columns (string for C, decimal for N)
- Filter syntax: `DataView.RowFilter` SQL-like syntax, enhanced by `FilterHelper.BuildRowFilter` for unquoted string values
- Encoding: auto-detected (UTF-8 BOM + field name heuristic), fallback GBK
- Delete marks: first byte of each DBF record (0x20=valid, 0x2A=deleted), toggled with Ctrl+Delete
- Column types: C → string, N/F → decimal, DBNull allowed

## Key files

| File | Role |
|------|------|
| `src/dbfview/Program.cs` | Entry point, command-line arg handling |
| `src/dbfview/MainForm.cs` | UI layout + all event handlers (open, save, filter, column sizing, validation, delete marks) |
| `src/dbfview/DbfHelper.cs` | DBF 2000 binary read/write + encoding detection |
| `src/dbfview/FilterHelper.cs` | Filter expression enhancement (auto-quote string values) |

## Conventions

- No designer file — UI is built programmatically in `MainForm.InitializeComponent()`
- 0 warnings required for build
- Windows-only (`net9.0-windows` target framework)
