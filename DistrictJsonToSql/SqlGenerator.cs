using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DistrictJsonToSql
{
    /// <summary>
    /// 行政区划 JSON 到 PostgreSQL INSERT 脚本的纯转换逻辑。
    /// 不依赖 WinForms，供界面层与单元测试共用。
    /// </summary>
    public static class SqlGenerator
    {
        public const string TableName = "public.tb_district";
        public const int DefaultBatchSize = 100;

        /// <summary>省 / 市 / 区县 / 街道的 code 长度（pcas-code.json 的实际约定）。</summary>
        private static readonly int[] ExpectedCodeLengths = { 2, 4, 6, 9 };

        private const int MaxDepth = 4;

        private static readonly GuidV7Generator GuidGenerator = new GuidV7Generator();

        /// <summary>
        /// 生成整份 SQL 脚本（含头部注释与分批 INSERT）。
        /// 输入不合法时抛 <see cref="ArgumentException"/>，消息里带节点路径。
        /// </summary>
        public static string GenerateSql(IEnumerable<DistrictItem> districts)
        {
            if (districts == null)
            {
                throw new ArgumentNullException(nameof(districts));
            }

            var items = districts.ToList();
            var seenCodes = new Dictionary<int, HashSet<string>>();
            for (var level = 1; level <= MaxDepth; level++)
            {
                seenCodes[level] = new HashSet<string>(StringComparer.Ordinal);
            }

            for (var i = 0; i < items.Count; i++)
            {
                ValidateNode(items[i], 1, "[" + i + "]", seenCodes);
            }

            var sqlBuilder = new StringBuilder();
            sqlBuilder.AppendLine("-- 地域数据插入SQL");
            sqlBuilder.AppendLine("-- 生成时间: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sqlBuilder.AppendLine();

            var insertStatements = new List<string>();

            foreach (var district in items)
            {
                var provinceGuid = GuidGenerator.Generate();
                var provinceSql = GenerateInsertStatement(provinceGuid, district.Code + "0000", district.Name, 1, null);
                insertStatements.Add(provinceSql);

                if (district.Children != null)
                {
                    foreach (var city in district.Children)
                    {
                        var cityGuid = GuidGenerator.Generate();
                        var citySql = GenerateInsertStatement(cityGuid, city.Code + "00", city.Name, 2, district.Code + "0000");
                        insertStatements.Add(citySql);

                        if (city.Children != null)
                        {
                            foreach (var county in city.Children)
                            {
                                var countyGuid = GuidGenerator.Generate();
                                var countySql = GenerateInsertStatement(countyGuid, county.Code, county.Name, 3, city.Code + "00");
                                insertStatements.Add(countySql);

                                if (county.Children != null)
                                {
                                    foreach (var street in county.Children)
                                    {
                                        var streetGuid = GuidGenerator.Generate();
                                        var streetSql = GenerateInsertStatement(streetGuid, street.Code, street.Name, 4, county.Code);
                                        insertStatements.Add(streetSql);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            for (int i = 0; i < insertStatements.Count; i += DefaultBatchSize)
            {
                var batch = insertStatements.Skip(i).Take(DefaultBatchSize);
                sqlBuilder.AppendLine("INSERT INTO " + TableName + " (guid, code, \"name\", district_type, parent_code) VALUES");
                sqlBuilder.AppendLine(string.Join(",\n", batch) + ";");
                sqlBuilder.AppendLine();
            }

            return sqlBuilder.ToString();
        }

        /// <summary>
        /// 生成单条 VALUES 元组，例如 (guid, '110000', '北京市', 1, NULL)。
        /// </summary>
        public static string GenerateInsertStatement(string guid, string code, string name, int districtType, string parentCode)
        {
            var parentValue = parentCode == null ? "NULL" : $"'{parentCode}'";
            return $"('{guid}'::uuid, '{code}', '{name.Replace("'", "''")}', {districtType}, {parentValue})";
        }

        /// <summary>
        /// 把 SQL 脚本写入磁盘，覆盖同名 .sql 文件；不带 UTF-8 BOM，避免 psql 首行解析失败。
        /// </summary>
        public static void WriteSqlFile(string sqlFilePath, string sql)
        {
            File.WriteAllText(sqlFilePath, sql, new UTF8Encoding(false));
        }

        private static void ValidateNode(DistrictItem node, int depth, string path, Dictionary<int, HashSet<string>> seenCodes)
        {
            if (depth > MaxDepth)
            {
                throw new ArgumentException(
                    "行政区划最多支持 " + MaxDepth + " 级，却发现第 " + depth + " 级节点：" + path);
            }

            if (node == null)
            {
                throw new ArgumentException("第 " + depth + " 级存在空节点：" + path);
            }

            if (string.IsNullOrWhiteSpace(node.Name))
            {
                throw new ArgumentException(
                    "第 " + depth + " 级节点缺少 name：" + path + " code=" + node.Code);
            }

            if (node.Code == null || node.Code.Length != ExpectedCodeLengths[depth - 1] || !IsDigitsOnly(node.Code))
            {
                throw new ArgumentException(
                    "第 " + depth + " 级 code 应为 " + ExpectedCodeLengths[depth - 1] + " 位数字，实际为 "
                    + (node.Code == null ? "null" : node.Code.Length + " 位（" + node.Code + "）")
                    + "：" + path + " name=" + node.Name);
            }

            if (!seenCodes[depth].Add(node.Code))
            {
                throw new ArgumentException(
                    "第 " + depth + " 级 code 重复：" + path + " code=" + node.Code + " name=" + node.Name);
            }

            if (node.Children == null)
            {
                return;
            }

            for (var i = 0; i < node.Children.Count; i++)
            {
                ValidateNode(node.Children[i], depth + 1, path + ".children[" + i + "]", seenCodes);
            }
        }

        private static bool IsDigitsOnly(string value)
        {
            for (var i = 0; i < value.Length; i++)
            {
                if (value[i] < '0' || value[i] > '9')
                {
                    return false;
                }
            }
            return true;
        }
    }
}
