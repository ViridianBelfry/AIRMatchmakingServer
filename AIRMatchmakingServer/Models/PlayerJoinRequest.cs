namespace AIRMatchmakingServer.Models
{
    public class PlayerJoinRequest
    {
        public string PlayerId { get; set; } = string.Empty;
        public int MMR { get; set; }  // For later use
    }
}
