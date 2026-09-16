using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Modules.Travail.Data.Migrations
{
    /// <inheritdoc />
    public partial class CouleurTypeConge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CouleurTypeConges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Type = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Couleur = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CouleurTypeConges", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_CouleurTypeConges_Type",
                table: "CouleurTypeConges",
                column: "Type",
                unique: true);

            migrationBuilder.InsertData(
                table: "CouleurTypeConges",
                columns: new[] { "Id", "Type", "Couleur" },
                values: new object[,]
                {
                    { 1, "Paye", "#0d6efd" },
                    { 2, "SansSolde", "#6c757d" },
                    { 3, "Maladie", "#dc3545" },
                    { 4, "RTT", "#20c997" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CouleurTypeConges");
        }
    }
}
