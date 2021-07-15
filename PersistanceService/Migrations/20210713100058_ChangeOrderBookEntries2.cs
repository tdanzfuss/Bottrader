using Microsoft.EntityFrameworkCore.Migrations;

namespace PersistanceService.Migrations
{
    public partial class ChangeOrderBookEntries2 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Volume",
                table: "OrderBookEntries",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Volume",
                table: "OrderBookEntries");
        }
    }
}
