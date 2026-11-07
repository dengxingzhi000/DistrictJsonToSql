using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace DistrictJsonToSql
{
    public partial class App : Application
    {
        private Window _window;
        private IServiceProvider _services;

        public App()
        {
            InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            var services = new ServiceCollection();
            services.AddSingleton<IDistrictReader, JsonDistrictReader>();
            services.AddSingleton<FilePickerService>();
            services.AddSingleton<IFilePickerService>(sp => sp.GetRequiredService<FilePickerService>());
            services.AddSingleton<DialogService>();
            services.AddSingleton<IDialogService>(sp => sp.GetRequiredService<DialogService>());
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<MainWindow>();

            _services = services.BuildServiceProvider();
            _window = _services.GetRequiredService<MainWindow>();
            _window.Activate();
        }
    }
}
