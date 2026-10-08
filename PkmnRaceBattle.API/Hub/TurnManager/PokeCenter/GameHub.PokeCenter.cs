using PkmnRaceBattle.API.Helper;
using PkmnRaceBattle.API.Helpers.PokemonGeneration;
using PkmnRaceBattle.Domain.Models.PlayerMongo;
using PkmnRaceBattle.Domain.Models.PokemonMongo;
using System.Text.RegularExpressions;
using Microsoft.AspNet.SignalR.Messaging;
using Microsoft.AspNet.SignalR.Tracing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.SignalR;
using PkmnRaceBattle.API.Helpers.PokemonStates;

namespace PkmnRaceBattle.API.Hub
{
    public partial class GameHub
    {
        public async Task GetPokeCenter(string userId)
        {
            PlayerMongo player = await _mongoPlayerRepository.GetByPlayerIdAsync(userId);

            await Clients.Caller.SendAsync("responsePokeCenter", player);
        }

        public async Task UsePokeCenter(string userId)
        {
            PlayerMongo player = await _mongoPlayerRepository.GetByPlayerIdAsync(userId);

            await HealTeamLikePokeCenter(player);

            await _mongoPlayerRepository.UpdateAsync(player);

            await Clients.Caller.SendAsync("healedPokeCenter", player);
        }

        // Soin complet du Centre Pokémon (aussi utilisé après une défaite et à chaque étape du tournoi) :
        // états de combat, PV, statuts et PP (valeurs de la collection Move). Ne sauvegarde pas le joueur.
        private async Task HealTeamLikePokeCenter(PlayerMongo player)
        {
            player.FieldChange = null;
            player.FieldChangeCount = null;
            player.ChosenMove = null;

            for (int i = 0; i < player.Team.Length; i++)
            {
                // Avant le soin : un Métamorph transformé reprend sa forme d'origine
                PokemonTeam pokemon = PokemonStatesHelper.ResetForSwap(player.Team[i]);
                pokemon.CurrHp = pokemon.BaseHp;
                pokemon.IsBurning = false;
                pokemon.IsParalyzed = false;
                pokemon.IsPoisoned = 0;
                pokemon.PoisonCount = null;
                pokemon.IsSleeping = 0;
                pokemon.IsFrozen = false;
                foreach (PokemonTeamMove move in pokemon.Moves)
                {
                    MoveMongo reference = await _mongoMoveRepository.GetMoveMongoByName(move.NameFr);
                    if (reference != null) move.Pp = move.MaxPp = reference.Pp;
                    else if (move.MaxPp > 0) move.Pp = move.MaxPp;
                }
                player.Team[i] = pokemon;
            }
        }
    }
}
