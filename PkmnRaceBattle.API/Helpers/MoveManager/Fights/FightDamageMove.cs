using PkmnRaceBattle.API.Hub;
using PkmnRaceBattle.Domain.Models.PlayerMongo;
using PkmnRaceBattle.Domain.Models.PokemonMongo;
using System.Linq;

namespace PkmnRaceBattle.API.Helpers.MoveManager.Fights
{
    public static class FightDamageMove
    {
        private static readonly Random Random = new Random();



        public static PokemonTeam[] PerformDamageMove(PokemonTeam attacker, PokemonTeam defenser, PokemonTeamMove move, TurnContext turnContext) 
        {

            int damages = CalculateDamage(attacker, defenser, move, turnContext);
            defenser.CurrHp -= damages;
            if(defenser.IsFrozen && move.Type == "fire")
            {
                defenser.IsFrozen = false;
                turnContext.AddMessage(defenser.NameFr + " n'est plus gelé");
            }
            if (defenser.CurrHp < 0) defenser.CurrHp = 0;
            return [attacker, defenser];
        }

        private static int CalculateDamage(PokemonTeam attacker, PokemonTeam defenser, PokemonTeamMove move, TurnContext turnContext)
        {
            int baseAttackStat = move.DamageType == "special" ? attacker.AtkSpe : attacker.Atk;
            int baseDefenseStat = move.DamageType == "special" ? defenser.DefSpe : defenser.Def;

            if (attacker.IsBurning) baseAttackStat = baseAttackStat / 2;

            double attackMultiplier = GetStatMultiplier(move.DamageType == "special" ? attacker.AtkSpeChanges : attacker.AtkChanges);
            double defenseMultiplier = GetStatMultiplier(move.DamageType == "special" ? defenser.DefSpeChanges : defenser.DefChanges);

            int attackStat = (int)(baseAttackStat * attackMultiplier);
            int defenseStat = (int)(baseDefenseStat * defenseMultiplier);


            double baseDamage = (double)(((2 * attacker.Level / 5.0 + 2) * move.Power * (attackStat / (double)defenseStat)) / 50 + 2);
            
            double modifier = CalculateModifier(attacker, defenser, move, turnContext);

            int damage = (int)(baseDamage * modifier);

            return damage;
        }

        private static double CalculateModifier(PokemonTeam attacker, PokemonTeam defenser, PokemonTeamMove move, TurnContext turnContext)
        {
            double stab = 1.0;
            foreach(TypeMongo type in attacker.Types)
            {
                if (type.Name == move.Type) stab = 1.5; 
            }



            double typeEffectiveness = CalculateTypeEffectiveness(move.Type, defenser.Types, turnContext);
            double critical = 1.0;
            if(IsCriticalHit(attacker, move))
            {
                turnContext.AddMessage("Coup critique !");
                critical = 1.5;
            }

            double randomFactor = Random.Next(85, 101) / 100.0;

            double otherModifiers = 1.0; //Prendre en compte plus tard les changements météos/talents/etc...

            return stab * typeEffectiveness * critical * randomFactor * otherModifiers;
        }

        private static bool IsCriticalHit(PokemonTeam attacker, PokemonTeamMove move)
        {
            double baseCritRate = 1.0 / 24.0; // Taux de base dans Pokémon (environ 4,17%)

            if (move.CritRate == 1)
            {
                baseCritRate = 1.0 / 8.0; // Taux de critique élevé (12,5%)
            }

            Random random = new Random();
            double randomValue = random.NextDouble();

            return randomValue < baseCritRate;
        }

        public static double GetStatMultiplier(int statChange)
        {
            // Table des multiplicateurs en fonction du niveau de changement
            double[] multipliers = {
                0.25,  // -6
                0.285, // -5
                0.333, // -4
                0.4,   // -3
                0.5,   // -2
                0.666, // -1
                1.0,   // 0
                1.5,   // +1
                2.0,   // +2
                2.5,   // +3
                3.0,   // +4
                3.5,   // +5
                4.0    // +6
                };

            // Le niveau de changement est compris entre -6 et +6
            int index = statChange + 6;
            return multipliers[index];
        }

        private static double CalculateTypeEffectiveness(string moveType, TypeMongo[] defenderTypes, TurnContext turnContext)
        {
            int strongScore = 0;
            int weakScore = 0;
            int notEffectiveScore = 0;


            switch (moveType)
            {
                case "bug":
                    string[] bugStrongness = ["grass", "dark", "psychic"];
                    string[] bugWeakness = ["steel", "fighting", "fairy", "fire", "poison", "ghost", "flying"];
                    foreach (TypeMongo type in defenderTypes)
                    {
                        if (bugStrongness.Contains(type.Name)) strongScore++;
                        if (bugWeakness.Contains(type.Name)) weakScore++;
                    }
                    break;

                case "dark":
                    string[] darkStrongness = ["ghost", "psychic"];
                    string[] darkWeakness = ["fighting", "fairy", "dark"];
                    foreach (TypeMongo type in defenderTypes)
                    {
                        if (darkStrongness.Contains(type.Name)) strongScore++;
                        if (darkWeakness.Contains(type.Name)) weakScore++;
                    }
                    break;

                case "dragon":
                    string[] dragonStrongness = ["dragon"];
                    string[] dragonWeakness = ["steel"];
                    string[] dragonNotEffective = ["fairy"];
                    foreach (TypeMongo type in defenderTypes)
                    {
                        if (dragonStrongness.Contains(type.Name)) strongScore ++;
                        if (dragonWeakness.Contains(type.Name)) weakScore ++;
                        if (dragonNotEffective.Contains(type.Name)) notEffectiveScore ++;
                    }
                    break;

                case "electric":
                    string[] electricStrongness = ["water", "flying"];
                    string[] electricWeakness = ["dragon", "electric", "grass"];
                    string[] electricNotEffetive = ["ground"];
                    foreach (TypeMongo type in defenderTypes)
                    {
                        if (electricStrongness.Contains(type.Name)) strongScore++;
                        if (electricWeakness.Contains(type.Name)) weakScore++;
                        if (electricNotEffetive.Contains(type.Name)) notEffectiveScore++;
                    }
                    break;

                case "fairy":
                    string[] fairyStrongness = ["fighting", "dragon", "dark"];
                    string[] fairyWeakness = ["steel", "fire", "poison"];
                    foreach (TypeMongo type in defenderTypes)
                    {
                        if (fairyStrongness.Contains(type.Name)) strongScore++;
                        if (fairyWeakness.Contains(type.Name)) weakScore++;
                    }
                    break;

                case "fighting":
                    string[] fightStrongness = ["steel", "ice", "normal", "rock", "dark"];
                    string[] fightWeakness = ["fairy", "bug", "poison", "psychic", "flying"];
                    string[] fightNotEffective = ["ghost"];
                    foreach (TypeMongo type in defenderTypes)
                    {
                        if (fightStrongness.Contains(type.Name)) strongScore++;
                        if (fightWeakness.Contains(type.Name)) weakScore++;
                        if (fightNotEffective.Contains(type.Name)) notEffectiveScore++;
                    }
                    break;

                case "fire":
                    string[] fireStrongness = ["steel", "ice", "bug", "grass"];
                    string[] fireWeakness = ["dragon", "water", "fire"];
                    foreach (TypeMongo type in defenderTypes)
                    {
                        if (fireStrongness.Contains(type.Name)) strongScore++;
                        if (fireWeakness.Contains(type.Name)) weakScore++;
                    }
                    break;

                case "flying":
                    string[] flyStrongness = ["fighting", "bug", "grass"];
                    string[] flyWeakness = ["steel", "electric", "rock"];
                    foreach (TypeMongo type in defenderTypes)
                    {
                        if (flyStrongness.Contains(type.Name)) strongScore++;
                        if (flyWeakness.Contains(type.Name)) weakScore++;
                    }
                    break;

                case "ghost":
                    string[] ghostStrongness = ["psychic", "ghost"];
                    string[] ghostWeakness = ["dark"];
                    string[] ghostNotEffective = ["normal"];
                    foreach (TypeMongo type in defenderTypes)
                    {
                        if (ghostStrongness.Contains(type.Name)) strongScore++;
                        if (ghostWeakness.Contains(type.Name)) weakScore++;
                        if (ghostNotEffective.Contains(type.Name)) notEffectiveScore++;
                    }
                    break;

                case "grass":
                    string[] grassStrongness = ["water", "rock", "ground"];
                    string[] grassWeakness = ["steel", "dragon", "fire", "bug", "grass", "poison", "flying"];
                    foreach (TypeMongo type in defenderTypes)
                    {
                        if (grassStrongness.Contains(type.Name)) strongScore++;
                        if (grassWeakness.Contains(type.Name)) weakScore++;
                    }
                    break;

                case "ground":
                    string[] groundStrongness = ["steel", "electric", "feu", "poison", "rock"];
                    string[] groundWeakness = ["bug", "grass"];
                    string[] groundNotEffective = ["fly"];
                    foreach (TypeMongo type in defenderTypes)
                    {
                        if (groundStrongness.Contains(type.Name)) strongScore++;
                        if (groundWeakness.Contains(type.Name)) weakScore++;
                        if (groundNotEffective.Contains(type.Name)) notEffectiveScore++;
                    }
                    break;

                case "ice":
                    string[] iceStrongness = ["dragon", "grass", "ground", "flying"];
                    string[] iceWeakness = ["steel", "water", "fire", "ice"];
                    foreach (TypeMongo type in defenderTypes)
                    {
                        if (iceStrongness.Contains(type.Name)) strongScore++;
                        if (iceWeakness.Contains(type.Name)) weakScore++;
                    }
                    break;

                case "normal":
                    string[] normalWeakness = ["steel", "rock"];
                    string[] normalNotEffective = ["gost"];
                    foreach (TypeMongo type in defenderTypes)
                    {
                        if (normalWeakness.Contains(type.Name)) weakScore++;
                        if (normalNotEffective.Contains(type.Name)) notEffectiveScore++;
                    }
                    break;

                case "poison":
                    string[] poisonStrongness = ["fiary", "grass"];
                    string[] poisonWeakness = ["poison", "rock", "ground", "ghost"];
                    string[] poisonNotEffectiver = ["steel"];
                    foreach (TypeMongo type in defenderTypes)
                    {
                        if (poisonStrongness.Contains(type.Name)) strongScore++;
                        if (poisonWeakness.Contains(type.Name)) weakScore++;
                        if (poisonNotEffectiver.Contains(type.Name)) notEffectiveScore++;
                    }
                    break;

                case "psychic":
                    string[] psyStrongness = ["fighting", "poison"];
                    string[] psyWeakness = ["steel", "psychic"];
                    string[] psyNotEffective = ["dark"];
                    foreach (TypeMongo type in defenderTypes)
                    {
                        if (psyStrongness.Contains(type.Name)) strongScore++;
                        if (psyWeakness.Contains(type.Name)) weakScore++;
                        if (psyNotEffective.Contains(type.Name)) notEffectiveScore++;
                    }
                    break;

                case "rock":
                    string[] rockStrongness = ["fire", "ice", "bug", "flying"];
                    string[] rockWeakness = ["steel", "fighting", "ground"];
                    foreach (TypeMongo type in defenderTypes)
                    {
                        if (rockStrongness.Contains(type.Name)) strongScore++;
                        if (rockWeakness.Contains(type.Name)) weakScore++;
                    }
                    break;

                case "steel":
                    string[] steelStrongness = ["fairy", "ice", "rock"];
                    string[] steelWeakness = ["steel", "water", "electric", "fire"];
                    foreach (TypeMongo type in defenderTypes)
                    {
                        if (steelStrongness.Contains(type.Name)) strongScore++;
                        if (steelWeakness.Contains(type.Name)) weakScore++;
                    }
                    break;

                case "water":
                    string[] waterStrongness = ["fire", "rock", "ground"];
                    string[] waterWeakness = ["dragon", "water", "grass"];
                    foreach (TypeMongo type in defenderTypes)
                    {
                        if (waterStrongness.Contains(type.Name)) strongScore++;
                        if (waterWeakness.Contains(type.Name)) weakScore++;
                    }
                    break;
            }

            if (notEffectiveScore == 1) { turnContext.AddMessage("Cela n'a aucun effet"); return 0.0; }
            if (strongScore == 2) { turnContext.AddMessage("C'est ultra efficace !"); return 4.0; }
            if (strongScore == 1 && weakScore == 1)  return 1.0;
            if (strongScore == 1 && weakScore == 0) { turnContext.AddMessage("C'est super efficace !"); return 2.0; }
            if (weakScore == 1 && strongScore == 0) { turnContext.AddMessage("Ce n'est pas très efficace..."); return 0.5; }
            if (weakScore == 2 && strongScore == 0) { turnContext.AddMessage("Ce n'est vraiment pas efficace..."); return 0.25; }
            return 1.0;

        }
    }
}
