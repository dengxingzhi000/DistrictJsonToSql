using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace DistrictJsonToSql.Cli
{
    internal static class ExitCodes
    {
        public const int Success = 0;
        public const int Unexpected = 1;
        public const int Usage = 2;
        public const int InputRead = 3;
        public const int Validation = 4;
        public const int Write = 5;
    }

    public static class Program
    {
        private const string HelpText =
            "DistrictJsonToSql.Cli 2.2.0\n" +
            "把行政区划 JSON（pcas-code.json 风格）转换成 PostgreSQL INSERT 脚本。\n" +
            "\n" +
            "用法：\n" +
            "  DistrictJsonToSql.Cli --input <json> --output <sql> [--table <name>] [--batch <n>]\n" +
            "\n" +
            "参数：\n" +
            "  -i, --input    输入 JSON 文件（必填）\n" +
            "  -o, --output   输出 SQL 文件（必填，UTF-8 无 BOM）\n" +
            "  -t, --table    目标表名，默认 public.tb_district\n" +
            "  -b, --batch    每条 INSERT 的批量行数，默认 100（必须大于 0）\n" +
            "  -h, --help     显示本帮助\n" +
            "\n" +
            "退出码：\n" +
            "  0  成功\n" +
            "  1  未预期的错误\n" +
            "  2  参数错误\n" +
            "  3  输入读取失败\n" +
            "  4  数据校验失败\n" +
            "  5  输出写入失败\n";

        public static int Main(string[] args)
        {
            CliArguments arguments;
            try
            {
                arguments = CliArguments.Parse(args);
            }
            catch (UsageException ex)
            {
                Console.Error.WriteLine("参数错误：" + ex.Message);
                Console.Error.WriteLine("运行 DistrictJsonToSql.Cli --help 查看用法。");
                return ExitCodes.Usage;
            }

            if (arguments.ShowHelp)
            {
                Console.Out.Write(HelpText);
                return ExitCodes.Success;
            }

            var options = new GeneratorOptions();
            if (arguments.TableName != null)
            {
                options.TableName = arguments.TableName;
            }

            if (arguments.BatchSize.HasValue)
            {
                options.BatchSize = arguments.BatchSize.Value;
            }

            IReadOnlyList<DistrictItem> districts;
            try
            {
                districts = new JsonDistrictReader().Read(arguments.InputPath);
            }
            catch (Exception ex) when (IsInputFailure(ex))
            {
                Console.Error.WriteLine("读取输入失败：" + ex.Message);
                return ExitCodes.InputRead;
            }

            if (districts.Count == 0)
            {
                Console.Error.WriteLine("警告：输入里没有行政区划数据，将生成不含 INSERT 的空脚本。");
            }

            SqlGenerationResult result;
            try
            {
                var dialect = SqlGenerator.CreateDialect(options);
                // CLI 只需要写全量文件，不保留预览（previewLineLimit = 0）
                result = SqlGenerator.GenerateSqlToFile(districts, arguments.OutputPath, dialect, 0, null);
            }
            catch (DistrictValidationException ex)
            {
                Console.Error.WriteLine("校验失败：" + ex.Message);
                Console.Error.WriteLine("节点路径：" + ex.NodePath);
                return ExitCodes.Validation;
            }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine("配置错误：" + ex.Message);
                return ExitCodes.Usage;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Console.Error.WriteLine("写入输出失败：" + ex.Message);
                return ExitCodes.Write;
            }

            Console.Out.WriteLine("已生成 " + result.NodeCount + " 个行政区划节点：" + arguments.OutputPath);
            return ExitCodes.Success;
        }

        private static bool IsInputFailure(Exception ex)
        {
            return ex is IOException
                   || ex is UnauthorizedAccessException
                   || ex is JsonException
                   || ex is ArgumentException;
        }
    }
}
