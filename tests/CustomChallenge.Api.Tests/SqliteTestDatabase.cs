using CustomChallenge.Api.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CustomChallenge.Api.Tests;

// Isolated, real relational SQLite database per test (in-memory, but with
// actual SQL execution/constraints — unlike the EF InMemory provider). The
// connection must stay open for the DB's lifetime; each test gets its own
// instance so tests never share state.
public sealed class SqliteTestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;
    public AppDbContext Context { get; }

    public SqliteTestDatabase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        Context = new AppDbContext(options);
        Context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}
