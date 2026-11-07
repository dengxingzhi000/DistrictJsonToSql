using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Storage.Pickers;

namespace DistrictJsonToSql
{
    public sealed partial class MainWindow : Window
    {
        private const int InitialWidth = 1100;
        private const int InitialHeight = 780;
        private const int MinWidth = 820;
        private const int MinHeight = 620;

        private string _selectedPath;

        public MainWindow()
        {
            InitializeComponent();

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);

            AppWindow.Resize(new Windows.Graphics.SizeInt32(InitialWidth, InitialHeight));
            if (AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.PreferredMinimumWidth = MinWidth;
                presenter.PreferredMinimumHeight = MinHeight;
            }

            ApplyBackdrop();
        }

        /// <summary>
        /// Win11 用 Mica；Win10 1809 起回退到亚克力；都不支持时保留主题背景色。
        /// </summary>
        private void ApplyBackdrop()
        {
            if (MicaController.IsSupported())
            {
                SystemBackdrop = new MicaBackdrop();
                RootGrid.ClearValue(Panel.BackgroundProperty);
            }
            else if (DesktopAcrylicController.IsSupported())
            {
                SystemBackdrop = new DesktopAcrylicBackdrop();
                RootGrid.ClearValue(Panel.BackgroundProperty);
            }
        }

        private async void OnSelectFileClick(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".json");
            picker.SuggestedStartLocation = PickerLocationId.ComputerFolder;

            // unpackaged 应用必须显式绑定窗口句柄
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSingleFileAsync();
            if (file == null)
            {
                return;
            }

            _selectedPath = file.Path;
            FilePathBox.Text = _selectedPath;
            GenerateButton.IsEnabled = true;
            ShowEmptyState();
            ShowStatus(InfoBarSeverity.Informational, "文件已选择", "点击「生成 SQL」开始生成");
        }

        private async void OnGenerateClick(object sender, RoutedEventArgs e)
        {
            if (_selectedPath == null)
            {
                return;
            }

            var path = _selectedPath;
            SetBusy(true);
            ShowStatus(InfoBarSeverity.Informational, "正在读取 JSON", Path.GetFileName(path));

            try
            {
                var districts = await Task.Run(() => new JsonDistrictReader().Read(path));
                if (districts.Count == 0)
                {
                    ShowStatus(InfoBarSeverity.Warning, "没有可生成的数据", "JSON 文件里没有行政区划数据");
                    return;
                }

                ShowStatus(InfoBarSeverity.Informational, "正在生成 SQL", $"共 {districts.Count} 个行政区划节点，正在生成…");
                var sql = await Task.Run(() => SqlGenerator.GenerateSql(districts));

                var sqlFileName = Path.ChangeExtension(path, ".sql");
                SqlGenerator.WriteSqlFile(sqlFileName, sql);

                ShowOutput(sql);
                ShowStatus(InfoBarSeverity.Success, "生成完成", sqlFileName);
            }
            catch (DistrictValidationException ex)
            {
                ErrorLogger.Log("validate " + ex.NodePath, ex);
                ShowStatus(InfoBarSeverity.Error, "数据校验失败", "位置 " + ex.NodePath);
                await ShowErrorDialogAsync(
                    "数据校验失败",
                    $"行政区划数据不合法（位置 {ex.NodePath}）：{ex.Message}\n\n详细信息见：{ErrorLogger.LogPath}");
            }
            catch (Exception ex)
            {
                ErrorLogger.Log("generate", ex);
                ShowStatus(InfoBarSeverity.Error, "生成失败", ex.Message);
                await ShowErrorDialogAsync(
                    "错误",
                    $"生成 SQL 时出错：{ex.Message}\n\n详细信息见：{ErrorLogger.LogPath}");
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void SetBusy(bool busy)
        {
            BusyRing.IsActive = busy;
            SelectButton.IsEnabled = !busy;
            GenerateButton.IsEnabled = !busy && _selectedPath != null;
        }

        private void ShowEmptyState()
        {
            EmptyState.Visibility = Visibility.Visible;
            SqlOutput.Visibility = Visibility.Collapsed;
            SqlOutput.Text = string.Empty;
        }

        private void ShowOutput(string sql)
        {
            SqlOutput.Text = sql;
            EmptyState.Visibility = Visibility.Collapsed;
            SqlOutput.Visibility = Visibility.Visible;
        }

        private void ShowStatus(InfoBarSeverity severity, string title, string message)
        {
            StatusBar.Severity = severity;
            StatusBar.Title = title;
            StatusBar.Message = message;
            StatusBar.IsOpen = true;
        }

        private async Task ShowErrorDialogAsync(string title, string message)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                CloseButtonText = "知道了",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = RootGrid.XamlRoot,
            };

            await dialog.ShowAsync();
        }
    }
}
