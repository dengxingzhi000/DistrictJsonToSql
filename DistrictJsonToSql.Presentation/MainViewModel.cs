using System;
using System.Globalization;
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

            // 高级参数初始值仍来自 exe 同目录的同名配置，行为与之前一致
            var defaults = GeneratorOptions.LoadDefault();
            TableName = defaults.TableName;
            BatchSizeText = defaults.BatchSize.ToString(CultureInfo.InvariantCulture);
        }

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(GenerateSqlCommand))]
        [NotifyPropertyChangedFor(nameof(ShowGenerateHint))]
        private string _selectedFilePath;

        /// <summary>高级选项：目标表名。</summary>
        [ObservableProperty]
        private string _tableName = "public.tb_district";

        /// <summary>高级选项：每条 INSERT 的批量行数（保持字符串，便于直接绑定输入框）。</summary>
        [ObservableProperty]
        private string _batchSizeText = "100";

        [ObservableProperty]
        private string _sqlText = string.Empty;

        /// <summary>生成后写出的 SQL 文件完整路径，显示在结果区标题右侧。</summary>
        [ObservableProperty]
        private string _outputFilePath = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowOutput))]
        [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
        private bool _hasOutput;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowGenerateHint))]
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

        /// <summary>生成按钮不可用时，在操作区给出明确原因（而不是只有禁用态）。</summary>
        public bool ShowGenerateHint => !IsBusy && SelectedFilePath == null;

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
            OutputFilePath = string.Empty;
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

            GeneratorOptions options;
            try
            {
                options = BuildOptions();
            }
            catch (ArgumentException ex)
            {
                ShowStatus(StatusSeverity.Error, "参数不合法", ex.Message);
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
                var dialect = SqlGenerator.CreateDialect(options);
                var sql = await Task.Run(() => SqlGenerator.GenerateSql(districts, dialect));

                var sqlFileName = Path.ChangeExtension(path, ".sql");
                SqlGenerator.WriteSqlFile(sqlFileName, sql);

                SqlText = sql;
                HasOutput = true;
                OutputFilePath = sqlFileName;
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

        /// <summary>把高级选项校验并合并到配置；不合法时抛 <see cref="ArgumentException"/>（文案给用户看）。</summary>
        private GeneratorOptions BuildOptions()
        {
            var tableName = (TableName ?? string.Empty).Trim();
            if (tableName.Length == 0)
            {
                throw new ArgumentException("目标表名不能为空");
            }

            int batchSize;
            if (!int.TryParse((BatchSizeText ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out batchSize)
                || batchSize < 1)
            {
                throw new ArgumentException("每批 INSERT 行数必须是大于 0 的整数");
            }

            var options = GeneratorOptions.LoadDefault();
            options.TableName = tableName;
            options.BatchSize = batchSize;
            return options;
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
