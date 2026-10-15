using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace loans_service.Migrations
{
    /// <inheritdoc />
    public partial class LoanStatusCreatedIndexAndXminConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "loans",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateIndex(
                name: "ix_loans_status_created_at",
                table: "loans",
                columns: new[] { "status", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_loans_status_created_at",
                table: "loans");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "loans");
        }
    }
}
