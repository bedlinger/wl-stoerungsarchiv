using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace wls_backend.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscribersTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SubscriberToken",
                table: "Line",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Subscribers",
                columns: table => new
                {
                    Token = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscribers", x => x.Token);
                });

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Line_Subscribers_SubscriberToken",
                table: "Line");

            migrationBuilder.DropTable(
                name: "Subscribers");

            migrationBuilder.DropIndex(
                name: "IX_Line_SubscriberToken",
                table: "Line");

            migrationBuilder.DropColumn(
                name: "SubscriberToken",
                table: "Line");
        }
    }
}
