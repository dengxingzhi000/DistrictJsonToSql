using System;

namespace DistrictJsonToSql
{
    /// <summary>INSERT 冲突处理策略，对应 PostgreSQL 的 ON CONFLICT 子句。</summary>
    public enum OnConflictMode
    {
        /// <summary>不追加 ON CONFLICT（默认，输出与历史版本一致）。</summary>
        None,

        /// <summary>ON CONFLICT DO NOTHING：跳过已存在的行。</summary>
        DoNothing,

        /// <summary>ON CONFLICT (code) DO UPDATE：按 code 覆盖 name / district_type / parent_code。</summary>
        DoUpdate
    }

    /// <summary>配置与界面上的文本值（none / doNothing / doUpdate）与枚举之间的转换。</summary>
    public static class OnConflictModes
    {
        public const string NoneText = "none";
        public const string DoNothingText = "doNothing";
        public const string DoUpdateText = "doUpdate";

        /// <summary>大小写不敏感；null 视为 none；其它值返回 false（由调用方给出明确错误）。</summary>
        public static bool TryParse(string value, out OnConflictMode mode)
        {
            if (value == null || string.Equals(value, NoneText, StringComparison.OrdinalIgnoreCase))
            {
                mode = OnConflictMode.None;
                return true;
            }

            if (string.Equals(value, DoNothingText, StringComparison.OrdinalIgnoreCase))
            {
                mode = OnConflictMode.DoNothing;
                return true;
            }

            if (string.Equals(value, DoUpdateText, StringComparison.OrdinalIgnoreCase))
            {
                mode = OnConflictMode.DoUpdate;
                return true;
            }

            mode = OnConflictMode.None;
            return false;
        }

        /// <summary>无法识别时回退到 none；调用方已通过 <see cref="TryParse"/> 校验过配置。</summary>
        public static OnConflictMode Parse(string value)
        {
            TryParse(value, out var mode);
            return mode;
        }

        public static string ToText(OnConflictMode mode)
        {
            switch (mode)
            {
                case OnConflictMode.DoNothing:
                    return DoNothingText;
                case OnConflictMode.DoUpdate:
                    return DoUpdateText;
                default:
                    return NoneText;
            }
        }
    }
}
