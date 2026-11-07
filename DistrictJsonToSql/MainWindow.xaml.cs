using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
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
            // x:Bind 在 InitializeComponent 里取值，必须先赋值
            ViewModel = viewModel;
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

            filePicker.AttachWindow(WinRT.Interop.WindowNative.GetWindowHandle(this));
            RootGrid.Loaded += (sender, e) => dialogService.AttachXamlRoot(RootGrid.XamlRoot);
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
