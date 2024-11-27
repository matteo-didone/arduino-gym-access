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
        // Questo impedisce errori quando gli oggetti JSON hanno riferimenti circolari
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });

// Aggiungiamo il supporto per la documentazione API tramite Swagger
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

// Configuriamo il factory di DbContext con pooling per gestire efficacemente le connessioni
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

// Configuriamo i servizi necessari per la comunicazione con Arduino
builder.Services.Configure<SerialPortSettings>(
    builder.Configuration.GetSection("SerialPort")
);
builder.Services.AddSingleton<SerialPortManager>();

// Creiamo l'applicazione
var app = builder.Build();

// In ambiente di sviluppo, attiviamo Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Configuriamo il pipeline HTTP
app.UseCors("AllowAll");
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Avviamo l'applicazione
app.Run();