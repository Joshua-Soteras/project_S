using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ProjectS.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSurveySchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Surveys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    BlobUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "queued"),
                    TotalRows = table.Column<int>(type: "integer", nullable: false),
                    ProcessedRows = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    UploadedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Surveys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SurveyColumns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SurveyId = table.Column<int>(type: "integer", nullable: false),
                    ColumnName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ColumnType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "text"),
                    AnalyzeSentiment = table.Column<bool>(type: "boolean", nullable: false),
                    ColumnIndex = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SurveyColumns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SurveyColumns_Surveys_SurveyId",
                        column: x => x.SurveyId,
                        principalTable: "Surveys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SurveyResponses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SurveyId = table.Column<int>(type: "integer", nullable: false),
                    RowIndex = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SurveyResponses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SurveyResponses_Surveys_SurveyId",
                        column: x => x.SurveyId,
                        principalTable: "Surveys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "KpiAggregates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SurveyId = table.Column<int>(type: "integer", nullable: false),
                    ColumnId = table.Column<int>(type: "integer", nullable: false),
                    TotalResponses = table.Column<int>(type: "integer", nullable: false),
                    AvgPositive = table.Column<float>(type: "real", nullable: false),
                    AvgNeutral = table.Column<float>(type: "real", nullable: false),
                    AvgNegative = table.Column<float>(type: "real", nullable: false),
                    CountPositive = table.Column<int>(type: "integer", nullable: false),
                    CountNeutral = table.Column<int>(type: "integer", nullable: false),
                    CountNegative = table.Column<int>(type: "integer", nullable: false),
                    ComputedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KpiAggregates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KpiAggregates_SurveyColumns_ColumnId",
                        column: x => x.ColumnId,
                        principalTable: "SurveyColumns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KpiAggregates_Surveys_SurveyId",
                        column: x => x.SurveyId,
                        principalTable: "Surveys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ResponseValues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ResponseId = table.Column<int>(type: "integer", nullable: false),
                    ColumnId = table.Column<int>(type: "integer", nullable: false),
                    RawValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResponseValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResponseValues_SurveyColumns_ColumnId",
                        column: x => x.ColumnId,
                        principalTable: "SurveyColumns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResponseValues_SurveyResponses_ResponseId",
                        column: x => x.ResponseId,
                        principalTable: "SurveyResponses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SentimentResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ResponseValueId = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PositiveScore = table.Column<float>(type: "real", nullable: false),
                    NeutralScore = table.Column<float>(type: "real", nullable: false),
                    NegativeScore = table.Column<float>(type: "real", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SentimentResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SentimentResults_ResponseValues_ResponseValueId",
                        column: x => x.ResponseValueId,
                        principalTable: "ResponseValues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KpiAggregates_ColumnId",
                table: "KpiAggregates",
                column: "ColumnId");

            migrationBuilder.CreateIndex(
                name: "IX_KpiAggregates_SurveyId_ColumnId",
                table: "KpiAggregates",
                columns: new[] { "SurveyId", "ColumnId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResponseValues_ColumnId",
                table: "ResponseValues",
                column: "ColumnId");

            migrationBuilder.CreateIndex(
                name: "IX_ResponseValues_ResponseId",
                table: "ResponseValues",
                column: "ResponseId");

            migrationBuilder.CreateIndex(
                name: "IX_ResponseValues_ResponseId_ColumnId",
                table: "ResponseValues",
                columns: new[] { "ResponseId", "ColumnId" });

            migrationBuilder.CreateIndex(
                name: "IX_SentimentResults_ResponseValueId",
                table: "SentimentResults",
                column: "ResponseValueId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SurveyColumns_SurveyId",
                table: "SurveyColumns",
                column: "SurveyId");

            migrationBuilder.CreateIndex(
                name: "IX_SurveyResponses_SurveyId",
                table: "SurveyResponses",
                column: "SurveyId");

            migrationBuilder.CreateIndex(
                name: "IX_SurveyResponses_SurveyId_RowIndex",
                table: "SurveyResponses",
                columns: new[] { "SurveyId", "RowIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_Surveys_Status",
                table: "Surveys",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Surveys_UploadedBy",
                table: "Surveys",
                column: "UploadedBy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KpiAggregates");

            migrationBuilder.DropTable(
                name: "SentimentResults");

            migrationBuilder.DropTable(
                name: "ResponseValues");

            migrationBuilder.DropTable(
                name: "SurveyColumns");

            migrationBuilder.DropTable(
                name: "SurveyResponses");

            migrationBuilder.DropTable(
                name: "Surveys");
        }
    }
}
