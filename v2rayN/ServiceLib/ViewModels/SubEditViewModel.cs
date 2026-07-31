namespace ServiceLib.ViewModels;

public class SubEditViewModel : MyReactiveObject, ICloseable
{
    private readonly string _originalRemarks;
    private readonly bool _originalAutoRemarks;

    public event EventHandler? RequestClose;

    [Reactive]
    public SubItem SelectedSource { get; set; }

    public ReactiveCommand<Unit, Unit> SelectPrevProfileCmd { get; }
    public ReactiveCommand<Unit, Unit> SelectNextProfileCmd { get; }
    public ReactiveCommand<Unit, Unit> SaveCmd { get; }

    public SubEditViewModel(SubItem subItem)
    {
        _config = AppManager.Instance.Config;

        SelectPrevProfileCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            var profileItem = await SelectProfileAsync();
            if (profileItem != null)
            {
                SelectedSource?.PrevProfile = profileItem.Remarks;
                SelectedSource = JsonUtils.DeepCopy(SelectedSource);
            }
        });
        SelectNextProfileCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            var profileItem = await SelectProfileAsync();
            if (profileItem != null)
            {
                SelectedSource?.NextProfile = profileItem.Remarks;
                SelectedSource = JsonUtils.DeepCopy(SelectedSource);
            }
        });
        SaveCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await SaveSubAsync();
        });

        SelectedSource = subItem.Id.IsNullOrEmpty() ? subItem : JsonUtils.DeepCopy(subItem);
        _originalRemarks = SelectedSource.Remarks;
        _originalAutoRemarks = SelectedSource.AutoRemarks;
    }

    private async Task SaveSubAsync()
    {
        var url = SelectedSource.Url;
        Uri? uri = null;
        if (url.IsNotEmpty())
        {
            uri = Utils.TryUri(url);
            if (uri == null)
            {
                NoticeManager.Instance.Enqueue(ResUI.InvalidUrlTip);
                return;
            }
            //Do not allow http protocol
            if (url.StartsWith(Global.HttpProtocol) && !Utils.IsPrivateNetwork(uri.IdnHost))
            {
                NoticeManager.Instance.Enqueue(ResUI.InsecureUrlProtocol);
                //return;
            }
        }

        var remarks = SelectedSource.Remarks;
        if (remarks.IsNullOrEmpty())
        {
            if (uri != null)
            {
                SelectedSource.Remarks = uri.IdnHost.IsNotEmpty() ? uri.IdnHost : "新订阅";
                SelectedSource.AutoRemarks = true;
            }
            else
            {
                NoticeManager.Instance.Enqueue(ResUI.PleaseFillRemarks);
                return;
            }
        }
        else if (!_originalAutoRemarks || !string.Equals(remarks, _originalRemarks, StringComparison.Ordinal))
        {
            SelectedSource.AutoRemarks = false;
        }

        if (await ConfigHandler.AddSubItem(_config, SelectedSource) == 0)
        {
            NoticeManager.Instance.Enqueue(ResUI.OperationSuccess);
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            NoticeManager.Instance.Enqueue(ResUI.OperationFailed);
        }
    }

    private async Task<ProfileItem?> SelectProfileAsync()
    {
        var profileSelectViewModel = new ProfilesSelectViewModel();
        profileSelectViewModel.SetConfigTypeFilter([EConfigType.Custom], exclude: true);
        var result = await AppManager.Instance.WindowDialog.ShowDialogAsync(profileSelectViewModel);
        if (result != true)
        {
            return null;
        }
        var profileItem = await profileSelectViewModel.GetProfileItem();
        return profileItem;
    }
}
