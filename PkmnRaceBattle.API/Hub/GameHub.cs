namespace PkmnRaceBattle.API.Hub
{
    using Microsoft.AspNet.SignalR.Messaging;
    using Microsoft.AspNet.SignalR.Tracing;
    using Microsoft.AspNetCore.Components.Web;
    using Microsoft.AspNetCore.SignalR;
    using Microsoft.Extensions.Primitives;
    using Microsoft.Owin.Security.Provider;
    using MongoDB.Driver.Core.Connections;
    using PkmnRaceBattle.API.Helper;
    using PkmnRaceBattle.API.Helpers.MoveManager;
    using PkmnRaceBattle.API.Helpers.MoveManager.Fights;
    using PkmnRaceBattle.API.Helpers.PokemonGeneration;
    using PkmnRaceBattle.API.Helpers.StatsCalculator;
    using PkmnRaceBattle.API.Helpers.TrainerGeneration;
    using PkmnRaceBattle.Application.Contracts;
    using PkmnRaceBattle.Domain.Models;
    using PkmnRaceBattle.Domain.Models.BracketMongo;
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
        private readonly IMongoBracketRepository _mongoBracketRepository;
        private readonly IHubContext<GameHub> _hubContext;

        public GameHub(
            IMongoRoomRepository mongoRoomRepository,
            IMongoMoveRepository mongoMoveRepository,
            IMongoPlayerRepository mongoPlayerRepository,
            IMongoPokemonRepository mongoPokemonRepository,
            IMongoWildPokemonRepository mongoWildPokemonRepository,
            IMongoBracketRepository mongoBracketRepository,
            IHubContext<GameHub> hubContext
            )
        {
            _mongoRoomRepository = mongoRoomRepository;
            _mongoPlayerRepository = mongoPlayerRepository;
            _mongoPokemonRepository = mongoPokemonRepository;
            _mongoWildPokemonRepository = mongoWildPokemonRepository;
            _mongoMoveRepository = mongoMoveRepository;
            _mongoBracketRepository = mongoBracketRepository;
            _hubContext = hubContext;
        }


        public async Task JoinGame(string username, int starterId, string trainerSprite, string gameCode)
        {
            // Vérifier si l'utilisateur est déjà dans une salle et l'en retirer
            var (currentRoomId, existingUserId) = UserConnectionManager.GetUserRoomByConnectionId(Context.ConnectionId);

            if (!string.IsNullOrEmpty(currentRoomId) && currentRoomId != gameCode && !string.IsNullOrEmpty(existingUserId))
            {

                await Groups.RemoveFromGroupAsync(Context.ConnectionId, currentRoomId);
                UserConnectionManager.RemoveUserFromRoom(existingUserId, currentRoomId);


                await Clients.Group(currentRoomId).SendAsync("UserLeft", currentRoomId);

                // Éventuellement supprimer les données de l'utilisateur si nécessaire
                if (!string.IsNullOrEmpty(existingUserId))
                {

                }
            }

            // Ajouter l'utilisateur à la nouvelle salle
            await Groups.AddToGroupAsync(Context.ConnectionId, gameCode);
            PokemonMongo starterInfos = await _mongoPokemonRepository.GetPokemonMongoById(starterId);
            PokemonTeam pokemonTeam = GenerateNewPokemon.GenerateNewPokemonTeam(starterInfos, 5, 5);

            //PokemonMongo pkmn4 = await _mongoPokemonRepository.GetPokemonMongoById(149);
            //PokemonTeam pkmn4Team = GenerateNewPokemon.GenerateNewPokemonTeam(pkmn4, 15, 15);

            PlayerMongo playerMongo = new PlayerMongo();
            playerMongo.Name = username;
            playerMongo.RoomId = gameCode;
            playerMongo.Team = [pokemonTeam];
            playerMongo.IsHost = false;
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

            var (currentRoomId, existingUserId) = UserConnectionManager.GetUserRoomByConnectionId(Context.ConnectionId);

            if (!string.IsNullOrEmpty(currentRoomId) && currentRoomId != gameCode && !string.IsNullOrEmpty(existingUserId))
            {

                await Groups.RemoveFromGroupAsync(Context.ConnectionId, currentRoomId);
                UserConnectionManager.RemoveUserFromRoom(existingUserId, currentRoomId);


                await Clients.Group(currentRoomId).SendAsync("UserLeft", currentRoomId);

                // Éventuellement supprimer les données de l'utilisateur si nécessaire
                if (!string.IsNullOrEmpty(existingUserId))
                {

                }
            }

            PokemonMongo starterInfos = await _mongoPokemonRepository.GetPokemonMongoById(starterId);
            /*  PokemonMongo pkmn= await _mongoPokemonRepository.GetPokemonMongoById(133);
              PokemonMongo pkmn1 = await _mongoPokemonRepository.GetPokemonMongoById(133);
            PokemonMongo pkmn2 = await _mongoPokemonRepository.GetPokemonMongoById(133);
            PokemonMongo pkmn3 = await _mongoPokemonRepository.GetPokemonMongoById(150);*/
            PokemonMongo pkmn4 = await _mongoPokemonRepository.GetPokemonMongoById(130); 

             PokemonTeam pokemonTeam = GenerateNewPokemon.GenerateNewPokemonTeam(starterInfos, 5, 5);
            /*  PokemonTeam pkmnTeam = GenerateNewPokemon.GenerateNewPokemonTeam(pkmn, 15, 15);
                PokemonTeam pkmn1Team = GenerateNewPokemon.GenerateNewPokemonTeam(pkmn1, 15, 15);
               PokemonTeam pkmn2Team = GenerateNewPokemon.GenerateNewPokemonTeam(pkmn2, 15, 15);
              PokemonTeam pkmn3Team = GenerateNewPokemon.GenerateNewPokemonTeam(pkmn3, 15, 15);*/
            //PokemonTeam pkmn4Team = GenerateNewPokemon.GenerateNewPokemonTeam(pkmn4, 15, 15); 

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

        private static readonly Dictionary<string, DateTime> _gameTimers = new Dictionary<string, DateTime>();
        private static readonly Dictionary<string, Timer> _timerObjects = new Dictionary<string, Timer>();
        public async Task StartGame(string gameCode, bool checkedTimer, int timerTime)
        {
            RoomMongo room = await _mongoRoomRepository.GetByRoomIdAsync(gameCode);
            room.state = 1;
            await _mongoRoomRepository.UpdateAsync(gameCode, room);

            // Définir l'heure de fin (5 minutes à partir de maintenant)
            //DateTime endTime = DateTime.UtcNow.AddSeconds(2);
            DateTime endTime = DateTime.UtcNow.AddMinutes(timerTime);
            _gameTimers[gameCode] = endTime;

            // Envoi du temps restant initial
            await Clients.Group(gameCode).SendAsync("GameStarted", gameCode);
            if (checkedTimer)
            {
                await Clients.Group(gameCode).SendAsync("TimerUpdate", (endTime - DateTime.UtcNow).TotalSeconds);

                // Utiliser _hubContext au lieu de Clients pour éviter les problèmes de contexte
                Timer timer = new Timer(async _ =>
                {
                    try
                    {
                        double remainingSeconds = (_gameTimers[gameCode] - DateTime.UtcNow).TotalSeconds;

                        if (remainingSeconds <= 0)
                        {
                            List<PlayerMongo> players = await _mongoPlayerRepository.GetByRoomId(gameCode);

                            foreach(PlayerMongo player in players)
                            {
                                for (int i = 0; i < player.Team.Length; i++)
                                {
                                    player.Team[i].CurrHp = player.Team[i].BaseHp;
                                    player.Team[i].IsBurning = false;
                                    player.Team[i].IsParalyzed = false;
                                    player.Team[i].IsPoisoned = 0;
                                    player.Team[i].IsSleeping = 0;
                                    player.Team[i].IsFrozen = false;
                                    player.Team[i] = ResetForSwap(player.Team[i]);
                                }

                                await _mongoPlayerRepository.UpdateAsync(player);
                            }

                            // Le temps est écoulé
                            await _hubContext.Clients.Group(gameCode).SendAsync("TimerEnded", gameCode);

                            if (_timerObjects.ContainsKey(gameCode))
                            {
                                _timerObjects[gameCode].Dispose();
                                _timerObjects.Remove(gameCode);
                            }

                            _gameTimers.Remove(gameCode);
                        }
                        else
                        {
                            // Mettre à jour le timer pour tous les joueurs
                            await _hubContext.Clients.Group(gameCode).SendAsync("TimerUpdate", remainingSeconds);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Gérer l'exception - logger ou autre
                        Console.WriteLine($"Erreur lors de la mise à jour du timer: {ex.Message}");

                        // Nettoyer les ressources en cas d'erreur
                        if (_timerObjects.ContainsKey(gameCode))
                        {
                            _timerObjects[gameCode].Dispose();
                            _timerObjects.Remove(gameCode);
                        }

                        _gameTimers.Remove(gameCode);
                    }
                }, null, 0, 1000);

                _timerObjects[gameCode] = timer;
            }
        }

        public async Task EndGame(string gameCode)
        {
            if (_timerObjects.ContainsKey(gameCode))
            {
                _timerObjects[gameCode].Dispose();
                _timerObjects.Remove(gameCode);
                _gameTimers.Remove(gameCode);
            }

            // Autres logiques de fin de jeu...
        }
        
        public async Task BuildTournament(string gameCode)
        {
            List<PlayerMongo> playersInRoom = await _mongoPlayerRepository.GetByRoomId(gameCode);
           
            //Réarangement aléatoire pour l'arbre
            Random random = new Random();
            playersInRoom = playersInRoom.OrderBy(x => random.Next()).ToList();

            PokemonMongo starterInfos = await _mongoPokemonRepository.GetPokemonMongoById(1);
            PokemonTeam pokemonTeam = GenerateNewPokemon.GenerateNewPokemonTeam(starterInfos, 5, 5);
           /* PlayerMongo player1 = new PlayerMongo();
            player1._id = "67b9bf8a260af2811d80d123";
            player1.Sprite = "cynthia";
            player1.Name = "Ouais";
            player1.RoomId = gameCode;
            player1.IsHost = false;
            player1.Team = [pokemonTeam];
            PlayerMongo player2 = new PlayerMongo();
            player2._id = "67b9bf8a260af2811d80d124";
            player2.Sprite = "brendan";
            player2.Name = "Gros";
            player2.RoomId = gameCode;
            player2.IsHost = false;
            player2.Team = [pokemonTeam];
            PlayerMongo player3 = new PlayerMongo();
            player3._id = "67b9bf8a260af2811d80d125";
            player3.Sprite = "gardenia";
            player3.Name = "sadikoi";
            player3.RoomId = gameCode;
            player3.IsHost = false;
            player3.Team = [pokemonTeam];


            playersInRoom.Add(player1);
            playersInRoom.Add(player2);
            playersInRoom.Add(player3); */

            BracketMongo bracket = new BracketMongo();
            bracket.Players = playersInRoom;
            bracket.NbTurn = 1;
            bracket.GameCode = gameCode;
            for(int i = 1; i <= playersInRoom.Count / 2; i++)
            {
                RoundMongo roundMongo = new RoundMongo();
                roundMongo.RoundNumber = (playersInRoom.Count/2)+1 - i ;
                int nbPlayers = i * 2;
                for(int y = 0; y<nbPlayers; y++)
                {
                    roundMongo.PlayersInRace.Add("?");
                }
                bracket.Rounds.Add(roundMongo);
            }
            bracket.Rounds[bracket.Rounds.Count - 1].PlayersInRace = [];
            foreach (PlayerMongo playerBracket in bracket.Players) {
                bracket.Rounds[bracket.Rounds.Count - 1].PlayersInRace.Add(playerBracket._id);
            }
            await _mongoBracketRepository.CreateAsync(bracket);

            await Clients.Group(gameCode).SendAsync("bracketCreated", bracket);
        }

        public async Task LaunchTournament(string gameCode)
        {
            await Clients.Group(gameCode).SendAsync("triggerTournament");
        }

        public async Task GetPvpFight(string gameCode, string userId)
        {
            List<PlayerMongo> players = await _mongoPlayerRepository.GetByRoomId(gameCode);
            PlayerMongo player = await _mongoPlayerRepository.GetByPlayerIdAsync(userId);
            BracketMongo bracket = await _mongoBracketRepository.GetByRoomId(gameCode);

            int playerIndex = bracket.Rounds[bracket.NbTurn - 1].PlayersInRace.IndexOf(userId);

            int opponentIndex = (playerIndex % 2 == 0) ? playerIndex + 1 : playerIndex - 1;

            string opponentId = bracket.Rounds[bracket.NbTurn - 1].PlayersInRace[opponentIndex];

            PlayerMongo opponent = bracket.Players.Where(x => x._id == opponentId).FirstOrDefault();

            //PlayerMongo opponent = players.FirstOrDefault(x => x._id != player._id);

            await Clients.Caller.SendAsync("responsePvpFight", opponent);
        }

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
                player.Team[i] = ResetForSwap(player.Team[i]);
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
            await FinishFight(player, opponent);

        }

        public async Task ReplacePokemon(string userId, string pokemonId, string opponentId, bool pvp)
        {
            PlayerMongo player = await _mongoPlayerRepository.GetByPlayerIdAsync(userId);
            PlayerMongo opponent;

            if(!pvp)opponent = await _mongoWildPokemonRepository.GetByIdAsync(opponentId);
            else opponent = await _mongoPlayerRepository.GetByPlayerIdAsync(opponentId);

            PokemonTeam pokemon = player.Team.FirstOrDefault(x => x.Id == pokemonId);

            if (player.Team[0].MultiTurnsMove != null && (player.Team[0].MultiTurnsMove.NameFr == "Ligotage" || player.Team[0].MultiTurnsMove.NameFr == "Étreinte") && player.Team[0].CurrHp > 0)
            {
                TurnContext turnContext = new TurnContext();
                if(player.Team[0].MultiTurnsMove.NameFr == "Ligotage") turnContext.AddMessage("Un Pokémon sous Ligotage ne peut pas être remplacé");
                if(player.Team[0].MultiTurnsMove.NameFr == "Étreinte") turnContext.AddMessage("Un Pokémon sous Ligotage ne peut pas être remplacé");
                await Clients.Caller.SendAsync("useMoveResult", turnContext);
                await Clients.Caller.SendAsync("turnFinished", player, opponent);
                return;
            }

            pokemon = ResetForSwap(pokemon);
            player.Team[0] = ResetForSwap(player.Team[0]);


            PokemonTeam pokemonSwap = player.Team[0];

            // Échanger les positions
            int indexOfPokemon = Array.IndexOf(player.Team, pokemon);
            
            player.Team[0] = pokemon;
            player.Team[indexOfPokemon] = pokemonSwap;

            await _mongoPlayerRepository.UpdateAsync(player);

            if(pokemonSwap.CurrHp <= 0)
            {
                TurnContext turnContext = new TurnContext();
                turnContext.AddMessage(player.Name + " envoie " + pokemon.NameFr);
                await Clients.Caller.SendAsync("useMoveResult", turnContext);
                if (pvp)
                {
                    string opponentConnectionId = UserConnectionManager.GetConnectionId(opponent._id, opponent.RoomId);
                    await Clients.Client(opponentConnectionId).SendAsync("useMoveResult", turnContext);
                    await Clients.Client(opponentConnectionId).SendAsync("turnFinished", opponent, player);
                }
                await Clients.Caller.SendAsync("turnFinished", player, opponent);
            }
            else
            {
                await HandleMove(userId, pokemon.Id, "swap:", opponentId, opponent.Team[0].Id, true, pvp);
            }
        }

        private static PokemonTeam ResetForSwap(PokemonTeam pokemon)
        {
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
            if (pokemon.UnmorphedForm != null)
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

            return pokemon;
        }

        public async Task FinishFight(PlayerMongo player, PlayerMongo opponent, bool unexpectedEnd = false)
        {
            PokemonTeam opponentPokemon = opponent.Team[0];
            string opponentConnectionId = "";
            if (opponent.IsPlayer) opponentConnectionId = UserConnectionManager.GetConnectionId(opponent._id, opponent.RoomId);

            bool lost = false;
            if (unexpectedEnd && opponent.IsTrainer) {
                if (player.Team.FirstOrDefault(x => x.SpecialCases.Contains("Ejected")) != null){
                    player.Team.FirstOrDefault(x => x.SpecialCases.Contains("Ejected")).SpecialCases = new();
                    _mongoPlayerRepository.UpdateAsync(player);
                    await Clients.Caller.SendAsync("playerPokemonDeath", "Changez de Pokémon");
                    return;
                }
                if (opponent.Team.FirstOrDefault(x => x.SpecialCases.Contains("Ejected")) != null)
                {
                    opponent.Team.FirstOrDefault(x => x.SpecialCases.Contains("Ejected")).SpecialCases = new();
                    _mongoWildPokemonRepository.UpdateAsync(opponent);
                    if (opponent.IsPlayer) 
                    {
                        await Clients.Client(opponentConnectionId).SendAsync("playerPokemonDeath", "Changez de Pokémon");
                        await Clients.Caller.SendAsync("playerPokemonDeath", "Changez de Pokémon");
                    } 
                    else
                    {
                        await TrainerSendNextPokemon(player, opponent);
                    }

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
                        team.CurrXP += PokemonExperienceCalculator.ExpGained(opponentPokemon, opponent.IsTrainer, false, 0);
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
                        TurnContext turnContext = new TurnContext();

                        if (!opponent.IsPlayer)
                        {
                            int earnedCredits = 2000;
                            player.Credits += earnedCredits;
                            turnContext.AddMessage("Vous avez battu " + opponent.Name);
                            turnContext.AddMessage("Vous remportez " + earnedCredits + " Pokédollz");
                        }
                        else
                        {
                            turnContext.AddMessage("Vous avez battu " + opponent.Name + ", vous êtes qualifié pour la suite");
                        }

                        await Clients.Caller.SendAsync("useMoveResult", turnContext);
                       

                        if (opponent.IsPlayer)
                        {
                            string message = "Vous n'avez plus de pokémon en forme, vous êtes éliminé.";
                            await Clients.Client(opponentConnectionId).SendAsync("playerLooseFight", message);
                        }
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

                if (opponent.IsPlayer)
                {
                    TurnContext turnContext = new TurnContext();
                    turnContext.AddMessage("Vous avez battu " + opponent.Name + ", vous êtes qualifié pour la manche suivante");
                    await Clients.Client(opponentConnectionId).SendAsync("useMoveResult", turnContext);
                }

                await Task.Delay(3000);
            }

            await this._mongoPlayerRepository.UpdateAsync(player);

            if(!opponent.IsPlayer) await this._mongoWildPokemonRepository.UpdateAsync(opponent);
            else await this._mongoPlayerRepository.UpdateAsync(opponent);

            if (!opponent.IsPlayer && opponent.IsTrainer && opponent.Team.FirstOrDefault(x => x.CurrHp > 0) != null && !lost) {

                await TrainerSendNextPokemon(player, opponent);
            }
            else
            {
                if (opponent.IsPlayer)
                {

                }
                else
                {
                    await GetNewTurn(player._id);
                }
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
                    MoveMongo moveMongo = pokemonBase.Moves.FirstOrDefault(x => x.LearnedAtLvl == team.Level);
                    if(team.Moves.FirstOrDefault(x => x.NameFr == moveMongo.NameFr) == null)
                    {
                        if (team.Moves.Length < 4)
                        {
                            PokemonTeamMove move = PokemonMoveSelector.ConvertToTeamMove(moveMongo);

                            PokemonTeamMove[] newMoves = new PokemonTeamMove[team.Moves.Length + 1];
                            Array.Copy(team.Moves, newMoves, team.Moves.Length);
                            newMoves[newMoves.Length - 1] = move;
                            team.Moves = newMoves;
                            learnedMove = move.NameFr;

                        }
                        else
                        {
                            movesToLearn.Add(moveMongo);
                        }
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

        public async Task HandleMove(string playerId, string playerPokemonId, string usedMoveName, string opponentId, string opponentPokemonId, bool isAttacking, bool pvp, int index = 0, bool skipTurn = false)
        {
            TurnContext turnContext = new TurnContext();
            PlayerMongo player = await _mongoPlayerRepository.GetByPlayerIdAsync(playerId);
            PokemonTeam playerPokemonMongo = await _mongoPlayerRepository.GetPlayerPokemonById(player._id, playerPokemonId);

            PlayerMongo opponentMongo;
            PokemonTeam opponentPokemonMongo;
            if (pvp)
            {
                opponentMongo = await _mongoPlayerRepository.GetByPlayerIdAsync(opponentId);
                //opponentPokemonMongo = await _mongoPlayerRepository.GetPlayerPokemonById(opponentMongo._id, opponentPokemonId);
                opponentPokemonMongo = opponentMongo.Team[0];
            }
            else 
            { 
                opponentMongo = await _mongoWildPokemonRepository.GetByIdAsync(opponentId);
                opponentPokemonMongo = await _mongoWildPokemonRepository.GetPlayerPokemonById(opponentMongo._id, opponentPokemonId);
            } 

            PokemonTeamMove usedMove;
            if (usedMoveName.StartsWith("item:") || usedMoveName.StartsWith("swap:"))
            {
                usedMove = PokemonMoveSelector.ConvertToActionMove(usedMoveName, index);
            }
            else
            {
                if (playerPokemonMongo.WaitingMove != null)
                {
                    usedMove = PokemonMoveSelector.ConvertToTeamMove(await _mongoMoveRepository.GetMoveMongoByName(playerPokemonMongo.WaitingMove.NameFr));
                }
                else
                {
                    usedMove = await _mongoPlayerRepository.GetPokemonTeamMoveByName(playerId, playerPokemonId, usedMoveName);
                }
                //usedMove = PokemonMoveSelector.ConvertToTeamMove(await _mongoMoveRepository.GetMoveMongoByName("Furie"));
            }

            PokemonTeam pkmnTeamToCheck;
            if (index != 0) pkmnTeamToCheck = player.Team[index];
            else pkmnTeamToCheck = playerPokemonMongo;
            if (!ValidatorMove.IsEverythingOk(player, pkmnTeamToCheck, usedMove, opponentMongo, opponentPokemonMongo, turnContext))
            {
                await Clients.Caller.SendAsync("useMoveResult", turnContext);
                await Clients.Caller.SendAsync("turnFinished", player, opponentMongo);
                return;
            }


            PokemonTeamMove opponentMove;
            if (pvp)
            {
                if (opponentPokemonMongo.WaitingMove != null) 
                {
                    opponentMongo.ChosenMove = opponentPokemonMongo.WaitingMove;
                }
                if (opponentMongo.ChosenMove == null) 
                {
                    player.ChosenMove = usedMove;
                    player.ChosenIndex = index;
                    await _mongoPlayerRepository.UpdateAsync(player);
                    await Clients.Caller.SendAsync("waitingOpponent");
                }
                else
                {
                    await UseMove(player, playerPokemonMongo, usedMove, opponentMongo, opponentPokemonMongo, opponentMongo.ChosenMove, isAttacking, pvp, index, opponentMongo.ChosenIndex);
                }
            }
            else
            {
                if (opponentPokemonMongo.WaitingMove != null)
                {
                    opponentMove = PokemonMoveSelector.ConvertToTeamMove(await _mongoMoveRepository.GetMoveMongoByName(opponentPokemonMongo.WaitingMove.NameFr));
                }
                else
                {
                    opponentMove = AIChoseMove.GetARandomMove(opponentPokemonMongo);
                    //opponentMove = AIChoseMove.GetThatMove(opponentPokemonMongo, "Bouclier");
                }
                await UseMove(player, playerPokemonMongo, usedMove, opponentMongo, opponentPokemonMongo, opponentMove, isAttacking, pvp, index, opponentMongo.ChosenIndex);
            }


        }

        //Player vs wildPokemon
        public async Task UseMove(PlayerMongo player, PokemonTeam playerPokemonMongo, PokemonTeamMove usedMove, PlayerMongo opponentMongo , PokemonTeam opponentPokemonMongo, PokemonTeamMove opponentMove, bool isAttacking, bool pvp, int indexPlayer=0, int indexOponnent=0, bool skipTurn = false)
        {
            TurnContext turnContext = new TurnContext();
            string playerId = player._id;
            string opponentId = opponentMongo._id;

            player.ChosenMove = null;
            opponentMongo.ChosenMove = null;
            player.ChosenIndex = 0;
            opponentMongo.ChosenIndex = 0;

            playerPokemonMongo.HavePlayed = true;
            string opponentConnectionId = "";
            if(pvp) opponentConnectionId = UserConnectionManager.GetConnectionId(opponentMongo._id, opponentMongo.RoomId);

            PokemonTeam playerPokemon;
            if(playerPokemonMongo.Substitute != null)
            {
                playerPokemon = (PokemonTeam)playerPokemonMongo.Substitute.Clone();
            }
            else
            {
                playerPokemon = (PokemonTeam)playerPokemonMongo.Clone();
            }

            PokemonTeam opponentPokemon;
            if(opponentPokemonMongo.Substitute != null)
            {
                opponentPokemon = (PokemonTeam)opponentPokemonMongo.Substitute.Clone();
            }
            else
            {
                opponentPokemon = (PokemonTeam)opponentPokemonMongo.Clone();
            }
            int catchValue = 0;

            playerPokemon = FightAilmentMove.TryRemoveAilment(playerPokemon, turnContext);
            opponentPokemon = FightAilmentMove.TryRemoveAilment(opponentPokemon, turnContext);

            if (FightPriority.IsPlayingFirst(playerPokemon, usedMove, opponentPokemon, opponentMove))
            {//Joueur joue en premier

                if (usedMove.NameFr == "Métronome")
                {
                    turnContext.AddMessage(playerPokemon.NameFr + " lance Métronome");
                    PokemonTeamMove newMove = PokemonMoveSelector.ConvertToTeamMove(await GetMoveExtApi.GetMetronomeMove());
                    usedMove = newMove;
                    await HandleUseMoveResult(turnContext, opponentConnectionId);
                    //await Clients.Caller.SendAsync("useMoveResult", turnContext);
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
                    catchValue = FightCatch.TryCatchPokemon(opponentPokemon, usedMove.NameFr);
                    await Task.Delay(1000);
                    await Clients.Caller.SendAsync("catchResult", catchValue);
                    if (catchValue == -1)
                    {
                        await Task.Delay(1000);
                    }
                    else
                    {
                        await Task.Delay(1000 * (catchValue + 1));
                    }

                }
                else
                {
                    if (usedMove.NameFr == "swap")
                    {
                        await Clients.Caller.SendAsync("swapPokemon", playerPokemon, turnContext.PrioMessages[0]);
                        if (pvp) await Clients.Client(opponentConnectionId).SendAsync("foeSwapPokemon", playerPokemon, turnContext.PrioMessages[0]);
                        await Task.Delay(2000);
                    }
                    else
                    {
                        if (FightPriority.MoveMustBePlayedLast(usedMove) || FightPerformMove.SpecialCaseFail(playerPokemon, usedMove, opponentPokemon, opponentMove))
                        {
                            turnContext.AddMessage("Mais cela michou");
                        }
                        else
                        {
                            if (FightPerformMove.IsFieldChangeMove(usedMove)) player = FightPerformMove.FieldChangeMove(usedMove, player, turnContext);

                            if (indexPlayer != 0) playerPokemon = player.Team[indexPlayer];
                            PokemonTeam[] t1Result = FightPerformMove.PerformMove(playerPokemon, opponentPokemon, usedMove, opponentPokemon.FieldChange, turnContext, true, player);
                            playerPokemon = t1Result[0];
                            opponentPokemon = t1Result[1];
                            PokemonTeam[] t1SpeCaseResult = FightPerformMove.PerformSpecialCaseMove(playerPokemon, opponentPokemon, usedMove, turnContext);
                            playerPokemon = t1SpeCaseResult[0];
                            opponentPokemon = t1SpeCaseResult[1];


                            if (usedMove.Type != "item")
                            {
                                await HandleUseMoveResult(turnContext, opponentConnectionId);
                                //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                                await Task.Delay(CalculateDelay(turnContext) + 500);
                                turnContext = new();
                            }


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
                            player.Team[indexPlayer] = playerPokemon;

                            if (usedMove.Type == "item")
                            {
                                await this._mongoPlayerRepository.UpdatePokemonTeamAsync(playerPokemon, player);
                                await Clients.Caller.SendAsync("useItemResult", turnContext, indexPlayer);
                                if (indexPlayer != 0) playerPokemon = playerPokemonMongo;
                                await Task.Delay(CalculateDelay(turnContext) + 500);
                                turnContext = new();
                            }

                            if (playerPokemon.Substitute != null)
                            {
                                playerPokemonMongo = (PokemonTeam)playerPokemon.Clone();
                                playerPokemon = (PokemonTeam)playerPokemonMongo.Substitute.Clone();
                            }

                            if (await ManageSpecialCasesAfterMove(opponentMongo, opponentPokemon, playerPokemon, player, turnContext)) {
                                player = await _mongoPlayerRepository.UpdatePokemonTeamAsync(playerPokemon, player);
                                opponentMongo = await _mongoWildPokemonRepository.UpdatePokemonTeamAsync(opponentPokemon, opponentMongo);
                                await HandleUseMoveResult(turnContext, opponentConnectionId);
                                //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                                await Task.Delay(CalculateDelay(turnContext) + 500);
                                await FinishFight(player, opponentMongo, true);
                                return;
                            }
                            else
                            {
                                if (opponentPokemon.SpecialCases.Contains("Ejected")) opponentPokemon.SpecialCases.Remove("Ejected");
                                if (playerPokemon.SpecialCases.Contains("Ejected")) playerPokemon.SpecialCases.Remove("Ejected");
                                if (opponentPokemon.SpecialCases.Contains("Teleport")) opponentPokemon.SpecialCases.Remove("Teleport");
                                if (playerPokemon.SpecialCases.Contains("Teleport")) playerPokemon.SpecialCases.Remove("Ejected");
                            }
                        }
                    }
                }



                if (catchValue == -1)//Pokémon capturé
                {
                    await Task.Delay(4000);
                    opponentMongo.Team[0] = opponentPokemon;
                    await Clients.Caller.SendAsync("caughtPokemon", opponentMongo);
                    skipTurn = true;
                }
                else
                {
                    turnContext = new();
                    if (opponentPokemon.CurrHp <= 0)//Pokémon mort
                    {
                        turnContext.AddPrioMessage(opponentPokemon.NameFr + " est K.O");

                        if (opponentPokemon.Substitute != null)
                        {
                            opponentPokemonMongo.Substitute = null;
                            opponentPokemon = opponentPokemonMongo;
                            await HandleUseMoveResult(turnContext, opponentConnectionId);
                            //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                            await Task.Delay(CalculateDelay(turnContext));
                        }
                        else
                        {
                            await HandleUseMoveResult(turnContext, opponentConnectionId);
                            //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                            await Task.Delay(CalculateDelay(turnContext));
                            player = await _mongoPlayerRepository.UpdatePokemonTeamAsync(playerPokemon, player);
                            opponentMongo = await _mongoWildPokemonRepository.UpdatePokemonTeamAsync(opponentPokemon, opponentMongo);
                            await FinishFight(player, opponentMongo);
                            return;
                        }
                    }
                    else
                    {
                        if (!skipTurn)
                        {
                            if (!opponentPokemon.IsFlinched)
                            {
                                if (opponentMove.NameFr == "Métronome")
                                {
                                    turnContext.AddMessage(opponentPokemon.NameFr + " lance Métronome");
                                    PokemonTeamMove newMove = PokemonMoveSelector.ConvertToTeamMove(await GetMoveExtApi.GetMetronomeMove());
                                    opponentMove = newMove;
                                    await HandleUseMoveResult(turnContext, opponentConnectionId);
                                    //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                                    await Task.Delay(CalculateDelay(turnContext));
                                    turnContext = new();
                                }
                                if (opponentMove.NameFr == "Mimique")
                                {
                                    turnContext.AddMessage(opponentPokemon.NameFr + " lance Mimique");
                                    opponentMove = usedMove;
                                    await HandleUseMoveResult(turnContext, opponentConnectionId);
                                    //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                                    await Task.Delay(CalculateDelay(turnContext));
                                    turnContext = new();
                                }


                                turnContext.AddPrioMessage(opponentPokemon.NameFr + " lance " + opponentMove.NameFr);
                                if (FightPerformMove.SpecialCaseFail(opponentPokemon, opponentMove, playerPokemon, usedMove))
                                {
                                    turnContext.AddMessage("Mais cela michou");
                                }
                                else
                                {

                                    if (FightPerformMove.IsFieldChangeMove(opponentMove)) opponentPokemon = FightPerformMove.FieldChangeMove(opponentMove, opponentPokemon, turnContext);
                                    PokemonTeam[] t2Result = FightPerformMove.PerformMove(opponentPokemon, playerPokemon, opponentMove, player.FieldChange, turnContext, false);
                                    playerPokemon = t2Result[1];
                                    opponentPokemon = t2Result[0];
                                    PokemonTeam[] t2SpeCaseResult = FightPerformMove.PerformSpecialCaseMove(opponentPokemon, playerPokemon, opponentMove, turnContext, usedMove);
                                    playerPokemon = t2SpeCaseResult[1];
                                    opponentPokemon = t2SpeCaseResult[0];
                                    if (await ManageSpecialCasesAfterMove(opponentMongo, opponentPokemon, playerPokemon, player, turnContext))
                                    {
                                        player = await _mongoPlayerRepository.UpdatePokemonTeamAsync(playerPokemon, player);
                                        opponentMongo = await _mongoWildPokemonRepository.UpdatePokemonTeamAsync(opponentPokemon, opponentMongo);
                                        await HandleUseMoveResult(turnContext, opponentConnectionId);
                                        //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                                        await Task.Delay(CalculateDelay(turnContext) + 500);
                                        await FinishFight(player, opponentMongo, true);
                                        return;
                                    }
                                    else
                                    {
                                        if (opponentPokemon.SpecialCases.Contains("Ejected")) opponentPokemon.SpecialCases.Remove("Ejected");
                                        if (playerPokemon.SpecialCases.Contains("Ejected")) playerPokemon.SpecialCases.Remove("Ejected");
                                        if (opponentPokemon.SpecialCases.Contains("Teleport")) opponentPokemon.SpecialCases.Remove("Teleport");
                                        if (playerPokemon.SpecialCases.Contains("Teleport")) playerPokemon.SpecialCases.Remove("Ejected");
                                    }
                                }
                            }
                            else
                            {
                                turnContext.AddPrioMessage("La peur empêche " + opponentPokemon.NameFr + " d'attaquer");
                            }



                            await HandleUseMoveResult(turnContext, opponentConnectionId);
                            //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                            await Task.Delay(CalculateDelay(turnContext));
                            turnContext = new();
                            if (playerPokemon.CurrHp <= 0)
                            {
                                string message = playerPokemon.NameFr + " est K.O";
                                if (playerPokemonMongo.Substitute != null)
                                {
                                    playerPokemonMongo.Substitute = null;
                                    playerPokemon = playerPokemonMongo;
                                    await HandleUseMoveResult(turnContext, opponentConnectionId);
                                    //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                                    await Task.Delay(CalculateDelay(turnContext));
                                }
                                else
                                {
                                    player = await _mongoPlayerRepository.UpdatePokemonTeamAsync(playerPokemon, player);
                                    opponentMongo = await _mongoWildPokemonRepository.UpdatePokemonTeamAsync(opponentPokemon, opponentMongo);
                                    if (player.Team.FirstOrDefault(x => x.CurrHp > 0) == null)
                                    {
                                        await FinishFight(player, opponentMongo);
                                        return;
                                    }
                                    else
                                    {
                                        await Clients.Caller.SendAsync("playerPokemonDeath", message);
                                        if (pvp) await Clients.Client(opponentConnectionId).SendAsync("waitingOpponent");
                                    }
                                }
                            }
                            if (opponentPokemon.CurrHp <= 0)//Pokémon mort
                            {
                                turnContext.AddPrioMessage(opponentPokemon.NameFr + " est K.O");

                                if (opponentPokemon.Substitute != null)
                                {
                                    opponentPokemonMongo.Substitute = null;
                                    opponentPokemon = opponentPokemonMongo;
                                    await HandleUseMoveResult(turnContext, opponentConnectionId);
                                    //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                                    await Task.Delay(CalculateDelay(turnContext));
                                }
                                else
                                {
                                    await HandleUseMoveResult(turnContext, opponentConnectionId);
                                    //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                                    await Task.Delay(CalculateDelay(turnContext));
                                    player = await _mongoPlayerRepository.UpdatePokemonTeamAsync(playerPokemon, player);
                                    opponentMongo = await _mongoWildPokemonRepository.UpdatePokemonTeamAsync(opponentPokemon, opponentMongo);
                                    await FinishFight(player, opponentMongo);
                                    return;
                                }


                            }
                            turnContext = new();
                            playerPokemon = FightAilmentMove.SufferAilment(playerPokemon, turnContext);
                            PokemonTeam[] specialCaseResponse1 = SufferSpecialCase(playerPokemon, opponentPokemon, turnContext);
                            specialCaseResponse1[0] = playerPokemon;
                            specialCaseResponse1[1] = opponentPokemon;
                            opponentPokemon = FightAilmentMove.SufferAilment(opponentPokemon, turnContext);
                            PokemonTeam[] specialCaseResponse2 = SufferSpecialCase(opponentPokemon, playerPokemon, turnContext);
                            specialCaseResponse2[0] = opponentPokemon;
                            specialCaseResponse2[1] = playerPokemon;
                            await HandleUseMoveResult(turnContext, opponentConnectionId);
                            //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                            await Task.Delay(CalculateDelay(turnContext));
                        }
                    }
                }
            }
            else
            {//Adversaire joue en premier 
                if (opponentMove.NameFr == "Métronome")
                {
                    turnContext.AddMessage(opponentPokemon.NameFr + " lance Métronome");
                    PokemonTeamMove newMove = PokemonMoveSelector.ConvertToTeamMove(await GetMoveExtApi.GetMetronomeMove());
                    opponentMove = newMove;
                    await HandleUseMoveResult(turnContext, opponentConnectionId);
                    //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                    await Task.Delay(CalculateDelay(turnContext));
                    turnContext = new();
                }


                if (opponentMove.Type == "item")
                {
                    opponentMongo.Items.FirstOrDefault(i => i.Name == opponentMove.NameFr).Number -= 1;
                    turnContext.AddPrioMessage(opponentMongo.Name + " utilise " + opponentMove.NameFr);
                }
                else if (opponentMove.Type == "swap") turnContext.AddPrioMessage(opponentMongo.Name + " change de Pokémon");
                else turnContext.AddPrioMessage(opponentPokemon.NameFr + " lance " + opponentMove.NameFr);

                    if (FightPriority.MoveMustBePlayedLast(opponentMove) || FightPerformMove.SpecialCaseFail(opponentPokemon, opponentMove, playerPokemon, usedMove))
                    {
                        turnContext.AddMessage("Mais cela michou");
                    }
                    else
                    {
                        if (opponentMove.NameFr == "swap")
                        {
                            await Clients.Caller.SendAsync("foeSwapPokemon", opponentPokemon, turnContext.PrioMessages[0]);
                            if (pvp) await Clients.Client(opponentConnectionId).SendAsync("swapPokemon", opponentPokemon, turnContext.PrioMessages[0]);
                            await Task.Delay(2000);
                        }
                        else
                        {
                            if (FightPerformMove.IsFieldChangeMove(opponentMove)) opponentPokemon = FightPerformMove.FieldChangeMove(opponentMove, opponentPokemon, turnContext);
                            if (indexOponnent != 0) opponentPokemon = opponentMongo.Team[indexOponnent];
                            PokemonTeam[] t1Result = FightPerformMove.PerformMove(opponentPokemon, playerPokemon, opponentMove, player.FieldChange, turnContext, false);
                            playerPokemon = t1Result[1];
                            opponentPokemon = t1Result[0];
                            PokemonTeam[] t1SpeCaseResult = FightPerformMove.PerformSpecialCaseMove(opponentPokemon, playerPokemon, usedMove, turnContext);
                            playerPokemon = t1SpeCaseResult[1];
                            opponentPokemon = t1SpeCaseResult[0];

                            if (opponentMove.Type != "item")
                            {
                                await HandleUseMoveResult(turnContext, opponentConnectionId);
                                //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                                await Task.Delay(CalculateDelay(turnContext) + 500);
                                turnContext = new();
                            }

                            opponentPokemon = await CheckLevelUp(opponentPokemon);
                            opponentMongo.Team[indexOponnent] = opponentPokemon;

                            if (opponentMove.Type == "item")
                            {
                                await this._mongoPlayerRepository.UpdatePokemonTeamAsync(playerPokemon, player);
                                await Clients.Caller.SendAsync("useMoveResult", turnContext);
                                await Clients.Client(opponentConnectionId).SendAsync("useItemResult", turnContext, indexOponnent);
                                if (indexOponnent != 0) opponentPokemon = opponentPokemonMongo;
                                await Task.Delay(CalculateDelay(turnContext) + 500);
                                turnContext = new();
                            }

                            if(opponentPokemon.Substitute != null)
                            {
                                opponentPokemonMongo = (PokemonTeam)opponentPokemon.Clone();
                                opponentPokemon = (PokemonTeam)opponentPokemonMongo.Substitute.Clone();
                            }

                            if (await ManageSpecialCasesAfterMove(opponentMongo, opponentPokemon, playerPokemon, player, turnContext))
                            {
                                player = await _mongoPlayerRepository.UpdatePokemonTeamAsync(playerPokemon, player);
                                opponentMongo = await _mongoWildPokemonRepository.UpdatePokemonTeamAsync(opponentPokemon, opponentMongo);
                                await HandleUseMoveResult(turnContext, opponentConnectionId);
                                //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                                await Task.Delay(CalculateDelay(turnContext) + 500);
                                await FinishFight(player, opponentMongo, true);
                                return;
                            }
                            else
                            {
                                if (opponentPokemon.SpecialCases.Contains("Ejected")) opponentPokemon.SpecialCases.Remove("Ejected");
                                if (playerPokemon.SpecialCases.Contains("Ejected")) playerPokemon.SpecialCases.Remove("Ejected");
                                if (opponentPokemon.SpecialCases.Contains("Teleport")) opponentPokemon.SpecialCases.Remove("Teleport");
                                if (playerPokemon.SpecialCases.Contains("Teleport")) playerPokemon.SpecialCases.Remove("Ejected");
                            }

                            await HandleUseMoveResult(turnContext, opponentConnectionId);
                        }
                    }
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
                            player = await _mongoPlayerRepository.UpdatePokemonTeamAsync(playerPokemon, player);
                            opponentMongo = await _mongoWildPokemonRepository.UpdatePokemonTeamAsync(opponentPokemon, opponentMongo);
                            if (player.Team.FirstOrDefault(x => x.CurrHp > 0) == null)
                            {
                                await FinishFight(player, opponentMongo);
                                return;
                            }
                            else
                            {
                                await Clients.Caller.SendAsync("playerPokemonDeath", message);
                            if (pvp) await Clients.Client(opponentConnectionId).SendAsync("waitingOpponent");
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
                                await HandleUseMoveResult(turnContext, opponentConnectionId);
                                //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                                await Task.Delay(CalculateDelay(turnContext));
                                turnContext = new();
                            }

                            if (usedMove.NameFr == "Mimique")
                            {
                                turnContext.AddMessage(playerPokemon.NameFr + " lance Mimique");
                                usedMove = opponentMove;
                                await HandleUseMoveResult(turnContext, opponentConnectionId);
                                //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                                await Task.Delay(CalculateDelay(turnContext));
                                turnContext = new();
                            }

                            if (usedMove.NameFr == "Copie")
                            {
                                //opponentMove
                                var move = playerPokemon.Moves.FirstOrDefault(o => o.NameFr == "Copie");

                                int r = Array.IndexOf(playerPokemon.Moves, move);

                                playerPokemon.SavedMove = move;
                                playerPokemon.SavedMoveSlot = r;
                                playerPokemon.Moves[r] = opponentMove;

                            }

                            turnContext.AddPrioMessage(playerPokemon.NameFr + " lance " + usedMove.NameFr);

                            if (FightPerformMove.SpecialCaseFail(playerPokemon, usedMove, opponentPokemon, opponentMove))
                            {
                                turnContext.AddMessage("Mais cela michou");
                            }
                            else
                            {

                                if (FightPerformMove.IsFieldChangeMove(usedMove)) player = FightPerformMove.FieldChangeMove(usedMove, player, turnContext);
                                PokemonTeam[] t2Result = FightPerformMove.PerformMove(playerPokemon, opponentPokemon, usedMove, opponentPokemon.FieldChange, turnContext, true, player);
                                playerPokemon = t2Result[0];
                                opponentPokemon = t2Result[1];
                                PokemonTeam[] t2SpeCaseResult = FightPerformMove.PerformSpecialCaseMove(playerPokemon, opponentPokemon, usedMove, turnContext, opponentMove);
                                playerPokemon = t2SpeCaseResult[0];
                                opponentPokemon = t2SpeCaseResult[1];
                                if (await ManageSpecialCasesAfterMove(opponentMongo, opponentPokemon, playerPokemon, player, turnContext))
                                {
                                    player = await _mongoPlayerRepository.UpdatePokemonTeamAsync(playerPokemon, player);
                                    opponentMongo = await _mongoWildPokemonRepository.UpdatePokemonTeamAsync(opponentPokemon, opponentMongo);
                                    await HandleUseMoveResult(turnContext, opponentConnectionId);
                                    //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                                    await Task.Delay(CalculateDelay(turnContext) + 500);
                                    await FinishFight(player, opponentMongo, true);
                                    return;
                                }
                                else
                                {
                                    if (opponentPokemon.SpecialCases.Contains("Ejected")) opponentPokemon.SpecialCases.Remove("Ejected");
                                    if (playerPokemon.SpecialCases.Contains("Ejected")) playerPokemon.SpecialCases.Remove("Ejected");
                                    if (opponentPokemon.SpecialCases.Contains("Teleport")) opponentPokemon.SpecialCases.Remove("Teleport");
                                    if (playerPokemon.SpecialCases.Contains("Teleport")) playerPokemon.SpecialCases.Remove("Ejected");
                                }

                                player.FieldChangeCount--;
                                if (player.FieldChangeCount <= 0)
                                {
                                    player.FieldChangeCount = null;
                                    player.FieldChange = null;
                                }

                                if (opponentPokemon.CurrHp <= 0)
                                {
                                    turnContext.AddMessage(opponentPokemon.NameFr + " est K.O");

                                    if (opponentPokemon.Substitute != null)
                                    {
                                        opponentPokemonMongo.Substitute = null;
                                        opponentPokemon = opponentPokemonMongo;
                                        await HandleUseMoveResult(turnContext, opponentConnectionId);
                                        //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                                        await Task.Delay(CalculateDelay(turnContext));
                                    }
                                    else
                                    {
                                        await HandleUseMoveResult(turnContext, opponentConnectionId);
                                        //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                                        await Task.Delay(CalculateDelay(turnContext));
                                        player = await _mongoPlayerRepository.UpdatePokemonTeamAsync(playerPokemon, player);
                                        opponentMongo = await _mongoWildPokemonRepository.UpdatePokemonTeamAsync(opponentPokemon, opponentMongo);
                                        await FinishFight(player, opponentMongo);
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
                        PokemonTeam[] specialCaseResponse1 = SufferSpecialCase(playerPokemon, opponentPokemon, turnContext);
                        specialCaseResponse1[0] = playerPokemon;
                        specialCaseResponse1[1] = opponentPokemon;
                        opponentPokemon = FightAilmentMove.SufferAilment(opponentPokemon, turnContext);
                        PokemonTeam[] specialCaseResponse2 = SufferSpecialCase(opponentPokemon, playerPokemon, turnContext);
                        specialCaseResponse2[0] = opponentPokemon;
                        specialCaseResponse2[1] = playerPokemon;

                        await HandleUseMoveResult(turnContext, opponentConnectionId);
                        //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                        await Task.Delay(CalculateDelay(turnContext));
                        turnContext = new();
                        if (opponentPokemon.CurrHp <= 0)
                        {
                            turnContext.AddMessage(opponentPokemon.NameFr + " est K.O");

                            if (opponentPokemon.Substitute != null)
                            {
                                opponentPokemonMongo.Substitute = null;
                                opponentPokemon = opponentPokemonMongo;
                                await HandleUseMoveResult(turnContext, opponentConnectionId);
                                //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                                await Task.Delay(CalculateDelay(turnContext));
                            }
                            else
                            {
                                await HandleUseMoveResult(turnContext, opponentConnectionId);
                                //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                                await Task.Delay(CalculateDelay(turnContext));
                                player = await _mongoPlayerRepository.UpdatePokemonTeamAsync(playerPokemon, player);
                                opponentMongo = await _mongoWildPokemonRepository.UpdatePokemonTeamAsync(opponentPokemon, opponentMongo);
                                await FinishFight(player, opponentMongo);
                                return;
                            }


                        }
                        if (playerPokemon.CurrHp <= 0)
                        {
                            if (playerPokemonMongo.Substitute != null)
                            {
                                playerPokemonMongo.Substitute = null;
                                playerPokemon = playerPokemonMongo;
                                await HandleUseMoveResult(turnContext, opponentConnectionId);
                                //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                                await Task.Delay(CalculateDelay(turnContext));
                            }
                            else
                            {
                                string message = playerPokemon.NameFr + " est K.O";
                                player = await _mongoPlayerRepository.UpdatePokemonTeamAsync(playerPokemon, player);
                                opponentMongo = await _mongoWildPokemonRepository.UpdatePokemonTeamAsync(opponentPokemon, opponentMongo);
                                if (player.Team.FirstOrDefault(x => x.CurrHp > 0) == null)
                                {
                                    await FinishFight(player, opponentMongo);
                                    return;
                                }
                                else
                                {
                                    await Clients.Caller.SendAsync("playerPokemonDeath", message);
                                if (pvp) await Clients.Client(opponentConnectionId).SendAsync("waitingOpponent");
                            }
                            }
                        }
                        turnContext = new();
                    }
                
            }

            if (!skipTurn)
            {
                if (opponentPokemon.MultiTurnsMove != null)
                {
                    PokemonTeam[] response = FightPerformMove.PerformMultiTurnMove(playerPokemon, opponentPokemon, opponentPokemon.MultiTurnsMove, turnContext);
                    playerPokemon = response[0];
                    opponentPokemon = response[1];
                    opponentPokemon.MultiTurnsMoveCount--;
                    if (opponentPokemon.MultiTurnsMoveCount <= 0)
                    {
                        if (opponentPokemon.MultiTurnsMove.NameFr == "Entrave")
                        {
                            turnContext.AddPrioMessage(opponentPokemon.CantUseMoves[0] + " n'est plus sous entrave");
                            opponentPokemon.CantUseMoves = [];
                        }
                        opponentPokemon.MultiTurnsMoveCount = null;
                        opponentPokemon.MultiTurnsMove = null;
                    }
                    await HandleUseMoveResult(turnContext, opponentConnectionId);
                    //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                    await Task.Delay(CalculateDelay(turnContext));
                    turnContext = new();
                }

                if (opponentPokemon.CurrHp <= 0)
                {
                    turnContext.AddMessage(opponentPokemon.NameFr + " est K.O");

                    if (opponentPokemon.Substitute != null)
                    {
                        opponentPokemonMongo.Substitute = null;
                        opponentPokemon = opponentPokemonMongo;
                        await HandleUseMoveResult(turnContext, opponentConnectionId);
                        //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                        await Task.Delay(CalculateDelay(turnContext));
                    }
                    else
                    {
                        await HandleUseMoveResult(turnContext, opponentConnectionId);
                        //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                        await Task.Delay(CalculateDelay(turnContext));
                        turnContext = new();
                        player = await _mongoPlayerRepository.UpdatePokemonTeamAsync(playerPokemon, player);
                        opponentMongo = await _mongoWildPokemonRepository.UpdatePokemonTeamAsync(opponentPokemon, opponentMongo);
                        await FinishFight(player, opponentMongo);
                        return;
                    }


                }

                if (playerPokemon.MultiTurnsMove != null)
                {
                    PokemonTeam[] response = FightPerformMove.PerformMultiTurnMove(opponentPokemon, playerPokemon, playerPokemon.MultiTurnsMove, turnContext);
                    opponentPokemon = response[0];
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
                    await HandleUseMoveResult(turnContext, opponentConnectionId);
                    //await Clients.Caller.SendAsync("useMoveResult", turnContext);
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
                        await HandleUseMoveResult(turnContext, opponentConnectionId);
                        //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                        await Task.Delay(CalculateDelay(turnContext));
                    }
                    else
                    {
                        player = await _mongoPlayerRepository.UpdatePokemonTeamAsync(playerPokemon, player);
                        opponentMongo = await _mongoWildPokemonRepository.UpdatePokemonTeamAsync(opponentPokemon, opponentMongo);
                        if (player.Team.FirstOrDefault(x => x.CurrHp > 0) == null)
                        {
                            await FinishFight(player, opponentMongo);
                            return;
                        }
                        else
                        {
                            await Clients.Caller.SendAsync("playerPokemonDeath", message);
                            if (pvp) await Clients.Client(opponentConnectionId).SendAsync("waitingOpponent");
                        }
                    }

                    turnContext = new();
                }

                opponentPokemon.FieldChangeCount--;
                if (opponentPokemon.FieldChangeCount <= 0)
                {
                    if (opponentPokemon.FieldChange == "Brume") turnContext.AddMessage("La brume disparaît");
                    if (opponentPokemon.FieldChange == "Mur Lumière") turnContext.AddMessage("Mur lumière n'est plus actif");
                    if (opponentPokemon.FieldChange == "Protection") turnContext.AddMessage("Protection n'est plus actif");
                    opponentPokemon.FieldChangeCount = null;
                    opponentPokemon.FieldChange = null;
                    await HandleUseMoveResult(turnContext, opponentConnectionId);
                    //await Clients.Caller.SendAsync("useMoveResult", turnContext);
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
                    await HandleUseMoveResult(turnContext, opponentConnectionId);
                    //await Clients.Caller.SendAsync("useMoveResult", turnContext);
                    await Task.Delay(CalculateDelay(turnContext));
                    turnContext = new();
                }

                if (!FightDamageMove.NeedStackDamages(playerPokemon))
                {
                    playerPokemon.BlowsTaken = 0;
                }
            }

            playerPokemon.IsFlinched = false;
            opponentPokemon.IsFlinched = false;
            
            if(playerPokemonMongo.Substitute != null)
            {
                playerPokemonMongo.Substitute = playerPokemon;
            }
            else
            {
                playerPokemonMongo = playerPokemon;
            }

            if (opponentPokemonMongo.Substitute != null)
            {
                opponentPokemonMongo.Substitute = opponentPokemon;
            }
            else
            {
                opponentPokemonMongo = opponentPokemon;
            }
            player.Team[0] = playerPokemonMongo;
            opponentMongo.Team[0] = opponentPokemonMongo;
            await _mongoPlayerRepository.UpdateAsync(player);
            //PlayerMongo updatedPlayer = await _mongoPlayerRepository.UpdatePokemonTeamAsync(playerPokemonMongo, player);
            if(pvp) await _mongoPlayerRepository.UpdateAsync(opponentMongo);
            else opponentMongo = await _mongoWildPokemonRepository.UpdatePokemonTeamAsync(opponentPokemon, opponentMongo);

            await Clients.Caller.SendAsync("turnFinished", player, opponentMongo);
            await Clients.Client(opponentConnectionId).SendAsync("turnFinished", opponentMongo, player);
        }

        public async Task HandleUseMoveResult(TurnContext turnContext, string opponentConnectionId)
        {
            await Clients.Caller.SendAsync("useMoveResult", turnContext);
            if(opponentConnectionId != "")
            {
                PokemonChanges player = turnContext.Opponent;
                PokemonChanges opponent = turnContext.Player;

                turnContext.Player = player;
                turnContext.Opponent = opponent;
                await Clients.Client(opponentConnectionId).SendAsync("useMoveResult", turnContext);
            }
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
