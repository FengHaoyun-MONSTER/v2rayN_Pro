namespace ServiceLib.ViewModels;

public class SubEditViewModel : MyReactiveObject
{
    private readonly string _originalRemarks;
    private readonly bool _originalAutoRemarks;

    [Reactive]
    public SubItem SelectedSource { get; set; }

    public ReactiveCommand<Unit, Unit> SaveCmd { get; }

    public SubEditViewModel(SubItem subItem, Func<EViewAction, object?, Task<bool>>? updateView)
    {
        _config = AppManager.Instance.Config;
        _updateView = updateView;

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
            _updateView?.Invoke(EViewAction.CloseWindow, null);
        }
        else
        {
            NoticeManager.Instance.Enqueue(ResUI.OperationFailed);
        }
    }
}
