namespace PkmnRaceBattle.API.Hub
{
    using Microsoft.AspNet.SignalR.Messaging;
    using Microsoft.AspNet.SignalR.Tracing;
    using Microsoft.AspNetCore.Components.Web;
    using Microsoft.AspNetCore.SignalR;
    using Microsoft.Extensions.Primitives;
    using PkmnRaceBattle.API.Helper;
    using PkmnRaceBattle.API.Helpers.MoveManager;
    using PkmnRaceBattle.API.Helpers.MoveManager.Fights;
    using PkmnRaceBattle.API.Helpers.PokemonGeneration;
    using PkmnRaceBattle.API.Helpers.StatsCalculator;
    using PkmnRaceBattle.API.Helpers.TrainerGeneration;
    using PkmnRaceBattle.Application.Contracts;
    using PkmnRaceBattle.Domain.Models;
    using PkmnRaceBattle.Domain.Models.PlayerMongo;
    using PkmnRaceBattle.Domain.Models.PokemonMongo;
    using PkmnRaceBattle.Domain.Models.RoomMongo;
    using PkmnRaceBattle.Persistence.ExternalAPI;
    using PkmnRaceBattle.Persistence.Helpers;
    using System;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Net.Sockets;
    using System.Numerics;
    using System.Runtime.ExceptionServices;

    public class GameHub : Hub
    {
        private readonly IMongoRoomRepository _mongoRoomRepository;
        private readonly IMongoPlayerRepository _mongoPlayerRepository;
        private readonly IMongoPokemonRepository _mongoPokemonRepository;
        private readonly IMongoWildPokemonRepository _mongoWildPokemonRepository;
        private readonly IMongoMoveRepository _mongoMoveRepository;

        public GameHub(
            IMongoRoomRepository mongoRoomRepository,
            IMongoMoveRepository mongoMoveRepository,
            IMongoPlayerRepository mongoPlayerRepository,
            IMongoPokemonRepository mongoPokemonRepository,
            IMongoWildPokemonRepository mongoWildPokemonRepository)
        {
            _mongoRoomRepository = mongoRoomRepository;
            _mongoPlayerRepository = mongoPlayerRepository;
            _mongoPokemonRepository = mongoPokemonRepository;
            _mongoWildPokemonRepository = mongoWildPokemonRepository;
            _mongoMoveRepository = mongoMoveRepository;
        }


        public async Task JoinGame(string username, int starterId, string trainerSprite, string gameCode)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, gameCode);

            PokemonMongo starterInfos = await _mongoPokemonRepository.GetPokemonMongoById(starterId);
            PokemonTeam pokemonTeam = GenerateNewPokemon.GenerateNewPokemonTeam(starterInfos, 5, 5);

            PlayerMongo playerMongo = new PlayerMongo();
            playerMongo.Name = username;
            playerMongo.RoomId = gameCode;
            playerMongo.Team = [pokemonTeam];
            playerMongo.IsHost = false;
            Random rnd = new Random();
            playerMongo.Sprite = trainerSprite;

            string id = await _mongoPlayerRepository.CreateAsync(playerMongo);

            UserConnectionManager.AddUserToRoom(id, gameCode, Context.ConnectionId);

            await Clients.Caller.SendAsync("JoinSuccess", gameCode, id);
            await Clients.Group(gameCode).SendAsync("UserJoined", gameCode);
        }

        public async Task LeaveGame(string groupName, string userId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
            UserConnectionManager.RemoveUserFromRoom(userId, groupName);
            await Clients.Group(groupName).SendAsync("UserLeft", userId);
        }

        public async Task CreateGame(string username, int starterId, string trainerSprite)
        {
            var gameCode = Guid.NewGuid().ToString().Substring(0, 6).ToUpper();

            PokemonMongo starterInfos = await _mongoPokemonRepository.GetPokemonMongoById(starterId);
            /*  PokemonMongo pkmn= await _mongoPokemonRepository.GetPokemonMongoById(133);
              PokemonMongo pkmn1 = await _mongoPokemonRepository.GetPokemonMongoById(133);
            PokemonMongo pkmn2 = await _mongoPokemonRepository.GetPokemonMongoById(133);
            PokemonMongo pkmn3 = await _mongoPokemonRepository.GetPokemonMongoById(150);
            PokemonMongo pkmn4 = await _mongoPokemonRepository.GetPokemonMongoById(149); */

             PokemonTeam pokemonTeam = GenerateNewPokemon.GenerateNewPokemonTeam(starterInfos, 5, 5);
            /*  PokemonTeam pkmnTeam = GenerateNewPokemon.GenerateNewPokemonTeam(pkmn, 15, 15);
                PokemonTeam pkmn1Team = GenerateNewPokemon.GenerateNewPokemonTeam(pkmn1, 15, 15);
               PokemonTeam pkmn2Team = GenerateNewPokemon.GenerateNewPokemonTeam(pkmn2, 15, 15);
              PokemonTeam pkmn3Team = GenerateNewPokemon.GenerateNewPokemonTeam(pkmn3, 15, 15);
              PokemonTeam pkmn4Team = GenerateNewPokemon.GenerateNewPokemonTeam(pkmn4, 15, 15); */

            PlayerMongo playerMongo = new PlayerMongo();
            playerMongo.Name = username;
            playerMongo.RoomId = gameCode;
            playerMongo.Team = [pokemonTeam]; 
            //playerMongo.Team = [pokemonTeam, pkmn1Team, pkmnTeam, pkmn2Team]; 
            playerMongo.IsHost = true;
            Random rnd = new Random();
            playerMongo.Sprite = trainerSprite;

            string id = await _mongoPlayerRepository.CreateAsync(playerMongo);

            await Groups.AddToGroupAsync(Context.ConnectionId, gameCode);
            await _mongoRoomRepository.CreateAsync(new RoomMongo
            {
                hostUserId = id,
                state = 0,
                roomId = gameCode
            });

            UserConnectionManager.AddUserToRoom(id, gameCode, Context.ConnectionId);

            // Informez le client de la création de la salle de jeu et du code de la salle
            await Clients.Caller.SendAsync("GameCreated", gameCode, id);
        }

        public async Task GetPlayersInRoom(string gameCode)
        {
            List<PlayerMongo> players = await _mongoPlayerRepository.GetByRoomId(gameCode);
            await Clients.Caller.SendAsync("ResponsePlayersInRoom", players);
        }

        public async Task GetPlayer(string userId)
        {
            PlayerMongo playerMongo = await _mongoPlayerRepository.GetByPlayerIdAsync(userId);
            await Clients.Caller.SendAsync("GetPlayerResponse", playerMongo);
        }

        public async Task StartGame(string gameCode)
        {
            RoomMongo room = await _mongoRoomRepository.GetByRoomIdAsync(gameCode);
            room.state = 1;
            await _mongoRoomRepository.UpdateAsync(gameCode, room);

            await Clients.Group(gameCode).SendAsync("GameStarted", gameCode);
        }

        static bool first = true;
        public async Task GetNewTurn(string userId)
        {
            string[] turnTypes = ["WildFight", "TrainerFight", "PokeCenter", "PokeShop"];
            Random rnd = new Random();
            string turnType = turnTypes[turnTypes.Length - 1];

            int random = rnd.Next(1, 101);



            if(random > 30)
            {
                turnType = "WildFight";
            }
            if (random > 20 && random <= 30) 
            {
                turnType = "PokeShop";
            }
            if(random > 10 && random <= 20)
            {
                turnType = "PokeCenter";
            }
            if(random <= 10)
            {
                turnType = "TrainerFight";
            }


            switch (turnType) {

                case "WildFight":
                    await GetWildFight(userId);
                    break;
                case "TrainerFight":
                    await GetTrainerFight(userId);
                    break;
                case "PokeCenter":
                    await GetPokeCenter(userId);
                    break;
                case "PokeShop":
                    await GetPokeShop(userId);
                    break;
            }
        }

        public async Task GetPokeCenter(string userId)
        {
            PlayerMongo player = await _mongoPlayerRepository.GetByPlayerIdAsync(userId);

            await Clients.Caller.SendAsync("responsePokeCenter", player);
        }

        public async Task UsePokeCenter(string userId)
        {
            PlayerMongo player = await _mongoPlayerRepository.GetByPlayerIdAsync(userId);

            for (int i = 0; i < player.Team.Length; i++)
            {
                player.Team[i].CurrHp = player.Team[i].BaseHp;
                player.Team[i].IsBurning = false;
                player.Team[i].IsParalyzed = false;
                player.Team[i].IsPoisoned = 0;
                player.Team[i].IsSleeping = 0;
                player.Team[i].IsFrozen = false;
            }

            await _mongoPlayerRepository.UpdateAsync(player);

            await Clients.Caller.SendAsync("healedPokeCenter", player);
        }

        public async Task GetPokeShop(string userId)
        {
            await Clients.Caller.SendAsync("responsePokeShop");
        }

        public async Task BuyItem(string userId, string itemName)
        {

            PlayerMongo player = await _mongoPlayerRepository.GetByPlayerIdAsync(userId);

            BagItem boughtItem = player.Items.FirstOrDefault(x => x.Name == itemName);

            if(player.Credits >= boughtItem.Price)
            {
                player.Items.FirstOrDefault(x => x.Name == itemName).Number++;
                player.Credits -= boughtItem.Price;
                await _mongoPlayerRepository.UpdateAsync(player);
            }
            await Clients.Caller.SendAsync("onBuyItemResponse", itemName, player);
            
        }

        public async Task TrainerSendNextPokemon(PlayerMongo player, PlayerMongo trainer)
        {
            // Récupère le premier Pokémon dont les HP sont > 0 et qui n'est pas à l'index 0
            PokemonTeam alivePokemon = trainer.Team.Where((x, index) => x.CurrHp > 0 && index != 0).FirstOrDefault();

            if (alivePokemon != null) {
               int indexAlive = Array.IndexOf(trainer.Team, alivePokemon);
                PokemonTeam deadPokemon = trainer.Team[0];
                trainer.Team[0] = alivePokemon;
                trainer.Team[indexAlive] = deadPokemon;

                await _mongoWildPokemonRepository.UpdateAsync(trainer);

                TurnContext turnContext = new TurnContext();
                turnContext.AddPrioMessage(trainer.Name + " envoie " + trainer.Team[0].NameFr);
                await Clients.Caller.SendAsync("useMoveResult", turnContext);
                await Task.Delay(1000);
                turnContext = new();
                await Clients.Caller.SendAsync("onTrainerSwitchPokemon", trainer);
            }
        }

        public async Task GetTrainerFight(string userId)
        {
            PlayerMongo player = await _mongoPlayerRepository.GetByPlayerIdAsync(userId);
            int levelAvg = player.GetAverageLevel();

            PlayerMongo trainer = await GenerateNewTrainer.GenerateNewTrainerTeam(3, levelAvg, _mongoPokemonRepository);

            await _mongoWildPokemonRepository.CreateAsync(trainer);
            await Clients.Caller.SendAsync("responseTrainerFight", trainer);
        }

        public async Task GetWildFight(string userId)
        {
            PlayerMongo player = await _mongoPlayerRepository.GetByPlayerIdAsync(userId);
            int levelAvg = player.GetAverageLevel();
            PokemonMongo rndPokemon = await _mongoPokemonRepository.GetRandom();
            //PokemonMongo rndPokemon = await _mongoPokemonRepository.GetPokemonMongoById(122);

            PokemonTeam wildPokemon = GenerateNewPokemon.GenerateNewPokemonTeam(rndPokemon, levelAvg-3, levelAvg-2);
            PlayerMongo wildOpponent = new PlayerMongo();
            wildOpponent.GenerateWild();
            wildOpponent.Team = [wildPokemon];
            await _mongoWildPokemonRepository.CreateAsync(wildOpponent);
            await Clients.Caller.SendAsync("responseWildFight", wildOpponent);
        }

        public async Task AddPokemonToTeam(string userId, string wildOpponentId, int index)
        {
            PlayerMongo player = await _mongoPlayerRepository.GetByPlayerIdAsync(userId);
            PlayerMongo opponent = await _mongoWildPokemonRepository.GetByIdAsync(wildOpponentId);
            PokemonTeam wildPokemon = await _mongoWildPokemonRepository.GetPlayerPokemonById(opponent._id, opponent.Team[0].Id);
            if (index == -1) {
                PokemonTeam[] newTeam = new PokemonTeam[player.Team.Length + 1];
                Array.Copy(player.Team, newTeam, player.Team.Length);
                newTeam[newTeam.Length - 1] = wildPokemon;
                player.Team = newTeam;
            }
            else
            {
                player.Team[index] = wildPokemon;
            }

            await _mongoPlayerRepository.UpdateAsync(player);
            await FinishFight(userId, opponent._id);

        }

        public async Task ReplacePokemon(string userId, string pokemonId, string wildOpponentId)
        {
            PlayerMongo player = await _mongoPlayerRepository.GetByPlayerIdAsync(userId);
            PlayerMongo wildPlayer = await _mongoWildPokemonRepository.GetByIdAsync(wildOpponentId);

            PokemonTeam pokemon = player.Team.FirstOrDefault(x => x.Id == pokemonId);

            if (player.Team[0].MultiTurnsMove != null && (player.Team[0].MultiTurnsMove.NameFr == "Ligotage" || player.Team[0].MultiTurnsMove.NameFr == "Étreinte") && player.Team[0].CurrHp > 0)
            {
                TurnContext turnContext = new TurnContext();
                if(player.Team[0].MultiTurnsMove.NameFr == "Ligotage") turnContext.AddMessage("Un Pokémon sous Ligotage ne peut pas être remplacé");
                if(player.Team[0].MultiTurnsMove.NameFr == "Étreinte") turnContext.AddMessage("Un Pokémon sous Ligotage ne peut pas être remplacé");
                await Clients.Caller.SendAsync("useMoveResult", turnContext);
                await Clients.Caller.SendAsync("turnFinished", player, wildPlayer);
                return;
            }

            pokemon.SpecialCases = new();
            pokemon.MultiTurnsMoveCount = null;
            pokemon.MultiTurnsMove = null;
            pokemon.CantUseMoves = new();
            pokemon.WaitingMove = null;
            pokemon.WaitingMoveTurns = null;
            pokemon.Untargetable = null;
            pokemon.BlowsTaken = 0;
            pokemon.BlowsTakenType = null;
            pokemon.IsConfused = 0;
            pokemon.CritChanges = 0;
            pokemon.AtkChanges = 0;
            pokemon.AtkSpeChanges = 0;
            pokemon.DefChanges = 0;
            pokemon.DefSpeChanges = 0;
            pokemon.SpeedChanges = 0;
            pokemon.CritChanges = 0;
            pokemon.EvasionChanges = 0;
            pokemon.AccuracyChanges = 0;

            if (pokemon.ConvertedType != null)
            {
                pokemon.Types[0].Name = pokemon.ConvertedType;
                pokemon.ConvertedType = null;
            }
            if(pokemon.UnmorphedForm != null)
            {
                pokemon = pokemon.UnmorphedForm;
                pokemon.UnmorphedForm = null;
            }
            if (pokemon.SavedMove != null)
            {
                pokemon.Moves[(int)pokemon.SavedMoveSlot] = pokemon.SavedMove;
                pokemon.SavedMove = null;
                pokemon.SavedMoveSlot = null;
            }
            PokemonTeam pokemonSwap = player.Team[0];

            // Échanger les positions
            int indexOfPokemon = Array.IndexOf(player.Team, pokemon);
            
            player.Team[0] = pokemon;
            player.Team[indexOfPokemon] = pokemonSwap;


            // Sauvegarder les modifications si nécessaire
            await _mongoPlayerRepository.UpdateAsync(player);

            if(pokemonSwap.CurrHp <= 0)
            {
                TurnContext turnContext = new TurnContext();
                turnContext.AddMessage(player.Name + " envoie " + pokemon.NameFr);
                await Clients.Caller.SendAsync("useMoveResult", turnContext);
                await Clients.Caller.SendAsync("turnFinished", player, wildPlayer);
            }
            else
            {
                await UseMove(userId, pokemon.Id, "swap:", wildOpponentId, wildPlayer.Team[0].Id, true);
            }

        }

        public async Task FinishFight(string userId, string wildOpponentId, bool unexpectedEnd = false)
        {
            PlayerMongo player = await _mongoPlayerRepository.GetByPlayerIdAsync(userId);
            PlayerMongo opponent = await _mongoWildPokemonRepository.GetByIdAsync(wildOpponentId);
            PokemonTeam wildPokemon = await _mongoWildPokemonRepository.GetPlayerPokemonById(opponent._id, opponent.Team[0].Id);
            bool lost = false;
            if (unexpectedEnd && opponent.IsTrainer) {
                if (player.Team.FirstOrDefault(x => x.SpecialCases.Contains("Ejected")) != null){
                    await Clients.Caller.SendAsync("playerPokemonDeath", "Changez de Pokémon");
                    return;
                }
                if (opponent.Team.FirstOrDefault(x => x.SpecialCases.Contains("Ejected")) != null)
                {
                    await TrainerSendNextPokemon(player, opponent);
                    return;
                }
            }

            if (player.Team.FirstOrDefault(t => t.CurrHp > 0) != null) { 
            
                for(int i = 0; i<=player.Team.Length-1; i++)
                {
                    PokemonTeam team = player.Team[i];
                    team.AtkChanges = 0;
                    team.AtkSpeChanges = 0;
                    team.DefChanges = 0;
                    team.DefSpeChanges = 0;
                    team.SpeedChanges = 0;
                    team.CritChanges = 0;
                    team.SpecialCases = new();
                    team.MultiTurnsMoveCount = null;
                    team.MultiTurnsMove = null;
                    team.CantUseMoves = new();
                    team.WaitingMove = null;
                    team.WaitingMoveTurns = null;
                    team.Untargetable = null;
                    team.BlowsTaken = 0;
                    team.BlowsTakenType = null;
                    team.IsConfused = 0;
                    if (team.ConvertedType != null)
                    {
                        team.Types[0].Name = team.ConvertedType;
                        team.ConvertedType = null;
                    }
                    if (team.UnmorphedForm != null)
                    {
                        team = team.UnmorphedForm;
                        team.UnmorphedForm = null;
                    }
                    if (team.SavedMove != null)
                    {
                        team.Moves[(int)team.SavedMoveSlot] = team.SavedMove;
                        team.SavedMove = null;
                        team.SavedMoveSlot = null;
                    }

                    if (team.HavePlayed && team.CurrHp > 0 && !unexpectedEnd)
                    {
                        team.CurrXP += PokemonExperienceCalculator.ExpGained(wildPokemon, opponent.IsTrainer, false, 0);
                        team.HavePlayed = false;

                        team = await CheckLevelUp(team);
                    }
                    player.Team[i] = team;
                }
                if (!unexpectedEnd)
                {
                    player.Credits += player.Jackpot;
                    player.Jackpot = 0;
                    if (opponent.IsTrainer && opponent.Team.FirstOrDefault(x => x.CurrHp > 0) == null) {
                        int earnedCredits = 2000;
                        player.Credits += earnedCredits;
                        TurnContext turnContext = new TurnContext();
                        turnContext.AddMessage("Vous avez battu " + opponent.Name);
                        turnContext.AddMessage("Vous remportez " + earnedCredits + " Pokédollz");
                        await Clients.Caller.SendAsync("useMoveResult", turnContext);
                        await Task.Delay(CalculateDelay(turnContext));
                    }
                }
           
            }
            else
            {
                for (int i = 0; i <= player.Team.Length-1; i++)
                {
                    PokemonTeam team = player.Team[i];
                    team.AtkChanges = 0;
                    team.AtkSpeChanges = 0;
                    team.DefChanges = 0;
                    team.DefSpeChanges = 0;
                    team.SpeedChanges = 0;
                    team.CritChanges = 0;
                    team.CurrHp = team.BaseHp;
                    team.SpecialCases = new();
                    team.MultiTurnsMoveCount = null;
                    team.MultiTurnsMove = null;
                    team.CantUseMoves = new();
                    team.WaitingMove = null;
                    team.WaitingMoveTurns = null;
                    team.Untargetable = null;
                    team.BlowsTaken = 0;
                    team.BlowsTakenType = null;
                    team.IsConfused = 0;
                    team.IsSleeping = 0;
                    team.IsBurning = false;
                    team.IsFrozen = false;
                    team.IsParalyzed = false;
                    team.IsPoisoned = 0;
                    team.PoisonCount = null;
                    team.HavePlayed = false;
                    if(team.ConvertedType != null)
                    {
                        team.Types[0].Name = team.ConvertedType;
                        team.ConvertedType = null;
                    }
                    if (team.UnmorphedForm != null)
                    {
                        team = team.UnmorphedForm;
                        team.UnmorphedForm = null;
                    }
                    if(team.SavedMove != null)
                    {
                        team.Moves[(int)team.SavedMoveSlot] = team.SavedMove;
                        team.SavedMove = null;
                        team.SavedMoveSlot = null;
                    }
                    player.Team[i] = team;
                }
                //Combat perdu go heal + diviser l'argent en 2
                int lostCredits = player.Credits / 2;
                player.Credits = lostCredits;
                lost = true;
                string message = "Vous n'avez plus de pokémon en forme, vous perdez "+ lostCredits +" pokédolz";
                await Clients.Caller.SendAsync("playerLooseFight", message);
                await Task.Delay(3000);
            }

            await this._mongoPlayerRepository.UpdateAsync(player);
            await this._mongoWildPokemonRepository.UpdateAsync(opponent);

            if (opponent.IsTrainer && opponent.Team.FirstOrDefault(x => x.CurrHp > 0) != null && !lost) {

                await TrainerSendNextPokemon(player, opponent);
            }
            else
            {
                await GetNewTurn(player._id);
            }
        }

        public async Task<PokemonTeam> CheckLevelUp(PokemonTeam team)
        {
            List<MoveMongo> movesToLearn = new List<MoveMongo>();
            bool evolvedThisTurn = false;
            string learnedMove = "";
            int oldLevel = team.Level;
            string oldPkmnName = "";
            while (PokemonExperienceCalculator.ExpToNextLevel(team) <= 0)
            {
                team.Level++;

                PokemonMongo pokemonBase;

                var possibleEvolutions = team.EvolutionDetails?.Where(evo => evo.MinLevel != null && evo.MinLevel <= team.Level).ToList();

                if (possibleEvolutions != null && possibleEvolutions.Count > 0)
                {
                    //A voir plus tard si différentes évolutions peuvent avoir lieu au même niveau 
                    var evolutionToUse = possibleEvolutions[0];

                    pokemonBase = await _mongoPokemonRepository.GetPokemonMongoByOGName(evolutionToUse.PokemonName);
                    PokemonTeam EvolvedPokemon = PokemonBaseToTeam.ConvertBaseToTeam(pokemonBase, team.Level, team.IsShiny);
                    EvolvedPokemon.Moves = team.Moves;
                    EvolvedPokemon.CurrHp = team.CurrHp + (EvolvedPokemon.BaseHp - team.BaseHp);
                    EvolvedPokemon.CurrXP = team.CurrXP;
                    oldPkmnName = team.NameFr;
                    team = EvolvedPokemon;
                    evolvedThisTurn = true;
                }
                else
                {
                    pokemonBase = await _mongoPokemonRepository.GetPokemonMongoById(team.IdDex);
                }
                
                if (pokemonBase != null && pokemonBase.Moves.FirstOrDefault(x => x.LearnedAtLvl == team.Level) != null)
                {
                    if (team.Moves.Length < 4)
                    {
                        PokemonTeamMove move = PokemonMoveSelector.ConvertToTeamMove(pokemonBase.Moves.FirstOrDefault(x => x.LearnedAtLvl == team.Level));

                        PokemonTeamMove[] newMoves = new PokemonTeamMove[team.Moves.Length + 1];
                        Array.Copy(team.Moves, newMoves, team.Moves.Length);
                        newMoves[newMoves.Length - 1] = move;
                        team.Moves = newMoves;
                        learnedMove = move.NameFr;

                    }
                    else
                    {
                        movesToLearn.Add(pokemonBase.Moves.FirstOrDefault(x => x.LearnedAtLvl == team.Level));
                    }
                }

                team.XpFromLastLvl = PokemonExperienceCalculator.ExpForLevel(team.Level, team.GrowthRate);
                team.XpForNextLvl = PokemonExperienceCalculator.ExpForLevel(team.Level + 1, team.GrowthRate);
                PokemonMongo basePokemon = await _mongoPokemonRepository.GetPokemonMongoById(team.IdDex);
                team = PokemonStatCalculator.CalculateAllStats(team, basePokemon);
            }
            if (oldLevel < team.Level)
            {
                string message;
                if (evolvedThisTurn) message = oldPkmnName + " monte niveau " + team.Level + "|" + oldPkmnName + " a évolué en " + team.NameFr;
                else message = team.NameFr + " monte niveau " + team.Level;

                if (learnedMove != "") message += "| " + oldPkmnName + " apprend " + learnedMove;

                await Clients.Caller.SendAsync("pokemonLevelUp", message, team, movesToLearn);
                int delay = 500;
                if (evolvedThisTurn) delay += 500;
                if (learnedMove != "") delay += 500;
                await Task.Delay(delay);
            }

            return team;
        }

        public async Task LearnMove(int oldMoveId, int newMoveId, string pokemonId, string userId)
        {
            PlayerMongo player = await _mongoPlayerRepository.GetByPlayerIdAsync(userId);
            
            PokemonTeam pokemon = player.Team.FirstOrDefault(x => x.Id == pokemonId);

            PokemonMongo pokemonBase = await _mongoPokemonRepository.GetPokemonMongoById(pokemon.IdDex);

            PokemonTeamMove moveToLearn = PokemonMoveSelector.ConvertToTeamMove(pokemonBase.Moves.FirstOrDefault(x => x.Id == newMoveId));

            int oldMoveIndex = Array.FindIndex(pokemon.Moves, x => x.Id == oldMoveId);
            pokemon.Moves[oldMoveIndex] = moveToLearn;

            PlayerMongo updatedPlayer = await _mongoPlayerRepository.UpdatePokemonTeamAsync(pokemon, player);
            await Clients.Caller.SendAsync("moveLearned", updatedPlayer);
        }

        //Player vs wildPokemon
        public async Task UseMove(string playerId, string playerPokemonId, string usedMoveName, string wildOpponentId, string wildPokemonId, bool isAttacking, int index=0, bool skipTurn = false)
        {
            TurnContext turnContext = new TurnContext();
            PlayerMongo player = await _mongoPlayerRepository.GetByPlayerIdAsync(playerId);
            PlayerMongo wildOpponentMongo = await _mongoWildPokemonRepository.GetByIdAsync(wildOpponentId);
            PokemonTeam playerPokemonMongo = await _mongoPlayerRepository.GetPlayerPokemonById(player._id, playerPokemonId);



            PokemonTeam wildPokemonMongo = await _mongoWildPokemonRepository.GetPlayerPokemonById(wildOpponentMongo._id, wildPokemonId);
            PokemonTeamMove usedMove;
            if (usedMoveName.StartsWith("item:"))
            {

                string[] itemProperties = usedMoveName.Split(':');
                string name = itemProperties[1];
                string type = itemProperties[2];

                usedMove = new PokemonTeamMove
                {
                    Accuracy = 100,
                    DamageType = type,
                    NameFr = name,
                    Power = 0,
                    Pp = 1,
                    Priority = 6,
                    StatsChanges = [],
                    Id = 0,
                    Type = "item",
                    Name = name,
                    Target = index.ToString()
                };
            }
            else
            {
                if (usedMoveName.StartsWith("swap:"))
                {
                    usedMove = new PokemonTeamMove
                    {
                        Accuracy = 100,
                        DamageType = "swap",
                        NameFr = "swap",
                        Power = 0,
                        Pp = 1,
                        Priority = 6,
                        StatsChanges = [],
                        Id = 0,
                        Type = "swap",
                        Name = "swap",
                        Target = "swap"
                    };
                }
                else
                {
                    if(playerPokemonMongo.WaitingMove != null)
                    {
                        usedMove = PokemonMoveSelector.ConvertToTeamMove(await _mongoMoveRepository.GetMoveMongoByName(playerPokemonMongo.WaitingMove.NameFr));
                    }
                    else
                    {
                        usedMove = await _mongoPlayerRepository.GetPokemonTeamMoveByName(playerId, playerPokemonId, usedMoveName);
                    }

                    //usedMove = PokemonMoveSelector.ConvertToTeamMove(await _mongoMoveRepository.GetMoveMongoByName("Furie"));
                }
            }



            PokemonTeamMove opponentMove;

            if (wildPokemonMongo.WaitingMove != null) 
            {
                opponentMove = PokemonMoveSelector.ConvertToTeamMove(await _mongoMoveRepository.GetMoveMongoByName(wildPokemonMongo.WaitingMove.NameFr));
            }
            else
            {
                opponentMove = AIChoseMove.GetARandomMove(wildPokemonMongo);
                //opponentMove = AIChoseMove.GetThatMove(wildPokemonMongo, "Bouclier");
            }

            PokemonTeam pkmnTeamToCheck;
            if (index != 0) pkmnTeamToCheck = player.Team[index];
            else pkmnTeamToCheck = playerPokemonMongo;
            if (!ValidatorMove.IsEverythingOk(player, pkmnTeamToCheck, usedMove, wildOpponentMongo, wildPokemonMongo, opponentMove, turnContext))
            {
                await Clients.Caller.SendAsync("useMoveResult", turnContext);
                await Clients.Caller.SendAsync("turnFinished", player, wildOpponentMongo);
                return;
            }

            playerPokemonMongo.HavePlayed = true;



            PokemonTeam playerPokemon;
            if(playerPokemonMongo.Substitute != null)
            {
                playerPokemon = (PokemonTeam)playerPokemonMongo.Substitute.Clone();
            }
            else
            {
                playerPokemon = (PokemonTeam)playerPokemonMongo.Clone();
            }

            PokemonTeam wildPokemon;
            if(wildPokemonMongo.Substitute != null)
            {
                wildPokemon = (PokemonTeam)wildPokemonMongo.Substitute.Clone();
            }
            else
            {
                wildPokemon = (PokemonTeam)wildPokemonMongo.Clone();
            }
            int catchValue = 0;

            playerPokemon = FightAilmentMove.TryRemoveAilment(playerPokemon, turnContext);
            wildPokemon = FightAilmentMove.TryRemoveAilment(wildPokemon, turnContext);

            if(FightPriority.IsPlayingFirst(playerPokemon, usedMove, wildPokemon, opponentMove))
            {//Joueur joue en premier

                if (usedMove.NameFr == "Métronome")
                {
                    turnContext.AddMessage(playerPokemon.NameFr + " lance Métronome");
                    PokemonTeamMove newMove = PokemonMoveSelector.ConvertToTeamMove(await GetMoveExtApi.GetMetronomeMove());
                    usedMove = newMove;
                    await Clients.Caller.SendAsync("useMoveResult", turnContext);
                    await Task.Delay(CalculateDelay(turnContext));
                    turnContext = new();
                }

                if (usedMove.Type == "item")
                {
                    player.Items.FirstOrDefault(i => i.Name == usedMove.NameFr).Number -= 1;
                    turnContext.AddPrioMessage(player.Name + " utilise " + usedMove.NameFr);
                }
                else if (usedMove.Type == "swap") turnContext.AddPrioMessage(player.Name + " change de Pokémon");
                else turnContext.AddPrioMessage(playerPokemon.NameFr + " lance " + usedMove.NameFr);

                if (usedMove.NameFr.EndsWith("ball"))
                {
                    await Clients.Caller.SendAsync("launchBall", usedMove.NameFr, turnContext);
                    catchValue = FightCatch.TryCatchPokemon(wildPokemon, usedMove.NameFr);
                    await Task.Delay(1000);
                    await Clients.Caller.SendAsync("catchResult", catchValue);
                    if(catchValue == -1)
                    {
                        await Task.Delay(1000);
                    }
                    else
                    {
                        await Task.Delay(1000*(catchValue+1));
                    }

                }
                else
                {
                    if(usedMove.NameFr == "swap")
                    {
                        await Clients.Caller.SendAsync("swapPokemon", playerPokemon, turnContext.PrioMessages[0]);
                        await Task.Delay(2000);
                    }
                    else
                    {
                        if (FightPriority.MoveMustBePlayedLast(usedMove) || FightPerformMove.SpecialCaseFail(playerPokemon, usedMove, wildPokemon))
                        {
                            turnContext.AddMessage("Mais cela michou");
                        }
                        else
                        {   
                            if(FightPerformMove.IsFieldChangeMove(usedMove)) player = FightPerformMove.FieldChangeMove(usedMove, player, turnContext);

                            if (index != 0) playerPokemon = player.Team[index];
                            PokemonTeam[] t1Result = FightPerformMove.PerformMove(playerPokemon, wildPokemon, usedMove, wildPokemon.FieldChange, turnContext, true, player);
                            playerPokemon = t1Result[0];
                            wildPokemon = t1Result[1];
                            PokemonTeam[] t1SpeCaseResult = FightPerformMove.PerformSpecialCaseMove(playerPokemon, wildPokemon, usedMove, turnContext);
                            playerPokemon = t1SpeCaseResult[0];
                            wildPokemon = t1SpeCaseResult[1];



                            await Clients.Caller.SendAsync("useMoveResult", turnContext);
                            await Task.Delay(CalculateDelay(turnContext) + 500);
                            turnContext = new();

                            if (usedMove.Type == "item" && usedMove.DamageType == "special")
                            {
                                string stoneLabel = FightUseItem.GetStoneLabel(usedMove.NameFr);
                                var possibleEvolutions = playerPokemon.EvolutionDetails?.Where(evo => evo.Item != null && evo.Item == stoneLabel).ToList();

                                if (possibleEvolutions != null && possibleEvolutions.Count > 0)
                                {
                                    //A voir plus tard si différentes évolutions peuvent avoir lieu au même niveau 
                                    var evolutionToUse = possibleEvolutions[0];

                                    PokemonMongo pokemonBase = await _mongoPokemonRepository.GetPokemonMongoByOGName(evolutionToUse.PokemonName);

                                    if (pokemonBase != null)
                                    {
                                        PokemonTeam EvolvedPokemon = PokemonBaseToTeam.ConvertBaseToTeam(pokemonBase, playerPokemon.Level, playerPokemon.IsShiny);
                                        EvolvedPokemon.Moves = playerPokemon.Moves;
                                        EvolvedPokemon.CurrHp = playerPokemon.CurrHp + (EvolvedPokemon.BaseHp - playerPokemon.BaseHp);
                                        EvolvedPokemon.CurrXP = playerPokemon.CurrXP;
                                        string oldPkmnName = playerPokemon.NameFr;
                                        playerPokemon = EvolvedPokemon;

                                        string message = oldPkmnName + " a évolué en " + playerPokemon.NameFr;

                                        await Clients.Caller.SendAsync("pokemonLevelUp", message, playerPokemon, new List<MoveMongo>());
                                        await Task.Delay(500);
                                    }

                                }
                            }

                            playerPokemon = await CheckLevelUp(playerPokemon);
                            player.Team[index] = playerPokemon;

                            if (index != 0)
                            {
                                await this._mongoPlayerRepository.UpdatePokemonTeamAsync(playerPokemon, player);
                                await Clients.Caller.SendAsync("useItemResult", turnContext, index);
                                playerPokemon = playerPokemonMongo;
                                await Task.Delay(CalculateDelay(turnContext) + 500);
                                turnContext = new();
                            }

                            if(playerPokemon.Substitute != null)
                            {
                                playerPokemonMongo = (PokemonTeam)playerPokemon.Clone();
                                playerPokemon = (PokemonTeam)playerPokemonMongo.Substitute.Clone();
                            }

                            if (await ManageSpecialCasesAfterMove(wildOpponentMongo, wildPokemon, playerPokemon, player, turnContext)) {
                                await _mongoPlayerRepository.UpdatePokemonTeamAsync(playerPokemon, player);
                                await _mongoWildPokemonRepository.UpdatePokemonTeamAsync(wildPokemon, wildOpponentMongo);
                                await Clients.Caller.SendAsync("useMoveResult", turnContext);
                                await Task.Delay(CalculateDelay(turnContext)+500);
                                await FinishFight(player._id, wildOpponentMongo._id, true);
                                return;
                            } 
                        }
                    }
                }
                


                if(catchValue == -1)//Pokémon capturé
                {
                    await Task.Delay(4000);
                    wildOpponentMongo.Team[0] = wildPokemon;
                    await Clients.Caller.SendAsync("caughtPokemon", wildOpponentMongo);
                }
                else
                {
                    turnContext = new();
                    if (wildPokemon.CurrHp <= 0)//Pokémon mort
                    {
                        turnContext.AddPrioMessage(wildPokemon.NameFr + " est K.O");

                        if (wildPokemon.Substitute != null)
                        {
                            wildPokemonMongo.Substitute = null;
                            wildPokemon = wildPokemonMongo;
                            await Clients.Caller.SendAsync("useMoveResult", turnContext);
                            await Task.Delay(CalculateDelay(turnContext));
                        }
                        else
                        {
                            await Clients.Caller.SendAsync("useMoveResult", turnContext);
                            await Task.Delay(CalculateDelay(turnContext));
                            await _mongoPlayerRepository.UpdatePokemonTeamAsync(playerPokemon, player);
                            await _mongoWildPokemonRepository.UpdatePokemonTeamAsync(wildPokemon, wildOpponentMongo);
                            await FinishFight(playerId, wildOpponentId);
                            return;
                        }
                    }
                    else
                    {
                        if (!skipTurn)
                        {
                            if (!wildPokemon.IsFlinched)
                            {
                                if (opponentMove.NameFr == "Métronome")
                                {
                                    turnContext.AddMessage(wildPokemon.NameFr + " lance Métronome");
                                    PokemonTeamMove newMove = PokemonMoveSelector.ConvertToTeamMove(await GetMoveExtApi.GetMetronomeMove());
                                    opponentMove = newMove;
                                    await Clients.Caller.SendAsync("useMoveResult", turnContext);
                                    await Task.Delay(CalculateDelay(turnContext));
                                    turnContext = new();
                                }
                                if (opponentMove.NameFr == "Mimique")
                                {
                                    turnContext.AddMessage(wildPokemon.NameFr + " lance Mimique");
                                    opponentMove = usedMove;
                                    await Clients.Caller.SendAsync("useMoveResult", turnContext);
                                    await Task.Delay(CalculateDelay(turnContext));
                                    turnContext = new();
                                }


                                turnContext.AddPrioMessage(wildPokemon.NameFr + " lance " + opponentMove.NameFr);
                                if (FightPerformMove.SpecialCaseFail(wildPokemon, opponentMove, playerPokemon))
                                {
                                    turnContext.AddMessage("Mais cela michou");
                                }
                                else
                                {

                                    if (FightPerformMove.IsFieldChangeMove(opponentMove)) wildPokemon = FightPerformMove.FieldChangeMove(opponentMove, wildPokemon, turnContext);
                                    PokemonTeam[] t2Result = FightPerformMove.PerformMove(wildPokemon, playerPokemon, opponentMove, player.FieldChange, turnContext, false);
                                    playerPokemon = t2Result[1];
                                    wildPokemon = t2Result[0];
                                    PokemonTeam[] t2SpeCaseResult = FightPerformMove.PerformSpecialCaseMove(wildPokemon, playerPokemon, opponentMove, turnContext, usedMove);
                                    playerPokemon = t2SpeCaseResult[1];
                                    wildPokemon = t2SpeCaseResult[0];
                                    if (await ManageSpecialCasesAfterMove(wildOpponentMongo, wildPokemon, playerPokemon, player, turnContext))
                                    {
                                        await _mongoPlayerRepository.UpdatePokemonTeamAsync(playerPokemon, player);
                                        await _mongoWildPokemonRepository.UpdatePokemonTeamAsync(wildPokemon, wildOpponentMongo);
                                        await Clients.Caller.SendAsync("useMoveResult", turnContext);
                                        await Task.Delay(CalculateDelay(turnContext) + 500);
                                        await FinishFight(player._id, wildOpponentMongo._id, true);
                                        return;
                                    }
                                }
                            }
                            else
                            {
                                turnContext.AddPrioMessage("La peur empêche " + wildPokemon.NameFr + " d'attaquer");
                            }



                            await Clients.Caller.SendAsync("useMoveResult", turnContext);
                            await Task.Delay(CalculateDelay(turnContext));
                            turnContext = new();
                            if (playerPokemon.CurrHp <= 0)
                            {
                                string message = playerPokemon.NameFr + " est K.O";
                                if(playerPokemonMongo.Substitute != null)
                                {
                                    playerPokemonMongo.Substitute = null;
                                    playerPokemon = playerPokemonMongo;
                                    await Clients.Caller.SendAsync("useMoveResult", turnContext);
                                    await Task.Delay(CalculateDelay(turnContext));
                                }
                                else
                                {
                                    await _mongoPlayerRepository.UpdatePokemonTeamAsync(playerPokemon, player);
                                    await _mongoWildPokemonRepository.UpdatePokemonTeamAsync(wildPokemon, wildOpponentMongo);
                                    if (player.Team.FirstOrDefault(x => x.CurrHp > 0) == null)
                                    {
                                        await FinishFight(playerId, wildOpponentId);
                                        return;
                                    }
                                    else
                                    {
                                        await Clients.Caller.SendAsync("playerPokemonDeath", message);
                                    }
                                }
                            }
                            if (wildPokemon.CurrHp <= 0)//Pokémon mort
                            {
                                turnContext.AddPrioMessage(wildPokemon.NameFr + " est K.O");

                                if (wildPokemon.Substitute != null)
                                {
                                    wildPokemonMongo.Substitute = null;
                                    wildPokemon = wildPokemonMongo;
                                    await Clients.Caller.SendAsync("useMoveResult", turnContext);
                                    await Task.Delay(CalculateDelay(turnContext));
                                }
                                else
                                {
                                await Clients.Caller.SendAsync("useMoveResult", turnContext);
                                await Task.Delay(CalculateDelay(turnContext));
                                await _mongoPlayerRepository.UpdatePokemonTeamAsync(playerPokemon, player);
                                await _mongoWildPokemonRepository.UpdatePokemonTeamAsync(wildPokemon, wildOpponentMongo);
                                await FinishFight(playerId, wildOpponentId);
                                return;
                                }


                            }
                            turnContext = new();
                            playerPokemon = FightAilmentMove.SufferAilment(playerPokemon, turnContext);
                            PokemonTeam[] specialCaseResponse1 = SufferSpecialCase(playerPokemon, wildPokemon, turnContext);
                            specialCaseResponse1[0] = playerPokemon;
                            specialCaseResponse1[1] = wildPokemon;
                            wildPokemon = FightAilmentMove.SufferAilment(wildPokemon, turnContext);
                            PokemonTeam[] specialCaseResponse2 = SufferSpecialCase(wildPokemon, playerPokemon, turnContext);
                            specialCaseResponse2[0] = wildPokemon;
                            specialCaseResponse2[1] = playerPokemon;
                            await Clients.Caller.SendAsync("useMoveResult", turnContext);
                            await Task.Delay(CalculateDelay(turnContext));
                        }
                    }
                }
            }
            else
            {//IA joue en premier 
                if (opponentMove.NameFr == "Métronome")
                {
                    turnContext.AddMessage(wildPokemon.NameFr + " lance Métronome");
                    PokemonTeamMove newMove = PokemonMoveSelector.ConvertToTeamMove(await GetMoveExtApi.GetMetronomeMove());
                    opponentMove = newMove;
                    await Clients.Caller.SendAsync("useMoveResult", turnContext);
                    await Task.Delay(CalculateDelay(turnContext));
                    turnContext = new();
                }
                turnContext.AddPrioMessage(wildPokemon.NameFr + " lance " + opponentMove.NameFr);
                if (FightPriority.MoveMustBePlayedLast(opponentMove) || FightPerformMove.SpecialCaseFail(wildPokemon, opponentMove, playerPokemon))
                {
                    turnContext.AddMessage("Mais cela michou");
                }
                else
                {

                    if (FightPerformMove.IsFieldChangeMove(opponentMove)) wildPokemon = FightPerformMove.FieldChangeMove(opponentMove, wildPokemon, turnContext);
                    PokemonTeam[] t1Result = FightPerformMove.PerformMove(wildPokemon, playerPokemon, opponentMove, player.FieldChange, turnContext, false);
                    playerPokemon = t1Result[1];
                    wildPokemon = t1Result[0];
                    PokemonTeam[] t1SpeCaseResult = FightPerformMove.PerformSpecialCaseMove(wildPokemon, playerPokemon, usedMove, turnContext);
                    playerPokemon = t1SpeCaseResult[1];
                    wildPokemon = t1SpeCaseResult[0];
                    if (await ManageSpecialCasesAfterMove(wildOpponentMongo, wildPokemon, playerPokemon, player, turnContext))
                    {
                        await _mongoPlayerRepository.UpdatePokemonTeamAsync(playerPokemon, player);
                        await _mongoWildPokemonRepository.UpdatePokemonTeamAsync(wildPokemon, wildOpponentMongo);
                        await Clients.Caller.SendAsync("useMoveResult", turnContext);
                        await Task.Delay(CalculateDelay(turnContext) + 500);
                        await FinishFight(player._id, wildOpponentMongo._id, true);
                        return;
                    }
                }
                int playerOldHp = playerPokemon.CurrHp;
                int opponentOldHp = wildPokemon.CurrHp;
                await Clients.Caller.SendAsync("useMoveResult", turnContext);
                await Task.Delay(CalculateDelay(turnContext) + 500);
                turnContext = new();
                if (playerPokemon.CurrHp <= 0)
                {
                    string message = playerPokemon.NameFr + " est K.O";
                    if (playerPokemonMongo.Substitute != null)
                    {
                        playerPokemonMongo.Substitute = null;
                        playerPokemon = playerPokemonMongo;
                        await Clients.Caller.SendAsync("useMoveResult", turnContext);
                        await Task.Delay(CalculateDelay(turnContext));
                    }
                    else
                    {
                        await _mongoPlayerRepository.UpdatePokemonTeamAsync(playerPokemon, player);
                        await _mongoWildPokemonRepository.UpdatePokemonTeamAsync(wildPokemon, wildOpponentMongo);
                        if (player.Team.FirstOrDefault(x => x.CurrHp > 0) == null)
                        {
                            await FinishFight(playerId, wildOpponentId);
                            return;
                        }
                        else
                        {
                            await Clients.Caller.SendAsync("playerPokemonDeath", message);
                        }
                    }
                }
                else
                {
                    if (!playerPokemon.IsFlinched) {

                        if (usedMove.NameFr == "Métronome")
                        {
                            turnContext.AddMessage(playerPokemon.NameFr + " lance Métronome");
                            PokemonTeamMove newMove = PokemonMoveSelector.ConvertToTeamMove(await GetMoveExtApi.GetMetronomeMove());
                            usedMove = newMove;
                            await Clients.Caller.SendAsync("useMoveResult", turnContext);
                            await Task.Delay(CalculateDelay(turnContext));
                            turnContext = new();
                        }

                        if (usedMove.NameFr == "Mimique")
                        {
                            turnContext.AddMessage(playerPokemon.NameFr + " lance Mimique");
                            usedMove = opponentMove;
                            await Clients.Caller.SendAsync("useMoveResult", turnContext);
                            await Task.Delay(CalculateDelay(turnContext));
                            turnContext = new();
                        }

                        if (usedMove.NameFr == "Copie")
                        {
                            //opponentMove
                            var move = playerPokemon.Moves.FirstOrDefault(o => o.NameFr== "Copie"); 

                            int r = Array.IndexOf(playerPokemon.Moves, move);

                            playerPokemon.SavedMove = move;
                            playerPokemon.SavedMoveSlot = r;
                            playerPokemon.Moves[r] = opponentMove;

                        }

                        turnContext.AddPrioMessage(playerPokemon.NameFr + " lance " + usedMove.NameFr);

                        if(FightPerformMove.SpecialCaseFail(playerPokemon, usedMove, wildPokemon))
                        {
                            turnContext.AddMessage("Mais cela michou");
                        }
                        else
                        {

                            if (FightPerformMove.IsFieldChangeMove(usedMove)) player = FightPerformMove.FieldChangeMove(usedMove, player, turnContext);
                            PokemonTeam[] t2Result = FightPerformMove.PerformMove(playerPokemon, wildPokemon, usedMove, wildPokemon.FieldChange, turnContext, true, player);
                            playerPokemon = t2Result[0];
                            wildPokemon = t2Result[1];
                            PokemonTeam[] t2SpeCaseResult = FightPerformMove.PerformSpecialCaseMove(playerPokemon, wildPokemon, usedMove, turnContext, opponentMove);
                            playerPokemon = t2SpeCaseResult[0];
                            wildPokemon = t2SpeCaseResult[1];
                            if (await ManageSpecialCasesAfterMove(wildOpponentMongo, wildPokemon, playerPokemon, player, turnContext))
                            {
                                await _mongoPlayerRepository.UpdatePokemonTeamAsync(playerPokemon, player);
                                await _mongoWildPokemonRepository.UpdatePokemonTeamAsync(wildPokemon, wildOpponentMongo);
                                await Clients.Caller.SendAsync("useMoveResult", turnContext);
                                await Task.Delay(CalculateDelay(turnContext) + 500);
                                await FinishFight(player._id, wildOpponentMongo._id, true);
                                return;
                            }

                            player.FieldChangeCount--;
                            if(player.FieldChangeCount <= 0)
                            {
                                player.FieldChangeCount = null;
                                player.FieldChange = null;
                            }

                            if (wildPokemon.CurrHp <= 0)
                            {
                                turnContext.AddMessage(wildPokemon.NameFr + " est K.O");

                                if (wildPokemon.Substitute != null)
                                {
                                    wildPokemonMongo.Substitute = null;
                                    wildPokemon = wildPokemonMongo;
                                    await Clients.Caller.SendAsync("useMoveResult", turnContext);
                                    await Task.Delay(CalculateDelay(turnContext));
                                }
                                else
                                {
                                    await Clients.Caller.SendAsync("useMoveResult", turnContext);
                                    await Task.Delay(CalculateDelay(turnContext));
                                    await _mongoPlayerRepository.UpdatePokemonTeamAsync(playerPokemon, player);
                                    await _mongoWildPokemonRepository.UpdatePokemonTeamAsync(wildPokemon, wildOpponentMongo);
                                    await FinishFight(playerId, wildOpponentId);
                                    return;
                                }


                            }

                        }
                    }
                    else
                    {
                        turnContext.AddPrioMessage("La peur empêche " + playerPokemon.NameFr + " d'attaquer");
                    }




                    playerPokemon = FightAilmentMove.SufferAilment(playerPokemon, turnContext);
                    PokemonTeam[] specialCaseResponse1 = SufferSpecialCase(playerPokemon, wildPokemon, turnContext);
                    specialCaseResponse1[0] = playerPokemon;
                    specialCaseResponse1[1] = wildPokemon;
                    wildPokemon = FightAilmentMove.SufferAilment(wildPokemon, turnContext);
                    PokemonTeam[] specialCaseResponse2 = SufferSpecialCase(wildPokemon, playerPokemon, turnContext);
                    specialCaseResponse2[0] = wildPokemon;
                    specialCaseResponse2[1] = playerPokemon;

                    await Clients.Caller.SendAsync("useMoveResult", turnContext);
                    await Task.Delay(CalculateDelay(turnContext));
                    turnContext = new();
                    if (wildPokemon.CurrHp <= 0)
                    {
                        turnContext.AddMessage(wildPokemon.NameFr + " est K.O");

                        if (wildPokemon.Substitute != null)
                        {
                            wildPokemonMongo.Substitute = null;
                            wildPokemon = wildPokemonMongo;
                            await Clients.Caller.SendAsync("useMoveResult", turnContext);
                            await Task.Delay(CalculateDelay(turnContext));
                        }
                        else
                        {
                            await Clients.Caller.SendAsync("useMoveResult", turnContext);
                            await Task.Delay(CalculateDelay(turnContext));
                            await _mongoPlayerRepository.UpdatePokemonTeamAsync(playerPokemon, player);
                            await _mongoWildPokemonRepository.UpdatePokemonTeamAsync(wildPokemon, wildOpponentMongo);
                            await FinishFight(playerId, wildOpponentId);
                            return;
                        }


                    }
                    if (playerPokemon.CurrHp <= 0)
                    {
                        if (playerPokemonMongo.Substitute != null)
                        {
                            playerPokemonMongo.Substitute = null;
                            playerPokemon = playerPokemonMongo;
                            await Clients.Caller.SendAsync("useMoveResult", turnContext);
                            await Task.Delay(CalculateDelay(turnContext));
                        }
                        else
                        {
                            string message = playerPokemon.NameFr + " est K.O";
                            await _mongoPlayerRepository.UpdatePokemonTeamAsync(playerPokemon, player);
                            await _mongoWildPokemonRepository.UpdatePokemonTeamAsync(wildPokemon, wildOpponentMongo);
                            if (player.Team.FirstOrDefault(x => x.CurrHp > 0) == null)
                            {
                                await FinishFight(playerId, wildOpponentId);
                                return;
                            }
                            else
                            {
                                await Clients.Caller.SendAsync("playerPokemonDeath", message);
                            }
                        }
                    }
                    turnContext = new();
                }
            }

            if (!skipTurn)
            {
                if (wildPokemon.MultiTurnsMove != null)
                {
                    PokemonTeam[] response = FightPerformMove.PerformMultiTurnMove(playerPokemon, wildPokemon, wildPokemon.MultiTurnsMove, turnContext);
                    playerPokemon = response[0];
                    wildPokemon = response[1];
                    wildPokemon.MultiTurnsMoveCount--;
                    if (wildPokemon.MultiTurnsMoveCount <= 0)
                    {
                        if (wildPokemon.MultiTurnsMove.NameFr == "Entrave")
                        {
                            turnContext.AddPrioMessage(wildPokemon.CantUseMoves[0] + " n'est plus sous entrave");
                            wildPokemon.CantUseMoves = [];
                        }
                        wildPokemon.MultiTurnsMoveCount = null;
                        wildPokemon.MultiTurnsMove = null;
                    }
                    await Clients.Caller.SendAsync("useMoveResult", turnContext);
                    await Task.Delay(CalculateDelay(turnContext));
                    turnContext = new();
                }

                if (wildPokemon.CurrHp <= 0)
                {
                    turnContext.AddMessage(wildPokemon.NameFr + " est K.O");

                    if (wildPokemon.Substitute != null)
                    {
                        wildPokemonMongo.Substitute = null;
                        wildPokemon = wildPokemonMongo;
                        await Clients.Caller.SendAsync("useMoveResult", turnContext);
                        await Task.Delay(CalculateDelay(turnContext));
                    }
                    else
                    {
                        await Clients.Caller.SendAsync("useMoveResult", turnContext);
                        await Task.Delay(CalculateDelay(turnContext));
                        turnContext = new();
                        await _mongoPlayerRepository.UpdatePokemonTeamAsync(playerPokemon, player);
                        await _mongoWildPokemonRepository.UpdatePokemonTeamAsync(wildPokemon, wildOpponentMongo);
                        await FinishFight(playerId, wildOpponentId);
                        return;
                    }


                }

                if (playerPokemon.MultiTurnsMove != null)
                {
                    PokemonTeam[] response = FightPerformMove.PerformMultiTurnMove(wildPokemon, playerPokemon, playerPokemon.MultiTurnsMove, turnContext);
                    wildPokemon = response[0];
                    playerPokemon = response[1];
                    playerPokemon.MultiTurnsMoveCount--;
                    if (playerPokemon.MultiTurnsMoveCount <= 0)
                    {
                        if (playerPokemon.MultiTurnsMove.NameFr == "Entrave")
                        {
                            turnContext.AddPrioMessage(playerPokemon.CantUseMoves[0] + " n'est plus sous entrave");
                            playerPokemon.CantUseMoves = [];
                        }
                        playerPokemon.MultiTurnsMoveCount = null;
                        playerPokemon.MultiTurnsMove = null;
                    }
                    await Clients.Caller.SendAsync("useMoveResult", turnContext);
                    await Task.Delay(CalculateDelay(turnContext));
                    turnContext = new();
                }

                if (playerPokemon.CurrHp <= 0)
                {
                    string message = playerPokemon.NameFr + " est K.O";
                    if (playerPokemonMongo.Substitute != null)
                    {
                        playerPokemonMongo.Substitute = null;
                        playerPokemon = playerPokemonMongo;
                        await Clients.Caller.SendAsync("useMoveResult", turnContext);
                        await Task.Delay(CalculateDelay(turnContext));
                    }
                    else
                    {
                        await _mongoPlayerRepository.UpdatePokemonTeamAsync(playerPokemon, player);
                        await _mongoWildPokemonRepository.UpdatePokemonTeamAsync(wildPokemon, wildOpponentMongo);
                        if (player.Team.FirstOrDefault(x => x.CurrHp > 0) == null)
                        {
                            await FinishFight(playerId, wildOpponentId);
                            return;
                        }
                        else
                        {
                            await Clients.Caller.SendAsync("playerPokemonDeath", message);
                        }
                    }

                    turnContext = new();
                }

                wildPokemon.FieldChangeCount--;
                if (wildPokemon.FieldChangeCount <= 0)
                {
                    if (wildPokemon.FieldChange == "Brume") turnContext.AddMessage("La brume disparaît");
                    if (wildPokemon.FieldChange == "Mur Lumière") turnContext.AddMessage("Mur lumière n'est plus actif");
                    if (wildPokemon.FieldChange == "Protection") turnContext.AddMessage("Protection n'est plus actif");
                    wildPokemon.FieldChangeCount = null;
                    wildPokemon.FieldChange = null;
                    await Clients.Caller.SendAsync("useMoveResult", turnContext);
                    await Task.Delay(CalculateDelay(turnContext));
                    turnContext = new();
                }
                player.FieldChangeCount--;
                if (player.FieldChangeCount <= 0)
                {
                    if (player.FieldChange == "Brume") turnContext.AddMessage("La brume disparaît");
                    if (player.FieldChange == "Mur Lumière") turnContext.AddMessage("Mur lumière n'est plus actif");
                    if (player.FieldChange == "Protection") turnContext.AddMessage("Protection n'est plus actif");
                    player.FieldChangeCount = null;
                    player.FieldChange = null;
                    await Clients.Caller.SendAsync("useMoveResult", turnContext);
                    await Task.Delay(CalculateDelay(turnContext));
                    turnContext = new();
                }

                if (!FightDamageMove.NeedStackDamages(playerPokemon))
                {
                    playerPokemon.BlowsTaken = 0;
                }
            }

            playerPokemon.IsFlinched = false;
            wildPokemon.IsFlinched = false;
            
            if(playerPokemonMongo.Substitute != null)
            {
                playerPokemonMongo.Substitute = playerPokemon;
            }
            else
            {
                playerPokemonMongo = playerPokemon;
            }

            if (wildPokemonMongo.Substitute != null)
            {
                wildPokemonMongo.Substitute = wildPokemon;
            }
            else
            {
                wildPokemonMongo = wildPokemon;
            }
            player.Team[0] = playerPokemonMongo;
            await _mongoPlayerRepository.UpdateAsync(player);
            //PlayerMongo updatedPlayer = await _mongoPlayerRepository.UpdatePokemonTeamAsync(playerPokemonMongo, player);
            PlayerMongo updatedOpponent = await _mongoWildPokemonRepository.UpdatePokemonTeamAsync(wildPokemon, wildOpponentMongo);

            await Clients.Caller.SendAsync("turnFinished", player, updatedOpponent);

        }

        private PokemonTeam[] SufferSpecialCase(PokemonTeam user, PokemonTeam target, TurnContext turnContext)
        {
            foreach (string specialCase in target.SpecialCases)
            {
                switch (specialCase)
                {
                    case "Vampigraine":
                        turnContext.AddMessage(user.NameFr + " draine l'énergie de " + target.NameFr);

                        int damageDealt = 0;
                        if (target.CurrHp > 16)
                        {
                            damageDealt = target.BaseHp / 8;
                        }
                        else
                        {
                            damageDealt = 1;
                        }
                        target.CurrHp -= damageDealt;
                        if (target.CurrHp < 0) target.CurrHp = 0;
                        user.CurrHp += damageDealt;
                        if (user.CurrHp > user.BaseHp) user.CurrHp = user.BaseHp;
                        break;
                }
            }
            return [user, target];
        }

        private async Task<bool> ManageSpecialCasesAfterMove(PlayerMongo opponent, PokemonTeam wildPokemon, PokemonTeam playerPokemon, PlayerMongo player, TurnContext turnContext)
        {
            foreach (string specialCase in playerPokemon.SpecialCases) {
                switch (specialCase)
                {
                    case "Ejected":
                        if(player.Team.Where(x => x.CurrHp > 0).ToList().Count > 1) return true;
                        else
                        {
                            turnContext.AddMessage("Mais cela michou");
                            return false;
                        }
                        break;
                    case "Teleport":
                        if (!opponent.IsTrainer)
                        {
                            turnContext.AddMessage(wildPokemon.NameFr + " se téléporte");
                        }
                        else
                        {
                            turnContext.AddMessage("Mais cela michou");
                            return false;
                        }

                        return true;
                        break;
                }
            }

            foreach (string specialCase in wildPokemon.SpecialCases)
            {
                switch (specialCase)
                {
                    case "Ejected":
                        if(opponent.Team.Where(x => x.CurrHp > 0).ToList().Count > 1)
                        {
                            return true;
                        }
                        else
                        {
                            if (!opponent.IsTrainer)
                            {
                                turnContext.AddMessage(playerPokemon.NameFr + " met fin au combat");
                                return true;
                            }
                            turnContext.AddMessage("Mais cela michou");
                            return false;
                        }

                        break;
                    case "Teleport":
                        if (!opponent.IsTrainer)
                        {
                            turnContext.AddMessage(playerPokemon.NameFr + " se téléporte");
                        }
                        else
                        {
                            turnContext.AddMessage("Mais cela michou");
                            return false;
                        }
                        return true;
                        break;
                }
            }

            return false;

        }
        public async Task DeleteMove(string playerId, int moveId)
        {
            PlayerMongo player = await _mongoPlayerRepository.GetByPlayerIdAsync(playerId);
            PokemonMongo pokemon = await _mongoPokemonRepository.GetPokemonMongoById(152);
            List<PokemonTeamMove> movesPlayer = player.Team[0].Moves.ToList();
            movesPlayer.RemoveAll(move => move.Id == moveId);
            player.Team[0].Moves = movesPlayer.ToArray();
            
            
            List<MoveMongo> movesPokemon = pokemon.Moves.ToList();
            movesPokemon.RemoveAll(move => move.Id == moveId);
            pokemon.Moves = movesPokemon.ToArray();


            await _mongoPlayerRepository.UpdateAsync(player);
            await _mongoPokemonRepository.UpdateAsync(152, pokemon);
            await Clients.Caller.SendAsync("moveLearned", player);
        }
    
        public int CalculateDelay(TurnContext turnContext)
        {
            int delay = 0;
            delay += turnContext.Messages.Count * 1000;
            delay += turnContext.PrioMessages.Count * 1000;
            delay += turnContext.Player.Hp.Count * 500;
            delay += turnContext.Opponent.Hp.Count * 500;
            return delay;
        }
    
    }

    public class TurnContext
    {
        public string ActionName { get; set; } = "";
        public PokemonChanges Opponent { get; set; } = new PokemonChanges();
        public PokemonChanges Player { get; set; } = new PokemonChanges();
        public List<string> PrioMessages { get; } = new List<string>();
        public List<string> Messages { get; } = new List<string>();

        public void AddMessage(string message)
        {
            Messages.Add(message);
        }
        
        public void DeleteLastMessage()
        {
            Messages.RemoveAt(Messages.Count - 1);
        }

        public void DeleteLastPrioMessage()
        {
            PrioMessages.RemoveAt(PrioMessages.Count - 1);
        }

        public void AddPrioMessage(string message)
        {
            PrioMessages.Add(message);
        }
    }

    public class PokemonChanges
    {
        public List<int> Hp { get; set; } = new List<int>(); 
        public int Atk { get; set; } = 0;
        public int AtkSpe { get; set; } = 0;
        public int Def { get; set; } = 0;
        public int DefSpe { get; set; } = 0;
        public int Speed { get; set; } = 0;
        public int Index { get; set; } = 0;

        public void AddStatChange(string statName, int change)
        {
            switch (statName)
            {
                case "attack":
                    Atk += change;
                    break;
                case "attack-special":
                    AtkSpe += change;
                    break;
                case "defense":
                    Def += change;
                    break;
                case "defense-special":
                    DefSpe += change;
                    break;
                case "speed":
                    Speed += change;
                    break;
            }
        }

    }
}
