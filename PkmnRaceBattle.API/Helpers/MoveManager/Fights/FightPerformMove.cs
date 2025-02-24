using PkmnRaceBattle.API.Hub;
using PkmnRaceBattle.Domain.Models.PlayerMongo;

namespace PkmnRaceBattle.API.Helpers.MoveManager.Fights
{
    public static class FightPerformMove
    {
        public static PokemonTeam[] PerformMove(PokemonTeam attacker, PokemonTeam defenser, PokemonTeamMove usedMove, TurnContext turnContext, bool playerAttacking)
        {

            if(usedMove.Type == "item")
            {
                if (usedMove.NameFr.EndsWith("ball"))
                {

                   

                }
                else
                {
                    PokemonTeam[] response = FightUseItem.UseItem(attacker, defenser, usedMove, turnContext, playerAttacking);
                    attacker = response[0];
                    defenser = response[1];
                }
            }
            else
            {
                
                bool isPlaying = FightAilmentMove.CanPokemonPlay(attacker, turnContext);
                if (!isPlaying && attacker.IsConfused > 0) { 
                
                    attacker = FightAilmentMove.SufferConfusion(attacker, turnContext);
                }
                if (isPlaying)
                {
                    if (IsSucceedHisMove(attacker, defenser, usedMove)) {

                        int nbHits = 1;
                        if (usedMove.MinHits != null && usedMove.MaxHits != null)
                        {
                            Random rnd = new Random();
                            nbHits = rnd.Next((int)usedMove.MinHits, (int)usedMove.MaxHits + 1);
                        }

                            //Inflige des dégâts ?
                        if (usedMove.Category.Contains("damage"))
                        {
                            if (nbHits > 1) {
                                

                                for(int i = 0; i < nbHits; i++)
                                {
                                    int oldDefHp = defenser.CurrHp;
                                    PokemonTeam[] response = FightDamageMove.PerformDamageMove(attacker, defenser, usedMove, turnContext);

                                    

                                    if (playerAttacking)
                                    {
                                        turnContext.Opponent.Hp.Add(oldDefHp - response[1].CurrHp);
                                    }
                                    else
                                    {
                                        turnContext.Player.Hp.Add(oldDefHp - response[1].CurrHp);
                                    }

                                    attacker = response[0];
                                    defenser = response[1];
                                }

                                turnContext.AddMessage("Touché " + nbHits + " fois");

                            }
                            else
                            {
                                int oldDefHp = defenser.CurrHp;
                                PokemonTeam[] response = FightDamageMove.PerformDamageMove(attacker, defenser, usedMove, turnContext);
                                if (playerAttacking)
                                {
                                    turnContext.Opponent.Hp.Add(oldDefHp - response[1].CurrHp);
                                }
                                else
                                {
                                    turnContext.Player.Hp.Add(oldDefHp - response[1].CurrHp);
                                }
                                attacker = response[0];
                                defenser = response[1];
                            }

                        }

                        //Applique un état ?
                        if (usedMove.Category.Contains("ailment") || usedMove.Category.Contains("swagger"))
                        {
                            for(int i = 0; i<nbHits; i++)
                            {
                                if (usedMove.AilmentChance != 0)
                                {
                                    Random rnd = new Random();
                                    int randomValue = rnd.Next(1, 101); // Génère un nombre aléatoire entre 1 et 100

                                    if (randomValue <= usedMove.AilmentChance)
                                    {
                                        defenser = FightAilmentMove.PerformAilment(defenser, usedMove, turnContext);
                                    }
                                }
                                else
                                {
                                    defenser = FightAilmentMove.PerformAilment(defenser, usedMove, turnContext);
                                }
                            } 
                        }

                        //Applique la peur ?
                        if (usedMove.FlinchChance != 0)
                        {
                            Random random = new Random();
                            int rnd = random.Next(1, 101);
                            if (rnd <= usedMove.FlinchChance)
                            {
                                defenser.IsFlinched = true;
                            }
                        }

                        //Changements de stats ?
                        if (usedMove.Category.Contains("net-good-stats") || usedMove.Category.Contains("swagger") || usedMove.Category.Contains("raise") || usedMove.Category.Contains("lower"))
                        {
                            bool useMove = true;
                            if (usedMove.StatChance != 0) {
                                Random random = new Random();
                                int rnd = random.Next(1, 101);
                                if (rnd > usedMove.StatChance)
                                {
                                    useMove = false;
                                }
                            }

                            if (useMove)
                            {
                                //Sur le lanceur
                                if (usedMove.Category.Contains("raise") || (usedMove.Category == "net-good-stats" && usedMove.Target == "user") || (usedMove.Category == "swagger" && usedMove.Target == "user"))
                                {
                                    attacker = FightStatusMove.PerformStatusMove(attacker, usedMove, turnContext, playerAttacking, usedMove.Target);
                                }
                                else //sur l'adversaire
                                {
                                    defenser = FightStatusMove.PerformStatusMove(defenser, usedMove, turnContext, playerAttacking, usedMove.Target);
                                }
                            }
                        }
                    }
                    else
                    {
                        turnContext.AddMessage(attacker.NameFr + " rate son attaque.");
                    }
                    
                }
            }


            return [attacker, defenser];
        }

        public static bool IsSucceedHisMove(PokemonTeam attacker, PokemonTeam defenser, PokemonTeamMove move)
        {
            if(move.Accuracy != null)
            {
                double multiplicator = GetAccuracyMultiplier(attacker.AccuracyChanges) / GetAccuracyMultiplier(defenser.EvasionChanges);
                double? Preussite = move.Accuracy * multiplicator;

                Random random = new Random();
                int rnd = random.Next(random.Next(1, 101));
                if (rnd <= Preussite.Value)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return true;
            }

        }

        public static double GetAccuracyMultiplier(int statChange)
        {
            // Table des multiplicateurs en fonction du niveau de changement
            double[] multipliers = {
                0.33,  // -6
                0.38, // -5
                0.43, // -4
                0.5,   // -3
                0.6,   // -2
                0.75, // -1
                1.0,   // 0
                1.33,   // +1
                1.67,   // +2
                2.0,   // +3
                2.33,   // +4
                2.67,   // +5
                3.0    // +6
                };

            // Le niveau de changement est compris entre -6 et +6
            int index = statChange + 6;
            return multipliers[index];
        }

    }
}
