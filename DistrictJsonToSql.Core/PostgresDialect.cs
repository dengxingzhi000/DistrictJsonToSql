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
        private readonly OnConflictMode _onConflict;

        public PostgresDialect(GeneratorOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _options.Validate();
            _onConflict = OnConflictModes.Parse(_options.OnConflict);
        }

        public int BatchSize
        {
            get { return _options.BatchSize; }
        }

        public string BuildInsertHeader()
        {
            var columns = _options.Columns;
            var header = "INSERT INTO " + _options.TableName + " ("
                         + columns.Guid + ", " + columns.Code + ", \"" + columns.Name + "\", "
                         + columns.DistrictType + ", " + columns.ParentCode + ") VALUES";

            switch (_onConflict)
            {
                case OnConflictMode.DoNothing:
                    // 不带冲突目标：即使表上没有 code 的唯一索引也能执行
                    return header + " ON CONFLICT DO NOTHING";

                case OnConflictMode.DoUpdate:
                    return header + " ON CONFLICT (" + columns.Code + ") DO UPDATE SET \""
                           + columns.Name + "\" = EXCLUDED.\"" + columns.Name + "\", "
                           + columns.DistrictType + " = EXCLUDED." + columns.DistrictType + ", "
                           + columns.ParentCode + " = EXCLUDED." + columns.ParentCode;

                default:
                    return header;
            }
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
