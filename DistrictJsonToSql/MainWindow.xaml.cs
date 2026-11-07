using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Storage.Pickers;

namespace DistrictJsonToSql
{
    public sealed partial class MainWindow : Window
    {
        private string _selectedPath;

        public MainWindow()
        {
            InitializeComponent();

            // 默认窗口尺寸，保证输出区域有足够空间
            AppWindow.Resize(new Windows.Graphics.SizeInt32(1040, 800));
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
            SqlOutput.Text = string.Empty;
            GenerateButton.IsEnabled = true;
            ShowInfo(InfoBarSeverity.Informational, "文件已选择，点击「生成 SQL」");
        }

        private async void OnGenerateClick(object sender, RoutedEventArgs e)
        {
            if (_selectedPath == null)
            {
                return;
            }

            var path = _selectedPath;
            SetBusy(true);
            ShowInfo(InfoBarSeverity.Informational, "正在读取 JSON...");

            try
            {
                var districts = await Task.Run(() => new JsonDistrictReader().Read(path));
                if (districts.Count == 0)
                {
                    ShowInfo(InfoBarSeverity.Warning, "JSON 文件里没有行政区划数据");
                    return;
                }

                ShowInfo(InfoBarSeverity.Informational, "正在生成 SQL...");
                var sql = await Task.Run(() => SqlGenerator.GenerateSql(districts));
                SqlOutput.Text = sql;

                var sqlFileName = Path.ChangeExtension(path, ".sql");
                SqlGenerator.WriteSqlFile(sqlFileName, sql);

                ShowInfo(InfoBarSeverity.Success, $"SQL 生成完成，已保存到：{sqlFileName}");
            }
            catch (DistrictValidationException ex)
            {
                ErrorLogger.Log("validate " + ex.NodePath, ex);
                ShowInfo(InfoBarSeverity.Error, "数据校验失败，详见对话框");
                await ShowErrorDialogAsync(
                    "数据校验失败",
                    $"行政区划数据不合法（位置 {ex.NodePath}）：{ex.Message}\n\n详细信息见：{ErrorLogger.LogPath}");
            }
            catch (Exception ex)
            {
                ErrorLogger.Log("generate", ex);
                ShowInfo(InfoBarSeverity.Error, "生成失败，详见对话框");
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

        private void ShowInfo(InfoBarSeverity severity, string message)
        {
            StatusBar.Severity = severity;
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
