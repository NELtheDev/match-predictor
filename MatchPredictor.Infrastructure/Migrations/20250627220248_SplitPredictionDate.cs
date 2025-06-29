using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MatchPredictor.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SplitPredictionDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "MatchDate",
                table: "Predictions",
                newName: "Time");

            migrationBuilder.AddColumn<string>(
                name: "Date",
                table: "Predictions",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Date",
                table: "Predictions");

            migrationBuilder.RenameColumn(
                name: "Time",
                table: "Predictions",
                newName: "MatchDate");
        }
    }
}
