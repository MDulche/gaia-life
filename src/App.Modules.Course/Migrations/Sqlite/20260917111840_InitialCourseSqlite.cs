using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Modules.Course.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class InitialCourseSqlite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CourseCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nom = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Couleur = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CourseMagasins",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nom = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Ordre = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseMagasins", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CourseParametres",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CompteCoursesParDefautId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseParametres", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CourseArticles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nom = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    CategorieId = table.Column<int>(type: "INTEGER", nullable: false),
                    MagasinId = table.Column<int>(type: "INTEGER", nullable: true),
                    Quantite = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    ArticleStockId = table.Column<int>(type: "INTEGER", nullable: true),
                    PrixEstime = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true),
                    Achete = table.Column<bool>(type: "INTEGER", nullable: false),
                    DateAjout = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DateAchat = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseArticles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourseArticles_CourseCategories_CategorieId",
                        column: x => x.CategorieId,
                        principalTable: "CourseCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CourseArticles_CourseMagasins_MagasinId",
                        column: x => x.MagasinId,
                        principalTable: "CourseMagasins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CourseArticles_Achete",
                table: "CourseArticles",
                column: "Achete");

            migrationBuilder.CreateIndex(
                name: "IX_CourseArticles_CategorieId",
                table: "CourseArticles",
                column: "CategorieId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseArticles_DateAchat",
                table: "CourseArticles",
                column: "DateAchat");

            migrationBuilder.CreateIndex(
                name: "IX_CourseArticles_MagasinId_Achete",
                table: "CourseArticles",
                columns: new[] { "MagasinId", "Achete" });

            migrationBuilder.CreateIndex(
                name: "IX_CourseCategories_Nom",
                table: "CourseCategories",
                column: "Nom",
                unique: true);

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
                name: "CourseArticles");

            migrationBuilder.DropTable(
                name: "CourseParametres");

            migrationBuilder.DropTable(
                name: "CourseCategories");

            migrationBuilder.DropTable(
                name: "CourseMagasins");
        }
    }
}
