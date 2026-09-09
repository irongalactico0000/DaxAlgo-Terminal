using TradingTerminal.App.Plugins;
using Xunit;

namespace TradingTerminal.App.Avalonia.Tests;

public sealed class OpenPackageRemoteInstallerTests
{
    [Fact]
    public async Task Download_rejects_non_http_urls_and_non_local_http()
    {
        using var http = new HttpClient();

        var missing = await OpenPackageRemoteInstaller.DownloadAsync(http, "");
        Assert.False(missing.Success);

        var ftp = await OpenPackageRemoteInstaller.DownloadAsync(http, "ftp://example.com/x.daxalgostrategy");
        Assert.False(ftp.Success);

        var plainRemote = await OpenPackageRemoteInstaller.DownloadAsync(
            http, "http://example.com/pack.daxalgostrategy");
        Assert.False(plainRemote.Success);
        Assert.Contains("HTTPS", plainRemote.Message, StringComparison.OrdinalIgnoreCase);
    }
}
