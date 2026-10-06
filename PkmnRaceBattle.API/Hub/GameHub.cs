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

    public partial class GameHub : Hub
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
                case "special-attack":
                case "attack-special":
                    AtkSpe += change;
                    break;
                case "defense":
                    Def += change;
                    break;
                case "special-defense":
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
