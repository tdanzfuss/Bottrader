using Microsoft.EntityFrameworkCore.Migrations;

namespace PersistanceService.Migrations
{
    public partial class ChangeOrderBookEntries : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Volume",
                table: "OrderBookEntries",
                newName: "MinPrice");

            migrationBuilder.RenameColumn(
                name: "Price",
                table: "OrderBookEntries",
                newName: "MaxPrice");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "MinPrice",
                table: "OrderBookEntries",
                newName: "Volume");

            migrationBuilder.RenameColumn(
                name: "MaxPrice",
                table: "OrderBookEntries",
                newName: "Price");
        }
    }
}
