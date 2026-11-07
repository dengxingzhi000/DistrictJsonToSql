using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace DistrictJsonToSql.Tests
{
    /// <summary>
    /// MainViewModel 的行为用例：用假的文件选择器 / 对话框 / 读取器，不依赖 WinUI。
    /// 原有 23 个用例不做任何改动。
    /// </summary>
    public class MainViewModelTests
    {
        private sealed class FakeFilePicker : IFilePickerService
        {
            public string PathToReturn;

            public Task<string> PickJsonFileAsync()
            {
                return Task.FromResult(PathToReturn);
            }
        }

        private sealed class FakeDialogService : IDialogService
        {
            public readonly List<(string Title, string Message)> Errors = new List<(string, string)>();

            public Task ShowErrorAsync(string title, string message)
            {
                Errors.Add((title, message));
                return Task.CompletedTask;
            }
        }

        private sealed class FakeReader : IDistrictReader
        {
            private readonly Exception _error;

            public FakeReader(Exception error)
            {
                _error = error;
            }

            public IReadOnlyList<DistrictItem> Read(string path)
            {
                throw _error;
            }
        }

        private static MainViewModel CreateViewModel(out FakeFilePicker picker, out FakeDialogService dialogs, IDistrictReader reader = null)
        {
            picker = new FakeFilePicker();
            dialogs = new FakeDialogService();
            return new MainViewModel(reader ?? new JsonDistrictReader(), picker, dialogs);
        }

        private static string CreateTempDirectory()
        {
            var directory = Path.Combine(Path.GetTempPath(), "djs-vm-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static string WriteJson(string directory, string content)
        {
            var path = Path.Combine(directory, "pcas-code.json");
            File.WriteAllText(path, content);
            return path;
        }

        [Fact]
        public async Task SelectFile_WithPath_SetsSelectionAndInfoStatus()
        {
            var directory = CreateTempDirectory();
            try
            {
                var jsonPath = WriteJson(directory, "[{\"code\":\"11\",\"name\":\"北京市\"}]");
                var viewModel = CreateViewModel(out var picker, out var dialogs);
                picker.PathToReturn = jsonPath;

                Assert.False(viewModel.GenerateSqlCommand.CanExecute(null));

                await viewModel.SelectFileCommand.ExecuteAsync(null);

                Assert.Equal(jsonPath, viewModel.SelectedFilePath);
                Assert.True(viewModel.GenerateSqlCommand.CanExecute(null));
                Assert.True(viewModel.IsStatusOpen);
                Assert.Equal(StatusSeverity.Informational, viewModel.StatusSeverity);
                Assert.Equal("文件已选择", viewModel.StatusTitle);
                Assert.True(viewModel.ShowEmptyState);
                Assert.Empty(dialogs.Errors);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Fact]
        public async Task SelectFile_Cancelled_KeepsNoSelection()
        {
            var viewModel = CreateViewModel(out var picker, out _);
            picker.PathToReturn = null;

            await viewModel.SelectFileCommand.ExecuteAsync(null);

            Assert.Null(viewModel.SelectedFilePath);
            Assert.False(viewModel.GenerateSqlCommand.CanExecute(null));
            Assert.False(viewModel.IsStatusOpen);
        }

        [Fact]
        public async Task Generate_EmptyArray_ShowsWarningWithoutOutput()
        {
            var directory = CreateTempDirectory();
            try
            {
                var jsonPath = WriteJson(directory, "[]");
                var viewModel = CreateViewModel(out var picker, out var dialogs);
                picker.PathToReturn = jsonPath;
                await viewModel.SelectFileCommand.ExecuteAsync(null);

                await viewModel.GenerateSqlCommand.ExecuteAsync(null);

                Assert.False(viewModel.HasOutput);
                Assert.False(viewModel.IsBusy);
                Assert.Equal(StatusSeverity.Warning, viewModel.StatusSeverity);
                Assert.Equal("没有可生成的数据", viewModel.StatusTitle);
                Assert.Empty(dialogs.Errors);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Fact]
        public async Task Generate_ValidJson_WritesSqlFileAndShowsOutput()
        {
            var directory = CreateTempDirectory();
            try
            {
                var jsonPath = WriteJson(
                    directory,
                    "[{\"code\":\"11\",\"name\":\"北京市\",\"children\":[{\"code\":\"1101\",\"name\":\"市辖区\"}]}]");
                var viewModel = CreateViewModel(out var picker, out var dialogs);
                picker.PathToReturn = jsonPath;
                await viewModel.SelectFileCommand.ExecuteAsync(null);

                await viewModel.GenerateSqlCommand.ExecuteAsync(null);

                Assert.True(viewModel.HasOutput);
                Assert.True(viewModel.ShowOutput);
                Assert.False(viewModel.ShowEmptyState);
                Assert.False(viewModel.IsBusy);
                Assert.Contains("INSERT INTO", viewModel.SqlText);
                Assert.Equal(StatusSeverity.Success, viewModel.StatusSeverity);
                Assert.Empty(dialogs.Errors);

                var sqlPath = Path.ChangeExtension(jsonPath, ".sql");
                Assert.True(File.Exists(sqlPath));
                Assert.Equal(viewModel.SqlText, File.ReadAllText(sqlPath));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Fact]
        public async Task Generate_InvalidCode_ShowsErrorStatusAndDialog()
        {
            var directory = CreateTempDirectory();
            try
            {
                var jsonPath = WriteJson(directory, "[{\"code\":\"1\",\"name\":\"北京市\"}]");
                var viewModel = CreateViewModel(out var picker, out var dialogs);
                picker.PathToReturn = jsonPath;
                await viewModel.SelectFileCommand.ExecuteAsync(null);

                await viewModel.GenerateSqlCommand.ExecuteAsync(null);

                Assert.False(viewModel.HasOutput);
                Assert.False(viewModel.IsBusy);
                Assert.Equal(StatusSeverity.Error, viewModel.StatusSeverity);
                Assert.Equal("数据校验失败", viewModel.StatusTitle);
                var error = Assert.Single(dialogs.Errors);
                Assert.Equal("数据校验失败", error.Title);
                Assert.Contains("详细信息见", error.Message);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Fact]
        public async Task Generate_UnexpectedException_ShowsErrorDialog()
        {
            var directory = CreateTempDirectory();
            try
            {
                var jsonPath = WriteJson(directory, "[]");
                var viewModel = CreateViewModel(out var picker, out var dialogs, new FakeReader(new IOException("boom")));
                picker.PathToReturn = jsonPath;
                await viewModel.SelectFileCommand.ExecuteAsync(null);

                await viewModel.GenerateSqlCommand.ExecuteAsync(null);

                Assert.False(viewModel.HasOutput);
                Assert.Equal(StatusSeverity.Error, viewModel.StatusSeverity);
                Assert.Equal("生成失败", viewModel.StatusTitle);
                var error = Assert.Single(dialogs.Errors);
                Assert.Equal("错误", error.Title);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
