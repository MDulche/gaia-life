using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Modules.Course.Data.Migrations
{
    /// <inheritdoc />
    public partial class CourseLiaisonsEtOrdreUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CourseMagasins_Ordre",
                table: "CourseMagasins");

            migrationBuilder.AddColumn<int>(
                name: "ArticleStockId",
                table: "CourseArticles",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PrixEstime",
                table: "CourseArticles",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CourseParametres",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CompteCoursesParDefautId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseParametres", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            // Normalise Ordre (1..n) avant l'index unique, en deux passes pour éviter les collisions.
            migrationBuilder.Sql(
                """
                UPDATE CourseMagasins AS m
                INNER JOIN (
                    SELECT Id, ROW_NUMBER() OVER (ORDER BY Ordre, Nom, Id) AS rn
                    FROM CourseMagasins
                ) AS ranked ON m.Id = ranked.Id
                SET m.Ordre = -ranked.rn;
                """);

            migrationBuilder.Sql("UPDATE CourseMagasins SET Ordre = -Ordre;");

            migrationBuilder.CreateIndex(
                name: "IX_CourseMagasins_Ordre",
                table: "CourseMagasins",
                column: "Ordre",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CourseParametres");

            migrationBuilder.DropIndex(
                name: "IX_CourseMagasins_Ordre",
                table: "CourseMagasins");

            migrationBuilder.DropColumn(
                name: "ArticleStockId",
                table: "CourseArticles");

            migrationBuilder.DropColumn(
                name: "PrixEstime",
                table: "CourseArticles");

            migrationBuilder.CreateIndex(
                name: "IX_CourseMagasins_Ordre",
                table: "CourseMagasins",
                column: "Ordre");
        }
    }
}
