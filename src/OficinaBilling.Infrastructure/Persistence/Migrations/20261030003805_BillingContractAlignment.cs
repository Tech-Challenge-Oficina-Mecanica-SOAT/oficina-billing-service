using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OficinaBilling.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BillingContractAlignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MotivoRecusa",
                table: "orcamentos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ValidadeAte",
                table: "orcamentos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                """UPDATE orcamentos SET "ValidadeAte" = "CriadoEm" + INTERVAL '7 days'""");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ValidadeAte",
                table: "orcamentos",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PecaId",
                table: "orcamento_itens",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ServicoId",
                table: "orcamento_itens",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MotivoRecusa",
                table: "orcamentos");

            migrationBuilder.DropColumn(
                name: "ValidadeAte",
                table: "orcamentos");

            migrationBuilder.DropColumn(
                name: "PecaId",
                table: "orcamento_itens");

            migrationBuilder.DropColumn(
                name: "ServicoId",
                table: "orcamento_itens");
        }
    }
}
