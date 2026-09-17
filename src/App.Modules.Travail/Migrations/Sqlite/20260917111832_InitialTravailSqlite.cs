using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Modules.Travail.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class InitialTravailSqlite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CouleurTypeConges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Type = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Couleur = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CouleurTypeConges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Employeurs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nom = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Adresse = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    DateDebut = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DateFin = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employeurs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Conges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EmployeurId = table.Column<int>(type: "INTEGER", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    DateDebut = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DateFin = table.Column<DateTime>(type: "TEXT", nullable: false),
                    NombreJours = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false),
                    Statut = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Commentaire = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Conges_Employeurs_EmployeurId",
                        column: x => x.EmployeurId,
                        principalTable: "Employeurs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FichePaies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EmployeurId = table.Column<int>(type: "INTEGER", nullable: false),
                    Mois = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SalaireBrut = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    SalaireNet = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TotalCotisations = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    DateEmission = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CheminFichier = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FichePaies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FichePaies_Employeurs_EmployeurId",
                        column: x => x.EmployeurId,
                        principalTable: "Employeurs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HeuresSupplementaires",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EmployeurId = table.Column<int>(type: "INTEGER", nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    HeureDebut = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    HeureFin = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    Contexte = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    DureeCalculee = table.Column<decimal>(type: "TEXT", precision: 8, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HeuresSupplementaires", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HeuresSupplementaires_Employeurs_EmployeurId",
                        column: x => x.EmployeurId,
                        principalTable: "Employeurs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SoldeConges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EmployeurId = table.Column<int>(type: "INTEGER", nullable: false),
                    Annee = table.Column<int>(type: "INTEGER", nullable: false),
                    JoursAcquis = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SoldeConges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SoldeConges_Employeurs_EmployeurId",
                        column: x => x.EmployeurId,
                        principalTable: "Employeurs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Conges_EmployeurId_DateDebut",
                table: "Conges",
                columns: new[] { "EmployeurId", "DateDebut" });

            migrationBuilder.CreateIndex(
                name: "IX_CouleurTypeConges_Type",
                table: "CouleurTypeConges",
                column: "Type",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FichePaies_EmployeurId_Mois",
                table: "FichePaies",
                columns: new[] { "EmployeurId", "Mois" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HeuresSupplementaires_EmployeurId_Date",
                table: "HeuresSupplementaires",
                columns: new[] { "EmployeurId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_SoldeConges_EmployeurId_Annee",
                table: "SoldeConges",
                columns: new[] { "EmployeurId", "Annee" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Conges");

            migrationBuilder.DropTable(
                name: "CouleurTypeConges");

            migrationBuilder.DropTable(
                name: "FichePaies");

            migrationBuilder.DropTable(
                name: "HeuresSupplementaires");

            migrationBuilder.DropTable(
                name: "SoldeConges");

            migrationBuilder.DropTable(
                name: "Employeurs");
        }
    }
}
