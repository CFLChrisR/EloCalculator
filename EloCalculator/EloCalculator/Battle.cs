using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace EloCalculator
{
    class Battle
    {
        //ELo system to determine performance of single event
        //add way to revert battle

        public void getResults(BasePlayerClass battleWinner, BasePlayerClass battleLoser) {

           /* Default Test Battle Loser
            BattleClass battleLoser = new BattleClass();
            battleLoser.name = "Goob";
            battleLoser.MMR = 1000;
            battleLoser.gamesplayed = 0;
            battleLoser.winPercent = 100.00f;
            battleLoser.varianceFactor = 1.00f;
            battleLoser.TotalofAllOpponentRatings = 0;
            battleLoser.BasePlayerWins = 0;
            battleLoser.BasePlayerLosses = 0;*/


            try
            {
                battleWinner.TotalofAllOpponentRatings = battleWinner.TotalofAllOpponentRatings + battleLoser.MMR;
                battleWinner.gamesplayed++;
                battleWinner.BasePlayerWins++;

                battleLoser.TotalofAllOpponentRatings = battleLoser.TotalofAllOpponentRatings + battleWinner.MMR;
                battleLoser.gamesplayed++;
                battleLoser.BasePlayerLosses++;

                int dummyInt;
                int dummyInt2;

                int currentbattleWinnerMMR = battleWinner.MMR;
                int currentBattleLoserMMR = battleLoser.MMR;

                dummyInt = (battleWinner.BasePlayerWins - battleWinner.BasePlayerLosses);
                dummyInt2 = (battleLoser.BasePlayerWins - battleLoser.BasePlayerLosses);

                Console.WriteLine(dummyInt);
                dummyInt = dummyInt * 400;
                dummyInt2 = dummyInt2 * 400;

                Console.WriteLine(dummyInt);
                dummyInt = (battleWinner.TotalofAllOpponentRatings + (dummyInt));
                dummyInt2 = (battleLoser.TotalofAllOpponentRatings + (dummyInt2));

                Console.WriteLine(dummyInt);
                dummyInt = (dummyInt / battleWinner.gamesplayed);
                dummyInt2 = (dummyInt2 / battleLoser.gamesplayed);

                Console.WriteLine(dummyInt);

                if (dummyInt < currentbattleWinnerMMR)
                {
                    int winnerLoseMMR = (currentbattleWinnerMMR - dummyInt);
                    Console.WriteLine(dummyInt);
                    Console.WriteLine(currentbattleWinnerMMR);

                    battleWinner.MMR = (currentbattleWinnerMMR + winnerLoseMMR);
                }

                else
                {
                    battleWinner.MMR = dummyInt;
                }

                battleWinner.winPercent = (battleWinner.BasePlayerWins / battleWinner.gamesplayed);


                // Console.WriteLine(dummyInt2);
                //Console.WriteLine(dummyInt2);
                // Console.WriteLine(dummyInt2);
                // Console.WriteLine(dummyInt2);

                if (dummyInt2 > currentBattleLoserMMR)
                {
                    int loserGainMMR = (dummyInt2 - currentBattleLoserMMR);
                    battleLoser.MMR = (currentBattleLoserMMR - loserGainMMR);
                }

                else
                {
                    battleLoser.MMR = dummyInt2;
                }
                battleLoser.winPercent = (battleLoser.BasePlayerWins / battleLoser.gamesplayed);
            }
            catch (NullReferenceException) { }
            

            
            /*battleLoser.MMR = (battleLoser.TotalofAllOpponentRatings - (400 * (battleLoser.BasePlayerWins - battleLoser.BasePlayerLosses))) / battleLoser.gamesplayed;
            battleLoser.winPercent = (battleLoser.BasePlayerWins / battleLoser.gamesplayed);
            //battleLoser.varianceFactor;
            //XmlSerialization.WriteToXmlFile<BasePlayerClass>("C:\\Users\\loofsroom\\Desktop\\person.txt", battleWinner, false);*/
        }

    }

}