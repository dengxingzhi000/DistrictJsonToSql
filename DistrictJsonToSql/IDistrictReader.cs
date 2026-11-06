using System.Collections.Generic;

namespace DistrictJsonToSql
{
    /// <summary>
    /// 输入适配层：把某种文件格式读成统一的行政区划节点树。
    /// 目前只有 JSON 实现；新增格式时实现该接口并在入口处替换即可。
    /// </summary>
    public interface IDistrictReader
    {
        IReadOnlyList<DistrictItem> Read(string path);
    }
}
