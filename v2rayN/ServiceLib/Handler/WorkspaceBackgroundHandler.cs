namespace ServiceLib.Handler;

public static class WorkspaceBackgroundHandler
{
    public const string DefaultImageFileName = "default-workspace-background.gif";

    public static string ResolvePath(string? image)
    {
        if (image.IsNullOrEmpty())
        {
            return string.Empty;
        }

        if (Path.IsPathRooted(image))
        {
            return image;
        }

        var userImagePath = Utils.GetConfigPath(image);
        if (File.Exists(userImagePath))
        {
            return userImagePath;
        }

        if (string.Equals(image, DefaultImageFileName, StringComparison.OrdinalIgnoreCase))
        {
            var bundledImagePath = Utils.GetBaseDirectory(Path.Combine("guiConfigs", DefaultImageFileName));
            if (File.Exists(bundledImagePath))
            {
                return bundledImagePath;
            }
        }

        return userImagePath;
    }

    public static string PrepareImage(string? sourcePath)
    {
        if (sourcePath.IsNullOrEmpty())
        {
            return string.Empty;
        }

        var source = Path.GetFullPath(sourcePath);
        if (!File.Exists(source))
        {
            throw new FileNotFoundException("找不到所选背景图片。", source);
        }

        var extension = Path.GetExtension(source).ToLowerInvariant();
        if (extension is not (".jpg" or ".jpeg" or ".png" or ".bmp" or ".gif"))
        {
            throw new InvalidOperationException("仅支持 JPG、PNG、BMP 和 GIF 图片。");
        }

        if (extension == ".gif")
        {
            ValidateAnimatedGif(source);
        }

        var defaultPath = Path.GetFullPath(Utils.GetConfigPath(DefaultImageFileName));
        if (string.Equals(source, defaultPath, StringComparison.OrdinalIgnoreCase))
        {
            return DefaultImageFileName;
        }

        var fileName = $"workspace-background{extension}";
        var destination = Path.GetFullPath(Utils.GetConfigPath(fileName));
        if (!string.Equals(source, destination, StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(source, destination, true);
        }

        return fileName;
    }

    public static void CleanupUserImages(string keepFileName)
    {
        try
        {
            var configPath = Utils.GetConfigPath();
            var keepPath = keepFileName.IsNotEmpty()
                ? Path.GetFullPath(Utils.GetConfigPath(keepFileName))
                : string.Empty;

            foreach (var file in Directory.GetFiles(configPath, "workspace-background.*", SearchOption.TopDirectoryOnly))
            {
                if (!string.Equals(Path.GetFullPath(file), keepPath, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(file);
                }
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog("Cleanup workspace background failed", ex);
        }
    }

    private static void ValidateAnimatedGif(string source)
    {
        const long maxFileSize = 50L * 1024 * 1024;
        const long maxPixelCount = 1920L * 1080;

        var fileInfo = new FileInfo(source);
        if (fileInfo.Length > maxFileSize)
        {
            throw new InvalidOperationException("GIF 动图不能超过 50 MB。");
        }

        Span<byte> header = stackalloc byte[10];
        using var stream = File.OpenRead(source);
        if (stream.Read(header) != header.Length
            || header[0] != (byte)'G'
            || header[1] != (byte)'I'
            || header[2] != (byte)'F')
        {
            throw new InvalidOperationException("所选文件不是有效的 GIF 动图。");
        }

        var width = header[6] | header[7] << 8;
        var height = header[8] | header[9] << 8;
        if (width <= 0 || height <= 0 || (long)width * height > maxPixelCount)
        {
            throw new InvalidOperationException("GIF 动图画布不能超过约 1920 x 1080 像素。");
        }
    }
}
