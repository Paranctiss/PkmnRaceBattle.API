using MongoDB.Driver;
using PkmnRaceBattle.API.Hub;
using PkmnRaceBattle.Domain.Models.PlayerMongo;

namespace PkmnRaceBattle.API.Helpers.MoveManager.Fights
{
    public static class FightPerformMove
    {
        public static PokemonTeam[] PerformMove(
            PokemonTeam attacker, 
            PokemonTeam defenser, 
            PokemonTeamMove usedMove, 
            string fieldChange, 
            TurnContext turnContext, 
            bool playerAttacking, 
            PlayerMongo player = null)
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
                    attacker = FightAilmentMove.SufferConfusion(attacker, fieldChange, turnContext);
                }
                if (isPlaying)
                {
                    if (IsSucceedHisMove(attacker, defenser, usedMove)) {
                        if(player != null) player = MoveSpecialCasePlayer(usedMove, attacker, player, turnContext);

                        if (IsWaitingMove(usedMove) && attacker.WaitingMove == null)
                        {
                            attacker = PrepareWaitingAtk(attacker, usedMove, turnContext);
                        }
                        if(IsWaitingTurnTriggering(attacker, defenser, turnContext) || attacker.WaitingMove == null){
                            usedMove = IsSpecialCaseMove(usedMove, attacker, defenser, turnContext);
                            attacker = IsSpecialCasePokemonAttacker(usedMove, attacker, defenser, turnContext, playerAttacking);
                            defenser = IsSpecialCasePokemonDefenser(usedMove, defenser, turnContext);
                            if (attacker.WaitingMoveTurns == 0)
                            {
                                if(attacker.WaitingMove.NameFr == "Mania" || attacker.WaitingMove.NameFr == "Danse Fleurs")
                                {
                                    Random rnde = new Random();
                                    attacker.IsConfused = rnde.Next(1, 5);
                                    turnContext.AddMessage("Cela rend " + attacker.NameFr + " confus");
                                }
                                attacker.WaitingMove = null;
                                attacker.WaitingMoveTurns = null;
                            }



                            int nbHits = 1;
                            int nbDamageHit = 0;
                            if (usedMove.MinHits != null && usedMove.MaxHits != null)
                            {
                                Random rnd = new Random();
                                nbHits = rnd.Next((int)usedMove.MinHits, (int)usedMove.MaxHits + 1);
                            }

                            
                            if (usedMove.MinTurns != null && usedMove.MaxTurns != null) {
                                PrepareMultiTurnAtk(attacker, defenser, usedMove, turnContext);
                            }

                            //Inflige des dégâts ?
                            if (usedMove.Category.Contains("damage"))
                            {
                                if (nbHits > 1)
                                {


                                    for (int i = 0; i < nbHits; i++)
                                    {
                                        int oldDefHp = defenser.CurrHp;
                                        PokemonTeam[] response = FightDamageMove.PerformDamageMove(attacker, defenser, usedMove, fieldChange, turnContext);



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
                                    PokemonTeam[] response = FightDamageMove.PerformDamageMove(attacker, defenser, usedMove, fieldChange, turnContext);
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
                                nbDamageHit = nbHits;

                            }
                            else
                            {
                                nbDamageHit = 0;
                            }

                            //Applique un état ?
                            if (usedMove.Category.Contains("ailment") || usedMove.Category.Contains("swagger"))
                            {
                                for (int i = 0; i < nbHits; i++)
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
                                if (usedMove.StatChance != 0)
                                {
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
                                        attacker = FightStatusMove.PerformStatusMove(attacker, usedMove, turnContext, playerAttacking, usedMove.Target, fieldChange);
                                    }
                                    else //sur l'adversaire
                                    {
                                        defenser = FightStatusMove.PerformStatusMove(defenser, usedMove, turnContext, playerAttacking, usedMove.Target, fieldChange);
                                    }
                                }
                            }
                        
                            //Healing ? 
                            if(usedMove.Healing > 0)
                            {
                                int oldAtkHp = attacker.CurrHp;
                                double hpChange = attacker.BaseHp * (usedMove.Healing / 100.0);

                                attacker.CurrHp += (int)hpChange;

                                if (attacker.CurrHp > attacker.BaseHp)
                                {
                                    attacker.CurrHp = attacker.BaseHp;
                                }
                                if (playerAttacking) turnContext.Player.Hp.Add(oldAtkHp - attacker.CurrHp);
                                if (!playerAttacking) turnContext.Opponent.Hp.Add(oldAtkHp - attacker.CurrHp);
                                turnContext.AddMessage(attacker.NameFr + " récupère des PV");
                            }
                            //Drain ?
                            if(usedMove.Drain != 0)
                            {
                                if (IsDrainSpecialCaseMove(usedMove))
                                {
                                    PokemonTeam[] response = DrainSpecialCaseMove(usedMove, attacker, defenser, turnContext, playerAttacking);
                                    attacker = response[0];
                                    defenser = response[1];
                                }
                                else
                                {
                                    int oldAtkHp = attacker.CurrHp;
                                    double hpChange = attacker.BaseHp * (usedMove.Drain / 100.0);

                                    attacker.CurrHp += (int)hpChange;

                                    if (attacker.CurrHp > attacker.BaseHp)
                                    {
                                        attacker.CurrHp = attacker.BaseHp;
                                    }

                                    if (playerAttacking) turnContext.Player.Hp.Add(oldAtkHp - attacker.CurrHp);
                                    if (!playerAttacking) turnContext.Opponent.Hp.Add(oldAtkHp - attacker.CurrHp);
                                }
                            }

                            defenser = SpecialCaseHits(nbDamageHit, defenser, playerAttacking, turnContext, fieldChange);

                        }
                        else
                        {
                            if(attacker.WaitingMoveTurns == 0)
                            {
                                if(attacker.WaitingMove.NameFr == "Ultralaser")
                                {
                                    turnContext.AddPrioMessage(attacker.NameFr + " doit se reposer");
                                }
                                attacker.WaitingMove = null;
                                attacker.WaitingMoveTurns = null;
                            }
                        }
                       
                    }
                    else
                    {
                        turnContext.AddMessage(attacker.NameFr + " rate son attaque.");
                        attacker = SpecialCaseMissMove(attacker, usedMove, turnContext);
                    }
                    
                }
            }


            return [attacker, defenser];
        }

        private static PokemonTeam SpecialCaseHits(int nbHits, PokemonTeam defenser, bool playerAttacking, TurnContext turnContext, string fieldChange)
        {
            foreach (string specialCase in defenser.SpecialCases)
            {
                switch (specialCase)
                {
                    case "Frénésie":
                        if(nbHits > 0)
                        {
                            MoveStatsChanges[] changes = [new MoveStatsChanges { Name = "attack", Changes = nbHits }];
                            PokemonTeamMove move = new();
                            move.StatsChanges = changes;
                            return FightStatusMove.PerformStatusMove(defenser, move, turnContext, playerAttacking, "user", fieldChange);
                        }
                        break;
                }
            }
            return defenser;
        }

        private static bool IsDrainSpecialCaseMove(PokemonTeamMove usedMove)
        {
            string[] drainSpecialCaseMoves = ["Bélier", "Damoclès", "Sacrifice"];

            if (drainSpecialCaseMoves.Contains(usedMove.NameFr)) return true;
            return false;
        }

        private static PokemonTeam[] DrainSpecialCaseMove(PokemonTeamMove usedMove, PokemonTeam attacker, PokemonTeam defenser, TurnContext turnContext, bool playerAttacking)
        {
            switch (usedMove.NameFr)
            {
                case "Bélier":
                case "Damoclès":
                case "Sacrifice":
                    int oldHp = attacker.CurrHp;
                    double hpChange = attacker.BaseHp * (usedMove.Drain / 100.0);

                    attacker.CurrHp += (int)hpChange;

                    if (attacker.CurrHp > attacker.BaseHp)
                    {
                        attacker.CurrHp = attacker.BaseHp;
                    }
                    if (playerAttacking) turnContext.Player.Hp.Add(oldHp - attacker.CurrHp); 
                    if (!playerAttacking) turnContext.Opponent.Hp.Add(oldHp - attacker.CurrHp); 
                    turnContext.AddMessage(attacker.NameFr + "se blesse");
                    break;
            }
            return [attacker, defenser];
        }

        public static PokemonTeam SpecialCaseMissMove(PokemonTeam attacker, PokemonTeamMove usedMove, TurnContext turnContext)
        {
            switch (usedMove.NameFr)
            {
                case "Pied Sauté":
                case "Pied Voltige":
                    attacker.CurrHp -= attacker.BaseHp / 2;
                    if (attacker.CurrHp < 0) attacker.CurrHp = 0;
                    turnContext.AddMessage(attacker.NameFr + " se blesse tout seul");
                    break;
            }

            return attacker;
        }

        private static PlayerMongo MoveSpecialCasePlayer(PokemonTeamMove usedMove, PokemonTeam attacker, PlayerMongo player, TurnContext turnContext)
        {
            switch (usedMove.NameFr)
            {
                case "Jackpot":
                    player.Jackpot += attacker.Level * 5;
                    turnContext.AddMessage(attacker.NameFr + " empoche " + attacker.Level * 5 + " Pokedollz de plus");
                    break;
            }

            return player;
        }

        public static bool IsSucceedHisMove(PokemonTeam attacker, PokemonTeam defenser, PokemonTeamMove move)
        {
            if(move.Accuracy != null)
            {
                int accuracy = (int)move.Accuracy;
                double multiplicator = 1.0;
                if(move.NameFr == "Guillotine" || move.NameFr == "Empal’Korne" || move.NameFr == "Abîme")
                {
                    accuracy = attacker.Level - defenser.Level + (int)move.Accuracy;
                }
                else
                {
                    multiplicator = GetAccuracyMultiplier(attacker.AccuracyChanges) / GetAccuracyMultiplier(defenser.EvasionChanges);
                }
                

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

        public static bool IsWaitingMove(PokemonTeamMove move)
        {
            string[] waitingTurnsMoves = [
                "Coupe-Vent", 
                "Vol", 
                "Tunnel", 
                "Coud’Krâne", 
                "Patience", 
                "Piqué", 
                "Mania", 
                "Danse Fleurs", 
                "Ultralaser",
                "Lance-Soleil"
                ];

            if (waitingTurnsMoves.Contains(move.NameFr)) return true;
            return false;
        }

        public static PokemonTeamMove IsSpecialCaseMove(PokemonTeamMove usedMove, PokemonTeam attacker, PokemonTeam defenser, TurnContext turnContext)
        {

            PokemonTeamMove move = usedMove;


            switch (usedMove.NameFr)
            {
                case "Coud’Krâne":
                    if(attacker.WaitingMoveTurns == 1)
                    {
                        move.StatsChanges = [new MoveStatsChanges{Changes=1, Name="defense" }];
                        move.Category = "net-good-stats";
                        move.DamageType = "status";
                        move.Target = "user";
                        return move;
                    }
                    break;
                case "Patience":
                    if(attacker.WaitingMoveTurns == 0)
                    {
                        move.BrutDamages = attacker.BlowsTaken *2;
                        return move;
                    }
                    break;
                case "Sonic Boom":
                    move.BrutDamages = 20;
                    break;
                case "Draco-Rage":
                    move.BrutDamages = 40;
                    break;
                case "Croc Fatal":
                    move.BrutDamages = defenser.CurrHp / 2;
                    break;
                case "Vague Psy":
                    Random rnd = new Random();
                    int random = rnd.Next(1, 11);
                    move.BrutDamages = attacker.Level * (random + 5) / 10;
                    break;
                case "Balayage":
                    if (defenser.Weight == null)
                    {
                        move.Power = 20;
                        break;
                    }
                    if (defenser.Weight < 10)
                    {
                        move.Power = 20;
                        break;
                    }
                    if (defenser.Weight < 25)
                    {
                        move.Power = 40;
                        break;
                    }
                    if (defenser.Weight < 50)
                    {
                        move.Power = 60;
                        break;
                    }
                    if (defenser.Weight < 100)
                    {
                        move.Power = 80;
                        break;
                    }
                    if (defenser.Weight < 200)
                    {
                        move.Power = 100;
                        break;
                    }
                    if (defenser.Weight > 200)
                    {
                        move.Power = 120;
                        break;
                    }
                    break;
                case "Frappe Atlas":
                case "Ombre Nocturne":
                    move.Power = attacker.Level;
                    break;
                case "Buée Noire":
                    ResetStats(attacker);
                    ResetStats(defenser);
                    turnContext.AddMessage("Les statistiques des Pokémons sur le terrain est réinitialisé");
                    break;
                case "Triplattaque":
                    Random rand = new Random();
                    int rndV = rand.Next(1, 10001);
                    if (rndV <= 667)
                    {
                        move.Ailment = "burn";
                        move.AilmentChance = 0;
                    }
                    else
                    {
                        rndV = rand.Next(1, 10001);
                        if (rndV <= 667)
                        {
                            move.Ailment = "freeze";
                            move.AilmentChance = 0;
                        }
                        else
                        {
                            rndV = rand.Next(1, 10001);
                            if (rndV <= 667)
                            {
                                move.Ailment = "paralysis";
                                move.AilmentChance = 0;
                            }
                        }
                    }
                    break;
                default:
                    return usedMove;
                    break;
            }
            return usedMove;
        }

        public static PokemonTeam ResetStats(PokemonTeam pokemon)
        {
            pokemon.AtkChanges = 0;
            pokemon.AtkSpeChanges = 0;
            pokemon.AccuracyChanges = 0;
            pokemon.CritChanges = 0;
            pokemon.DefChanges = 0;
            pokemon.DefSpeChanges = 0;
            pokemon.EvasionChanges = 0;
            pokemon.SpeedChanges = 0;
            return pokemon;
        }

        public static PokemonTeam IsSpecialCasePokemonAttacker(PokemonTeamMove usedMove, PokemonTeam target, PokemonTeam defenser, TurnContext turnContext, bool playerAttacking)
        {

            switch (usedMove.NameFr) {
                case "Repos":
                        target.IsSleeping = 3;
                        target = FightAilmentMove.RemoveAllAilments(target, "sleep");
                        target.CurrHp = target.BaseHp;
                        return target;
                    break;
                case "Frénésie":
                    turnContext.AddMessage(target.NameFr + " entre en frénésie");
                    target.SpecialCases.Add("Frénésie");
                    return target;
                    break;
                case "Puissance":
                    turnContext.AddMessage("La chance de coups critiques de " + target.NameFr + " augmente");
                    target.CritChanges = 2;
                    break;
                case "Explosion":
                case "Destruction":
                    if (playerAttacking) turnContext.Player.Hp.Add(target.CurrHp);
                    if (!playerAttacking) turnContext.Opponent.Hp.Add(target.CurrHp);
                    target.CurrHp = 0;
                    break;
                case "Conversion":
                    string typeToAssign = target.Moves[0].Type;
                    target.ConvertedType = target.Types[0].Name;
                    target.Types[0].Name = typeToAssign;
                    turnContext.AddMessage(target.NameFr + " a désormais le type " + typeToAssign);
                    break;
                case "Morphing":
                    if(target.NameFr != "Métamorph")
                    {
                        PokemonTeam oldForm = (PokemonTeam)target.Clone();

                        target.UnmorphedForm = oldForm;
                        target.Atk = defenser.Atk;
                        target.AtkSpe = defenser.AtkSpe;
                        target.Def = defenser.Def;
                        target.DefSpe = defenser.DefSpe;
                        target.Speed = defenser.Speed;
                        target.FrontSprite = defenser.FrontSprite;
                        target.BackSprite = defenser.BackSprite;
                        target.Types = defenser.Types;
                        target.Moves = defenser.Moves;
                        foreach (PokemonTeamMove move in target.Moves)
                        {
                            move.Pp = 5;
                        }
                        target.Weight = defenser.Weight;
                        target.IsShiny = defenser.IsShiny;
                        target.UnmorphedForm.UnmorphedForm = null;
                        turnContext.AddMessage(target.NameFr + " prend la forme de " + defenser.NameFr);
                    }
                    else
                    {
                        turnContext.AddMessage("Mais cela michou");
                    }
                    break;
                case "Clonage":
                    if(target.Substitute == null)
                    {
                        if(target.CurrHp > target.BaseHp / 4)
                        {
                            target.CurrHp -= target.BaseHp / 4;
                            target.Substitute = target.CreateSubstitute(target.BaseHp / 4);
                            turnContext.AddMessage(target.NameFr + " invoque un clone de lui");
                        }
                        else
                        {
                            turnContext.AddMessage("Mais cela michou");
                        }
                        
                    }
                    else
                    {
                        turnContext.AddMessage("Mais cela michou");
                    }
                    break;

            }

            return target;
        }

        public static PokemonTeam IsSpecialCasePokemonDefenser(PokemonTeamMove usedMove, PokemonTeam target, TurnContext turnContext)
        {
            switch (usedMove.NameFr)
            {
                case "Guillotine":
                case "Empal’Korne":
                case "Abîme":
                    turnContext.AddMessage("K.O en un coup");
                    target.CurrHp = 0;
                    return target;
                    break;
                case "Cyclone":
                case "Hurlement":
                    target.SpecialCases.Add("Ejected");
                    break;
                case "Téléport":
                    target.SpecialCases.Add("Teleport");
                    break;
                case "Vampigraine":
                    if (target.Types.FirstOrDefault(x => x.Name == "grass") == null) target.SpecialCases.Add("Vampigraine");
                    else turnContext.AddMessage("Mais cela michou");
                    break;
            }

            return target;
        }

        public static PokemonTeam PrepareWaitingAtk(PokemonTeam attacker, PokemonTeamMove move, TurnContext turnContext)
        {

            attacker.WaitingMove = move;
            turnContext.AddPrioMessage(attacker.NameFr + " " + GetWaitingTurnFlavor(move));
            switch (move.NameFr)
            {
                case "Vol":
                    attacker.WaitingMoveTurns = 2;
                    attacker.Untargetable = "Vol";
                    break;
                case "Tunnel":
                    attacker.WaitingMoveTurns = 2;
                    attacker.Untargetable = "Tunnel";
                    break;
                case "Coupe-Vent":
                case "Coud’Krâne":
                case "Piqué":
                case "Ultralaser":
                case "Lance-Soleil":
                    attacker.WaitingMoveTurns = 2;
                    break;
                case "Patience":
                    attacker.WaitingMoveTurns = 3;
                    break;
                case "Mania":
                case "Danse Fleurs":
                    Random rnd = new Random();
                    attacker.WaitingMoveTurns = rnd.Next(2,4);
                    break;
                default:
                    attacker.WaitingMoveTurns = null;
                    break;
            }

            return attacker;
        }

        public static string GetWaitingTurnFlavor(PokemonTeamMove move)
        {
            switch (move.NameFr)
            {
                case "Vol":
                    return "s'envole";
                    break;
                case "Coupe-Vent":
                    return "se prépare à lancer une bourrasque";
                    break;
                case "Tunnel":
                    return "creuse un trou";
                    break;
                case "Coud’Krâne":
                    return "baisse la tête";
                    break;
                case "Patience":
                    return "se prépare à encaisser les coups";
                    break;
                case "Piqué":
                    return "brille";
                    break;
                case "Mania":
                case "Danse Fleurs":
                    return "entre en furie";
                case "Ultralaser":
                    return "lance un laser dévastateur";
                case "Lance-Soleil":
                    return "absorbe la lumière";
                case "Repos":
                    return "a récupéré en dormant";
                default:
                    return "jsp";
                    break;
            }
        }

        public static bool IsWaitingTurnTriggering(PokemonTeam attacker, PokemonTeam defenser, TurnContext turnContext)
        {
            if(attacker.WaitingMove == null) return true;
            if(attacker.WaitingMoveTurns != null) attacker.WaitingMoveTurns--;

            switch (attacker.WaitingMove.NameFr)
            {
                case "Vol":
                case "Tunnel":
                    if (attacker.WaitingMoveTurns == 0)
                    {
                        attacker.Untargetable = null;
                        return true;
                    }
                    break;
                case "Coupe-Vent":
                case "Patience":
                case "Piqué":
                case "Lance-Soleil":
                    if (attacker.WaitingMoveTurns == 0) return true;
                    break;
                case "Coud’Krâne":
                    if (attacker.WaitingMoveTurns == 0 || attacker.WaitingMoveTurns == 1) return true;
                    break;
                case "Mania":
                case "Danse Fleurs":
                    return true;
                    break;
                case "Ultralaser":
                    if (attacker.WaitingMoveTurns == 1) return true;
                    break;
                default:
                    return false;
                    break;
            }

            return false;
        }

        public static PokemonTeam[] PrepareMultiTurnAtk(PokemonTeam attacker, PokemonTeam defenser, PokemonTeamMove move, TurnContext turnContext)
        {
            Random rnd = new Random();
            
            switch (move.NameFr)
            {
                case "Étreinte":
                case "Ligotage":
                case "Danse Flammes":
                case "Claquoir":
                case "Entrave":
                    if(defenser.MultiTurnsMove == null)
                    {
                        defenser.MultiTurnsMove = move;
                        defenser.MultiTurnsMoveCount = rnd.Next((int)move.MinTurns, (int)move.MaxTurns + 1);
                        turnContext.AddMessage(defenser.NameFr + " " + GetMultiTurnFlavor(move));
                    }
                    break;
            }

            return [attacker, defenser];
        }

        public static string GetMultiTurnFlavor(PokemonTeamMove move)
        {
            switch (move.NameFr)
            {
                case "Étreinte":
                    return "est prit dans l'étreinte";
                case "Ligotage":
                    return "est prit dans le ligotage";
                case "Danse Flammes":
                    return "est piégé dans tourbillon";
                case "Claquoir":
                    return "est piégé dans une coquille";
                case "Entrave":
                    return "a sa dernière capacité sous entrave";
                default:
                    return "jsp";
                    break;
            }
        }

        public static PokemonTeam[] PerformMultiTurnMove(PokemonTeam attacker, PokemonTeam defenser, PokemonTeamMove move, TurnContext turnContext)
        {
            switch (move.NameFr)
            {
                case "Étreinte":
                    defenser.CurrHp -= defenser.BaseHp / 8;
                    turnContext.AddPrioMessage(defenser.NameFr + " est blessé par l'étreinte");
                    break;
                case "Ligotage":
                    defenser.CurrHp -= defenser.BaseHp / 8;
                    turnContext.AddPrioMessage(defenser.NameFr + " est blessé par ligotage");
                    break;
                case "Danse Flammes":
                    defenser.CurrHp -= defenser.BaseHp / 8;
                    turnContext.AddPrioMessage(defenser.NameFr + " est piégé dans les flammes");
                    break;
                case "Claquoir":
                    defenser.CurrHp -= defenser.BaseHp / 8;
                    turnContext.AddPrioMessage(defenser.NameFr + " est piégé dans la coquille");
                    break;
            }
            return [attacker, defenser];
        }

        public static PokemonTeam[] PerformSpecialCaseMove(PokemonTeam attacker, PokemonTeam defenser, PokemonTeamMove usedMove, TurnContext turnContext, PokemonTeamMove opponentUsedMove = null)
        {

            if (opponentUsedMove != null && usedMove.NameFr == "Entrave") {
                defenser.CantUseMoves.Add(opponentUsedMove.NameFr);
            }
            if(usedMove.NameFr == "Ultralaser" && attacker.WaitingMoveTurns == 0)
            {
                turnContext.AddPrioMessage(attacker.NameFr + " doit de reposer");
            }

            return [attacker, defenser];
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

        public static bool SpecialCaseFail(PokemonTeam attacker, PokemonTeamMove usedMove, PokemonTeam defenser)
        {
            switch (usedMove.NameFr)
            {
                case "Entrave":
                    if (defenser.CantUseMoves.Count > 0) 
                    {
                        return true;
                    }
                    break;
                case "Dévorêve":
                    if(defenser.IsSleeping == 0)
                    {
                        return true;
                    }
                    break;
                default:
                    return false;
            }
            return false;
        }

        public static bool IsFieldChangeMove(PokemonTeamMove usedMove)
        {
            string[] fieldChangeMoves = ["Brume", "Mur Lumière", "Protection"];

            if (fieldChangeMoves.Contains(usedMove.NameFr))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public static PlayerMongo FieldChangeMove(PokemonTeamMove usedMove, PlayerMongo player, TurnContext turnContext)
        {
            if (player.FieldChange == null)
            {
                switch (usedMove.NameFr)
                {
                    case "Brume":
                        turnContext.AddMessage("Une Brume enveloppe l'équipe");
                        player.FieldChange = "Brume";
                        player.FieldChangeCount = 5;
                        break;
                    case "Mur Lumière":
                        turnContext.AddMessage("Un mur de lumière entoure l'équipe");
                        player.FieldChange = "Mur Lumière";
                        player.FieldChangeCount = 5;
                        break;
                    case "Protection":
                        turnContext.AddMessage("Une protection entoure l'équipe");
                        player.FieldChange = "Protection";
                        player.FieldChangeCount = 5;
                        break;
                    default:
                        return player;
                }
            }
            else
            {
                turnContext.AddMessage("Mais cela michou");
            }
            return player;
        }

        public static PokemonTeam FieldChangeMove(PokemonTeamMove usedMove, PokemonTeam player, TurnContext turnContext)
        {
            if(player.FieldChange == null)
            {
                switch (usedMove.NameFr)
                {
                    case "Brume":
                        turnContext.AddMessage("Une Brume enveloppe l'équipe");
                        player.FieldChange = "Brume";
                        player.FieldChangeCount = 5;
                        break;
                    case "Mur Lumière":
                        turnContext.AddMessage("Un mur de lumière entoure l'équipe");
                        player.FieldChange = "Mur Lumière";
                        player.FieldChangeCount = 5;
                        break;
                    case "Protection":
                        turnContext.AddMessage("Une protection entoure l'équipe");
                        player.FieldChange = "Protection";
                        player.FieldChangeCount = 5;
                        break;
                    default:
                        return player;
                }
            }
            else
            {
                turnContext.AddMessage("Mais cela michou");
            }

            return player;
        }
    }
}
