/*var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
*/

using AIRMatchmakingServer.Services;
using AIRMatchmakingServer.Hubs;
using System.Text.Json.Serialization;
using AIRMatchmakingServer.Options;

var builder = WebApplication.CreateBuilder(args);
// Configure simple console logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.AddControllers()
.AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
});

builder.Services.Configure<MatchmakingOptions>(builder.Configuration.GetSection("Matchmaking"));
builder.Services.AddSingleton<MatchmakingService>(); // Register the service
builder.Services.AddSingleton<ConnectionRegistry>();
builder.Services.AddSignalR();
builder.Services.AddHostedService<MatchmakingSweeper>();

var app = builder.Build();

app.MapControllers();
app.MapHub<MatchmakingHub>("/hubs/matchmaking");
app.Run();
