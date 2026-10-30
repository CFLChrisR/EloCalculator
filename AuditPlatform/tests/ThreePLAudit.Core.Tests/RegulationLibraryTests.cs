using System.Net;
using ThreePLAudit.Core.Regulations;
using static ThreePLAudit.Core.Tests.TestSupport;

namespace ThreePLAudit.Core.Tests;

public class RegulationLibraryTests
{
    [Fact]
    public void Seed_library_is_well_formed()
    {
        var lib = SeedLibrary();
        Assert.True(lib.All.Count > 100);
        Assert.All(lib.All, r =>
        {
            Assert.True(r.Jurisdiction is Jurisdictions.Federal or Jurisdictions.ModelOrIndustry || Jurisdictions.IsState(r.Jurisdiction), r.Id);
            Assert.NotEmpty(r.Topics);
            Assert.All(r.Topics, t => Assert.Contains(t, Topics.All));
            Assert.False(string.IsNullOrWhiteSpace(r.Citation), r.Id);
            if (r.SourceUrl is not null) Assert.StartsWith("http", r.SourceUrl);
            // Nothing ships pre-verified: verification has to happen against the live source.
            Assert.Null(r.Verification.VerifiedBy);
        });
        Assert.All(Jurisdictions.States.Keys, s => Assert.Contains(lib.All, r => r.Jurisdiction == s));
    }

    [Fact]
    public void State_query_includes_federal_and_model_entries_with_state_first()
    {
        var results = SeedLibrary().Query("fl");
        Assert.Equal("FL", results[0].Jurisdiction);
        Assert.Contains(results, r => r.Jurisdiction == "US");
        Assert.Contains(results, r => r.Jurisdiction == "MODEL");
        Assert.DoesNotContain(results, r => r.Jurisdiction == "TX");

        var stateOnly = SeedLibrary().Query("FL", includeFederal: false, includeModel: false);
        Assert.All(stateOnly, r => Assert.Equal("FL", r.Jurisdiction));
    }

    [Fact]
    public void Topic_and_text_filters_apply()
    {
        var lib = SeedLibrary();
        Assert.All(lib.Query(topic: Topics.FreightClaims), r => Assert.Contains(Topics.FreightClaims, r.Topics));
        Assert.Contains(lib.Query(text: "Carmack"), r => r.Id == "US-49USC14706");
    }

    [Fact]
    public void Coverage_reports_gaps()
    {
        var cells = SeedLibrary().Coverage();
        Assert.Equal(Jurisdictions.States.Count * Topics.StateCoverage.Count, cells.Count);
        Assert.Contains(cells, c => c is { Jurisdiction: "FL", Topic: Topics.WarehouseReceipts, Entries: > 0 });
        Assert.Contains(cells, c => c is { Topic: Topics.SalesTax, Entries: 0 });
    }

    [Fact]
    public void Save_and_load_round_trips()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        var lib = SeedLibrary();
        lib.SaveDirectory(dir);
        var reloaded = RegulationLibrary.LoadDirectory(dir);
        Assert.Equal(lib.All.Count, reloaded.All.Count);
        Assert.Equal(lib.Get("US-49USC13710")!.Requirement, reloaded.Get("US-49USC13710")!.Requirement);
        Assert.Equal(36, reloaded.Get("US-49CFR172.704")!.Requirement!.MaxAgeMonths);
    }
}

public class SourceVerifierTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(respond(request));
    }

    private static SourceVerifier Verifier(HttpStatusCode code, string html) =>
        new(new HttpClient(new StubHandler(_ => new HttpResponseMessage(code) { Content = new StringContent(html) })));

    private static readonly RegulationReference Ref = new()
    {
        Id = "US-X", Jurisdiction = "US", Title = "X", Citation = "49 U.S.C. § 13710",
        SourceUrl = "https://example.gov/x", VerificationPhrase = "within 180 days",
    };

    [Fact]
    public void New_entry_is_unverified_and_entry_without_url_needs_source()
    {
        Assert.Equal(VerificationStatus.Unverified, Ref.Status);
        Assert.Equal(VerificationStatus.NeedsSource, (Ref with { SourceUrl = null }).Status);
    }

    [Fact]
    public async Task Phrase_is_matched_through_markup_and_whitespace()
    {
        var r = await Verifier(HttpStatusCode.OK, "<html><script>x</script><p>must contest  <b>within\n180 days</b></p></html>").CheckAsync(Ref);
        Assert.Equal(VerificationStatus.SourceMatched, r.Status);
        Assert.Equal(200, r.Verification.LastHttpStatus);
        Assert.NotNull(r.Verification.LastContentSha256);
    }

    [Fact]
    public async Task Missing_phrase_and_http_errors_are_reported()
    {
        Assert.Equal(VerificationStatus.PhraseMissing, (await Verifier(HttpStatusCode.OK, "<p>repealed</p>").CheckAsync(Ref)).Status);
        Assert.Equal(VerificationStatus.Broken, (await Verifier(HttpStatusCode.NotFound, "").CheckAsync(Ref)).Status);

        var throwing = new SourceVerifier(new HttpClient(new StubHandler(_ => throw new HttpRequestException("dns"))));
        var r = await throwing.CheckAsync(Ref);
        Assert.Equal(VerificationStatus.Broken, r.Status);
        Assert.Equal("dns", r.Verification.LastError);
    }

    [Fact]
    public async Task Human_verification_survives_cosmetic_changes_but_not_losing_the_phrase()
    {
        var checkedRef = await Verifier(HttpStatusCode.OK, "<p>within 180 days</p>").CheckAsync(Ref);
        var confirmed = SourceVerifier.Confirm(checkedRef, "Chris", "within 180 days", Now);
        Assert.Equal(VerificationStatus.HumanVerified, confirmed.Status);

        var cosmetic = await Verifier(HttpStatusCode.OK, "<p>Updated nav. within 180 days</p>").CheckAsync(confirmed);
        Assert.Equal(VerificationStatus.HumanVerified, cosmetic.Status);

        var changed = await Verifier(HttpStatusCode.OK, "<p>within 90 days</p>").CheckAsync(confirmed);
        Assert.Equal(VerificationStatus.ChangedSinceVerified, changed.Status);
    }
}
