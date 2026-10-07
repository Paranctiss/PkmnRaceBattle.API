# Mécaniques de combat de PkmnRaceBattle

Référence pour les développeurs et les agents : **comment le combat est codé** aujourd'hui et **quelles règles sont attendues**.
Les règles attendues sont celles vérifiées par `PkmnRaceBattle.Tests` (voir `PkmnRaceBattle.Tests/README.md`).

## 1. Référentiel de règles retenu

Le jeu est un **mix voulu** : base Gen 1 (151 Pokémon, capacités de RBY, parcours) avec des mécaniques **modernes** quand elles rendent le jeu meilleur. Les tests suivent ce mix ; un test n'échoue que pour un vrai bug (faute de frappe, condition inversée, mauvaise unité, formule contraire à son intention, règle fausse dans toutes les générations). En cas de doute sur un écart, demander au propriétaire avant de trancher.

| Domaine | Règle retenue |
|---|---|
| Données (puissance, précision, PP, types, physique/spécial) | Celles de la base (import pokeapi, donc **modernes** : Morsure Ténèbres, Mélofée Fée, Magnéti Électrik/Acier…). |
| Table des types | **Moderne (Gen 6+)**, avec Acier, Fée, Ténèbres. Référence : `PkmnRaceBattle.Tests/Support/Rules.cs`. |
| Statuts | Moderne : brûlure et poison 1/8 des PV max, Toxik N/16, gel qui dégèle à 20 % par tour, sommeil 1 à 4 tours (réveil sans perte du tour), confusion 1 à 4 tours. |
| Critiques | 1/24 (1/8 pour les capacités à taux élevé), ×2 par palier de critique (Puissance = +2 paliers), dégâts ×1,5 ; un critique ignore les baisses d'attaque du lanceur, les hausses de défense de la cible et Protection/Mur Lumière. |
| Paliers de stats | Modernes : (2+n)/2 ou 2/(2−n) ; précision/esquive (3+n)/3 ou 3/(3−n). |
| Choix de design | Formule de stats moderne (IV 31, effort de l'espèce passé comme EV), formule de capture Gen 3+, Hyper Potion = 120 PV, Jackpot = 5 × niveau, 2000 ₽ fixes par dresseur vaincu, défaite = perte de la moitié de l'argent + équipe soignée, XP non partagée entre participants (chacun reçoit tout), la capture rapporte de l'XP. |
| Réglages d'XP | Choisis par l'hôte au lancement (`StartGame`, stockés dans `RoomMongo`). **Multi Exp** (activé par défaut, règle des jeux récents) : les participants reçoivent toute l'XP, le reste de l'équipe encore debout la moitié ; sans Multi Exp, seuls les participants. **Vitesse de l'XP** : normale, ×2 ou ×5 (toute autre valeur = normale), appliquée à toute l'XP de combat (pas au Super Bonbon). |
| Niveaux | Starter niveau 5 (hôte comme invités). Les niveaux des adversaires suivent des **paliers par zone** (`Helpers/PathManager/ZoneLevels.cs`, table générée par `tools/zone-levels.js`), indépendants du niveau de l'équipe : seules les maps de combat comptent comme zones (Boutique et Centre n'avancent pas le palier), soit 2 zones toutes les 4 étapes ; la table couvre 24 zones, au-delà elle est prolongée au rythme de la dernière zone (plafond 100). Chaque réglage d'XP (Multi Exp × vitesse) a sa table, calée sur la progression d'un Pokémon de référence qui gagne tous ses combats en ne recevant que 80 % de l'XP possible (fuites, captures, K.O.…), et encore 80 % de cela sans Multi Exp (zones un peu plus douces). Sauvages vers référence − 3, dresseur entre référence − 2 et référence, jamais sous les sauvages. Zone 1 : sauvages niv. 2 à 4 en XP normale. Sauvages : le niveau monte du bas au haut de la fourchette au fil des 5 combats de la map (± 1). Dresseurs : autant de Pokémon que le numéro de la zone (1 en zone 1, 2 en zone 2…, 6 au plus), dans la fourchette du dresseur, envoyés du plus faible au plus fort. La fourchette (1er sauvage → dresseur) est affichée sur la carte, l'embranchement et le bandeau. Les sauvages ne sont jamais sous le `MinimumLevel` de l'espèce dans la map (collection `Environment`, tirage pondéré par la rareté : Commun 50, Peu commun 30, Rare 15, Très rare 4, Légendaire 1). Niveau max 100. |
| Évolutions | Au niveau d'évolution **ou au-delà** (à la montée de niveau suivante) ; plusieurs évolutions peuvent s'enchaîner dans une même montée. Évolutions par échange (Kadabra, Machopeur, Gravalanch, Spectrum) au **niveau 37** (appliqué en base locale et en production le 06/10/2026, script `tools/trade-evolutions.mongodb.js`). Évolution vers un Pokémon absent de la base (M. Glaquette, Berserkatt, Nostenfer…) ignorée. L'évolution garde l'identifiant, les capacités, l'XP, les dégâts subis, le shiny et les statuts. |

## 2. Flux d'un tour (`GameHub.HandleMove` → `UseMove`)

1. `HandleMove(playerId, pokemonId, moveName, opponentId, opponentPokemonId, isAttacking, pvp, index, skipTurn)`
   - Charge le joueur et l'adversaire (collection `WildPokemon` pour sauvages/dresseurs, `Player` en PvP où l'adversaire est toujours `Team[0]`).
   - `moveName` peut être une **pseudo-capacité** : `item:<nom>:<poche>` (objet, `index` = Pokémon ciblé dans l'équipe) ou `swap:` (changement, envoyé par `ReplacePokemon`). Converties par `PokemonMoveSelector.ConvertToActionMove` en capacité de priorité 6.
   - Si le Pokémon a une `WaitingMove` (attaque en deux tours, Mania…), c'est elle qui est rejouée.
   - `ValidatorMove.IsEverythingOk` refuse : Pokémon K.O. qui attaque, 0 PP, Poké Ball contre un dresseur ou un clone, objets inutiles (potion PV max, rappel sur un Pokémon vivant, soin de statut sans statut…).
   - **PvP** : le premier joueur qui choisit voit sa capacité sauvegardée dans `PlayerMongo.ChosenMove`/`ChosenIndex` et reçoit `waitingOpponent` ; le second déclenche `UseMove` avec les deux choix.
   - **Sauvage / dresseur** : l'IA (`AIChoseMove.GetARandomMove`) tire une capacité qui n'est pas sous Entrave.
2. `UseMove` (≈ 850 lignes) travaille sur des **copies** des Pokémon (ou de leur clone si `Substitute`), puis :
   - `TryRemoveAilment` des deux côtés (dégel 20 %, décompte du sommeil et de la confusion) ;
   - ordre : `FightPriority.IsPlayingFirst` (priorité puis vitesse × palier, paralysie ÷ 4) ;
   - pour chaque attaquant : Métronome / Mimique / Copie, `SpecialCaseFail`, effets de terrain, `FightPerformMove.PerformMove`, `PerformSpecialCaseMove`, fin de combat anticipée (`ManageSpecialCasesAfterMove` : Cyclone, Hurlement, Téléport), K.O. ;
   - fin de tour : `SufferAilment` (brûlure, poison) et `SufferSpecialCases` (Vampigraine), attaques sur plusieurs tours (`MultiTurnsMove` : pièges, Entrave), décompte des effets de terrain ;
   - sauvegarde et `turnFinished(joueur, adversaire)` (chaque joueur reçoit son propre état en premier en PvP).
3. `FinishFight(player, opponent, unexpectedEnd)` : remise à zéro des états temporaires, XP aux Pokémon qui ont joué (`HavePlayed`), argent (Jackpot, dresseur), défaite (moitié de l'argent, soin), `MapFightCount++` puis `GetNewTurn` (ou `TrainerSendNextPokemon` si le dresseur a encore des Pokémon). Le PvP ne fait pas avancer la carte.

### TurnContext et animation côté client

`TurnContext` accumule `PrioMessages`, `Messages` et, pour chaque camp (`Player` / `Opponent`), la liste des **variations de PV** (`Hp`, positif = dégâts, négatif = soin) et des **paliers de stats** gagnés/perdus. Le serveur envoie plusieurs `useMoveResult` par tour et attend `CalculateDelay()` ms entre chaque (`GameDelay.Message` = 700 par message, `GameDelay.HpChange` = 300 par variation de PV, alignés sur `shared/utils/timings.ts` du client) pendant que `BattleFieldComponent` les joue dans l'ordre : messages prioritaires → stats → PV joueur → PV adversaire → messages.
**Invariant** (testé pour chaque capacité) : la somme des variations envoyées doit égaler la variation réelle des PV, sinon la barre de vie du client est fausse jusqu'au `turnFinished`. En PvP, le contexte est inversé (`HandleUseMoveResult`) avant l'envoi à l'adversaire.

## 3. État d'un Pokémon en combat (`PokemonTeam`)

| Champ | Rôle | Remis à zéro au changement (`PokemonStatesHelper.ResetForSwap`) / en fin de combat |
|---|---|---|
| `AtkChanges`… `SpeedChanges`, `AccuracyChanges`, `EvasionChanges`, `CritChanges` | Paliers −6..+6 (critique : Puissance) | oui |
| `IsConfused` | Tours de confusion restants | oui |
| `IsSleeping` (tours), `IsBurning`, `IsFrozen`, `IsParalyzed`, `IsPoisoned` (1 = poison, 2 = Toxik), `PoisonCount` | Statuts majeurs | non (persistent) ; le compteur de Toxik (`PoisonCount`) repart à 0 |
| `IsFlinched` | Peur pour ce tour | oui |
| `SpecialCases` | `Vampigraine`, `Frénésie`, `Ejected`, `Teleport` | oui |
| `CantUseMoves`, `MultiTurnsMove`, `MultiTurnsMoveCount` | Entrave / piège subi | oui |
| `WaitingMove`, `WaitingMoveTurns`, `Untargetable` | Attaque en deux tours, Vol/Tunnel | oui |
| `BlowsTaken`, `BlowsTakenType` | Dégâts reçus (Riposte, Patience) | oui |
| `Substitute` | Clone (`CreateSubstitute`) | oui |
| `ConvertedType`, `UnmorphedForm`, `SavedMove`/`SavedMoveSlot` | Conversion, Morphing, Copie | annulés |
| `HavePlayed` | A participé (XP) | après distribution de l'XP |

Effets de terrain : `PlayerMongo.FieldChange` pour le joueur, `PokemonTeam.FieldChange` pour l'adversaire (Brume, Mur Lumière, Protection, compteur `FieldChangeCount`, décompté une fois en fin de tour). Ils sont retirés en fin de combat, comme le clone, la peur et les paliers de précision/esquive.

## 4. Formules

- **Dégâts** : `((2N/5 + 2) × Puissance × Att / Déf) / 50 + 2`, × STAB 1,5, × type, × critique 1,5, × aléatoire 85..100 %. La brûlure divise l'Attaque des capacités **physiques** uniquement. Protection / Mur Lumière divisent par 2 les dégâts physiques / spéciaux (sauf critique).
- **Précision** : un tirage 1..100 ≤ précision × palier précision / palier esquive. Les capacités sans précision (Météores, statut sur soi) ne ratent jamais. Une cible sous Vol/Tunnel est intouchable (sauf capacités sur soi / le terrain).
- **Statuts** : un seul statut majeur, la confusion (volatile) s'y ajoute. Paralysie : vitesse ÷ 4, 25 % de paralysie totale. Immunités : Feu (brûlure par une attaque Feu), Glace (gel), Poison et Acier (poison), Électrik (paralysie), Sol (Cage Éclair). Toxik : le compteur repart à 1/16 quand le Pokémon est rappelé ou en fin de combat (il reste gravement empoisonné).
- **Égalité de vitesse** : tirage 50/50 (`RandomPurpose.TurnOrder`).
- **Multi-coups (2 à 5)** : 35 %, 35 %, 15 %, 15 %.
- **Courbes d'XP** (noms pokeapi) : `fast` 4n³/5, `medium` n³, `medium-slow` 6n³/5 − 15n² + 100n − 140, `slow` 5n³/4.
- **XP gagnée** : `XP de base × niveau du vaincu × (1,5 si dresseur) / 7`, pour les Pokémon ayant participé (la moitié pour le reste de l'équipe avec le Multi Exp ; aucune pour un Pokémon K.O.), puis × vitesse de l'XP.
- **PP** : chaque utilisation consomme 1 PP (pas le 2e tour d'une attaque en deux tours). Une capacité à 0 PP est refusée tant qu'une autre a des PP ; sans aucun PP, le Pokémon utilise **Lutte** (recul ¼ des PV max), y compris l'IA. Le Centre Pokémon restaure les PP (valeurs de la collection `Move`) et remet à jour `MaxPp` (PP max, affichés « restants/max » côté client ; Morphing : 5/5).
- **Barre de vie** : toute variation de PV (dégâts, soins, drain, recul, Clonage, Repos, K.O. en un coup, brûlure, poison, Vampigraine, pièges, confusion) est envoyée au client dans le `TurnContext`.

## 5. Attaques au comportement particulier

| Capacité | Règle |
|---|---|
| Sonic Boom / Draco-Rage | 20 / 40 PV fixes (immunités de type respectées) |
| Frappe Atlas / Ombre Nocturne | Dégâts = niveau du lanceur (Frappe Atlas sans effet sur Spectre, Ombre Nocturne sans effet sur Normal) |
| Croc Fatal | Moitié des PV actuels (min 1) |
| Vague Psy | Entre 0,6 et 1,5 × niveau |
| Balayage | Puissance selon le poids de la cible en **kg** (pokeapi donne des hectogrammes) : < 10 : 20, < 25 : 40, < 50 : 60, < 100 : 80, < 200 : 100, sinon 120 |
| Guillotine / Empal'Korne / Abîme | K.O. direct, précision 30 % + (niveau du lanceur − niveau de la cible) |
| Explosion / Destruction | Lanceur K.O., dégâts normaux |
| Vole-Vie, Méga-Sangsue, Vampirisme, Dévorêve | Rendent ½ des **dégâts infligés** (min 1) ; Dévorêve échoue si la cible est éveillée |
| Bélier, Sacrifice, Damoclès | Contrecoup = `-Drain` % des **dégâts infligés** |
| Pied Sauté / Pied Voltige | ½ des PV max si l'attaque rate |
| Soin / E-Coque | +½ PV max, échouent aux PV max |
| Repos | PV max, statut soigné, dort 2 tours ; échoue aux PV max |
| Vol / Tunnel | Tour 1 : intouchable ; tour 2 : attaque |
| Lance-Soleil, Coupe-Vent, Piqué, Coud'Krâne | Tour 1 : charge (Coud'Krâne : Défense +1) ; tour 2 : attaque |
| Ultralaser | Tour de repos ensuite, sauf si la cible est K.O. |
| Mania / Danse Fleurs | 2 ou 3 tours bloqué puis confus |
| Patience | Règle moderne : priorité +1, le lanceur encaisse pendant 2 tours puis frappe au 3e tour **avant** l'adversaire : renvoie 2 × les dégâts reçus pendant les 2 tours d'attente (le coup du 3e tour n'est pas compté) |
| Riposte | Priorité −5 ; 2 × les dégâts **physiques** reçus, échoue contre une attaque spéciale |
| Ligotage, Étreinte, Danse Flammes, Claquoir | La cible perd 1/8 de ses PV max à chaque fin de tour pendant la durée (données `MinTurns`..`MaxTurns`), peut attaquer, **ne peut pas être rappelée** (Ligotage, Étreinte) |
| Entrave | Bloque la capacité utilisée ce tour par la cible ; l'IA et `ValidatorMove` la refusent |
| Copie / Mimique (et Riposte, Entrave) | Doivent être jouées **après** l'adversaire (`FightPriority.MoveMustBePlayedLast`) : échouent si le lanceur joue en premier ; Copie remplace la capacité jusqu'à la fin du combat, Mimique réutilise l'attaque du tour |
| Métronome | Lance une capacité tirée au sort (`MetronomeMoveProvider`, pokeapi en production) |
| Morphing | Copie types, stats (sauf PV), paliers et capacités (5 PP, copies sans toucher à la cible) ; annulé au changement / en fin de combat |
| Conversion | Le lanceur prend le type de sa **première** capacité |
| Clonage | Coûte ¼ des PV max (échoue si PV ≤ ¼ ou clone existant) ; le clone encaisse les dégâts et bloque les capacités de statut |
| Vampigraine | Draine 1/8 des PV max de la cible chaque tour au profit du lanceur ; Plante immunisés ; échoue si déjà infecté |
| Buée Noire | Remet tous les paliers à zéro (les deux Pokémon) |
| Brume / Protection / Mur Lumière | Bloque les baisses de stats / divise par 2 les dégâts physiques / spéciaux pendant **5 tours** (quel que soit l'ordre des attaques) ; une seule fois ; retirés en fin de combat |
| Frénésie | Attaque +1 à chaque coup reçu |
| Triplattaque | 3 chances de 6,67 % : brûlure, gel ou paralysie |
| Jackpot | +5 × niveau ₽ crédités en fin de combat gagné |
| Cyclone / Hurlement / Téléport | Terminent un combat sauvage ; échouent contre un dresseur |
| Trempette | « Mais rien ne se passe » |

## 6. Objets

- Potions : Potion 20, Super Potion 50, Hyper Potion 120, Potion Max = max, Guérison = max + statut (utilisable aux PV max si statut). Rappel = ½ PV max, Rappel Max = max (uniquement sur un K.O.).
- Soins de statut : Anti-Brûle, Antidote (poison et Toxik), Antigel, Anti-Para, Réveil, Total Soin (tous).
- Pierres : Feu, Eau, Foudre, Lune, Plante via `EvolutionDetails.Item` (`fire-stone`…). Une pierre incompatible est refusée par `ValidatorMove` (pas consommée). Super Bonbon : +1 niveau (XP = début du niveau suivant). Ces objets « spéciaux » sont envoyés avec `skipTurn = true` : l'adversaire ne joue pas.
- Poké Balls : interdites contre un dresseur ou un clone ; `TryCatchPokemon` renvoie −1 (capturé) ou le nombre de secousses (0..3). Équipe limitée à 6 (au-delà, remplacement d'un Pokémon choisi). Le Pokémon capturé rejoint l'équipe **entièrement soigné** (PV, statuts, PP, états de combat ; un Métamorph transformé reprend sa forme).
- Un objet à 0 exemplaire est refusé (`ValidatorMove`). Guérison est utilisable aux PV max si le Pokémon a un statut.

## 7. Carte et progression (`PlayerPathHelper`)

Chemin **sans fin**, généré au fur et à mesure : 12 étapes au départ, puis 6 de plus dès qu'il ne reste que 6 étapes devant le joueur (en arrivant en 6 : étapes 13 à 18, en 12 : 19 à 24…). Étape 1 = Plaine ; après chaque paire de maps de combat, un Centre Pokémon puis une Boutique collée derrière (jamais l'un sans l'autre) ; alternance embranchement à deux environnements différents / étape simple (la première paire Plaine + map simple mise à part) ; une map de combat n'est jamais du même environnement que la map de combat précédente (ni que l'une des deux branches précédentes). Sur une map de combat : 5 combats sauvages puis 1 dresseur (`MapFightCount`). Map terminée → étape suivante (ou `chooseNextPath` si embranchement, la branche non choisie est marquée `IsSkipped`). (Un chemin incomplet, ancien format, se prolonge au premier déplacement ; s'il n'y a vraiment plus d'étape : nouveau cycle sur la dernière map.) Le client affiche la progression avec la même constante (`WILD_FIGHTS_PER_MAP = 5`).

## 8. Aléatoire et délais (testabilité)

Tout l'aléatoire du jeu passe par `Helpers/Randomness/GameRandom` avec un **`RandomPurpose`** (Accuracy, Critical, DamageRoll, SecondaryEffect, Duration, MultiHit, StatusCheck, Catch, AiMoveChoice, Generation, SpecialMove, TurnOrder). Ne jamais écrire `new Random()` dans le code du jeu. Les pauses d'animation passent par `GameDelay.Wait(ms)` (jamais `Task.Delay`), et Métronome par `MetronomeMoveProvider`. Les tests remplacent ces trois points d'entrée.
