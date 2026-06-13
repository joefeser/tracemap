namespace EndpointServerSample.Controllers;

[ApiController]
[Route("api/admin/[controller]")]
public sealed class RunnerController
{
    private readonly RunnerRepository repository = new();

    [HttpGet("get-by-id/{runnerId:guid}")]
    public object GetById(string runnerId)
    {
        var status = repository.Query(runnerId);
        return new { runnerId, status };
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
