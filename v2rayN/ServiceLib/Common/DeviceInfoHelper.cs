namespace ServiceLib.Common;

public static class DeviceInfoHelper
{
    private static readonly Lazy<string> _hwid = new(GetHwidCore);
    private static readonly Lazy<string> _deviceOs = new(GetDeviceOsCore);
    private static readonly Lazy<string> _osVersion = new(GetOsVersionCore);
    private static readonly Lazy<string> _deviceModel = new(GetDeviceModelCore);

    public static IReadOnlyDictionary<string, string> GetSubscriptionHeaders()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["x-hwid"] = GetHwid(),
            ["x-device-os"] = GetDeviceOs(),
            ["x-ver-os"] = GetOsVersion(),
            ["x-device-model"] = GetDeviceModel()
        };
    }

    public static string GetHwid()
    {
        return _hwid.Value;
    }

    public static string GetDeviceOs()
    {
        return _deviceOs.Value;
    }

    public static string GetOsVersion()
    {
        return _osVersion.Value;
    }

    public static string GetDeviceModel()
    {
        return _deviceModel.Value;
    }

    private static string GetHwidCore()
    {
        var raw = GetPlatformMachineId();

        if (raw.IsNullOrEmpty())
        {
            raw = string.Join("|",
                GetNetworkFingerprint(),
                Environment.MachineName,
                Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER"),
                RuntimeInformation.OSDescription);
        }

        if (raw.IsNullOrEmpty())
        {
            raw = RuntimeInformation.OSDescription;
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash).ToLowerInvariant()[..16];
    }

    private static string GetPlatformMachineId()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                return ReadRegistryValue(@"SOFTWARE\Microsoft\Cryptography", "MachineGuid");
            }

            if (OperatingSystem.IsMacOS())
            {
                var output = RunCommand("ioreg", "-rd1 -c IOPlatformExpertDevice");
                return Regex.Match(output, @"""IOPlatformUUID""\s*=\s*""([^""]+)""").Groups[1].Value.Trim();
            }

            if (OperatingSystem.IsLinux())
            {
                foreach (var file in new[] { "/etc/machine-id", "/var/lib/dbus/machine-id" })
                {
                    if (File.Exists(file))
                    {
                        var text = File.ReadAllText(file).Trim();
                        if (text.IsNotEmpty())
                        {
                            return text;
                        }
                    }
                }
            }
        }
        catch
        {
            // Fall back to a less specific but still stable local fingerprint.
        }

        return string.Empty;
    }

    private static string GetDeviceOsCore()
    {
        if (OperatingSystem.IsWindows())
        {
            return "Windows";
        }

        if (OperatingSystem.IsMacOS())
        {
            return "macOS";
        }

        if (OperatingSystem.IsLinux())
        {
            return "Linux";
        }

        return RuntimeInformation.OSDescription.TrimEx();
    }

    private static string GetOsVersionCore()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var displayVersion = ReadRegistryValue(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "DisplayVersion");
                return displayVersion.IsNotEmpty() ? displayVersion : Environment.OSVersion.Version.ToString();
            }

            if (OperatingSystem.IsMacOS())
            {
                var version = RunCommand("sw_vers", "-productVersion");
                return version.IsNotEmpty() ? version : Environment.OSVersion.Version.ToString();
            }

            if (OperatingSystem.IsLinux())
            {
                var osRelease = ReadOsRelease();
                var name = Regex.Match(osRelease, "^NAME=\"?([^\"\\n]+)\"?", RegexOptions.Multiline).Groups[1].Value.Trim();
                var versionId = Regex.Match(osRelease, "^VERSION_ID=\"?([^\"\\n]+)\"?", RegexOptions.Multiline).Groups[1].Value.Trim();

                if (name.IsNotEmpty() && versionId.IsNotEmpty())
                {
                    return $"{name} {versionId}";
                }

                if (name.IsNotEmpty())
                {
                    return name;
                }
            }
        }
        catch
        {
            // Use the runtime OS version below.
        }

        return Environment.OSVersion.Version.ToString();
    }

    private static string GetDeviceModelCore()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var productName = ReadRegistryValue(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "ProductName");
                if (productName.IsNotEmpty())
                {
                    var build = Environment.OSVersion.Version.Build;
                    if (build >= 22000 && productName.Contains("Windows 10", StringComparison.OrdinalIgnoreCase))
                    {
                        return productName.Replace("Windows 10", "Windows 11", StringComparison.OrdinalIgnoreCase);
                    }

                    return productName;
                }

                return $"Windows {Environment.OSVersion.Version}";
            }

            if (OperatingSystem.IsMacOS())
            {
                var model = RunCommand("sysctl", "-n hw.model");
                if (model.IsNullOrEmpty())
                {
                    return "Mac";
                }

                var cpu = RunCommand("sysctl", "-n machdep.cpu.brand_string");
                var chip = Regex.Match(cpu, @"Apple\s+(M\d+\s*\w*)", RegexOptions.IgnoreCase).Groups[1].Value.Trim();
                return chip.IsNotEmpty() ? $"{model} ({chip})" : model;
            }

            if (OperatingSystem.IsLinux())
            {
                var osRelease = ReadOsRelease();
                var prettyName = Regex.Match(osRelease, "^PRETTY_NAME=\"?([^\"\\n]+)\"?", RegexOptions.Multiline).Groups[1].Value.Trim();
                if (prettyName.IsNotEmpty())
                {
                    return prettyName;
                }

                var name = Regex.Match(osRelease, "^NAME=\"?([^\"\\n]+)\"?", RegexOptions.Multiline).Groups[1].Value.Trim();
                return name.IsNotEmpty() ? name : "Linux";
            }
        }
        catch
        {
            // Use a conservative fallback below.
        }

        return RuntimeInformation.OSDescription.TrimEx();
    }

    private static string ReadRegistryValue(string subKey, string valueName)
    {
        if (!OperatingSystem.IsWindows())
        {
            return string.Empty;
        }

        using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(subKey);
        return key?.GetValue(valueName)?.ToString()?.Trim() ?? string.Empty;
    }

    private static string GetNetworkFingerprint()
    {
        try
        {
            var macs = NetworkInterface.GetAllNetworkInterfaces()
                .Where(it => it.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .Select(it => it.GetPhysicalAddress().ToString())
                .Where(it => it.IsNotEmpty() && !Regex.IsMatch(it, "^0+$"))
                .OrderBy(it => it);

            return string.Join(":", macs);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ReadOsRelease()
    {
        return File.Exists("/etc/os-release") ? File.ReadAllText("/etc/os-release") : string.Empty;
    }

    private static string RunCommand(string fileName, string arguments)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            if (!process.Start())
            {
                return string.Empty;
            }

            if (!process.WaitForExit(3000))
            {
                process.Kill(true);
                return string.Empty;
            }

            return process.StandardOutput.ReadToEnd().Trim();
        }
        catch
        {
            return string.Empty;
        }
    }
}
