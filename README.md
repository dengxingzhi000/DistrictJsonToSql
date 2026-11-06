<div align="center">

# 行政区划 JSON → SQL 桌面工具

### 一键把四级行政区划 JSON 转换为 PostgreSQL 批量插入脚本。

**.NET Framework 4.7.2 WinForms。仅依赖 Newtonsoft.Json，离线运行，
UUIDv7 主键时间有序，索引写入更友好。**

[![.NET](https://img.shields.io/badge/.NET%20Framework-4.7.2-512BD4?logo=dotnet&logoColor=white&style=flat-square)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-green?style=flat-square)](#许可证)

[使用方式](#使用方式) · [转换规则](#转换规则) · [项目结构](#项目结构)

<code>DistrictJsonToSql.exe</code>

</div>

> [!NOTE]
> 目标框架是 **.NET Framework 4.7.2**（SDK-style 工程，`TargetFramework=net472`），
> 不是 .NET 5+，产物仍是可在 Windows 上双击运行的 WinForms exe。
> 工程已迁移到 SDK-style + `PackageReference`，可以直接 `dotnet build`；
> CI 依赖 `Microsoft.NETFramework.ReferenceAssemblies.net472`，
> 因此在没有 4.7.2 targeting pack 的机器上也能编译。
> JSON 解析依赖 Newtonsoft.Json 13.0.4。

---

手工整理行政区划数据通常是这样的：拿到一份四级 JSON，然后写脚本、写正则、
在数据库客户端里粘贴几千条 INSERT。这里把这个过程做成了一个拖入文件就能出
SQL 的小工具。

## 使用方式

1. 编译或直接运行 `DistrictJsonToSql.exe`
2. 点击选择按钮，载入行政区划 JSON 文件（仓库自带示例 `pcas-code.json`）
3. 复制生成的 SQL 到数据库客户端执行

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
- **主键为 UUIDv7** —— 按 RFC 9562 生成，字符串顺序即时间顺序，索引局部性优于 UUIDv4
- **PostgreSQL 方言** —— 主键带 `::uuid` 类型转换，`name` 用双引号包裹

> [!NOTE]
> UUIDv7 生成器是 `GuidV7Generator.cs` 里的手写实现，而不是
> `Guid.CreateVersion7()` —— 后者是 .NET 9+ 的 API，在 4.7.2 上不可用。
> 手写实现严格按 RFC 9562 组合时间戳、版本位与变体位，产出的字符串可直接按时间排序。

## 项目结构

```text
DistrictJsonToSql/
├── DistrictJsonToSql.sln
├── pcas-code.json                          # 示例行政区划数据
├── LICENSE                                 # MIT
├── .github/workflows/ci.yml                # windows-latest 上构建 + 跑测试
├── tests/DistrictJsonToSql.Tests/          # xunit golden / 回归用例
└── DistrictJsonToSql/
    ├── MainForm.cs                         # 界面（选择文件、展示结果）
    ├── SqlGenerator.cs                     # JSON → SQL 的纯转换逻辑与输入校验
    ├── DistrictItem.cs                     # 行政区划节点模型
    ├── GuidV7Generator.cs                  # RFC 9562 UUIDv7 生成器
    ├── Program.cs                          # 入口点
    ├── DistrictJsonToSql.csproj            # net472 WinExe（SDK-style）
    ├── App.config
    └── Properties/                         # Resources / Settings
```

## 许可证

MIT，见 [LICENSE](LICENSE)。
