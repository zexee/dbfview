using System.Data;
using System.Reflection;
using System.Text;

namespace dbfview;

public class MainForm : Form
{
    // Data
    private DataTable? _data;
    private DataView? _view;
    private Encoding _encoding = Encoding.GetEncoding("gbk");
    private string? _filePath;

    // Controls
    private MenuStrip _menu = null!;
    private ToolStrip _toolbar = null!;
    private ToolStripComboBox _encodingCombo = null!;
    private ToolStripTextBox _filterBox = null!;
    private ToolStripButton _btnOpen = null!;
    private ToolStripButton _btnSave = null!;
    private ToolStripButton _btnApplyFilter = null!;
    private ToolStripButton _btnClearFilter = null!;
    private ToolStripButton _btnFitContent = null!;
    private ToolStripButton _btnFitWindow = null!;
    private DataGridView _grid = null!;
    private StatusStrip _status = null!;
    private ToolStripStatusLabel _lblCount = null!;
    private ToolStripStatusLabel _lblFiltered = null!;
    private ToolStripStatusLabel _lblEncoding = null!;
    private ToolStripStatusLabel _lblPath = null!;

    public MainForm(string? openFile = null)
    {
        InitializeComponent();
        _encodingCombo.SelectedIndex = 0;

        if (openFile != null && File.Exists(openFile))
            LoadFile(openFile);
    }

    private void InitializeComponent()
    {
        Text = "dbfview";
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        Size = new Size(1000, 650);
        MinimumSize = new Size(600, 400);
        KeyPreview = true;

        // Menu
        _menu = new MenuStrip();
        var fileMenu = new ToolStripMenuItem("文件(&F)");
        fileMenu.DropDownItems.Add("打开(&O)...", null, (_, _) => OpenFile());
        fileMenu.DropDownItems.Add("保存(&S)", null, (_, _) => SaveFile());
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add("退出(&X)", null, (_, _) => Close());
        _menu.Items.Add(fileMenu);

        var viewMenu = new ToolStripMenuItem("查看(&V)");
        viewMenu.DropDownItems.Add("适配内容", null, (_, _) => AutoFitContent());
        viewMenu.DropDownItems.Add("适配窗口", null, (_, _) => FitToWindow());
        _menu.Items.Add(viewMenu);

        // Toolbar
        _toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden };

        _toolbar.Items.Add(new ToolStripLabel("编码:"));
        _encodingCombo = new ToolStripComboBox();
        _encodingCombo.Items.AddRange(["GBK", "UTF-8"]);
        _encodingCombo.SelectedIndex = 0;
        _encodingCombo.Width = 80;
        _encodingCombo.SelectedIndexChanged += (_, _) =>
        {
            if (_encodingCombo.SelectedIndex == 0)
                _encoding = Encoding.GetEncoding("gbk");
            else
                _encoding = Encoding.UTF8;

            if (_filePath != null)
                LoadFile(_filePath);
        };
        _toolbar.Items.Add(_encodingCombo);

        _btnOpen = new ToolStripButton("打开", CreateOpenIcon(), (_, _) => OpenFile());
        _btnOpen.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
        _toolbar.Items.Add(_btnOpen);

        _btnSave = new ToolStripButton("保存", CreateSaveIcon(), (_, _) => SaveFile());
        _btnSave.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
        _toolbar.Items.Add(_btnSave);

        _toolbar.Items.Add(new ToolStripSeparator());

        _toolbar.Items.Add(new ToolStripLabel("过滤:"));
        _filterBox = new ToolStripTextBox { Width = 180 };
        _filterBox.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) ApplyFilter(); };
        _toolbar.Items.Add(_filterBox);

        _btnApplyFilter = new ToolStripButton("应用", null, (_, _) => ApplyFilter());
        _toolbar.Items.Add(_btnApplyFilter);

        _btnClearFilter = new ToolStripButton("清除", null, (_, _) => ClearFilter());
        _toolbar.Items.Add(_btnClearFilter);

        _toolbar.Items.Add(new ToolStripSeparator());

        _btnFitContent = new ToolStripButton("适配内容", null, (_, _) => AutoFitContent());
        _toolbar.Items.Add(_btnFitContent);

        _btnFitWindow = new ToolStripButton("适配窗口", null, (_, _) => FitToWindow());
        _toolbar.Items.Add(_btnFitWindow);

        // DataGridView
        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToOrderColumns = true,
            AllowUserToResizeColumns = true,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
            SelectionMode = DataGridViewSelectionMode.CellSelect,
            ReadOnly = false,
            RowHeadersWidth = 40,
            VirtualMode = false,
            BackgroundColor = SystemColors.Window
        };
        typeof(DataGridView).GetProperty("DoubleBuffered",
            BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(_grid, true);
        _grid.CellValidating += Grid_CellValidating;
        _grid.RowPrePaint += Grid_RowPrePaint;
        _grid.CellFormatting += Grid_CellFormatting;
        _grid.KeyDown += Grid_KeyDown;

        // Status bar
        _status = new StatusStrip();
        _lblCount = new ToolStripStatusLabel("就绪") { BorderSides = ToolStripStatusLabelBorderSides.Right };
        _lblFiltered = new ToolStripStatusLabel("") { BorderSides = ToolStripStatusLabelBorderSides.Right };
        _lblEncoding = new ToolStripStatusLabel("GBK") { BorderSides = ToolStripStatusLabelBorderSides.Right };
        _lblPath = new ToolStripStatusLabel("");
        _status.Items.AddRange(new ToolStripItem[] { _lblCount, _lblFiltered, _lblEncoding, _lblPath });

        // Layout
        Controls.Add(_grid);
        Controls.Add(_toolbar);
        Controls.Add(_menu);
        Controls.Add(_status);
        MainMenuStrip = _menu;
    }

    // ─── File operations ────────────────────────────────────────

    private void OpenFile()
    {
        using var dlg = new OpenFileDialog
        {
            Filter = "DBF 文件|*.dbf|所有文件|*.*",
            Title = "打开 DBF 文件"
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        LoadFile(dlg.FileName);
    }

    private void LoadFile(string path)
    {
        try
        {
            var detected = DbfHelper.DetectEncoding(path);
            _encoding = detected;
            _encodingCombo.SelectedIndex = detected.Equals(Encoding.UTF8) ? 1 : 0;

            _data = DbfHelper.Load(path, _encoding);
            _view = new DataView(_data);
            _filePath = path;

            BindGrid();
            _data.AcceptChanges();
            AutoFitContent();
            UpdateStatus();
            Text = $"dbfview - {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"打开文件失败:\n{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SaveFile()
    {
        if (_data == null) return;
        if (_filePath == null)
        {
            using var dlg = new SaveFileDialog
            {
                Filter = "DBF 文件|*.dbf",
                Title = "保存 DBF 文件"
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;
            _filePath = dlg.FileName;
        }
        else
        {
            var result = MessageBox.Show($"确认覆盖 {Path.GetFileName(_filePath)}？", "保存",
                MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
            if (result != DialogResult.OK) return;
        }

        try
        {
            _data.AcceptChanges();
            DbfHelper.Save(_filePath, _data, _encoding);
            UpdateStatus();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存失败:\n{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ─── Grid binding ───────────────────────────────────────────

    private void BindGrid()
    {
        if (_view == null) return;

        _grid.DataSource = null;
        _grid.Columns.Clear();
        _grid.DataSource = _view;
        _grid.RowHeadersWidth = 40;

        // Hide internal columns
        if (_grid.Columns["_deleted"] != null) _grid.Columns["_deleted"]!.Visible = false;
        if (_grid.Columns["_row"] != null) _grid.Columns["_row"]!.Visible = true;

        // Set up the _row column
        if (_grid.Columns["_row"] != null)
        {
            _grid.Columns["_row"]!.HeaderText = "#";
            _grid.Columns["_row"]!.Width = 50;
            _grid.Columns["_row"]!.ReadOnly = true;
            _grid.Columns["_row"]!.DisplayIndex = 0;
        }

        foreach (DataGridViewColumn col in _grid.Columns)
        {
            if (col.Name == "_deleted" || col.Name == "_row") continue;
            col.MinimumWidth = 40;
        }

        _grid.ClearSelection();
    }

    // ─── Filter ─────────────────────────────────────────────────

    private void ApplyFilter()
    {
        if (_view == null || _data == null) return;
        var expr = _filterBox.Text.Trim();
        if (string.IsNullOrEmpty(expr))
        {
            ClearFilter();
            return;
        }

        try
        {
            var rowFilter = FilterHelper.BuildRowFilter(expr, _data);
            _view.RowFilter = rowFilter;
            UpdateStatus();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"过滤表达式错误:\n{ex.Message}", "过滤", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ClearFilter()
    {
        if (_view == null) return;
        _view.RowFilter = string.Empty;
        _filterBox.Text = string.Empty;
        UpdateStatus();
    }

    // ─── Column sizing ──────────────────────────────────────────

    private void AutoFitContent()
    {
        if (_grid.Columns.Count <= 2) return;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;

        foreach (DataGridViewColumn col in _grid.Columns)
        {
            if (col.Name == "_deleted" || col.Name == "_row") continue;
            col.MinimumWidth = 2;
        }

        _grid.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.AllCells);

        foreach (DataGridViewColumn col in _grid.Columns)
        {
            if (col.Name == "_deleted" || col.Name == "_row") continue;
            col.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            col.MinimumWidth = 40;
            col.Width = Math.Clamp(col.Width, 40, 500);
        }
    }

    private void FitToWindow()
    {
        if (_grid.Columns.Count <= 2) return;

        var dataCols = _grid.Columns.Cast<DataGridViewColumn>()
            .Where(c => c.Name != "_deleted" && c.Name != "_row").ToList();

        // Save original widths
        var originalWidths = dataCols.Select(c => c.Width).ToArray();
        var totalOriginal = originalWidths.Sum();
        if (totalOriginal == 0) return;

        // Let Fill mode determine exact available width
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        foreach (var col in dataCols)
        {
            col.AutoSizeMode = DataGridViewAutoSizeColumnMode.NotSet;
            col.MinimumWidth = 2;
        }
        _grid.PerformLayout();

        var fillTotal = dataCols.Sum(c => c.Width);
        var ratio = (double)fillTotal / totalOriginal;

        // Lock widths with proportional scaling
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        for (var i = 0; i < dataCols.Count; i++)
        {
            var col = dataCols[i];
            col.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            col.Width = Math.Clamp((int)Math.Round(originalWidths[i] * ratio), 20, 800);
            col.MinimumWidth = 40;
        }
    }

    // ─── Validation ─────────────────────────────────────────────

    private void Grid_CellValidating(object? sender, DataGridViewCellValidatingEventArgs e)
    {
        if (_grid.Columns[e.ColumnIndex].Name is "_deleted" or "_row") return;
        if (_data == null) return;

        var col = _data.Columns[_grid.Columns[e.ColumnIndex].Name];
        if (col == null || col.DataType != typeof(decimal)) return;

        var value = e.FormattedValue?.ToString();
        if (string.IsNullOrWhiteSpace(value)) return;

        if (!decimal.TryParse(value, out _))
        {
            e.Cancel = true;
        }
    }

    // ─── Delete mark ────────────────────────────────────────────

    private void Grid_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.Delete)
        {
            ToggleDeleteMark();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }

    private void ToggleDeleteMark()
    {
        if (_grid.CurrentRow == null || _data == null) return;
        if (_grid.CurrentRow.DataBoundItem is not DataRowView rowView) return;
        var row = rowView.Row;
        var current = row["_deleted"] is bool deleted && deleted;
        row["_deleted"] = !current;
        _grid.InvalidateRow(_grid.CurrentRow.Index);
    }

    private void Grid_RowPrePaint(object? sender, DataGridViewRowPrePaintEventArgs e)
    {
        if (_data == null || e.RowIndex < 0) return;
        if (_view == null) return;

        if (e.RowIndex < _view.Count)
        {
            _grid.Rows[e.RowIndex].HeaderCell!.Value = (e.RowIndex + 1).ToString();
        }
    }

    private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (_data == null || e.RowIndex < 0) return;
        if (_view == null || e.RowIndex >= _view.Count) return;

        var rowView = _view[e.RowIndex];
        if (rowView.Row["_deleted"] is bool deleted && deleted)
        {
            e.CellStyle!.ForeColor = Color.Gray;
        }
        else if (e.CellStyle!.ForeColor == Color.Gray)
        {
            e.CellStyle.ForeColor = _grid.DefaultCellStyle.ForeColor;
        }
    }

    // ─── Status ─────────────────────────────────────────────────

    private void UpdateStatus()
    {
        if (_data == null)
        {
            _lblCount.Text = "就绪";
            _lblFiltered.Text = "";
            _lblEncoding.Text = "";
            _lblPath.Text = "";
            return;
        }

        var total = _data.Rows.Count;
        var filtered = _view != null && !string.IsNullOrEmpty(_view.RowFilter)
            ? _view.Count
            : total;

        _lblCount.Text = $"共 {total} 条";
        _lblFiltered.Text = filtered != total ? $"已筛选: {filtered} 条" : "";
        _lblEncoding.Text = _encoding.Equals(Encoding.UTF8) ? "UTF-8" : "GBK";
        _lblPath.Text = _filePath ?? "";
    }

    // ─── Keyboard shortcuts ─────────────────────────────────────

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Control && e.KeyCode == Keys.O) { OpenFile(); e.Handled = true; }
        if (e.Control && e.KeyCode == Keys.S) { SaveFile(); e.Handled = true; }
        if (e.Control && e.KeyCode == Keys.F) { _filterBox.Focus(); e.Handled = true; }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_data?.GetChanges() != null)
        {
            var result = MessageBox.Show("文件已修改，是否保存？", "dbfview",
                MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (result == DialogResult.Yes) SaveFile();
            else if (result == DialogResult.Cancel) e.Cancel = true;
        }
        base.OnFormClosing(e);
    }

    private static Image CreateOpenIcon()
    {
        var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        // Folder body
        g.FillRectangle(Brushes.Gold, 1, 4, 14, 11);
        // Folder tab
        var tab = new Point[] { new(1, 6), new(1, 3), new(7, 3), new(7, 6) };
        g.FillPolygon(Brushes.Gold, tab);
        // Outline
        g.DrawLine(Pens.DarkGoldenrod, 1, 6, 1, 15);
        g.DrawLine(Pens.DarkGoldenrod, 1, 15, 14, 15);
        g.DrawLine(Pens.DarkGoldenrod, 14, 15, 14, 4);
        g.DrawLine(Pens.DarkGoldenrod, 14, 4, 7, 4);
        g.DrawLine(Pens.DarkGoldenrod, 7, 4, 7, 3);
        g.DrawLine(Pens.DarkGoldenrod, 7, 3, 1, 3);
        g.DrawLine(Pens.DarkGoldenrod, 1, 3, 1, 6);
        return bmp;
    }

    private static Image CreateSaveIcon()
    {
        var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        // Disk body
        g.FillRectangle(Brushes.SteelBlue, 2, 1, 12, 14);
        // Label area (white)
        g.FillRectangle(Brushes.White, 5, 3, 6, 6);
        // Shutter
        g.FillRectangle(Brushes.SlateGray, 5, 12, 6, 2);
        // Outline
        g.DrawRectangle(Pens.DarkBlue, 2, 1, 12, 14);
        g.DrawRectangle(Pens.DarkBlue, 5, 3, 6, 6);
        return bmp;
    }
}
