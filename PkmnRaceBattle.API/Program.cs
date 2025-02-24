using Microsoft.Extensions.Options;
using MongoDB.Driver;
using PkmnRaceBattle.API.Hub;
using PkmnRaceBattle.Application.Contracts;
using PkmnRaceBattle.Persistence.ExternalAPI;
using PkmnRaceBattle.Persistence.Models;
using PkmnRaceBattle.Persistence.Repositories;
using PkmnRaceBattle.Persistence.Services;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true; // Active les erreurs détaillées
});
builder.Services.AddCors();
builder.Services.Configure<MongoSettings>(
builder.Configuration.GetSection("MongoSettings"));
builder.Services.AddSingleton<IMongoDatabase>(sp =>
{
    var settings = sp.GetRequiredService<IOptions<MongoSettings>>().Value;
    var client = new MongoClient(settings.ConnectionString);
    return client.GetDatabase(settings.DatabaseName);
});
//builder.Services.AddSingleton<PokemonDbService>();
builder.Services.AddSingleton<IMongoPokemonRepository>(sp =>
{
    var database = sp.GetRequiredService<IMongoDatabase>();
    var settings = sp.GetRequiredService<IOptions<MongoSettings>>().Value;
    return new MongoPokemonRepository(database, settings.PokemonCollectionName);
});
builder.Services.AddSingleton<IMongoRoomRepository>(sp =>
{
    var database = sp.GetRequiredService<IMongoDatabase>();
    var settings = sp.GetRequiredService<IOptions<MongoSettings>>().Value;
    return new MongoRoomRepository(database, settings.RoomCollectionName);
});
builder.Services.AddSingleton<IMongoPlayerRepository>(sp =>
{
    var database = sp.GetRequiredService<IMongoDatabase>();
    var settings = sp.GetRequiredService<IOptions<MongoSettings>>().Value;
    return new MongoPlayerRepository(database, settings.PlayerCollectionName);
});
builder.Services.AddSingleton<IMongoWildPokemonRepository>(sp =>
{
    var database = sp.GetRequiredService<IMongoDatabase>();
    var settings = sp.GetRequiredService<IOptions<MongoSettings>>().Value;
    return new MongoWildPokemonRepository(database, settings.WildPokemonCollectionName);
});
builder.Services.AddSingleton<PokemonExtAPI>();
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseWebSockets();

app.UseCors(options => options
    .WithOrigins("http://localhost:4200") // Remplacez par votre domaine frontend
    .AllowAnyMethod()
    .AllowAnyHeader()
    .AllowCredentials() // Important pour SignalR
);


app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
    endpoints.MapHub<GameHub>("/gameHub");
});
app.MapControllers();

app.Run();
