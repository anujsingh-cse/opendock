namespace OpenDock.Core.Search;

/// <summary>A candidate paired with its match score (higher is better).</summary>
public sealed record RankedResult<T>(T Item, double Score);

/// <summary>
/// Pure fuzzy ranking for Spotlight-style search. No UI, no platform calls,
/// fully unit-testable: given a query and candidates it returns matches
/// ordered by relevance.
/// </summary>
public static class SpotlightRanker
{
    /// <param name="nameSelector">The primary display name (weighted highest).</param>
    /// <param name="haystackSelector">
    /// Optional extra searchable text (path, keywords). Defaults to the name.
    /// </param>
    public static List<RankedResult<T>> Rank<T>(
        string query,
        IEnumerable<T> items,
        Func<T, string> nameSelector,
        Func<T, string>? haystackSelector = null)
    {
        var results = new List<RankedResult<T>>();
        string q = query.Trim().ToLowerInvariant();
        if (q.Length == 0)
            return results;

        foreach (var item in items)
        {
            string name = (nameSelector(item) ?? string.Empty).ToLowerInvariant();
            string hay = haystackSelector is null
                ? name
                : ((haystackSelector(item) ?? string.Empty) + " " + name).ToLowerInvariant();

            double score = Score(q, name, hay);
            if (score > 0)
                results.Add(new RankedResult<T>(item, score));
        }

        results.Sort((a, b) => b.Score.CompareTo(a.Score));
        return results;
    }

    private static double Score(string q, string name, string hay)
    {
        if (name.Length == 0)
            return 0;

        // Exact match beats everything.
        if (name == q)
            return 1000;

        // Prefix of the name: the classic "as-you-type" case.
        if (name.StartsWith(q, StringComparison.Ordinal))
            return 500 - Math.Min(name.Length * 0.5, 200);

        // Query matches the start of a word ("vis" → "Visual Studio").
        int wordStart = IndexOfWordStart(hay, q);
        if (wordStart >= 0)
            return 300 - Math.Min(wordStart, 100);

        // Plain substring anywhere.
        int sub = hay.IndexOf(q, StringComparison.Ordinal);
        if (sub >= 0)
            return 150 - Math.Min(sub, 100);

        // Fuzzy subsequence ("vsc" → "Visual Studio Code").
        return FuzzyScore(q, name);
    }

    private static int IndexOfWordStart(string hay, string q)
    {
        int index = hay.IndexOf(q, StringComparison.Ordinal);
        while (index >= 0)
        {
            if (index == 0 || !char.IsLetterOrDigit(hay[index - 1]))
                return index;
            index = hay.IndexOf(q, index + 1, StringComparison.Ordinal);
        }
        return -1;
    }

    /// <summary>
    /// Subsequence match: every query character appears in the name in order.
    /// Consecutive runs score higher; gaps cost. Returns 0 on any miss.
    /// </summary>
    private static double FuzzyScore(string q, string name)
    {
        int namePos = 0;
        int matched = 0;
        int consecutive = 0;
        int maxConsecutive = 0;
        int firstMatch = -1;

        foreach (char c in q)
        {
            int found = name.IndexOf(c, namePos);
            if (found < 0)
                return 0;
            if (firstMatch < 0)
                firstMatch = found;
            consecutive = found == namePos ? consecutive + 1 : 1;
            maxConsecutive = Math.Max(maxConsecutive, consecutive);
            matched++;
            namePos = found + 1;
        }

        // Coverage of the name matters: "note" should beat a long
        // "nonsense-other-thing-entirely" that happens to contain n-o-t-e.
        double coverage = (double)matched / name.Length;
        return 10 + matched * 4 + maxConsecutive * 6 + coverage * 40 - firstMatch * 0.5;
    }
}
