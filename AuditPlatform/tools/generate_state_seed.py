"""Generates data/regulations/us-states.json: one UCC Article 7 entry and one workplace-safety
jurisdiction entry per state. Citations and URLs come from general knowledge and every entry
starts Unverified. Run the CLI's verify-sources command, then confirm each one by hand.

Entries with no URL are 'NeedsSource': the citation still needs finding on the state's
official code site."""
import json, pathlib

STATES = {
 "AL":"Alabama","AK":"Alaska","AZ":"Arizona","AR":"Arkansas","CA":"California","CO":"Colorado",
 "CT":"Connecticut","DE":"Delaware","DC":"District of Columbia","FL":"Florida","GA":"Georgia",
 "HI":"Hawaii","ID":"Idaho","IL":"Illinois","IN":"Indiana","IA":"Iowa","KS":"Kansas","KY":"Kentucky",
 "LA":"Louisiana","ME":"Maine","MD":"Maryland","MA":"Massachusetts","MI":"Michigan","MN":"Minnesota",
 "MS":"Mississippi","MO":"Missouri","MT":"Montana","NE":"Nebraska","NV":"Nevada","NH":"New Hampshire",
 "NJ":"New Jersey","NM":"New Mexico","NY":"New York","NC":"North Carolina","ND":"North Dakota",
 "OH":"Ohio","OK":"Oklahoma","OR":"Oregon","PA":"Pennsylvania","RI":"Rhode Island","SC":"South Carolina",
 "SD":"South Dakota","TN":"Tennessee","TX":"Texas","UT":"Utah","VT":"Vermont","VA":"Virginia",
 "WA":"Washington","WV":"West Virginia","WI":"Wisconsin","WY":"Wyoming",
}

# (citation, official URL or None). Only filled where the codification is well known.
UCC7 = {
 "AL": ("Ala. Code Title 7, Chapter 7", None),
 "AZ": ("A.R.S. Title 47, Chapter 7", "https://www.azleg.gov/arsDetail/?title=47"),
 "CA": ("Cal. Com. Code §§ 7101–7603 (Division 7)", "https://leginfo.legislature.ca.gov/faces/codes_displayexpandedbranch.xhtml?tocCode=COM&division=7."),
 "CO": ("C.R.S. Title 4, Article 7", None),
 "FL": ("Fla. Stat. Chapter 677", "http://www.leg.state.fl.us/statutes/index.cfm?App_mode=Display_Statute&URL=0600-0699/0677/0677ContentsIndex.html"),
 "GA": ("O.C.G.A. Title 11, Article 7", None),
 "IL": ("810 ILCS 5/Article 7", None),
 "IN": ("Ind. Code 26-1-7", None),
 "KY": ("KRS Chapter 355, Article 7", None),
 "MA": ("M.G.L. c. 106, Article 7", "https://malegislature.gov/Laws/GeneralLaws/PartI/TitleXV/Chapter106"),
 "MI": ("MCL 440.7101 et seq.", None),
 "MN": ("Minn. Stat. Chapter 336, Article 7", "https://www.revisor.mn.gov/statutes/cite/336"),
 "MO": ("RSMo Chapter 400, Article 7", "https://revisor.mo.gov/main/OneChapter.aspx?chapter=400"),
 "NC": ("N.C. Gen. Stat. Chapter 25, Article 7", "https://www.ncleg.gov/Laws/GeneralStatuteSections/Chapter25"),
 "NJ": ("N.J.S.A. 12A:7", None),
 "NV": ("NRS Chapter 104, Article 7", "https://www.leg.state.nv.us/NRS/NRS-104.html"),
 "NY": ("N.Y. U.C.C. Law Article 7", "https://www.nysenate.gov/legislation/laws/UCC/A7"),
 "OH": ("Ohio Rev. Code Chapter 1307", "https://codes.ohio.gov/ohio-revised-code/chapter-1307"),
 "OK": ("12A O.S. Article 7", None),
 "OR": ("ORS Chapter 77", "https://www.oregonlegislature.gov/bills_laws/ors/ors077.html"),
 "PA": ("13 Pa.C.S. Division 7", None),
 "SC": ("S.C. Code Title 36, Chapter 7", "https://www.scstatehouse.gov/code/t36c007.php"),
 "TN": ("Tenn. Code Ann. Title 47, Chapter 7", None),
 "TX": ("Tex. Bus. & Com. Code Chapter 7", "https://statutes.capitol.texas.gov/Docs/BC/htm/BC.7.htm"),
 "VA": ("Va. Code Title 8.7A", "https://law.lis.virginia.gov/vacode/title8.7A/"),
 "WA": ("RCW 62A.7", "https://app.leg.wa.gov/RCW/default.aspx?cite=62A.7"),
 "WI": ("Wis. Stat. Chapter 407", "https://docs.legis.wisconsin.gov/statutes/statutes/407"),
}

# States with OSHA-approved plans covering private-sector employers (verify against osha.gov/stateplans).
PRIVATE_SECTOR_PLANS = {"AK","AZ","CA","HI","IN","IA","KY","MD","MI","MN","NV","NM","NC","OR","SC","TN","UT","VT","VA","WA","WY"}
# Plans covering only state and local government workers; federal OSHA covers private employers.
PUBLIC_ONLY_PLANS = {"CT","IL","ME","MA","NJ","NY"}

entries = []
for code, name in STATES.items():
    cit, url = UCC7.get(code, (f"UCC Article 7 as enacted in {name} (citation to be confirmed)", None))
    entries.append({
        "id": f"{code}-UCC7", "jurisdiction": code, "kind": "statute",
        "topics": ["warehouse-receipts-liens"],
        "title": f"{name}: documents of title / warehouse receipts (UCC Article 7)",
        "citation": cit,
        "summary": f"{name}'s enactment of UCC Article 7, which governs warehouse receipts, the warehouse's duty of care and liability limits, and warehouse liens. It controls over the model text (MODEL-UCC7-*) where they differ. Check for non-uniform amendments.",
        **({"sourceUrl": url, "sourceAuthority": f"{name} official legislative code site"} if url else {}),
        **({"notes": "Find the official codification on the state legislature's code site, add its URL, then verify."} if not url else {}),
    })

    if code in PRIVATE_SECTOR_PLANS:
        title, summary = (f"{name}: state OSHA plan covers private-sector warehouses",
            f"{name} runs an OSHA-approved State Plan covering private employers, so the state agency enforces workplace safety at 3PL facilities here. Its standards must be at least as effective as federal OSHA and may be stricter. Add the state's own powered-truck, heat or ergonomics rules as separate entries.")
    elif code in PUBLIC_ONLY_PLANS:
        title, summary = (f"{name}: federal OSHA covers private-sector warehouses",
            f"{name}'s State Plan covers only state and local government workers. Private 3PL facilities fall under federal OSHA (29 CFR 1910).")
    else:
        title, summary = (f"{name}: federal OSHA jurisdiction",
            f"{name} has no OSHA-approved State Plan for private employers, so federal OSHA (29 CFR 1910) applies to 3PL facilities here.")
    entries.append({
        "id": f"{code}-OSHA-JURISDICTION", "jurisdiction": code, "kind": "agencyGuidance",
        "topics": ["workplace-safety"], "title": title, "citation": "OSHA State Plans listing",
        "summary": summary, "sourceUrl": "https://www.osha.gov/stateplans",
        "sourceAuthority": "U.S. Department of Labor, OSHA", "verificationPhrase": name,
    })

out = pathlib.Path(__file__).resolve().parent.parent / "data" / "regulations" / "us-states.json"
out.write_text(json.dumps(entries, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
print(f"wrote {len(entries)} entries to {out}")
