using System;
using System.Text;

namespace DistrictJsonToSql
{
    /// <summary>
    /// 手写 UUIDv7 生成器（.NET Framework 4.7.2 没有 Guid.CreateVersion7()）。
    /// 按 RFC 9562 布局组装：unix_ts_ms(48) | ver(4)=7 | rand_a(12) | var(2)=10 | rand_b(62)，
    /// 并以规范的带连字符十六进制文本返回，保证字符串顺序等于生成时间顺序。
    /// </summary>
    internal sealed class GuidV7Generator
    {
        private readonly Random _random;
        private long _lastTimestamp;
        private int _counter;
        private readonly object _lock = new object();

        public GuidV7Generator()
        {
            _random = new Random();
            _lastTimestamp = 0;
            _counter = 0;
        }

        public string Generate()
        {
            lock (_lock)
            {
                var currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                if (currentTimestamp == _lastTimestamp)
                {
                    _counter++;
                    if (_counter > 0xFFF)
                    {
                        currentTimestamp++;
                        _counter = 0;
                    }
                }
                else
                {
                    _counter = 0;
                }

                _lastTimestamp = currentTimestamp;

                var bytes = new byte[16];

                // 高 48 位毫秒时间戳，按大端写入前 6 字节
                var timestamp = currentTimestamp;
                for (var i = 5; i >= 0; i--)
                {
                    bytes[i] = (byte)(timestamp & 0xFF);
                    timestamp >>= 8;
                }

                // 版本位 0111 + 12 位计数器（rand_a）
                bytes[6] = (byte)(0x70 | ((_counter >> 8) & 0x0F));
                bytes[7] = (byte)(_counter & 0xFF);

                // 变体位 10 + 62 位随机（rand_b）
                var randomBytes = new byte[8];
                _random.NextBytes(randomBytes);
                bytes[8] = (byte)(0x80 | (randomBytes[0] & 0x3F));
                Array.Copy(randomBytes, 1, bytes, 9, 7);

                return ToRfc9562String(bytes);
            }
        }

        private static string ToRfc9562String(byte[] bytes)
        {
            var builder = new StringBuilder(36);
            for (var i = 0; i < bytes.Length; i++)
            {
                if (i == 4 || i == 6 || i == 8 || i == 10)
                {
                    builder.Append('-');
                }
                builder.Append(bytes[i].ToString("x2"));
            }
            return builder.ToString();
        }
    }
}
