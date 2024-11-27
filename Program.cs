using ArduinoGymAccess.Data;
using ArduinoGymAccess.Models;
using ArduinoGymAccess.Services;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Configuriamo i controller con la gestione delle referenze cicliche nei JSON
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });

// Configuriamo la documentazione API tramite Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configuriamo CORS per permettere richieste da altri domini
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder =>
        {
            builder.AllowAnyOrigin()
                   .AllowAnyMethod()
                   .AllowAnyHeader();
        });
});

// Configuriamo il factory di DbContext con pooling
builder.Services.AddPooledDbContextFactory<AppDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        new MySqlServerVersion(new Version(8, 0, 34)),
        mySqlOptions => mySqlOptions
            .EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorNumbersToAdd: null
            )
    )
);

// Configuriamo i servizi per la gestione della comunicazione seriale
builder.Services.Configure<SerialPortSettings>(
    builder.Configuration.GetSection("SerialPort")
);
builder.Services.AddSingleton<SerialPortManager>();

builder.Services.AddHostedService<SerialEventHandler>();

// Creiamo l'applicazione
var app = builder.Build();

// Configuriamo Swagger in ambiente di sviluppo
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Configuriamo la pipeline HTTP
app.UseCors("AllowAll");
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Avviamo l'applicazione
try
{
    // Aggiungi un log per verificare il corretto avvio dell'applicazione
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Avvio dell'applicazione...");

    // Recupera e inizializza SerialPortManager
    var serialPortManager = app.Services.GetRequiredService<SerialPortManager>();
    if (serialPortManager.IsConnected)
    {
        logger.LogInformation("SerialPortManager è connesso alla porta: {PortName}", serialPortManager?.GetAvailablePorts()?.FirstOrDefault());
    }
    else
    {
        logger.LogWarning("SerialPortManager non è connesso. Verifica la configurazione della porta seriale.");
    }

    // Esegui l'applicazione
    app.Run();
}
catch (Exception ex)
{
    // Log di errore in caso di problemi durante l'avvio dell'applicazione
    Console.WriteLine($"Errore critico durante l'avvio: {ex.Message}");
    throw;
}
