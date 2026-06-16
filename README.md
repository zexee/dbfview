# dbfview

A lightweight Windows DBF viewer/editor for Visual FoxPro (DBF 2000) files, with no external dependencies.

## Features

- Open and edit DBF files with Visual FoxPro signature (0x30)
- GBK and UTF-8 encoding support with auto-detection
- Edit cell values directly in the grid
- Toggle delete marks (**Ctrl+Delete**)
- Row filter with user-friendly syntax (auto-quote string values)
- Column resize: fit to content / fit to window
- Save with correct language driver (0x86 = Chinese) for FoxPro compatibility

## Keyboard Shortcuts

| Key | Action |
|-----|--------|
| `Ctrl+O` | Open file |
| `Ctrl+S` | Save file |
| `Ctrl+F` | Focus filter box |
| `Ctrl+Delete` | Toggle delete mark |

## Requirements

- Windows
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

## Build & Run

```powershell
dotnet build
dotnet run --project src/dbfview
```

Open a file directly:

```powershell
dotnet run --project src/dbfview -- "path\to\file.dbf"
```

Publish single-file executable:

```powershell
dotnet publish src/dbfview -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

## Project Structure

| File | Role |
|------|------|
| `src/dbfview/Program.cs` | Entry point |
| `src/dbfview/MainForm.cs` | UI and event handlers |
| `src/dbfview/DbfHelper.cs` | DBF 2000 binary read/write |
| `src/dbfview/FilterHelper.cs` | Filter expression enhancement |

## License

MIT
