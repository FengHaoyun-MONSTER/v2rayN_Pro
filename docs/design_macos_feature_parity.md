# macOS 功能对等移植设计

## 目标

macOS 使用 `v2rayN.Desktop`（Avalonia），Windows 使用 `v2rayN`（WPF）。两端共享
`ServiceLib`，但界面、窗口和平台资源必须分别实现。本次移植要求 macOS 能访问并展示
Windows 定制版本的全部业务功能。

## 功能对等矩阵

| 功能 | 共享逻辑 | Windows WPF | macOS Avalonia |
| --- | --- | --- | --- |
| 新增订阅后直连更新 | `ProfilesViewModel` | 已接入 | 已接入 |
| 自动真延迟、排序、活动节点与系统代理 | `ProfilesViewModel` | 已接入 | 已接入 |
| 首次启动自动打开新增订阅 | `AppEvents` | 已接入 | 已接入 |
| URL 优先、自动别名、折叠高级设置 | `SubEditViewModel` | 已实现 | 已实现 |
| HWID 请求头 | `DeviceInfoHelper` | 共享 | 共享 |
| Cloudflare Trace 国家/机房 | `SpeedtestService` | 已展示 | 已展示 |
| CFST 自动生成最优节点 | `CloudflareSpeedTestService` | 已有菜单 | 已有菜单 |
| CFST 节点专属清理规则 | `ProfilesViewModel` | 共享 | 共享 |
| 首页订阅信息、公告、流量、到期时间 | `SubscriptionInfoViewModel` | 已实现 | 已实现 |
| 首页 / 添加订阅 / 代理导航 | 配置保存共享 | 已实现 | 已实现 |
| 代理页订阅流量摘要 | `ProfilesViewModel` | 已展示 | 已展示 |
| 图片/GIF 背景与透明度 | 配置与文件管理共享 | WPF 动画 | Avalonia/Skia 动画 |
| 外观入口 | 无 | 已实现 | 已实现 |

## Avalonia 实现要点

- `MainWindow` 一次性创建首页和代理页，只切换可见性，不重新创建节点表、日志或 ViewModel。
- `ProfilesView` 接收 `AddSubscriptionRequested`，修复首次启动只有发布端、没有接收端的问题。
- CFST 菜单直接绑定共享命令，不增加域名判断。
- 首页绑定共享 `SubscriptionInfoViewModel`，支持公告、流量、到期时间和服务商链接。
- 背景图位于首页和代理页共同父容器中，顶部工具栏和底部状态栏保持实色。
- GIF 使用 SkiaSharp 按需解码当前帧并写入复用画布，不预解码全部帧；窗口隐藏或最小化时暂停。
- 默认背景发布到 `guiConfigs/default-workspace-background.gif`，新配置默认透明度为 100%。

## 打包要求

- 分别发布 `osx-arm64` 和 `osx-x64`，禁止将不同架构的主程序或 core 混装。
- `.app/Contents/MacOS/bin/cfst/` 必须包含对应架构的 `cfst` 和 `ip.txt`，并保留可执行权限。
- `.app/Contents/MacOS/guiConfigs/` 必须包含默认 GIF。
- `.app` 完成 ad-hoc 签名后再使用保留 Unix 权限的 ZIP 方式发布。

## 验证清单

1. 首次启动无数据时自动显示新增订阅窗口。
2. 仅填写订阅链接可保存，并完成更新、真延迟、国家探测和排序。
3. 已有活动节点且系统代理为自动配置时不切换节点；否则选择首个可用节点并启用自动系统代理。
4. 首页与代理页切换不清空列表、日志或当前选择。
5. CFST 菜单对所有正常节点显示，日志持续写入 v2rayN 日志区域。
6. 普通自动测速和手动测速不删除节点；仅 CFST 生成的最优节点执行 `-1` 或 `>500ms` 清理。
7. 静态图片和 GIF 均可显示，透明度即时生效，重新启动后保持设置。
