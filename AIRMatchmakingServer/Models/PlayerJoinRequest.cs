namespace AIRMatchmakingServer.Models
{
    public class PlayerJoinRequest
    {
        public string PlayerId { get; set; } = string.Empty;
        public int MMR { get; set; }  // For later use
        public QueueType QueueType { get; set; } = QueueType.Casual;
        public LobbySize LobbySize { get; set; } = LobbySize.Small;
        // If true, requester wants the lobby filled with bots up to capacity (solo vs bots)
        public bool BotFill { get; set; } = false;
        // Optional explicit number of bots requested (0..capacity-1). If provided and >0, takes precedence over BotFill.
        public int? BotCount { get; set; }
    }

    public enum LobbySize
    {
        Small,
        Medium,
        Large,
    }

    public enum QueueType
    {
        Casual,
        Ranked
    }
}
