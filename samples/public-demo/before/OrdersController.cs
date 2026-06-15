using System;
using System.Linq;

namespace PublicDemo.Before;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RouteAttribute : Attribute
{
    public RouteAttribute(string template)
    {
        Template = template;
    }

    public string Template { get; }
}

[AttributeUsage(AttributeTargets.Class)]
public sealed class ApiControllerAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class HttpGetAttribute : Attribute
{
    public HttpGetAttribute(string template)
    {
        Template = template;
    }

    public string Template { get; }
}

[ApiController]
[Route("api/public/[controller]")]
public sealed class OrdersController
{
    private readonly OrderRepository repository = new();

    [HttpGet("{orderId}")]
    public object GetById(string orderId)
    {
        var status = repository.LoadStatus(orderId);
        return new { orderId, status };
    }
}

public sealed class OrderRepository
{
    public string LoadStatus(string orderId)
    {
        const string sql = "select order_id, status from demo_orders where order_id = @orderId";
        var rows = new[]
        {
            new OrderRecord(orderId, "pending")
        };

        return rows
            .Where(row => row.OrderId == orderId)
            .Select(row => row.Status)
            .FirstOrDefault() ?? sql.Length.ToString();
    }
}

public sealed record OrderRecord(string OrderId, string Status);
