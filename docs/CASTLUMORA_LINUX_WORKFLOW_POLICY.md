# Castlumora — Linux-Only Development and Workflow Correction

**Repository:** `dangerousnick/Castlumora`
**Development branch:** `development/castlumora-web-1.3`
**Existing pull request:** #3

## 1. Official platform change

Castlumora 1.0 will officially support **Linux servers only**.

Target environments:
- Linux Mint 22.3 and newer compatible versions
- Ubuntu LTS
- Debian
- Docker on Linux
- Compatible Linux-based NAS systems

**macOS and Windows server support are no longer requirements for Castlumora 1.0.**

Remove macOS and Windows from mandatory CI, release acceptance, packaging, and platform-specific development priorities.

Do not waste development time fixing platform-specific problems that cannot affect Linux.

Preserve existing cross-platform code where practical. Do not undertake unnecessary platform-removal refactoring or intentionally break Windows/macOS clients.

Linux server functionality, security, and compatibility are the priorities.

## 2. Eliminate unnecessary repetitive testing

The previous development sessions spent excessive time repeatedly running thousands of tests, rebuilding unchanged components, and troubleshooting unrelated environments.

Improve the workflow:

1. Run focused tests while developing individual features.
2. Run full backend validation before publishing substantial application changes.
3. Run frontend checks when frontend code or shared contracts change.
4. Run native importer tests when importer or related dependencies change.
5. Do not repeatedly run identical full suites against unchanged code.
6. Use appropriate documentation-only validation for documentation-only commits.
7. Avoid rebuilding unchanged packages unnecessarily.
8. Reuse trustworthy validation results for the exact unchanged source revision when appropriate.

Review the existing pre-push hook and CI configuration. Remove redundant checks only when their coverage is genuinely duplicated.

**Do not disable unique security tests, backup integrity tests, migration tests, or important regression coverage merely to make development faster.**

Never report a test as passed unless it actually passed.

## 3. Stop losing track of the original roadmap

The original Castlumora product requirements are authoritative.

Before starting work, read:

- `DEVELOPMENT_PROGRESS.md`
- `FEATURE_PARITY.md`
- `PRODUCT_ROADMAP.md`
- `docs/CASTLUMORA_PRODUCT_REQUIREMENTS.md`
- `docs/CASTLUMORA_PHASE_1_4.md`
- `docs/CASTLUMORA_IMPORT_AND_CREATOR_METADATA.md`

Identify the latest verified commit and compare completed features against the actual requirements.

Do not restart completed work.

Do not repeatedly redesign working features without a demonstrated defect.

Do not introduce unrelated architectural changes unless necessary for security, correctness, or the current milestone.

Keep the development focused on delivering the original Castlumora 1.0 vision.

## 4. Prioritize actual feature completion

The immediate next milestone is the existing importer continuation:

**Atomic multi-URL importing for up to 25 authorized direct MP4 URLs.**

Build on the completed persistent queue, retries, security checks, and library publication.

After that, proceed through the existing roadmap:

1. Complete YouTube, playlist, channel, and bulk importing.
2. Complete native creator metadata identification and management.
3. Complete Live TV, IPTV, EPG, and DVR workflows.
4. Complete Castlumora Web and server-controlled customization.
5. Complete custom categories, collections, banners, and layouts.
6. Complete built-in intro/outro skipping.
7. Complete accounts, profiles, parental controls, and personalization.
8. Complete remaining administration and supported media features.
9. Complete Linux packaging, compatibility testing, and stable 1.0 release requirements.

Preserve the remaining creator, business, resource-management, storage, and other previously agreed requirements. Do not silently remove them.

The 1.0-to-2.0 migration architecture must remain in the roadmap as future preparation. Do not begin rewriting the backend for 2.0.

## 5. Improve development efficiency

Use the following workflow:

**Inspect → Implement → Focused tests → Fix → Full validation when required → Commit → Push → Update progress → Next milestone**

Avoid repeatedly returning to completed stages.

Keep progress documentation concise and accurate.

Record:
- Completed milestone
- Exact published commit
- Verified test results
- Remaining issues
- Next unfinished task

Do not repeatedly rewrite historical progress or generate unnecessary documentation checkpoints.

Prefer a meaningful, tested feature increment over numerous tiny commits that each trigger an expensive full pipeline.

Do not compromise security, data integrity, or release quality to reduce testing time.

## 6. Update the permanent roadmap

Update the existing repository documentation to reflect:

- Linux-only server support for Castlumora 1.0
- macOS and Windows server support deferred indefinitely
- Streamlined, nonduplicative validation
- Feature-first development
- Preservation of all original product requirements
- Continued 1.0-to-2.0 migration planning

Do not rewrite the entire roadmap or remove historical development evidence.

## 7. Continue development

After applying the platform and workflow changes, return immediately to the current importer milestone.

Do not spend an entire development session merely reorganizing documentation or investigating platforms that are no longer supported.

**The objective is to finish Castlumora 1.0, not endlessly rebuild and revalidate its development infrastructure.**

Keep PR #3 open. Do not merge into `main` without owner approval.
