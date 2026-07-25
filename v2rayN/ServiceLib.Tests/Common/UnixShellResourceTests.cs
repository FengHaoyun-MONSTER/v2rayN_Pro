using AwesomeAssertions;
using ServiceLib.Common;
using Xunit;

namespace ServiceLib.Tests.Common;

public class UnixShellResourceTests
{
    [Theory]
    [InlineData(Global.ProxySetOSXShellFileName)]
    [InlineData(Global.ProxySetLinuxShellFileName)]
    [InlineData(Global.KillAsSudoOSXShellFileName)]
    [InlineData(Global.KillAsSudoLinuxShellFileName)]
    public void EmbeddedShellResource_ShouldContainOnlyLfLineEndings(string resourceName)
    {
        var contents = EmbedUtils.GetEmbedText(resourceName);

        contents.Should().StartWith("#!/bin/bash\n");
        contents.Should().NotContain("\r");
    }

    [Fact]
    public void MacOsSystemProxyScript_ShouldUseAbsoluteNetworkSetupAndReportState()
    {
        var contents = EmbedUtils.GetEmbedText(Global.ProxySetOSXShellFileName);

        contents.Should().Contain("/usr/sbin/networksetup");
        contents.Should().Contain("-getwebproxy");
        contents.Should().Contain("-getsecurewebproxy");
        contents.Should().Contain("-getsocksfirewallproxy");
    }
}