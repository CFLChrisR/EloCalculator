using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ThreePLAudit.Core.Regulations;

/// <summary>
/// Fetches each reference's source URL, records the HTTP status and a hash of the text, and
/// checks for the expected phrase. A passing check proves the source exists and still says
/// what we expect. It does not replace a person reading the law.
/// </summary>
public sealed partial class SourceVerifier(HttpClient http, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public async Task<RegulationReference> CheckAsync(RegulationReference reference, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reference.SourceUrl)) return reference;

        var v = reference.Verification;
        try
        {
            using var resp = await http.GetAsync(reference.SourceUrl, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            var text = NormalizeText(body);
            v = v with
            {
                LastCheckedAt = _clock.GetUtcNow(),
                LastHttpStatus = (int)resp.StatusCode,
                LastError = resp.StatusCode == HttpStatusCode.OK ? null : $"HTTP {(int)resp.StatusCode}",
                LastContentSha256 = Sha256(text),
                LastPhraseFound = reference.VerificationPhrase is { } phrase
                    ? text.Contains(NormalizeText(phrase), StringComparison.OrdinalIgnoreCase)
                    : null,
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            v = v with { LastCheckedAt = _clock.GetUtcNow(), LastHttpStatus = null, LastError = ex.Message };
        }
        return reference with { Verification = v };
    }

    public async Task<IReadOnlyList<RegulationReference>> CheckAllAsync(
        IEnumerable<RegulationReference> references, int maxParallel = 4, CancellationToken ct = default)
    {
        var results = new List<RegulationReference>();
        using var gate = new SemaphoreSlim(maxParallel);
        var tasks = references.Select(async r =>
        {
            await gate.WaitAsync(ct);
            try { return await CheckAsync(r, ct); }
            finally { gate.Release(); }
        });
        results.AddRange(await Task.WhenAll(tasks));
        return results;
    }

    /// <summary>Records that a person read the source and confirmed the entry against the current content.</summary>
    public static RegulationReference Confirm(RegulationReference reference, string verifiedBy, string? excerpt, DateTimeOffset at) =>
        reference with
        {
            Verification = reference.Verification with
            {
                VerifiedBy = verifiedBy,
                VerifiedAt = at,
                VerifiedExcerpt = excerpt,
                VerifiedContentSha256 = reference.Verification.LastContentSha256,
            },
        };

    /// <summary>Strips markup and collapses whitespace so cosmetic page changes don't look like content changes.</summary>
    public static string NormalizeText(string html)
    {
        var noScripts = ScriptOrStyle().Replace(html, " ");
        var noTags = Tags().Replace(noScripts, " ");
        var decoded = WebUtility.HtmlDecode(noTags).Replace('§', '§');
        return Whitespace().Replace(decoded, " ").Trim();
    }

    private static string Sha256(string s) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(s)));

    [GeneratedRegex(@"<(script|style)[^>]*>.*?</\1>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ScriptOrStyle();
    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tags();
    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
