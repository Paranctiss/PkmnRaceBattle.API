using PkmnRaceBattle.API.Helper;
using PkmnRaceBattle.API.Helpers.PokemonGeneration;
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
        public async Task GetPokeShop(string userId)
        {
            await Clients.Caller.SendAsync("responsePokeShop");
        }

        public async Task BuyItem(string userId, string itemName)
        {

            PlayerMongo player = await _mongoPlayerRepository.GetByPlayerIdAsync(userId);

            BagItem boughtItem = player.Items.FirstOrDefault(x => x.Name == itemName);

            if (player.Credits >= boughtItem.Price)
            {
                player.Items.FirstOrDefault(x => x.Name == itemName).Number++;
                player.Credits -= boughtItem.Price;
                await _mongoPlayerRepository.UpdateAsync(player);
            }
            await Clients.Caller.SendAsync("onBuyItemResponse", itemName, player);

        }
    }
}
