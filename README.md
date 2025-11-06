# AIRMatchmakingServer

This is the **region-based matchmaking server** for **Alpha Instinct Royale**, responsible for managing player matchmaking queues and returning game instance URLs once a match is full.

Each game consists of 8, 32, or 100 players. Once enough players are queued, this server returns a **simulated game server URL** (real container orchestration coming later).

---

## 🔧 Tech Stack

- **ASP.NET Core Web API** (.NET 8)
- C#
- JSON over HTTP
- Unity client integration via `UnityWebRequest`

---

## 🚀 Running Locally

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download)

### 1. Trust HTTPS (if running locally for the first time)

```bash
dotnet dev-certs https --trust
```

This sets up a development SSL certificate so HTTPS endpoints work without browser warnings.

---

### 2. Run the server

From the project root:

```bash
dotnet run
```

You should see something like this in the terminal:

```text
info: Microsoft.Hosting.Lifetime[14]
Now listening on: https://localhost:7084
Now listening on: http://localhost:5163
Application started. Press Ctrl+C to shut down.
```

The port numbers may vary — both `https://localhost:7084` and `http://localhost:5163` are valid.

---

### 3. Test the API

You can use curl or Postman to hit the matchmaking endpoint:

```bash
curl -X POST https://localhost:7084/matchmaking/join -k \
  -H "Content-Type: application/json" \
  -d '{ "PlayerId": "Player_1234", "MMR": 1200 }'
```

Expected response:

```json
{
  "message": "Waiting for match..."
}
```

Once enough players (e.g. 32) are queued, the server returns:

```json
{
  "message": "Match created!",
  "gameUrl": "https://game-instance-ab12cd34.alpha.com",
  "players": [ "Player_1234", ... ]
}
```

---

## 📦 Project Structure

```text
Constants/            // Constant values for reuse
Controllers/          // Web API endpoints
Services/             // Match queue logic
Plugins/              // BattleSim Library with Request/Response payloads
Utils/                // General utility functions
Program.cs            // Server entry point
```

---

## 🧪 In Unity

Use `UnityWebRequest` from your Unity client to hit the `/matchmaking/join` endpoint when the player clicks "Start Matchmaking."

For local development, use a `CertificateHandler` to bypass dev cert issues:

```csharp
request.certificateHandler = new BypassCertificate(); // Only for dev
```

---

## 🛠️ Planned Features

- Real match server instance spawning (via Docker/Kubernetes)
- Matchmaking by MMR tiers
- Redis or SQL match tracking
- Authentication / player tokens

---

## 🤖 Bot-Filled Matches

The join request supports two optional fields to request bots:

- `botFill` (bool): if true, immediately creates a solo match filled with bots up to lobby capacity.
- `botCount` (int): explicit number of bots to add (0..capacity-1). If provided and > 0, it takes precedence over `botFill`.

Notes:
- Do not set both `botFill` and a positive `botCount` — the API will reject the request.
- Capacity depends on `lobbySize` (Small=8, Medium=32, Large=100).

Examples

- Solo vs bots (fill lobby):

```bash
curl -X POST https://localhost:7084/matchmaking/join -k \
  -H "Content-Type: application/json" \
  -d '{ "PlayerId": "Player1", "QueueType": "Casual", "LobbySize": "Small", "BotFill": true }'
```

- Solo with N bots:

```bash
curl -X POST https://localhost:7084/matchmaking/join -k \
  -H "Content-Type: application/json" \
  -d '{ "PlayerId": "Player1", "QueueType": "Casual", "LobbySize": "Small", "BotCount": 3 }'
```

Response includes the generated `gameUrl`, the `players` list (with synthetic `BOT_XXXXX` IDs), and `botCount` when bots are used.
