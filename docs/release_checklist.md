# 定制版本发布检查表

## 版本和源码

- [ ] 发布提交位于 `custom/main` 或 `release/<version>`
- [ ] 工作区干净
- [ ] `.custom/upstream.json` 与实际官方基线一致
- [ ] 版本号采用 `v<upstream>-custom.<revision>`
- [ ] 定制功能清单和设计文档已更新

## 自动验证

- [ ] ServiceLib 全部测试通过
- [ ] Windows x64 Release 构建通过
- [ ] macOS ARM64 Release 构建通过
- [ ] `git diff --check` 通过

## 安装包

- [ ] Windows 包含 Xray、sing-box、mihomo、geodata 和 CFST
- [ ] macOS 包含 Xray、sing-box、mihomo、geodata 和 CFST
- [ ] 默认背景资源存在
- [ ] macOS 可执行权限和 ARM64 架构正确
- [ ] macOS 签名验证通过
- [ ] 两个平台 SHA256 已生成

## 实机验收

- [ ] 全新配置首次启动流程
- [ ] 从上一发布版本原地升级
- [ ] 系统代理启停
- [ ] TUN 启停及退出清理
- [ ] 订阅更新完整自动化
- [ ] CFST 最优节点生成
- [ ] 首页、代理页、背景和流量显示

## GitHub 发布

- [ ] 创建带说明的 annotated tag
- [ ] Release Notes 区分官方变更和定制变更
- [ ] 上传安装包和 SHA256
- [ ] 保留上一稳定版本
