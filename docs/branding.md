# Phase 1 rebrand inventory

Base: `release-12.z`. Development branch: `development/castlumora-phase-1`.

## Server changes

- Entry assembly display title and product identify Castlumora Server, and its company identifies Castlumora Project. `ApplicationHost.ApplicationProductName` reads that entry assembly's product metadata, so system information reports the new product.
- Startup version banner, IIS warning and CLI option descriptions use Castlumora.
- Self-contained startup/error/recovery page uses Castlumora text and the crescent/castle SVG, including when normal static hosting is unavailable.
- Swagger and ReDoc document titles and OpenAPI title use Castlumora; Swagger's custom logo and the Windows executable icon use the new assets.
- Localized loading/update messages replace the product token without changing translation keys or placeholders. Translations otherwise remain upstream text.
- README identifies the fork, license, attribution and development workflow. Upstream development documentation links remain explicitly identified.

## Compatibility boundaries

Keep namespaces, project/assembly names (`jellyfin` executable included), NuGet packages, resource identifiers, plugin contracts, authentication schemes/claims, HTTP routes, `x-jellyfin-version`, discovery protocol, database schemas and migration history unchanged. Keep existing JELLYFIN environment variables, command-line flags, default paths and user-configured server names. Upstream library company metadata, all copyright notices, LICENSE, CONTRIBUTORS and third-party notices retain their upstream attribution.

Upstream update/plugin/FFmpeg endpoints remain upstream services; a Castlumora release feed and package distribution require a separate design. Existing deployment/container templates remain upstream recipes and must not be presented as Castlumora packages. This phase does not publish a package, run a migration against existing data or deploy a service.

## Separate Jellyfin Web work

This server repository does not contain the main browser client. In a compatible Web fork, update:

- HTML document titles, application name, welcome/login/setup/dashboard/about strings and translated product labels.
- Header/wordmark, splash/loading/error illustrations, favicon, touch icons and installable PWA icons.
- Web manifest name/short name/theme colors and platform tile metadata, service-worker cache revision and cached branding.
- Client-facing links, support/about attribution and any bundled promotional images.

Copy approved assets from `branding/`, build the compatible Web distribution and test it with this server (root path and a nonempty base URL). Test login, setup, playback, reconnect and installed PWA updates. Keep protocol/client IDs and authentication intact. Native/mobile/TV clients are separate projects as well.

## Visual assets

Assets adapt the supplied `3151.jpg` reference: violet crescent, three castle towers, flags and a central play symbol. The scalable mark simplifies the textured cinematic artwork for small icons; it is not a pixel-for-pixel trace. See `branding/README.md` for dimensions and integration points.

## Verification

See `docs/verification-phase-1.md` for actual commands and results. Development verification uses only this disposable workspace. The running Coon Media server is outside this work and must not be targeted.
