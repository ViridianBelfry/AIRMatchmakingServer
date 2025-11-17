using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AIRMatchmakingServer.Options;

namespace AIRMatchmakingServer.Services
{
    public class MatchmakingSweeper : BackgroundService
    {
        private readonly MatchmakingService _service;
        private readonly ILogger<MatchmakingSweeper> _logger;
        private readonly IOptions<MatchmakingOptions> _options;

        public MatchmakingSweeper(MatchmakingService service, ILogger<MatchmakingSweeper> logger, IOptions<MatchmakingOptions> options)
        {
            _service = service;
            _logger = logger;
            _options = options;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Matchmaking sweeper started");
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var (expired, removed, kept) = _service.SweepOnce();
                    _logger.LogInformation("Matchmaking sweep completed: markedExpired={MarkedExpired}, removedTickets={RemovedTickets}", expired, removed);
                    _logger.LogTrace("Swept queues: expired={Expired}, removedTickets={Removed}, keptEntries={Kept}", expired, removed, kept);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during matchmaking sweep");
                }

                var seconds = Math.Max(5, _options.Value.SweepIntervalSeconds);
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(seconds), stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    // shutting down
                }
            }
            _logger.LogInformation("Matchmaking sweeper stopping");
        }
    }
}
