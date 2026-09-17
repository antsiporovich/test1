using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropertyManagement.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicantResidenceUnitExtendedFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Bathrooms",
                table: "Units",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "MonthlyRent",
                table: "Residences",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReasonForLeaving",
                table: "Residences",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AnnualIncome",
                table: "ApplicantInfos",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DateOfBirth",
                table: "ApplicantInfos",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DesiredMoveInDate",
                table: "ApplicantInfos",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Employment",
                table: "ApplicantInfos",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Bathrooms",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "MonthlyRent",
                table: "Residences");

            migrationBuilder.DropColumn(
                name: "ReasonForLeaving",
                table: "Residences");

            migrationBuilder.DropColumn(
                name: "AnnualIncome",
                table: "ApplicantInfos");

            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                table: "ApplicantInfos");

            migrationBuilder.DropColumn(
                name: "DesiredMoveInDate",
                table: "ApplicantInfos");

            migrationBuilder.DropColumn(
                name: "Employment",
                table: "ApplicantInfos");
        }
    }
}
