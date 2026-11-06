using System;

namespace DistrictJsonToSql
{
    /// <summary>
    /// 手写 UUIDv7 生成器（.NET Framework 4.7.2 没有 Guid.CreateVersion7()）。
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

                var guidBytes = new byte[16];

                var timestampBytes = BitConverter.GetBytes(currentTimestamp);
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(timestampBytes);
                Array.Copy(timestampBytes, 2, guidBytes, 0, 6);

                guidBytes[6] = (byte)((_counter >> 8) & 0x0F | 0x70);
                guidBytes[7] = (byte)(_counter & 0xFF);

                var randomBytes = new byte[8];
                _random.NextBytes(randomBytes);
                randomBytes[0] = (byte)((randomBytes[0] & 0x3F) | 0x80);
                Array.Copy(randomBytes, 0, guidBytes, 8, 8);

                return new Guid(guidBytes).ToString();
            }
        }
    }
}
