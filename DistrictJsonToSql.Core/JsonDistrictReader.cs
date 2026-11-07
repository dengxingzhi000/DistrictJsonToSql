using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace DistrictJsonToSql
{
    /// <summary>
    /// pcas-code.json 风格的读取器：UTF-8 JSON 数组，每项含 code / name / children。
    /// 用 JsonTextReader 直接流式反序列化，不再先把整份 JSON 复制成一个大字符串。
    /// 对象树保持一致，因此校验失败的 NodePath（如 [0].children[1]）与之前完全相同。
    /// </summary>
    public sealed class JsonDistrictReader : IDistrictReader
    {
        public IReadOnlyList<DistrictItem> Read(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("文件路径不能为空", nameof(path));
            }

            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var textReader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
            using (var jsonReader = new JsonTextReader(textReader))
            {
                var districts = new JsonSerializer().Deserialize<List<DistrictItem>>(jsonReader);

                if (districts == null)
                {
                    throw new ArgumentException("JSON 根节点必须是行政区划数组：" + path);
                }

                return districts;
            }
        }
    }
}
