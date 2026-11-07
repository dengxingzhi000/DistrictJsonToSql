namespace DistrictJsonToSql
{
    /// <summary>
    /// 批量写入 SQL 文件的结果：
    /// <see cref="TotalLines"/> 是完整输出（不是预览）的行数；<see cref="PreviewText"/> 最多保留调用方指定的行数。
    /// </summary>
    public sealed class SqlGenerationResult
    {
        public SqlGenerationResult(int nodeCount, int totalLines, string previewText, bool previewTruncated)
        {
            NodeCount = nodeCount;
            TotalLines = totalLines;
            PreviewText = previewText;
            PreviewTruncated = previewTruncated;
        }

        public int NodeCount { get; }

        public int TotalLines { get; }

        public string PreviewText { get; }

        public bool PreviewTruncated { get; }
    }
}
