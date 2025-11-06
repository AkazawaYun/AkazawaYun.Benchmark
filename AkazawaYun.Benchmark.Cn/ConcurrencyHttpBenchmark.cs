using AkazawaYun.PRO7;
using System.Buffers;
using System.Diagnostics;
using System.Text;

namespace AkazawaYun.Benchmark.Cn;

internal class ConcurrencyHttpBenchmark
{
    static readonly string Website = "127.0.0.1:8080";
    static int ReqMode = 0;
    static string ReqModeStr => ReqMode is 0 ? "管道模式" : "请求-响应模式";
    static int Concurrency = 0;
    static int PerReqCount = 0;
    static int TotalReqCount = 0;
    static readonly string ReqTxt = "GET /plaintext HTTP/1.1\r\nHost: 127.0.0.1:8080\r\nConnection: keep-alive\r\n\r\n";
    static readonly ReadOnlyMemory<byte> Req;
    static readonly int BatchCount = 1000;
    static readonly ReadOnlyMemory<byte> Reqs;
    static TaskCompletionSource<int> WaitTest = new();
    static readonly Stopwatch Stopwatch = new();
    static int TotalResCount = 0;
    static int IsSending = 0;
    static int IsFinish = 0;

    static ConcurrencyHttpBenchmark()
    {
#if DEBUG
        akzLog.War("建议使用 Release 模式编译并运行压测工具");
#endif
        string path = $"{akzWindows.GetBaseDirectory()}req.txt";
        if (File.Exists(path))
            ReqTxt = File.ReadAllText(path);
        else
            File.WriteAllText(path, ReqTxt);

        Req = Encoding.UTF8.GetBytes(ReqTxt);
        Memory<byte> arr = new byte[Req.Length * BatchCount];
        for (int i = 0; i < BatchCount; i++)
        {
            int start = i * Req.Length;
            Memory<byte> slice = arr[start..(start + Req.Length)];
            Req.CopyTo(slice);
        }
        Reqs = arr;
    }
    public static async Task Main()
    {
        _ = Execute();
        await Task.Delay(-1);
    }
    public static async Task Restart()
    {
        Console.WriteLine("按下 空格键 清屏, 回车键 不清屏:");
        while (true)
        {
            ConsoleKeyInfo input = Console.ReadKey();

            if (input.Key is ConsoleKey.Spacebar)
            {
                Console.Clear();
                break;
            }
            else if (input.Key is ConsoleKey.Enter)
            {
                Console.WriteLine();
                break;
            }
        }

        WaitTest = new();
        TotalResCount = 0;
        IsSending = 0;
        IsFinish = 0;

        await Execute();
    }
    public static async Task Execute()
    {
        Console.ForegroundColor = ConsoleColor.Gray;
        akzLog.Default = akzLog.Output.NoneButWar;

        await Task.Delay(10);
        Log($"欢迎使用高性能Http压测工具 AkazawaYun.ConcurrencyHttpBenchmark v2025.11.1!");
        Log("请输入请求响应模式: 0: 管道模式 1: 请求-响应模式");
        string input = Console.ReadLine()!;
        if (input is "1")
            ReqMode = 1;
        else if (input is "0")
            ReqMode = 0;
        else
        {
            ReqMode = 0;
            Log($"输入的不是合理的值, 将使用默认模式: 管道模式");
        }
        Log($"请指定并发连接的客户端数量:");
        string input1 = Console.ReadLine()!;
        if (int.TryParse(input1, out int num1) && num1 > 0)
            Concurrency = num1;
        else
        {
            Concurrency = 1;
            Log($"输入的不是合理的数字, 将使用 1 个客户端(无并发)");
        }

        Log($"请指定每个客户端发送Http请求的数量(单位:千):");
        string input2 = Console.ReadLine()!;
        if (int.TryParse(input2, out int num2))
            PerReqCount = num2 * 1000;
        else
        {
            PerReqCount = -1;
            Log($"输入的不是合理的数字, 将永远不停下!!!");
        }

        TotalReqCount = Concurrency * PerReqCount;
        Log($"目标服务器地址: {Website}");


        Log($"正在测试服务是否可用...");
        Log($"=== 请求内容 (发送 {Req.Length:N0} 字节) ===");
        Log(ReqTxt, ConsoleColor.Blue);
        TClient test = new();
        try
        {
            await test.Connect(Website);
            await test.Send(Req);
        }
        catch
        {
            Log($"连接失败!请检查服务状态!", ConsoleColor.Red);
            await Task.Delay(-1);
        }
        int len = await WaitTest.Task;
        Log($"测试完成, 服务可用!");


        Log($"开始建立 {Concurrency:N0} 个Http连接...");
        CClient[] lst = new CClient[Concurrency];
        for (int i = 0; i < Concurrency; i++)
            lst[i] = await New(len);

        string per = PerReqCount < 0 ? "∞" : PerReqCount.ToString("N0");
        string all = PerReqCount < 0 ? "∞" : TotalReqCount.ToString("N0");
        Log($"全部连接已建立完成!");
        Log($"每个连接执行 {per} 次{ReqModeStr}Http请求...");
        Log($"总计 {all} 次Http请求");
        Log($"正在并发执行...");

        Stopwatch.Restart();
        foreach (var client in lst)
        {
            if (ReqMode is 0)
                _ = client.StartSend(Reqs);
            else
                _ = client.Send(Req).AsTask();
        }
    }
    static async Task<CClient> New(int len)
    {
        CClient client = new(len);
        await client.Connect(Website);
        return client;
    }
    static async Task ShowFinalResult()
    {
        Stopwatch.Stop();

        // 性能统计输出
        var totalElapsedMs = Stopwatch.ElapsedMilliseconds;
        var totalElapsedSeconds = totalElapsedMs / 1000.0;
        var throughputPerSecond = TotalReqCount / totalElapsedSeconds;
        var avgLatencyMs = (double)totalElapsedMs / TotalReqCount;
        var totalDataSent = Req.Length * (long)TotalReqCount;
        var dataThroughputMBps = totalDataSent / 1024.0 / 1024.0 / totalElapsedSeconds;

        Log();
        Log("=== 性能测试结果 ===");
        Log($"请求模式: {ReqModeStr}", ConsoleColor.Cyan);
        Log($"并发连接数: {Concurrency:N0}", ConsoleColor.Cyan);
        Log($"总请求数: {TotalReqCount:N0}");
        Log($"总耗时: {totalElapsedMs:N0} ms ({totalElapsedSeconds:F2} 秒)");
        Log($"吞吐量: {throughputPerSecond:F2} 请求/秒", ConsoleColor.Cyan);
        Log($"平均延迟: {avgLatencyMs:F4} ms/请求");
        Log($"数据传输量: {totalDataSent:N0} 字节 ({totalDataSent / 1024.0 / 1024.0:F2} Mb)");
        Log($"数据吞吐量: {dataThroughputMBps:F2} Mb/s");
        Log();
        await Restart();
    }
    static readonly Lock locker = new();
    static void Log(string log = "", ConsoleColor color = ConsoleColor.Gray)
    {
        lock (locker)
        {
            ConsoleColor cc = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(log);
            Console.ForegroundColor = cc;
        }
    }

    internal class TClient : akzTcpClientVI
    {
        int SelfResCount = 0;
        public TClient()
        {
            LogLevel = 0;
        }
        protected override async ValueTask WhenRead(ReadOnlySequence<byte> seq)
        {
            SelfResCount++;
            string res = Encoding.UTF8.GetString(seq);
            Log($"=== 响应内容{SelfResCount} (收到 {res.Length:N0} 字节) ===");
            Log(res, ConsoleColor.Blue);

            if (SelfResCount > 1)
            {
                WaitTest.SetResult(res.Length);
                return;
            }
            await Send(Req);
        }
    }
    internal class CClient : akzTcpClientVI_FixLength
    {
        int SelfReqCount = 0;
        int SelfResCount = 0;

        public CClient(int len) : base(len)
        {
            LogLevel = 0;
        }


        public async Task StartSend(TcpData data)
        {
            if (PerReqCount < 0)
                while (true)
                {
                    await Send(data);
                }

            int loop = PerReqCount / 1000;
            for (int i = 0; i < loop; i++)
                await Send(data);
        }
        public override async ValueTask Send(TcpData data)
        {
            if (SelfReqCount is 0)
            {
                int sum = Interlocked.Increment(ref IsSending);
                if (sum % Math.Max(1, Concurrency / 10) == 0 || sum == Concurrency)
                    Log($"▲ 已有 {sum:N0} 个客户端开始发送 {PerReqCount:N0} 个请求", ConsoleColor.DarkYellow);
            }
            await base.Send(data);
            SelfReqCount++;
        }
        protected override async ValueTask WhenRead(ReadOnlySequence<byte> seq)
        {
            SelfResCount++;
            Interlocked.Increment(ref TotalResCount);
            int loop = Math.Min(100000, TotalReqCount / 10);
            if (loop < 1)
                loop = 100000;
            if (TotalResCount % loop == 0)
            {
                // 每100000次请求输出一次进度
                var elapsed = Stopwatch.ElapsedMilliseconds;
                var currentThroughput = TotalResCount * 1000.0 / elapsed;
                Log($"● 已发送: {TotalResCount:N0} 次，当前吞吐量: {currentThroughput:F2} 请求/秒", ConsoleColor.Green);
            }
            if (PerReqCount > 0 && SelfResCount == PerReqCount)
            {
                int sum = Interlocked.Increment(ref IsFinish);
                if (sum % Math.Max(1, Concurrency / 10) == 0 || sum == Concurrency)
                    Log($"■ 已有 {sum:N0} 个客户端发送完成 {PerReqCount:N0} 个请求", ConsoleColor.DarkRed);
                CloseActive();
                if (sum == Concurrency)
                    _ = ShowFinalResult();
                return;
            }
            if (ReqMode is 1)
                await Send(Req);
        }
    }
}
