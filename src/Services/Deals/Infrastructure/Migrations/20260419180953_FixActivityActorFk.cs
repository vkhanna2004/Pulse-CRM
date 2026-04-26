using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PulseCRM.Deals.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixActivityActorFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Activities_Users_ActorId",
                table: "Activities");

            migrationBuilder.DropIndex(
                name: "IX_Activities_ActorId",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "ActorId",
                table: "Activities");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_ActorUserId",
                table: "Activities",
                column: "ActorUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Activities_Users_ActorUserId",
                table: "Activities",
                column: "ActorUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Activities_Users_ActorUserId",
                table: "Activities");

            migrationBuilder.DropIndex(
                name: "IX_Activities_ActorUserId",
                table: "Activities");

            migrationBuilder.AddColumn<Guid>(
                name: "ActorId",
                table: "Activities",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Activities_ActorId",
                table: "Activities",
                column: "ActorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Activities_Users_ActorId",
                table: "Activities",
                column: "ActorId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
