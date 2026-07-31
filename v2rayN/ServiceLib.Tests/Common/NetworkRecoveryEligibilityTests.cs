using AwesomeAssertions;
using Xunit;

namespace ServiceLib.Tests.Common;

public class NetworkRecoveryEligibilityTests
{
    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(1, 0, false)]
    [InlineData(0, 1, false)]
    [InlineData(1, 1, true)]
    [InlineData(3, 20, true)]
    public void CanRun_RequiresSubscriptionsAndProfiles(
        int subscriptionCount,
        int profileCount,
        bool expected)
    {
        NetworkRecoveryEligibility
            .CanRun(subscriptionCount, profileCount)
            .Should()
            .Be(expected);
    }
}
