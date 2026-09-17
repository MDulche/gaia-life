using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Modules.Stock.Data.Migrations
{
    /// <inheritdoc />
    public partial class StockMouvementsEtUnicite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StockArticles_Nom",
                table: "StockArticles");

            migrationBuilder.AddColumn<string>(
                name: "NomNormalise",
                table: "StockArticles",
                type: "varchar(128)",
                maxLength: 128,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            // Normalisation + désambiguïsation des doublons éventuels avant l'index unique.
            migrationBuilder.Sql("""
                UPDATE StockArticles
                SET NomNormalise = LOWER(TRIM(Nom));
                """);

            migrationBuilder.Sql("""
                UPDATE StockArticles a
                INNER JOIN (
                    SELECT Id,
                           CONCAT(NomNormalise, '-', Id) AS Nouveau
                    FROM StockArticles
                    WHERE NomNormalise IN (
                        SELECT NomNormalise
                        FROM StockArticles
                        GROUP BY NomNormalise
                        HAVING COUNT(*) > 1
                    )
                ) d ON a.Id = d.Id
                SET a.NomNormalise = d.Nouveau,
                    a.Nom = CONCAT(TRIM(a.Nom), ' (', a.Id, ')');
                """);

            migrationBuilder.AlterColumn<string>(
                name: "NomNormalise",
                table: "StockArticles",
                type: "varchar(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(128)",
                oldMaxLength: 128,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "StockMouvements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ArticleStockId = table.Column<int>(type: "int", nullable: false),
                    Delta = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Motif = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DateMouvement = table.Column<DateTime>(type: "datetime(6)", nullable: false)
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
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            // Quantité existante = mouvement initial (source de vérité alignée).
            migrationBuilder.Sql("""
                INSERT INTO StockMouvements (ArticleStockId, Delta, Motif, DateMouvement)
                SELECT Id, Quantite, 'Stock initial', UTC_TIMESTAMP(6)
                FROM StockArticles;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_StockArticles_NomNormalise",
                table: "StockArticles",
                column: "NomNormalise",
                unique: true);

            // CHECK Quantite >= 0 non ajouté ici : incompatible avec le motif « Ajustement inventaire »
            // qui peut laisser une quantité négative (rattrapage). Garde-fou = AjusterQuantiteAsync.
            // Si on restreint l'inventaire à une cible >= 0 uniquement, on pourra ajouter :
            // ALTER TABLE StockArticles ADD CONSTRAINT CK_StockArticles_Quantite CHECK (Quantite >= 0);

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

            migrationBuilder.DropIndex(
                name: "IX_StockArticles_NomNormalise",
                table: "StockArticles");

            migrationBuilder.DropColumn(
                name: "NomNormalise",
                table: "StockArticles");

            migrationBuilder.CreateIndex(
                name: "IX_StockArticles_Nom",
                table: "StockArticles",
                column: "Nom");
        }
    }
}
