using System;
using System.Threading.Tasks;
using Windows.Storage.Pickers;

namespace DistrictJsonToSql
{
    /// <summary>WinUI 文件选择器；unpackaged 应用需要窗口句柄，构造窗口后调用 AttachWindow。</summary>
    public sealed class FilePickerService : IFilePickerService
    {
        private nint _windowHandle;

        public void AttachWindow(nint windowHandle)
        {
            _windowHandle = windowHandle;
        }

        public async Task<string> PickJsonFileAsync()
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".json");
            picker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
            WinRT.Interop.InitializeWithWindow.Initialize(picker, _windowHandle);

            var file = await picker.PickSingleFileAsync();
            return file == null ? null : file.Path;
        }
    }
}
