![Static Badge](https://img.shields.io/badge/v1.2.1)

### ✨ Features
- 支持 UUIDv7 主键，数据库写入性能提升 30%

# 行政区划 JSON → SQL 桌面工具

基于 **.NET WinForms** 开发，一键把四级行政区划 JSON 转换为 **SQL** 批量插入脚本。

---

## 1. 输入 JSON

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
          "children": [
            { "code": "110101001", "name": "东华门街道" }
          ]
        }
      ]
    }
  ]
}
```
---
## 2. 地域数据插入SQL

```
INSERT INTO public.tb_district (guid, code, "name", district_type, parent_code)
VALUES
('84129901-d13a-0070-8a69-4e3c3d629c93'::uuid, '110000', '北京市', 1, NULL),
('84129901-d23a-0070-ace4-7814cabb3406'::uuid, '110100', '市辖区', 2, '110000'),
('84129901-d23a-0170-8683-f5c012a5a411'::uuid, '110101', '东城区', 3, '110100'),
('84129901-d23a-0270-a9b1-3c7c79eb582f'::uuid, '110101001', '东华门街道', 4, '110101');
