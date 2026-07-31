namespace ServiceLib.Common;

internal static class NetworkRecoveryEligibility
{
    public static bool CanRun(int subscriptionCount, int profileCount)
    {
        return subscriptionCount > 0 && profileCount > 0;
    }
}
