using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "email_outbox",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    to_email = table.Column<string>(type: "varchar(254)", maxLength: 254, nullable: false),
                    subject = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    html_body = table.Column<string>(type: "mediumtext", nullable: false),
                    status = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                    attempts = table.Column<int>(type: "int", nullable: false),
                    last_error = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    sent_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_email_outbox", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "homestays",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    slug = table.Column<string>(type: "varchar(140)", maxLength: 140, nullable: false),
                    name = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false),
                    province = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false),
                    address = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    check_in_time = table.Column<TimeSpan>(type: "time", nullable: false),
                    check_out_time = table.Column<TimeSpan>(type: "time", nullable: false),
                    deposit_percent = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    hold_hours = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    free_cancel_days = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    partial_refund_days = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    partial_refund_percent = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    status = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_homestays", x => x.id);
                    table.CheckConstraint("ck_homestays_deposit_percent", "deposit_percent <= 100");
                    table.CheckConstraint("ck_homestays_hold_hours", "hold_hours BETWEEN 1 AND 72");
                    table.CheckConstraint("ck_homestays_refund_policy", "partial_refund_days <= free_cancel_days AND partial_refund_percent <= 100");
                    table.CheckConstraint("ck_homestays_status", "status IN ('active','hidden')");
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    email = table.Column<string>(type: "varchar(254)", maxLength: 254, nullable: false),
                    password_hash = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    full_name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    phone = table.Column<string>(type: "varchar(15)", maxLength: 15, nullable: true),
                    role = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                    is_active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    last_login_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                    table.CheckConstraint("ck_users_role", "role IN ('guest','staff','admin')");
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "homestay_amenities",
                columns: table => new
                {
                    homestay_id = table.Column<long>(type: "bigint", nullable: false),
                    code = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_homestay_amenities", x => new { x.homestay_id, x.code });
                    table.ForeignKey(
                        name: "fk_homestay_amenities_homestays_homestay_id",
                        column: x => x.homestay_id,
                        principalTable: "homestays",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "photos",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    homestay_id = table.Column<long>(type: "bigint", nullable: false),
                    file_name = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false),
                    width = table.Column<int>(type: "int", nullable: false),
                    height = table.Column<int>(type: "int", nullable: false),
                    sort_order = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_photos", x => x.id);
                    table.ForeignKey(
                        name: "fk_photos_homestays_homestay_id",
                        column: x => x.homestay_id,
                        principalTable: "homestays",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "rooms",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    homestay_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    bed_info = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: true),
                    area_m2 = table.Column<short>(type: "smallint", nullable: true),
                    base_guests = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    max_guests = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    min_nights = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    base_price = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    weekend_price = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: true),
                    extra_guest_fee = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    cleaning_fee = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    status = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                    sort_order = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rooms", x => x.id);
                    table.CheckConstraint("ck_rooms_guests", "base_guests >= 1 AND base_guests <= max_guests AND max_guests <= 20");
                    table.CheckConstraint("ck_rooms_min_nights", "min_nights BETWEEN 1 AND 30");
                    table.CheckConstraint("ck_rooms_prices", "base_price > 0 AND (weekend_price IS NULL OR weekend_price > 0) AND extra_guest_fee >= 0 AND cleaning_fee >= 0");
                    table.CheckConstraint("ck_rooms_status", "status IN ('active','inactive')");
                    table.ForeignKey(
                        name: "fk_rooms_homestays_homestay_id",
                        column: x => x.homestay_id,
                        principalTable: "homestays",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    user_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    token_hash = table.Column<string>(type: "char(64)", fixedLength: true, maxLength: 64, nullable: false),
                    expires_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    replaced_by_hash = table.Column<string>(type: "char(64)", fixedLength: true, maxLength: 64, nullable: true),
                    user_agent = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_refresh_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "bookings",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    code = table.Column<string>(type: "char(12)", fixedLength: true, maxLength: 12, nullable: false),
                    homestay_id = table.Column<long>(type: "bigint", nullable: false),
                    room_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<Guid>(type: "char(36)", nullable: true),
                    guest_name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    guest_phone = table.Column<string>(type: "varchar(15)", maxLength: 15, nullable: false),
                    guest_email = table.Column<string>(type: "varchar(254)", maxLength: 254, nullable: true),
                    guests = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    nights = table.Column<short>(type: "smallint", nullable: false),
                    check_in = table.Column<DateTime>(type: "date", nullable: false),
                    check_out = table.Column<DateTime>(type: "date", nullable: false),
                    room_total = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    extra_guest_total = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    cleaning_fee = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    total = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    deposit_amount = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    refund_amount = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    status = table.Column<string>(type: "varchar(12)", maxLength: 12, nullable: false),
                    source = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                    guest_note = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    internal_note = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    cancel_reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    hold_until = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    confirmed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    checked_in_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    checked_out_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    cancelled_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    created_by_id = table.Column<Guid>(type: "char(36)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bookings", x => x.id);
                    table.CheckConstraint("ck_bookings_amounts", "room_total >= 0 AND extra_guest_total >= 0 AND cleaning_fee >= 0 AND total >= 0 AND deposit_amount >= 0 AND deposit_amount <= total AND refund_amount >= 0");
                    table.CheckConstraint("ck_bookings_dates", "check_out > check_in");
                    table.CheckConstraint("ck_bookings_guests", "guests BETWEEN 1 AND 20 AND nights >= 1");
                    table.CheckConstraint("ck_bookings_source", "source IN ('web','phone','walk_in','ota')");
                    table.CheckConstraint("ck_bookings_status", "status IN ('pending','confirmed','checked_in','checked_out','cancelled','expired','no_show')");
                    table.ForeignKey(
                        name: "fk_bookings_homestays_homestay_id",
                        column: x => x.homestay_id,
                        principalTable: "homestays",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_bookings_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_bookings_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_bookings_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "rate_overrides",
                columns: table => new
                {
                    room_id = table.Column<long>(type: "bigint", nullable: false),
                    date = table.Column<DateTime>(type: "date", nullable: false),
                    price = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    note = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rate_overrides", x => new { x.room_id, x.date });
                    table.CheckConstraint("ck_rate_overrides_price", "price > 0");
                    table.ForeignKey(
                        name: "fk_rate_overrides_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "room_blocks",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    room_id = table.Column<long>(type: "bigint", nullable: false),
                    from_date = table.Column<DateTime>(type: "date", nullable: false),
                    to_date = table.Column<DateTime>(type: "date", nullable: false),
                    reason = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    created_by_id = table.Column<Guid>(type: "char(36)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_room_blocks", x => x.id);
                    table.CheckConstraint("ck_room_blocks_dates", "to_date >= from_date");
                    table.ForeignKey(
                        name: "fk_room_blocks_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_room_blocks_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "booking_events",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    booking_id = table.Column<long>(type: "bigint", nullable: false),
                    type = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    from_status = table.Column<string>(type: "varchar(12)", maxLength: 12, nullable: true),
                    to_status = table.Column<string>(type: "varchar(12)", maxLength: 12, nullable: true),
                    message = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    actor_id = table.Column<Guid>(type: "char(36)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_booking_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_booking_events_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_booking_events_users_actor_id",
                        column: x => x.actor_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "booking_nights",
                columns: table => new
                {
                    booking_id = table.Column<long>(type: "bigint", nullable: false),
                    night = table.Column<DateTime>(type: "date", nullable: false),
                    price = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_booking_nights", x => new { x.booking_id, x.night });
                    table.ForeignKey(
                        name: "fk_booking_nights_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    booking_id = table.Column<long>(type: "bigint", nullable: false),
                    kind = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                    method = table.Column<string>(type: "varchar(15)", maxLength: 15, nullable: false),
                    amount = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    paid_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    note = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    recorded_by_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payments", x => x.id);
                    table.CheckConstraint("ck_payments_amount", "amount > 0");
                    table.CheckConstraint("ck_payments_kind", "kind IN ('deposit','balance','extra','refund')");
                    table.CheckConstraint("ck_payments_method", "method IN ('cash','bank_transfer','card','ewallet')");
                    table.ForeignKey(
                        name: "fk_payments_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_payments_users_recorded_by_id",
                        column: x => x.recorded_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "reviews",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    booking_id = table.Column<long>(type: "bigint", nullable: false),
                    homestay_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    rating = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    comment = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    is_hidden = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reviews", x => x.id);
                    table.CheckConstraint("ck_reviews_rating", "rating BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "fk_reviews_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_reviews_homestays_homestay_id",
                        column: x => x.homestay_id,
                        principalTable: "homestays",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_reviews_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "room_nights",
                columns: table => new
                {
                    room_id = table.Column<long>(type: "bigint", nullable: false),
                    night = table.Column<DateTime>(type: "date", nullable: false),
                    booking_id = table.Column<long>(type: "bigint", nullable: true),
                    block_id = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_room_nights", x => new { x.room_id, x.night });
                    table.CheckConstraint("ck_room_nights_owner", "(booking_id IS NULL) <> (block_id IS NULL)");
                    table.ForeignKey(
                        name: "fk_room_nights_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_room_nights_room_blocks_block_id",
                        column: x => x.block_id,
                        principalTable: "room_blocks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_room_nights_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_booking_events_actor_id",
                table: "booking_events",
                column: "actor_id");

            migrationBuilder.CreateIndex(
                name: "ix_booking_events_booking_id_id",
                table: "booking_events",
                columns: new[] { "booking_id", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_bookings_code",
                table: "bookings",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_bookings_created_by_id",
                table: "bookings",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_bookings_guest_phone",
                table: "bookings",
                column: "guest_phone");

            migrationBuilder.CreateIndex(
                name: "ix_bookings_homestay_id_check_in",
                table: "bookings",
                columns: new[] { "homestay_id", "check_in" });

            migrationBuilder.CreateIndex(
                name: "ix_bookings_room_id_check_in",
                table: "bookings",
                columns: new[] { "room_id", "check_in" });

            migrationBuilder.CreateIndex(
                name: "ix_bookings_status_hold_until",
                table: "bookings",
                columns: new[] { "status", "hold_until" });

            migrationBuilder.CreateIndex(
                name: "ix_bookings_user_id_check_in",
                table: "bookings",
                columns: new[] { "user_id", "check_in" });

            migrationBuilder.CreateIndex(
                name: "ix_email_outbox_status_id",
                table: "email_outbox",
                columns: new[] { "status", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_homestays_province_status",
                table: "homestays",
                columns: new[] { "province", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_homestays_slug",
                table: "homestays",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_payments_booking_id_paid_at",
                table: "payments",
                columns: new[] { "booking_id", "paid_at" });

            migrationBuilder.CreateIndex(
                name: "ix_payments_paid_at",
                table: "payments",
                column: "paid_at");

            migrationBuilder.CreateIndex(
                name: "ix_payments_recorded_by_id",
                table: "payments",
                column: "recorded_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_photos_homestay_id_sort_order",
                table: "photos",
                columns: new[] { "homestay_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_token_hash",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_user_id",
                table: "refresh_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_reviews_booking_id",
                table: "reviews",
                column: "booking_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reviews_homestay_id_is_hidden",
                table: "reviews",
                columns: new[] { "homestay_id", "is_hidden" });

            migrationBuilder.CreateIndex(
                name: "ix_reviews_user_id",
                table: "reviews",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_room_blocks_created_by_id",
                table: "room_blocks",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_room_blocks_room_id_from_date",
                table: "room_blocks",
                columns: new[] { "room_id", "from_date" });

            migrationBuilder.CreateIndex(
                name: "ix_room_nights_block_id",
                table: "room_nights",
                column: "block_id");

            migrationBuilder.CreateIndex(
                name: "ix_room_nights_booking_id",
                table: "room_nights",
                column: "booking_id");

            migrationBuilder.CreateIndex(
                name: "ix_rooms_homestay_id_name",
                table: "rooms",
                columns: new[] { "homestay_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_phone",
                table: "users",
                column: "phone");

            migrationBuilder.CreateIndex(
                name: "ix_users_role_is_active",
                table: "users",
                columns: new[] { "role", "is_active" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "booking_events");

            migrationBuilder.DropTable(
                name: "booking_nights");

            migrationBuilder.DropTable(
                name: "email_outbox");

            migrationBuilder.DropTable(
                name: "homestay_amenities");

            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropTable(
                name: "photos");

            migrationBuilder.DropTable(
                name: "rate_overrides");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "reviews");

            migrationBuilder.DropTable(
                name: "room_nights");

            migrationBuilder.DropTable(
                name: "bookings");

            migrationBuilder.DropTable(
                name: "room_blocks");

            migrationBuilder.DropTable(
                name: "rooms");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "homestays");
        }
    }
}
