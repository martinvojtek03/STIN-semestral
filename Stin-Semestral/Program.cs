using Microsoft.EntityFrameworkCore;
using Stin_Semestral.Data;
using Stin_Semestral.Services;

var builder = WebApplication.CreateBuilder(args); 

// 1. Registrace databáze (přidání do kontejneru služeb)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite("Data Source=stin_database.db"));

// 2. Registrace HttpClient (pro budoucí volání ExchangeRate API)
builder.Services.AddHttpClient();


builder.Services.AddScoped<ExchangeRateService>(); 

var app = builder.Build();


// Testovací endpoint pro ověření API a User Secrets
app.MapGet("/test-api", async (ExchangeRateService service, IConfiguration config) =>
{
    // Diagnostika do konzole (uvidíš v černém okně Visual Studia)
    var keyCheck = config["ExchangeRateApi:ApiKey"];
    if (string.IsNullOrEmpty(keyCheck))
    {
        Console.WriteLine("CHYBA: API klíč nebyl v konfiguraci (User Secrets) nalezen!");
    }
    else
    {
        Console.WriteLine($"INFO: API klíč načten (začíná na: {keyCheck.Substring(0, Math.Min(4, keyCheck.Length))}...)");
    }

    // Samotné volání služby
    var data = await service.GetExchangeRatesAsync("EUR");

    if (string.IsNullOrEmpty(data))
    {
        return Results.Problem("API vrátilo prázdnou odpověď. Zkontroluj konzoli a logy v DB.");
    }

    return Results.Content(data, "application/json");
});



// Testovací endpoint: otevři v prohlížeči http://localhost:5080/status
//pod tímto už je pouze app.run();
app.MapGet("/status", () =>
{
    var html = """
    <!DOCTYPE html>
    <html lang="cs">
    <head>
        <meta charset="UTF-8">
        <meta name="viewport" content="width=device-width, initial-scale=1.0">
        <title>Aplikace funguje</title>
        <style>
            @import url('https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600&display=swap');

            * { margin: 0; padding: 0; box-sizing: border-box; }

            body {
                font-family: 'Inter', system-ui, sans-serif;
                background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
                height: 100vh;
                display: flex;
                align-items: center;
                justify-content: center;
                color: white;
                overflow: hidden;
            }

            .container {
                text-align: center;
                padding: 60px 40px;
                background: rgba(255, 255, 255, 0.1);
                backdrop-filter: blur(20px);
                border-radius: 24px;
                box-shadow: 0 20px 40px rgba(0, 0, 0, 0.2);
                border: 1px solid rgba(255, 255, 255, 0.2);
                max-width: 500px;
                animation: fadeIn 1s ease-out;
            }

            .success-icon {
                width: 120px;
                height: 120px;
                background: #4ade80;
                border-radius: 50%;
                display: flex;
                align-items: center;
                justify-content: center;
                margin: 0 auto 30px;
                box-shadow: 0 10px 30px rgba(74, 222, 128, 0.4);
                animation: scaleIn 0.6s cubic-bezier(0.34, 1.56, 0.64, 1);
            }

            .success-icon::after {
                content: '✓';
                font-size: 70px;
                color: white;
                font-weight: bold;
            }

            h1 {
                font-size: 42px;
                font-weight: 600;
                margin-bottom: 16px;
                letter-spacing: -1px;
            }

            p {
                font-size: 18px;
                opacity: 0.9;
                line-height: 1.6;
                margin-bottom: 40px;
            }

            .status {
                display: inline-flex;
                align-items: center;
                gap: 10px;
                background: rgba(255, 255, 255, 0.2);
                padding: 12px 28px;
                border-radius: 50px;
                font-weight: 500;
                font-size: 17px;
                backdrop-filter: blur(10px);
            }

            .dot {
                width: 12px;
                height: 12px;
                background: #4ade80;
                border-radius: 50%;
                animation: pulse 2s infinite;
            }

            @keyframes fadeIn {
                from { opacity: 0; transform: translateY(30px); }
                to { opacity: 1; transform: translateY(0); }
            }

            @keyframes scaleIn {
                from { transform: scale(0); }
                to { transform: scale(1); }
            }

            @keyframes pulse {
                0%, 100% { opacity: 1; }
                50% { opacity: 0.4; }
            }

            .footer {
                margin-top: 50px;
                font-size: 14px;
                opacity: 0.7;
            }
        </style>
    </head>
    <body>
        <div class="container">
            <div class="success-icon"></div>
            <h1>Výborně!</h1>
            <p>Aplikace běží správně a vše funguje jak má.</p>

            <div class="status">
                <span class="dot"></span>
                Aplikace je online a plně funkční
            </div>

            <div class="footer">
                Vytvořeno pro testování • <span id="current-time"></span>
            </div>
        </div>

        <script>
            function updateTime() {
                const now = new Date();
                const timeString = now.toLocaleTimeString('cs-CZ', {
                    hour: '2-digit',
                    minute: '2-digit',
                    second: '2-digit'
                });
                document.getElementById('current-time').textContent = timeString;
            }

            setInterval(updateTime, 1000);
            updateTime();
        </script>
    </body>
    </html>
    """;

    return Results.Content(html, "text/html; charset=utf-8");
});



app.Run();