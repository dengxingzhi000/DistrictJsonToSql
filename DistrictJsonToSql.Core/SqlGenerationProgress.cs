namespace DistrictJsonToSql
{
    /// <summary>生成进度：已处理节点数 / 总节点数。</summary>
    public sealed class SqlGenerationProgress
    {
        public SqlGenerationProgress(int nodesDone, int nodesTotal)
        {
            NodesDone = nodesDone;
            NodesTotal = nodesTotal;
        }

        public int NodesDone { get; }

        public int NodesTotal { get; }
    }
}
