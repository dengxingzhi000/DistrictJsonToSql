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

        private static readonly GuidV7Generator GuidGenerator = new GuidV7Generator();

        /// <summary>
        /// 生成整份 SQL 脚本（含头部注释与分批 INSERT）。
        /// </summary>
        public static string GenerateSql(IEnumerable<DistrictItem> districts)
        {
            var sqlBuilder = new StringBuilder();
            sqlBuilder.AppendLine("-- 地域数据插入SQL");
            sqlBuilder.AppendLine("-- 生成时间: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sqlBuilder.AppendLine();

            var insertStatements = new List<string>();

            foreach (var district in districts)
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
        /// 把 SQL 脚本写入磁盘，覆盖同名 .sql 文件。
        /// </summary>
        public static void WriteSqlFile(string sqlFilePath, string sql)
        {
            File.WriteAllText(sqlFilePath, sql, Encoding.UTF8);
        }
    }
}
