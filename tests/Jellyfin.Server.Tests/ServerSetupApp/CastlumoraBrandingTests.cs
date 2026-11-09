using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Jellyfin.Server.ServerSetupApp;
using Xunit;

namespace Jellyfin.Server.Tests.ServerSetupApp;

public class CastlumoraBrandingTests
{
    [Fact]
    public void ServerMetadata_PreservesAssemblyIdentityAndUpstreamAttribution()
    {
        var assembly = typeof(StartupUiRenderer).Assembly;
        Assert.Equal("jellyfin", assembly.GetName().Name);
        Assert.Equal("Castlumora Server", assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product);
        Assert.Equal("Castlumora Project", assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company);
        Assert.Contains("Jellyfin Contributors", assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false, "Castlumora is still starting.")]
    [InlineData(true, "Castlumora has encountered an error")]
    public async Task StartupPage_RendersBrandingAndRecoveryState(bool reportingMode, string expectedMessage)
    {
        var renderer = await StartupUiRenderer.CreateAsync(
            Path.Combine(AppContext.BaseDirectory, "ServerSetupApp", "index.mstemplate.html"));
        using var output = new MemoryStream();
        var model = new Dictionary<string, object>
        {
            ["isInReportingMode"] = reportingMode,
            ["currentActivity"] = "Scanning media",
            ["retryValue"] = TimeSpan.FromSeconds(5),
            ["version"] = new Version(12, 0, 0),
            ["logs"] = Array.Empty<object>(),
            ["networkManagerReady"] = false,
            ["localNetworkRequest"] = false
        };
        await renderer.RenderAsync(model, output);

        var html = Encoding.UTF8.GetString(output.ToArray());
        Assert.Contains(expectedMessage, html, StringComparison.Ordinal);
        Assert.Contains("castle-mark", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Jellyfin", html, StringComparison.Ordinal);
    }
}
