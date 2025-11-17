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

using Microsoft.AspNetCore.SignalR;
using AIRMatchmakingServer.Services;
using AIRMatchmakingServer.Hubs;
using AIRMatchmakingServer.Options;

var builder = WebApplication.CreateBuilder(args);
// Configure simple console logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.Configure<MatchmakingOptions>(builder.Configuration.GetSection("Matchmaking"));
builder.Services.AddSingleton<MatchmakingService>(); // Register the service
builder.Services.AddSingleton<ConnectionRegistry>();
builder.Services.AddSignalR((o) =>
{
    o.EnableDetailedErrors = true;
});
builder.Services.AddHostedService<MatchmakingSweeper>();

var app = builder.Build();

app.MapHub<MatchmakingHub>("/Matchmaking");
app.Run();
