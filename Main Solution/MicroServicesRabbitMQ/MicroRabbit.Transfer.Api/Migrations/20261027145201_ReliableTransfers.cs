using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroRabbit.Transfer.Api.Migrations
{
    /// <inheritdoc />
    public partial class ReliableTransfers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "EventId",
                table: "TransferLogs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql("UPDATE TransferLogs SET EventId = NEWID() WHERE EventId IS NULL");
            migrationBuilder.AlterColumn<Guid>(name: "EventId", table: "TransferLogs", type: "uniqueidentifier", nullable: false, oldClrType: typeof(Guid), oldType: "uniqueidentifier", oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransferLogs_EventId",
                table: "TransferLogs",
                column: "EventId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TransferLogs_EventId",
                table: "TransferLogs");

            migrationBuilder.DropColumn(
                name: "EventId",
                table: "TransferLogs");
        }
    }
}
