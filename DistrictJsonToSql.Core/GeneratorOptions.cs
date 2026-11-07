using System;
using System.IO;
using System.Text.Json;

namespace DistrictJsonToSql
{
    /// <summary>输出表的列名配置，默认值对应历史版本。</summary>
    public sealed class ColumnNames
    {
        public string Guid { get; set; } = "guid";
        public string Code { get; set; } = "code";
        public string Name { get; set; } = "name";
        public string DistrictType { get; set; } = "district_type";
        public string ParentCode { get; set; } = "parent_code";
    }

    /// <summary>
    /// 可外置的生成配置：放在 exe 同目录的 DistrictJsonToSql.json 即可覆盖默认值。
    /// </summary>
    public sealed class GeneratorOptions
    {
        public const string FileName = "DistrictJsonToSql.json";

        public string TableName { get; set; } = "public.tb_district";
        public int BatchSize { get; set; } = 100;
        public string Dialect { get; set; } = "postgres";
        public ColumnNames Columns { get; set; } = new ColumnNames();

        public static GeneratorOptions LoadDefault()
        {
            return LoadFrom(Path.Combine(AppContext.BaseDirectory, FileName));
        }

        public static GeneratorOptions LoadFrom(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return new GeneratorOptions();
            }

            var json = File.ReadAllText(path);
            var options = JsonSerializer.Deserialize<GeneratorOptions>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });

            if (options == null)
            {
                return new GeneratorOptions();
            }

            if (options.Columns == null)
            {
                options.Columns = new ColumnNames();
            }

            return options;
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(TableName))
            {
                throw new ArgumentException("配置项 tableName 不能为空");
            }

            if (BatchSize < 1)
            {
                throw new ArgumentException("配置项 batchSize 必须大于 0，当前为 " + BatchSize);
            }

            if (!string.Equals(Dialect, "postgres", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("暂不支持方言：" + Dialect + "（当前仅支持 postgres）");
            }

            if (string.IsNullOrWhiteSpace(Columns.Guid) || string.IsNullOrWhiteSpace(Columns.Code)
                || string.IsNullOrWhiteSpace(Columns.Name) || string.IsNullOrWhiteSpace(Columns.DistrictType)
                || string.IsNullOrWhiteSpace(Columns.ParentCode))
            {
                throw new ArgumentException("配置项 columns 的列名不能为空");
            }
        }
    }
}
