# MTG Arena tracker daemon (specification)

Application will run as a service on the computer, listening for MTGA process, on the background.

Will start a local HTTP server on boot at port 6842 ("mtga" on numpad) (should be configurable?).
Will listen for MTG Arena process and update its status for apps to read/track.
UI is not needed as it is very straightforward (only tracks MTGA memory), but it should have at least a tray icon to stop and an option to start on system startup.

If it is a console app or we can catch terminal arguments, it would be nice to send the port configuration like that;

`./mtgaDaemon.exe -p 9000`

## Endpoints:

### GET /status
Check if the MTGA process is running or not, and get its Process ID. (some apps use this to get other metrics or data like the window position and size)
Json Response
```
{
  isRunning: Boolean,
  processId: Number | -1,
}
```

### GET /cards
Get a list (array) of all cards owned by the current player.
Json Response:
```
{
  cards: [
    {
      grpId: Number,
      owned: Number,
    }
  ]
}
```

### GET /playerId
Return the current ID (wizards account ID)
Json Response:
```
{
  playerId: String
}
```

### GET /inventory
Return the state of the player inventory
Json Response:
```
{
  gems: Number,
  gold: Number
}
```

