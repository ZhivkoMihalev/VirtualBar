using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VirtualBar.Infrastructure.Migrations
{
    /// <summary>
    /// Hides the bottle images that linking let a collector store with any address (#131). Data only: no schema change.
    /// Approved by the repository owner on the issue's PR (label <c>db-destructive-approved</c>).
    /// </summary>
    public partial class HideBottleImagesWithArbitraryUrls : Migration
    {
        /// <summary>
        /// Soft-deletes every live image whose <c>Url</c> is neither one of the API's own uploads (a file name straight
        /// under <c>/uploads/bottles/</c>, no deeper path, no <c>..</c>) nor the <c>ImageUrl</c> of a catalog product,
        /// which is all the app itself ever stores. Where such an image was its bottle's cover, the bottle's first
        /// remaining image becomes the cover, so the feed (which shows only a primary image) keeps a picture.
        /// Public so the HTTP tests can run it against rows on SQL Server (<c>BottleImageCleanupTests</c>).
        /// </summary>
        public const string CleanupSql =
            """
            DECLARE @Hidden TABLE ([BottleId] uniqueidentifier NOT NULL, [WasPrimary] bit NOT NULL);

            UPDATE [BottleImages]
            SET [IsDeleted] = CAST(1 AS bit),
                [DeletedAt] = SYSUTCDATETIME(),
                [UpdatedAt] = SYSUTCDATETIME()
            OUTPUT inserted.[BottleId], inserted.[IsPrimary] INTO @Hidden ([BottleId], [WasPrimary])
            WHERE [IsDeleted] = CAST(0 AS bit)
              AND NOT ([Url] LIKE '/uploads/bottles/_%'
                       AND [Url] NOT LIKE '/uploads/bottles/%[/\]%'
                       AND [Url] NOT LIKE '%..%')
              AND NOT EXISTS (SELECT 1 FROM [Products] AS [p] WHERE [p].[ImageUrl] = [BottleImages].[Url]);

            UPDATE [i]
            SET [IsPrimary] = CAST(1 AS bit),
                [UpdatedAt] = SYSUTCDATETIME()
            FROM [BottleImages] AS [i]
            WHERE [i].[Id] IN (
                    SELECT [first].[Id]
                    FROM (SELECT DISTINCT [BottleId] FROM @Hidden WHERE [WasPrimary] = CAST(1 AS bit)) AS [h]
                    CROSS APPLY (
                        SELECT TOP (1) [f].[Id]
                        FROM [BottleImages] AS [f]
                        WHERE [f].[BottleId] = [h].[BottleId] AND [f].[IsDeleted] = CAST(0 AS bit)
                        ORDER BY [f].[SortOrder], [f].[CreatedAt]) AS [first])
              AND NOT EXISTS (
                    SELECT 1 FROM [BottleImages] AS [c]
                    WHERE [c].[BottleId] = [i].[BottleId] AND [c].[IsDeleted] = CAST(0 AS bit) AND [c].[IsPrimary] = CAST(1 AS bit));
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(CleanupSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to undo: the hidden images are soft-deleted rows that stay in the table, and showing them again
            // would bring back the addresses #131 removed.
        }
    }
}
