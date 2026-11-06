using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;

namespace DistrictJsonToSql.Tests
{
    /// <summary>
    /// P3 扩展点用例：方言、配置外置、输入适配、节点路径错误。
    /// 默认配置必须与历史输出逐字节一致，自定义配置只改表名/列名/批量大小。
    /// </summary>
    public class ExtensionPointTests
    {
        private const string DefaultHeader =
            "INSERT INTO public.tb_district (guid, code, \"name\", district_type, parent_code) VALUES";

        [Fact]
        public void DefaultOptions_ShouldKeepHistoricalHeader()
        {
            var dialect = new PostgresDialect(new GeneratorOptions());

            Assert.Equal(DefaultHeader, dialect.BuildInsertHeader());
            Assert.Equal(100, dialect.BatchSize);
        }

        [Fact]
        public void CustomTableAndColumnNames_ShouldBeUsedInHeader()
        {
            var options = new GeneratorOptions
            {
                TableName = "public.area",
                Columns = new ColumnNames
                {
                    Guid = "id",
                    Code = "area_code",
                    Name = "area_name",
                    DistrictType = "level",
                    ParentCode = "parent"
                }
            };

            var dialect = new PostgresDialect(options);

            Assert.Equal(
                "INSERT INTO public.area (id, area_code, \"area_name\", level, parent) VALUES",
                dialect.BuildInsertHeader());
        }

        [Fact]
        public void LoadFromMissingFile_ShouldFallBackToDefaults()
        {
            var options = GeneratorOptions.LoadFrom(
                Path.Combine(Path.GetTempPath(), "djs-missing-" + Guid.NewGuid().ToString("N") + ".json"));

            Assert.Equal("public.tb_district", options.TableName);
            Assert.Equal(100, options.BatchSize);
            Assert.Equal("postgres", options.Dialect);
        }

        [Fact]
        public void LoadFromCamelCaseJsonWithComments_ShouldOverrideDefaults()
        {
            var path = TempJson(
                "{\n"
                + "  // 自定义表名\n"
                + "  \"tableName\": \"public.area\",\n"
                + "  \"batchSize\": 7,\n"
                + "  \"columns\": { \"name\": \"area_name\" },\n"
                + "}\n");

            try
            {
                var options = GeneratorOptions.LoadFrom(path);

                Assert.Equal("public.area", options.TableName);
                Assert.Equal(7, options.BatchSize);
                Assert.Equal("area_name", options.Columns.Name);
                Assert.Equal("guid", options.Columns.Guid);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void Validate_ShouldRejectNonPositiveBatchSize()
        {
            var options = new GeneratorOptions { BatchSize = 0 };

            Assert.Throws<ArgumentException>(() => options.Validate());
        }

        [Fact]
        public void Validate_ShouldRejectUnsupportedDialect()
        {
            var options = new GeneratorOptions { Dialect = "mysql" };

            Assert.Throws<ArgumentException>(() => options.Validate());
        }

        [Fact]
        public void GenerateSql_ShouldRespectDialectBatchSize()
        {
            var options = new GeneratorOptions { BatchSize = 2 };
            var dialect = SqlGenerator.CreateDialect(options);
            var province = new DistrictItem
            {
                Code = "11",
                Name = "北京市",
                Children = new List<DistrictItem>
                {
                    new DistrictItem { Code = "1101", Name = "市辖区" },
                    new DistrictItem { Code = "1102", Name = "县" }
                }
            };

            var sql = SqlGenerator.GenerateSql(new List<DistrictItem> { province }, dialect);

            Assert.Equal(2, CountOccurrences(sql, DefaultHeader));
        }

        [Fact]
        public void ValidationError_ShouldCarryNodePath()
        {
            var province = new DistrictItem
            {
                Code = "11",
                Name = "北京市",
                Children = new List<DistrictItem>
                {
                    new DistrictItem { Code = "110101", Name = "市辖区" }
                }
            };

            var error = Assert.Throws<DistrictValidationException>(
                () => SqlGenerator.GenerateSql(new List<DistrictItem> { province }));

            Assert.Equal("[0].children[0]", error.NodePath);
            Assert.Contains("[0].children[0]", error.Message);
        }

        [Fact]
        public void JsonDistrictReader_ShouldReadSampleSubset()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "SampleData", "beijing-subset.json");

            var districts = new JsonDistrictReader().Read(path);

            Assert.Single(districts);
            Assert.Equal("北京市", districts[0].Name);
            Assert.Equal("110101", districts[0].Children[0].Children[0].Code);
        }

        [Fact]
        public void JsonDistrictReader_ShouldRejectNullRoot()
        {
            var path = TempJson("null");

            try
            {
                var reader = new JsonDistrictReader();

                Assert.Throws<ArgumentException>(() => reader.Read(path));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void JsonDistrictReader_ShouldThrowForMissingFile()
        {
            var reader = new JsonDistrictReader();

            Assert.ThrowsAny<IOException>(
                () => reader.Read(Path.Combine(Path.GetTempPath(), "djs-missing-" + Guid.NewGuid().ToString("N") + ".json")));
        }

        [Fact]
        public void ErrorLogger_ShouldWriteUnderLocalApplicationData()
        {
            Assert.Contains("DistrictJsonToSql", ErrorLogger.LogPath);
            Assert.EndsWith("error.log", ErrorLogger.LogPath);
        }

        private static string TempJson(string json)
        {
            var path = Path.Combine(Path.GetTempPath(), "djs-options-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(path, json, new UTF8Encoding(false));
            return path;
        }

        private static int CountOccurrences(string text, string value)
        {
            var count = 0;
            var index = 0;
            while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += value.Length;
            }

            return count;
        }
    }
}
