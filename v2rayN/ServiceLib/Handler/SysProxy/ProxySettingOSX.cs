using CliWrap.Buffered;

namespace ServiceLib.Handler.SysProxy;

[SupportedOSPlatform("macos")]
public static class ProxySettingOSX
{
    private const string Tag = "ProxySettingOSX";
    private const string SudoPath = "/usr/bin/sudo";
    private static readonly string _proxySetFileName = $"{Global.ProxySetOSXShellFileName.Replace(Global.NamespaceSample, "")}.sh";

    public static async Task<bool> SetProxy(string host, int port, string exceptions)
    {
        List<string> args = ["set", host, port.ToString()];
        if (exceptions.IsNotEmpty())
        {
            args.AddRange(exceptions.Split(','));
        }

        return await ExecCmd(args);
    }

    public static async Task<bool> UnsetProxy()
    {
        List<string> args = ["clear"];
        return await ExecCmd(args);
    }

    private static async Task<bool> ExecCmd(List<string> args)
    {
        var customSystemProxyScriptPath = AppManager.Instance.Config.SystemProxyItem?.CustomSystemProxyScriptPath;
        var useCustomScript = customSystemProxyScriptPath.IsNotEmpty() && File.Exists(customSystemProxyScriptPath);
        var fileName = useCustomScript
            ? customSystemProxyScriptPath!
            : await FileUtils.CreateLinuxShellFile(
                _proxySetFileName,
                EmbedUtils.GetEmbedText(Global.ProxySetOSXShellFileName),
                true);

        try
        {
            var command = CliWrap.Cli.Wrap(fileName).WithArguments(args);
            if (!useCustomScript)
            {
                var password = AppManager.Instance.LinuxSudoPwd;
                if (password.IsNullOrEmpty())
                {
                    LogMessage("macOS system proxy authorization is missing.");
                    return false;
                }

                List<string> sudoArgs = ["-S", "-p", "", "--", fileName, .. args];
                command = CliWrap.Cli.Wrap(SudoPath)
                    .WithArguments(sudoArgs)
                    .WithStandardInputPipe(CliWrap.PipeSource.FromString($"{password}{Environment.NewLine}"));
            }

            LogMessage($"macOS system proxy command started. Script={fileName}, Action={args.FirstOrDefault()}.");
            var result = await command
                .WithValidation(CliWrap.CommandResultValidation.None)
                .ExecuteBufferedAsync();
            LogMessage($"macOS system proxy command exited. ExitCode={result.ExitCode}.");
            LogOutput("STDOUT", result.StandardOutput);
            LogOutput("STDERR", result.StandardError);
            if (!result.IsSuccess && !useCustomScript)
            {
                AppManager.Instance.LinuxSudoPwd = string.Empty;
            }
            return result.IsSuccess;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(Tag, ex);
            NoticeManager.Instance.SendMessage($"macOS system proxy command failed: {ex.Message}");
            return false;
        }
    }

    private static void LogOutput(string stream, string? output)
    {
        foreach (var line in (output ?? string.Empty)
                 .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            LogMessage($"macOS system proxy {stream}: {line}");
        }
    }

    private static void LogMessage(string message)
    {
        Logging.SaveLog(message);
        NoticeManager.Instance.SendMessage(message);
    }
}
