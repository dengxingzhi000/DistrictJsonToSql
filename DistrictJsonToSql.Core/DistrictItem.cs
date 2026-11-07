using System.Collections.Generic;
using Newtonsoft.Json;

namespace DistrictJsonToSql
{
    /// <summary>
    /// 行政区划节点，对应 pcas-code.json 中的一层数据。
    /// </summary>
    public class DistrictItem
    {
        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("children")]
        public List<DistrictItem> Children { get; set; }
    }
}
