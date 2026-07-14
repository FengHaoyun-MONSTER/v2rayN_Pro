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
    public string RemainingDays { get; set; } = "-";

    [Reactive]
    public string ExpireDate { get; set; } = "-";

    [Reactive]
    public string LastUpdated { get; set; } = "尚未更新";

    [Reactive]
    public bool HasSubscription { get; set; }

    public SubscriptionInfoViewModel()
    {
        _config = AppManager.Instance.Config;

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
            RemainingDays = "-";
            ExpireDate = "-";
            LastUpdated = "尚未更新";
            return;
        }

        HasSubscription = true;
        SubscriptionName = item.Remarks.IsNotEmpty() ? item.Remarks : "未命名订阅";
        Announcement = item.Announce.IsNotEmpty() ? item.Announce! : "暂无订阅公告";

        var usedTraffic = Math.Max(0, item.TrafficUpload) + Math.Max(0, item.TrafficDownload);
        RemainingTraffic = item.TrafficTotal > 0
            ? FormatGigabytes(Math.Max(0, item.TrafficTotal - usedTraffic))
            : "∞";

        if (item.ExpireTime > 0)
        {
            try
            {
                var expire = DateTimeOffset.FromUnixTimeSeconds(item.ExpireTime).ToLocalTime();
                var remaining = expire - DateTimeOffset.Now;
                RemainingDays = Math.Max(0, (int)Math.Floor(remaining.TotalDays)).ToString();
                ExpireDate = expire.ToString("yyyy-MM-dd");
            }
            catch (ArgumentOutOfRangeException)
            {
                RemainingDays = "-";
                ExpireDate = "-";
            }
        }
        else
        {
            RemainingDays = "∞";
            ExpireDate = "永久";
        }

        LastUpdated = item.UpdateTime > 0
            ? $"更新于 {DateTimeOffset.FromUnixTimeSeconds(item.UpdateTime).ToLocalTime():yyyy-MM-dd HH:mm}"
            : "尚未更新";
    }

    private static string FormatGigabytes(long bytes)
    {
        const double bytesPerGigabyte = 1024d * 1024d * 1024d;
        return $"{bytes / bytesPerGigabyte:#,##0.##} GB";
    }
}
