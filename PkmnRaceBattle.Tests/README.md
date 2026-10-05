# PkmnRaceBattle.Tests

Tests xUnit du serveur : mécaniques de combat (helpers), flux complets du `GameHub` et contrat client ↔ serveur.
Les règles attendues sont décrites dans `../docs/MECANIQUES_COMBAT.md`. **Un test qui échoue signale un écart entre le jeu et ces règles** : ne pas « corriger » un test pour qu'il passe sans avoir vérifié la règle.

## Lancer les tests

```bash
dotnet test PkmnRaceBattle.Tests
# Si l'API tourne dans l'IDE (bin/ verrouillé), compiler ailleurs :
dotnet test PkmnRaceBattle.Tests --artifacts-path %TEMP%/pkmn-tests
# Un seul groupe
dotnet test PkmnRaceBattle.Tests --filter "FullyQualifiedName~Mechanics.StatusConditionTests"
```

Aucun test n'utilise MongoDB ni le réseau : dépôts en mémoire, aléatoire piloté, délais supprimés. Exécution séquentielle (≈ 20 s) car `UserConnectionManager` est statique et non thread-safe.

## Organisation

| Dossier | Contenu |
|---|---|
| `Mechanics/` | Helpers de combat appelés directement : types, dégâts/critiques, précision/priorité, statuts, paliers de stats, objets, capture, XP/stats/génération |
| `Moves/` | Attaques particulières : effets en un tour (`SingleTurnSpecialMovesTests`, via `FightPerformMove`) et sur plusieurs tours (`MultiTurnMovesTests`, via le hub) |
| `Hub/` | Flux complets via `GameHub` : création/jonction/lancement, carte et tours, combats sauvages/dresseurs, objets en combat et capture, montée de niveau/évolution, changement et remises à zéro, Centre/Boutique, PvP et tournoi |
| `Contract/` | Lit le code du client (`../../PokemonRaceBattle/src/app`) : noms SignalR, arguments, prix de la boutique, environnements. Ignorés si le client est absent |
| `Support/` | Infrastructure (ci-dessous) |
| `Fixtures/` | Export JSON des collections `Pokemon`, `Move`, `Environment` (voir `Fixtures/README.md`) |

## Infrastructure (`Support/`)

- **`GameData`** : vraies données du jeu (151 Pokémon, 165 capacités, 6 environnements) chargées depuis `Fixtures/`. `GameData.Pokemon("Salamèche")`, `GameData.Move("Charge")` renvoient des copies.
- **`Pkmn`** : `Pkmn.Create("Pikachu", 20, "Éclair", "Vive-Attaque")` génère un Pokémon comme en jeu (jamais shiny) ; `.WithStats(hp:, atk:, speed: …)`, `.WithHp()`, `.WithTypes("neutre")` (type sans interaction pour isoler un calcul) ; `Pkmn.Item("Potion", "potion", index)`.
- **`TestRandom`** : implémente `IRandomSource`. Chaque tirage est une **fraction** dans [0, 1[ par `RandomPurpose` (un entier de [min, max[ vaut `min + floor(f × (max − min))`). `TestRandom.Neutral()` = touche toujours, jamais de critique, dégâts max, pas d'effet secondaire, pas de paralysie totale / auto-dégâts / dégel, durées et coups minimum, l'IA prend sa 1re capacité. `.Set(purpose, f)`, `.Queue(purpose, f1, f2…)`, `.AlwaysCrit()`, `.AlwaysMiss()`… Installer avec `using var _ = TestRandom.Neutral().Install();` (isolé par flux asynchrone). Exprimer les tests en probabilités (« un jet à 24 % paralyse, à 26 % non ») plutôt qu'en ordre d'appels.
- **`Rules`** : référence des règles du jeu (table des types moderne, formule de dégâts, paliers, courbes d'XP) et raccourcis `Rules.Attack(attaquant, défenseur, "Capacité")`, `Rules.DamageDealt(...)`.
- **`HubHarness`** : `GameHub` branché sur des dépôts en mémoire (lecture/écriture par sérialisation BSON, comme Mongo) et des clients SignalR qui enregistrent tous les `SendAsync` (`Sent`, `Named("useMoveResult", connexion)`, `DialogFor(connexion)`). `AddPlayer` place le joueur sur la première map (Plaine). `HubHarness.Metronome("Ultimapoing")` fixe l'attaque tirée par Métronome.
- **`Battle`** : combat joueur contre sauvage/dresseur piloté par le hub (`Battle.VsWild(moi, sauvage, banc…)`, `Use("Charge")`, `UseItem("Potion", "potion", index)`, `SwitchTo(i)`, `Mine`, `Foe`, `HpShownForPlayer/Opponent` = somme des variations de PV envoyées au client). Donner `Trempette` comme seule capacité à l'adversaire pour qu'il ne fasse rien.
- `TestSetup` met `GameDelay` à zéro pour tout l'assembly.

## Écarts restants (état au 06/10/2026 : 1166 tests, 10 échecs)

Les tests suivent les règles du jeu (mix Gen 1 / moderne). Une première série de corrections a été faite le 06/10/2026 (précision, types, critiques, statuts et immunités, drains/reculs, dégâts fixes, Balayage, Vampigraine, PP + Lutte, XP et niveau 100, évolutions, remises à zéro, objets, barre de vie, jonction de partie, niveaux des starters/sauvages, `MinimumLevel`). Restent à corriger :

| Test(s) en échec | Problème |
|---|---|
| `Clonage_UnCloneDetruitLaissePlaceAuPokemon` | Quand le clone est détruit, `UseMove` restaure la copie du Pokémon prise avant le coût du Clonage : le ¼ de PV payé est rendu |
| `BarreDeVie_…(move: "Clonage")` | Les dégâts encaissés par le clone sont envoyés comme des dégâts du Pokémon (le client ne connaît pas encore le clone pendant l'animation) |
| `Patience_RendLeDoubleDesDegatsSubis` | Les dégâts reçus pendant Patience ne sont pas tous cumulés (`BlowsTaken` remis à 0 / incrémenté au mauvais moment) |
| `PvpTests.KOAvecDesPokemonRestants…` | En PvP, quand le Pokémon de l'adversaire est K.O. et qu'il lui en reste, `FinishFight` ne lui demande pas d'en envoyer un autre (et réinitialise l'équipe du gagnant en plein combat) |
| `TournamentTests.CombatPvp_LesJoueursSontAppariesDeuxADeux`, `NombreImpairDeJoueurs_NePlantePas` | `GetPvpFight` lit `Rounds[NbTurn - 1]` (le tour suivant, rempli de « ? ») au lieu du tour en cours → crash dès 4 joueurs ; nombre impair non géré |
| `Tableau_ContientTousLesJoueursEtLeBonNombreDeTours(8)` | `BuildTournament` crée N/2 tours au lieu de log2(N) |
| `VictoirePvp_LeVainqueurPasseAuTourSuivant` | `AddWinnerToNextRound` n'est jamais appelé après une victoire PvP |
| `Contract.ChaqueAppelDuClient_UtiliseDesTypesSimples…` | Le client expose `finishFight()` qui appelle `FinishFight` avec des chaînes alors que la méthode attend des objets (code mort côté client) |
| `Contract.ChaqueEcouteDuClient_CorrespondAUnEvenementDuServeur` | Le client écoute `NewTurn`, jamais envoyé par le serveur (code mort) |

Non couvert par un test en échec mais à traiter : `UserConnectionManager` (dictionnaires statiques non thread-safe utilisés par le hub en parallèle) ; côté client, les écouteurs `HubService.on*` ne sont jamais retirés (voir `PokemonRaceBattle/CLAUDE.md`).

## Ajouter un test

1. Choisir la règle attendue dans `docs/MECANIQUES_COMBAT.md` (mix Gen 1 / moderne voulu par le propriétaire). Si le code diffère d'une règle d'une génération : se demander si c'est un choix (règle moderne reconnue) ou un bug (faute de frappe, condition inversée, mauvaise unité…) — en cas de doute, demander.
2. Installer un `TestRandom` et fixer les tirages concernés par leur probabilité.
3. Préférer `Rules.Attack` pour un effet immédiat, `Battle` pour tout ce qui dépend de l'ordre du tour, de plusieurs tours ou de la persistance.
4. Vérifier aussi ce que voit le client (`HpShownForPlayer`, `Dialog`, `Received(...)`), pas seulement l'état en base.
