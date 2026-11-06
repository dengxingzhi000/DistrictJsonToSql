using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Xunit;

namespace DistrictJsonToSql.Tests
{
    /// <summary>
    /// P1 修复的三个缺陷的回归用例，以及升级到 Guid.CreateVersion7() 之后的等价断言。
    /// </summary>
    public class RegressionTests
    {
        [Fact]
        public void GeneratedGuids_ShouldFollowRfc9562FieldLayout()
        {
            var guids = ExtractGeneratedGuids();

            Assert.NotEmpty(guids);
            foreach (var guid in guids)
            {
                var groups = guid.Split('-');
                Assert.Equal(5, groups.Length);
                Assert.Equal('7', groups[2][0]);
                Assert.True("89ab".IndexOf(groups[3][0]) >= 0, "变体位必须在第四段首位: " + guid);
            }
        }

        /// <summary>
        /// Guid.CreateVersion7() 只保证跨毫秒的时间顺序；同一毫秒内 rand_a 是随机值
        /// （RFC 9562 允许），因此这里比较两次不同毫秒的生成结果。
        /// </summary>
        [Fact]
        public void GeneratedGuids_ShouldSortByGenerationTimeAcrossMilliseconds()
        {
            var first = ExtractGeneratedGuids()[0];
            Thread.Sleep(20);
            var second = ExtractGeneratedGuids()[0];

            Assert.True(
                string.CompareOrdinal(first, second) < 0,
                first + " 应排在 " + second + " 之前");
        }

        [Fact]
        public void GeneratedGuids_ShouldCarryCurrentTimestamp()
        {
            var guid = ExtractGeneratedGuids()[0];
            var hex = guid.Replace("-", string.Empty).Substring(0, 12);
            var timestamp = Convert.ToInt64(hex, 16);
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            Assert.True(Math.Abs(now - timestamp) < 10_000, "前 48 位应为当前毫秒时间戳，实际 " + timestamp);
        }

        [Fact]
        public void SixDigitProvinceCode_ShouldBeRejectedInsteadOfSilentlyPadded()
        {
            var districts = new List<DistrictItem>
            {
                new DistrictItem { Code = "110000", Name = "北京市" }
            };

            var error = Assert.Throws<ArgumentException>(() => SqlGenerator.GenerateSql(districts));
            Assert.Contains("[0]", error.Message);
            Assert.Contains("2 位", error.Message);
        }

        [Fact]
        public void DuplicateCodeInSameLevel_ShouldBeRejected()
        {
            var districts = new List<DistrictItem>
            {
                new DistrictItem { Code = "11", Name = "北京市" },
                new DistrictItem { Code = "11", Name = "重复的省" }
            };

            var error = Assert.Throws<ArgumentException>(() => SqlGenerator.GenerateSql(districts));
            Assert.Contains("重复", error.Message);
        }

        [Fact]
        public void FifthLevelNode_ShouldBeRejected()
        {
            var district = NestedToDepth(5);

            var error = Assert.Throws<ArgumentException>(() => SqlGenerator.GenerateSql(new List<DistrictItem> { district }));
            Assert.Contains("第 5 级", error.Message);
        }

        [Fact]
        public void MissingName_ShouldBeRejected()
        {
            var districts = new List<DistrictItem>
            {
                new DistrictItem { Code = "11", Name = " " }
            };

            Assert.Throws<ArgumentException>(() => SqlGenerator.GenerateSql(districts));
        }

        [Fact]
        public void WriteSqlFile_ShouldNotEmitUtf8Bom()
        {
            var path = Path.Combine(Path.GetTempPath(), "djs-" + Guid.NewGuid().ToString("N") + ".sql");
            try
            {
                SqlGenerator.WriteSqlFile(path, "SELECT 1;");

                var bytes = File.ReadAllBytes(path);

                Assert.False(
                    bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF,
                    "文件不应带 UTF-8 BOM");
                Assert.Equal((byte)'S', bytes[0]);
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        /// <summary>从生成的 SQL 中按出现顺序取出 guid 字面量。</summary>
        private static List<string> ExtractGeneratedGuids()
        {
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

            var sql = SqlGenerator.GenerateSql(new List<DistrictItem> { province });

            return Regex
                .Matches(sql, "'([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})'::uuid")
                .Cast<Match>()
                .Select(m => m.Groups[1].Value)
                .ToList();
        }

        /// <summary>构造一条到指定层级（1=省）的单链节点。</summary>
        private static DistrictItem NestedToDepth(int depth)
        {
            var lengths = new[] { 2, 4, 6, 9, 12 };
            DistrictItem root = null;
            DistrictItem current = null;

            for (var level = 1; level <= depth; level++)
            {
                var node = new DistrictItem
                {
                    Code = new string('1', lengths[level - 1]),
                    Name = "第" + level + "级"
                };

                if (root == null)
                {
                    root = node;
                }
                else
                {
                    current.Children = new List<DistrictItem> { node };
                }

                current = node;
            }

            return root;
        }
    }
}
