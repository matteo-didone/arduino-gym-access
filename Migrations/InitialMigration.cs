using Microsoft.EntityFrameworkCore.Migrations;

namespace ArduinoGymAccess.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Inserisci una riga nella tabella __EFMigrationsHistory senza creare tabelle
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
                    `MigrationId` varchar(150) NOT NULL,
                    `ProductVersion` varchar(32) NOT NULL,
                    PRIMARY KEY (`MigrationId`)
                );
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Non facciamo nulla nel Down poiché non vogliamo eliminare le tabelle esistenti
        }
    }
}