namespace ServiceLib.Common;

internal static class LatencySortHelper
{
    public static IOrderedEnumerable<T> OrderAscending<T>(
        IEnumerable<T> source,
        Func<T, int> delaySelector)
    {
        return source
            .OrderBy(item => delaySelector(item) switch
            {
                > 0 => 0,
                0 => 1,
                _ => 2
            })
            .ThenBy(delaySelector);
    }

    public static bool ShouldSwitchToCandidate(
        bool currentExists,
        int currentDelay,
        int candidateDelay)
    {
        return candidateDelay > 0
            && (!currentExists || currentDelay <= 0 || candidateDelay < currentDelay);
    }
}
