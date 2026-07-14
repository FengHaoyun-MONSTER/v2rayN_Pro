# v2rayN HWID 订阅请求头设计

## 目标

在不增加 UI、不开关、不展示额外信息的前提下，所有订阅更新请求默认携带 HWID 相关请求头，兼容 Koala Clash / Happ 一类订阅服务端的设备绑定逻辑。

## 请求头

订阅下载时默认追加以下请求头：

- `x-hwid`: 稳定设备指纹，使用原始机器标识做 SHA256 后取前 16 位小写十六进制。
- `x-device-os`: `Windows` / `macOS` / `Linux`。
- `x-ver-os`: 系统版本。
- `x-device-model`: 设备/系统型号描述。

用户自定义的订阅 `User-Agent` 保持原逻辑不变。

## HWID 来源

优先使用平台稳定机器标识：

- Windows: `HKLM\SOFTWARE\Microsoft\Cryptography\MachineGuid`
- macOS: `IOPlatformUUID`
- Linux: `/etc/machine-id` 或 `/var/lib/dbus/machine-id`

如果平台机器标识不可用，则回退到网卡 MAC、机器名、CPU 标识、系统描述拼接后的哈希，避免生成空指纹。

## 接入点

实现文件：

- `v2rayN/ServiceLib/Common/DeviceInfoHelper.cs`
- `v2rayN/ServiceLib/Handler/SubscriptionHandler.cs`
- `v2rayN/ServiceLib/Services/DownloadService.cs`
- `v2rayN/ServiceLib/Helper/DownloaderHelper.cs`

订阅下载链路为：

`SubscriptionHandler` -> `DownloadService.TryDownloadString` -> `HttpClient`，失败后回退到 `DownloaderHelper`。

HWID headers 同时接入 `HttpClient` 主路径和 `DownloaderHelper` 兜底路径；当“通过代理”下载失败并自动直连重试时，重试请求也会携带同一组 HWID headers。
