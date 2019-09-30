using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;


namespace EloCalculator
{
    [Serializable]

    public class peopleList
    {
        public List<BasePlayerClass> peeps = new List<BasePlayerClass>();

    }

    public class BasePlayerClass
    {
        [XmlElement("name")]
        public String name { get;  set; }

        [XmlElement("MMR")]
        public int MMR  { get; set; }

        [XmlElement("gamesplayed")]
        public int gamesplayed { get; set; }

        [XmlElement("winPercent")]
        public decimal winPercent { get; set; }

        [XmlElement("varianceFactor")]
        public decimal varianceFactor { get; set; }

        [XmlElement("TotalofAllOpponentRatings")]
        public int TotalofAllOpponentRatings { get; set; }

        [XmlElement("BasePlayerWins")]
        public int BasePlayerWins { get; set; }

        [XmlElement("BasePlayerLosses")]
        public int BasePlayerLosses { get; set; }

    }

}