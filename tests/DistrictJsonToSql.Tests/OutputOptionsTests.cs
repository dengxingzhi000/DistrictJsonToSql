using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace DistrictJsonToSql.Tests
{
    /// <summary>
    /// 第 7 步输出选项：事务包裹、ON CONFLICT，以及默认不改变历史输出。
    /// </summary>
    public class OutputOptionsTests
    {
        private static readonly Regex UuidLiteral = new Regex(
            "'[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}'::uuid",
            RegexOptions.Compiled);

        private static readonly Regex TimestampLine = new Regex(
            "-- 生成时间: \\d{4}-\\d{2}-\\d{2} \\d{2}:\\d{2}:\\d{2}",
            RegexOptions.Compiled);

        [Fact]
        public void GenerateSql_WrapInTransaction_AddsBeginAndCommitAndMatchesFile()
        {
            var districts = LoadSample();
            var dialect = new PostgresDialect(new GeneratorOptions());
            var expected = SqlGenerator.GenerateSql(districts, dialect, wrapInTransaction: true);
            var path = NewPath();

            try
            {
                var result = SqlGenerator.GenerateSqlToFile(districts, path, dialect, 4096, null, false, true);
                var written = File.ReadAllText(path, Encoding.UTF8);

                Assert.Equal(Normalize(expected), Normalize(written));
                // 12 行基线 + BEGIN;、空行、COMMIT; = 15 行
                Assert.Equal(15, result.TotalLines);
                Assert.Equal(15, CountLines(written));
                Assert.False(result.PreviewTruncated);

                var lines = written.Replace("\r\n", "\n").Split('\n');
                Assert.Equal("BEGIN;", lines[3]);
                Assert.Equal(string.Empty, lines[4]);
                Assert.Equal("COMMIT;", lines[14]);
                // 文件仍以换行结尾
                Assert.Equal(string.Empty, lines[15]);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void OnConflictDoNothing_AppendsTargetlessClauseWithoutChangingLineCount()
        {
            var districts = LoadSample();
            var dialect = new PostgresDialect(
                new GeneratorOptions { OnConflict = OnConflictModes.DoNothingText });
            var sql = SqlGenerator.GenerateSql(districts, dialect);

            Assert.EndsWith("VALUES ON CONFLICT DO NOTHING", HeaderLine(sql));
            Assert.Equal(12, CountLines(sql));

            // 默认配置不追加任何子句，输出与历史版本一致
            var plain = HeaderLine(SqlGenerator.GenerateSql(
                districts, new PostgresDialect(new GeneratorOptions())));
            Assert.EndsWith("VALUES", plain);
        }

        [Fact]
        public void OnConflictDoUpdate_UsesCodeTargetAndExcludedColumns()
        {
            var districts = LoadSample();
            var dialect = new PostgresDialect(
                new GeneratorOptions { OnConflict = OnConflictModes.DoUpdateText });
            var header = HeaderLine(SqlGenerator.GenerateSql(districts, dialect));

            Assert.Equal(
                "INSERT INTO public.tb_district (guid, code, \"name\", district_type, parent_code) "
                + "VALUES ON CONFLICT (code) DO UPDATE SET \"name\" = EXCLUDED.\"name\", "
                + "district_type = EXCLUDED.district_type, parent_code = EXCLUDED.parent_code",
                header);
        }

        [Fact]
        public void GeneratorOptions_RejectsUnknownOnConflict()
        {
            var error = Assert.Throws<ArgumentException>(
                () => new PostgresDialect(new GeneratorOptions { OnConflict = "merge" }));

            Assert.Contains("onConflict", error.Message);
            Assert.Contains("doUpdate", error.Message);
        }

        private static IReadOnlyList<DistrictItem> LoadSample()
        {
            return new JsonDistrictReader().Read(
                Path.Combine(AppContext.BaseDirectory, "SampleData", "beijing-subset.json"));
        }

        private static string HeaderLine(string sql)
        {
            return sql.Replace("\r\n", "\n")
                .Split('\n')
                .First(line => line.StartsWith("INSERT INTO", StringComparison.Ordinal));
        }

        private static int CountLines(string text)
        {
            return text.Replace("\r\n", "\n").Split('\n').Length - 1;
        }

        private static string NewPath()
        {
            var directory = Path.Combine(Path.GetTempPath(), "djs-output-tests");
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, Guid.NewGuid().ToString("N") + ".sql");
        }

        private static string Normalize(string sql)
        {
            var text = sql.Replace("\r\n", "\n");
            text = UuidLiteral.Replace(text, "'<uuid>'::uuid");
            text = TimestampLine.Replace(text, "-- 生成时间: <ts>");
            return text;
        }
    }
}
