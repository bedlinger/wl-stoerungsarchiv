using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace wls_backend.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriberLineRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Line_Subscribers_SubscriberToken",
                table: "Line");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Subscribers",
                table: "Subscribers");

            migrationBuilder.DropIndex(
                name: "IX_Line_SubscriberToken",
                table: "Line");

            migrationBuilder.DropColumn(
                name: "SubscriberToken",
                table: "Line");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "Subscribers",
                type: "integer",
                nullable: false,
                defaultValue: 0)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Subscribers",
                table: "Subscribers",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "Subscriptions",
                columns: table => new
                {
                    SubscriberId = table.Column<int>(type: "integer", nullable: false),
                    LineId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscriptions", x => new { x.SubscriberId, x.LineId });
                    table.ForeignKey(
                        name: "FK_Subscriptions_Line_LineId",
                        column: x => x.LineId,
                        principalTable: "Line",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Subscriptions_Subscribers_SubscriberId",
                        column: x => x.SubscriberId,
                        principalTable: "Subscribers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Subscribers_Token",
                table: "Subscribers",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_LineId",
                table: "Subscriptions",
                column: "LineId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Subscriptions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Subscribers",
                table: "Subscribers");

            migrationBuilder.DropIndex(
                name: "IX_Subscribers_Token",
                table: "Subscribers");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "Subscribers");

            migrationBuilder.AddColumn<string>(
                name: "SubscriberToken",
                table: "Line",
                type: "text",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Subscribers",
                table: "Subscribers",
                column: "Token");

            migrationBuilder.CreateIndex(
                name: "IX_Line_SubscriberToken",
                table: "Line",
                column: "SubscriberToken");

            migrationBuilder.AddForeignKey(
                name: "FK_Line_Subscribers_SubscriberToken",
                table: "Line",
                column: "SubscriberToken",
                principalTable: "Subscribers",
                principalColumn: "Token");
        }
    }
}
