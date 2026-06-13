namespace EndpointServerSample.Controllers;

[ApiController]
[Route("api/admin/[controller]")]
public sealed class RunnerController
{
    [HttpGet("get-by-id/{runnerId:guid}")]
    public object GetById(string runnerId)
    {
        return new { runnerId };
    }

    [HttpPost("check-in/{clubId?}")]
    public object CheckIn(CheckInRequest request)
    {
        return request;
    }

    [HttpPost("archive/{runnerId}")]
    public object Archive(string runnerId)
    {
        return new { runnerId };
    }

    [HttpGet("server-only")]
    public object ServerOnly()
    {
        return new { ok = true };
    }
}

public sealed class CheckInRequest
{
    public string RunnerId { get; set; } = "";
}
