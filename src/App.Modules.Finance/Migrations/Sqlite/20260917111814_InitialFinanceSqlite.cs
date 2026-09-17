using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Modules.Finance.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class InitialFinanceSqlite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nom = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Couleur = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChargesAnnuelles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nom = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Montant = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    MoisEcheance = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChargesAnnuelles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Comptes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nom = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    SoldeInitial = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false, defaultValue: "Courant"),
                    EstPrincipal = table.Column<bool>(type: "INTEGER", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Comptes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FinanceParametres",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    JoursMoyennePrevision = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceParametres", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChargesMensuelles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nom = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Montant = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    CompteId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChargesMensuelles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChargesMensuelles_Comptes_CompteId",
                        column: x => x.CompteId,
                        principalTable: "Comptes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ObjectifsEpargne",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nom = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    MontantCible = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    DateCibleOptionnelle = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompteId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ObjectifsEpargne", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ObjectifsEpargne_Comptes_CompteId",
                        column: x => x.CompteId,
                        principalTable: "Comptes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Transactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CompteId = table.Column<int>(type: "INTEGER", nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Montant = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Categorie = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    EstVirementInterne = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    TransfertId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Transactions_Comptes_CompteId",
                        column: x => x.CompteId,
                        principalTable: "Comptes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Nom",
                table: "Categories",
                column: "Nom",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChargesMensuelles_CompteId",
                table: "ChargesMensuelles",
                column: "CompteId");

            migrationBuilder.CreateIndex(
                name: "IX_ObjectifsEpargne_CompteId",
                table: "ObjectifsEpargne",
                column: "CompteId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_CompteId_Date",
                table: "Transactions",
                columns: new[] { "CompteId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_EstVirementInterne",
                table: "Transactions",
                column: "EstVirementInterne");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_TransfertId",
                table: "Transactions",
                column: "TransfertId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "ChargesAnnuelles");

            migrationBuilder.DropTable(
                name: "ChargesMensuelles");

            migrationBuilder.DropTable(
                name: "FinanceParametres");

            migrationBuilder.DropTable(
                name: "ObjectifsEpargne");

            migrationBuilder.DropTable(
                name: "Transactions");

            migrationBuilder.DropTable(
                name: "Comptes");
        }
    }
}
