<div align="center">

<img src="branding/castlumora-wordmark.svg" alt="Castlumora" width="640">

# Castlumora

**Your media. Your server. Your experience.**

**Version 1.0 · Free and open-source · Self-hosted streaming**

[Source code](https://github.com/dangerousnick/Castlumora) · [Issues](https://github.com/dangerousnick/Castlumora/issues) · [Pull requests](https://github.com/dangerousnick/Castlumora/pulls)

</div>

---

## What is Castlumora?

**Castlumora** is an independent, open-source media streaming platform for people who want to organize and enjoy their own libraries, live TV and other hosted content—on their own terms.

We're building a streaming experience that combines a simple, attractive interface with powerful controls for the people running the server.

## The vision

- **Server-controlled interface:** Configure navigation, featured content, categories and layouts from one place, with device-aware presentation.
- **Organized browsing:** Put movies and shows in multiple categories without duplicating the media files.
- **Easy discovery:** Search, watchlists, continue watching, recommendations and a “Surprise Me” option.
- **Profiles and permissions:** Family-friendly controls, invitations, and flexible access rules.
- **Creator and organization tools:** Build toward hosted live streams, uploads and configurable service offerings.
- **Open source for everyone:** No premium software feature tiers.

**Development note:** These describe the product direction, not a claim that every feature has been implemented. Castlumora 1.0 is the project's initial version identity; a public downloadable 1.0 release has not been verified or announced.

## Repository status

This repository contains **Castlumora's server/backend**. Web, television and mobile applications are separate development efforts. Some underlying source project names and protocols still use upstream identifiers for compatibility.

### Build from source

The imported server currently uses the .NET 10 SDK. The compatible web client and FFmpeg build are separate dependencies. These instructions are for developers and do not represent a packaged Castlumora installer.

```bash
git clone https://github.com/dangerousnick/Castlumora.git
cd Castlumora
dotnet build Jellyfin.sln
```

For the original server rebranding decisions and verification notes, see [Branding notes](docs/branding.md) and [Phase 1 verification](docs/verification-phase-1.md).

## License and acknowledgments

Castlumora is based on the open-source **Jellyfin** project, with earlier roots in **Emby**. Castlumora is independently developed and is not affiliated with or endorsed by the Jellyfin Project.

The original [GNU GPL license](LICENSE), [contributors](CONTRIBUTORS.md), copyright notices, and relevant third-party attributions are preserved. Upstream identifiers or development dependencies in the code are retained where needed for compatibility, not as Castlumora branding.

---

<div align="center"><strong>Castlumora · Version 1.0</strong></div>

## Castlumora Web — Phase 1.3 development

An independent React/TypeScript streaming frontend lives in [Castlumora.Web](Castlumora.Web/README.md). It uses the existing server APIs and adds server-owned presentation drafts/publishing, categories and separate watchlist/resume preferences. The inherited Web client remains available for advanced administration and media features not yet covered. This work does not deploy or modify a running server.

- [Development progress and exact next steps](DEVELOPMENT_PROGRESS.md)
- [Permanent product requirements](docs/CASTLUMORA_PRODUCT_REQUIREMENTS.md)
- [Feature parity and limitations](FEATURE_PARITY.md)
- [Product roadmap](PRODUCT_ROADMAP.md)
- [Presentation schema and persistence](docs/castlumora-presentation-v1.md)
- [API adapter](docs/castlumora-api-adapter.md)

Castlumora remains free and GPL licensed; upstream Jellyfin source history, copyright, contributors and existing compatibility contracts are preserved.
