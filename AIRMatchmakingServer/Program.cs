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

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<MatchmakingService>(); // Register the service
builder.Services.AddControllers();             // Allow attribute routing


var app = builder.Build();
app.MapControllers();
// app.MapGet("/", () => "AIRMatchmakingServer is running.");
app.Run();
