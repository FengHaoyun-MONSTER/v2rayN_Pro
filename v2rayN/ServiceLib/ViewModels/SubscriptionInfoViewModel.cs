namespace ServiceLib.ViewModels;

public class SubscriptionInfoViewModel : MyReactiveObject
{
    [Reactive]
    public string SubscriptionName { get; set; } = "订阅信息";

    [Reactive]
    public string Announcement { get; set; } = "选择一个订阅分组以查看订阅信息";

    [Reactive]
    public string RemainingTraffic { get; set; } = "-";

    [Reactive]
    public string TotalTraffic { get; set; } = "-";

    [Reactive]
    public string UsedTraffic { get; set; } = "-";

    [Reactive]
    public string ExpireDate { get; set; } = "-";

    [Reactive]
    public string LastUpdated { get; set; } = "尚未更新";

    [Reactive]
    public bool HasSubscription { get; set; }

    [Reactive]
    public string SupportUrl { get; set; } = string.Empty;

    [Reactive]
    public bool HasSupportUrl { get; set; }

    public ReactiveCommand<Unit, Unit> OpenSupportCmd { get; }

    public SubscriptionInfoViewModel()
    {
        _config = AppManager.Instance.Config;
        OpenSupportCmd = ReactiveCommand.Create(
            () => ProcUtils.ProcessStart(SupportUrl),
            this.WhenAnyValue(x => x.HasSupportUrl));

        AppEvents.SubscriptionSelectionChanged
            .AsObservable()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(async subId => await Refresh(subId));

        AppEvents.SubscriptionsRefreshRequested
            .AsObservable()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(async _ => await Refresh(_config.SubIndexId));

        _ = Refresh(_config.SubIndexId);
    }

    private async Task Refresh(string? subId)
    {
        var item = subId.IsNotEmpty()
            ? await AppManager.Instance.GetSubItem(subId)
            : null;

        if (item is null)
        {
            HasSubscription = false;
            SubscriptionName = "订阅信息";
            Announcement = "选择一个订阅分组以查看订阅信息";
            RemainingTraffic = "-";
            TotalTraffic = "-";
            UsedTraffic = "-";
            ExpireDate = "-";
            LastUpdated = "尚未更新";
            SupportUrl = string.Empty;
            HasSupportUrl = false;
            return;
        }

        HasSubscription = true;
        SubscriptionName = item.Remarks.IsNotEmpty() ? item.Remarks : "未命名订阅";
        Announcement = item.Announce.IsNotEmpty() ? item.Announce! : "暂无订阅公告";

        var usedTraffic = Math.Max(0, item.TrafficUpload) + Math.Max(0, item.TrafficDownload);
        TotalTraffic = item.TrafficTotal > 0 ? FormatGigabytes(item.TrafficTotal) : "∞";
        UsedTraffic = FormatGigabytes(usedTraffic);
        RemainingTraffic = item.TrafficTotal > 0
            ? FormatGigabytes(Math.Max(0, item.TrafficTotal - usedTraffic))
            : "∞";

        if (item.ExpireTime > 0)
        {
            try
            {
                var expire = DateTimeOffset.FromUnixTimeSeconds(item.ExpireTime).ToLocalTime();
                ExpireDate = expire.ToString("yyyy-MM-dd");
            }
            catch (ArgumentOutOfRangeException)
            {
                ExpireDate = "-";
            }
        }
        else
        {
            ExpireDate = "永久";
        }

        LastUpdated = item.UpdateTime > 0
            ? $"更新于 {DateTimeOffset.FromUnixTimeSeconds(item.UpdateTime).ToLocalTime():yyyy-MM-dd HH:mm}"
            : "尚未更新";

        SupportUrl = NormalizeSupportUrl(item.SupportUrl);
        HasSupportUrl = SupportUrl.IsNotEmpty();
    }

    private static string NormalizeSupportUrl(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return string.Empty;
        }

        return uri.Scheme is "http" or "https" or "tg" ? uri.AbsoluteUri : string.Empty;
    }

    private static string FormatGigabytes(long bytes)
    {
        const double bytesPerGigabyte = 1024d * 1024d * 1024d;
        return $"{bytes / bytesPerGigabyte:#,##0.##} GB";
    }
}
