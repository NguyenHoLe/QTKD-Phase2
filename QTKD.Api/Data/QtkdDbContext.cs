using Microsoft.EntityFrameworkCore;
using QTKD.Api.Models;

namespace QTKD.Api.Data;

public class QtkdDbContext(DbContextOptions<QtkdDbContext> options)
    : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Contract> Contracts => Set<Contract>();

    public DbSet<PriceList> PriceLists => Set<PriceList>();

    public DbSet<PriceItem> PriceItems => Set<PriceItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>().HasData(
            new Customer
            {
                Id = 1,
                Name = "Công ty Minh Phát",
                Status = "Active"
            },
            new Customer
            {
                Id = 2,
                Name = "Công ty An Bình",
                Status = "Active"
            });

        modelBuilder.Entity<Contract>()
            .HasOne(contract => contract.Customer)
            .WithMany(customer => customer.Contracts)
            .HasForeignKey(contract => contract.CustomerId)
            .IsRequired();

        modelBuilder.Entity<Contract>().HasData(
            new Contract
            {
                Id = 1,
                ContractNo = "HD-2026-018",
                CustomerId = 1,
                StartDate = new DateOnly(2026, 10, 1),
                EndDate = new DateOnly(2027, 9, 30),
                Status = "Draft"
            });

        modelBuilder.Entity<PriceList>()
            .HasMany(priceList => priceList.Items)
            .WithOne(priceItem => priceItem.PriceList)
            .HasForeignKey(priceItem => priceItem.PriceListId)
            .IsRequired();

        modelBuilder.Entity<PriceList>().HasData(
            new PriceList
            {
                Id = 1,
                Name = "Bảng giá demo 2026",
                EffectiveDate = new DateOnly(2026, 1, 1)
            });

        modelBuilder.Entity<PriceItem>()
            .HasKey(priceItem => new { priceItem.PriceListId, priceItem.ServiceCode });

        modelBuilder.Entity<PriceItem>().HasData(
            new PriceItem
            {
                PriceListId = 1,
                ServiceCode = "SVC-PORT-001",
                UnitPrice = 100000m
            },
            new PriceItem
            {
                PriceListId = 1,
                ServiceCode = "SVC-PORT-002",
                UnitPrice = 250000m
            });
    }
}
