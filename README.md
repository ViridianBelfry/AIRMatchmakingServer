# AIRMatchmakingServer

This is the **region-based matchmaking server** for **Alpha Instinct Royale**, responsible for managing player queues and returning a simulated game instance URL once a lobby is full. Lobby sizes are currently 8, 32, or 100 players.

---

## Tech Stack

- **ASP.NET Core + SignalR** (.NET 8)
- C#
- JSON payloads over WebSockets (SignalR handles HTTP negotiation internally)
- Unity client integration via `Microsoft.AspNetCore.SignalR.Client`

---

## Running Locally

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download)

### 1. Trust HTTPS (first run only)

```bash
dotnet dev-certs https --trust
```

SignalR still negotiates over HTTPS, so trusting the dev cert avoids browser / Unity warnings.

### 2. Run the server

```bash
dotnet run
```

You should see logs similar to:

```text
info: Microsoft.Hosting.Lifetime[14]
Now listening on: https://localhost:7084
Now listening on: http://localhost:5163
Application started. Press Ctrl+C to shut down.
```

### 3. Connect through SignalR

All matchmaking now happens exclusively through the SignalR hub at `/Matchmaking`. Example console client:

```csharp
var connection = new HubConnectionBuilder()
    .WithUrl("https://localhost:7084/Matchmaking")
    .WithAutomaticReconnect()
    .Build();

connection.On<object>("Queued", payload => Console.WriteLine($"Queued: {JsonSerializer.Serialize(payload)}"));
connection.On<object>("MatchOffer", payload => Console.WriteLine($"MatchOffer: {JsonSerializer.Serialize(payload)}"));

await connection.StartAsync();
await connection.InvokeAsync("Identify", "cPlayer_1234");
await connection.InvokeAsync("JoinQueue", new PlayerJoinRequest
{
    PlayerId = "Player_1234",
    QueueType = QueueType.Casual,
    LobbySize = LobbySize.Medium,
    MMR = 1200
});
```

The server will send `Queued` updates while you wait, `MatchOffer` when a lobby is ready, and you can call `Heartbeat(ticketId)` / `Leave(ticketId)` on the hub to keep a ticket active or exit the queue.

---

## SignalR Flow

- **Identify**: Bind a `playerId` to the current SignalR connection. This allows multiple tabs/devices to receive events.
- **JoinQueue(PlayerJoinRequest)**: Enqueue the player or, if `botFill` / `botCount` is set, immediately respond with a bot-filled lobby via `MatchOffer`.
- **Queued event**: Contains `ticketId` and `ttlSeconds`; store the ticket so you can heartbeat.
- **MatchOffer event**: Includes the generated `gameUrl`, participating player IDs, and a shared `ticketId` for the lobby.
- **Heartbeat(ticketId)**: Keeps the queue entry alive (call roughly every `ttlSeconds/2` seconds).
- **Leave(ticketId)**: Cancels the queue entry.

---

## Project Structure

```text
Constants/            // Constant values (lobby limits, etc.)
Game/                 // Match session abstractions
Hubs/                 // SignalR hubs (MatchmakingHub)
Options/              // Configuration objects
Services/             // Queue logic + connection tracking
Utils/                // Shared helpers (validation, lobby math)
Program.cs            // Server entry point + DI wiring
```

---

## Unity Integration

Use the official SignalR .NET client (`Microsoft.AspNetCore.SignalR.Client`) from Unity:

1. Build a `HubConnection` pointing to `https://<server>/Matchmaking`.
2. After `StartAsync`, immediately call `Identify(playerId)`.
3. Register listeners for `Queued` and `MatchOffer` using `connection.On<T>()`.
4. Invoke `JoinQueue` with a serialized `PlayerJoinRequest` when the player clicks "Start Matchmaking".
5. Store the returned `ticketId` from the `Queued` event and call `Heartbeat` periodically until a match arrives or the player cancels with `Leave`.

This removes all HTTP-specific plumbing - SignalR handles negotiation, transport fallbacks, and reconnects for you.

---

## Bot-Filled Matches

`JoinQueue` supports optional bot parameters:

- `botFill` (bool): fill the entire lobby with bots except the requesting player. Great for solo practice.
- `botCount` (int): explicit number of bots to add (0..capacity-1). Takes precedence over `botFill` when > 0.

Notes:

- Do **not** set both `botFill` and a positive `botCount` - the request will be rejected.
- Lobby capacity depends on `lobbySize` (Small=8, Medium=32, Large=100).
- Immediate bot matches send `MatchOffer` right away with a generated `gameUrl`, the `players` list (including synthetic `BOT_xxxxx` IDs), and `botCount`.

---

## Planned Features

- Real match server instance spawning (Docker/Kubernetes)
- MMR-based queue bucketing
- Redis or SQL match tracking
- Authentication / player tokens
