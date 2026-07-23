using Avalonia.Platform;
using SkiaSharp;

namespace v2rayN.Desktop.Views;

public sealed class AnimatedGifImage : Image, IDisposable
{
    private readonly DispatcherTimer _timer;
    private SKCodec? _codec;
    private SKBitmap? _decodedFrame;
    private WriteableBitmap? _displayBitmap;
    private SKCodecFrameInfo[] _frameInfos = [];
    private SKImageInfo _imageInfo;
    private int _frameIndex;
    private bool _playRequested;

    public AnimatedGifImage()
    {
        _timer = new DispatcherTimer(DispatcherPriority.Background);
        _timer.Tick += (_, _) => DecodeNextFrame();
    }

    public bool LoadFile(string imagePath)
    {
        Clear();
        if (imagePath.IsNullOrEmpty() || !File.Exists(imagePath))
        {
            return false;
        }

        if (!string.Equals(Path.GetExtension(imagePath), ".gif", StringComparison.OrdinalIgnoreCase))
        {
            using var stream = File.OpenRead(imagePath);
            Source = new Avalonia.Media.Imaging.Bitmap(stream);
            return true;
        }

        _codec = SKCodec.Create(imagePath);
        if (_codec is null)
        {
            return false;
        }

        var info = _codec.Info;
        _imageInfo = new SKImageInfo(info.Width, info.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        _decodedFrame = new SKBitmap(_imageInfo);
        _displayBitmap = new WriteableBitmap(
            new PixelSize(info.Width, info.Height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);
        _frameInfos = _codec.FrameInfo ?? [];
        Source = _displayBitmap;
        _frameIndex = 0;
        DecodeFrame(0, -1);
        return true;
    }

    public void SetPlaying(bool playing)
    {
        _playRequested = playing;
        if (playing && _frameInfos.Length > 1)
        {
            ScheduleNextFrame();
        }
        else
        {
            _timer.Stop();
        }
    }

    public void Clear()
    {
        _timer.Stop();
        _playRequested = false;
        Source = null;
        _displayBitmap?.Dispose();
        _displayBitmap = null;
        _decodedFrame?.Dispose();
        _decodedFrame = null;
        _codec?.Dispose();
        _codec = null;
        _frameInfos = [];
        _frameIndex = 0;
    }

    public void Dispose()
    {
        Clear();
    }

    private void DecodeNextFrame()
    {
        _timer.Stop();
        if (!_playRequested || _codec is null || _frameInfos.Length <= 1)
        {
            return;
        }

        var priorFrame = _frameIndex;
        _frameIndex = (_frameIndex + 1) % _frameInfos.Length;
        if (_frameIndex == 0)
        {
            priorFrame = -1;
            _decodedFrame?.Erase(SKColors.Transparent);
        }

        DecodeFrame(_frameIndex, priorFrame);
        ScheduleNextFrame();
    }

    private void ScheduleNextFrame()
    {
        if (!_playRequested || _frameInfos.Length <= 1)
        {
            return;
        }

        var duration = _frameInfos[Math.Clamp(_frameIndex, 0, _frameInfos.Length - 1)].Duration;
        _timer.Interval = TimeSpan.FromMilliseconds(Math.Clamp(duration, 20, 1000));
        _timer.Start();
    }

    private unsafe void DecodeFrame(int frameIndex, int priorFrame)
    {
        if (_codec is null || _decodedFrame is null || _displayBitmap is null)
        {
            return;
        }

        var result = _codec.GetPixels(
            _imageInfo,
            _decodedFrame.GetPixels(),
            _decodedFrame.RowBytes,
            new SKCodecOptions(frameIndex, priorFrame));
        if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
        {
            return;
        }

        using var frameBuffer = _displayBitmap.Lock();
        var source = (byte*)_decodedFrame.GetPixels();
        var destination = (byte*)frameBuffer.Address;
        var rowBytes = Math.Min(_decodedFrame.RowBytes, frameBuffer.RowBytes);
        for (var y = 0; y < _imageInfo.Height; y++)
        {
            Buffer.MemoryCopy(
                source + y * _decodedFrame.RowBytes,
                destination + y * frameBuffer.RowBytes,
                frameBuffer.RowBytes,
                rowBytes);
        }

        InvalidateVisual();
    }
}
