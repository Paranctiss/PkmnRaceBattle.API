using PkmnRaceBattle.API.Hub;
using PkmnRaceBattle.Domain.Models.PlayerMongo;
using PkmnRaceBattle.Domain.Models.PokemonMongo;
using System.Linq;

namespace PkmnRaceBattle.API.Helpers.MoveManager.Fights
{
    public static class FightDamageMove
    {
        private static readonly Random Random = new Random();



        public static PokemonTeam[] PerformDamageMove(PokemonTeam attacker, PokemonTeam defenser, PokemonTeamMove move, string fieldChange, TurnContext turnContext) 
        {
            int damages;
            if (move.BrutDamages != null)
            {
                damages = (int)move.BrutDamages;
                attacker.BlowsTaken = 0;
            }
            else
            {
                damages = CalculateDamage(attacker, defenser, move, turnContext);
                if (fieldChange == "Mur Lumière" && move.DamageType == "special") damages = damages / 2;
                if (fieldChange == "Protection" && move.DamageType == "physical") damages = damages / 2;
            }

            damages = SpecialCaseDamages(damages, move, attacker, defenser, turnContext);
            
            if (attacker.CurrHp > attacker.BaseHp) attacker.CurrHp = attacker.BaseHp;

            int oldDefHp = defenser.CurrHp;
            defenser.CurrHp -= damages;
            if(defenser.CurrHp < 0) defenser.CurrHp = 0;

            int damagesTaken = oldDefHp - defenser.CurrHp;
            //attacker.CurrHp += SpecialCaseDrain(damagesTaken, move, attacker, defenser, turnContext);
            SpecialCaseDrain(damagesTaken, move, attacker, defenser, turnContext);

            defenser.BlowsTaken += damages;
            defenser.BlowsTakenType = move.DamageType;

            if (defenser.IsFrozen && move.Type == "fire")
            {
                defenser.IsFrozen = false;
                turnContext.AddMessage(defenser.NameFr + " n'est plus gelé");
            }
            if (defenser.CurrHp < 0) defenser.CurrHp = 0;
            return [attacker, defenser];
        }

        private static void SpecialCaseDrain(int damages, PokemonTeamMove move, PokemonTeam attacker, PokemonTeam defenser, TurnContext turnContext)
        {
            switch (move.NameFr)
            {
                case "Vole-Vie":
                case "Méga-Sangsue":
                case "Vampirisme":
                case "Dévorêve":
                    turnContext.AddMessage("L'énergie du " + defenser.NameFr + "est drainée");
                    break;
            }
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

            if (move.Power == null) move.Power = 0;
            double baseDamage = (double)(((2 * attacker.Level / 5.0 + 2) * move.Power * (attackStat / (double)defenseStat)) / 50 + 2);
            

            double modifier = CalculateModifier(attacker, defenser, move, turnContext);
            if (IsSpecialCasesModifier(move) && modifier != 0.0) modifier = 1.0;
            int damage = (int)(baseDamage * modifier);

            return damage;
        }

        private static bool IsSpecialCasesModifier(PokemonTeamMove move)
        {
            string[] specialCaseModifier = ["Balayage", "Frappe Atlas", "Ombre Nocturne", "Croc Fatal", "Vague Psy"];
            if (specialCaseModifier.Contains(move.NameFr)) return true;
            return false;
        }

        private static double CalculateModifier(PokemonTeam attacker, PokemonTeam defenser, PokemonTeamMove move, TurnContext turnContext)
        {
            double stab = 1.0;
            foreach(TypeMongo type in attacker.Types)
            {
                if (type.Name == move.Type) stab = 1.5; 
            }



            double typeEffectiveness = CalculateTypeEffectiveness(move.Type, defenser.Types, turnContext);
            if (typeEffectiveness == 0.0) attacker = FightPerformMove.SpecialCaseMissMove(attacker, move, turnContext);
            double critical = 1.0;
            if(move.NameFr != "Confusion" && IsCriticalHit(attacker, move))
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
            // Taux de critique de base
            double baseCritRate = 1.0 / 24.0; // Taux de base normal (environ 4,17%)

            // Si l'attaque a un taux de critique élevé
            if (move.CritRate == 1)
            {
                baseCritRate = 1.0 / 8.0; // Taux de critique élevé (12,5%)
            }

            // Appliquer le multiplicateur de taux de critique en fonction de CritChanges
            double critMultiplier = Math.Pow(2, attacker.CritChanges); // 2^CritChanges
            double finalCritRate = baseCritRate * critMultiplier;

            // Générer une valeur aléatoire entre 0 et 1
            Random random = new Random();
            double randomValue = random.NextDouble();

            // Vérifier si un critique se produit
            return randomValue < finalCritRate;
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
                    string[] fireWeakness = ["dragon", "water", "fire", "rock"];
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
                    string[] normalNotEffective = ["ghost"];
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
    
        public static bool NeedStackDamages(PokemonTeam defenser)
        {
            if(defenser.WaitingMove != null)
            {
                if (defenser.WaitingMove.NameFr == "Patience") return true;
            }

            return false;
        }

        private static int SpecialCaseDamages(int damages, PokemonTeamMove move, PokemonTeam attacker, PokemonTeam defenser, TurnContext turnContext)
        {
            switch (move.NameFr)
            {
                case "Riposte":
                    if (attacker.BlowsTakenType == "physical") return attacker.BlowsTaken * 2;
                    else
                    {
                        turnContext.AddMessage("Mais cela michou");
                        return 0;
                    }
                    break;
            }
            return damages;
        }
    }
}
