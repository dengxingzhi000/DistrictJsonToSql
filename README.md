<div align="center">

# 行政区划 JSON → SQL 桌面工具

### 一键把四级行政区划 JSON 转换为 PostgreSQL 批量插入脚本。

**.NET 10 + WinUI 3（Fluent Design），自包含单文件发布，双击即用、离线运行。
UUIDv7 主键时间有序，索引写入更友好。**

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white&style=flat-square)](https://dotnet.microsoft.com/)
[![WinUI 3](https://img.shields.io/badge/WinUI-3%20%2F%20Windows%20App%20SDK%202.6.5-0067C0?style=flat-square)](https://learn.microsoft.com/windows/apps/winui/winui3/)
[![Windows](https://img.shields.io/badge/Windows-10%201809%2B-0078D4?logo=windows&logoColor=white&style=flat-square)](#系统要求)
[![License](https://img.shields.io/badge/license-MIT-green?style=flat-square)](#许可证)

[使用方式](#使用方式) · [转换规则](#转换规则) · [项目结构](#项目结构)

<code>DistrictJsonToSql.exe</code>

</div>

> [!NOTE]
> 目标框架是 **.NET 10 + WinUI 3**（`net10.0-windows10.0.19041.0`，Windows App SDK 2.6.5）。
> 界面使用 Fluent Design：Win11 上 Mica 背景，Win10 1809+ 回退亚克力/纯色；跟随系统明暗主题。
> CI 以 `win-x64` 自包含单文件方式发布，双击即用，目标机器不需要安装 .NET 或 Windows App SDK 运行时。
> 单 exe 约 **236MB**，首次启动会把依赖解压到临时目录，之后启动不再解压。
> 每次 CI 运行产出的 artifact 名为 `DistrictJsonToSql-win-x64`，其中只有 `DistrictJsonToSql.exe` 这一个文件。
> 开发构建需要 .NET 10 SDK，`dotnet build` / `dotnet test` 即可。
> JSON 解析依赖 Newtonsoft.Json 13.0.4。

---

手工整理行政区划数据通常是这样的：拿到一份四级 JSON，然后写脚本、写正则、
在数据库客户端里粘贴几千条 INSERT。这里把这个过程做成了一个拖入文件就能出
SQL 的小工具。

## 系统要求

- Windows 10 版本 1809（build 17763）或更高，x64
- 不需要安装 .NET 运行时，也不需要安装 Windows App SDK 运行时
- Win11 上使用 Mica 背景；Windows 10 上自动回退为亚克力/纯色背景，功能不受影响

## 使用方式

1. 直接运行 `DistrictJsonToSql.exe`（自包含单文件，首次启动会先解压依赖，可能需要几秒）；
   从 CI 运行的 artifacts 里下载 `DistrictJsonToSql-win-x64`，解压即得到 `DistrictJsonToSql.exe`；
   自行构建用
   `dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist`，
   产物同样是 `dist/DistrictJsonToSql.exe`
2. 点击选择按钮，载入行政区划 JSON 文件（仓库自带示例 `pcas-code.json`）
3. 复制生成的 SQL 到数据库客户端执行

### 命令行（CLI）

```shell
dotnet run --project DistrictJsonToSql.Cli -- --input pcas-code.json --output district.sql --table public.tb_district --batch 100
```

参数：`--input`（必填）、`--output`（必填，UTF-8 无 BOM）、`--table`（默认 `public.tb_district`）、`--batch`（默认 100）、`--all-errors`（校验失败时汇总输出全部问题，默认只报第一条）、`--help`。
退出码：`0` 成功、`1` 未预期错误、`2` 参数错误、`3` 输入读取失败、`4` 数据校验失败、`5` 输出写入失败；错误信息一律走 stderr，stdout 只放正常输出。

### 输入 JSON

```json
{
  "code": "11",
  "name": "北京市",
  "children": [
    {
      "code": "1101",
      "name": "市辖区",
      "children": [
        {
          "code": "110101",
          "name": "东城区",
          "children": [{ "code": "110101001", "name": "东华门街道" }]
        }
      ]
    }
  ]
}
```

### 输出 SQL

```sql
INSERT INTO public.tb_district (guid, code, "name", district_type, parent_code)
VALUES
('84129901-d13a-0070-8a69-4e3c3d629c93'::uuid, '110000', '北京市', 1, NULL),
('84129901-d23a-0070-ace4-7814cabb3406'::uuid, '110100', '市辖区', 2, '110000'),
('84129901-d23a-0170-8683-f5c012a5a411'::uuid, '110101', '东城区', 3, '110100'),
('84129901-d23a-0270-a9b1-3c7c79eb582f'::uuid, '110101001', '东华门街道', 4, '110101');
```

## 转换规则

输入 JSON 里的 `code` 是**变长的**（`11` / `1101` / `110101`），而
`tb_district.code` 要求定长 6 位，因此按层级补零：

| 层级        | district_type | code 生成规则   | 示例输入    | 输出        | parent_code |
| ----------- | ------------- | --------------- | ----------- | ----------- | ----------- |
| 省 / 直辖市 | 1             | `code + "0000"` | `11`        | `110000`    | `NULL`      |
| 市          | 2             | `code + "00"`   | `1101`      | `110100`    | `110000`    |
| 区 / 县     | 3             | 原样            | `110101`    | `110101`    | `110100`    |
| 街道        | 4             | 原样            | `110101001` | `110101001` | `110101`    |

其余规则：

- **批量 INSERT** —— 每 100 行合成一条 `INSERT ... VALUES`，不逐条插入
- **名称转义** —— 名称中的单引号按 SQL 标准加倍（`'` → `''`）
- **主键为 UUIDv7** —— 按 RFC 9562 生成，跨毫秒的字符串顺序即时间顺序，索引局部性优于 UUIDv4
- **PostgreSQL 方言** —— 主键带 `::uuid` 类型转换，`name` 用双引号包裹

> [!NOTE]
> 主键由 `Guid.CreateVersion7()` 生成（.NET 9+ API），
> 前 48 位是毫秒时间戳，跨毫秒的字符串顺序即时间顺序；
> 同一毫秒内的 `rand_a` 是随机值（RFC 9562 允许），不保证严格递增。

## 扩展点与配置

转换逻辑与界面、方言、输入格式已经拆开，但接口保持最小——没有第二方言或第二种输入格式的真实需求，
就不预置插件注册机制。

| 扩展点 | 文件 | 说明 |
| ------ | ---- | ---- |
| `ISqlDialect` | `DistrictJsonToSql.Core/ISqlDialect.cs` | INSERT 头部、一行 VALUES、批量大小；目前只有 `PostgresDialect` |
| `IDistrictReader` | `DistrictJsonToSql.Core/IDistrictReader.cs` | 输入适配层；目前只有 `JsonDistrictReader`（pcas-code.json 风格） |
| `GeneratorOptions` | `DistrictJsonToSql.Core/GeneratorOptions.cs` | 表名、列名、批量大小、方言名，可外置为 JSON |
| `DistrictValidationException` | `DistrictJsonToSql.Core/DistrictValidationException.cs` | 校验失败时携带节点路径（如 `[0].children[1]`） |

### 外置配置

把 `DistrictJsonToSql.example.json` 复制成 `DistrictJsonToSql.json`，放在 exe 同目录即可覆盖默认值：

```json
{
  "tableName": "public.tb_district",
  "batchSize": 100,
  "dialect": "postgres",
  "columns": { "name": "name" }
}
```

- 只写需要修改的项即可，未写的项沿用默认值（默认值与历史输出逐字节一致）
- 键名大小写不敏感，允许注释与末尾逗号
- `dialect` 目前只接受 `postgres`
- 校验失败不会中断界面，错误详情写入 `%LOCALAPPDATA%\DistrictJsonToSql\error.log`

## 项目结构

```text
DistrictJsonToSql/
├── DistrictJsonToSql.sln
├── pcas-code.json                          # 示例行政区划数据
├── DistrictJsonToSql.example.json          # 外置配置示例
├── LICENSE                                 # MIT
├── .github/workflows/ci.yml                # windows-latest 上构建 + 跑测试
├── tests/DistrictJsonToSql.Tests/          # xunit golden / 回归 / 扩展点用例
├── DistrictJsonToSql.Core/                # net10 纯逻辑，无 UI 依赖
│   ├── SqlGenerator.cs                     # 递归展开 + 输入校验 + 分批输出
│   ├── ISqlDialect.cs / PostgresDialect.cs # 方言扩展点
│   ├── IDistrictReader.cs / JsonDistrictReader.cs  # 输入适配
│   ├── GeneratorOptions.cs                 # 表名 / 列序 / 批量大小配置
│   ├── DistrictValidationException.cs      # 带节点路径的校验错误
│   ├── ErrorLogger.cs                      # 错误详情落盘
│   ├── DistrictItem.cs                     # 行政区划节点模型
│   └── DistrictJsonToSql.Core.csproj      # net10 类库
├── DistrictJsonToSql.Presentation/         # net10 ViewModel，不依赖 WinUI（CommunityToolkit.Mvvm）
│   ├── MainViewModel.cs                    # 选择文件 / 生成 SQL / 状态反馈
│   ├── IFilePickerService.cs / IDialogService.cs  # 平台能力接口
│   └── DistrictJsonToSql.Presentation.csproj
├── DistrictJsonToSql.Cli/                  # net10 命令行，复用 Core
│   ├── Program.cs                          # --input/--output/--table/--batch 与退出码
│   └── DistrictJsonToSql.Cli.csproj
└── DistrictJsonToSql/
    ├── App.xaml / App.xaml.cs               # WinUI 应用入口
    ├── MainWindow.xaml / MainWindow.xaml.cs # Fluent 界面（Mica / 暗色 / InfoBar / ContentDialog）
    └── DistrictJsonToSql.csproj            # net10 WinUI 3（SDK-style）
```

## 许可证

MIT，见 [LICENSE](LICENSE)。
