using System;
using System.Collections.Generic;

namespace DistrictJsonToSql
{
    /// <summary>
    /// 输入数据校验失败。消息里带节点路径，便于定位是哪一条行政区划数据有问题。
    /// 默认只报第一条（<see cref="ArgumentException.Message"/> 与历史版本一致）；
    /// 汇总模式下 <see cref="Issues"/> 含全部问题，首条仍与默认模式完全相同。
    /// </summary>
    public sealed class DistrictValidationException : ArgumentException
    {
        public DistrictValidationException(string message, string nodePath)
            : base(message)
        {
            NodePath = nodePath;
            Issues = new[] { new DistrictValidationIssue(nodePath, message) };
        }

        public DistrictValidationException(string message, string nodePath, IReadOnlyList<DistrictValidationIssue> issues)
            : base(message)
        {
            NodePath = nodePath;
            Issues = issues;
        }

        /// <summary>例如 [0].children[1].children[3]。</summary>
        public string NodePath { get; }

        /// <summary>全部校验问题，至少一条；首条与默认（首错即抛）模式报出的错误一致。</summary>
        public IReadOnlyList<DistrictValidationIssue> Issues { get; }
    }
}
