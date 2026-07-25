# macOS system proxy fix

## Symptom

The UI reports `ForcedChange`, but HTTP, HTTPS, and SOCKS remain disabled in
macOS System Settings. No command result is shown in the main log.

## Findings

The customized source matched upstream v2rayN in this area. The failure was in
the inherited macOS implementation and in the Windows cross-package path:

1. The built-in script invoked write operations of `networksetup` without sudo.
2. `ProxySettingOSX` discarded successful output and did not propagate failure to
   `SysProxyHandler` or the status-bar state.
3. The generated built-in script used `overwrite=false`, so upgrades retained an
   old script in `binConfigs`.
4. Extensionless embedded shell resources were not matched by the existing
   `*.sh text eol=lf` rule. A Windows build could therefore embed CRLF and write a
   script with an invalid `/bin/bash\r` shebang on macOS.

The same user-visible behavior is documented in upstream issue #6462 and the
upstream FAQ. It is not caused by the customized home/proxy UI.

## Fix

- Reuse the existing validated in-memory sudo password used by TUN.
- Execute the built-in proxy script through `/usr/bin/sudo -S`.
- Preserve direct execution for user-supplied custom proxy scripts.
- Always refresh the generated built-in script after an application upgrade.
- Normalize shell contents to LF when writing and enforce LF for `Sample/*_sh`.
- Use `/usr/sbin/networksetup` and enumerate every enabled network service.
- Log command start, exit code, stdout, stderr, and resulting proxy state.
- Propagate failures and keep the previous UI/config state when macOS rejects the
  update.
- Save automatic `ForcedChange` only after the system proxy command succeeds.

## Expected test log

A successful set operation contains an exit code of zero and `Enabled: Yes` for
HTTP, HTTPS, and SOCKS on the active network service. A failure contains the
actual sudo or `networksetup` error and does not report a successful state
change.