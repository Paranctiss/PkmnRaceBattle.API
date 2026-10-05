using PkmnRaceBattle.API.Helpers.Randomness;
using PkmnRaceBattle.API.Helper;
using PkmnRaceBattle.API.Helpers.PokemonGeneration;
using PkmnRaceBattle.Domain.Models.PlayerMongo;
using PkmnRaceBattle.Domain.Models.PokemonMongo;
using System.Text.RegularExpressions;
using Microsoft.AspNet.SignalR.Messaging;
using Microsoft.AspNet.SignalR.Tracing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.SignalR;
using PkmnRaceBattle.Domain.Models.BracketMongo;

namespace PkmnRaceBattle.API.Hub
{
    public partial class GameHub
    {
        public async Task BuildTournament(string gameCode)
        {
            List<PlayerMongo> playersInRoom = await _mongoPlayerRepository.GetByRoomId(gameCode);

            //Réarangement aléatoire pour l'arbre
            playersInRoom = playersInRoom.OrderBy(x => GameRandom.Next(RandomPurpose.Generation, 0, int.MaxValue)).ToList();

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
            for (int i = 1; i <= playersInRoom.Count / 2; i++)
            {
                RoundMongo roundMongo = new RoundMongo();
                roundMongo.RoundNumber = (playersInRoom.Count / 2) + 1 - i;
                int nbPlayers = i * 2;
                for (int y = 0; y < nbPlayers; y++)
                {
                    roundMongo.PlayersInRace.Add("?");
                }
                bracket.Rounds.Add(roundMongo);
            }
            bracket.Rounds[bracket.Rounds.Count - 1].PlayersInRace = [];
            foreach (PlayerMongo playerBracket in bracket.Players)
            {
                bracket.Rounds[bracket.Rounds.Count - 1].PlayersInRace.Add(playerBracket._id);
            }
            await _mongoBracketRepository.CreateAsync(bracket);

            await Clients.Group(gameCode).SendAsync("bracketCreated", bracket);
        }
    }
}
