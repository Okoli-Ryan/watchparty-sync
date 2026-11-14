using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WatchParty.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rooms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    primary_owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    primary_owner_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    last_active_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    chat_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    chat_salt = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    chat_visibility = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    chat_verifier_ciphertext = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    chat_verifier_iv = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    host_position_current_time = table.Column<double>(type: "double precision", nullable: true),
                    host_position_is_playing = table.Column<bool>(type: "boolean", nullable: true),
                    host_position_updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    playback_current_time = table.Column<double>(type: "double precision", nullable: false),
                    playback_is_playing = table.Column<bool>(type: "boolean", nullable: false),
                    playback_rate = table.Column<double>(type: "double precision", nullable: false),
                    playback_updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    playback_updated_by = table.Column<Guid>(type: "uuid", nullable: false),
                    video_frame_origin = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    video_page_url = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    video_selector = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rooms", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    display_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "chat_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sender_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sender_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    body_ciphertext = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    body_iv = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_chat_messages", x => x.id);
                    table.ForeignKey(
                        name: "fk_chat_messages_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "history_entries",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    last_attended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_favourite = table.Column<bool>(type: "boolean", nullable: false),
                    last_read_at = table.Column<long>(type: "bigint", nullable: true),
                    room_created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    room_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    room_page_url = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_history_entries", x => new { x.user_id, x.room_id });
                    table.ForeignKey(
                        name: "fk_history_entries_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "memberships",
                columns: table => new
                {
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_seen = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_memberships", x => new { x.room_id, x.user_id });
                    table.ForeignKey(
                        name: "fk_memberships_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_memberships_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_chat_messages_room_id_sent_at",
                table: "chat_messages",
                columns: new[] { "room_id", "sent_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_history_entries_user_id_last_attended_at",
                table: "history_entries",
                columns: new[] { "user_id", "last_attended_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_memberships_user_id",
                table: "memberships",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_rooms_created_at",
                table: "rooms",
                column: "created_at",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                table: "users",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "chat_messages");

            migrationBuilder.DropTable(
                name: "history_entries");

            migrationBuilder.DropTable(
                name: "memberships");

            migrationBuilder.DropTable(
                name: "rooms");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
