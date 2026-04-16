using Microsoft.EntityFrameworkCore;
using Stin_Semestral.Data;

var builder = WebApplication.CreateBuilder(args); 

// 1. Registrace databáze (pøidání do kontejneru služeb)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite("Data Source=stin_database.db"));

// 2. Registrace HttpClient (pro budoucí volání ExchangeRate API)
builder.Services.AddHttpClient();

var app = builder.Build();

// Tady pozdìji pøibudou cesty (Endpoints) pro tvé API

app.Run();