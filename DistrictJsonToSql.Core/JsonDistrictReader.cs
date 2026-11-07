using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace DistrictJsonToSql
{
    /// <summary>
    /// pcas-code.json 风格的读取器：UTF-8 JSON 数组，每项含 code / name / children。
    /// </summary>
    public sealed class JsonDistrictReader : IDistrictReader
    {
        public IReadOnlyList<DistrictItem> Read(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("文件路径不能为空", nameof(path));
            }

            var json = File.ReadAllText(path, Encoding.UTF8);
            var districts = JsonConvert.DeserializeObject<List<DistrictItem>>(json);

            if (districts == null)
            {
                throw new ArgumentException("JSON 根节点必须是行政区划数组：" + path);
            }

            return districts;
        }
    }
}
