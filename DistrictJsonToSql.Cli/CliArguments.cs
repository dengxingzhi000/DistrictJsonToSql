using System;
using System.Collections.Generic;

namespace DistrictJsonToSql.Cli
{
    /// <summary>命令行参数解析结果。</summary>
    internal sealed class CliArguments
    {
        public bool ShowHelp { get; private set; }

        public string InputPath { get; private set; }

        public string OutputPath { get; private set; }

        public string TableName { get; private set; }

        public int? BatchSize { get; private set; }

        /// <summary>为真时一次性汇总全部校验错误（默认仍首错即抛）。</summary>
        public bool AllErrors { get; private set; }

        /// <summary>为真时用 BEGIN / COMMIT 包裹脚本。</summary>
        public bool Transaction { get; private set; }

        /// <summary>冲突处理策略，取值 none / doNothing / doUpdate。</summary>
        public string OnConflict { get; private set; } = OnConflictModes.NoneText;

        public static CliArguments Parse(string[] args)
        {
            var result = new CliArguments();

            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                switch (arg)
                {
                    case "-h":
                    case "--help":
                        result.ShowHelp = true;
                        return result;

                    case "-i":
                    case "--input":
                        result.InputPath = TakeValue(args, ref i, arg);
                        break;

                    case "-o":
                    case "--output":
                        result.OutputPath = TakeValue(args, ref i, arg);
                        break;

                    case "-t":
                    case "--table":
                        result.TableName = TakeValue(args, ref i, arg);
                        if (string.IsNullOrWhiteSpace(result.TableName))
                        {
                            throw new UsageException("--table 不能为空");
                        }

                        break;

                    case "-b":
                    case "--batch":
                        var raw = TakeValue(args, ref i, arg);
                        if (!int.TryParse(raw, out var batch) || batch < 1)
                        {
                            throw new UsageException("--batch 必须是大于 0 的整数，实际为：" + raw);
                        }

                        result.BatchSize = batch;
                        break;

                    case "--all-errors":
                        result.AllErrors = true;
                        break;

                    case "--transaction":
                        result.Transaction = true;
                        break;

                    case "--on-conflict":
                        var rawConflict = TakeValue(args, ref i, arg);
                        if (!OnConflictModes.TryParse(rawConflict, out var conflict))
                        {
                            throw new UsageException("--on-conflict 只能是 none / doNothing / doUpdate，实际为：" + rawConflict);
                        }

                        result.OnConflict = OnConflictModes.ToText(conflict);
                        break;

                    default:
                        throw new UsageException("未知参数：" + arg);
                }
            }

            if (string.IsNullOrWhiteSpace(result.InputPath))
            {
                throw new UsageException("缺少必填参数 --input");
            }

            if (string.IsNullOrWhiteSpace(result.OutputPath))
            {
                throw new UsageException("缺少必填参数 --output");
            }

            return result;
        }

        private static string TakeValue(string[] args, ref int index, string name)
        {
            if (index + 1 >= args.Length)
            {
                throw new UsageException(name + " 缺少取值");
            }

            index++;
            return args[index];
        }
    }

    /// <summary>参数错误，映射到退出码 2。</summary>
    internal sealed class UsageException : Exception
    {
        public UsageException(string message)
            : base(message)
        {
        }
    }
}
