using System.Reflection;
using System.Reflection.Emit;

using Napkin.Core.Geometry;
using Napkin.Core.RulesEngine;
using Napkin.Modules.Building;

namespace Napkin.Modules.Assistant.Tests;

/// <summary>
/// The assistant never asks the rules engine anything (docs/design/llm-assistant.md §1, §12.9): the
/// module may read the engine's result types, and nothing in it builds an engine, loads or finds a
/// pack, resolves a code, or runs a check. Held by reading every method body's IL for the members it
/// calls, constructs or takes the address of — including lambdas, iterators and async state machines,
/// which are methods of their own.
/// </summary>
public class NoEngineTests
{
    [Fact]
    public void The_module_builds_no_rules_engine_and_loads_no_pack()
    {
        Assembly module = typeof(ContextPack).Assembly;
        List<string> found = [.. Forbidden(Bodies(module.GetTypes()))];
        Assert.True(found.Count == 0, "The assistant module reaches the rules engine: " + string.Join("; ", found));

        // The scan saw the module's code: it found the members the module does use.
        List<MethodBase> used = [.. Bodies(module.GetTypes()).SelectMany(body => Called(body))];
        Assert.Contains(used, member => member.DeclaringType == typeof(CodeCheck) && member.Name == nameof(CodeCheck.CheckingStatus));
        Assert.Contains(used, member => member.DeclaringType == typeof(Napkin.Core.Geometry.Length) && member.Name == nameof(Napkin.Core.Geometry.Length.TryParse));
    }

    [Fact]
    public void The_scan_finds_each_way_of_reaching_the_engine_where_there_is_one()
    {
        // A positive control: a scanner that finds nothing might only be broken.
        List<string> found = [.. Forbidden(Bodies([typeof(Offender)]))];
        Assert.Contains(found, line => line.Contains("RulesEngine.For", StringComparison.Ordinal));
        Assert.Contains(found, line => line.Contains("IRulesEngine.SizeHeader", StringComparison.Ordinal));
        Assert.Contains(found, line => line.Contains("CodePacks.Discover", StringComparison.Ordinal));
        Assert.Contains(found, line => line.Contains("CodePacks.Resolve", StringComparison.Ordinal));
        Assert.Contains(found, line => line.Contains("CodePacks..ctor", StringComparison.Ordinal));
        Assert.Contains(found, line => line.Contains("CodeCheck.Of", StringComparison.Ordinal));
        Assert.Contains(found, line => line.Contains("BracingCheck.For", StringComparison.Ordinal));
        Assert.Contains(found, line => line.Contains("PackCatalog.Discover", StringComparison.Ordinal));
        Assert.Contains(found, line => line.Contains("LoadedPack.<Clone>$", StringComparison.Ordinal));
        Assert.Contains(found, line => line.Contains("Loaded..ctor", StringComparison.Ordinal));

        // A lambda's body is a method of a compiler-generated type, and the scan reads it too.
        Assert.Contains(found, line => line.Contains("<Lambda>", StringComparison.Ordinal) && line.EndsWith("reaches RulesEngine.For", StringComparison.Ordinal));
    }

    /// <summary>What the scan must find. Never called.</summary>
    private static class Offender
    {
        public static object Build(LoadedPack pack) => RulesEngine.For(pack);

        public static object Size(IRulesEngine engine, HeaderRequest request) => engine.SizeHeader(request);

        public static object Discover() => CodePacks.Discover(["nowhere"]);

        public static object Resolve(CodePacks packs) => packs.Resolve(null);

        public static object Construct() => new CodePacks([]);

        public static object Check(Sketch sketch, CodePacks packs) => CodeCheck.Of(sketch, packs);

        public static object Brace(Sketch sketch, WallLine line, CodeResolution code) => BracingCheck.For(sketch, line, code);

        public static object Catalog() => PackCatalog.Discover("nowhere");

        public static object Copy(LoadedPack pack) => pack with { };

        public static object Wrap(LoadedPack pack) => new PackLoadResult.Loaded(pack);

        public static Func<LoadedPack, IRulesEngine> Lambda() => pack => RulesEngine.For(pack);
    }

    private static readonly HashSet<string> ForbiddenMembers =
    [
        $"{nameof(CodePacks)}.{nameof(CodePacks.Discover)}",
        $"{nameof(CodePacks)}.{nameof(CodePacks.Resolve)}",
        $"{nameof(CodeCheck)}.{nameof(CodeCheck.Of)}",
        $"{nameof(CodeCheck)}.{nameof(CodeCheck.OfView)}",
        $"{nameof(CodeCheck)}.{nameof(CodeCheck.Check)}",
        $"{nameof(CodeCheck)}.{nameof(CodeCheck.For)}",
        $"{nameof(BracingCheck)}.{nameof(BracingCheck.Of)}",
        $"{nameof(BracingCheck)}.{nameof(BracingCheck.For)}",
        $"{nameof(DeckCheck)}.{nameof(DeckCheck.Of)}",
        $"{nameof(DeckCheck)}.{nameof(DeckCheck.For)}",
    ];

    private static readonly Type[] ForbiddenTypes = [typeof(RulesEngine), typeof(PackLoader), typeof(PackCatalog), typeof(PackLocations), typeof(GoldenRunner)];

    private static readonly Type[] NeverConstructed = [typeof(LoadedPack), typeof(CodePacks), typeof(PackLoadResult)];

    private static IEnumerable<string> Forbidden(IEnumerable<MethodBase> bodies)
    {
        foreach (MethodBase body in bodies)
        {
            foreach (MethodBase member in Called(body))
            {
                Type? declaring = member.DeclaringType;
                string name = $"{declaring?.Name}.{member.Name}";
                bool forbidden =
                    (declaring is not null && typeof(IRulesEngine).IsAssignableFrom(declaring))
                    || (member is MethodInfo method && typeof(IRulesEngine).IsAssignableFrom(method.ReturnType))
                    || (declaring is not null && ForbiddenTypes.Contains(declaring))
                    || ForbiddenMembers.Contains(name)
                    || (declaring is not null && member.IsConstructor && NeverConstructed.Any(type => type.IsAssignableFrom(declaring)))
                    || (declaring is not null && member.Name == "<Clone>$" && NeverConstructed.Any(type => type.IsAssignableFrom(declaring)));
                if (forbidden)
                {
                    yield return $"{body.DeclaringType?.FullName}.{body.Name} reaches {name}";
                }
            }
        }
    }

    /// <summary>Every method and constructor with a body, in these types and every type nested in them.</summary>
    private static IEnumerable<MethodBase> Bodies(IEnumerable<Type> types)
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        foreach (Type type in types)
        {
            foreach (MethodBase method in type.GetMethods(all).Cast<MethodBase>().Concat(type.GetConstructors(all)))
            {
                if (method.GetMethodBody() is not null)
                {
                    yield return method;
                }
            }

            foreach (MethodBase nested in Bodies(type.GetNestedTypes(all)))
            {
                yield return nested;
            }
        }
    }

    private static readonly Dictionary<short, OpCode> OpCodesByValue = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(field => (OpCode)field.GetValue(null)!)
        .ToDictionary(code => code.Value);

    /// <summary>Every method or constructor a body calls, constructs or takes the address of, read from its IL.</summary>
    private static IEnumerable<MethodBase> Called(MethodBase body)
    {
        byte[] il = body.GetMethodBody()?.GetILAsByteArray() ?? [];
        Type[] typeArguments = body.DeclaringType is { IsGenericType: true } generic ? generic.GetGenericArguments() : [];
        Type[] methodArguments = body.IsGenericMethod ? body.GetGenericArguments() : [];
        int i = 0;
        while (i < il.Length)
        {
            short value = il[i] == 0xFE ? (short)(0xFE00 | il[++i]) : il[i];
            i++;
            OpCode code = OpCodesByValue[value];
            if (code.OperandType is OperandType.InlineMethod)
            {
                MethodBase? member = null;
                try
                {
                    member = body.Module.ResolveMethod(BitConverter.ToInt32(il, i), typeArguments, methodArguments);
                }
                catch (ArgumentException)
                {
                    // A token that is not a method in this generic context: nothing to check.
                }

                if (member is not null)
                {
                    yield return member;
                }
            }

            i += code.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + (4 * BitConverter.ToInt32(il, i)),
                _ => 4,
            };
        }
    }
}
