namespace AIRMatchmakingServer.Models
{
    public class PlayerJoinRequest
    {
        public string PlayerId { get; set; } = string.Empty;
        public int MMR { get; set; }  // For later use
        public QueueType QueueType { get; set; } = QueueType.Casual;
        public LobbySize LobbySize { get; set; } = LobbySize.Small;
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
