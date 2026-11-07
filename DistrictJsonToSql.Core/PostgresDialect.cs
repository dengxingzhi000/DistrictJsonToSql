using System;

namespace DistrictJsonToSql
{
    /// <summary>
    /// PostgreSQL 方言：表名/列名来自配置，主键用 ::uuid 字面量，name 用双引号包裹。
    /// 默认配置下的输出与历史版本逐字节一致。
    /// </summary>
    public sealed class PostgresDialect : ISqlDialect
    {
        private readonly GeneratorOptions _options;

        public PostgresDialect(GeneratorOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _options.Validate();
        }

        public int BatchSize
        {
            get { return _options.BatchSize; }
        }

        public string BuildInsertHeader()
        {
            var columns = _options.Columns;
            return "INSERT INTO " + _options.TableName + " ("
                   + columns.Guid + ", " + columns.Code + ", \"" + columns.Name + "\", "
                   + columns.DistrictType + ", " + columns.ParentCode + ") VALUES";
        }

        public string BuildValueTuple(string guid, string code, string name, int districtType, string parentCode)
        {
            var parentValue = parentCode == null ? "NULL" : "'" + parentCode + "'";
            return "('" + guid + "'::uuid, '" + code + "', '" + Escape(name) + "', " + districtType + ", " + parentValue + ")";
        }

        /// <summary>SQL 标准转义：单引号加倍。</summary>
        private static string Escape(string value)
        {
            return value.Replace("'", "''");
        }
    }
}
