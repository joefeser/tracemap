namespace TraceMap.Tests;

/// <summary>
/// Platform-neutral tests for the ILDasm text parser used by the public
/// ILAsm/ILDAsm parity gate. The embedded snippets mirror the exact shapes
/// Microsoft ILDasm prints for the public fixtures (method headers, wrapped
/// headers, try regions, switch jump tables, multi-line locals, and .line
/// directives). They are parser test data only, never scanner inputs or
/// evidence.
/// </summary>
public sealed class IlDasmTextParserTests
{
    [Theory]
    [InlineData(".locals init (int32 V_0)", ".locals init (int64 V_0)")]
    [InlineData(".locals init (int32 V_0)", ".locals (int32 V_0)")]
    [InlineData("Loop(int32 count)", "Loop(int64 count)")]
    [InlineData("static int32  Loop", "static int64  Loop")]
    [InlineData("static int32  Loop", "static vararg int32  Loop")]
    public void Canonical_comparison_preserves_signatures_and_local_initialization(string original, string replacement)
    {
        Assert.Contains(original, Sample, StringComparison.Ordinal);
        Assert.NotEqual(IlDasmTextParser.ParseText(Sample).CanonicalMethodsText(),
            IlDasmTextParser.ParseText(Sample.Replace(original, replacement, StringComparison.Ordinal)).CanonicalMethodsText());
    }

    [Fact]
    public void Canonical_comparison_preserves_wrapped_symbolic_operands()
    {
        var wrapped = Sample.Replace("void [System.Runtime]System.Console::WriteLine(string)",
            "void [System.Runtime]System.Console::WriteLine(\n                     string)", StringComparison.Ordinal);
        var changed = wrapped.Replace("                     string)", "                     int32)", StringComparison.Ordinal);
        Assert.NotEqual(IlDasmTextParser.ParseText(wrapped).CanonicalMethodsText(),
            IlDasmTextParser.ParseText(changed).CanonicalMethodsText());
    }

    [Fact]
    public void Handler_at_end_of_method_keeps_the_already_observed_try_end()
    {
        const string text = """
            .class public X
            {
              .method public static void M() cil managed
              {
                // Code size 5 (0x5)
                .maxstack 1
                .try
                {
                  IL_0000: ldnull
                  IL_0001: throw
                }
                catch [System.Runtime]System.Exception
                {
                  IL_0002: pop
                  IL_0003: rethrow
                }
              }
            }
            """;
        var region = Assert.Single(IlDasmTextParser.ParseText(text).Method("X", "M").ExceptionRegions);
        Assert.Equal(2, region.TryEnd);
        Assert.Equal(5, region.HandlerEnd);
    }

    [Fact]
    public void Consecutive_catches_share_the_same_protected_range()
    {
        var text = Sample.Replace("} // end handler", "} // end handler\ncatch [System.Runtime]System.ArgumentException\n{\nIL_0015: throw\n}", StringComparison.Ordinal);
        var regions = IlDasmTextParser.ParseText(text).Method("TraceMap.CompiledFixtures.CSharp.Il.SampleShapes", "Guarded").ExceptionRegions;
        Assert.Equal(2, regions.Count);
        Assert.All(regions, region => { Assert.Equal(1, region.TryStart); Assert.Equal(8, region.TryEnd); });
        Assert.Equal(0x15, regions[0].HandlerEnd);
        Assert.Equal(0x15, regions[1].HandlerStart);
        Assert.Equal(0x16, regions[1].HandlerEnd);
    }

    [Fact]
    public void Lexical_scope_braces_do_not_close_the_enclosing_exception_region()
    {
        var scoped = Sample.Replace("IL_0002:  ldc.i4.s   41", "{\nIL_0002:  ldc.i4.s   41\n}", StringComparison.Ordinal);
        Assert.Equal(IlDasmTextParser.ParseText(Sample).CanonicalMethodsText(),
            IlDasmTextParser.ParseText(scoped).CanonicalMethodsText());
    }

    [Theory]
    [InlineData("int32 modreq([System.Runtime]System.Runtime.CompilerServices.IsVolatile) M(int32 value)", "M")]
    [InlineData("void M<(class [System.Runtime]System.IDisposable) T>(!!T value)", "M")]
    [InlineData("method int32 *(int32) M()", "M")]
    [InlineData("void 'A name'(int32 value)", "A name")]
    public void Method_names_ignore_parentheses_in_modifiers_constraints_and_function_pointers(string signature, string name)
    {
        var text = $".class public X\n{{\n.method public static {signature} cil managed\n{{\n// Code size 1\n.maxstack 8\nIL_0000: ret\n}}\n}}";
        Assert.Equal(name, Assert.Single(IlDasmTextParser.ParseText(text).Methods).MethodName);
    }

    [Fact]
    public void Wrapped_header_preserves_parameter_modifiers_and_generic_constraints()
    {
        const string header = ".method public static void M<(class [System.Runtime]System.IDisposable) T>(\n!!T modopt([System.Runtime]System.Runtime.CompilerServices.IsConst) arg) cil managed";
        var text = $".class public X\n{{\n{header}\n{{\n// Code size 1\n.maxstack 8\nIL_0000: ret\n}}\n}}";
        var original = IlDasmTextParser.ParseText(text);
        Assert.Equal("M", Assert.Single(original.Methods).MethodName);
        foreach (var changed in new[] { text.Replace("modopt", "modreq"), text.Replace("IDisposable", "ICloneable") })
            Assert.NotEqual(original.CanonicalMethodsText(), IlDasmTextParser.ParseText(changed).CanonicalMethodsText());
    }

    [Fact]
    public void Large_offsets_and_switch_targets_are_not_truncated()
    {
        var text = Sample.Replace("IL_001a", "IL_1001a", StringComparison.Ordinal)
            .Replace("IL_001b", "IL_1001b", StringComparison.Ordinal)
            .Replace("40 (0x28)", "65564 (0x1001c)", StringComparison.Ordinal);
        var method = IlDasmTextParser.ParseText(text).Method("TraceMap.CompiledFixtures.CSharp.Il.SampleShapes", "Choose");
        Assert.Contains(method.Instructions, instruction => instruction.Offset == 0x1001a);
        Assert.Contains("1001a", Assert.Single(method.Instructions, instruction => instruction.Opcode == "switch").Operand, StringComparison.Ordinal);
    }

    [Fact]
    public void Quoted_whitespace_in_operands_is_significant()
    {
        var first = Sample.Replace("\"done\"", "\"two  spaces\"", StringComparison.Ordinal);
        var second = first.Replace("two  spaces", "two spaces", StringComparison.Ordinal);
        Assert.NotEqual(IlDasmTextParser.ParseText(first).CanonicalMethodsText(), IlDasmTextParser.ParseText(second).CanonicalMethodsText());
        var catchType = Sample.Replace("catch [System.Runtime]System.Exception", "catch [Sample]'Two  spaces'", StringComparison.Ordinal);
        Assert.NotEqual(IlDasmTextParser.ParseText(catchType).CanonicalMethodsText(),
            IlDasmTextParser.ParseText(catchType.Replace("Two  spaces", "Two spaces", StringComparison.Ordinal)).CanonicalMethodsText());
    }

    [Fact]
    public void Unsupported_line_and_exception_directives_and_unterminated_switches_fail_closed()
    {
        Assert.Throws<InvalidOperationException>(() => IlDasmTextParser.ParseText(Sample.Replace(".maxstack  2", ".line unexpected")));
        Assert.Throws<InvalidOperationException>(() => IlDasmTextParser.ParseText(Sample.Replace(".try", ".try IL_0001 to IL_0008 catch X handler IL_0008 to IL_0016")));
        Assert.Throws<InvalidOperationException>(() => IlDasmTextParser.ParseText(Sample.Replace("IL_001a)", "IL_001a,")));
    }

    private const string Sample = """
        .assembly extern System.Runtime
        {
          .ver 10:0:0:0
        }
        .assembly Sample
        {
          .custom instance void [System.Runtime]System.Runtime.Versioning.TargetFrameworkAttribute::.ctor(string) = ( 01 00 93 02 01 )
          .custom instance void [System.Runtime]System.Reflection.AssemblyCompanyAttribute::.ctor(string) = ( 01 00 07 54 72 61 63 65 )
          .ver 1:0:0:0
        }
        .module Sample.dll
        // MVID: {9a1f1b2c-1111-2222-3333-444455556666}
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
    public void Parses_method_headers_maxstack_locals_code_size_and_instructions()
    {
        var parsed = IlDasmTextParser.ParseText(Sample);

        Assert.Equal(4, parsed.Methods.Count);
        var loop = parsed.Method("TraceMap.CompiledFixtures.CSharp.Il.SampleShapes", "Loop");
        Assert.Equal(2, loop.MaxStack);
        Assert.Equal(1, loop.LocalCount);
        Assert.Equal(33, loop.CodeSize);
        Assert.Equal(14, loop.Instructions.Count);
        Assert.Equal(0, loop.ExceptionRegionCount);
        Assert.Equal("ldstr", loop.Instructions[10].Opcode);
        Assert.Equal("\"done\"", loop.Instructions[10].Operand);
        Assert.Equal(0x11, loop.Instructions[11].Offset);
        Assert.Equal([(0x11, "call")], loop.CallSites);
    }

    [Fact]
    public void Parses_exception_region_kinds_and_instruction_boundary_extents()
    {
        var parsed = IlDasmTextParser.ParseText(Sample);

        var guarded = parsed.Method("TraceMap.CompiledFixtures.CSharp.Il.SampleShapes", "Guarded");
        Assert.Equal(2, guarded.LocalCount);
        var region = Assert.Single(guarded.ExceptionRegions);
        Assert.Equal("catch", region.Kind);
        Assert.Equal(0x01, region.TryStart);
        Assert.Equal(0x08, region.TryEnd);
        Assert.Equal(0x08, region.HandlerStart);
        Assert.Equal(0x16, region.HandlerEnd);
        Assert.Equal("[System.Runtime]System.Exception", region.CatchType);
        Assert.Null(region.FilterOffset);
        Assert.Equal([(0x09, "callvirt"), (0x0e, "call")], guarded.CallSites);
    }

    [Fact]
    public void Canonical_exception_region_changes_when_catch_identity_changes()
    {
        var original = IlDasmTextParser.ParseText(Sample);
        var changed = IlDasmTextParser.ParseText(Sample.Replace(
            "catch [System.Runtime]System.Exception",
            "catch [System.Runtime]System.ArgumentException", StringComparison.Ordinal));

        Assert.NotEqual(original.CanonicalMethodsText(), changed.CanonicalMethodsText());
        Assert.Equal("[System.Runtime]System.ArgumentException",
            Assert.Single(changed.Method("TraceMap.CompiledFixtures.CSharp.Il.SampleShapes", "Guarded").ExceptionRegions).CatchType);
    }

    [Fact]
    public void Parses_filter_start_and_includes_it_in_canonical_exception_region()
    {
        const string text = """
            .class public X
            {
              .method public static void M() cil managed
              {
                // Code size 8 (0x8)
                .maxstack 1
                .try
                {
                  IL_0000: nop
                  IL_0001: leave.s IL_0007
                } // end .try
                filter
                {
                  IL_0003: ldc.i4.1
                  IL_0004: endfilter
                } // end filter
                { // handler
                  IL_0005: pop
                  IL_0006: leave.s IL_0007
                } // end handler
                IL_0007: ret
              } // end of method X::M
            } // end of class X
            """;
        var original = IlDasmTextParser.ParseText(text);
        var region = Assert.Single(original.Method("X", "M").ExceptionRegions);
        Assert.Equal("filter", region.Kind);
        Assert.Equal(0x03, region.FilterOffset);
        Assert.Equal(0x05, region.HandlerStart);
        Assert.Null(region.CatchType);

        var method = original.Method("X", "M");
        var differentFilterStart = method with { ExceptionRegions = [region with { FilterOffset = 0x04 }] };
        Assert.NotEqual(method.Canonical(), differentFilterStart.Canonical());
    }

    [Fact]
    public void Captures_switch_jump_table_targets_from_continuation_lines()
    {
        var parsed = IlDasmTextParser.ParseText(Sample);

        var choose = parsed.Method("TraceMap.CompiledFixtures.CSharp.Il.SampleShapes", "Choose");
        Assert.Equal(1, choose.LocalCount);
        Assert.Equal(3, choose.MaxStack);
        var jump = Assert.Single(choose.Instructions, instruction => instruction.Opcode == "switch");
        Assert.Equal(0x01, jump.Offset);
        Assert.EndsWith(":0010:0015:001a", jump.Operand, StringComparison.Ordinal);
    }

    [Fact]
    public void Canonical_methods_text_ignores_metadata_row_order_but_not_bodies()
    {
        var first = IlDasmTextParser.ParseText(Sample);
        // Reordering the assembly-level custom attributes is exactly what
        // ILAsm legitimately does on re-emission; canonical member bodies
        // must not care. The swap exchanges the two attribute lines'
        // contents in place, so the normalized file order changes while no
        // method body does.
        var reordered = Sample
            .Replace("TargetFrameworkAttribute::.ctor(string) = ( 01 00 93 02 01 )", "PLACEHOLDER-ATTRIBUTE::.ctor(string) = ( 01 00 93 02 01 )")
            .Replace("AssemblyCompanyAttribute::.ctor(string) = ( 01 00 07 54 72 61 63 65 )", "TargetFrameworkAttribute::.ctor(string) = ( 01 00 07 54 72 61 63 65 )")
            .Replace("PLACEHOLDER-ATTRIBUTE::.ctor(string) = ( 01 00 93 02 01 )", "AssemblyCompanyAttribute::.ctor(string) = ( 01 00 93 02 01 )");
        Assert.NotEqual(first.NormalizedText, IlDasmTextParser.ParseText(reordered).NormalizedText);
        Assert.Equal(first.CanonicalMethodsText(), IlDasmTextParser.ParseText(reordered).CanonicalMethodsText());
        // Any body change must still be caught: bump the loop's constant.
        var mutated = Sample.Replace("IL_0006:  ldc.i4.1", "IL_0006:  ldc.i4.2");
        Assert.NotEqual(first.CanonicalMethodsText(), IlDasmTextParser.ParseText(mutated).CanonicalMethodsText());
    }

    [Fact]
    public void Line_directives_bind_to_the_following_instruction_offset()
    {
        var text = """
            .class public X
            {
              .method public static void  M() cil managed
              {
                // Code size 2 (0x2)
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
    public void Wrapped_method_headers_join_until_the_parameter_parenthesis()
    {
        var text = """
            .class public Wrapped
            {
              .method public hidebysig static int32
                      LongSignatureAcrossLines(int32 value) cil managed
              {
                // Code size 4 (0x4)
                .maxstack  1
                IL_0000:  ldarg.0
                IL_0001:  ret
              } // end of method Wrapped::LongSignatureAcrossLines
            } // end of class Wrapped
            """;
        var parsed = IlDasmTextParser.ParseText(text);

        var method = parsed.Method("Wrapped", "LongSignatureAcrossLines");
        Assert.Equal(2, method.Instructions.Count);
        Assert.Equal(0, method.LocalCount);
        Assert.Equal(4, method.CodeSize);
    }

    [Fact]
    public void Malformed_method_headers_and_missing_locals_fail_loudly()
    {
        Assert.Throws<InvalidOperationException>(() => IlDasmTextParser.ParseText(".method public static\n"));
        Assert.Throws<InvalidOperationException>(() => IlDasmTextParser.ParseText(".method public static void  NoBody cil managed\n"));
        var thrown = Assert.Throws<InvalidOperationException>(() => IlDasmTextParser.ParseText(".method public static void  NeverCompletes cil managed\n.more lines without a parenthesis\n"));
        Assert.Contains("NeverCompletes", thrown.Message, StringComparison.Ordinal);
    }
}
