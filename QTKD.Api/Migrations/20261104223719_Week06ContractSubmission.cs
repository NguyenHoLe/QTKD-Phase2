using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QTKD.Api.Migrations
{
    /// <inheritdoc />
    public partial class Week06ContractSubmission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AttachmentFileName",
                table: "Contracts",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContentSummary",
                table: "Contracts",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ContractSubmission",
                columns: table => new
                {
                    ContractId = table.Column<int>(type: "integer", nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractSubmission", x => x.ContractId);
                    table.ForeignKey(
                        name: "FK_ContractSubmission_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "Contracts",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "AttachmentFileName", "ContentSummary" },
                values: new object[] { null, null });

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_ContractNo",
                table: "Contracts",
                column: "ContractNo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContractSubmission");

            migrationBuilder.DropIndex(
                name: "IX_Contracts_ContractNo",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "AttachmentFileName",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "ContentSummary",
                table: "Contracts");
        }
    }
}
