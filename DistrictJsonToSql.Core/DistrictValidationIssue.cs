using System;

namespace DistrictJsonToSql
{
    /// <summary>
    /// 单条校验错误：完整消息 + 节点路径，便于在汇总模式下逐条定位。
    /// </summary>
    public sealed class DistrictValidationIssue
    {
        public DistrictValidationIssue(string nodePath, string message)
        {
            if (string.IsNullOrEmpty(nodePath))
            {
                throw new ArgumentException("节点路径不能为空", nameof(nodePath));
            }

            if (string.IsNullOrEmpty(message))
            {
                throw new ArgumentException("错误消息不能为空", nameof(message));
            }

            NodePath = nodePath;
            Message = message;
        }

        /// <summary>例如 [0].children[1].children[3]。</summary>
        public string NodePath { get; }

        /// <summary>与首错模式下 <see cref="DistrictValidationException.Message"/> 完全一致的文本。</summary>
        public string Message { get; }
    }
}
