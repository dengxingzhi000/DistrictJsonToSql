using System;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DistrictJsonToSql
{
    /// <summary>
    /// 主窗口的视图模型：选择文件、读取、生成、写出 SQL 与状态反馈。
    /// 不引用任何 WinUI 类型，便于在无 UI 的测试项目里直接构造。
    /// </summary>
    public partial class MainViewModel : ObservableObject
    {
        private readonly IDistrictReader _reader;
        private readonly IFilePickerService _filePicker;
        private readonly IDialogService _dialogs;

        public MainViewModel(IDistrictReader reader, IFilePickerService filePicker, IDialogService dialogs)
        {
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
            _filePicker = filePicker ?? throw new ArgumentNullException(nameof(filePicker));
            _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        }

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(GenerateSqlCommand))]
        private string _selectedFilePath;

        [ObservableProperty]
        private string _sqlText = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowOutput))]
        [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
        private bool _hasOutput;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SelectFileCommand))]
        [NotifyCanExecuteChangedFor(nameof(GenerateSqlCommand))]
        private bool _isBusy;

        [ObservableProperty]
        private bool _isStatusOpen;

        [ObservableProperty]
        private string _statusTitle = string.Empty;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private StatusSeverity _statusSeverity = StatusSeverity.Informational;

        public bool ShowOutput => HasOutput;

        public bool ShowEmptyState => !HasOutput;

        private bool CanSelectFile() => !IsBusy;

        private bool CanGenerateSql() => !IsBusy && SelectedFilePath != null;

        [RelayCommand(CanExecute = nameof(CanSelectFile))]
        private async Task SelectFileAsync()
        {
            var path = await _filePicker.PickJsonFileAsync();
            if (path == null)
            {
                return;
            }

            SelectedFilePath = path;
            HasOutput = false;
            SqlText = string.Empty;
            ShowStatus(StatusSeverity.Informational, "文件已选择", "点击「生成 SQL」开始生成");
        }

        [RelayCommand(CanExecute = nameof(CanGenerateSql))]
        private async Task GenerateSqlAsync()
        {
            var path = SelectedFilePath;
            if (path == null)
            {
                return;
            }

            IsBusy = true;
            ShowStatus(StatusSeverity.Informational, "正在读取 JSON", Path.GetFileName(path));

            try
            {
                var districts = await Task.Run(() => _reader.Read(path));
                if (districts.Count == 0)
                {
                    ShowStatus(StatusSeverity.Warning, "没有可生成的数据", "JSON 文件里没有行政区划数据");
                    return;
                }

                ShowStatus(StatusSeverity.Informational, "正在生成 SQL", $"共 {districts.Count} 个行政区划节点，正在生成…");
                var sql = await Task.Run(() => SqlGenerator.GenerateSql(districts));

                var sqlFileName = Path.ChangeExtension(path, ".sql");
                SqlGenerator.WriteSqlFile(sqlFileName, sql);

                SqlText = sql;
                HasOutput = true;
                ShowStatus(StatusSeverity.Success, "生成完成", sqlFileName);
            }
            catch (DistrictValidationException ex)
            {
                ErrorLogger.Log("validate " + ex.NodePath, ex);
                ShowStatus(StatusSeverity.Error, "数据校验失败", "位置 " + ex.NodePath);
                await _dialogs.ShowErrorAsync(
                    "数据校验失败",
                    $"行政区划数据不合法（位置 {ex.NodePath}）：{ex.Message}\n\n详细信息见：{ErrorLogger.LogPath}");
            }
            catch (Exception ex)
            {
                ErrorLogger.Log("generate", ex);
                ShowStatus(StatusSeverity.Error, "生成失败", ex.Message);
                await _dialogs.ShowErrorAsync(
                    "错误",
                    $"生成 SQL 时出错：{ex.Message}\n\n详细信息见：{ErrorLogger.LogPath}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ShowStatus(StatusSeverity severity, string title, string message)
        {
            StatusSeverity = severity;
            StatusTitle = title;
            StatusMessage = message;
            IsStatusOpen = true;
        }
    }
}
