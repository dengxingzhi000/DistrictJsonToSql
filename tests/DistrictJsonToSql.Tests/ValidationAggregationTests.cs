using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;

namespace DistrictJsonToSql.Tests
{
    /// <summary>
    /// 校验汇总：默认仍是首错即抛（异常文案/路径不变），可选模式一次性收集全部问题。
    /// </summary>
    public class ValidationAggregationTests
    {
        [Fact]
        public void ValidateAll_CollectsEveryIssueInDepthFirstOrder()
        {
            var districts = BuildMultipleIssueTree();

            var issues = SqlGenerator.ValidateAll(districts);

            Assert.Equal(5, issues.Count);
            Assert.Equal("[0]", issues[0].NodePath);
            Assert.Contains("第 1 级 code 应为 2 位数字", issues[0].Message);
            Assert.Equal("[1].children[0]", issues[1].NodePath);
            Assert.Equal("[1].children[1]", issues[2].NodePath);
            Assert.Equal("[1].children[2].children[0].children[0].children[0]", issues[3].NodePath);
            Assert.Contains("最多支持 4 级", issues[3].Message);
            Assert.Equal("[2]", issues[4].NodePath);
            Assert.Contains("code 重复", issues[4].Message);
        }

        [Fact]
        public void GenerateSql_FirstError_IsIdenticalToFirstIssue()
        {
            var districts = BuildMultipleIssueTree();
            var dialect = new PostgresDialect(new GeneratorOptions());
            var issues = SqlGenerator.ValidateAll(districts);

            // 默认（首错即抛）：异常文案与路径必须与汇总结果的第一条完全一致
            var error = Assert.Throws<DistrictValidationException>(() => SqlGenerator.GenerateSql(districts, dialect));
            Assert.Equal(issues[0].Message, error.Message);
            Assert.Equal(issues[0].NodePath, error.NodePath);
            Assert.Single(error.Issues);
        }

        [Fact]
        public void GenerateSqlToFile_CollectAllErrors_ThrowsWithEveryIssueAndWritesNothing()
        {
            var districts = BuildMultipleIssueTree();
            var dialect = new PostgresDialect(new GeneratorOptions());
            var path = NewPath();
            var issues = SqlGenerator.ValidateAll(districts);

            try
            {
                // 默认模式：仍只报第一条，且不产生输出文件
                var first = Assert.Throws<DistrictValidationException>(
                    () => SqlGenerator.GenerateSqlToFile(districts, path, dialect, 0, null));
                Assert.Equal(issues[0].Message, first.Message);
                Assert.Equal(issues[0].NodePath, first.NodePath);
                Assert.False(File.Exists(path));

                // 汇总模式：异常里带全部问题，首条与默认模式完全相同
                var aggregated = Assert.Throws<DistrictValidationException>(
                    () => SqlGenerator.GenerateSqlToFile(districts, path, dialect, 0, null, collectAllErrors: true));
                Assert.Equal(5, aggregated.Issues.Count);
                Assert.Equal(issues[0].Message, aggregated.Message);
                Assert.Equal(issues[0].NodePath, aggregated.NodePath);
                Assert.False(File.Exists(path));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void GenerateSqlToFile_CollectAllErrors_OnValidData_KeepsOutputIdentical()
        {
            var path = Path.Combine(
                AppContext.BaseDirectory, "SampleData", "beijing-subset.json");
            var districts = new JsonDistrictReader().Read(path);
            var dialect = new PostgresDialect(new GeneratorOptions());
            var output = NewPath();

            try
            {
                var result = SqlGenerator.GenerateSqlToFile(districts, output, dialect, 200, null, collectAllErrors: true);
                Assert.Equal(7, result.NodeCount);
                Assert.Equal(12, result.TotalLines);
                Assert.False(result.PreviewTruncated);

                // 汇总开关只影响校验，不影响输出内容（UTF-8 无 BOM）
                var bytes = File.ReadAllBytes(output);
                Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
                Assert.Equal(12, CountLines(File.ReadAllText(output, Encoding.UTF8)));
            }
            finally
            {
                File.Delete(output);
            }
        }

        private static List<DistrictItem> BuildMultipleIssueTree()
        {
            return new List<DistrictItem>
            {
                new DistrictItem { Code = "1", Name = "坏省" },
                new DistrictItem
                {
                    Code = "11",
                    Name = "省A",
                    Children = new List<DistrictItem>
                    {
                        new DistrictItem { Code = "1", Name = "坏市" },
                        new DistrictItem { Code = "1101", Name = string.Empty },
                        new DistrictItem
                        {
                            Code = "1102",
                            Name = "市C",
                            Children = new List<DistrictItem>
                            {
                                new DistrictItem
                                {
                                    Code = "110201",
                                    Name = "区",
                                    Children = new List<DistrictItem>
                                    {
                                        new DistrictItem
                                        {
                                            Code = "110201001",
                                            Name = "街道",
                                            Children = new List<DistrictItem>
                                            {
                                                new DistrictItem { Code = "110201001001", Name = "五级" }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                },
                new DistrictItem { Code = "11", Name = "重复省" }
            };
        }

        private static int CountLines(string text)
        {
            return text.Replace("\r\n", "\n").Split('\n').Length - 1;
        }

        private static string NewPath()
        {
            var directory = Path.Combine(Path.GetTempPath(), "djs-validation-tests");
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, Guid.NewGuid().ToString("N") + ".sql");
        }
    }
}
