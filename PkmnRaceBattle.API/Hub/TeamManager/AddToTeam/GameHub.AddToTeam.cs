using PkmnRaceBattle.API.Helper;
using PkmnRaceBattle.API.Helpers.PokemonGeneration;
using PkmnRaceBattle.API.Helpers.PokemonStates;
using PkmnRaceBattle.Domain.Models.PlayerMongo;
using PkmnRaceBattle.Domain.Models.PokemonMongo;
using System.Text.RegularExpressions;
using Microsoft.AspNet.SignalR.Messaging;
using Microsoft.AspNet.SignalR.Tracing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.SignalR;

namespace PkmnRaceBattle.API.Hub
{
    public partial class GameHub
    {
        public async Task AddPokemonToTeam(string userId, string wildOpponentId, int index)
        {
            PlayerMongo player = await _mongoPlayerRepository.GetByPlayerIdAsync(userId);
            PlayerMongo opponent = await _mongoWildPokemonRepository.GetByIdAsync(wildOpponentId);
            PokemonTeam wildPokemon = await _mongoWildPokemonRepository.GetPlayerPokemonById(opponent._id, opponent.Team[0].Id);
            wildPokemon = HealCaughtPokemon(wildPokemon);
            if (index == -1 && player.Team.Length >= 6)
            {
                // Équipe pleine sans Pokémon à remplacer : le joueur a choisi de relâcher le Pokémon capturé
            }
            else if (index == -1)
            {
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

        // Pokémon capturé : rejoint l'équipe entièrement soigné (PV, statuts, PP, états de combat)
        private static PokemonTeam HealCaughtPokemon(PokemonTeam pokemon)
        {
            // Avant le soin : un Métamorph capturé transformé reprend sa forme d'origine
            pokemon = PokemonStatesHelper.ResetForSwap(pokemon);
            pokemon.CurrHp = pokemon.BaseHp;
            pokemon.IsBurning = false;
            pokemon.IsParalyzed = false;
            pokemon.IsPoisoned = 0;
            pokemon.PoisonCount = null;
            pokemon.IsSleeping = 0;
            pokemon.IsFrozen = false;
            foreach (PokemonTeamMove move in pokemon.Moves)
                if (move.MaxPp > 0) move.Pp = move.MaxPp;
            return pokemon;
        }
    }
}
