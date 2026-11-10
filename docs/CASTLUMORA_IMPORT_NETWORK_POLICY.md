# Import network policy prerequisite

The internal `ImportNetworkPolicy` is a tested prerequisite for the authorized media
queue, not an enabled import service. Managed runtime readiness still reports
`downloadJobsAvailable=false`. No public endpoint accepts URLs or starts downloads.

## Implemented boundary

- Absolute HTTPS only, port 443, maximum 4,096 characters; no credentials,
  fragments, control characters, whitespace or backslashes. Ordinary HTTPS query
  strings remain supported; callers must not log source credentials/query tokens.
- Reject local names and private, loopback, link-local, shared-address, multicast,
  reserved and documentation IPv4 ranges. IPv4-mapped IPv6 is checked as IPv4.
  IPv6 admission is conservative global unicast, excluding special transition,
  protocol-assignment and documentation ranges and scoped addresses.
- Resolve on **every connection**, bound DNS/connect work to ten seconds, limit
  answers to 64 addresses, and reject the entire answer if any address is unsafe.
  An empty answer fails closed. No fallback to an unvalidated DNS result.
- Connect to the approved **numeric IP endpoint**, fixed port 443; do not hand the
  hostname to a second resolver. The returned stream owns the socket. Connection
  failure disposes that socket; caller cancellation propagates.

The connection helper returns a raw transport stream. The eventual HTTP/proxy
adapter must preserve TLS hostname/certificate validation against the original
hostname, reapply this policy for every redirect and extracted resource, and cap
headers, connections, bytes and lifetime. A validated starting URL alone does not
make an unconstrained yt-dlp subprocess safe. No external downloader may bypass
the egress adapter. Never disable TLS verification or trust arbitrary proxy URLs.

The built-in direct socket adapter is intended for the self-hosted application.
It has not been exercised against public services in this managed environment,
which uses its supplied network proxy. Deterministic resolver/connector fixtures
prove policy decisions and DNS-pinned endpoint selection without contacting third
parties. Public egress, TLS, redirects and the eventual yt-dlp proxy integration
require separate end-to-end verification before downloads can be enabled.

## Verification and next work

API Release build: zero warnings/errors. Complete API suite: 264 passed, three
existing platform skips; 59 new network-policy cases. Existing actual pinned Linux
runtime health tests also ran. Targeted formatting checked separately. No frontend,
streaming, hardware or real download test is claimed for this internal-only change.

Next: bounded authenticated subprocess egress adapter with redirect/TLS/resource
fixtures, then destination/path policy and durable job transitions, owned-process
cancel/retry/restart, provenance/deduplication, native admin controls and scan after
an authorized successful import. Keep the verified runtime preview intact. Bulk,
playlist/channel and scheduled imports remain mandatory subsequent increments.

## Authenticated per-job tunnel adapter

`ImportTunnel` now provides an internal IPv4-loopback-only CONNECT proxy with a
fresh 256-bit Basic authentication secret per instance. It accepts only HTTPS
port443 targets; every tunnel runs ImportNetworkPolicy, including new connections
for redirects/resources. It never decrypts TLS or replaces certificate validation.
Proxy URLs contain secrets and must never enter logs, API payloads or job history.

Limits: 4 active connections, 32 total accepted clients, 4KiB ASCII headers with a
five-second deadline, ten-minute lifetime, 30-second read/write inactivity, and a
64MiB aggregate bidirectional traffic budget (not a media-size guarantee). Rejects
HTTP forwarding, unsafe CONNECT authority, body/transfer headers, duplicate auth,
folding, oversized headers and missing/wrong credentials. Cancellation/disposal
closes only owned sockets and observes client tasks. Limits are per future job.

Fourteen actual loopback cases cover rejection, rebinding/new redirect targets,
traffic/concurrency limits, cancellation, encrypted TLS payload and wrong-hostname
certificate rejection using a private fixture trust root. No TLS verification is
disabled. The trust root and loopback destination injection exist only in tests.
The actual yt-dlp process has not yet been wired through this adapter: native
downloader flags, executable leases, bounded process/output supervision, supported
source protocols and actual executable/fixture integration are the next task.
Downloading remains disabled, with no new external endpoint or UI promise.
