# Import failures — fixed codes and private diagnostics

This increment is under verification; see DEVELOPMENT_PROGRESS.md for the exact
published commit and evidence. It extends the existing single-video importer;
it does not alter authorization, DNS/address validation, certificate checks,
executable pins, process lifetime or download/resource limits.

## Operator workflow

Administration → Imports & Sources shows a fixed explanation beside each failed
job and retains its job ID/attempt count. Correct the cause before manually
retrying. Optional bounded transient retries are described in
[the retry policy](CASTLUMORA_IMPORT_RETRIES.md); they share the same three-attempt
budget and persisted rights/destinations.

| Code | Meaning and action |
| --- | --- |
| certificateRejected | Source TLS verification failed; fix trust/source configuration, never disable validation. |
| sourceDenied | Source rejected access; verify permission, availability and source authorization. |
| sourceNetwork | Validated source transport failed; review DNS/firewall/direct HTTPS egress. Proxy-only networks remain unsupported. |
| sourceRateLimited | Source rate limited requests; wait before retrying. |
| unsupportedSource | Use this preview's supported direct HTTPS MP4 source, not playlist/channel/YouTube pages. |
| sizeLimit | Media or metadata exceeded a resource limit; choose a supported smaller source. |
| incompleteOutput | A complete media/metadata pair was not produced; review format and size. |
| unexpectedOutput | Unexpected private staging files were rejected; retain job ID and inspect diagnostics without relaxing path checks. |
| downloadFailed | Download failed without a more precise safe classification; review source availability/format and job diagnostics. |
| timeLimit | Execution deadline reached; review source and server connection. |
| permissionDenied | Requesting administrator or destination permission changed; restore authorized access before retrying. |
| interrupted | Previous execution was interrupted; review recovery state and attempt budget. |
| executionFailed | Unclassified failure; report job ID and safe server logs. |

Codes persist in the existing private version1 journal and survive restart. No
new database migration or ordinary-user configuration is introduced. Unknown or
legacy untrusted error strings are clamped to the generic public code; known old
executionFailed/interrupted values remain compatible.

## Privacy and diagnosis

The process classifies only bounded captured stderr into a small fixed vocabulary;
raw output is neither returned nor logged. Known application-generated exception
messages map to fixed codes; other exception text is discarded. Logs contain job
ID, attempt, code and exception type, without source URL/query credentials, proxy
nonce, filesystem paths or stdout/stderr. API responses expose only known codes;
the UI renders fixed guidance rather than arbitrary error strings.

The per-job authenticated tunnel records one internal boolean only when validated
numeric connection establishment fails. It still closes failed connections and
returns no DNS/error details. It does not change policy or mark rejected malformed/
unauthenticated requests as a network failure. A failed download can use that flag
when stderr has no precise safe cause; specific certificate/access/size errors take
precedence. Successful downloads remain successful despite any earlier retry.

## Verification boundary

New tests exercise classification/redaction, unknown history-code clamping, real
journal persistence/reload and safe UI guidance. Existing real TLS certificate/
access-denial tests retain their cleanup, secret-redaction and lease assertions.
A real loopback tunnel with a refused validated endpoint must close without
reflecting details and set only the flag; unsafe/authentication requests retain
zero connection calls and an unset flag. Actual compiled-server/API/Chromium verification also persisted sourceNetwork at
the final allowed retry, displayed fixed guidance/job ID without private diagnostics,
and kept Retry unavailable after exhaustion. Final full gate and required native
CI must still pass before claiming this increment complete.

Windows compatibility staging-cleanup failures remain advisory/deferred and logged;
no assertion, required Linux/macOS test or production safeguard is removed here.
The full importer/online metadata and remaining1.0 requirements are unchanged.
