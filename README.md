# mtga-tracker-daemon

An HTTP server for getting game data from MTG Arena.

Usage is very straightforward;

`./mtga-tracker-daemon.exe -p 9000`

### GET /status
Check if the MTGA process is running or not, and get its Process ID. (some apps use this to get other metrics or data like the window position and size)

Response
```
{
  isRunning: Boolean,
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
Return the current ID (wizards account ID)

Response:
```
{
  playerId: String,
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

