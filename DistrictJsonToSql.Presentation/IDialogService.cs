using System.Threading.Tasks;

namespace DistrictJsonToSql
{
    /// <summary>
    /// 错误提示对话框的平台能力；实现由 View 层提供（需要 XamlRoot）。
    /// </summary>
    public interface IDialogService
    {
        Task ShowErrorAsync(string title, string message);
    }
}
