# AGENTS.md

## Setup

- Visual Studio 2022 Community with ".NET 桌面开发" workload
- .NET 9 SDK (`dotnet --list-sdks`)

## Dev commands

```powershell
# Build
dotnet build

# Run (optional: pass a .dbf/.csv/.xlsx file as argument)
dotnet run --project src/dbfview
dotnet run --project src/dbfview -- "path\to\file.dbf"

# Publish single-file executable
dotnet publish src/dbfview -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

## Architecture

- Single WinForms project at `src/dbfview/`
- **No external dependencies** — all format readers are custom implementations
- `IFileReader` interface: `DbfReader`, `CsvReader`, `ExcelReader` — each handles one format
- `EncodingDetector`: shared encoding detection (BOM + UTF-8 sampling → fallback GBK)
- Data model: `IFileReader.Read()` → `DataTable` → `DataView` → `DataGridView`
- `MainForm` uses `IFileReader` for all file opening; format-specific controls shown/hidden via `UpdateCsvControls()`
- `DbfHelper.Save` is the only remaining static helper, used only for DBF write

### DataTable columns
- DBF: `_deleted` (bool), `_row` (int), then field columns (string/decimal)
- CSV / Excel: `_row` (int), then field columns (string/decimal, no `_deleted`)
- Delete marks (DBF only): first byte of record (0x20=valid, 0x2A=deleted), Ctrl+Delete toggles
- CellFormatting: `_deleted` check guarded by `_data.Columns.Contains("_deleted")` — safe for CSV/Excel

### Encoding
- `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)` in `Program.cs` — required or GBK fails
- `EncodingDetector.Detect(byte[] sample, int len)`: BOM → UTF-8; try UTF-8 decode → replacement char check → GBK fallback
- DBF: sample from field area only
- CSV: sample from first 4KB of file
- Excel: always UTF-8 (internal format)
- User can override encoding via toolbar combo; triggers `LoadFile()` re-read

### Format readers

| Format | Class | Extensions | Save | Notes |
|--------|-------|-----------|------|-------|
| DBF | `DbfReader` | .dbf | Yes (via `DbfHelper.Save`) | Visual FoxPro 0x30, language driver 0x86 |
| CSV | `CsvReader` | .csv | No | Configurable delimiter (`Delimiter`), first-row-as-header toggle |
| Excel | `ExcelReader` | .xlsx | No | Parses ZIP/OpenXML, reads shared strings, discovers first sheet via workbook.xml rels |

### CsvReader details
- `Delimiter` property (default `,`), `FirstRowAsHeader` property (default `true`)
- Column name generation when header disabled: Excel-style A, B, ..., Z, AA, AB, ...
- BOM stripping: if first char is `\uFEFF`, removed before parsing
- Numeric column auto-detection: sample first 100 rows, `decimal.TryParse` on all non-empty values
- Quoted fields with `""` escaping, newlines inside quoted fields
- Format-specific toolbar controls visible when `_reader is CsvReader`

### ExcelReader details
- Reads first sheet only (discovered via `xl/workbook.xml` + `xl/_rels/workbook.xml.rels`)
- Shared strings from `xl/sharedStrings.xml`
- Cell types: inlineStr, shared string (`s`), boolean (`b`), number/plain
- Empty cells handled via cell reference parsing (`GetColumnIndex`)
- First row used as header (auto-detect)

## Key files

| File | Role |
|------|------|
| `src/dbfview/Program.cs` | Entry point, encoding provider registration |
| `src/dbfview/MainForm.cs` | WinForms UI: layout, file open/save/export, filter, column sizing, delete marks, tooltip, keyboard shortcuts |
| `src/dbfview/Readers/IFileReader.cs` | Reader interface |
| `src/dbfview/Readers/EncodingDetector.cs` | Shared BOM + UTF-8 heuristic encoding detector |
| `src/dbfview/Readers/DbfReader.cs` | DBF 2000 binary read |
| `src/dbfview/Readers/CsvReader.cs` | CSV parser with configurable delimiter |
| `src/dbfview/Readers/ExcelReader.cs` | .xlsx reader (ZIP + XML) |
| `src/dbfview/FilterHelper.cs` | Filter expression auto-quoting for string columns |
| `src/dbfview/DbfHelper.cs` | DBF 2000 binary write only (Save) |

## Keyboard shortcuts

| Key | Action |
|-----|--------|
| `Ctrl+O` | Open file |
| `Ctrl+S` | Save file (DBF only) |
| `Ctrl+F` | Focus filter box |
| `Ctrl+Delete` | Toggle delete mark (DBF only) |

## Conventions

- No designer file — UI is built programmatically in `MainForm.InitializeComponent()`
- `_grid` double-buffering enabled via reflection to reduce flicker
- 0 warnings required for build
- Windows-only (`net9.0-windows` target framework)
- `dbfview.csproj.user` contains a stale `Form1.cs` reference — ignore it
- All readers are stateless except `CsvReader` (has `Delimiter` and `FirstRowAsHeader` properties)
