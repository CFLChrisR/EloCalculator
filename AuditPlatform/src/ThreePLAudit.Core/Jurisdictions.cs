namespace ThreePLAudit.Core;

public static class Jurisdictions
{
    /// <summary>Federal US law and agency rules.</summary>
    public const string Federal = "US";
    /// <summary>Model codes and industry standards that are not law until adopted (UCC, NMFC, GS1).</summary>
    public const string ModelOrIndustry = "MODEL";

    public static readonly IReadOnlyDictionary<string, string> States = new Dictionary<string, string>
    {
        ["AL"] = "Alabama", ["AK"] = "Alaska", ["AZ"] = "Arizona", ["AR"] = "Arkansas",
        ["CA"] = "California", ["CO"] = "Colorado", ["CT"] = "Connecticut", ["DE"] = "Delaware",
        ["DC"] = "District of Columbia", ["FL"] = "Florida", ["GA"] = "Georgia", ["HI"] = "Hawaii",
        ["ID"] = "Idaho", ["IL"] = "Illinois", ["IN"] = "Indiana", ["IA"] = "Iowa",
        ["KS"] = "Kansas", ["KY"] = "Kentucky", ["LA"] = "Louisiana", ["ME"] = "Maine",
        ["MD"] = "Maryland", ["MA"] = "Massachusetts", ["MI"] = "Michigan", ["MN"] = "Minnesota",
        ["MS"] = "Mississippi", ["MO"] = "Missouri", ["MT"] = "Montana", ["NE"] = "Nebraska",
        ["NV"] = "Nevada", ["NH"] = "New Hampshire", ["NJ"] = "New Jersey", ["NM"] = "New Mexico",
        ["NY"] = "New York", ["NC"] = "North Carolina", ["ND"] = "North Dakota", ["OH"] = "Ohio",
        ["OK"] = "Oklahoma", ["OR"] = "Oregon", ["PA"] = "Pennsylvania", ["RI"] = "Rhode Island",
        ["SC"] = "South Carolina", ["SD"] = "South Dakota", ["TN"] = "Tennessee", ["TX"] = "Texas",
        ["UT"] = "Utah", ["VT"] = "Vermont", ["VA"] = "Virginia", ["WA"] = "Washington",
        ["WV"] = "West Virginia", ["WI"] = "Wisconsin", ["WY"] = "Wyoming",
    };

    public static bool IsState(string? code) => code is not null && States.ContainsKey(code.ToUpperInvariant());

    public static string Name(string code) => code.ToUpperInvariant() switch
    {
        Federal => "United States (federal)",
        ModelOrIndustry => "Model codes & industry standards",
        var c when States.TryGetValue(c, out var n) => n,
        var c => c,
    };
}
