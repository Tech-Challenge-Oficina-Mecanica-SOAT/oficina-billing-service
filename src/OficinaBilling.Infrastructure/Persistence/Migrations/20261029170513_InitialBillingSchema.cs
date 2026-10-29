using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OficinaBilling.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialBillingSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "idempotency_keys",
                columns: table => new
                {
                    Chave = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProcessadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_idempotency_keys", x => x.Chave);
                });

            migrationBuilder.CreateTable(
                name: "orcamentos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrdemServicoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ValorTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AprovadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CanceladoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_orcamentos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Topico = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OcorridoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Tentativas = table.Column<int>(type: "integer", nullable: false),
                    UltimoErro = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "pagamentos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrcamentoId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrdemServicoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Valor = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MercadoPagoPreferenceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LinkPagamento = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MercadoPagoPaymentId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConfirmadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pagamentos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "orcamento_itens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Descricao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Quantidade = table.Column<int>(type: "integer", nullable: false),
                    PrecoUnitario = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    OrcamentoId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_orcamento_itens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_orcamento_itens_orcamentos_OrcamentoId",
                        column: x => x.OrcamentoId,
                        principalTable: "orcamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_orcamento_itens_OrcamentoId",
                table: "orcamento_itens",
                column: "OrcamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_orcamentos_OrdemServicoId",
                table: "orcamentos",
                column: "OrdemServicoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_OcorridoEm",
                table: "outbox_messages",
                column: "OcorridoEm",
                filter: "\"ProcessadoEm\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_pagamentos_MercadoPagoPaymentId",
                table: "pagamentos",
                column: "MercadoPagoPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_pagamentos_OrdemServicoId",
                table: "pagamentos",
                column: "OrdemServicoId",
                unique: true,
                filter: "\"Status\" IN ('Pendente','Confirmado')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "idempotency_keys");

            migrationBuilder.DropTable(
                name: "orcamento_itens");

            migrationBuilder.DropTable(
                name: "outbox_messages");

            migrationBuilder.DropTable(
                name: "pagamentos");

            migrationBuilder.DropTable(
                name: "orcamentos");
        }
    }
}
