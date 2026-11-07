using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace DistrictJsonToSql.Tests
{
    /// <summary>
    /// 流式写盘路径的回归：与 GenerateSql 逐字节等价、100k 节点的精确行数、预览截断，
    /// 以及流式读取后 NodePath 仍保持原有格式。
    /// </summary>
    public class SqlGeneratorFileTests
    {
        private static readonly Regex UuidLiteral = new Regex(
            "'[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}'::uuid",
            RegexOptions.Compiled);

        private static readonly Regex TimestampLine = new Regex(
            "-- 生成时间: \\d{4}-\\d{2}-\\d{2} \\d{2}:\\d{2}:\\d{2}",
            RegexOptions.Compiled);

        private sealed class RecordingProgress : IProgress<SqlGenerationProgress>
        {
            public readonly List<SqlGenerationProgress> Reports = new List<SqlGenerationProgress>();

            public SqlGenerationProgress Last => Reports[Reports.Count - 1];

            public void Report(SqlGenerationProgress value)
            {
                Reports.Add(value);
            }
        }

        [Fact]
        public void GenerateSqlToFile_MatchesGenerateSqlByteForByte()
        {
            var districts = LoadSample();
            var dialect = new PostgresDialect(new GeneratorOptions());
            var expected = SqlGenerator.GenerateSql(districts, dialect);
            var path = NewPath(".sql");

            try
            {
                var result = SqlGenerator.GenerateSqlToFile(districts, path, dialect, 4096, null);
                var written = File.ReadAllText(path, Encoding.UTF8);

                // 除每次都会变化的 uuid 和生成时间外逐字节一致：CRLF、批次分隔、末尾空行都必须相同
                Assert.Equal(Normalize(expected), Normalize(written));
                Assert.Equal(12, result.TotalLines);
                Assert.Equal(12, CountLines(written));
                Assert.Equal(7, result.NodeCount);
                Assert.False(result.PreviewTruncated);
                // 预览是给界面看的，统一用 CRLF；文件里批次内部仍是 \n，按行尾归一后内容相同
                Assert.Equal(written.Replace("\r\n", "\n").Replace("\n", Environment.NewLine), result.PreviewText);

                // 与 WriteSqlFile 一样，UTF-8 无 BOM
                var bytes = File.ReadAllBytes(path);
                Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void GenerateSqlToFile_HundredThousandNodes_WritesExactLineCount()
        {
            var districts = BuildHundredThousandNodes();
            var dialect = new PostgresDialect(new GeneratorOptions { BatchSize = 100 });
            var reporter = new RecordingProgress();
            var path = NewPath(".sql");

            try
            {
                var result = SqlGenerator.GenerateSqlToFile(districts, path, dialect, 200, reporter);

                // 100,000 个节点 = 1000 个满批；行数 = 3 + 1000 * 2 + 100,000
                Assert.Equal(100000, result.NodeCount);
                Assert.Equal(102003, result.TotalLines);
                Assert.Equal(102003, CountLines(File.ReadAllText(path, Encoding.UTF8)));
                Assert.True(result.PreviewTruncated);
                Assert.Equal(200, CountLines(result.PreviewText));

                Assert.True(reporter.Reports.Count > 0);
                Assert.Equal(100000, reporter.Last.NodesTotal);
                Assert.Equal(100000, reporter.Last.NodesDone);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void GenerateSqlToFile_PreviewTruncated_KeepsFullLineCount()
        {
            var province = new DistrictItem { Code = "11", Name = "省", Children = new List<DistrictItem>() };
            for (var i = 0; i < 300; i++)
            {
                province.Children.Add(new DistrictItem { Code = (i + 1).ToString("D4"), Name = "市" + i });
            }

            var dialect = new PostgresDialect(new GeneratorOptions { BatchSize = 100 });
            var path = NewPath(".sql");

            try
            {
                var result = SqlGenerator.GenerateSqlToFile(
                    new List<DistrictItem> { province }, path, dialect, 5, null);

                // 301 个节点 / 4 个批次：3 + 8 + 301 = 312 行
                Assert.True(result.PreviewTruncated);
                Assert.Equal(312, result.TotalLines);
                Assert.Equal(312, CountLines(File.ReadAllText(path, Encoding.UTF8)));
                Assert.Equal(5, CountLines(result.PreviewText));
                Assert.Equal("-- 地域数据插入SQL", FirstLine(result.PreviewText));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void JsonDistrictReader_StreamingPreservesNodePathAndHandlesBom()
        {
            var json = "[{\"code\":\"11\",\"name\":\"省\",\"children\":[{\"code\":\"1101\",\"name\":\"市\","
                       + "\"children\":[{\"code\":\"110101\",\"name\":\"区\"},{\"code\":\"12\",\"name\":\"坏节点\"}]}]}]";
            var path = NewPath(".json");

            try
            {
                // 带 BOM 也要能读；对象树不变，报错位置仍是 [0].children[0].children[1]
                File.WriteAllText(path, json, new UTF8Encoding(true));
                var districts = new JsonDistrictReader().Read(path);
                Assert.Equal(1, districts.Count);

                var dialect = new PostgresDialect(new GeneratorOptions());
                var error = Assert.Throws<DistrictValidationException>(
                    () => SqlGenerator.GenerateSql(districts, dialect));
                Assert.Equal("[0].children[0].children[1]", error.NodePath);
            }
            finally
            {
                File.Delete(path);
            }
        }

        private static List<DistrictItem> BuildHundredThousandNodes()
        {
            var province = new DistrictItem { Code = "11", Name = "省", Children = new List<DistrictItem>() };
            var districts = new List<DistrictItem> { province };
            var districtIndex = 0;

            for (var cityIndex = 0; cityIndex < 100; cityIndex++)
            {
                var city = new DistrictItem
                {
                    Code = (cityIndex + 1).ToString("D4"),
                    Name = "市" + cityIndex,
                    Children = new List<DistrictItem>()
                };
                province.Children.Add(city);

                var count = cityIndex == 99 ? 998 : 999;
                for (var i = 0; i < count; i++)
                {
                    districtIndex++;
                    city.Children.Add(new DistrictItem
                    {
                        Code = districtIndex.ToString("D6"),
                        Name = "区" + districtIndex
                    });
                }
            }

            return districts;
        }

        private static string FirstLine(string text)
        {
            return text.Replace("\r\n", "\n").Split('\n')[0];
        }

        private static int CountLines(string text)
        {
            return text.Replace("\r\n", "\n").Split('\n').Length - 1;
        }

        private static string NewPath(string extension)
        {
            var directory = Path.Combine(Path.GetTempPath(), "djs-file-tests");
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, Guid.NewGuid().ToString("N") + extension);
        }

        private static string Normalize(string sql)
        {
            var text = sql.Replace("\r\n", "\n");
            text = UuidLiteral.Replace(text, "'<uuid>'::uuid");
            text = TimestampLine.Replace(text, "-- 生成时间: <ts>");
            return text;
        }

        private static IReadOnlyList<DistrictItem> LoadSample()
        {
            return new JsonDistrictReader().Read(
                Path.Combine(AppContext.BaseDirectory, "SampleData", "beijing-subset.json"));
        }
    }
}
