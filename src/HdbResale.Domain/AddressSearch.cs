namespace HdbResale.Domain;

// One query word, with the HDB abbreviations it may stand for ("avenue" or "aven" for AVE).
public sealed record SearchTerm(string Text, IReadOnlyList<string> Abbreviations);

// Search over the address summaries the API already published: every query word must begin a word of the block,
// street, town or postal code, or spell out a street term that HDB abbreviates. It runs locally and synchronously;
// nothing is requested while the user types. Addresses matching more query words exactly rank first.
public static class AddressSearch
{
    // HDB writes these street terms abbreviated; people often type them in full.
    private static readonly IReadOnlyDictionary<string, string> Abbreviations = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["avenue"] = "ave", ["street"] = "st", ["road"] = "rd", ["drive"] = "dr", ["crescent"] = "cres",
        ["close"] = "cl", ["place"] = "pl", ["lorong"] = "lor", ["jalan"] = "jln", ["bukit"] = "bt",
        ["kampong"] = "kg", ["tanjong"] = "tg", ["north"] = "nth", ["south"] = "sth", ["central"] = "ctrl",
        ["gardens"] = "gdns", ["heights"] = "hts", ["terrace"] = "ter", ["park"] = "pk", ["square"] = "sq",
        ["upper"] = "upp", ["commonwealth"] = "cwealth", ["market"] = "mkt", ["industrial"] = "ind",
    };

    // Lower case, apostrophes dropped (C'WEALTH, MA'MOR) and any other punctuation as a word break (KALLANG/WHAMPOA).
    public static string[] Words(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        var buffer = new System.Text.StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (c is '\'' or '’') continue;
            buffer.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
        }
        return buffer.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    // The words an address can be found by.
    public static string[] Index(AddressSummary address)
    {
        ArgumentNullException.ThrowIfNull(address);
        return [.. Words(address.Block), .. Words(address.StreetName), .. Words(address.Town), .. Words(address.PostalCode),
            .. Words(address.DisplayName)];
    }

    // A partly typed long form ("aven") already stands for its abbreviation, so results do not vanish mid-word.
    public static IReadOnlyList<SearchTerm> Parse(string? query) => Words(query).Distinct().Select(word => new SearchTerm(word,
        word.Length < 3 ? [] : Abbreviations.Where(pair => pair.Key.StartsWith(word, StringComparison.Ordinal))
            .Select(pair => pair.Value).Distinct().ToArray())).ToArray();

    // Null when a term matches nothing; otherwise how many terms matched a whole word exactly.
    public static int? Score(IReadOnlyList<string> words, IReadOnlyList<SearchTerm> terms)
    {
        ArgumentNullException.ThrowIfNull(words);
        ArgumentNullException.ThrowIfNull(terms);
        var exact = 0;
        foreach (var term in terms)
        {
            var whole = false;
            var found = false;
            foreach (var word in words)
            {
                if (word == term.Text || term.Abbreviations.Contains(word)) { whole = found = true; break; }
                if (word.StartsWith(term.Text, StringComparison.Ordinal)) found = true;
            }
            if (!found) return null;
            if (whole) exact++;
        }
        return exact;
    }
}
