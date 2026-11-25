using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Api.Castlumora;
using Jellyfin.Server.Integration.Tests.Castlumora;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Jellyfin.Server.Integration.Tests.Controllers;

public sealed class CastlumoraImportLifecycleTests
{
    [Fact]
    public async Task ConfirmedYouTubeJob_UsesRealQueuePublicationAndLibraryPlayback_WithOwnedTransportFixture()
    {
        // This verifies the API/queue/library contract with real owned media, not public YouTube extraction.
        await using var source = await ImportHttpFixture.CreateAsync();
        await using var factory = source.Host();
        using var admin = factory.CreateClient();
        admin.Timeout = TimeSpan.FromSeconds(30);
        var token = TestContext.Current.CancellationToken;
        admin.DefaultRequestHeaders.AddAuthHeader(await AuthHelper.CompleteStartupAsync(admin));
        using var library = await admin.PostAsJsonAsync("Library/VirtualFolders?name=CreatorImports&collectionType=homevideos&paths=" + Uri.EscapeDataString(source.Library), new { }, token);
        library.EnsureSuccessStatusCode();
        using var install = await admin.PostAsync("Castlumora/Imports/Runtime/Install", null, token);
        install.EnsureSuccessStatusCode();
        var state = await admin.GetFromJsonAsync<JsonObject>("Castlumora/Imports/Queue", token);
        var destination = Guid.Parse(Assert.Single(state!["destinations"]!.AsArray())!["id"]!.GetValue<string>());
        var me = await admin.GetFromJsonAsync<JsonObject>("Users/Me", token);
        var administrator = Guid.Parse(me!["Id"]!.GetValue<string>());
        var metadata = new YouTubeMetadataPreview.Metadata("4Xsn6oe-AEg", "Unspeakable creator fixture", "Owned fixture description", "UCabcdefghijklmnopqrstuv", "Fixture creator", 1, [new("18", 64)]);
        var options = YouTubeImportOptions.Select(metadata, 720, 128);
        var paths = factory.Services.GetRequiredService<IApplicationPaths>();
        var receipt = new YouTubePreviewStore(paths.DataPath).Create(administrator, destination, "https://www.youtube.com/watch?v=4Xsn6oe-AEg", metadata.Title, options);
        var body = new { previewId = receipt.Id, destinationId = destination, title = metadata.Title, rights = "Generated owned media; source identity is a test fixture", rightsConfirmed = true, automaticRetries = false };
        using var submitted = await admin.PostAsJsonAsync("Castlumora/Imports/Jobs/YouTube", body, token);
        submitted.EnsureSuccessStatusCode();
        var id = (await submitted.Content.ReadFromJsonAsync<JsonObject>(token))!["id"]!.GetValue<string>();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(90));
        string? itemId = null;
        while (itemId is null)
        {
            var queue = await admin.GetFromJsonAsync<JsonObject>("Castlumora/Imports/Queue", deadline.Token);
            var job = Assert.Single(queue!["jobs"]!.AsArray())!;
            Assert.NotEqual("failed", job["state"]!.GetValue<string>());
            Assert.Equal("youtube", job["sourceType"]!.GetValue<string>());
            Assert.Equal(options.VideoId, job["videoId"]!.GetValue<string>());
            var items = await admin.GetFromJsonAsync<JsonObject>("Items?Recursive=true&IncludeItemTypes=Video&SearchTerm=Unspeakable", deadline.Token);
            itemId = items!["Items"]!.AsArray().FirstOrDefault()?["Id"]?.GetValue<string>();
            if (itemId is null)
            {
                await Task.Delay(200, deadline.Token);
            }
        }

        using var playback = await admin.GetAsync("Items/" + itemId + "/Download", token);
        playback.EnsureSuccessStatusCode();
        Assert.Equal(source.Payload, await playback.Content.ReadAsByteArrayAsync(token));
        using var duplicate = await admin.PostAsJsonAsync("Castlumora/Imports/Jobs/YouTube", body, token);
        duplicate.EnsureSuccessStatusCode();
        Assert.Equal(id, (await duplicate.Content.ReadFromJsonAsync<JsonObject>(token))!["id"]!.GetValue<string>());
        var nfo = System.Xml.Linq.XDocument.Load(Assert.Single(Directory.GetFiles(source.Library, "*.nfo", SearchOption.AllDirectories)));
        Assert.Equal(options.VideoId, nfo.Root!.Elements("uniqueid").Single(element => element.Attribute("type")!.Value == "YouTube").Value);
        Assert.DoesNotContain("signed-secret", nfo.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthorizedHttpsImport_PersistsIndexesAndServesRealVideo_WithoutSourceQueryDisclosure()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Hosted importer preview targets Linux/macOS; Windows compatibility remains advisory.");
            return;
        }

        await using var source = await ImportHttpFixture.CreateAsync();
        await using var factory = source.Host();
        using var admin = factory.CreateClient();
        admin.Timeout = TimeSpan.FromSeconds(15);
        var accessToken = await AuthHelper.CompleteStartupAsync(admin);
        admin.DefaultRequestHeaders.AddAuthHeader(accessToken);
        var token = TestContext.Current.CancellationToken;
        using var library = await admin.PostAsJsonAsync("Library/VirtualFolders?name=OwnedImports&collectionType=homevideos&paths=" + Uri.EscapeDataString(source.Library) + "&refreshLibrary=true", new { }, token);
        library.EnsureSuccessStatusCode();
        using var install = await admin.PostAsync("Castlumora/Imports/Runtime/Install", null, token);
        install.EnsureSuccessStatusCode();
        var state = await admin.GetFromJsonAsync<JsonObject>("Castlumora/Imports/Queue", token);
        Assert.True(state!["available"]!.GetValue<bool>());
        var destination = Assert.Single(state["destinations"]!.AsArray())!["id"]!.GetValue<string>();
        var body = new { url = source.Source, destinationId = destination, title = "Owned imported clip", rights = "Generated Castlumora test clip", rightsConfirmed = true, automaticRetries = true };
        using var submit = await admin.PostAsJsonAsync("Castlumora/Imports/Jobs", body, token);
        submit.EnsureSuccessStatusCode();
        var submitted = await submit.Content.ReadFromJsonAsync<JsonObject>(token);
        var id = submitted!["id"]!.GetValue<string>();
        Assert.True(submitted["automaticRetries"]!.GetValue<bool>());
        Assert.Null(submitted["nextAttemptAt"]);
        Assert.DoesNotContain("signed-secret", submitted.ToJsonString(), StringComparison.Ordinal);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(90));
        while (true)
        {
            state = await admin.GetFromJsonAsync<JsonObject>("Castlumora/Imports/Queue", deadline.Token);
            var job = state!["jobs"]!.AsArray().Single(job => job!["id"]!.GetValue<string>() == id)!;
            Assert.DoesNotContain("signed-secret", state.ToJsonString(), StringComparison.Ordinal);
            Assert.NotEqual("failed", job["state"]!.GetValue<string>());
            if (job["state"]!.GetValue<string>() == "completed")
            {
                break;
            }

            await Task.Delay(100, deadline.Token);
        }

        using var duplicate = await admin.PostAsJsonAsync("Castlumora/Imports/Jobs", body, token);
        duplicate.EnsureSuccessStatusCode();
        Assert.Equal(id, (await duplicate.Content.ReadFromJsonAsync<JsonObject>(token))!["id"]!.GetValue<string>());
        Assert.Single(Directory.GetFiles(source.Library, "*.mp4", SearchOption.AllDirectories));
        using var invalidBatch = await admin.PostAsJsonAsync(
            "Castlumora/Imports/Jobs/Batch",
            new
            {
                entries = new[] { new { url = source.Source + "&variant=two", title = "Owned second source" }, new { url = "https://127.0.0.1/unsafe.mp4", title = "Unsafe" } },
                destinationId = destination,
                rights = "Owned generated fixture",
                rightsConfirmed = true
            },
            token);
        Assert.Equal(HttpStatusCode.BadRequest, invalidBatch.StatusCode);
        Assert.Single((await admin.GetFromJsonAsync<JsonObject>("Castlumora/Imports/Queue", token))!["jobs"]!.AsArray());
        using var batch = await admin.PostAsJsonAsync(
            "Castlumora/Imports/Jobs/Batch",
            new
            {
                entries = new[] { new { url = source.Source, title = "Do not replace original" }, new { url = source.Source + "&variant=two", title = "Owned second source" } },
                destinationId = destination,
                rights = "Owned generated fixture",
                rightsConfirmed = true,
                automaticRetries = false
            },
            token);
        batch.EnsureSuccessStatusCode();
        var accepted = (await batch.Content.ReadFromJsonAsync<JsonObject>(token))!["jobs"]!.AsArray();
        Assert.Equal(2, accepted.Count);
        Assert.Equal(id, accepted[0]!["id"]!.GetValue<string>());
        Assert.True(accepted[0]!["automaticRetries"]!.GetValue<bool>());
        var secondId = accepted[1]!["id"]!.GetValue<string>();
        while (true)
        {
            var current = await admin.GetFromJsonAsync<JsonObject>("Castlumora/Imports/Queue", deadline.Token);
            var second = current!["jobs"]!.AsArray().Single(job => job!["id"]!.GetValue<string>() == secondId)!;
            Assert.DoesNotContain("signed-secret", current.ToJsonString(), StringComparison.Ordinal);
            Assert.NotEqual("failed", second["state"]!.GetValue<string>());
            if (second["state"]!.GetValue<string>() == "completed")
            {
                Assert.Equal(11734, second["bytes"]!.GetValue<long>());
                Assert.Equal(accepted[0]!["sha256"]!.GetValue<string>(), second["sha256"]!.GetValue<string>());
                break;
            }

            await Task.Delay(100, deadline.Token);
        }

        Assert.Single(Directory.GetFiles(source.Library, "*.mp4", SearchOption.AllDirectories));
        string? itemId = null;
        while (itemId is null)
        {
            if (deadline.IsCancellationRequested)
            {
                var indexed = await admin.GetStringAsync("Items?Recursive=true&Fields=Path", token);
                var tasks = await admin.GetStringAsync("ScheduledTasks", token);
                Assert.Fail("Automatic indexing did not complete. Items: " + indexed + "; tasks: " + tasks + "; diagnostics: " + string.Join("\n", source.Diagnostics));
            }

            var items = await admin.GetFromJsonAsync<JsonObject>("Items?Recursive=true&IncludeItemTypes=Video&SearchTerm=Owned%20imported%20clip&Fields=Path", token);
            var item = items!["Items"]!.AsArray().FirstOrDefault();
            itemId = item?["Id"]?.GetValue<string>();
            if (itemId is null)
            {
                await Task.Delay(200, token);
            }
            else
            {
                Assert.Equal("Video", item!["Type"]!.GetValue<string>());
                Assert.Equal("Owned imported clip", item["Name"]!.GetValue<string>());
            }
        }

        using var playback = await admin.GetAsync("Items/" + itemId + "/Download", token);
        playback.EnsureSuccessStatusCode();
        Assert.Equal(source.Payload, await playback.Content.ReadAsByteArrayAsync(token));
        using var unsafeSource = await admin.PostAsJsonAsync("Castlumora/Imports/Jobs", new { url = "https://127.0.0.1/private.mp4", destinationId = destination, title = "Blocked", rights = "Test", rightsConfirmed = true }, token);
        Assert.Equal(HttpStatusCode.BadRequest, unsafeSource.StatusCode);

        await factory.DisposeAsync();
        await using var restarted = source.Host();
        using var returning = restarted.CreateClient();
        returning.DefaultRequestHeaders.AddAuthHeader(accessToken);
        var recovered = await returning.GetFromJsonAsync<JsonObject>("Castlumora/Imports/Queue", token);
        Assert.Equal(2, recovered!["jobs"]!.AsArray().Count);
        var restored = recovered["jobs"]!.AsArray().Single(job => job!["id"]!.GetValue<string>() == id)!;
        Assert.Equal("completed", recovered["jobs"]!.AsArray().Single(job => job!["id"]!.GetValue<string>() == secondId)!["state"]!.GetValue<string>());
        Assert.Equal(id, restored["id"]!.GetValue<string>());
        Assert.Equal("completed", restored["state"]!.GetValue<string>());
        Assert.Equal(1, restored["attempts"]!.GetValue<int>());
        Assert.True(restored["automaticRetries"]!.GetValue<bool>());
        Assert.Null(restored["nextAttemptAt"]);
        Assert.DoesNotContain("signed-secret", recovered.ToJsonString(), StringComparison.Ordinal);
        using var resumed = await returning.GetAsync("Items/" + itemId + "/Download", token);
        resumed.EnsureSuccessStatusCode();
        Assert.Equal(source.Payload, await resumed.Content.ReadAsByteArrayAsync(token));
        using var afterRestart = await returning.PostAsJsonAsync("Castlumora/Imports/Jobs", body, token);
        afterRestart.EnsureSuccessStatusCode();
        Assert.Equal(id, (await afterRestart.Content.ReadFromJsonAsync<JsonObject>(token))!["id"]!.GetValue<string>());
        Assert.Single(Directory.GetFiles(source.Library, "*.mp4", SearchOption.AllDirectories));
    }
}
