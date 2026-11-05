using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TraceMap.Core;

// Public synthetic virtual byte stream: no disk allocation, private inputs or SQL execution.
var bytes = args.Length == 0 ? SqlServerLocalDefinitions.MaxStreamFileBytes : long.Parse(args[0]);
const int chunkBytes = SqlServerLocalDefinitions.MaxStreamBatchBytes;
if (bytes < chunkBytes || bytes > SqlServerLocalDefinitions.MaxStreamFileBytes || bytes % chunkBytes != 0)
    throw new ArgumentException("Choose a positive multiple of 4 MiB, at most 8 GiB.");
var blocks = checked((int)(bytes / chunkBytes));
var padding = Encoding.UTF8.GetBytes("--" + new string('x', chunkBytes - 6) + "\nGO\n");
const string ending = "\nCREATE PROC dbo.ReadItems AS SELECT 1 AS Id;\nGO\n";
var last = Encoding.UTF8.GetBytes("--" + new string('x', chunkBytes - ending.Length - 2) + ending);
using var process = Process.GetCurrentProcess();
long sampledPeak = 0;
await using var watchdog = new Timer(_ =>
{
    process.Refresh();
    var resident = process.WorkingSet64;
    long previous;
    do { previous = Interlocked.Read(ref sampledPeak); }
    while (resident > previous && Interlocked.CompareExchange(ref sampledPeak, resident, previous) != previous);
    if (resident > 1024L * 1024 * 1024)
    {
        Console.Error.WriteLine("FAIL: stress process exceeded 1 GiB resident memory");
        Environment.Exit(3);
    }
}, null, 250, 250);
var elapsed = Stopwatch.StartNew();
using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
for (var i = 0; i < blocks; i++) digest.AppendData(i == blocks - 1 ? last : padding);
var sha = Convert.ToHexStringLower(digest.GetHashAndReset());
var input = new SqlLocalInput(SqlServerLocalDefinitions.StreamInputVersion, "local-only", "public-stress", new('a', 40),
    "public-eight-gib-stream", "ExampleServer", "ExampleDatabase", [new("virtual.sql", sha)]);
var json = JsonSerializer.Serialize(input, new JsonSerializerOptions(JsonSerializerDefaults.Web));
SyntheticStream? observed = null;
var export = SqlServerLocalDefinitions.ProduceStreamed(json, _ => observed = new SyntheticStream(padding, last, blocks));
if (observed?.BytesRead != bytes || export.Objects.Count != 1 || export.Objects[0].Name != "ReadItems")
    throw new InvalidOperationException("Full byte stream or final definition was not consumed");
if (export.Objects[0].Span.StartLine != (blocks - 1) * 2 + 2)
    throw new InvalidOperationException("Original line mapping incorrect");
process.Refresh();
Console.WriteLine($"PASS bytesConsumed={observed.BytesRead}; expectedBytes={bytes}; batches={blocks}; objects={export.Objects.Count}");
Console.WriteLine($"elapsedMs={elapsed.ElapsedMilliseconds}; sampledPeakResidentBytes={Interlocked.Read(ref sampledPeak)}; osPeakWorkingSetBytes={process.PeakWorkingSet64}; residentLimitBytes=1073741824");
Console.WriteLine("Virtual content was generated/read/hashed in full. No 8 GiB byte array or disk file was allocated. SQL was not executed.");

sealed class SyntheticStream(byte[] repeated, byte[] last, int blocks) : Stream
{
    public long BytesRead { get; private set; }
    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => (long)repeated.Length * blocks;
    public override long Position { get => BytesRead; set => throw new NotSupportedException(); }
    public override int Read(byte[] buffer, int offset, int count)
    {
        if (BytesRead == Length) return 0;
        var block = (int)(BytesRead / repeated.Length);
        var within = (int)(BytesRead % repeated.Length);
        var source = block == blocks - 1 ? last : repeated;
        var take = Math.Min(count, source.Length - within);
        source.AsSpan(within, take).CopyTo(buffer.AsSpan(offset, take));
        BytesRead += take;
        return take;
    }
    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
