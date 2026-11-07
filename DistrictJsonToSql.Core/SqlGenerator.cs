using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DistrictJsonToSql
{
    /// <summary>
    /// 行政区划 JSON 到 INSERT 脚本的纯转换逻辑。
    /// 不依赖 WinForms；方言由 <see cref="ISqlDialect"/> 决定，表名/列名/批量大小由配置决定。
    /// </summary>
    public static class SqlGenerator
    {
        public const int DefaultBatchSize = 100;

        /// <summary>省 / 市 / 区县 / 街道的 code 长度（pcas-code.json 的实际约定）。</summary>
        private static readonly int[] ExpectedCodeLengths = { 2, 4, 6, 9 };

        private const int MaxDepth = 4;

        /// <summary>使用 exe 同目录的默认配置生成 SQL。</summary>
        public static string GenerateSql(IEnumerable<DistrictItem> districts)
        {
            return GenerateSql(districts, CreateDialect(GeneratorOptions.LoadDefault()));
        }

        public static string GenerateSql(IEnumerable<DistrictItem> districts, ISqlDialect dialect)
        {
            if (districts == null)
            {
                throw new ArgumentNullException(nameof(districts));
            }

            if (dialect == null)
            {
                throw new ArgumentNullException(nameof(dialect));
            }

            var items = districts.ToList();
            Validate(items);

            var tuples = new List<string>();
            foreach (var item in items)
            {
                tuples.AddRange(Collect(item, 1, null, dialect));
            }

            var sqlBuilder = new StringBuilder();
            sqlBuilder.AppendLine("-- 地域数据插入SQL");
            sqlBuilder.AppendLine("-- 生成时间: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sqlBuilder.AppendLine();

            for (var i = 0; i < tuples.Count; i += dialect.BatchSize)
            {
                var batch = tuples.Skip(i).Take(dialect.BatchSize);
                sqlBuilder.AppendLine(dialect.BuildInsertHeader());
                sqlBuilder.AppendLine(string.Join(",\n", batch) + ";");
                sqlBuilder.AppendLine();
            }

            return sqlBuilder.ToString();
        }

        /// <summary>
        /// 边生成边按批写入文件，不在内存里保留整份 SQL 文本；返回节点数、实际行数与前若干行预览。
        /// 输出与 <see cref="GenerateSql(IEnumerable{DistrictItem}, ISqlDialect)"/> 逐字节一致（行尾、批次分隔、末尾空行都相同）。
        /// </summary>
        /// <param name="previewLineLimit">预览保留的最大行数；0 表示不保留预览。写盘始终是全量。</param>
        public static SqlGenerationResult GenerateSqlToFile(
            IEnumerable<DistrictItem> districts,
            string sqlFilePath,
            ISqlDialect dialect,
            int previewLineLimit,
            IProgress<SqlGenerationProgress> progress)
        {
            if (districts == null)
            {
                throw new ArgumentNullException(nameof(districts));
            }

            if (dialect == null)
            {
                throw new ArgumentNullException(nameof(dialect));
            }

            if (string.IsNullOrWhiteSpace(sqlFilePath))
            {
                throw new ArgumentException("输出文件路径不能为空", nameof(sqlFilePath));
            }

            if (previewLineLimit < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(previewLineLimit), "预览行数不能为负数");
            }

            var items = districts.ToList();
            Validate(items);

            var nodesTotal = CountNodes(items);
            var nodesDone = 0;
            var totalLines = 0;
            var previewLines = 0;
            var preview = new StringBuilder();
            var batch = new List<string>(dialect.BatchSize);

            using (var stream = new FileStream(sqlFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.NewLine = Environment.NewLine;

                // 预览只保留前 previewLineLimit 个物理行，用于界面展示
                void AppendPreview(string text)
                {
                    if (previewLines >= previewLineLimit)
                    {
                        return;
                    }

                    preview.Append(text).Append(Environment.NewLine);
                    previewLines++;
                }

                // 标题行、空行：一个物理行
                void WriteSimpleLine(string text = "")
                {
                    writer.WriteLine(text);
                    totalLines++;
                    AppendPreview(text);
                }

                void FlushBatch()
                {
                    if (batch.Count == 0)
                    {
                        return;
                    }

                    WriteSimpleLine(dialect.BuildInsertHeader());

                    // 与 GenerateSql 完全一致：批次内部用 \n 连接，最后一条加 ";" 后整体换行
                    writer.WriteLine(string.Join(",\n", batch) + ";");
                    totalLines += batch.Count;

                    for (var i = 0; i < batch.Count; i++)
                    {
                        AppendPreview(i == batch.Count - 1 ? batch[i] + ";" : batch[i] + ",");
                    }

                    WriteSimpleLine();
                    batch.Clear();
                }

                WriteSimpleLine("-- 地域数据插入SQL");
                WriteSimpleLine("-- 生成时间: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                WriteSimpleLine();

                foreach (var item in items)
                {
                    foreach (var tuple in Collect(item, 1, null, dialect))
                    {
                        batch.Add(tuple);
                        nodesDone++;

                        if (batch.Count == dialect.BatchSize)
                        {
                            FlushBatch();
                            progress?.Report(new SqlGenerationProgress(nodesDone, nodesTotal));
                        }
                    }
                }

                FlushBatch();
                progress?.Report(new SqlGenerationProgress(nodesDone, nodesTotal));
            }

            return new SqlGenerationResult(
                nodesDone,
                totalLines,
                preview.ToString(),
                totalLines > previewLineLimit);
        }

        public static ISqlDialect CreateDialect(GeneratorOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            options.Validate();
            return new PostgresDialect(options);
        }

        /// <summary>
        /// 把 SQL 脚本写入磁盘，覆盖同名 .sql 文件；不带 UTF-8 BOM，避免 psql 首行解析失败。
        /// </summary>
        public static void WriteSqlFile(string sqlFilePath, string sql)
        {
            File.WriteAllText(sqlFilePath, sql, new UTF8Encoding(false));
        }

        /// <summary>递归展开节点树，按深度优先逐条产出 VALUES 元组（用于大文件流式写入）。</summary>
        private static IEnumerable<string> Collect(DistrictItem node, int depth, string parentCode, ISqlDialect dialect)
        {
            var code = EffectiveCode(node, depth);
            yield return dialect.BuildValueTuple(Guid.CreateVersion7().ToString(), code, node.Name, depth, parentCode);

            if (node.Children == null)
            {
                yield break;
            }

            foreach (var child in node.Children)
            {
                foreach (var tuple in Collect(child, depth + 1, code, dialect))
                {
                    yield return tuple;
                }
            }
        }

        private static int CountNodes(List<DistrictItem> items)
        {
            var count = 0;
            foreach (var item in items)
            {
                count += CountNode(item);
            }

            return count;
        }

        private static int CountNode(DistrictItem node)
        {
            var count = 1;
            if (node.Children == null)
            {
                return count;
            }

            foreach (var child in node.Children)
            {
                count += CountNode(child);
            }

            return count;
        }

        private static string EffectiveCode(DistrictItem node, int depth)
        {
            if (depth == 1)
            {
                return node.Code + "0000";
            }

            return depth == 2 ? node.Code + "00" : node.Code;
        }

        private static void Validate(List<DistrictItem> items)
        {
            var seenCodes = new Dictionary<int, HashSet<string>>();
            for (var level = 1; level <= MaxDepth; level++)
            {
                seenCodes[level] = new HashSet<string>(StringComparer.Ordinal);
            }

            for (var i = 0; i < items.Count; i++)
            {
                ValidateNode(items[i], 1, "[" + i + "]", seenCodes);
            }
        }

        private static void ValidateNode(DistrictItem node, int depth, string path, Dictionary<int, HashSet<string>> seenCodes)
        {
            if (depth > MaxDepth)
            {
                throw new DistrictValidationException(
                    "行政区划最多支持 " + MaxDepth + " 级，却发现第 " + depth + " 级节点：" + path, path);
            }

            if (node == null)
            {
                throw new DistrictValidationException("第 " + depth + " 级存在空节点：" + path, path);
            }

            if (string.IsNullOrWhiteSpace(node.Name))
            {
                throw new DistrictValidationException("第 " + depth + " 级节点缺少 name：" + path + " code=" + node.Code, path);
            }

            if (node.Code == null || node.Code.Length != ExpectedCodeLengths[depth - 1] || !IsDigitsOnly(node.Code))
            {
                throw new DistrictValidationException(
                    "第 " + depth + " 级 code 应为 " + ExpectedCodeLengths[depth - 1] + " 位数字，实际为 "
                    + (node.Code == null ? "null" : node.Code.Length + " 位（" + node.Code + "）")
                    + "：" + path + " name=" + node.Name, path);
            }

            if (!seenCodes[depth].Add(node.Code))
            {
                throw new DistrictValidationException(
                    "第 " + depth + " 级 code 重复：" + path + " code=" + node.Code + " name=" + node.Name, path);
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
