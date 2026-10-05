# AGENTS.md

Instructions pour les agents de code (Claude Code, Codex, etc.) travaillant sur ce dépôt.

- Répondre à l'utilisateur **en français**.
- Lire `CLAUDE.md` (commandes, architecture, règles de testabilité), `docs/MECANIQUES_COMBAT.md` (règles de combat attendues) et `PkmnRaceBattle.Tests/README.md` (tests et écarts connus).
- Ne pas modifier le code du jeu (hors tests) sans l'accord du propriétaire ; ne pas committer à sa place.
- Ne jamais lancer de tests contre la base de `appsettings.json` (elle peut pointer sur la production).
- Le client Angular vit dans le dépôt voisin `../PokemonRaceBattle` : tout changement de méthode/événement SignalR se fait des deux côtés (tests `Contract/`).
