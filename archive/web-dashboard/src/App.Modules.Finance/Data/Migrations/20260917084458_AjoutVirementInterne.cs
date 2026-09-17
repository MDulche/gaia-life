using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Modules.Finance.Data.Migrations
{
    /// <inheritdoc />
    public partial class AjoutVirementInterne : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EstVirementInterne",
                table: "Transactions",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "TransfertId",
                table: "Transactions",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_EstVirementInterne",
                table: "Transactions",
                column: "EstVirementInterne");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_TransfertId",
                table: "Transactions",
                column: "TransfertId");

            // Données historiques : l'ancien libellé « Versement épargne » devient un virement interne.
            migrationBuilder.Sql(
                """
                UPDATE Transactions
                SET EstVirementInterne = 1
                WHERE Categorie = 'Versement épargne';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_EstVirementInterne",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_TransfertId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "EstVirementInterne",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "TransfertId",
                table: "Transactions");
        }
    }
}
