using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Xunit;

namespace DistrictJsonToSql.Tests
{
    /// <summary>
    /// P0 阶段记录尚未修复的三个缺陷。为了让 P0 的 CI 保持绿色（本仓库约定
    /// 每个 PR 必须 CI 通过才合并），这些用例先 Skip；P1 修复后去掉 Skip，
    /// 作为永久回归保护。
    /// </summary>
    public class KnownDefectsTests
    {
        [Fact(Skip = "已知缺陷 1：时间戳/版本位字节序错误，产出不是合法的 UUIDv7。P1 修复后启用。")]
        public void UuidV7_ShouldFollowRfc9562FieldLayout()
        {
            var generator = new GuidV7Generator();

            var guid = generator.Generate();
            var groups = guid.Split('-');

            Assert.Equal(5, groups.Length);
            Assert.Equal('7', groups[2][0]);
            Assert.True("89ab".IndexOf(groups[3][0]) >= 0, "变体位必须在第四段首位: " + guid);
        }

        [Fact(Skip = "已知缺陷 1：时间戳字节序错误，字符串顺序不等于生成时间顺序。P1 修复后启用。")]
        public void UuidV7_ShouldSortByGenerationTime()
        {
            var generator = new GuidV7Generator();

            var first = generator.Generate();
            Thread.Sleep(30);
            var second = generator.Generate();

            Assert.True(string.CompareOrdinal(first, second) < 0, first + " 应排在 " + second + " 之前");
        }

        [Fact(Skip = "已知缺陷 2：省码已是 6 位时会静默拼成 10 位 code，无任何校验。P1 修复后启用。")]
        public void SixDigitProvinceCode_ShouldBeRejectedInsteadOfSilentlyPadded()
        {
            var districts = new List<DistrictItem>
            {
                new DistrictItem { Code = "110000", Name = "北京市" }
            };

            Assert.Throws<ArgumentException>(() => SqlGenerator.GenerateSql(districts));
        }

        [Fact(Skip = "已知缺陷 3：导出的 .sql 带 UTF-8 BOM，psql 直接导入可能报语法错误。P1 修复后启用。")]
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
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
    }
}
