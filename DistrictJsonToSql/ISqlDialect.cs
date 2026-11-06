namespace DistrictJsonToSql
{
    /// <summary>
    /// 目标数据库方言的最小扩展点：只描述“INSERT 头部长什么样”和“一行 VALUES 怎么拼”。
    /// 目前只有 PostgreSQL 实现，不预置插件注册机制。
    /// </summary>
    public interface ISqlDialect
    {
        int BatchSize { get; }

        string BuildInsertHeader();

        string BuildValueTuple(string guid, string code, string name, int districtType, string parentCode);
    }
}
