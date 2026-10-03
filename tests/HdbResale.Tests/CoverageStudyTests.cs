using HdbResale.Domain;
using System.Text.Json;
using System.Security.Cryptography;
using Xunit;
namespace HdbResale.Tests;
public sealed class CoverageStudyTests
{
    private static ImportResult Study() => CsvImport.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "coverage"));
    [Fact]
    public void SampleCountsCrossTabsAndSourceDerivedHashesStayConsistent()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "coverage");
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "manifest.json")));
        foreach (var hash in manifest.RootElement.GetProperty("derived_sha256").EnumerateObject())
            Assert.Equal(hash.Value.GetString(), Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path.Combine(directory, hash.Name)))));
        var import = Study(); var report = CoverageStudy.Summarize(import);
        Assert.Equal(416, report.Total); Assert.Equal(0, report.Rejected); Assert.Equal(0, report.Diagnostics);
        Assert.Equal(22, report.MatchQuality["ExactAddress"]); Assert.Equal(48, report.MatchQuality["NormalizedAddress"]);
        Assert.Equal(1, report.MatchQuality["Ambiguous"]); Assert.Equal(345, report.MatchQuality["Unmatched"]);
        Assert.Equal(70, report.CoordinateQuality["BlockApproximation"]); Assert.Equal(346, report.CoordinateQuality["Missing"]);
        Assert.Equal(report.Total, report.CrossTabs.Sum(c => c.Count));
        Assert.Equal(report.Total, report.Reasons.Values.Sum());
        Assert.Equal(report.Total, report.MatchQuality.Values.Sum());
        Assert.Equal(report.Total, report.CoordinateQuality.Values.Sum());
        Assert.Equal(346, report.Failures.Count);
        Assert.All(report.CrossTabs.GroupBy(c => (c.Town,c.Period)), g => Assert.Equal(4, g.Sum(c => c.Count)));
        Assert.Equal(26, report.CrossTabs.Select(c => c.Town).Distinct().Count());
        Assert.Equal(7, import.Accepted.Select(t => t.FlatType).Distinct().Count());
        Assert.Equal(0, report.MatchedWithoutGeometry);
    }
    [Fact]
    public void RealHougangPostalConflictNeverChoosesMajorityOrFootprint()
    {
        var row = Assert.Single(Study().Accepted, r => r.Id == "HDB-6769");
        Assert.Equal(MatchQuality.Ambiguous, row.Match.Quality);
        Assert.Equal(CoverageReason.ConflictingPostals, CoverageStudy.Reason(row));
        Assert.Contains(row.Match.PostalAssertions, p => p.SourceRow == 60428 && p.PostalCode == "530836");
        Assert.Contains(row.Match.PostalAssertions, p => p.PostalCode == "530446");
        Assert.Null(row.Match.MatchedFootprint); Assert.Null(row.Location.Point);
    }
    [Fact]
    public void TaxonomySeparatesMissingEvidenceFromIdentityAndGeometry()
    {
        var canonical = Assert.Single(ExplorerStateTests.Fixture().Accepted, r => r.Id == "HDB-34");
        var matchedMissing = canonical with { Location = new(null, CoordinateQuality.Missing, "Explicit test-only null geometry.") };
        Assert.Equal(CoverageReason.MatchedWithoutGeometry, CoverageStudy.Reason(matchedMissing));
        Assert.All(Study().Accepted.Where(t => t.Match.Quality == MatchQuality.Unmatched), t =>
        {
            Assert.Single(t.Match.PropertyCandidates); Assert.Empty(t.Match.PostalAssertions);
            Assert.Equal(CoverageReason.MissingPostalCorroboration, CoverageStudy.Reason(t));
        });
    }
}
