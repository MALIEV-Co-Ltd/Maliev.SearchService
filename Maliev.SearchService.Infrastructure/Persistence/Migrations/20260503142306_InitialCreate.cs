using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maliev.SearchService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "search_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_service = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    resource_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    resource_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    subtitle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    keywords = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    status = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    required_permission = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_search_documents", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_search_documents_is_deleted",
                table: "search_documents",
                column: "is_deleted");

            migrationBuilder.CreateIndex(
                name: "ix_search_documents_resource_type",
                table: "search_documents",
                column: "resource_type");

            migrationBuilder.CreateIndex(
                name: "ix_search_documents_source_service",
                table: "search_documents",
                column: "source_service");

            migrationBuilder.CreateIndex(
                name: "ux_search_documents_source_resource",
                table: "search_documents",
                columns: new[] { "source_service", "resource_type", "resource_id" },
                unique: true);

            migrationBuilder.Sql("""
                CREATE INDEX ix_search_documents_full_text
                ON search_documents
                USING GIN (
                    to_tsvector(
                        'simple',
                        coalesce(title, '') || ' ' ||
                        coalesce(subtitle, '') || ' ' ||
                        coalesce(summary, '') || ' ' ||
                        coalesce(keywords, '')
                    )
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "search_documents");
        }
    }
}
