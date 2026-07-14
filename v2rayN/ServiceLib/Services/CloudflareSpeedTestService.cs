namespace ServiceLib.Services;

public sealed class CloudflareSpeedTestService
{
    private const string DefaultIpFileName = "ip.txt";
    private const int ProcessTimeoutMinutes = 30;
    private const int MaxPingConcurrency = 1000;
    private const int DownloadTestCount = 10;
    private const int DownloadTestSeconds = 3;
    private const int MaxLogLength = 128 * 1024;
    private static readonly TimeSpan ProgressLogInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ProcessHeartbeatInterval = TimeSpan.FromSeconds(10);
    private static readonly string[] ExeFileNames = ["cfst.exe", "CloudflareST.exe"];

    public async Task<CloudflareSpeedTestRunResult> RunAsync(int resultLimit, CancellationToken cancellationToken = default)
    {
        if (resultLimit <= 0)
        {
            return CloudflareSpeedTestRunResult.Ok([]);
        }

        var tool = ResolveTool(out var resolveDiagnostics);
        LogInfo($"CloudflareST resolve diagnostics:{Environment.NewLine}{resolveDiagnostics}");
        if (tool is null)
        {
            LogInfo("CloudflareST 未找到内置程序或 ip.txt，优选终止。");
            return CloudflareSpeedTestRunResult.Fail("未找到内置 CloudflareST 程序或 ip.txt。");
        }
        LogInfo($"CloudflareST resolved. Directory={tool.Directory}, ExeFile={tool.ExeFile}, IpFile={tool.IpFile}, ExeExists={File.Exists(tool.ExeFile)}, IpExists={File.Exists(tool.IpFile)}");

        var workDir = Utils.GetTempPath($"cfst_{Utils.GetGuid(false)}");
        Directory.CreateDirectory(workDir);

        var resultFile = Path.Combine(workDir, "result.csv");
        var downloadTestCount = Math.Min(resultLimit, DownloadTestCount);
        var args = $"-n {MaxPingConcurrency} -dt {DownloadTestSeconds} -debug -p {resultLimit} -dn {downloadTestCount} -f {tool.IpFile.AppendQuotes()} -o {resultFile.AppendQuotes()}";

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromMinutes(ProcessTimeoutMinutes));

            LogInfo($"CloudflareST start: {tool.ExeFile} {args}");
            LogInfo($"CloudflareST plan: output candidates={resultLimit}, download speed tests={downloadTestCount}, ping concurrency={MaxPingConcurrency}, download timeout={DownloadTestSeconds}s.");
            var (exitCode, output, error) = await RunProcessAsync(tool.ExeFile, args, tool.Directory, timeoutCts.Token);
            SaveProcessLog(args, exitCode, output, error);
            if (exitCode != 0)
            {
                LogInfo($"CloudflareST 运行失败，退出码 {exitCode}。");
                return CloudflareSpeedTestRunResult.Fail($"CloudflareST 运行失败，退出码 {exitCode}。");
            }

            if (!File.Exists(resultFile))
            {
                LogInfo($"CloudflareST did not generate result.csv: {resultFile}");
                return CloudflareSpeedTestRunResult.Fail("CloudflareST 未生成 result.csv。");
            }

            var records = ParseResultCsv(resultFile)
                .OrderByDescending(t => t.DownloadSpeed ?? 0)
                .ThenBy(t => t.Delay ?? double.MaxValue)
                .Take(resultLimit)
                .ToList();
            LogInfo($"CloudflareST parsed {records.Count} valid record(s) from result.csv.");

            if (records.Count == 0)
            {
                LogInfo("CloudflareST result.csv 中没有解析到有效 IP。");
                return CloudflareSpeedTestRunResult.Fail("CloudflareST 没有返回可用 IP。");
            }

            return CloudflareSpeedTestRunResult.Ok(records);
        }
        catch (OperationCanceledException)
        {
            LogInfo($"CloudflareST 运行超过 {ProcessTimeoutMinutes} 分钟，已判定超时。");
            return CloudflareSpeedTestRunResult.Fail("CloudflareST 运行超时。");
        }
        catch (Exception ex)
        {
            LogException("CloudflareST run failed", ex);
            return CloudflareSpeedTestRunResult.Fail($"CloudflareST 运行异常：{ex.Message}");
        }
        finally
        {
            LogInfo($"CloudflareST 清理临时目录：{workDir}");
            TryDeleteDirectory(workDir);
        }
    }

    public bool IsToolReady() => ResolveTool(out _) is not null;

    private static CloudflareSpeedTestTool? ResolveTool(out string diagnostics)
    {
        var candidateDirs = GetCandidateDirs();
        var sb = new StringBuilder();
        sb.AppendLine($"StartupPath={Utils.StartupPath()}");
        sb.AppendLine($"BaseDirectory={Utils.GetBaseDirectory()}");

        foreach (var dir in candidateDirs.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var ipFile = Path.Combine(dir, DefaultIpFileName);
            sb.AppendLine($"CandidateDir={dir}; DirExists={Directory.Exists(dir)}; IpFile={ipFile}; IpExists={File.Exists(ipFile)}");

            foreach (var exeName in ExeFileNames)
            {
                var exeFile = Path.Combine(dir, exeName);
                sb.AppendLine($"  ExeFile={exeFile}; Exists={File.Exists(exeFile)}");
            }

            if (!File.Exists(ipFile))
            {
                continue;
            }

            foreach (var exeName in ExeFileNames)
            {
                var exeFile = Path.Combine(dir, exeName);
                if (File.Exists(exeFile))
                {
                    sb.AppendLine($"ResolvedExe={exeFile}");
                    diagnostics = sb.ToString();
                    return new CloudflareSpeedTestTool(dir, exeFile, ipFile);
                }
            }
        }

        sb.AppendLine("ResolvedExe=<null>");
        diagnostics = sb.ToString();
        return null;
    }

    private static string[] GetCandidateDirs()
    {
        return
        [
            Utils.GetBaseDirectory(Path.Combine("bin", "cfst")),
            Utils.GetBinPath(string.Empty, "cfst"),
            Utils.GetBaseDirectory(Path.Combine("Resources", "CloudflareST")),
        ];
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunProcessAsync(
        string exeFile,
        string arguments,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = exeFile,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        var startContext = $"FileName={exeFile}, Arguments={arguments}, WorkingDirectory={workingDirectory}, FileExists={File.Exists(exeFile)}, WorkingDirectoryExists={Directory.Exists(workingDirectory)}";
        LogInfo($"CloudflareST process start requested. {startContext}");
        try
        {
            var started = process.Start();
            if (!started)
            {
                LogInfo($"CloudflareST process start returned false. {startContext}");
                throw new InvalidOperationException("Process.Start returned false.");
            }
            LogInfo($"CloudflareST process started successfully. PID={process.Id}. {startContext}");

            try
            {
                process.StandardInput.Close();
            }
            catch (Exception ex)
            {
                LogException("CloudflareST close stdin failed", ex);
            }
        }
        catch (Exception ex)
        {
            LogException($"CloudflareST process start failed. {startContext}", ex);
            throw;
        }

        var output = new StringBuilder();
        var error = new StringBuilder();
        var outputTask = ReadProcessStreamAsync(process.StandardOutput, "STDOUT", output, cancellationToken);
        var errorTask = ReadProcessStreamAsync(process.StandardError, "STDERR", error, cancellationToken);
        using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var heartbeatTask = ReportProcessHeartbeatAsync(process, heartbeatCts.Token);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
            await Task.WhenAll(outputTask, errorTask);
            return (process.ExitCode, output.ToString(), error.ToString());
        }
        catch (OperationCanceledException)
        {
            TryKillProcess(process);
            throw;
        }
        finally
        {
            heartbeatCts.Cancel();
            try
            {
                await heartbeatTask;
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private static async Task ReportProcessHeartbeatAsync(Process process, CancellationToken cancellationToken)
    {
        var startedAt = DateTime.Now;
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(ProcessHeartbeatInterval, cancellationToken);
            if (process.HasExited)
            {
                return;
            }

            var elapsed = DateTime.Now - startedAt;
            LogInfo($"CloudflareST still running. PID={process.Id}, Elapsed={elapsed:hh\\:mm\\:ss}.");
        }
    }

    private static async Task ReadProcessStreamAsync(
        StreamReader reader,
        string source,
        StringBuilder aggregate,
        CancellationToken cancellationToken)
    {
        var frame = new StringBuilder();
        var buffer = new char[512];
        var lastProgressAt = DateTime.MinValue;
        var lastProgressText = string.Empty;

        while (true)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read <= 0)
            {
                break;
            }

            for (var i = 0; i < read; i++)
            {
                var ch = buffer[i];
                aggregate.Append(ch);

                if (ch is '\r' or '\n')
                {
                    var text = CleanProcessFrame(frame.ToString());
                    frame.Clear();
                    if (text.IsNullOrEmpty())
                    {
                        continue;
                    }

                    if (ch == '\r')
                    {
                        var now = DateTime.Now;
                        if (text == lastProgressText || now - lastProgressAt < ProgressLogInterval)
                        {
                            continue;
                        }
                        lastProgressAt = now;
                        lastProgressText = text;
                    }

                    SendRealtimeProcessLine(source, text);
                    continue;
                }

                frame.Append(ch);
            }
        }

        var remaining = CleanProcessFrame(frame.ToString());
        if (remaining.IsNotEmpty() && remaining != lastProgressText)
        {
            SendRealtimeProcessLine(source, remaining);
        }
    }

    private static IEnumerable<CloudflareSpeedTestRecord> ParseResultCsv(string resultFile)
    {
        if (!File.Exists(resultFile))
        {
            yield break;
        }

        var first = true;
        foreach (var line in File.ReadLines(resultFile, Encoding.UTF8))
        {
            if (first)
            {
                first = false;
                continue;
            }

            var columns = SplitCsvLine(line);
            if (columns.Count < 1)
            {
                continue;
            }

            var ip = columns[0].Trim();
            if (!IPAddress.TryParse(ip, out _))
            {
                continue;
            }

            var delay = TryParseDouble(columns.ElementAtOrDefault(4));
            var speed = TryParseDouble(columns.ElementAtOrDefault(5));
            var colo = columns.ElementAtOrDefault(6)?.Trim() ?? string.Empty;

            yield return new CloudflareSpeedTestRecord(ip, delay, speed, colo);
        }
    }

    private static List<string> SplitCsvLine(string line)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
                continue;
            }

            if (ch == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        result.Add(current.ToString());
        return result;
    }

    private static double? TryParseDouble(string? value)
    {
        if (double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var ret))
        {
            return ret;
        }

        return null;
    }

    private static void SaveProcessLog(string arguments, int exitCode, string output, string error)
    {
        var log = new StringBuilder();
        log.AppendLine($"CloudflareST finished. ExitCode={exitCode}");
        log.AppendLine($"Arguments: {arguments}");

        var cleanOutput = CleanProcessLog(output);
        if (cleanOutput.IsNotEmpty())
        {
            log.AppendLine("STDOUT:");
            log.AppendLine(cleanOutput);
        }

        var cleanError = CleanProcessLog(error);
        if (cleanError.IsNotEmpty())
        {
            log.AppendLine("STDERR:");
            log.AppendLine(cleanError);
        }

        Logging.SaveLog(log.ToString());
        NoticeManager.Instance.SendMessageEx($"CloudflareST finished. ExitCode={exitCode}");
        NoticeManager.Instance.SendMessage($"CloudflareST Arguments: {arguments}");
    }

    private static string CleanProcessLog(string value)
    {
        if (value.IsNullOrEmpty())
        {
            return string.Empty;
        }

        var text = Regex.Replace(value, @"\x1B\[[0-?]*[ -/]*[@-~]", string.Empty)
            .Replace("\r\n", "\n")
            .Replace('\r', '\n');

        var lines = text.Split('\n')
            .Select(t => t.TrimEnd())
            .Where(t => t.IsNotEmpty());

        var result = string.Join(Environment.NewLine, lines);
        if (result.Length <= MaxLogLength)
        {
            return result;
        }

        return result[..MaxLogLength] + Environment.NewLine + "... CloudflareST log truncated ...";
    }

    private static string CleanProcessFrame(string value)
    {
        if (value.IsNullOrEmpty())
        {
            return string.Empty;
        }

        return Regex.Replace(value, @"\x1B\[[0-?]*[ -/]*[@-~]", string.Empty)
            .Trim();
    }

    private static void SendRealtimeProcessLine(string source, string text)
    {
        var message = $"CloudflareST {source}: {text}";
        Logging.SaveLog(message);
        NoticeManager.Instance.SendMessageEx(message);
    }

    private static void LogInfo(string message)
    {
        if (message.IsNullOrEmpty())
        {
            return;
        }

        Logging.SaveLog(message);
        NoticeManager.Instance.SendMessageEx(message);
    }

    private static void LogException(string title, Exception ex)
    {
        Logging.SaveLog(title, ex);
        NoticeManager.Instance.SendMessageEx($"{title}: {ex.Message}");
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch (Exception ex)
        {
            LogException("Delete CloudflareST temp directory failed", ex);
        }
    }

    private static void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(true);
            }
        }
        catch (Exception ex)
        {
            LogException("Kill CloudflareST process failed", ex);
        }
    }

    private sealed record CloudflareSpeedTestTool(string Directory, string ExeFile, string IpFile);
}

public sealed record CloudflareSpeedTestRecord(string IpAddress, double? Delay, double? DownloadSpeed, string Colo);

public sealed record CloudflareSpeedTestRunResult(bool Success, string Message, IReadOnlyList<CloudflareSpeedTestRecord> Records)
{
    public static CloudflareSpeedTestRunResult Ok(IReadOnlyList<CloudflareSpeedTestRecord> records)
        => new(true, string.Empty, records);

    public static CloudflareSpeedTestRunResult Fail(string message)
        => new(false, message, []);
}
