using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kart.Analytics.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "analytics_audit_log",
                columns: table => new
                {
                    entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_name = table.Column<string>(type: "text", nullable: false),
                    actor_id = table.Column<string>(type: "text", nullable: false),
                    actor_type = table.Column<string>(type: "text", nullable: false),
                    action = table.Column<string>(type: "text", nullable: false),
                    entity_type = table.Column<string>(type: "text", nullable: false),
                    entity_id = table.Column<string>(type: "text", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    metadata = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analytics_audit_log", x => x.entry_id);
                });

            migrationBuilder.CreateTable(
                name: "analytics_dlq_events",
                columns: table => new
                {
                    dlq_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "text", nullable: false),
                    payload = table.Column<string>(type: "text", nullable: false),
                    failure_reason = table.Column<string>(type: "text", nullable: false),
                    retry_count = table.Column<int>(type: "integer", nullable: false),
                    dlq_landed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reprocessed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analytics_dlq_events", x => x.dlq_id);
                });

            migrationBuilder.CreateTable(
                name: "analytics_pii_redactions",
                columns: table => new
                {
                    redaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<string>(type: "text", nullable: false),
                    triggering_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rows_redacted = table.Column<int>(type: "integer", nullable: false),
                    redacted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analytics_pii_redactions", x => x.redaction_id);
                });

            migrationBuilder.CreateTable(
                name: "analytics_raw_events",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    contains_pii = table.Column<bool>(type: "boolean", nullable: false),
                    ingested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    pii_redacted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: false),
                    event_type = table.Column<string>(type: "text", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    partition_key = table.Column<string>(type: "text", nullable: false),
                    publisher_service = table.Column<string>(type: "text", nullable: false),
                    schema_id = table.Column<string>(type: "text", nullable: false),
                    schema_version_label = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analytics_raw_events", x => x.event_id);
                });

            migrationBuilder.CreateTable(
                name: "analytics_reconciliation_runs",
                columns: table => new
                {
                    run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    run_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analytics_reconciliation_runs", x => x.run_id);
                    table.CheckConstraint("ck_analytics_reconciliation_runs_status", "status IN ('running', 'completed', 'failed')");
                });

            migrationBuilder.CreateIndex(
                name: "idx_analytics_audit_log_entity",
                table: "analytics_audit_log",
                columns: new[] { "entity_type", "entity_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "idx_analytics_dlq_events_pending",
                table: "analytics_dlq_events",
                column: "dlq_landed_at",
                filter: "reprocessed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "idx_analytics_pii_redactions_user",
                table: "analytics_pii_redactions",
                columns: new[] { "user_id", "redacted_at" });

            migrationBuilder.CreateIndex(
                name: "idx_analytics_raw_events_pii_pending",
                table: "analytics_raw_events",
                column: "contains_pii",
                filter: "contains_pii = true AND pii_redacted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "uq_analytics_reconciliation_runs_run_date",
                table: "analytics_reconciliation_runs",
                column: "run_date",
                unique: true);

            // idx_analytics_raw_events_type_occurred (database-design.md) — added as raw SQL
            // because EF Core's fluent HasIndex lambda overload rejects a multi-property
            // anonymous-type expression that reaches into a ComplexProperty's nested members
            // (design-time-only limitation of the fluent API, not of the resulting index itself).
            migrationBuilder.Sql(
                "CREATE INDEX idx_analytics_raw_events_type_occurred ON analytics_raw_events (event_type, occurred_at);");

            // Deliberate deviation from database-design.md's illustrative "PARTITION BY RANGE
            // (ingested_at)" DDL: native Postgres declarative partitioning requires every
            // unique/primary-key constraint on the table to include the partition key column.
            // `event_id` is this table's idempotency PK (the atomic ON CONFLICT (event_id) DO
            // UPDATE upsert target that guarantees no double-counting on replay/redelivery) and
            // must stay globally unique on its own — a composite (event_id, ingested_at) key
            // would reopen exactly the double-counting race this schema exists to prevent, since
            // two concurrent first-deliveries of the same never-before-seen event_id could each
            // compute a slightly different "now" and both succeed as two distinct rows. Given the
            // explicit priority on idempotency/no-race-conditions over this table's own D3 note
            // that tiered hot/cold partitioning is "a later cost-optimization detail, not
            // load-bearing yet," this table ships as a normal (non-partitioned) table for this
            // build; see the completion summary for how to add partitioning safely later
            // (partition on a derived, monotonic column instead of relying on a raw-Postgres
            // unique constraint spanning the partition key).
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "analytics_audit_log");

            migrationBuilder.DropTable(
                name: "analytics_dlq_events");

            migrationBuilder.DropTable(
                name: "analytics_pii_redactions");

            migrationBuilder.DropTable(
                name: "analytics_raw_events");

            migrationBuilder.DropTable(
                name: "analytics_reconciliation_runs");
        }
    }
}
