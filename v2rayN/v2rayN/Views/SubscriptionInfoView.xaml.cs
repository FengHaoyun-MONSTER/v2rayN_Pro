namespace v2rayN.Views;

public partial class SubscriptionInfoView
{
    public SubscriptionInfoView()
    {
        InitializeComponent();
        ViewModel = new SubscriptionInfoViewModel();
        btnHideSubscriptionDashboard.Click += (_, _) => AppEvents.SubscriptionDashboardVisibilityChanged.Publish(false);

        this.WhenActivated(disposables =>
        {
            this.OneWayBind(ViewModel, vm => vm.SubscriptionName, v => v.txtSubscriptionName.Text).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.Announcement, v => v.txtAnnouncement.Text).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.RemainingTraffic, v => v.txtRemainingTraffic.Text).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.RemainingDays, v => v.txtRemainingDays.Text).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.ExpireDate, v => v.txtExpireDate.Text).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.LastUpdated, v => v.txtLastUpdated.Text).DisposeWith(disposables);
        });
    }
}
