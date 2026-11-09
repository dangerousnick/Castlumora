# Castlumora

![Castlumora crescent castle logo](branding/castlumora-wordmark.svg)

Castlumora is a free software media server based on Jellyfin, descended from Emby 3.5.2. This repository contains the server; the browser client is maintained separately in Jellyfin Web.

Phase 1 establishes the Castlumora server identity on `release-12.z`. See [the rebrand inventory](docs/branding.md) for compatibility decisions, visual asset status, verification and the separate Web work.

## License and attribution

Castlumora preserves the upstream GNU GPL license: see [LICENSE](LICENSE) and [CONTRIBUTORS.md](CONTRIBUTORS.md). Jellyfin and Emby copyright notices and third-party notices remain in their original files. Upstream documentation and tools linked below belong to their respective projects; they are not Castlumora releases or support channels.

## Development

Use .NET 10 and the compatible [Jellyfin FFmpeg](https://github.com/jellyfin/jellyfin-ffmpeg). The executable, project names, configuration paths and API contracts retain their upstream identifiers for compatibility.

```bash
git clone --branch release-12.z https://github.com/dangerousnick/Castlumora.git
cd Castlumora
dotnet build Jellyfin.sln
dotnet test Jellyfin.sln --no-build
```

Use an isolated data/config/cache directory for development. Do not point a development build at an existing production installation. No deployment is part of this phase.

## Upstream development guidance (adapted)

## Server Development

These instructions will help you get set up with a local development environment in order to contribute to this repository. Before you start, please be sure to completely read our [guidelines on development contributions](https://jellyfin.org/docs/general/contributing/development.html). Note that this project is supported on all major operating systems except FreeBSD, which is still incompatible.

### Prerequisites

Before the project can be built, you must first install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet) on your system.

Instructions to run this project from the command line are included here, but you will also need to install an IDE if you want to debug the server while it is running. Any IDE that supports .NET 6 development will work, but two options are recent versions of [Visual Studio](https://visualstudio.microsoft.com/downloads/) (at least 2022) and [Visual Studio Code](https://code.visualstudio.com/Download).

[ffmpeg](https://github.com/jellyfin/jellyfin-ffmpeg) will also need to be installed.

### Cloning the Repository

After dependencies have been installed you will need to clone a local copy of this repository. If you just want to run the server from source you can clone this repository directly, but if you are intending to contribute code changes to the project, you should [set up your own fork](https://jellyfin.org/docs/general/contributing/development.html#set-up-your-copy-of-the-repo) of the repository. The following example shows how you can clone the repository directly over HTTPS.

```bash
git clone --branch release-12.z https://github.com/dangerousnick/Castlumora.git
```

### Installing the Web Client

The server is configured to host the static files required for the [web client](https://github.com/jellyfin/jellyfin-web) in addition to serving the backend by default. Before you can run the server, you will need to get a copy of the web client since they are not included in this repository directly.

Note that it is recommended for development to [host the web client separately](#hosting-the-web-client-separately) from the web server with some additional configuration, in which case you can skip this step.

There are two options to get the files for the web client.

1. Build them from source following the instructions on the [jellyfin-web repository](https://github.com/jellyfin/jellyfin-web)
2. Get the pre-built files from an existing installation of the server. For example, with a Windows server installation the client files are located at `C:\Program Files\Jellyfin\Server\jellyfin-web`

### Running The Server

The following instructions will help you get the project up and running via the command line, or your preferred IDE.

#### Running With Visual Studio

To run the project with Visual Studio you can open the Solution (`.sln`) file and then press `F5` to run the server.

#### Running With Visual Studio Code

To run the project with Visual Studio Code you will first need to open the repository directory with Visual Studio Code using the `Open Folder...` option.

Second, you need to [install the recommended extensions for the workspace](https://code.visualstudio.com/docs/editor/extension-gallery#_recommended-extensions). Note that extension recommendations are classified as either "Workspace Recommendations" or "Other Recommendations", but only the "Workspace Recommendations" are required.

After the required extensions are installed, you can run the server by pressing `F5`.

#### Running From the Command Line

To run the server from the command line you can use the `dotnet run` command. The example below shows how to do this if you have cloned the repository into a directory named `Castlumora` (the default directory name) and should work on all operating systems.

```bash
cd Castlumora                          # Move into the repository directory
dotnet run --project Jellyfin.Server --webdir /absolute/path/to/jellyfin-web/dist # Run the server startup project
```

A second option is to build the project and then run the resulting executable file directly. When running the executable directly you can easily add command line options. Add the `--help` flag to list details on all the supported command line options.

1. Build the project

```bash
dotnet build                       # Build the project
cd Jellyfin.Server/bin/Debug/net10.0 # Change into the build output directory
```

2. Execute the build output. On Linux, Mac, etc. use `./jellyfin` and on Windows use `jellyfin.exe`.

#### Accessing the Hosted Web Client

If the Server is configured to host the Web Client, and the Server is running, the Web Client can be accessed at `http://localhost:8096` by default.

API documentation can be viewed at `http://localhost:8096/api-docs/swagger/index.html`


### Running from GitHub Codespaces

As Castlumora will run on a container on a GitHub hosted server, JF needs to handle some things differently.

**NOTE:** Depending on the selected configuration (if you just click 'create codespace' it will create a default configuration one) it might take 20-30 seconds to load all extensions and prepare the environment while VS Code is already open. Just give it some time and wait until you see `Downloading .NET version(s) 7.0.15~x64 ...... Done!` in the output tab.

**NOTE:** If you want to access the JF instance from outside, like with a WebClient on another PC, remember to set the "ports" in the lower VS Code window to public.

**NOTE:** When first opening the server instance with any WebUI, you will be sent to the login instead of the setup page. Refresh the login page once and you should be redirected to the Setup.

There are two configurations for you to choose from.
#### Default - Development Castlumora Server
This creates a container that has everything to run and debug the Castlumora media server but does not setup anything else. Each time you create a new container you have to run through the whole setup again. There is also no ffmpeg, webclient or media preloaded. Use the `.NET Launch (nowebclient)` launch config to start the server.

> Keep in mind that as this has no web client you have to connect to it via an external client. This can be just another codespace container running the WebUI. vuejs does not work from the get-go as it does not support the setup steps.

#### Development Castlumora Server ffmpeg
this extends the default server with a default installation of ffmpeg6 though the means described here: https://jellyfin.org/docs/general/installation/linux#repository-manual
If you want to install a specific ffmpeg version, follow the comments embedded in the `.devcontainer/Dev - Server Ffmpeg/install.ffmpeg.sh` file.

Use the `ghcs .NET Launch (nowebclient, ffmpeg)` launch config to run with the jellyfin-ffmpeg enabled.


### Running The Tests

This repository also includes unit tests that are used to validate functionality as part of a CI pipeline on Azure. There are several ways to run these tests.

1. Run tests from the command line using `dotnet test`
2. Run tests in Visual Studio using the [Test Explorer](https://docs.microsoft.com/en-us/visualstudio/test/run-unit-tests-with-test-explorer)
3. Run individual tests in Visual Studio Code using the associated [CodeLens annotation](https://github.com/OmniSharp/omnisharp-vscode/wiki/How-to-run-and-debug-unit-tests)

### Advanced Configuration

The following sections describe some more advanced scenarios for running the server from source that build upon the standard instructions above.

#### Hosting The Web Client Separately

It is not necessary to host the frontend web client as part of the backend server. Hosting these two components separately may be useful for frontend developers who would prefer to host the client in a separate webpack development server for a tighter development loop. See the [jellyfin-web](https://github.com/jellyfin/jellyfin-web#getting-started) repo for instructions on how to do this.

To instruct the server not to host the web content, there is a `nowebclient` configuration flag that must be set. This can be specified using the command line
switch `--nowebclient` or the environment variable `JELLYFIN_NOWEBCONTENT=true`.

Since this is a common scenario, there is also a separate launch profile defined for Visual Studio called `Jellyfin.Server (nowebcontent)` that can be selected from the 'Start Debugging' dropdown in the main toolbar.

**NOTE:** The setup wizard cannot be run if the web client is hosted separately.

---
<p align="center">
This project is supported by:
<br/>
<br/>
<a href="https://www.jetbrains.com"><img src="https://gist.githubusercontent.com/anthonylavado/e8b2403deee9581e0b4cb8cd675af7db/raw/199ae22980ef5da64882ec2de3e8e5c03fe535b8/jetbrains.svg" height="50px" alt="JetBrains logo"></a>
</p>
