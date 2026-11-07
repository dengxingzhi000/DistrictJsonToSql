using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace DistrictJsonToSql
{
    public sealed partial class MainWindow : Window
    {
        private const int InitialWidth = 1100;
        private const int InitialHeight = 780;
        private const int MinWidth = 820;
        private const int MinHeight = 620;

        public MainViewModel ViewModel { get; }

        public MainWindow(MainViewModel viewModel, FilePickerService filePicker, DialogService dialogService)
        {
            ViewModel = viewModel;
            InitializeComponent();
            // Window 不是 FrameworkElement，没有 DataContext；绑定挂在根 Grid 上
            RootGrid.DataContext = viewModel;

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);

            ApplyBackdrop();

            filePicker.AttachWindow(WinRT.Interop.WindowNative.GetWindowHandle(this));
            RootGrid.Loaded += (sender, e) =>
            {
                dialogService.AttachXamlRoot(RootGrid.XamlRoot);
                ApplyDpiAwareSizing();
            };
        }

        /// <summary>
        /// AppWindow 的尺寸单位是物理像素。按当前显示器缩放做换算，保证 125%/150% 下
        /// 初始窗口与最小窗口的逻辑尺寸和 100% 时一致，不会一打开就被挤到最小尺寸。
        /// </summary>
        private void ApplyDpiAwareSizing()
        {
            var scale = RootGrid.XamlRoot == null ? 1.0 : RootGrid.XamlRoot.RasterizationScale;
            if (double.IsNaN(scale) || scale <= 0)
            {
                scale = 1.0;
            }

            AppWindow.Resize(new Windows.Graphics.SizeInt32(
                (int)System.Math.Round(InitialWidth * scale),
                (int)System.Math.Round(InitialHeight * scale)));

            if (AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.PreferredMinimumWidth = (int)System.Math.Round(MinWidth * scale);
                presenter.PreferredMinimumHeight = (int)System.Math.Round(MinHeight * scale);
            }
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
    }
}
