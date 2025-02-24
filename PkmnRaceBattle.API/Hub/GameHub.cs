namespace PkmnRaceBattle.API.Hub
{
    using Microsoft.AspNet.SignalR.Messaging;
    using Microsoft.AspNetCore.SignalR;
    using PkmnRaceBattle.API.Helper;
    using PkmnRaceBattle.API.Helpers.MoveManager;
    using PkmnRaceBattle.API.Helpers.MoveManager.Fights;
    using PkmnRaceBattle.API.Helpers.PokemonGeneration;
    using PkmnRaceBattle.API.Helpers.StatsCalculator;
    using PkmnRaceBattle.Application.Contracts;
    using PkmnRaceBattle.Domain.Models;
    using PkmnRaceBattle.Domain.Models.PlayerMongo;
    using PkmnRaceBattle.Domain.Models.PokemonMongo;
    using PkmnRaceBattle.Domain.Models.RoomMongo;

    public class GameHub : Hub
    {
        private readonly IMongoRoomRepository _mongoRoomRepository;
        private readonly IMongoPlayerRepository _mongoPlayerRepository;
        private readonly IMongoPokemonRepository _mongoPokemonRepository;
        private readonly IMongoWildPokemonRepository _mongoWildPokemonRepository;

        public GameHub(
            IMongoRoomRepository mongoRoomRepository,
            IMongoPlayerRepository mongoPlayerRepository,
            IMongoPokemonRepository mongoPokemonRepository,
            IMongoWildPokemonRepository mongoWildPokemonRepository)
        {
            _mongoRoomRepository = mongoRoomRepository;
            _mongoPlayerRepository = mongoPlayerRepository;
            _mongoPokemonRepository = mongoPokemonRepository;
            _mongoWildPokemonRepository = mongoWildPokemonRepository;
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
            PokemonTeam pokemonTeam = GenerateNewPokemon.GenerateNewPokemonTeam(starterInfos, 5, 5);

            PlayerMongo playerMongo = new PlayerMongo();
            playerMongo.Name = username;
            playerMongo.RoomId = gameCode;
            playerMongo.Team = [pokemonTeam];
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

        public async Task GetNewTurn(string userId)
        {
            string[] turnTypes = ["WildFight", "TrainerFight", "PokeCenter", "PokeShop"];
            Random rnd = new Random();
            string turnType = turnTypes[turnTypes.Length - 1];

            turnType = "WildFight"; //à enlever

            switch (turnType) {

                case "WildFight":
                    await GetWildFight(userId);
                    break;
            }
        }
        
        public async Task GetWildFight(string userId)
        {
            PlayerMongo player = await _mongoPlayerRepository.GetByPlayerIdAsync(userId);
            int levelAvg = player.GetAverageLevel();
            PokemonMongo rndPokemon = await _mongoPokemonRepository.GetRandom();
            PokemonTeam wildPokemon = GenerateNewPokemon.GenerateNewPokemonTeam(rndPokemon, levelAvg-3, levelAvg-1);
            await _mongoWildPokemonRepository.CreateAsync(wildPokemon);
            await Clients.Caller.SendAsync("responseWildFight", wildPokemon);
        }

        public async Task AddPokemonToTeam(string userId, string wildPokemonId, int index)
        {
            PlayerMongo player = await _mongoPlayerRepository.GetByPlayerIdAsync(userId);
            PokemonTeam wildPokemon = await _mongoWildPokemonRepository.GetByIdAsync(wildPokemonId);
            if (index == 0) {
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
            await FinishFight(userId, wildPokemonId);

        }

        public async Task ReplacePokemon(string userId, string pokemonId, string wildPokemonId)
        {
            PlayerMongo player = await _mongoPlayerRepository.GetByPlayerIdAsync(userId);

            PokemonTeam pokemon = player.Team.FirstOrDefault(x => x.Id == pokemonId);

            PokemonTeam pokemonSwap = player.Team[0];

            // Échanger les positions
            int indexOfPokemon = Array.IndexOf(player.Team, pokemon);
            player.Team[0] = pokemon;
            player.Team[indexOfPokemon] = pokemonSwap;

            // Sauvegarder les modifications si nécessaire
            await _mongoPlayerRepository.UpdateAsync(player);

            await UseMove(userId, pokemon.Id, "swap:", wildPokemonId, true);
        }

        public async Task FinishFight(string userId, string wildPokemonId)
        {
            PlayerMongo player = await _mongoPlayerRepository.GetByPlayerIdAsync(userId);
            PokemonTeam wildPokemon = await _mongoWildPokemonRepository.GetByIdAsync(wildPokemonId);

            if (player.Team.FirstOrDefault(t => t.CurrHp > 0) != null) { 
            
                for(int i = 0; i<=player.Team.Length-1; i++)
                {
                    PokemonTeam team = player.Team[i];
                    team.AtkChanges = 0;
                    team.AtkSpeChanges = 0;
                    team.DefChanges = 0;
                    team.DefSpeChanges = 0;
                    team.SpeedChanges = 0;
                    if (team.HavePlayed && team.CurrHp > 0)
                    {
                        List<MoveMongo> movesToLearn = new List<MoveMongo>(); 
                        bool evolvedThisTurn = false;
                        string learnedMove = "";
                        team.CurrXP += PokemonExperienceCalculator.ExpGained(wildPokemon, true, false, 0);
                        team.CurrXP += 500;
                        team.HavePlayed = false;
                        int oldLevel = team.Level;
                        while (PokemonExperienceCalculator.ExpToNextLevel(team) < 0)
                        {
                            team.Level++;

                            PokemonMongo pokemonBase;

                            if(team.EvolutionDetails.MinLevel != null && team.EvolutionDetails.MinLevel <= team.Level)
                            {
                                pokemonBase = await _mongoPokemonRepository.GetPokemonMongoByOGName(team.EvolutionDetails.PokemonName);

                                PokemonTeam EvolvedPokemon = PokemonBaseToTeam.ConvertBaseToTeam(pokemonBase, team.Level, team.IsShiny);
                                EvolvedPokemon.Moves = team.Moves;
                                EvolvedPokemon.CurrHp = team.CurrHp + (EvolvedPokemon.BaseHp - team.BaseHp);
                                team = EvolvedPokemon;
                                evolvedThisTurn = true;
                            }
                            else
                            {
                                pokemonBase = await _mongoPokemonRepository.GetPokemonMongoById(team.IdDex);
                            }

                            if(pokemonBase.Moves.FirstOrDefault(x => x.LearnedAtLvl == team.Level) != null)
                            {
                                if(team.Moves.Length < 4)
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
                            if (evolvedThisTurn) message = player.Team[i].NameFr + " monte niveau "+ team.Level+ "|" + player.Team[i].NameFr + " a évolué en " + team.NameFr;
                            else message = team.NameFr + " monte niveau " + team.Level;

                            if (learnedMove != "") message += "| " + player.Team[i].NameFr + " apprend " + learnedMove;

                            await Clients.Caller.SendAsync("pokemonLevelUp", message, team, movesToLearn);
                        }
                    }
                    player.Team[i] = team;
                }
            }
            else
            {
                foreach (var team in player.Team) {
                    team.AtkChanges = 0;
                    team.AtkSpeChanges = 0;
                    team.DefChanges = 0;
                    team.DefSpeChanges = 0;
                    team.SpeedChanges = 0;
                    team.CurrHp = team.BaseHp;
                }
                //Combat perdu go heal + diviser l'argent en 2
                string message = "Vous n'avez plus de pokémon en forme, vous perdez X pokédolz";
                await Clients.Caller.SendAsync("playerLooseFight", message);
                await Task.Delay(3000);

            }
            await this._mongoPlayerRepository.UpdateAsync(player);
            await GetNewTurn(player._id);
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
        public async Task UseMove(string playerId, string playerPokemonId, string usedMoveName, string wildPokemonId, bool isAttacking)
        {
            TurnContext turnContext = new TurnContext();
            PlayerMongo player = await _mongoPlayerRepository.GetByPlayerIdAsync(playerId);
            PokemonTeam wildPokemon = await _mongoWildPokemonRepository.GetByIdAsync(wildPokemonId);
            PokemonTeamMove usedMove;
            if (usedMoveName.StartsWith("item:"))
            {
                usedMove = new PokemonTeamMove
                {
                    Accuracy = 100,
                    DamageType = "item",
                    NameFr = usedMoveName.Substring(5),
                    Power = 0,
                    Pp  = 1,
                    Priority = 6,
                    StatsChanges = [],
                    Id = 0,
                    Type = "item",
                    Name = usedMoveName.Substring(5),
                    Target = "item"
                };
                player.Items.FirstOrDefault(i => i.Name == usedMoveName.Substring(5)).Number -= 1;
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
                    usedMove = await _mongoPlayerRepository.GetPokemonTeamMoveByName(playerId, playerPokemonId, usedMoveName);
                }
            }

            PokemonTeam playerPokemon = await _mongoPlayerRepository.GetPlayerPokemonById(player._id, playerPokemonId);
            playerPokemon.HavePlayed = true;
            PokemonTeamMove opponentMove = AIChoseMove.GetARandomMove(wildPokemon);
            int catchValue = 0;

            playerPokemon = FightAilmentMove.TryRemoveAilment(playerPokemon, turnContext);
            wildPokemon = FightAilmentMove.TryRemoveAilment(wildPokemon, turnContext);

            if(FightPriority.IsPlayingFirst(playerPokemon, usedMove, wildPokemon, opponentMove))
            {//Joueur joue en premier
                if(usedMove.Type == "item") turnContext.AddPrioMessage(player.Name + " utilise " + usedMove.NameFr);
                else if(usedMove.Type == "swap") turnContext.AddPrioMessage(player.Name + " change de Pokémon");
                else turnContext.AddPrioMessage(playerPokemon.NameFr + " lance " + usedMove.NameFr);

                if (usedMove.NameFr.EndsWith("ball"))
                {
                    await Clients.Caller.SendAsync("launchBall", usedMove.NameFr, turnContext);
                    catchValue = FightCatch.TryCatchPokemon(wildPokemon, usedMove.NameFr);
                    await Task.Delay(1000);
                    await Clients.Caller.SendAsync("catchResult", catchValue);
                    await Task.Delay(1000);
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
                        PokemonTeam[] t1Result = FightPerformMove.PerformMove(playerPokemon, wildPokemon, usedMove, turnContext, true);

                        playerPokemon = t1Result[0];
                        wildPokemon = t1Result[1];
                        await Clients.Caller.SendAsync("useMoveResult", turnContext);
                        await Task.Delay(CalculateDelay(turnContext));
                    }

                }
                


                if(catchValue == -1)//Pokémon capturé
                {
                    await Task.Delay(4000);
                    await Clients.Caller.SendAsync("caughtPokemon", wildPokemon);
                }
                else
                {
                    turnContext = new();
                    if (wildPokemon.CurrHp <= 0)//Pokémon mort
                    {
                        turnContext.AddPrioMessage(wildPokemon.NameFr + " est K.O");
                        await Clients.Caller.SendAsync("deadOpponent", turnContext);
                        await _mongoPlayerRepository.UpdatePokemonTeamAsync(playerPokemon, player);
                        await FinishFight(playerId, wildPokemonId);
                        return;
                    }
                    else
                    {
                        if (!wildPokemon.IsFlinched)
                        {
                            turnContext.AddPrioMessage(wildPokemon.NameFr + " lance " + opponentMove.NameFr);
                            PokemonTeam[] t2Result = FightPerformMove.PerformMove(wildPokemon, playerPokemon, opponentMove, turnContext, false);
                            playerPokemon = t2Result[1];
                            wildPokemon = t2Result[0];
                        }
                        else
                        {
                            turnContext.AddPrioMessage("La peur empêche " + wildPokemon.NameFr + " d'attaquer");
                        }


                        playerPokemon = FightAilmentMove.SufferAilment(playerPokemon, turnContext);
                        wildPokemon = FightAilmentMove.SufferAilment(wildPokemon, turnContext);

                        await Clients.Caller.SendAsync("useMoveResult", turnContext);
                        await Task.Delay(CalculateDelay(turnContext));
                        turnContext = new();
                        if (playerPokemon.CurrHp <= 0)
                        {
                            string message = playerPokemon.NameFr + " est K.O";
                            await _mongoPlayerRepository.UpdatePokemonTeamAsync(playerPokemon, player);
                            if (player.Team.FirstOrDefault(x => x.CurrHp > 0) == null)
                            {
                                await FinishFight(playerId, wildPokemonId);
                                return;
                            }
                            else
                            {
                                await Clients.Caller.SendAsync("playerPokemonDeath", message);
                            }
                        }
                        if (wildPokemon.CurrHp <= 0)//Pokémon mort
                        {
                            turnContext.AddPrioMessage(wildPokemon.NameFr + " est K.O");
                            await Clients.Caller.SendAsync("deadOpponent", turnContext);
                            await _mongoPlayerRepository.UpdatePokemonTeamAsync(playerPokemon, player);
                            await FinishFight(playerId, wildPokemonId);
                            return;
                        }
                    }
                }
            }
            else
            {//IA joue en premier 
                turnContext.AddPrioMessage(wildPokemon.NameFr + " lance " + opponentMove.NameFr);
                PokemonTeam[] t1Result = FightPerformMove.PerformMove(wildPokemon, playerPokemon, opponentMove, turnContext, false);
                int playerOldHp = playerPokemon.CurrHp;
                int opponentOldHp = wildPokemon.CurrHp;
                playerPokemon = t1Result[1];
                wildPokemon = t1Result[0];
                await Clients.Caller.SendAsync("useMoveResult", turnContext);
                await Task.Delay(CalculateDelay(turnContext));
                turnContext = new();
                if (playerPokemon.CurrHp <= 0)
                {
                    string message = playerPokemon.NameFr + " est K.O";
                    await _mongoPlayerRepository.UpdatePokemonTeamAsync(playerPokemon, player);
                    if (player.Team.FirstOrDefault(x => x.CurrHp > 0) == null)
                    {
                        await FinishFight(playerId, wildPokemonId);
                        return;
                    }
                    else
                    {
                        await Clients.Caller.SendAsync("playerPokemonDeath", message);
                    }
                }
                else
                {
                    if (!playerPokemon.IsFlinched) {
                        turnContext.AddPrioMessage(playerPokemon.NameFr + " lance " + usedMove.NameFr);
                        PokemonTeam[] t2Result = FightPerformMove.PerformMove(playerPokemon, wildPokemon, usedMove, turnContext, true);
                        playerPokemon = t2Result[0];
                        wildPokemon = t2Result[1];
                    }
                    else
                    {
                        turnContext.AddPrioMessage("La peur empêche " + playerPokemon.NameFr + " d'attaquer");
                    }

                    playerPokemon = FightAilmentMove.SufferAilment(playerPokemon, turnContext);
                    wildPokemon = FightAilmentMove.SufferAilment(wildPokemon, turnContext);
                    await Clients.Caller.SendAsync("useMoveResult", turnContext);
                    await Task.Delay(CalculateDelay(turnContext));
                    turnContext = new();
                    if (wildPokemon.CurrHp <= 0)
                    {
                        turnContext.AddMessage(wildPokemon.NameFr + " est K.O");

                        await Clients.Caller.SendAsync("deadOpponent", turnContext);
                        await _mongoPlayerRepository.UpdatePokemonTeamAsync(playerPokemon, player);
                        await FinishFight(playerId, wildPokemonId);
                        return;
                    }
                    if (playerPokemon.CurrHp <= 0)
                    {
                        string message = playerPokemon.NameFr + " est K.O";
                        await _mongoPlayerRepository.UpdatePokemonTeamAsync(playerPokemon, player);
                        if (player.Team.FirstOrDefault(x => x.CurrHp > 0) == null)
                        {
                            await FinishFight(playerId, wildPokemonId);
                            return;
                        }
                        else
                        {
                            await Clients.Caller.SendAsync("playerPokemonDeath", message);
                        }
                    }
                }
            }


            playerPokemon.IsFlinched = false;
            wildPokemon.IsFlinched = false;
            PlayerMongo updatedPlayer = await _mongoPlayerRepository.UpdatePokemonTeamAsync(playerPokemon, player);
            await _mongoWildPokemonRepository.UpdateAsync(wildPokemon);


            await Clients.Caller.SendAsync("turnFinished", updatedPlayer, wildPokemon);

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
