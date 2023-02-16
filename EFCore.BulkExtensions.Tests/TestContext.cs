using EFCore.BulkExtensions.SqlAdapters;
using EFCore.BulkExtensions.SqlAdapters.MySql;
using EFCore.BulkExtensions.SqlAdapters.SQLite;
using EFCore.BulkExtensions.SQLAdapters;
using EFCore.BulkExtensions.SQLAdapters.SQLServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Configuration;
using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace EFCore.BulkExtensions.Tests
{
    public class TestContext : DbContext
    {
        //public DbSet<Item> Items { get; set; }
        //public DbSet<ItemHistory> ItemHistories { get; set; }

        //public DbSet<UserRole> UserRoles { get; set; }

        public DbSet<Documents> Documents { get; set; }
        //public DbSet<File> Files { get; set; }
        //public DbSet<Person> Persons { get; set; }
        //public DbSet<Student> Students { get; set; }
        //public DbSet<Teacher> Teachers { get; set; }
        //public DbSet<Modul> Moduls { get; set; }
        //public DbSet<Info> Infos { get; set; }
        //public DbSet<ChangeLog> ChangeLogs { get; set; }
        //public DbSet<ItemLink> ItemLinks { get; set; }
        //public DbSet<Address> Addresses { get; set; }

        //public DbSet<Parent> Parents { get; set; }
        //public DbSet<ParentDetail> ParentDetails { get; set; }
        //public DbSet<Child> Children { get; set; }

        //public DbSet<Animal> Animals { get; set; }

        //public DbSet<Setting> Settings { get; set; }

        //public DbSet<LogPersonReport> LogPersonReports { get; set; }

        //public DbSet<AtypicalRowVersionEntity> AtypicalRowVersionEntities { get; set; }

        //public DbSet<AtypicalRowVersionConverterEntity> AtypicalRowVersionConverterEntities { get; set; }

        //public DbSet<Event> Events { get; set; }

        //public DbSet<Archive> Archives { get; set; }

        //public DbSet<Source> Sources { get; set; }

        //public DbSet<Department> Departments { get; set; }
        //public DbSet<Division> Divisions { get; set; }

        public TestContext(DbContextOptions options) : base(options)
        {
            Database.EnsureCreated();

            if (Database.IsSqlServer())
            {
                Database.ExecuteSqlRaw(@"if exists(select 1 from systypes where name='UdttIntInt') drop type UdttIntInt");
                Database.ExecuteSqlRaw(@"create type UdttIntInt AS TABLE(C1 int not null, C2 int not null)");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            //modelBuilder.RemovePluralizingTableNameConvention();

            //modelBuilder.Entity<UserRole>().HasKey(a => new { a.UserId, a.RoleId });

            //modelBuilder.Entity<Info>(e => { e.Property(p => p.ConvertedTime).HasConversion((value) => value.AddDays(1), (value) => value.AddDays(-1)); });
            //modelBuilder.Entity<Info>().Property(e => e.InfoType).HasConversion(new EnumToStringConverter<InfoType>());

            //modelBuilder.Entity<Info>(e => { e.Property("LogData"); });
            //modelBuilder.Entity<Info>(e => { e.Property("TimeCreated"); });
            //modelBuilder.Entity<Info>(e => { e.Property("Remark"); });

            //modelBuilder.Entity<ChangeLog>().OwnsOne(e => e.Audit, b => b.Property(e => e.InfoType).HasConversion(new EnumToStringConverter<InfoType>()));

            //modelBuilder.Entity<Person>().HasIndex(a => a.Name).IsUnique(); // In SQLite UpdateByColumn(nonPK) requires it has UniqueIndex

            modelBuilder.Entity<Documents>().Property(p => p.IsActive).HasDefaultValue(true);
            modelBuilder.Entity<Documents>().Property(p => p.Tag).HasDefaultValue("DefaultData");

            //modelBuilder.Entity<Log>().ToTable("Log");
            //modelBuilder.Entity<LogPersonReport>().ToTable("LogPersonReport");

            if (Database.IsSqlServer())
            {
                modelBuilder.Entity<Documents>().HasKey(p => p.DocumentId);

                modelBuilder.Entity<UdttIntInt>(entity => { entity.HasNoKey(); });

                modelBuilder.Entity<Address>().Property(p => p.LocationGeometry).HasColumnType("geometry");

                modelBuilder.Entity<Division>().Property(p => p.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
                modelBuilder.Entity<Department>().Property(p => p.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
            }
            //else if (Database.IsSqlite())
            //{
            //    modelBuilder.Entity<File>().Property(p => p.VersionChange).ValueGeneratedOnAddOrUpdate().IsConcurrencyToken().HasDefaultValueSql("CURRENT_TIMESTAMP");

            //    modelBuilder.Entity<Address>().Ignore(p => p.LocationGeography);
            //    modelBuilder.Entity<Address>().Ignore(p => p.LocationGeometry);

            //    modelBuilder.Entity<ItemHistory>().ToTable(nameof(ItemHistory));
            //}

            ////modelBuilder.Entity<Modul>(buildAction => { buildAction.HasNoKey(); });
            //modelBuilder.Entity<Modul>().Property(et => et.Code).ValueGeneratedNever();

            //modelBuilder.Entity<Setting>().Property(e => e.Settings).HasConversion<string>();

            //modelBuilder.Entity<AtypicalRowVersionEntity>().HasKey(e => e.Id);
            //modelBuilder.Entity<AtypicalRowVersionEntity>().Property(e => e.RowVersion).HasDefaultValue(0).IsConcurrencyToken().ValueGeneratedOnAddOrUpdate().Metadata.SetBeforeSaveBehavior(PropertySaveBehavior.Save);
            //modelBuilder.Entity<AtypicalRowVersionEntity>().Property(e => e.SyncDevice).IsRequired(true).IsConcurrencyToken().HasDefaultValue("");

            //modelBuilder.Entity<AtypicalRowVersionConverterEntity>().Property(e => e.RowVersionConverted).HasConversion(new NumberToBytesConverter<long>()).HasColumnType("timestamp").IsRowVersion().IsConcurrencyToken();

            //modelBuilder.Entity<Parent>().Property(parent => parent.PhoneNumber)
            //    .HasColumnType("varchar(12)").HasMaxLength(12).HasField("_phoneNumber").IsRequired();

            //modelBuilder.Entity<Person>().HasDiscriminator<string>("Discriminator").HasValue<Student>("Student").HasValue<Teacher>("Teacher"); // name of classes are default values

            // [Timestamp] alternative:
            //modelBuilder.Entity<Document>().Property(x => x.RowVersion).HasColumnType("timestamp").ValueGeneratedOnAddOrUpdate().HasConversion(new NumberToBytesConverter<ulong>()).IsConcurrencyToken();

            //modelBuilder.Entity<Item>().HasQueryFilter(p => p.Description != "1234"); // For testing Global Filter
        }
    }

    public static class ContextUtil
    {
        // TODO: Pass DbService through all the GetOptions methods as a parameter and eliminate this property so the automated tests
        // are thread safe
        static IDbServer? _dbServerMapping;
        static DbServerType _dbServerValue;

        // TODO: Pass DbService through all the GetOptions methods as a parameter and eliminate this property so the automated tests
        // are thread safe
        public static DbServerType DbServer
        {
            get => _dbServerValue;
            set
            {
                _dbServerValue = value;
                _dbServerMapping = value switch
                {
                    DbServerType.SQLServer => new SqlServerDbServer(),
                    DbServerType.SQLite => new SqlLiteDbServer(),
                    // DbServerType.PostgreSQL => new SqlAdapters.PostgreSql.PostgreSqlDbServer(),
                    DbServerType.MySQL => new MySqlDbServer(),
                    //DbServerType.Oracle => new SqlAdapters.Oracle.OracleDbServer(),
                    _ => throw new NotImplementedException(),
                };
            }
        }

        public static DbContextOptions GetOptions(IInterceptor dbInterceptor) => GetOptions(new[] { dbInterceptor });
        public static DbContextOptions GetOptions(IEnumerable<IInterceptor> dbInterceptors = null) => GetOptions<TestContext>(dbInterceptors);

        public static DbContextOptions GetOptions<TDbContext>(IEnumerable<IInterceptor> dbInterceptors = null, string databaseName = nameof(EFCoreBulkTest))
            where TDbContext : DbContext
            => GetOptions<TDbContext>(ContextUtil.DbServer, dbInterceptors, databaseName);

        public static DbContextOptions GetOptions<TDbContext>(DbServerType dbServerType, IEnumerable<IInterceptor> dbInterceptors = null, string databaseName = nameof(EFCoreBulkTest))
            where TDbContext : DbContext
        {
            var optionsBuilder = new DbContextOptionsBuilder<TDbContext>();

            if (dbServerType == DbServerType.SQLServer)
            {
                var connectionString = GetSqlServerConnectionString(databaseName);

                // ALTERNATIVELY (Using MSSQLLocalDB):
                //var connectionString = $@"Data Source=(localdb)\MSSQLLocalDB;Database={databaseName};Trusted_Connection=True;MultipleActiveResultSets=True";

                //optionsBuilder.UseSqlServer(connectionString); // Can NOT Test with UseInMemoryDb (Exception: Relational-specific methods can only be used when the context is using a relational)
                optionsBuilder.UseSqlServer(connectionString, opt => opt.UseNetTopologySuite()); // NetTopologySuite for Geometry / Geometry types
            }
            else if (dbServerType == DbServerType.SQLite)
            {
                string connectionString = GetSqliteConnectionString(databaseName);
                optionsBuilder.UseSqlite(connectionString);
                SQLitePCL.Batteries.Init();

                // ALTERNATIVELY:
                //string connectionString = (new SqliteConnectionStringBuilder { DataSource = $"{databaseName}Lite.db" }).ToString();
                //optionsBuilder.UseSqlite(new SqliteConnection(connectionString));
            }
            else if (dbServerType == DbServerType.MySQL)
            {
                string connectionString = GetMySqlConnectionString(databaseName);
                optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
            }
            else
            {
                throw new NotSupportedException($"Database {dbServerType} is not supported. Only SQL Server and SQLite are Currently supported.");
            }

            if (dbInterceptors?.Any() == true)
            {
                optionsBuilder.AddInterceptors(dbInterceptors);
            }

            return optionsBuilder.Options;
        }

        private static IConfiguration GetConfiguration()
        {
            var configBuilder = new ConfigurationBuilder()
                .AddJsonFile("testsettings.json", optional: false)
                .AddJsonFile("testsettings.local.json", optional: true);

            return configBuilder.Build();
        }

        public static string GetSqlServerConnectionString(string databaseName)
        {
            return GetConfiguration().GetConnectionString("SqlServer");
        }

        public static string GetSqliteConnectionString(string databaseName)
        {
            return GetConfiguration().GetConnectionString("Sqlite").Replace("{databaseName}", databaseName);
        }

        public static string GetMySqlConnectionString(string databaseName)
        {
            return GetConfiguration().GetConnectionString("MySql");
        }
    }

    public static class ModelBuilderExtensions
    {
        public static void RemovePluralizingTableNameConvention(this ModelBuilder modelBuilder)
        {
            foreach (IMutableEntityType entity in modelBuilder.Model.GetEntityTypes())
            {
                if (!entity.IsOwned() && entity.BaseType == null) // without this exclusion OwnedType would not be by default in Owner Table
                {
                    entity.SetTableName(entity.ClrType.Name);
                }
            }
        }
    }

    public class Item
    {
        public int ItemId { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public int Quantity { get; set; }

        public decimal? Price { get; set; }

        //[Column(TypeName = (nameof(DateTime)))] // Column to be of DbType 'datetime' instead of default 'datetime2'
        public DateTime TimeUpdated { get; set; }

        public ICollection<ItemHistory> ItemHistories { get; set; }
    }

    // ItemHistory is used to test bulk Ops to multiple tables(Item and ItemHistory), to test Guid as PK and to test other Schema(his)
    [Table(nameof(ItemHistory), Schema = "his")] // different schema is not supported in Sqlite
    public class ItemHistory
    {
        public Guid ItemHistoryId { get; set; }

        public int ItemId { get; set; }
        public virtual Item Item { get; set; }

        public string Remark { get; set; }
    }

    // UserRole is used to test tables with Composite PrimaryKey
    public class UserRole
    {
        [Key]
        public int UserId { get; set; }

        [Key]
        public int RoleId { get; set; }

        public string Description { get; set; }
    }

    // User Defined Table Type
    public class UdttIntInt
    {
        public int C1 { get; set; }
        public int C2 { get; set; }
    }

    // Person, Instructor nad Student are used to test Bulk with Shadow Property and Discriminator column
    public abstract class Person
    {
        public int PersonId { get; set; }

        public string Name { get; set; }
    }

    public class Student : Person
    {
        public string Subject { get; set; }
    }

    public class Address
    {
        public int AddressId { get; set; }
        public string Street { get; set; }

        public Geometry LocationGeography { get; set; }
        public Geometry LocationGeometry { get; set; }
    }

    public class Teacher : Person
    {
        public string Class { get; set; }
    }

    // For testing Computed columns Default values
    public class Documents
    {
        //[DefaultValueSql("NEWID()")] // no native [DefaultValueSql] annotation so this is configured via FluentAPI in modelBuilder
        public Guid DocumentId { get; set; }

        //[DefaultValue(true)] // EF doesn't use DefaultValue attribute, not annotation, so this is configured via FluentAPI as well
        public bool? IsActive { get; set; }

        [Required]
        public string Content { get; set; }

        public string Tag { get; set; }

        [DatabaseGenerated(DatabaseGeneratedOption.Computed)] // Computed columns also have to be configured with FluentAPI
        public int ContentLength { get; set; }
    }

    // For testing TimeStamp Property and Column with Concurrency Lock
    public class File
    {
        [Column("Id")] // test different column Name, PK in this case
        public int FileId { get; set; }

        [Required]
        public string Description { get; set; }

        public byte[] DataBytes { get; set; }

        [Timestamp]
        public byte[] VersionChange { get; set; }
        //public ulong RowVersion { get; set; }
    }

    public enum InfoType
    {
        InfoTypeA,
        InfoTypeB
    }

    public class Modul
    {
        [Key]
        [Required]
        public string Code { get; set; }
        public string Name { get; set; }
    }

    // For testring ValueConversion
    public class Info
    {
        public Info()
        {
            logData = "logged";
            TimeCreated = DateTime.Now;
        }
        [Required]
        private string logData; // To test private Field with protected explicit getter/setter

        public long InfoId { get; set; }

        public string Message { get; set; }

        public DateTime ConvertedTime { get; set; }

        public InfoType InfoType { get; set; }

        public string Note { get; protected set; } = "NN"; // To test protected Setter

        public string Remark { get; } // To test without Setter

        private DateTime TimeCreated { get; set; } // To test private Property

        [Required]
        private string LogData { get { return logData; } set { logData = value; } }

        public string GetLogData() { return logData; }
        public DateTime GetDateCreated() { return TimeCreated.Date; }
    }


    public class Animal
    {
        public int AnimalId { get; set; }

        [Required]
        public string Name { get; set; }
    }

    public class Mammal : Animal
    {
        public string Category { get; set; }
    }

    public enum SettingsEnum
    {
        Sett1,
        Sett2
    }
    // For testing Convertible Property (Key Settings in modelBuilder configured as 'nvarchar' insted of 'int')
    public class Setting
    {
        [Key]
        public SettingsEnum Settings { get; set; }
        [Required]
        [MaxLength(20)]
        public string Value { get; set; }
    }


    // For testing ForeignKey Shadow Properties
    public class ItemLink
    {
        public int ItemLinkId { get; set; }
        public virtual Item Item { get; set; }
    }

    // For testing OwnedTypes
    public class ChangeLog
    {
        public int ChangeLogId { get; set; }

        public string Description { get; set; }

        public Audit Audit { get; set; }

        //public AuditExtended AuditExtended { get; set; }

        //public AuditExtended AuditExtendedSecond { get; set; }
    }

    [Owned]
    public class Audit
    {
        [Column(nameof(ChangedBy))] // for setting custom column name, in this case prefix OwnedType_ ('Audit_') removed, so column would be only ('ChangedBy')
        public string ChangedBy { get; set; } // default Column name for Property of OwnedType is OwnedType_Property ('Audit_ChangedBy')

        public bool IsDeleted { get; set; }

        [NotMapped] // alternatively in OnModelCreating(): modelBuilder.Entity<Audit>().Ignore(a => a.ChangedTime);
        public DateTime? ChangedTime { get; set; }

        public InfoType InfoType { get; set; }
    }

    [Owned]
    public class AuditExtended
    {
        public string CreatedBy { get; set; }

        [NotMapped]
        public DateTime? CreatedTime { get; set; }

        [NotMapped]
        public string Remark { get; set; }
    }

    // For testing BatchUpdate expressions that use nested queries
    public class Parent
    {
        public ICollection<Child> Children { get; set; }
        public decimal CombinedChildBalance { get; set; }
        public string Description { get; set; }
        public virtual ParentDetail Details { get; set; }
        public int ParentId { get; set; }

        private string _phoneNumber;
        public string PhoneNumber { get => _phoneNumber; set => _phoneNumber = value; }

        public decimal Value { get; set; }
    }

    public class ParentDetail
    {
        public int ParentDetailId { get; set; }

        public virtual Parent Parent { get; set; }
        public int ParentId { get; set; }

        public string Notes { get; set; }
    }

    public class Child
    {
        public int ChildId { get; set; }
        public bool IsEnabled { get; set; }
        public decimal Value { get; set; }

        public virtual Parent Parent { get; set; }
        public int ParentId { get; set; }
    }

    public abstract class Log // To Test TPT (TablePerType) - https://docs.microsoft.com/en-us/ef/core/modeling/inheritance#table-per-type-configuration
    {
        public int LogId { get; set; }
        public int PersonId { get; set; }
        public int RegBy { get; set; }
        public DateTime CreatedDate { get; set; }
    }
    public class LogPersonReport : Log
    {
        public int ReportId { get; set; }
        public int LogPersonReportTypeId { get; set; }
    }

    public class AtypicalRowVersionEntity
    {
        public Guid Id { get; set; }
        public long RowVersion { get; set; }
        public string SyncDevice { get; set; }
        public string Name { get; set; }
    }

    public class AtypicalRowVersionConverterEntity
    {
        public Guid Id { get; set; }
        public long RowVersionConverted { get; set; }
        public string Name { get; set; }
    }

    public class Event // CustomPrecision DateTime Test (SqlServer only)
    {
        public int EventId { get; set; }

        [Required]
        public string Name { get; set; }

        public string Description { get; set; }

        [Column(TypeName = "datetime2(3)")]
        public DateTime TimeCreated { get; set; }
    }

    public class Archive
    {
        public byte[] ArchiveId { get; set; }
        public string Description { get; set; }
    }

    public class Source
    {
        public int SourceId { get; set; }
        public Status StatusId { get; set; }
        public Type TypeId { get; set; }
    }
    public enum Status : byte { Init, Changed }
    public enum Type : byte { Undefined, Type1, Type2 }

    public class Department
    {
        public Guid Id { get; set; }
        public string Name { get; set; }

        public ICollection<Division> Divisions { get; set; }
    }

    public class Division
    {
        public Guid Id { get; set; }
        public string Name { get; set; }

        public Guid DepartmentId { get; set; }
        public Department Department { get; set; }
    }
}
