using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace ClassEasy.Services
{
    /// <summary>
    /// 授时中心服务：
    ///   - 内置 10 大类 NTP 服务器清单（按国家/机构/区域/高校/云厂商 分类）
    ///   - 每次同步前从全部有效候选节点随机均匀选一个（保证长期均匀分布）
    ///   - 域名优先，域名解析失败再回退到该组里预置的 IP（IPv4 + IPv6 均可）
    ///   - 连接失败不崩：写日志记录 server name / timestamp / success，然后继续用下一候选
    /// </summary>
    public static class NtpTimeService
    {
        // ----------------------------------------------------------------------------
        // 1. NTP 服务器清单（10 大类，严格按用户提供的内容组织）
        // ----------------------------------------------------------------------------
        // 每一个「组」有：Category 分类名；Primary 优先（域名）；Fallback IPs 域名失败时依次尝试
        private class NtpGroup
        {
            public string Category { get; set; }
            public string[] Domains { get; set; }
            public string[] FallbackIps { get; set; }
        }

        private static readonly List<NtpGroup> ServerGroups = new List<NtpGroup>
        {
            // 1. 国家授时中心 NTSC
            new NtpGroup
            {
                Category = "国家授时中心 NTSC",
                Domains = new [] { "ntp.ntsc.ac.cn" },
                FallbackIps = new [] { "114.118.7.161", "114.118.7.163" }
            },
            // 2. CNNIC 中国互联网络信息中心
            new NtpGroup
            {
                Category = "CNNIC",
                Domains = new [] { "ntp.cnnic.cn" },
                FallbackIps = Array.Empty<string>()
            },
            // 3. 中国 NTP 快速授时 NTP ORG CN
            new NtpGroup
            {
                Category = "NTP.ORG.CN 快速授时",
                Domains = new [] { "cn.ntp.org.cn" },
                FallbackIps = new [] { "2001:da8:9000::81", "223.113.97.98", "114.67.103.73", "119.29.26.206", "120.25.115.20" }
            },
            // 4. 教育网
            new NtpGroup
            {
                Category = "教育网 edu.ntp.org.cn",
                Domains = new [] { "edu.ntp.org.cn" },
                FallbackIps = new [] { "2001:da8:9000::130", "2001:250:380A:5::10", "202.118.1.130", "202.118.1.81", "116.13.10.10" }
            },
            // 5. 区域 NTP（香港/台湾/日韩新美德/印尼）
            new NtpGroup
            {
                Category = "区域 NTP（亚洲/欧美）",
                Domains = new []
                {
                    "hk.ntp.org.cn", "tw.ntp.org.cn", "jp.ntp.org.cn", "kr.ntp.org.cn",
                    "sgp.ntp.org.cn", "us.ntp.org.cn", "de.ntp.org.cn", "ina.ntp.org.cn"
                },
                FallbackIps = new []
                {
                    "149.129.123.30",   // hk
                    "133.100.11.8",     // jp
                    "211.233.40.78",    // kr
                    "218.186.3.36",     // sgp
                    "158.69.48.97",     // us
                    "131.188.3.223",    // de
                    "203.114.74.17",    // ina
                }
            },
            // 6. 中国计量科学研究院 NIM
            new NtpGroup
            {
                Category = "中国计量科学研究院 NIM",
                Domains = new [] { "ntp1.nim.ac.cn", "ntp2.nim.ac.cn" },
                FallbackIps = new [] { "111.203.6.13" }
            },
            // 7. 国际 Pool NTP
            new NtpGroup
            {
                Category = "Pool.ntp.org",
                Domains = new [] { "cn.pool.ntp.org" },
                FallbackIps = new [] { "120.25.115.20", "111.230.189.174", "119.28.183.184" }
            },
            // 8. 云厂商（阿里云 + 腾讯云）
            new NtpGroup
            {
                Category = "云厂商（阿里云 / 腾讯云）",
                Domains = new []
                {
                    "ntp.aliyun.com", "ntp1.aliyun.com", "ntp2.aliyun.com", "ntp3.aliyun.com",
                    "ntp4.aliyun.com", "ntp5.aliyun.com", "ntp6.aliyun.com", "ntp7.aliyun.com",
                    "ntp.tencent.com", "ntp1.tencent.com", "ntp2.tencent.com",
                    "ntp3.tencent.com", "ntp4.tencent.com", "ntp5.tencent.com",
                },
                FallbackIps = new [] { "203.107.6.88", "182.92.12.11", "120.25.108.11" }
            },
            // 9. 专用 NTP（高通 / 海康威视）
            new NtpGroup
            {
                Category = "专用（高通 / 海康）",
                Domains = new [] { "time.izatcloud.net", "time.gpsonextra.net", "hik-time.ys7.com", "time.ys7.com" },
                FallbackIps = Array.Empty<string>()
            },
            // 10. 高校（上交 / 东北大 / 北邮 / 上大 / 清华 TUNA / 中科大 / 复旦 / 南大 / 同济）
            new NtpGroup
            {
                Category = "高校 NTP（教育网）",
                Domains = new []
                {
                    "ntp.sjtu.edu.cn",
                    "ntp.neu.edu.cn",
                    "ntp.bupt.edu.cn",
                    "ntp.shu.edu.cn",
                    "ntp.tuna.tsinghua.edu.cn",
                    "time.ustc.edu.cn",
                    "ntp.fudan.edu.cn",
                    "ntp.nju.edu.cn",
                    "ntp.tongji.edu.cn",
                },
                FallbackIps = Array.Empty<string>()
            },
        };

        // ----------------------------------------------------------------------------
        // 2. 节点扁平化 + 随机均匀抽样
        // ----------------------------------------------------------------------------
        private class NtpEndpoint
        {
            public string Category { get; set; }     // 服务器所属分类
            public string Host { get; set; }         // 实际尝试的主机：域名或 IP
            public bool IsIp { get; set; }           // 是否为 IP （Fallback）
        }

        // 把所有候选节点扁平化为一个列表，按域优先、IP 回退的顺序拼接，便于随机均匀抽样。
        private static IReadOnlyList<NtpEndpoint> Flattened = BuildFlatList();

        private static List<NtpEndpoint> BuildFlatList()
        {
            var list = new List<NtpEndpoint>();
            foreach (var g in ServerGroups)
            {
                // 域名优先
                if (g.Domains != null)
                {
                    foreach (var d in g.Domains)
                    {
                        if (!string.IsNullOrWhiteSpace(d))
                        {
                            list.Add(new NtpEndpoint { Category = g.Category, Host = d, IsIp = false });
                        }
                    }
                }
                // IP 回退
                if (g.FallbackIps != null)
                {
                    foreach (var ip in g.FallbackIps)
                    {
                        if (!string.IsNullOrWhiteSpace(ip))
                        {
                            list.Add(new NtpEndpoint { Category = g.Category, Host = ip, IsIp = true });
                        }
                    }
                }
            }
            return list;
        }

        /// <summary>
        /// 开发者模式专用：用用户自定义的服务器列表替换内置清单。
        /// 传入 null/空字符串时回退到内置清单。
        /// 格式支持逗号、换行或分号分隔；自动 trim；空项忽略；每项以"自定义"分类名归类。
        /// </summary>
        public static void OverrideServers(string customList)
        {
            if (string.IsNullOrWhiteSpace(customList))
            {
                Flattened = BuildFlatList();
                return;
            }

            var separators = new[] { ',', '\n', '\r', ';', ' ', '\t' };
            var hosts = customList.Split(separators, StringSplitOptions.RemoveEmptyEntries);
            var list = new List<NtpEndpoint>();
            foreach (var raw in hosts)
            {
                var host = raw.Trim();
                if (string.IsNullOrEmpty(host)) continue;

                // 简单判断：含 ":"（IPv6）或全数字点（IPv4）算 IP；否则算域名
                bool isIp = host.Contains(":") ||
                            (host.Count(c => c == '.') == 3 &&
                             host.Split('.').All(p => byte.TryParse(p, out _)));
                list.Add(new NtpEndpoint
                {
                    Category = "自定义",
                    Host = host,
                    IsIp = isIp
                });
            }
            Flattened = list.Count > 0 ? list : BuildFlatList();
        }

        private static readonly object _syncRoot = new object();
        private static readonly Random _rnd = new Random();

        private const int NtpPort = 123;
        private const int SocketTimeoutMs = 3000;

        /// <summary>校准后时钟的累计偏差（授时中心 now − 本地 now）。正数表示本地偏慢。</summary>
        private static TimeSpan _localClockOffset = TimeSpan.Zero;

        public static bool IsSynchronized { get; private set; }

        /// <summary>「✓ 国家授时」或「⚠ 本地时间」——失败后立即切换回警告。</summary>
        public static string ClockSourceText { get; private set; } = "⚠ 本地时间";

        /// <summary>最近一次成功所用的服务器（主机名/IP）。默认空字符串。</summary>
        public static string LastServer { get; private set; } = "";

        /// <summary>最近一次成功所用的分类名称（例如「云厂商（阿里云 / 腾讯云）」）。</summary>
        public static string LastCategory { get; private set; } = "";

        public static DateTime GetNow()
        {
            lock (_syncRoot)
            {
                return DateTime.Now + _localClockOffset;
            }
        }

        /// <summary>
        /// 每轮同步：从所有候选节点里随机均匀抽一个作为"首选"，首选失败再按列表顺序继续试别的，
        /// 只要有一个成功就写偏差并记录日志。全部失败 → 降级到本地时间。
        /// </summary>
        public static async Task SyncOnceAsync()
        {
            // 0. 先"均匀洗牌"得到本次的尝试顺序（Fisher-Yates）。保证长期均匀分布。
            var order = new int[Flattened.Count];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            lock (_rnd)
            {
                for (int i = order.Length - 1; i > 0; i--)
                {
                    int j = _rnd.Next(i + 1);
                    (order[i], order[j]) = (order[j], order[i]);
                }
            }

            string lastServerTried = "";
            string lastCategoryTried = "";
            Exception lastError = null;

            foreach (int idx in order)
            {
                var ep = Flattened[idx];
                lastServerTried = ep.Host;
                lastCategoryTried = ep.Category;

                // IPv6 主机要包一层 [xxx]，如果是域名就让 Dns 自己解
                string host = ep.Host;
                bool isIpv6 = ep.IsIp && ep.Host.Contains(":") && !ep.Host.StartsWith("[");
                if (isIpv6) host = "[" + ep.Host + "]";

                try
                {
                    var offset = await QueryNtpOffsetAsync(host, ep.Host).ConfigureAwait(false);
                    if (Math.Abs(offset.TotalDays) < 30)
                    {
                        lock (_syncRoot)
                        {
                            _localClockOffset = offset;
                            IsSynchronized = true;
                            ClockSourceText = $"[成功]已向 NTP 服务器 {ep.Host} 同步时间";
                            LastServer = ep.Host;
                            LastCategory = ep.Category;
                        }
                        LogService.Info(string.Format(
                            "NTP 同步成功  [{0}] {1}  偏差 {2:F1}ms",
                            ep.Category, ep.Host, offset.TotalMilliseconds));
                        return;
                    }
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    LogService.Warn(string.Format(
                        "NTP 尝试失败 [{0}] {1}: {2}",
                        ep.Category, ep.Host, ex.Message));
                }
            }

            // 全部候选都失败
            lock (_syncRoot)
            {
                _localClockOffset = TimeSpan.Zero;
                IsSynchronized = false;
                ClockSourceText = "[警告] 当前为本地时间，时间可能不准确！";
            }
            LogService.Warn("全部 NTP 服务器同步失败，使用本地系统时间。最后尝试：" +
                            $"{lastCategoryTried}/{lastServerTried} — {(lastError?.Message ?? "(无)")}");
        }

        /// <summary>
        /// 向单个主机（可以是域名或 IPv4/IPv6；IPv6 传入时已加方括号）发起一次 NTP 请求。
        /// </summary>
        /// <param name="hostForSocket">传进 UdpClient 的主机（IPv6 已加 []）。</param>
        /// <param name="displayHost">日志显示用的主机名（不做修改）。</param>
        private static async Task<TimeSpan> QueryNtpOffsetAsync(string hostForSocket, string displayHost)
        {
            // NTP 报文：首字节 LI=0 VN=4 Mode=3(client) → 0x23。
            var ntpData = new byte[48];
            ntpData[0] = 0x23;

            using (var udp = new UdpClient())
            {
                udp.Client.SendTimeout = SocketTimeoutMs;
                udp.Client.ReceiveTimeout = SocketTimeoutMs;

                await udp.SendAsync(ntpData, ntpData.Length, hostForSocket, NtpPort).ConfigureAwait(false);

                var receive = await udp.ReceiveAsync().ConfigureAwait(false);
                byte[] buffer = receive.Buffer;

                if (buffer.Length < 48)
                {
                    throw new InvalidOperationException("NTP 回复长度不足（host: " + displayHost + "）");
                }

                // Transmit Timestamp [40..48) = 32bit seconds + 32bit fraction（1900/01/01 epoch）
                ulong intPart = SwapEndianness(BitConverter.ToUInt32(buffer, 40));
                ulong fracPart = SwapEndianness(BitConverter.ToUInt32(buffer, 44));

                double milliseconds = intPart * 1000d + (fracPart * 1000d / 0x100000000L);
                var ntpEpoch = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                DateTime serverUtc = ntpEpoch.AddMilliseconds(milliseconds);
                DateTime serverLocal = serverUtc.ToLocalTime();

                return serverLocal - DateTime.Now;
            }
        }

        private static uint SwapEndianness(uint x)
        {
            return
                ((x & 0x000000FF) << 24) |
                ((x & 0x0000FF00) << 8) |
                ((x & 0x00FF0000) >> 8) |
                ((x & 0xFF000000) >> 24);
        }
    }
}