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
                // 预览统一用 CRLF 展示，文件里批次内部仍是 \n；按行尾归一后内容一致
                Assert.Equal(
                    NormalizeLineEndings(viewModel.SqlText),
                    NormalizeLineEndings(File.ReadAllText(sqlPath)));
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
        public async Task Generate_LargeOutput_ShowsPreviewTruncationNotice()
        {
            var directory = CreateTempDirectory();
            try
            {
                var json = new System.Text.StringBuilder("[{\"code\":\"11\",\"name\":\"省\",\"children\":[");
                for (var i = 0; i < 300; i++)
                {
                    if (i > 0)
                    {
                        json.Append(',');
                    }

                    json.Append("{\"code\":\"").Append((i + 1).ToString("D4")).Append("\",\"name\":\"市").Append(i).Append("\"}");
                }

                json.Append("]}]");
                var jsonPath = WriteJson(directory, json.ToString());
                var viewModel = CreateViewModel(out var picker, out var dialogs);
                picker.PathToReturn = jsonPath;
                await viewModel.SelectFileCommand.ExecuteAsync(null);

                await viewModel.GenerateSqlCommand.ExecuteAsync(null);

                // 301 个节点 / 4 个批次：共 312 行，只预览前 200 行
                Assert.True(viewModel.HasOutput);
                Assert.True(viewModel.IsPreviewTruncated);
                Assert.Equal(312, viewModel.TotalLineCount);
                Assert.Contains("仅预览前 200 行", viewModel.PreviewNotice);
                Assert.Equal(200, CountLines(viewModel.SqlText));
                Assert.False(viewModel.IsBusy);
                Assert.Empty(dialogs.Errors);

                var sqlPath = Path.ChangeExtension(jsonPath, ".sql");
                Assert.True(File.Exists(sqlPath));
                Assert.Equal(312, CountLines(File.ReadAllText(sqlPath)));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Fact]
        public async Task Generate_InvalidBatchSize_ShowsParameterErrorWithoutWriting()
        {
            var directory = CreateTempDirectory();
            try
            {
                var jsonPath = WriteJson(directory, "[{\"code\":\"11\",\"name\":\"北京市\"}]");
                var viewModel = CreateViewModel(out var picker, out var dialogs);
                picker.PathToReturn = jsonPath;
                await viewModel.SelectFileCommand.ExecuteAsync(null);
                viewModel.BatchSizeText = "0";

                await viewModel.GenerateSqlCommand.ExecuteAsync(null);

                Assert.False(viewModel.HasOutput);
                Assert.False(viewModel.IsBusy);
                Assert.Equal(StatusSeverity.Error, viewModel.StatusSeverity);
                Assert.Equal("参数不合法", viewModel.StatusTitle);
                Assert.Empty(dialogs.Errors);
                Assert.False(File.Exists(Path.ChangeExtension(jsonPath, ".sql")));
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

        [Fact]
        public async Task Generate_CollectAllErrors_ShowsAggregatedDialog()
        {
            var directory = CreateTempDirectory();
            try
            {
                // 两个独立问题：[0] code 位数不对；[2] code 与 [1] 重复
                var jsonPath = WriteJson(
                    directory,
                    "[{\"code\":\"1\",\"name\":\"甲\"},{\"code\":\"11\",\"name\":\"乙\"},{\"code\":\"11\",\"name\":\"丙\"}]");
                var viewModel = CreateViewModel(out var picker, out var dialogs);
                viewModel.CollectAllErrors = true;
                picker.PathToReturn = jsonPath;
                await viewModel.SelectFileCommand.ExecuteAsync(null);

                await viewModel.GenerateSqlCommand.ExecuteAsync(null);

                Assert.False(viewModel.HasOutput);
                Assert.Equal(StatusSeverity.Error, viewModel.StatusSeverity);
                Assert.Equal("数据校验失败", viewModel.StatusTitle);
                Assert.Contains("共 2 处问题", viewModel.StatusMessage);

                var error = Assert.Single(dialogs.Errors);
                Assert.Equal("数据校验失败", error.Title);
                Assert.Contains("2 处不合法", error.Message);
                Assert.Contains("[0]", error.Message);
                Assert.Contains("[2]", error.Message);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        private static int CountLines(string text)
        {
            return text.Replace("\r\n", "\n").Split('\n').Length - 1;
        }

        private static string NormalizeLineEndings(string text)
        {
            return text.Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
        }
    }
}
