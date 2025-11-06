using AkazawaYun.PRO7;
using System.Buffers;
using System.Diagnostics;
using System.Text;

namespace AkazawaYun.Benchmark.En;

internal class ConcurrencyHttpBenchmark
{
    static readonly string Website = "127.0.0.1:8080";
    static int ReqMode = 0;
    static string ReqModeStr => ReqMode is 0 ? "pipelining-mode" : "req-res-mode";
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
        akzLog.War("please run benchmark in Release mode.");
#endif
        string path = $"{akzWindows.GetBaseDirectory()}req.txt";
        Console.WriteLine($"read request content from file: {path}");
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
        Console.WriteLine("press Spacebar to clean output, or Enter to continue without clean output:");
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
        Log($"welcome to use AkazawaYun.ConcurrencyHttpBenchmark v2025.11.1!");
        Log("input number to select request mode:  0: pipelining-mode  1: requset-response-mode");
        string input = Console.ReadLine()!;
        if (input is "1")
            ReqMode = 1;
        else if (input is "0")
            ReqMode = 0;
        else
        {
            ReqMode = 0;
            Log($"invalid value, will use: pipelining-mode.");
        }
        Log($"input the count of concurrency client:");
        string input1 = Console.ReadLine()!;
        if (int.TryParse(input1, out int num1) && num1 > 0)
            Concurrency = num1;
        else
        {
            Concurrency = 1;
            Log($"invalid value, will use only one client ( no concurrency ).");
        }

        Log($"input the count of request for each client ( unit: thousands ):");
        string input2 = Console.ReadLine()!;
        if (int.TryParse(input2, out int num2))
            PerReqCount = num2 * 1000;
        else
        {
            PerReqCount = -1;
            Log($"invalid value, will never stop requesting.");
        }

        TotalReqCount = Concurrency * PerReqCount;
        Log($"target server: {Website}");


        Log($"testing if availiable...");
        Log($"=== REQUEST ( {Req.Length:N0} bytes) ===");
        Log(ReqTxt, ConsoleColor.Blue);
        ScoutClient test = new();
        try
        {
            await test.Connect(Website);
            await test.Send(Req);
        }
        catch
        {
            Log($"connected failed, please check the server aliving!", ConsoleColor.Red);
            await Task.Delay(-1);
        }
        int len = await WaitTest.Task;
        Log($"test ok!");


        Log($"start to connect {Concurrency:N0} http connection...");
        AttackClient[] lst = new AttackClient[Concurrency];
        for (int i = 0; i < Concurrency; i++)
            lst[i] = await New(len);

        string per = PerReqCount < 0 ? "∞" : PerReqCount.ToString("N0");
        string all = PerReqCount < 0 ? "∞" : TotalReqCount.ToString("N0");
        Log($"all connections is completed!");
        Log($"send {per} http requst in {ReqModeStr} for each connection...");
        Log($"total {all} http requests.");
        Log($"start to send concurrently...");

        Stopwatch.Restart();
        foreach (var client in lst)
        {
            if (ReqMode is 0)
                _ = client.StartSend(Reqs);
            else
                _ = client.Send(Req).AsTask();
        }
    }
    static async Task<AttackClient> New(int len)
    {
        AttackClient client = new(len);
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
        Log("=== RESULT ===");
        Log($"request mode: {ReqModeStr}", ConsoleColor.Cyan);
        Log($"concurrency: {Concurrency:N0}", ConsoleColor.Cyan);
        Log($"total req: {TotalReqCount:N0}");
        Log($"total time: {totalElapsedMs:N0} ms ({totalElapsedSeconds:F2} s)");
        Log($"throughput: {throughputPerSecond:F2} req/s", ConsoleColor.Cyan);
        Log($"avg latency: {avgLatencyMs:F4} ms/req");
        Log($"total data: {totalDataSent:N0} bytes ({totalDataSent / 1024.0 / 1024.0:F2} Mb)");
        Log($"data throughput: {dataThroughputMBps:F2} Mb/s");
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


    internal class ScoutClient : akzTcpClientVI
    {
        int SelfResCount = 0;

        public ScoutClient()
        {
            LogLevel = 0;
        }


        protected override async ValueTask WhenRead(ReadOnlySequence<byte> seq)
        {
            SelfResCount++;
            string res = Encoding.UTF8.GetString(seq);
            Log($"=== RESPONSE{SelfResCount} ( {res.Length:N0} bytes) ===");
            Log(res, ConsoleColor.Blue);

            if (SelfResCount > 1)
            {
                WaitTest.SetResult(res.Length);
                return;
            }
            await Send(Req);
        }
    }
    internal class AttackClient : akzTcpClientVI_FixLength
    {
        int SelfReqCount = 0;
        int SelfResCount = 0;

        public AttackClient(int len) : base(len)
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
                    Log($"▲ {sum:N0} clients has started send {PerReqCount:N0} request.", ConsoleColor.DarkYellow);
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
                Log($"● total send: {TotalResCount:N0} request，throughput: {currentThroughput:F2} req/s", ConsoleColor.Green);
            }
            if (PerReqCount > 0 && SelfResCount == PerReqCount)
            {
                int sum = Interlocked.Increment(ref IsFinish);
                if (sum % Math.Max(1, Concurrency / 10) == 0 || sum == Concurrency)
                    Log($"■ {sum:N0} clients completed sending {PerReqCount:N0} request.", ConsoleColor.DarkRed);
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
