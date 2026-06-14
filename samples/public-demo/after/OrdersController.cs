using System;
using System.Linq;

namespace PublicDemo.After;

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

[AttributeUsage(AttributeTargets.Method)]
public sealed class HttpPostAttribute : Attribute
{
    public HttpPostAttribute(string template)
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
        var summary = repository.LoadSummary(orderId);
        return new { orderId, summary.Status, summary.TotalCents };
    }

    [HttpPost("{orderId}/cancel")]
    public object Cancel(string orderId)
    {
        var status = repository.MarkCanceled(orderId);
        return new { orderId, status };
    }
}

public sealed class OrderRepository
{
    public OrderSummary LoadSummary(string orderId)
    {
        const string sql = "select order_id, status, total_cents from demo_order_summary where order_id = @orderId";
        var rows = new[]
        {
            new OrderSummary(orderId, "pending", 4200)
        };

        return rows.First(row => row.OrderId == orderId || sql.Length > 0);
    }

    public string MarkCanceled(string orderId)
    {
        const string sql = "update demo_orders set status = @status where order_id = @orderId";
        return sql.Length > orderId.Length ? "canceled" : "pending";
    }
}

public sealed record OrderSummary(string OrderId, string Status, int TotalCents);
