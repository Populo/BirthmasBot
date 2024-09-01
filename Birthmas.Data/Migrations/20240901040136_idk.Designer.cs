﻿// <auto-generated />
using System;
using Birthmas.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace Birthmas.Data.Migrations
{
    [DbContext(typeof(BirthmasContext))]
    [Migration("20240901040136_idk")]
    partial class idk
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.8")
                .HasAnnotation("Relational:MaxIdentifierLength", 64);

            MySqlModelBuilderExtensions.AutoIncrementColumns(modelBuilder);

            modelBuilder.Entity("Birthmas.Data.Birthmas", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("char(36)");

                    b.Property<ulong>("PersonUserId")
                        .HasColumnType("bigint unsigned");

                    b.Property<ulong>("ServerConfigServerId")
                        .HasColumnType("bigint unsigned");

                    b.HasKey("Id");

                    b.HasIndex("PersonUserId");

                    b.HasIndex("ServerConfigServerId");

                    b.ToTable("Birthmas");
                });

            modelBuilder.Entity("Birthmas.Data.Config", b =>
                {
                    b.Property<string>("Name")
                        .HasColumnType("varchar(255)");

                    b.Property<string>("Value")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.HasKey("Name");

                    b.ToTable("Configs");
                });

            modelBuilder.Entity("Birthmas.Data.Person", b =>
                {
                    b.Property<ulong>("UserId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint unsigned");

                    MySqlPropertyBuilderExtensions.UseMySqlIdentityColumn(b.Property<ulong>("UserId"));

                    b.Property<DateTime>("Date")
                        .HasColumnType("datetime(6)");

                    b.HasKey("UserId");

                    b.ToTable("People");
                });

            modelBuilder.Entity("Birthmas.Data.ServerConfig", b =>
                {
                    b.Property<ulong>("ServerId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint unsigned");

                    MySqlPropertyBuilderExtensions.UseMySqlIdentityColumn(b.Property<ulong>("ServerId"));

                    b.Property<ulong>("AnnouncementChannelId")
                        .HasColumnType("bigint unsigned");

                    b.Property<bool>("GiveRole")
                        .HasColumnType("tinyint(1)");

                    b.Property<ulong>("RoleId")
                        .HasColumnType("bigint unsigned");

                    b.HasKey("ServerId");

                    b.ToTable("ServerConfigs");
                });

            modelBuilder.Entity("Birthmas.Data.Birthmas", b =>
                {
                    b.HasOne("Birthmas.Data.Person", "Person")
                        .WithMany("Birthmas")
                        .HasForeignKey("PersonUserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Birthmas.Data.ServerConfig", "ServerConfig")
                        .WithMany("Birthmas")
                        .HasForeignKey("ServerConfigServerId");

                    b.Navigation("Person");

                    b.Navigation("ServerConfig");
                });

            modelBuilder.Entity("Birthmas.Data.Person", b =>
                {
                    b.Navigation("Birthmas");
                });

            modelBuilder.Entity("Birthmas.Data.ServerConfig", b =>
                {
                    b.Navigation("Birthmas");
                });
#pragma warning restore 612, 618
        }
    }
}