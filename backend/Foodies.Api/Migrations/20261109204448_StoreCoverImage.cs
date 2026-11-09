using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Foodies.Api.Migrations
{
    /// <inheritdoc />
    public partial class StoreCoverImage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CoverImageUrl",
                table: "Stores",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
            // Preserve the sample storefronts while moving their old UI images into data.
            migrationBuilder.Sql("UPDATE Stores SET CoverImageUrl = 'https://images.unsplash.com/photo-1547592180-85f173990554?auto=format&fit=crop&w=900&q=80' WHERE Slug = 'la-cocina-de-barrio'");
            migrationBuilder.Sql("UPDATE Stores SET CoverImageUrl = 'https://images.unsplash.com/photo-1512621776951-a57141f2eefd?auto=format&fit=crop&w=900&q=80' WHERE Slug = 'verde-y-grano'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CoverImageUrl",
                table: "Stores");
        }
    }
}
