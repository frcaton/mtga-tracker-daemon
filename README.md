# mtga-tracker-daemon

An HTTP server for getting game data from MTG Arena.

Usage is very straightforward;

`./mtga-tracker-daemon.exe -p 9000`

On Linux, the daemon runs against MTGA launched via Steam/Proton (`MTGA.exe`).

Most endpoints that read game memory return an error object if MTGA is not running or the request fails:

```
{
  error: String,
}
```

### GET /status
Check if the MTGA process is running or not and whether is updating itself or not, and get its Process ID.
(some apps use this to get other metrics or data like the window position and size)

Response
```
{
  isRunning: Boolean,
  daemonVersion: String,
  updating: Boolean,
  processId: Number | -1,
}
```

### POST /shutdown
Stops the daemon

Response:
```
{
  result: String,
}
```

### POST /checkForUpdates
Tells the daemon to check for updates

Response:
```
{
  updatesAvailable: Boolean,
}
```

### GET /cards
Get a list (array) of all cards owned by the current player.

Response:
```
{
  cards: [
    {
      grpId: Number,
      owned: Number,
    }
  ],
  elapsedTime: Number,
}
```

### GET /playerId
Return the current account information (Wizards account ID, display name, and persona ID).

Response:
```
{
  playerId: String,
  displayName: String,
  personaId: String,
  elapsedTime: Number,
}
```

### GET /inventory
Return the state of the player inventory

Response:
```
{
  gems: Number,
  gold: Number,
  elapsedTime: Number,
}
```

### GET /events
Return the internal event names currently cached by the game.

Response:
```
{
  events: [ String ],
  elapsedTime: Number,
}
```

### GET /matchState
Return match and ranked-play information for the current match.

Response:
```
{
  matchId: String,
  playerRank: {
    mythicPercentile: Number,
    mythicPlacement: Number,
    class: Number,
    tier: Number,
  },
  opponentRank: {
    mythicPercentile: Number,
    mythicPlacement: Number,
    class: Number,
    tier: Number,
  },
  elapsedTime: Number,
}
```

### GET /allCardsConnectionString
Return the SQLite connection string for the in-memory card database used by MTGA.

On Linux/Proton, Wine drive letters (e.g. `S:`) are stripped and the path is resolved to an absolute filesystem path using the MTGA process location (via `/proc`), so clients can open the database file directly.

Response:
```
{
  connectionString: String,
  elapsedTime: Number,
}
```

### GET /allCards
Return every card in the MTGA card database (not just cards owned by the player), including localized English titles.

This endpoint opens the SQLite database using the same connection-string resolution as `/allCardsConnectionString`. Card titles are JSON-escaped.

Response:
```
{
  cards: [
    {
      grpId: Number,
      title: String,
      expansionCode: String,
    }
  ],
  elapsedTime: Number,
}
```
