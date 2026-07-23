# macOS arm64 定制功能对等审计

## 比较基线

- 当前定制分支基于官方 `7.23.2`，本地基线标签为 `v7.23.2-freedompost-baseline.1`。
- 远程官方仓库为 `2dust/v2rayN`，本次同时检查了 `7.23.2` 与 `7.24.1`。
- `7.24.1` 包含较大的 Avalonia 12 与 ViewModel-First 重构。为避免把未提交的定制功能卷入高风险合并，本次只吸收 macOS 发布约束，不直接合并整个上游版本。

## 功能结论

以下业务能力位于共享 `ServiceLib`，Windows WPF 与 macOS Avalonia 使用同一实现：

- HWID 与设备信息订阅请求头；macOS 使用 `ioreg`、`sw_vers` 和 `sysctl`。
- 新增订阅后直连更新、真延迟测试、延迟升序排序与条件切换活动节点。
- Cloudflare trace 机房/国家探测。
- CloudflareST 最优节点生成、实时日志、100 节点池与仅针对“最优节点”的失效清理。
- 订阅标题、公告、流量、到期时间和支持链接解析。

以下界面能力已在 `v2rayN.Desktop` 单独实现：

- 首页、添加订阅、代理三入口与无重建页面切换。
- 首次空配置自动打开添加订阅窗口。
- 首页订阅看板与代理页流量摘要。
- 国家列固定显示在地址列后。
- 所有节点均显示“以此节点自动生成最优节点”。
- URL 优先、别名可选、默认折叠高级字段的订阅编辑窗口。
- 静态图片/GIF 背景、100% 默认透明度、外观入口和状态持久化。

## arm64 发布方案

- `.github/workflows/build-custom-macos-arm64.yml` 在原生 macOS runner 上发布 `osx-arm64`，不再由 Windows 拼装最终安装包。
- `package-osx.sh` 下载官方 arm64 core 与 XIU2 CloudflareST `v2.3.5` 的 `cfst_darwin_arm64.zip`。
- 包内强制校验 `bin/cfst/cfst`、`bin/cfst/ip.txt` 和默认 GIF。
- 完整 `.app` 组装后执行深度签名校验，再创建并挂载验证 DMG。
- 工作流遍历包内 Mach-O 文件，任何不含 arm64 的文件都会使构建失败。
- 最低系统版本与官方 `7.24.1` macOS 包保持一致，为 macOS `13.7`。

## 签名边界

没有 Apple Developer 证书时，工作流执行 ad-hoc 签名，可保证包内原生文件签名一致，但不能替代 Apple 公证。正式对外分发时应通过 `APPLE_CODESIGN_IDENTITY` 使用 Developer ID，并补充 notarization 凭据与步骤。

## 本机可验证范围

- Windows 可完成 `osx-arm64` 交叉发布、单元测试、Mach-O 架构解析、默认资源和 CFST arm64 文件校验。
- DMG 创建、`codesign`、`hdiutil` 挂载与 Gatekeeper 行为必须在 macOS runner 或真实 Apple Silicon 设备完成。
