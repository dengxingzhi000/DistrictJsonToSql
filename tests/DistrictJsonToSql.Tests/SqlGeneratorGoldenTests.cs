using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Xunit;

namespace DistrictJsonToSql.Tests
{
    /// <summary>
    /// 输出格式基线：把当前 PostgreSQL 方言的表名、列序、::uuid 字面量、
    /// 层级补零与 parent_code 关系统统钉死，后续重构不得改变。
    /// </summary>
    public class SqlGeneratorGoldenTests
    {
        private static readonly Regex UuidLiteral = new Regex(
            "'[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}'::uuid",
            RegexOptions.Compiled);

        private static readonly Regex TimestampLine = new Regex(
            "-- 生成时间: \\d{4}-\\d{2}-\\d{2} \\d{2}:\\d{2}:\\d{2}",
            RegexOptions.Compiled);

        private static readonly string[] ExpectedLines =
        {
            "-- 地域数据插入SQL",
            "-- 生成时间: <ts>",
            "",
            "INSERT INTO public.tb_district (guid, code, \"name\", district_type, parent_code) VALUES",
            "('<uuid>'::uuid, '110000', '北京市', 1, NULL),",
            "('<uuid>'::uuid, '110100', '市辖区', 2, '110000'),",
            "('<uuid>'::uuid, '110101', '东城区', 3, '110100'),",
            "('<uuid>'::uuid, '110101001', '东华门街道', 4, '110101'),",
            "('<uuid>'::uuid, '110101002', '景山街道', 4, '110101'),",
            "('<uuid>'::uuid, '110102', '西城区', 3, '110100'),",
            "('<uuid>'::uuid, '110102001', '西长安街街道', 4, '110102');",
            "",
            ""
        };

        [Fact]
        public void GenerateSql_SampleSubset_KeepsCurrentPostgresFormat()
        {
            var districts = LoadSample();

            var actual = Normalize(SqlGenerator.GenerateSql(districts));

            Assert.Equal(string.Join("\n", ExpectedLines), actual);
        }

        [Fact]
        public void GenerateSql_MoreThanOneBatch_SplitsInsertStatements()
        {
            var districts = new List<DistrictItem>();
            for (var i = 0; i < SqlGenerator.DefaultBatchSize + 1; i++)
            {
                districts.Add(new DistrictItem { Code = "11", Name = "省" + i });
            }

            var sql = SqlGenerator.GenerateSql(districts);

            var headers = Regex.Matches(sql, "INSERT INTO public\\.tb_district").Count;
            Assert.Equal(2, headers);
        }

        [Fact]
        public void GenerateInsertStatement_DoublesSingleQuotesInName()
        {
            var tuple = SqlGenerator.GenerateInsertStatement(
                "00000000-0000-0000-0000-000000000000", "110000", "张'三", 1, null);

            Assert.Equal(
                "('00000000-0000-0000-0000-000000000000'::uuid, '110000', '张''三', 1, NULL)",
                tuple);
        }

        private static string Normalize(string sql)
        {
            var text = sql.Replace("\r\n", "\n");
            text = UuidLiteral.Replace(text, "'<uuid>'::uuid");
            text = TimestampLine.Replace(text, "-- 生成时间: <ts>");
            return text;
        }

        private static List<DistrictItem> LoadSample()
        {
            var path = Path.Combine(System.AppContext.BaseDirectory, "SampleData", "beijing-subset.json");
            var json = File.ReadAllText(path, Encoding.UTF8);
            return JsonConvert.DeserializeObject<List<DistrictItem>>(json);
        }
    }
}
