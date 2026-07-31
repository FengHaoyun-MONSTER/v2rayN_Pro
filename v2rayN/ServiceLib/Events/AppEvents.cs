namespace ServiceLib.Events;

public static class AppEvents
{
    public static readonly EventChannel<Unit> AddServerViaClipboardRequested = new();
    public static readonly EventChannel<Unit> AddSubscriptionRequested = new();
    public static readonly EventChannel<bool> SubscriptionsUpdateRequested = new();
    public static readonly EventChannel<string> CurrentSubscriptionUpdateRequested = new();
    public static readonly EventChannel<bool> HasUpdateNotified = new();

    public static readonly EventChannel<Unit> ProfilesRefreshRequested = new();
    public static readonly EventChannel<Unit> SubscriptionsRefreshRequested = new();
    public static readonly EventChannel<string> SubscriptionSelectionChanged = new();
    public static readonly EventChannel<Unit> SubscriptionAutoSpeedtestRequested = new();
    public static readonly EventChannel<Unit> OneClickNetworkSetupRequested = new();
    public static readonly EventChannel<Unit> EditCurrentSubscriptionRequested = new();
    public static readonly EventChannel<NetworkAvailabilityInfo> NetworkAvailabilityChanged = new();
    public static readonly EventChannel<Unit> ProxiesReloadRequested = new();
    public static readonly EventChannel<ServerSpeedItem> DispatcherStatisticsRequested = new();

    public static readonly EventChannel<string> SendSnackMsgRequested = new();
    public static readonly EventChannel<string> SendMsgViewRequested = new();

    public static readonly EventChannel<Unit> AppExitRequested = new();
    public static readonly EventChannel<bool> ShutdownRequested = new();

    public static readonly EventChannel<ESysProxyType> SysProxyChangeRequested = new();
}
