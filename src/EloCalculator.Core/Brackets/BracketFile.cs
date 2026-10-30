using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace EloCalculator.Core.Brackets
{
    /// <summary>On-disk form of a <see cref="Tournament"/>.</summary>
    [XmlRoot("tournament")]
    public sealed class BracketFile
    {
        [XmlAttribute("name")]
        public string Name { get; set; } = string.Empty;

        [XmlAttribute("rated")]
        public bool IsRated { get; set; }

        [XmlAttribute("created")]
        public DateTime CreatedAt { get; set; }

        /// <summary>Entrant names, top seed first.</summary>
        [XmlArray("entrants")]
        [XmlArrayItem("entrant")]
        public List<string> Entrants { get; set; } = new List<string>();

        /// <summary>Reported results in the order they were entered.</summary>
        [XmlArray("results")]
        [XmlArrayItem("result")]
        public List<BracketResult> Results { get; set; } = new List<BracketResult>();
    }

    public sealed class BracketResult
    {
        [XmlAttribute("match")]
        public int MatchId { get; set; }

        [XmlAttribute("winner")]
        public int WinnerSlot { get; set; }

        /// <summary>Id of the rated game in the player history, or 0 if the result wasn't rated.</summary>
        [XmlAttribute("ratingRecord")]
        public int RatingRecordId { get; set; }
    }
}
