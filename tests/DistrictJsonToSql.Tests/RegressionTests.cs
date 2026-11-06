using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Xunit;

namespace DistrictJsonToSql.Tests
{
    /// <summary>
    /// P1 修复的三个缺陷的回归用例（P0 阶段以 Skip 记录，现全部启用）。
    /// </summary>
    public class RegressionTests
    {
        [Fact]
        public void UuidV7_ShouldFollowRfc9562FieldLayout()
        {
            var generator = new GuidV7Generator();

            var guid = generator.Generate();
            var groups = guid.Split('-');

            Assert.Equal(5, groups.Length);
            Assert.Equal('7', groups[2][0]);
            Assert.True("89ab".IndexOf(groups[3][0]) >= 0, "变体位必须在第四段首位: " + guid);
        }

        [Fact]
        public void UuidV7_ShouldSortByGenerationTime()
        {
            var generator = new GuidV7Generator();

            var first = generator.Generate();
            Thread.Sleep(30);
            var second = generator.Generate();

            Assert.True(string.CompareOrdinal(first, second) < 0, first + " 应排在 " + second + " 之前");
        }

        [Fact]
        public void UuidV7_FirstSixBytes_ShouldCarryCurrentTimestamp()
        {
            var generator = new GuidV7Generator();

            var guid = generator.Generate();
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
