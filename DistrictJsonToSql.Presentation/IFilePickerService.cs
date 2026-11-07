using System.Threading.Tasks;

namespace DistrictJsonToSql
{
    /// <summary>
    /// 选择输入文件的平台能力；实现由 View 层提供（需要窗口句柄）。
    /// </summary>
    public interface IFilePickerService
    {
        /// <summary>弹出文件选择框；用户取消时返回 null。</summary>
        Task<string> PickJsonFileAsync();
    }
}
