using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace TraceMap.Core;

public static partial class SqlServerLocalDefinitions
{
    public const string StreamInputVersion = "sqlserver-streamed-local-definitions-input/1";
    public const string StreamExportVersion = "sqlserver-streamed-local-definitions/1";
    public const string StreamExtractorVersion = "sqlserver-streamed-local-definitions/1";
    public const long MaxStreamFileBytes = 8L * 1024 * 1024 * 1024;
    public const long MaxStreamTotalBytes = MaxStreamFileBytes;
    public const int MaxStreamBatchBytes = 4 * 1024 * 1024;
    public const int MaxStreamBatches = 65_536;
    public const int MaxStreamObjects = 8192;
    public const int MaxStreamReferences = 65_536;
    public const int MaxStreamGaps = 65_536;
    public const int MaxStreamExportBytes = 32 * 1024 * 1024;
    private static readonly Regex StreamGoLine = new(@"\A[\t ]*GO[\t ]*(?:--[^\r\n]*)?\r?\n?\z",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    public static SqlLocalInput ReadStreamInput(string json)
    {
        var input = SqlServerSchemaEvidence.Read<SqlLocalInput>(json);
        Require(input.Version == StreamInputVersion, "stream-input-contract-invalid");
        // Preserve every existing context/path/digest/provenance admission condition.
        _ = ReadInput(SqlServerSchemaEvidence.Json(input with { Version = InputVersion }));
        return input;
    }

    /// <summary>Reads exact UTF-8 originals sequentially; only lexically verified GO batches reach the parser.</summary>
    public static SqlLocalExport ProduceStreamed(string json, Func<string, Stream> openSource, CancellationToken token = default)
    {
        var input = ReadStreamInput(json);
        long totalBytes = 0;
        var batches = 0;
        return ProduceParts(input, Parts(), MaxStreamBatchBytes, true, token);

        IEnumerable<SqlLocalPart> Parts()
        {
            foreach (var file in input.Files.OrderBy(file => file.Path, StringComparer.Ordinal))
            {
                token.ThrowIfCancellationRequested();
                using var raw = openSource(file.Path);
                Require(raw.CanRead && (!raw.CanSeek || raw.Length <= MaxStreamFileBytes), "stream-file-size-limit");
                using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                using var bounded = new SqlHashReadStream(raw, digest, count =>
                {
                    totalBytes += count;
                    Require(totalBytes <= MaxStreamTotalBytes, "stream-total-size-limit");
                    token.ThrowIfCancellationRequested();
                });
                using var reader = new StreamReader(bounded, new UTF8Encoding(false, true), false, 16_384, leaveOpen: true);
                var buffer = new StringBuilder();
                var startLine = 1;
                var lineNumber = 0;
                while (ReadBoundedLine(reader, token) is { } line)
                {
                    Require(lineNumber < 2_000_000_000, "stream-line-limit");
                    lineNumber++;
                    Require(buffer.Length + line.Length <= MaxStreamBatchBytes, "stream-batch-character-limit");
                    buffer.Append(line);
                    if (!StreamGoLine.IsMatch(line)) continue;
                    var text = buffer.ToString();
                    Require(new UTF8Encoding(false, true).GetByteCount(text) <= MaxStreamBatchBytes, "stream-batch-byte-limit");
                    // A textual GO inside a multiline string/comment is not a boundary.
                    var parser = new TSql160Parser(initialQuotedIdentifiers: true);
                    var lex = startLine == 1 && text.StartsWith('\uFEFF') ? text[1..] : text;
                    var (tokens, errors) = SqlServerDdlEvidence.OnParserStack(() =>
                    {
                        using var inputReader = new StringReader(lex);
                        var result = parser.GetTokenStream(inputReader, out var failures);
                        return (result, failures);
                    });
                    if (errors.Count != 0 || !tokens.Any(item => item.TokenType == TSqlTokenType.Go
                        && item.Line == lineNumber - startLine + 1)) continue;
                    Require(tokens.Count <= SqlServerDdlEvidence.MaxTokens, "stream-token-limit");
                    Require(++batches <= MaxStreamBatches, "stream-batch-count-limit");
                    yield return new(file.Path, text, startLine);
                    buffer.Clear();
                    startLine = checked(lineNumber + 1);
                }
                if (buffer.Length != 0)
                {
                    Require(++batches <= MaxStreamBatches, "stream-batch-count-limit");
                    yield return new(file.Path, buffer.ToString(), startLine);
                }
                Require(Convert.ToHexStringLower(digest.GetHashAndReset()) == file.Sha256, "source-digest-mismatch");
            }
        }
    }

    private static string? ReadBoundedLine(TextReader reader, CancellationToken token)
    {
        var line = new StringBuilder();
        while (true)
        {
            var next = reader.Read();
            if (next < 0) return line.Length == 0 ? null : line.ToString();
            Require(line.Length < MaxStreamBatchBytes, "stream-line-character-limit");
            line.Append((char)next);
            if ((line.Length & 8191) == 0) token.ThrowIfCancellationRequested();
            if (next == '\n') return line.ToString();
        }
    }

    private sealed class SqlHashReadStream(Stream inner, IncrementalHash hash, Action<int> consume) : Stream
    {
        private long bytes;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => bytes; set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = inner.Read(buffer, offset, count);
            bytes += read;
            Require(bytes <= MaxStreamFileBytes, "stream-file-size-limit");
            hash.AppendData(buffer, offset, read);
            consume(read);
            return read;
        }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
