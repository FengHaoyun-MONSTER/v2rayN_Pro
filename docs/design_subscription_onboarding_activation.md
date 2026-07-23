# Subscription onboarding and automatic activation

## Behavior

- On startup, when both the subscription table and profile table are empty, show the Home page and open the existing Add Subscription dialog.
- Cancelling the dialog does not reopen it during the same process. A later startup checks again.
- A successful subscription update keeps the existing automatic sequence: real delay test, ascending delay sort, and failed delay values (`-1`) at the end.
- After sorting, keep the current global active profile only when it still exists and the system proxy mode is already `ForcedChange`.
- Otherwise, select the first non-custom profile whose delay is at least zero, persist it as active, enable `ForcedChange`, reload the core, and refresh the system proxy UI.
- If every tested profile has delay `-1`, keep the current state and show a failure notice instead of enabling the system proxy.

## Scope

The post-test activation rule is shared by normal subscription updates and the Cloudflare optimal-node workflow. Manual delay tests do not request automatic sorting, activation, or proxy changes.
