# dbfview 开发计划

## 1. 技术栈

| 层 | 选型 |
|----|------|
| 运行时 | .NET 9.0 |
| UI 框架 | Windows Forms (WinForms) |
| 语言 | C# 13 |
| DBF 库 | DotNetDBF 7.0.1 (NuGet) |
| 目标框架 | `net9.0-windows` |

---

## 2. 项目结构

```
D:\code\dbfview\
├── dbfview.sln
├── AGENTS.md
├── docs/
│   ├── requirements.md
│   └── dev_plan.md            ← 本文件
└── src/
    └── dbfview/
        ├── dbfview.csproj
        ├── Program.cs          ← 入口 + 命令行参数
        ├── MainForm.cs         ← 主窗体逻辑
        ├── MainForm.Designer.cs← 设计器生成代码
        ├── MainForm.resx       ← 资源文件
        ├── DbfHelper.cs        ← DBF 读写封装
        └── FilterHelper.cs     ← 过滤表达式处理
```

---

## 3. 核心类设计

### 3.1 DbfHelper

```
DbfHelper
├── + DetectEncoding(string path) → Encoding
│   检测策略:
│   1. 读取文件头 BOM，有 UTF-8 BOM(EF BB BF) → UTF-8
│   2. 读取 DBF 头字段区，按 UTF-8 解码字段名，有非法字节 → GBK
│   3. 检查 DBF 头字节 29 Language Driver 辅助判断
│   4. 无法判断 → GBK
├── + Load(string path, Encoding enc) → DataTable
│   读取 DBF → 转换为 DataTable（C 型列用 string，N 型列用 decimal，其他用 string 只读）
│   处理删除标记：增加 _deleted 布尔列
└── + Save(string path, DataTable table, Encoding enc)
    DataTable → DBF 写入，Signature = 0x30(DBF 2000)
```

### 3.2 FilterHelper

```
FilterHelper
├── + BuildRowFilter(string expression, DataTable table) → string
│   解析用户输入的过滤表达式，将字段名映射为 DataView.RowFilter 表达式
│   自动判断字段类型：N 型字段值不加引号，C 型字段值加单引号
│   例如: "A=123" → (列A为N型) "A=123"  or  (列A为C型) "A='123'"
│   例如: "A=123 AND NAME LIKE '张%'" → 按各字段类型处理
│   支持的操作符: =, >, <, >=, <=, <>, LIKE
```

### 3.3 MainForm（主界面）

```
MainForm
├── 控件
│   ├── MenuStrip       ← 文件(打开/保存/退出)、查看(适配内容/适配窗口)
│   ├── ToolStrip       ← 编码选择、打开、保存、分隔、过滤输入框、应用、清除、适配内容、适配窗口
│   ├── DataGridView    ← 数据表格（AllowUserToAddRows=False, 行号自动显示）
│   ├── StatusStrip     ← 总行数、筛选行数、编码、文件路径
│   └── OpenFileDialog  ← 文件打开对话框
├── 字段
│   ├── DataTable _data         ← 数据源
│   ├── DataView _view          ← 过滤视图
│   ├── Encoding _encoding      ← 当前编码
│   ├── string _filePath        ← 当前文件路径
│   └── bool _modified          ← 是否有未保存修改
├── 方法
│   ├── OpenFile()              ← 弹出对话框 → 选编码 → 加载 → 绑定 → 自动适配列宽
│   ├── SaveFile()              ← 回写 DataTable → 保存 DBF
│   ├── ApplyFilter()           ← 解析表达式 → DataView.RowFilter → 刷新
│   ├── ClearFilter()           ← 清除 DataView.RowFilter → 刷新
│   ├── AutoFitContent()        ← 逐列测量最宽文本像素宽 → 设置 Column.Width
│   ├── FitToWindow()           ← AutoSizeColumnsMode = Fill
│   └── UpdateStatusBar()       ← 刷新状态栏信息
├── 事件
│   ├── CellValidating          ← N 型字段校验（只允许数字、负号、小数点）
│   ├── CellValueChanged        ← 标记 _modified = true
│   ├── KeyDown                 ← Ctrl+O/Ctrl+S/Ctrl+F/Ctrl+Delete 快捷键
│   ├── RowPrePaint             ← 绘制行号、灰色显示删除行
│   └── CellFormatting          ← 删除行文字灰色
```

---

## 4. 数据流

```
  DBF 文件 ──(DotNetDBF)──→ DataTable ──→ DataView ──→ DataGridView
                                                   ↑
                                            FilterHelper
                                              解析过滤表达式
```

编辑流程：
```
  DataGridView 编辑 ──→ DataTable 修改 ──→ Save ──(DotNetDBF)──→ DBF 文件
```

---

## 5. 开发步骤

| 步骤 | 任务 | 产出 | 预估 |
|------|------|------|------|
| 1 | 创建解决方案 + 项目结构，安装 NuGet 依赖 | sln, csproj | 5 min |
| 2 | 实现 `DbfHelper.DetectEncoding` + `Load` + `Save` | DbfHelper.cs | 40 min |
| 3 | 实现 `FilterHelper.BuildRowFilter` | FilterHelper.cs | 20 min |
| 4 | 搭建主界面（工具栏、DataGridView、状态栏） | MainForm + Designer | 20 min |
| 5 | 实现文件打开逻辑（编码选择 → 加载 → 绑定 → 自动列宽） | MainForm.cs | 30 min |
| 6 | 实现文件保存逻辑 | MainForm.cs | 15 min |
| 7 | 实现过滤功能（解析 + 应用 + 清除 + 状态栏） | MainForm.cs | 20 min |
| 8 | 实现列宽适配功能（适配内容 / 适配窗口） | MainForm.cs | 15 min |
| 9 | 实现 N 型字段输入校验 + 删除标记 + 快捷键 | MainForm.cs | 15 min |
| 10 | 实现命令行参数支持 + 窗口标题显示文件名 | Program.cs | 5 min |
| 11 | 编译测试，修复问题 | — | 20 min |

**总预估**: ~3 小时

---

## 6. 关键技术点

### 6.1 DBF 2000 写入

DotNetDBF 默认为 dBase III（`Signature=0x03`），写入 DBF 2000 时需设置 `writer.Signature = 0x30`。

### 6.2 N 型字段校验

DataGridView `CellValidating` 事件中正则校验 `^-?\d*\.?\d*$`，不通过则 `e.Cancel = true`。

### 6.3 过滤表达式处理

用户输入 `NAME LIKE '张%' AND AGE>=18`，FilterHelper 需：
1. 解析出各条件（按 AND/OR 分割）
2. 对每个条件提取字段名，从 DataTable 获取字段类型
3. N 型字段的 value 不加引号，C 型字段的 value 加单引号
4. 重新组合为 DataView.RowFilter 表达式

### 6.4 命令行参数

```csharp
// Program.cs
[STAThread]
static void Main(string[] args)
{
    Application.EnableVisualStyles();
    Application.SetCompatibleTextRenderingDefault(false);
    Application.Run(new MainForm(args.Length > 0 ? args[0] : null));
}
```

### 6.5 删除标记

DBF 文件每条记录首字节为删除标记（`0x20`=正常, `0x2A`=已删除）。
读取时存入 `_deleted` 列，保存时写回首字节。
Ctrl+Delete 切换标记，RowPrePaint 事件中已删除行设灰色背景。

### 6.6 自动编码检测

实现顺序：
1. 用 FileStream 读取前几百字节
2. 检查 BOM：`0xEF 0xBB 0xBF` → UTF-8
3. 读取 DBF 头字段区（offset 32 起），按 UTF-8 解码字段名
4. 如有 `�` 替换字符或异常 → 判定为 GBK
5. 头字节 29 Language Driver：`0x86` 等中文代码页 → GBK
6. 默认回退 GBK
