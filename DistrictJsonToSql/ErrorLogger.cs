using System;
using System.IO;
using System.Text;

namespace DistrictJsonToSql
{
    /// <summary>
    /// 把失败详情落到 %LOCALAPPDATA%\DistrictJsonToSql\error.log，
    /// 界面只展示简短提示，排障时看日志即可。
    /// </summary>
    public static class ErrorLogger
    {
        public static string LogPath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "DistrictJsonToSql",
                    "error.log");
            }
        }

        public static void Log(string context, Exception error)
        {
            try
            {
                var directory = Path.GetDirectoryName(LogPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                           + " [" + context + "] "
                           + (error == null ? "unknown error" : error.ToString());

                File.AppendAllText(LogPath, line + Environment.NewLine, new UTF8Encoding(false));
            }
            catch
            {
                // 日志写入失败不能影响主流程，忽略即可
            }
        }
    }
}
