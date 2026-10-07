using PkmnRaceBattle.API.Helper;
using PkmnRaceBattle.API.Helpers.PokemonGeneration;
using PkmnRaceBattle.Domain.Models.PlayerMongo;
using PkmnRaceBattle.Domain.Models.PokemonMongo;
using System.Text.RegularExpressions;
using Microsoft.AspNet.SignalR.Messaging;
using Microsoft.AspNet.SignalR.Tracing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.SignalR;
using PkmnRaceBattle.Domain.Models.RoomMongo;
using PkmnRaceBattle.API.Helpers.PokemonStates;
using PkmnRaceBattle.API.Helpers.Experience;
using PkmnRaceBattle.API.Helpers.PathManager;

namespace PkmnRaceBattle.API.Hub
{
    public partial class GameHub
    {
        private static readonly Dictionary<string, DateTime> _gameTimers = new Dictionary<string, DateTime>();
        private static readonly Dictionary<string, Timer> _timerObjects = new Dictionary<string, Timer>();

        // RoomMongo.state : 0 = salle d'attente, 1 = course en cours, 2 = course terminée (minuteur écoulé)
        public const int RoomStateRaceOver = 2;

        private async Task<bool> IsRaceOver(string roomId)
        {
            RoomMongo room = await _mongoRoomRepository.GetByRoomIdAsync(roomId);
            return room != null && room.state >= RoomStateRaceOver;
        }

        // Fin de la course : les équipes entrent soignées dans le tournoi
        private async Task HealTeamsForTournament(string gameCode)
        {
            List<PlayerMongo> players = await _mongoPlayerRepository.GetByRoomId(gameCode);

            foreach (PlayerMongo player in players)
            {
                for (int i = 0; i < player.Team.Length; i++)
                {
                    player.Team[i].CurrHp = player.Team[i].BaseHp;
                    player.Team[i].IsBurning = false;
                    player.Team[i].IsParalyzed = false;
                    player.Team[i].IsPoisoned = 0;
                    player.Team[i].IsSleeping = 0;
                    player.Team[i].IsFrozen = false;
                    player.Team[i] = PokemonStatesHelper.ResetForSwap(player.Team[i]);
                }

                await _mongoPlayerRepository.UpdateAsync(player);
            }
        }
        private async Task<XpSettings> GetXpSettings(string roomId) =>
            XpSettings.FromRoom(await _mongoRoomRepository.GetByRoomIdAsync(roomId));

        // multiXp / xpMultiplier : réglages d'XP choisis par l'hôte (Multi Exp activé et XP normale par défaut)
        public async Task StartGame(string gameCode, bool checkedTimer, int timerTime, bool multiXp = true, int xpMultiplier = 1)
        {
            RoomMongo room = await _mongoRoomRepository.GetByRoomIdAsync(gameCode);
            room.state = 1;
            room.MultiXp = multiXp;
            room.XpMultiplier = XpSettings.NormalizeMultiplier(xpMultiplier);
            await _mongoRoomRepository.UpdateAsync(gameCode, room);

            // Paliers de niveaux affichés sur la carte de chaque joueur
            XpSettings xpSettings = XpSettings.FromRoom(room);
            foreach (PlayerMongo player in await _mongoPlayerRepository.GetByRoomId(gameCode))
            {
                ZoneLevels.AnnotatePath(player, xpSettings);
                await _mongoPlayerRepository.UpdateAsync(player);
            }

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
                            // Course terminée avant le soin : plus aucun tour ne peut être lancé (GetNewTurn)
                            RoomMongo endedRoom = await _mongoRoomRepository.GetByRoomIdAsync(gameCode);
                            if (endedRoom != null)
                            {
                                endedRoom.state = RoomStateRaceOver;
                                await _mongoRoomRepository.UpdateAsync(gameCode, endedRoom);
                            }

                            await HealTeamsForTournament(gameCode);

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
    }
}
