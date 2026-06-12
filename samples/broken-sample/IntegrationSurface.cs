using System.Net.Http;

namespace BrokenSample;

public sealed class CustomerDbContext : DbContext
{
    public DbSet<CustomerProfile> Customers { get; set; }

    public async Task SyncAsync(HttpClient http, IHttpClientFactory factory, dynamic connection)
    {
        await http.GetAsync("/customers");
        await http.PostAsJsonAsync("/customers", new CustomerProfile());
        var billing = factory.CreateClient("billing");
        connection.Query<CustomerProfile>("select Id, PrimaryEmail from Customers");
        await connection.ExecuteAsync("update Customers set PrimaryEmail = @PrimaryEmail where Id = @Id");
        SaveChanges();
        using var command = new SqlCommand("select Id, PrimaryEmail from Customers");
    }
}
