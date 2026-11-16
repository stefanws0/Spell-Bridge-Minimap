using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;

// Reads metadata only; never loads or runs the assembly.
// usage: InteropDump <dll> <typeNameRegex> [memberRegex]
if (args.Length < 2)
{
    Console.WriteLine("usage: InteropDump <dll> <typeNameRegex> [memberRegex]");
    return 1;
}
var typeRx = new Regex(args[1]);
var memRx = args.Length > 2 ? new Regex(args[2], RegexOptions.IgnoreCase) : null;
using var fs = File.OpenRead(args[0]);
using var pe = new PEReader(fs);
var md = pe.GetMetadataReader();
var prov = new Prov(md);
foreach (var th in md.TypeDefinitions)
{
    var td = md.GetTypeDefinition(th);
    var full = TypeName(md, td);
    if (!typeRx.IsMatch(full)) continue;
    string baseName = td.BaseType.IsNil ? "" : prov.Name(td.BaseType);
    Console.WriteLine($"== {full} : {baseName}  [{td.Attributes & TypeAttributes.VisibilityMask}]");
    foreach (var fh in td.GetFields())
    {
        var f = md.GetFieldDefinition(fh);
        var n = md.GetString(f.Name);
        // Il2CppInterop's per-member pointer fields: hidden unless the member regex asks for them.
        // A method with a NativeMethodInfoPtr_ field exists in the game build; one without was "unstripped".
        bool native = n.StartsWith("NativeFieldInfoPtr") || n.StartsWith("NativeMethodInfoPtr");
        if (native && (memRx == null || !memRx.IsMatch(n))) continue;
        if (memRx != null && !memRx.IsMatch(n)) continue;
        var st = (f.Attributes & FieldAttributes.Static) != 0 ? "static " : "";
        Console.WriteLine($"  F {st}{f.DecodeSignature(prov, null)} {n}");
    }
    foreach (var ph in td.GetProperties())
    {
        var p = md.GetPropertyDefinition(ph);
        var n = md.GetString(p.Name);
        if (memRx != null && !memRx.IsMatch(n)) continue;
        Console.WriteLine($"  P {p.DecodeSignature(prov, null).ReturnType} {n}");
    }
    foreach (var mh in td.GetMethods())
    {
        var m = md.GetMethodDefinition(mh);
        var n = md.GetString(m.Name);
        if ((n.StartsWith("get_") || n.StartsWith("set_")) && memRx == null) continue;
        if (n == ".cctor") continue;
        if (memRx != null && !memRx.IsMatch(n)) continue;
        var sig = m.DecodeSignature(prov, null);
        var names = m.GetParameters().Select(h => md.GetString(md.GetParameter(h).Name)).Where(s => s.Length > 0).ToList();
        var parts = sig.ParameterTypes.Select((t, i) => i < names.Count ? $"{t} {names[i]}" : t);
        var st = (m.Attributes & MethodAttributes.Static) != 0 ? "static " : "";
        Console.WriteLine($"  M {st}{sig.ReturnType} {n}({string.Join(", ", parts)})");
    }
}
return 0;

static string TypeName(MetadataReader md, TypeDefinition td)
{
    try
    {
        var ns = md.GetString(td.Namespace);
        var n = md.GetString(td.Name);
        if (td.IsNested) return TypeName(md, md.GetTypeDefinition(td.GetDeclaringType())) + "+" + n;
        return string.IsNullOrEmpty(ns) ? n : ns + "." + n;
    }
    catch (BadImageFormatException) { return "?"; }
}

class Prov : ISignatureTypeProvider<string, object>
{
    readonly MetadataReader md;
    public Prov(MetadataReader m) { md = m; }
    public string Name(EntityHandle h) => h.Kind switch
    {
        HandleKind.TypeDefinition => GetTypeFromDefinition(md, (TypeDefinitionHandle)h, 0),
        HandleKind.TypeReference => GetTypeFromReference(md, (TypeReferenceHandle)h, 0),
        HandleKind.TypeSpecification => GetTypeFromSpecification(md, null, (TypeSpecificationHandle)h, 0),
        _ => "?"
    };
    public string GetArrayType(string e, ArrayShape s) => e + "[,]";
    public string GetByReferenceType(string e) => "ref " + e;
    public string GetFunctionPointerType(MethodSignature<string> s) => "fnptr";
    public string GetGenericInstantiation(string g, ImmutableArray<string> a) => $"{g}<{string.Join(",", a)}>";
    public string GetGenericMethodParameter(object c, int i) => "!!" + i;
    public string GetGenericTypeParameter(object c, int i) => "!" + i;
    public string GetModifiedType(string m, string u, bool r) => u;
    public string GetPinnedType(string e) => e;
    public string GetPointerType(string e) => e + "*";
    public string GetPrimitiveType(PrimitiveTypeCode t) => t.ToString().ToLower();
    public string GetSZArrayType(string e) => e + "[]";
    public string GetTypeFromDefinition(MetadataReader r, TypeDefinitionHandle h, byte k) => r.GetString(r.GetTypeDefinition(h).Name);
    public string GetTypeFromReference(MetadataReader r, TypeReferenceHandle h, byte k) => r.GetString(r.GetTypeReference(h).Name);
    public string GetTypeFromSpecification(MetadataReader r, object c, TypeSpecificationHandle h, byte k) => r.GetTypeSpecification(h).DecodeSignature(this, c);
}
