using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Modules.Stock.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class InitialStockSqlite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StockCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nom = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Couleur = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StockArticles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nom = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    NomNormalise = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Quantite = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: false),
                    Unite = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    SeuilAlerte = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: true),
                    CategorieId = table.Column<int>(type: "INTEGER", nullable: false),
                    DateMaj = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockArticles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockArticles_StockCategories_CategorieId",
                        column: x => x.CategorieId,
                        principalTable: "StockCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockMouvements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ArticleStockId = table.Column<int>(type: "INTEGER", nullable: false),
                    Delta = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: false),
                    Motif = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    DateMouvement = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockMouvements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockMouvements_StockArticles_ArticleStockId",
                        column: x => x.ArticleStockId,
                        principalTable: "StockArticles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockArticles_CategorieId",
                table: "StockArticles",
                column: "CategorieId");

            migrationBuilder.CreateIndex(
                name: "IX_StockArticles_NomNormalise",
                table: "StockArticles",
                column: "NomNormalise",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockCategories_Nom",
                table: "StockCategories",
                column: "Nom",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockMouvements_ArticleStockId",
                table: "StockMouvements",
                column: "ArticleStockId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMouvements_DateMouvement",
                table: "StockMouvements",
                column: "DateMouvement");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StockMouvements");

            migrationBuilder.DropTable(
                name: "StockArticles");

            migrationBuilder.DropTable(
                name: "StockCategories");
        }
    }
}
