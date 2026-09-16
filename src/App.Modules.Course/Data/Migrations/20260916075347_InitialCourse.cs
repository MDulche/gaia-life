using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Modules.Course.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCourse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CourseCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Nom = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Couleur = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseCategories", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CourseMagasins",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Nom = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Ordre = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseMagasins", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CourseArticles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Nom = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CategorieId = table.Column<int>(type: "int", nullable: false),
                    MagasinId = table.Column<int>(type: "int", nullable: true),
                    Quantite = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Achete = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DateAjout = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    DateAchat = table.Column<DateTime>(type: "datetime(6)", nullable: true)
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
                })
                .Annotation("MySql:CharSet", "utf8mb4");

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
                column: "Ordre");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CourseArticles");

            migrationBuilder.DropTable(
                name: "CourseCategories");

            migrationBuilder.DropTable(
                name: "CourseMagasins");
        }
    }
}
