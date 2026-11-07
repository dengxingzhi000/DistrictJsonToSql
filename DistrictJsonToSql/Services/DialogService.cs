using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DistrictJsonToSql
{
    /// <summary>ContentDialog 实现；窗口加载后调用 AttachXamlRoot 注入 XamlRoot。</summary>
    public sealed class DialogService : IDialogService
    {
        private XamlRoot _xamlRoot;

        public void AttachXamlRoot(XamlRoot xamlRoot)
        {
            _xamlRoot = xamlRoot;
        }

        public async Task ShowErrorAsync(string title, string message)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                CloseButtonText = "知道了",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = _xamlRoot,
            };

            await dialog.ShowAsync();
        }
    }
}
