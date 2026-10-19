namespace NHi.Inventory.Data;

public static class DbContextFactory
{
    public static AppDbContext Create() => new AppDbContext();
}
