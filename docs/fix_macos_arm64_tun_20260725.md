# macOS ARM64 TUN connectivity fix

## Symptom

Xray starts, but enabling TUN prevents traffic and the current real delay becomes
`-1`. The log repeatedly contains:

```text
Unables to find local process name: common/net: process lookup is not supported on this platform
```

## Root cause

The packaged application still contained Xray `26.6.1`. v2rayN's Xray TUN
routing uses process matchers to keep its own traffic out of the TUN loop, while
that Xray version did not implement process lookup on Darwin.

Darwin process lookup was added by Xray `v26.7.11`. The macOS package now
overrides the generic core bundle with the official `v26.7.11` macOS binary and
verifies the release archive checksum. The Windows-side ZIP packager verifies
the exact ARM64 Xray binary checksum so an older cached runtime cannot be
published accidentally.

## Upstream v2rayN alignment

The following post-7.23.2 upstream fixes are applied without rewriting the TUN
implementation:

- `74ab7ad0` - protect only core processes used by the active outbound chain
- `ca9978b6` - follow-up protection fix
- `e6153145` - enable Xray TUN sniffing `routeOnly`
- `223642dd` - preserve the runtime environment when launching the privileged core
- `09ea4890` - follow-up TUN traffic takeover fix

## Packaging contract

- Xray release: `v26.7.11`
- ARM64 archive SHA256:
  `61f8f74d099098af710fa43613d9934d97b901dee909801d34f496cd463956d1`
- ARM64 `xray` binary SHA256:
  `672590b1c35b1d8cd7ba3e786ab53189001f32e46369e18c0d81d099a4d66c52`

When Xray is upgraded later, update these checksums intentionally in
`package-osx.sh` and `package-osx-windows.py`.
