using System;

namespace DistrictJsonToSql
{
    /// <summary>
    /// 输入数据校验失败。消息里带节点路径，便于定位是哪一条行政区划数据有问题。
    /// </summary>
    public sealed class DistrictValidationException : ArgumentException
    {
        public DistrictValidationException(string message, string nodePath)
            : base(message)
        {
            NodePath = nodePath;
        }

        /// <summary>例如 [0].children[1].children[3]。</summary>
        public string NodePath { get; private set; }
    }
}
