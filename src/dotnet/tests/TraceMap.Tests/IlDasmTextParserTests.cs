namespace TraceMap.Tests;

/// <summary>
/// Platform-neutral tests for the ILDasm text parser used by the public
/// ILAsm/ILDAsm parity gate. The embedded snippets mirror the exact shapes
/// Microsoft ILDasm prints for the public fixtures (method headers, try
/// regions, switch tables, multi-line locals, and .line directives). They are
/// parser test data only, never scanner inputs or evidence.
/// </summary>
public sealed class IlDasmTextParserTests
{
    private const string Sample = """
        .assembly extern System.Runtime
        {
          .ver 10:0:0:0
        }
        .assembly Sample
        {
          .ver 1:0:0:0
        }
        .module Sample.dll
        // MVID: {9a1f1b2c-1111-2222-3333-444455556666}
        // Images and binaries size may not be correct
        .class public auto ansi beforefieldinit TraceMap.CompiledFixtures.CSharp.Il.SampleShapes
               extends [System.Runtime]System.Object
        {
          .method public hidebysig static int32  Loop(int32 count) cil managed
          {
            // Code size       33 (0x21)
            .maxstack  2
            .locals init (int32 V_0)
            IL_0000:  nop
            IL_0001:  ldc.i4.0
            IL_0002:  stloc.0
            IL_0003:  br.s       IL_0007
            IL_0005:  ldloc.0
            IL_0006:  ldc.i4.1
            IL_0007:  add
            IL_0008:  stloc.0
            IL_0009:  ldloc.1
            IL_000a:  blt.s      IL_0005
            IL_000c:  ldstr      "done"
            IL_0011:  call       void [System.Runtime]System.Console::WriteLine(string)
            IL_0016:  ldloc.0
            IL_0017:  ret
          } // end of method SampleShapes::Loop

          .method public hidebysig static int32  Guarded(int32 value) cil managed
          {
            // Code size       30 (0x1e)
            .maxstack  2
            .locals init (int32 V_0,
                          int32 V_1)
            IL_0000:  nop
            .try
            {
              IL_0001:  ldarg.0
              IL_0002:  ldc.i4.s   41
              IL_0004:  add
              IL_0005:  stloc.0
              IL_0006:  leave.s    IL_0016
            } // end .try
            catch [System.Runtime]System.Exception
            {
              IL_0008:  pop
              IL_0009:  callvirt   instance string [System.Runtime]System.Exception::get_Message()
              IL_000e:  call       void [System.Runtime]System.Console::WriteLine(string)
              IL_0013:  leave.s    IL_0016
            } // end handler
            IL_0016:  ldloc.0
            IL_0017:  ret
          } // end of method SampleShapes::Guarded

          .method public hidebysig static int32  Choose(int32 value) cil managed
          {
            // Code size       40 (0x28)
            .maxstack  3
            .locals (class [System.Runtime]System.String V_0)
            IL_0000:  ldarg.0
            IL_0001:  switch     (
                                    IL_0010,
                                    IL_0015,
                                    IL_001a)
            IL_0010:  ldc.i4.1
            IL_0011:  ret
            IL_0015:  ldc.i4.2
            IL_0016:  ret
            IL_001a:  ldc.i4.3
            IL_001b:  ret
          } // end of method SampleShapes::Choose

          .method public hidebysig static void  Generic() cil managed
          {
            // Code size       2 (0x2)
            .maxstack  8
            IL_0000:  nop
            IL_0001:  ret
          } // end of method SampleShapes::Generic
        } // end of class TraceMap.CompiledFixtures.CSharp.Il.SampleShapes
        """;

    [Fact]
    public void Parses_method_headers_maxstack_locals_and_instructions()
    {
        var parsed = IlDasmTextParser.ParseText(Sample);

        Assert.Equal(4, parsed.Methods.Count);
        var loop = parsed.Method("TraceMap.CompiledFixtures.CSharp.Il.SampleShapes", "Loop");
        Assert.Equal(2, loop.MaxStack);
        Assert.Equal(1, loop.LocalCount);
        Assert.Equal(14, loop.Instructions.Count);
        Assert.Equal(0, loop.ExceptionRegionCount);
        Assert.Equal("ldstr", loop.Instructions[10].Opcode);
        Assert.Equal("\"done\"", loop.Instructions[10].Operand);
        Assert.Equal(0x11, loop.Instructions[11].Offset);
        Assert.Equal([(0x11, "call")], loop.CallSites);
    }

    [Fact]
    public void Counts_exception_regions_and_multi_line_locals()
    {
        var parsed = IlDasmTextParser.ParseText(Sample);

        var guarded = parsed.Method("TraceMap.CompiledFixtures.CSharp.Il.SampleShapes", "Guarded");
        Assert.Equal(2, guarded.LocalCount);
        Assert.Equal(1, guarded.ExceptionRegionCount);
        Assert.Equal(1, guarded.TryBlockCount);
        Assert.Equal([(0x09, "callvirt"), (0x0e, "call")], guarded.CallSites);
        var choose = parsed.Method("TraceMap.CompiledFixtures.CSharp.Il.SampleShapes", "Choose");
        Assert.Equal(1, choose.LocalCount);
        Assert.Equal(3, choose.MaxStack);
    }

    [Fact]
    public void Normalized_text_drops_comment_lines_and_blank_lines()
    {
        var first = IlDasmTextParser.ParseText(Sample);
        var noisy = Sample.Replace("// Code size       33 (0x21)", "// Code size       99 (0x63)")
            .Replace("// MVID: {9a1f1b2c-1111-2222-3333-444455556666}", "// MVID: {00000000-0000-0000-0000-000000000000}")
            + "\n\n";
        var second = IlDasmTextParser.ParseText(noisy);

        Assert.Equal(first.NormalizedText, second.NormalizedText);
        Assert.DoesNotContain("// MVID", first.NormalizedText, StringComparison.Ordinal);
        Assert.DoesNotContain("// Code size", first.NormalizedText, StringComparison.Ordinal);
    }

    [Fact]
    public void Line_directives_bind_to_the_following_instruction_offset()
    {
        var text = """
            .class public X
            {
              .method public static void  M() cil managed
              {
                .maxstack  8
                .line 7,7 : 13,20 'c:\repo\Fixture.cs'
                IL_0000:  nop
                .line 8,9 : 4,17 'c:\repo\Fixture.cs'
                IL_0001:  ret
              } // end of method X::M
            } // end of class X
            """;
        var parsed = IlDasmTextParser.ParseText(text);

        var method = parsed.Method("X", "M");
        Assert.Equal(2, method.LineDirectives.Count);
        Assert.Equal(0, method.LineDirectives[0].Offset);
        Assert.Equal(7, method.LineDirectives[0].StartLine);
        Assert.Equal(7, method.LineDirectives[0].EndLine);
        Assert.Equal(13, method.LineDirectives[0].StartColumn);
        Assert.Equal(20, method.LineDirectives[0].EndColumn);
        Assert.Equal(@"c:\repo\Fixture.cs", method.LineDirectives[0].Document);
        Assert.Equal(1, method.LineDirectives[1].Offset);
        Assert.Equal(9, method.LineDirectives[1].EndLine);
    }

    [Fact]
    public void Malformed_method_headers_and_missing_locals_fail_loudly()
    {
        Assert.Throws<InvalidOperationException>(() => IlDasmTextParser.ParseText(".method public static\n"));
        Assert.Throws<InvalidOperationException>(() => IlDasmTextParser.ParseText(".method public static void  NoBody cil managed\n"));
    }
}
