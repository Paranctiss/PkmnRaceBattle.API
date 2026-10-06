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

            // log2(N) tours, de la finale (Rounds[0]) au premier tour (Rounds[^1]) ; RoundNumber 1 = premier tour
            int nbRounds = 1;
            while ((1 << nbRounds) < playersInRoom.Count) nbRounds++;
            for (int roundNumber = nbRounds; roundNumber >= 1; roundNumber--)
            {
                RoundMongo roundMongo = new RoundMongo();
                roundMongo.RoundNumber = roundNumber;
                int nbSlots = 1 << (nbRounds - roundNumber + 1);
                for (int y = 0; y < nbSlots; y++)
                {
                    roundMongo.PlayersInRace.Add("?");
                }
                bracket.Rounds.Add(roundMongo);
            }

            // Un joueur par duel d'abord, puis les adversaires : les places restantes ("?") sont des exemptions
            List<string> firstRound = bracket.Rounds[^1].PlayersInRace;
            int nbMatches = firstRound.Count / 2;
            for (int i = 0; i < playersInRoom.Count; i++)
            {
                int slot = i < nbMatches ? i * 2 : (i - nbMatches) * 2 + 1;
                firstRound[slot] = playersInRoom[i]._id;
            }
            // Un joueur sans adversaire passe directement au tour suivant
            for (int match = 0; match < nbMatches; match++)
            {
                if (firstRound[match * 2 + 1] == "?") PlaceTournamentWinner(bracket, firstRound[match * 2]);
            }

            await _mongoBracketRepository.CreateAsync(bracket);

            await Clients.Group(gameCode).SendAsync("bracketCreated", bracket);
        }

        // Qualifie le vainqueur d'un duel du tour en cours ; passe au tour suivant quand tous les duels sont joués
        private static void PlaceTournamentWinner(BracketMongo bracket, string winnerId)
        {
            int current = bracket.Rounds.Count - bracket.NbTurn;
            if (current < 0) return;
            int position = bracket.Rounds[current].PlayersInRace.IndexOf(winnerId);
            if (position == -1) return;

            if (current == 0)
            {
                // Finale gagnée : NbTurn dépasse le nombre de tours, le tournoi est terminé
                bracket.Champion = winnerId;
                bracket.NbTurn++;
                return;
            }

            List<string> nextRound = bracket.Rounds[current - 1].PlayersInRace;
            nextRound[position / 2] = winnerId;
            if (!nextRound.Contains("?")) bracket.NbTurn++;
        }

        private async Task AdvanceTournament(string gameCode, string winnerId)
        {
            BracketMongo bracket = await _mongoBracketRepository.GetByRoomId(gameCode);
            if (bracket == null) return;

            int turnBefore = bracket.NbTurn;
            PlaceTournamentWinner(bracket, winnerId);
            await _mongoBracketRepository.UpdateAsync(bracket);

            // Tour terminé : tout le monde revient au tableau (l'hôte relance le tour suivant)
            if (bracket.NbTurn != turnBefore) await Clients.Group(gameCode).SendAsync("bracketCreated", bracket);
        }
    }
}
