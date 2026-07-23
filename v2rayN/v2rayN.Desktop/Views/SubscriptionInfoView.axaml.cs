namespace v2rayN.Desktop.Views;

public partial class SubscriptionInfoView : ReactiveUserControl<SubscriptionInfoViewModel>
{
    public SubscriptionInfoView()
    {
        InitializeComponent();
        ViewModel = new SubscriptionInfoViewModel();

        this.WhenActivated(disposables =>
        {
            this.OneWayBind(ViewModel, vm => vm.SubscriptionName, v => v.txtSubscriptionName.Text).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.Announcement, v => v.txtAnnouncement.Text).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.TotalTraffic, v => v.txtTotalTraffic.Text).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.RemainingTraffic, v => v.txtRemainingTraffic.Text).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.UsedTraffic, v => v.txtUsedTraffic.Text).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.ExpireDate, v => v.txtExpireDate.Text).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.LastUpdated, v => v.txtLastUpdated.Text).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.HasSupportUrl, v => v.btnSupport.IsVisible).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.OpenSupportCmd, v => v.btnSupport).DisposeWith(disposables);
        });
    }
}
