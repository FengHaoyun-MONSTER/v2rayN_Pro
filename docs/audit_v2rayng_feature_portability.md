# v2rayN Pro 功能迁移至 v2rayNG 综合审核

## 审核范围

- 桌面端基线：当前工作区中的 v2rayN 7.23.2 定制代码及 `docs/design_*.md`。
- Android 端基线：`docs/v2rayNG`，v2rayNG 2.2.6，`minSdk 24`、`targetSdk 37`。
- Android 技术栈：Kotlin、XML/ViewBinding、MMKV、OkHttp、协程、WorkManager、前台服务、`libv2ray` AAR。
- 本次只审核可移植性，不修改 Android 代码。

## 总结

按 20 个用户可感知功能点统计：

- 14 项可在 Kotlin/Android 应用层完整实现，其中批量真延迟和延迟排序已有基础代码。
- 4 项可以实现，但必须采用 Android 形态，不能照搬 Windows UI 或进程模型。
- 1 项需要扩展 Go/`libv2ray` 原生层，属于高投入功能。
- 1 项受 Android 安全模型限制，只能实现等价流程，不能静默完成。

因此：

- **用户功能目标约 95% 可落地（19/20）**。
- **可保持与桌面端完全相同语义的约 70%（14/20）**。
- **C#、WPF 代码不能直接复用，预计直接代码复用率仅 10% 到 15%**；可复用的主要是数据规则、正则、Cloudflare 机房字典、流程状态机和测试用例。
- 唯一明确不能原样实现的是“第一次添加订阅后，无交互静默开启代理”。Android 首次使用 VPN 必须由用户确认系统授权。

## 现有 Android 基础

### 已有真延迟能力

v2rayNG 的批量测速不是普通 TCP 连接耗时。`RealPingWorkerService` 为每个节点生成轻量配置，并调用：

```text
Libv2ray.measureOutboundDelay(config, testUrl)
```

当前活动节点的延迟也由正在运行的 `CoreController.measureDelay(testUrl)` 获取。因此列表延迟和当前延迟已经具备桌面端“真延迟”的核心语义。

### 已有排序能力

`MainViewModel.sortByTestResultsForSub` 已将 `delay <= 0` 转换为大值后升序排序，所以失败节点排在最后。需要补的是“订阅更新成功后自动触发测速，再在完成事件中排序和选中首个可用节点”的编排状态，而不是重写排序算法。

### 已有后台任务与前台测速服务

- 订阅定时更新已有 WorkManager 实现。
- 批量真延迟已有 `CoreTestService` 前台服务和进度通知。
- 节点选中、运行中切换后重启核心、订阅分组 ViewPager 均已有实现。

这些能力足以承载自动订阅流程，不需要新建第二套任务框架。

## 逐项审核

| # | 定制功能 | 结论 | Android 实现要点 | 投入 |
|---|---|---|---|---|
| 1 | 新增订阅时 URL 放首位，高级项默认折叠 | 可完整实现 | 调整 `activity_sub_edit.xml`，使用可折叠容器；编辑已有订阅时可默认展开 | 低 |
| 2 | 只填 URL 即可，`profile-title` 作为别名，域名兜底 | 可完整实现 | 取消 `remarks` 必填；先以 URL host 建临时别名，更新成功后仅覆盖自动别名 | 中 |
| 3 | 新增后自动选中新订阅分组 | 可完整实现 | 保存新订阅 ID，在 `setupGroupTab` 后定位 ViewPager，已有无动画切换能力 | 低 |
| 4 | 新增后直连更新当前订阅 | 可完整实现 | 给 `updateConfigViaSub` 增加下载策略；自动流程使用 direct-only，手动更新保持原策略 | 低 |
| 5 | 更新后自动测试当前分组真延迟 | 可完整实现 | 复用 `CoreTestService`，传新订阅 ID；用操作 ID 关联完成事件，避免与手动测速串线 | 中 |
| 6 | 延迟升序，`-1` 排最后 | 已具备核心能力 | 现有排序已满足；显式保留稳定排序，并统一 `0/-1` 的失败定义 | 低 |
| 7 | 按条件保留活动节点，否则选首个可用节点 | 可完整实现 | 检查当前选中节点是否仍存在及 VPN 是否运行；否则选择首个 `delay >= 0` 节点 | 中 |
| 8 | 首次流程自动启用代理 | 需 Android 等价实现 | 自动选择节点后调用 `VpnService.prepare()`；首次必须展示系统授权，授权成功后启动 VPN | 中/平台限制 |
| 9 | 首次无订阅、无节点时自动打开添加订阅 | 可完整实现 | `MainActivity` 首次稳定显示后检查 MMKV；每进程最多弹一次，取消后下次启动再提示 | 低 |
| 10 | 解析流量、到期、公告、支持链接、配置标题 | 可完整实现 | 将 HTTP 返回值从 `String` 改为 `body + headers + finalUrl`；扩展 `SubscriptionItem` 持久化字段 | 中 |
| 11 | 首页订阅仪表盘和“点我有惊喜” | 可完整实现 | 新增 Home Fragment；公告限高滚动，支持链接交给系统 Intent，URL 白名单校验 | 中 |
| 12 | 首页/代理无感切换并记忆页面 | 需 Android 化 | 使用两个常驻 Fragment 或 ViewPager2 禁止滑动；保留 Proxy Fragment 的列表和滚动状态 | 中 |
| 13 | 代理页分组名称下显示流量摘要 | 可完整实现 | 扩展 Tab 自定义视图或在节点列表顶部放当前订阅摘要；不做“全部”分组聚合 | 低 |
| 14 | 列表延迟和当前延迟都是真延迟 | 已具备 | 保持 `measureOutboundDelay` 与 `CoreController.measureDelay`，仅统一 URL 和失败展示 | 低 |
| 15 | 测速同时探测 Cloudflare `colo` 国家/地区 | 可实现，需原生扩展 | 推荐给 `AndroidLibXrayLite` 增加出站 HTTP 探测 API，返回 trace 正文；Kotlin 解析并持久化地区 | 高 |
| 16 | HWID 请求头默认开启、无 UI | 可完整实现 | 对 `ANDROID_ID` 做 SHA-256 后截取 16 位；补充 Android、系统版本和设备型号请求头 | 低 |
| 17 | “以此节点自动生成最优节点”及 100 节点池 | 可实现，原生重构 | 不运行 Windows `cfst.exe`；把 CFST Go 算法重构为可调用库并编入 AAR，前台服务执行 | 很高 |
| 18 | 仅清理生成的最优节点中 `-1` 或 `>500ms` 项 | 可完整实现 | 建议增加 `generatedBy=cloudflareST` 元数据，不只依赖备注；按操作 ID 限定本次测试集合 | 中 |
| 19 | CFST 全日志、进度、心跳、取消和超时 | 需 Android 化 | Go 回调到 Kotlin Flow，显示前台通知和应用内任务页；不依赖控制台、CSV 文件或子进程 stdout | 高 |
| 20 | 文字入口、外观入口、背景图/GIF、透明度 | 需 Android 化 | 静态图可直接实现；GIF 使用生命周期感知图片库，页面不可见时暂停；移动端不照搬桌面表格布局 | 中 |

## 关键技术差异

### 1. 订阅下载目前丢弃响应头

`HttpUtil.getUrlContentWithUserAgent` 当前只返回响应正文，`AngConfigManager.updateConfigViaSub` 因而拿不到：

- `profile-title`
- `subscription-userinfo`
- `announce` / `*-announce`
- `support-url`
- `profile-web-page-url`

应新增结构化返回类型，例如：

```kotlin
data class HttpFetchResult(
    val body: String,
    val headers: Map<String, List<String>>,
    val finalUrl: String
)
```

重定向链路应只持久化最终成功响应的元数据。HWID 请求头必须同时覆盖“经当前代理尝试”和“直连回退”两条路径。

### 2. 自动流程必须使用状态机

不要把更新、测速、排序、选中操作简单串在 Activity 回调中。推荐建立 `SubscriptionOnboardingCoordinator`：

```text
Saved -> Updating -> Testing -> Sorting -> Selecting -> RequestingVpn -> Completed
```

每次操作生成 `operationId`，测速服务完成消息携带该 ID。这样手动测速、定时更新和首次引导并发时，不会误触发自动选中或最优节点清理。

### 3. VPN 不是 Windows 系统代理

Windows 的“自动配置系统代理”可由应用直接切换。Android 的等价能力是启动 `VpnService`：

- 第一次必须调用 `VpnService.prepare()` 并让用户确认。
- 授权可能被用户撤销，不能永久假设有效。
- Android 12 及以后限制后台启动前台服务，因此自动启动应发生在用户可见的首次引导流程中。
- 后台定时更新可以更新、测速和排序，但不应无条件从后台拉起 VPN。

### 4. 国家/机房探测不应启动大量临时代理进程

当前批量真延迟 API 只返回毫秒值，不返回 HTTP 响应正文。推荐扩展原生 API：

```text
probeOutbound(configJson, delayUrl, traceUrl)
  -> { delayMillis, traceText, error }
```

原生层使用同一节点配置发起请求，Kotlin 层解析 `colo` 和 `loc`，复用桌面端机房字典。相比每个节点启动一个临时 SOCKS 端口，这个方案：

- 没有端口竞争。
- 不需要管理几十个临时 CoreController。
- 更容易限制并发、超时和取消。
- 能保证 trace 确实通过被测节点。

注意：当前克隆目录中的 `AndroidLibXrayLite` 是未初始化的 Git 子模块。实施前需执行：

```powershell
git submodule update --init --recursive
```

### 5. CloudflareST 不能照搬 `cfst.exe`

桌面端依赖子进程、stdout/stderr、stdin 关闭、CSV 和进程超时。Android 应改为库内调用：

1. 将 CloudflareST 的扫描、延迟测试、下载测试从 `package main` 拆成无 UI 的 Go package。
2. 将参数封装为结构体，将结果直接返回对象，不写 `result.csv`。
3. 将进度、单项结果和错误改为回调。
4. 编入 v2rayNG 已使用的 Go AAR，避免额外执行动态二进制。
5. 由专用前台服务承载，支持取消、总超时、Wi-Fi 提示和通知进度。

CFST 当前会展开约 5955 个候选并进行高并发网络探测。在手机上继续使用 `-n 1000` 会带来文件描述符、NAT、发热、耗电和移动数据风险。建议 Android 默认并发 128 到 256，并通过真机压力测试确定上限；“不减少候选覆盖面”和“瞬时并发达到 1000”是两个不同目标。

### 6. HWID 的 Android 语义

Android 不应读取硬件序列号或 MAC 地址。推荐：

```text
raw = Settings.Secure.ANDROID_ID
x-hwid = sha256(raw).hex().take(16)
x-device-os = Android
x-ver-os = Build.VERSION.RELEASE
x-device-model = Build.MANUFACTURER + " " + Build.MODEL
```

`ANDROID_ID` 对应用签名、设备和用户相对稳定，但恢复出厂设置、切换用户或更换签名后可能变化。这是 Android 合规语义，应由服务端接受，不应尝试绕过系统限制获取永久硬件标识。

## 推荐实施路线

### 第一阶段：订阅闭环

目标是先复刻最有价值、风险最低的核心体验：

1. URL-only 紧凑订阅编辑器。
2. 结构化订阅 HTTP 返回值和 HWID headers。
3. `profile-title`、流量、到期、公告和支持链接持久化。
4. 新增后自动选中、直连更新、真延迟、升序排序、首个可用节点选中。
5. 首次启动引导和一次性 VPN 系统授权。

### 第二阶段：首页与地区

1. Home/Proxy 导航和首页订阅卡片。
2. 代理页流量摘要。
3. 扩展 `AndroidLibXrayLite`，实现延迟加 Cloudflare trace 探测。
4. 地区缓存、失败重试、并发限制和 UI 展示。

### 第三阶段：Cloudflare 最优节点

1. CFST Go 代码库化并绑定到 AAR。
2. Android 前台任务、日志、进度、取消和超时。
3. 克隆模板节点、显式生成来源标记、100 节点池维护。
4. 生成后真延迟、限定范围清理、排序和活动节点切换。
5. 在至少一台中端 arm64 真机上测试 5955 候选的耗时、峰值内存、文件描述符、耗电和网络切换。

### 第四阶段：视觉定制

1. 静态背景、透明度和外观入口。
2. GIF 背景按 Activity/Fragment 生命周期暂停与恢复。
3. 根据手机、平板和横屏分别设计信息密度，不复制 Windows 双栏表格。

## 建议保留和调整的产品规则

应保持：

- 普通订阅自动测速不删除节点。
- 手动测速不删除节点，也不自动切换节点。
- 只有 CFST 生成节点才执行 `-1` 或 `>500ms` 清理。
- `-1` 永远排到正常延迟之后。
- 不再根据模板节点域名判断是否显示最优节点入口。
- 当前 VPN 已运行且活动节点仍有效时，不自动切换。

应调整：

- “自动配置系统代理”改称“启动 VPN”或“连接”。
- 国家列在 Android 节点卡片中显示为地区副标题，避免模拟桌面表格列。
- CFST 默认仅在用户主动点击后运行，并清晰显示网络消耗；不建议由后台订阅更新自动触发。
- GIF 背景默认关闭动画或仅在前台播放，避免代理常驻时持续消耗 GPU。

## 最终判断

这个迁移项目是可行的，而且 v2rayNG 已经具备最关键的真延迟、分组存储、排序、选中节点、VPN 服务和后台任务基础。第一、二阶段不需要替换代理核心，主要是 Kotlin 业务编排、数据模型和 Android UI 工作。

最大的技术项目只有两个：

1. 给 Go 原生层增加“通过指定出站获取 HTTP 正文”的能力，用于 Cloudflare 机房探测。
2. 将命令行 CloudflareST 重构为 Android 可调用的库，而不是尝试运行 `cfst.exe`。

完成前三阶段后，Android 版可以覆盖定制 v2rayN 的主要业务价值；视觉定制可以独立迭代，不应阻塞订阅与测速闭环。

## 平台依据

- Android `VpnService.prepare()` 要求应用取得用户授权，授权也可能被撤销：<https://developer.android.com/reference/android/net/VpnService>
- Android 12+ 对后台启动前台服务有限制：<https://developer.android.com/develop/background-work/services/fgs/restrictions-bg-start>
- Android 推荐将原生能力作为按 ABI 打包的 `.so` 库使用：<https://developer.android.com/ndk/guides/abis>
- Android 建议避免运行时动态加载代码，优先把功能直接随应用打包：<https://developer.android.com/privacy-and-security/risks/dynamic-code-loading>
