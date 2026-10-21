using HdbResale.Domain;
using Xunit;
namespace HdbResale.Tests;

// Address search over the recorded API addresses (tests/fixtures/worker-api), combined with the filters. Expected keys
// are spelled out from the recorded data, not recomputed with the code under test.
public sealed class AddressSearchTests
{
    private static string[] Keys(AddressExplorer explorer) => explorer.Addresses.Select(a => a.AddressKey).ToArray();
    private static AddressExplorer Everything()
    {
        var explorer = AddressExplorerTests.Recorded();
        explorer.Filter(AddressFilters.Default with { MaximumPrice = 2_000_000 });
        return explorer;
    }

    [Fact]
    public void WordsAreLowerCaseWithApostrophesDroppedAndPunctuationAsBreaks()
    {
        Assert.Equal(["58", "jln", "mamor"], AddressSearch.Words("58 JLN MA'MOR"));
        Assert.Equal(["kallang", "whampoa"], AddressSearch.Words("KALLANG/WHAMPOA"));
        Assert.Equal(["cwealth", "cres"], AddressSearch.Words("C’wealth  Cres."));
        Assert.Empty(AddressSearch.Words("  -/ "));
    }

    [Fact]
    public void SpelledOutAndPartlyTypedStreetTermsStandForHdbAbbreviations()
    {
        var terms = AddressSearch.Parse("Avenue sou st bukit");
        Assert.Equal(["ave"], terms[0].Abbreviations);
        Assert.Equal(["sth"], terms[1].Abbreviations);
        Assert.Empty(terms[2].Abbreviations);          // two letters are a prefix only
        Assert.Equal(["bt"], terms[3].Abbreviations);
    }

    [Theory]
    [InlineData("748b bedok reservoir cres", new[] { "bedok-748b-bedok-reservoir-cres" })]
    [InlineData("Bedok Reservoir", new[] { "bedok-748a-bedok-reservoir-cres", "bedok-748b-bedok-reservoir-cres", "bedok-747a-bedok-reservoir-cres" })]
    [InlineData("471747", new[] { "bedok-747a-bedok-reservoir-cres" })]
    [InlineData("4717", new[] { "bedok-748a-bedok-reservoir-cres", "bedok-747a-bedok-reservoir-cres" })]
    [InlineData("bedok south avenue 2", new[] { "bedok-10d-bedok-sth-ave-2" })]
    [InlineData("bedok sou", new[] { "bedok-39-bedok-sth-rd", "bedok-10d-bedok-sth-ave-2" })]
    [InlineData("jalan ma'mor", new[] { "kallang-whampoa-58-jln-ma-mor" })]
    [InlineData("whampoa", new[] { "kallang-whampoa-46-bendemeer-rd", "kallang-whampoa-58-jln-ma-mor" })]
    [InlineData("tampines", new string[0])]
    public void FindsAddressesByFullAddressStreetPostalCodeAndTown(string query, string[] expected)
    {
        var explorer = Everything();
        explorer.Search(query);
        Assert.Equal(expected, Keys(explorer));
    }

    [Fact]
    public void AnExactBlockRanksBeforeLongerBlocksThatStartTheSame()
    {
        var explorer = Everything();
        explorer.Search("58");
        // By median alone 588C and 588B come first; the exact block 58 outranks them.
        Assert.Equal(["kallang-whampoa-58-jln-ma-mor", "ang-mo-kio-588c-ang-mo-kio-st-52", "ang-mo-kio-588b-ang-mo-kio-st-52"],
            Keys(explorer));
    }

    [Fact]
    public void SearchNarrowsTheFilteredAddressesAndAnEmptySearchRestoresThem()
    {
        var explorer = AddressExplorerTests.Recorded();
        explorer.Filter(AddressFilters.Default with { Town = "BEDOK" });
        explorer.Search("748");
        Assert.Equal(["bedok-748a-bedok-reservoir-cres", "bedok-748b-bedok-reservoir-cres"], Keys(explorer));
        explorer.Search("  ");
        Assert.Equal(["bedok-39-bedok-sth-rd", "bedok-115-bedok-nth-rd", "bedok-748a-bedok-reservoir-cres",
            "bedok-748b-bedok-reservoir-cres", "bedok-747a-bedok-reservoir-cres"], Keys(explorer));
    }

    [Fact]
    public void AnEmptyResultCanTellWhetherTheFiltersHidTheMatches()
    {
        var explorer = AddressExplorerTests.Recorded();      // the default maximum hides the S$1.39m address
        explorer.Search("ma'mor");
        Assert.Empty(explorer.Addresses);
        Assert.Equal(1, explorer.CountSearchMatchesIgnoringFilters());
        explorer.Search("tampines");
        Assert.Equal(0, explorer.CountSearchMatchesIgnoringFilters());
    }

    [Fact]
    public void TheSelectionSurvivesASearchOnlyWhileItStillMatches()
    {
        var explorer = Everything();
        explorer.Select("bedok-748a-bedok-reservoir-cres");
        explorer.Search("bedok res");
        Assert.Equal("bedok-748a-bedok-reservoir-cres", explorer.Selected?.AddressKey);
        explorer.Search("bedok res 747");
        Assert.Null(explorer.Selected);
        explorer.Search("");
        Assert.Null(explorer.Selected);          // clearing the search does not bring a hidden selection back
        Assert.Equal(11, explorer.Addresses.Count);
    }
}
