namespace AIRMatchmakingServer.Options
{
    public class MatchmakingOptions
    {
        // Default TTL (seconds) if not configured
        public int HeartbeatTtlSeconds { get; set; } = 30;

        // How often to run background sweeps (seconds)
        public int SweepIntervalSeconds { get; set; } = 15;
    }
}
