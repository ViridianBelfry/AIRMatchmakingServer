namespace AIRMatchmakingServer.Models
{
    public enum QueueStatus
    {
        Queued,
        Cancelled,
        Expired
    }

    public class QueueEntry
    {
        public string TicketId { get; init; } = Guid.NewGuid().ToString("N");
        public string PlayerId { get; init; } = string.Empty;
        public int MMR { get; init; }
        public QueueType QueueType { get; init; }
        public LobbySize LobbySize { get; init; }
        public bool BotFill { get; init; }
        public int? BotCount { get; init; }

        public DateTime EnqueuedAtUtc { get; init; } = DateTime.UtcNow;
        public DateTime LastSeenAtUtc { get; set; } = DateTime.UtcNow;
        public QueueStatus Status { get; set; } = QueueStatus.Queued;

        public PlayerJoinRequest ToRequest() => new PlayerJoinRequest
        {
            PlayerId = PlayerId,
            MMR = MMR,
            QueueType = QueueType,
            LobbySize = LobbySize,
            BotFill = BotFill,
            BotCount = BotCount
        };
    }
}
