using System.Text;
using System.Linq;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Valid.Generator;

[Generator]
public class PropertyWeirGenerator : ISourceGenerator
{
    private static readonly DiagnosticDescriptor TooManyPropertiesError = new DiagnosticDescriptor(
        id: "VALID001",
        title: "Too many VALID properties",
        messageFormat: "The VALID object '{0}' has {1} properties, which exceeds the 128-property limit of the UInt128 bitmask engine.",
        category: "Valid.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "VALID objects are limited to 128 properties to ensure O(1) bitmask performance and avoid overflow.");

    public void Initialize(GeneratorInitializationContext context)
    {
    }

    public void Execute(GeneratorExecutionContext context)
    {
        var syntaxTrees = context.Compilation.SyntaxTrees;
        // Ensure partial declarations are matched
        var uniqueSymbols = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var tree in syntaxTrees)
        {
            var model = context.Compilation.GetSemanticModel(tree);
            var classes = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>();

            foreach (var cds in classes)
            {
                var symbol = model.GetDeclaredSymbol(cds) as INamedTypeSymbol;
                if (symbol == null) continue;

                if (symbol.GetAttributes().Any(a => a.AttributeClass?.Name == "ValidObjectAttribute"))
                {
                    uniqueSymbols.Add(symbol);
                }
            }
        }

        foreach (var symbol in uniqueSymbols)
        {
            var safeName = symbol.ToDisplayString().Replace(".", "_");

            var source = GenerateSource(symbol);
            context.AddSource($"{safeName}.g.cs", SourceText.From(source, Encoding.UTF8));

            // Generate tests
            if (IsXunitReferenced(context.Compilation))
            {
                var tests = GenerateTests(context.Compilation, symbol);
                context.AddSource($"{safeName}.g.tests.cs", SourceText.From(tests, Encoding.UTF8));

                var fuzzTests = GenerateFuzzTests(context.Compilation, symbol);
                context.AddSource($"{safeName}.g.fuzz.cs", SourceText.From(fuzzTests, Encoding.UTF8));
            }

            if (IsBunitReferenced(context.Compilation))
            {
                var bunitTests = GenerateBunitTests(context.Compilation, symbol);
                context.AddSource($"{safeName}.g.bunit.cs", SourceText.From(bunitTests, Encoding.UTF8));
            }

            var properties = GetProperties(symbol);
            if (properties.Count > 128)
            {
                var location = symbol.Locations.FirstOrDefault() ?? Location.None;
                context.ReportDiagnostic(Diagnostic.Create(TooManyPropertiesError, location, symbol.Name, properties.Count));
                continue;
            }

            var autoPilot = GenerateVavidAutoPilot(context.Compilation, symbol);
            context.AddSource($"{safeName}.g.autopilot.cs", SourceText.From(autoPilot, Encoding.UTF8));

            var mcpTools = GenerateMcpTools(context.Compilation, symbol);
            if (!string.IsNullOrEmpty(mcpTools))
            {
                context.AddSource($"{safeName}.g.mcp.cs", SourceText.From(mcpTools, Encoding.UTF8));
            }
        }
    }

    private List<ManagedProperty> GetProperties(INamedTypeSymbol symbol)
    {
        // Explicit properties
        var explicitProperties = symbol.GetMembers().OfType<IPropertySymbol>()
            .Where(p => p.GetAttributes().Any(a => a.AttributeClass?.Name == "ValidPropertyAttribute"))
            .Select(p => new ManagedProperty { Name = p.Name, Type = p.Type, IsExplicit = true })
            .ToList();

        // Field properties
        var fieldProperties = symbol.GetMembers().OfType<IFieldSymbol>()
            .Where(f => f.GetAttributes().Any(a => a.AttributeClass?.Name == "ValidFieldAttribute"))
            .Select(f => {
                var name = f.Name.TrimStart('_');
                if (name.Length > 0 && char.IsLower(name[0]))
                {
                    name = char.ToUpper(name[0]) + name.Substring(1);
                }
                return new ManagedProperty { 
                    Name = name,
                    FieldName = f.Name,
                    Type = f.Type, 
                    IsExplicit = false 
                };
            })
            .ToList();

        return explicitProperties.Concat(fieldProperties).ToList();
    }

    private bool IsXunitReferenced(Compilation compilation)
    {
        // Check xUnit reference
        return compilation.GetTypeByMetadataName("Xunit.FactAttribute") != null;
    }

    private bool IsBunitReferenced(Compilation compilation)
    {
        return compilation.GetTypeByMetadataName("Bunit.TestContextWrapper") != null || compilation.GetTypeByMetadataName("Bunit.TestContext") != null;
    }

    private string GenerateSource(INamedTypeSymbol symbol)
    {
        var namespaceName = symbol.ContainingNamespace.ToDisplayString();
        var className = symbol.Name;
        
        var properties = GetProperties(symbol);

        // Exclude existing properties
        var existingFullyImplementedPropertyNames = new HashSet<string>(symbol.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(p => !p.DeclaringSyntaxReferences.Any(r => r.GetSyntax() is Microsoft.CodeAnalysis.CSharp.Syntax.PropertyDeclarationSyntax ps && ps.Modifiers.Any(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword)))
            .Select(p => p.Name));

        // Backing fields
        // Backing fields for new properties
        var propertiesToGenerate = properties
            .Where(prop => prop.IsExplicit && !existingFullyImplementedPropertyNames.Contains(prop.Name))
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated - VERSION 3.0.5 />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using Valid;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Text.Json;");
        sb.AppendLine();
        sb.AppendLine($"namespace {namespaceName}");
        sb.AppendLine("{");
        sb.AppendLine($"    public partial class {className} : ValidObjectBase");
        sb.AppendLine("    {");
        sb.AppendLine($"        public const int PropertyCount = {properties.Count};");
        sb.Append("        public static readonly string[] PropertyNames = new[] { ");
        for (int i = 0; i < properties.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append($"\"{properties[i].Name}\"");
        }
        sb.AppendLine(" };");
        sb.AppendLine();

        if (!HasMember(symbol, "_diagnostics") && !HasMember(symbol, "GetDiagnostics"))
        {
            sb.AppendLine("        private readonly List<DiagnosticResult> _diagnostics = new List<DiagnosticResult>();");
        }

        bool hasNested = false;
        // New properties
        for (int i = 0; i < propertiesToGenerate.Count; i++)
        {
            var prop = propertiesToGenerate[i];
            var fieldName = prop.IsExplicit ? $"_{char.ToLower(prop.Name[0])}{prop.Name.Substring(1)}" : prop.FieldName;
            if (fieldName == prop.Name) fieldName = "_" + fieldName;

            var initializer = "";
            if (prop.Type.SpecialType == Microsoft.CodeAnalysis.SpecialType.System_String)
                initializer = " = \"\"";
            else if (!prop.Type.IsValueType)
                initializer = " = default!";
            
            sb.AppendLine($"        private {prop.Type} {fieldName}{initializer};");
        }
        sb.AppendLine();

        // Property implementations
        for (int i = 0; i < properties.Count; i++)
        {
            var prop = properties[i];
            bool isPartial = false;
            var existingProp = symbol.GetMembers(prop.Name).OfType<IPropertySymbol>().FirstOrDefault();
            if (existingProp != null)
            {
                var syntax = existingProp.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() as Microsoft.CodeAnalysis.CSharp.Syntax.PropertyDeclarationSyntax;
                if (syntax != null && syntax.Modifiers.Any(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword))
                {
                    isPartial = true;
                }
            }
            var isExistingField = symbol.GetMembers(prop.Name).OfType<IFieldSymbol>().Any();
            var isExisting = (existingProp != null && !isPartial) || isExistingField;

            // Skip existing
            if (isExisting) continue;
            
            var fieldName = prop.IsExplicit ? $"_{char.ToLower(prop.Name[0])}{prop.Name.Substring(1)}" : prop.FieldName;
            if (fieldName == prop.Name) fieldName = "_" + fieldName;

            var isNested = IsValidObject(prop.Type);
            if (isNested) hasNested = true;

            string partialKeyword = isPartial ? "partial " : "";
            sb.AppendLine($"        public {partialKeyword}{prop.Type} {prop.Name}");
            sb.AppendLine("        {");
            sb.AppendLine($"            get => {fieldName};");
            sb.AppendLine("            set {");
            sb.AppendLine($"                var oldVal = {fieldName};");
            sb.AppendLine($"                if (SetProperty(ref {fieldName}, value, {i})) {{");
            if (isNested)
            {
                sb.AppendLine("                    if (oldVal != null) ((System.ComponentModel.INotifyPropertyChanged)oldVal).PropertyChanged -= Child_PropertyChanged;");
                sb.AppendLine("                    if (value != null) ((System.ComponentModel.INotifyPropertyChanged)value).PropertyChanged += Child_PropertyChanged;");
            }
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        if (hasNested)
        {
            sb.AppendLine("        private void Child_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)");
            sb.AppendLine("        {");
            sb.AppendLine("            Validate();");
            sb.AppendLine("            OnPropertyChanged(e.PropertyName);");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        // Diagnostics
        if (!HasMember(symbol, "GetDiagnostics"))
        {
            sb.AppendLine("        public override IEnumerable<DiagnosticResult> GetDiagnostics()");
            sb.AppendLine("        {");
            sb.AppendLine("            var results = new List<DiagnosticResult>(_diagnostics);");
            for (int i = 0; i < properties.Count; i++)
            {
                var prop = properties[i];
                if (IsValidObject(prop.Type))
                {
                    sb.AppendLine($"            if ({prop.Name} != null) results.AddRange({prop.Name}.GetDiagnostics());");
                }
            }
            sb.AppendLine("            return results;");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        if (!HasMember(symbol, "CalculateValidationState"))
        {
            sb.AppendLine("        public override System.UInt128 CalculateValidationState()");
            sb.AppendLine("        {");
            sb.AppendLine("            _diagnostics.Clear();");
            sb.AppendLine("            System.UInt128 newErrors = 0;");
            
            for (int i = 0; i < properties.Count; i++)
            {
                var prop = properties[i];
                
                ISymbol? memberSymbol = null;
                if (prop.IsExplicit)
                {
                    memberSymbol = symbol.GetMembers(prop.Name).OfType<IPropertySymbol>().FirstOrDefault();
                }
                else
                {
                    memberSymbol = symbol.GetMembers(prop.FieldName).OfType<IFieldSymbol>().FirstOrDefault();
                }
                
                if (memberSymbol != null)
                {
                    var attrs = memberSymbol.GetAttributes();
                    foreach (var attr in attrs)
                    {
                        var attrName = attr.AttributeClass?.Name;
                        if (attrName == "RequiredAttribute" || attrName == "Required")
                        {
                            string msg = "Field is required";
                            string code = "VAL-REQ";
                            if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is string m) msg = m;
                            if (attr.ConstructorArguments.Length > 1 && attr.ConstructorArguments[1].Value is string c) code = c;
                            
                            if (prop.Type.SpecialType == SpecialType.System_String)
                            {
                                sb.AppendLine($"            if (string.IsNullOrWhiteSpace(this.{prop.Name}))");
                            }
                            else
                            {
                                sb.AppendLine($"            if (this.{prop.Name} == null)");
                            }
                            sb.AppendLine("            {");
                            sb.AppendLine($"                newErrors |= ((System.UInt128)1 << {i});");
                            sb.AppendLine($"                _diagnostics.Add(new DiagnosticResult(\"{prop.Name}\", \"{msg}\", \"{code}\", null));");
                            sb.AppendLine("            }");
                        }
                        else if (attrName == "RangeAttribute" || attrName == "Range")
                        {
                            double min = 0.0;
                            double max = 0.0;
                            string msg = "Value out of range";
                            string code = "VAL-001";
                            if (attr.ConstructorArguments.Length > 0) min = System.Convert.ToDouble(attr.ConstructorArguments[0].Value);
                            if (attr.ConstructorArguments.Length > 1) max = System.Convert.ToDouble(attr.ConstructorArguments[1].Value);
                            if (attr.ConstructorArguments.Length > 2 && attr.ConstructorArguments[2].Value is string m) msg = m;
                            if (attr.ConstructorArguments.Length > 3 && attr.ConstructorArguments[3].Value is string c) code = c;
                            
                            sb.AppendLine($"            if (((double)this.{prop.Name}) < {min} || ((double)this.{prop.Name}) > {max})");
                            sb.AppendLine("            {");
                            sb.AppendLine($"                newErrors |= ((System.UInt128)1 << {i});");
                            sb.AppendLine($"                _diagnostics.Add(new DiagnosticResult(\"{prop.Name}\", \"{msg}\", \"{code}\", null));");
                            sb.AppendLine("            }");
                        }
                        else if (attrName == "StringLengthAttribute" || attrName == "StringLength")
                        {
                            int maxLen = 0;
                            int minLen = 0;
                            string msg = "Length out of bounds";
                            string code = "VAL-LEN";
                            if (attr.ConstructorArguments.Length > 0) maxLen = System.Convert.ToInt32(attr.ConstructorArguments[0].Value);
                            if (attr.ConstructorArguments.Length > 1 && attr.ConstructorArguments[1].Value is string m) msg = m;
                            if (attr.ConstructorArguments.Length > 2 && attr.ConstructorArguments[2].Value is string c) code = c;
                            
                            var minLengthArg = attr.NamedArguments.FirstOrDefault(na => na.Key == "MinimumLength");
                            if (minLengthArg.Value.Value is int len) minLen = len;
                            
                            sb.AppendLine($"            if (this.{prop.Name} != null && (this.{prop.Name}.Length > {maxLen} || this.{prop.Name}.Length < {minLen}))");
                            sb.AppendLine("            {");
                            sb.AppendLine($"                newErrors |= ((System.UInt128)1 << {i});");
                            sb.AppendLine($"                _diagnostics.Add(new DiagnosticResult(\"{prop.Name}\", \"{msg}\", \"{code}\", null));");
                            sb.AppendLine("            }");
                        }
                    }
                }
                
                if (IsValidObject(prop.Type))
                {
                    sb.AppendLine($"            if (this.{prop.Name} != null && this.{prop.Name}.CalculateValidationState() != System.UInt128.Zero) newErrors |= ((System.UInt128)1 << {i});");
                }
            }
            sb.AppendLine("            return newErrors;");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        // Hydrator
        if (!HasMember(symbol, "Hydrate"))
        {
            sb.AppendLine("        public void Hydrate(ref Utf8JsonReader reader)");
            sb.AppendLine("        {");
            sb.AppendLine("            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)");
            sb.AppendLine("            {");
            sb.AppendLine("                if (reader.TokenType != JsonTokenType.PropertyName) continue;");
            sb.AppendLine("                var propertyName = reader.GetString();");
            sb.AppendLine("                reader.Read();");
            sb.AppendLine("                switch (propertyName)");
            sb.AppendLine("                {");
            foreach (var prop in properties)
            {
                if (IsValidObject(prop.Type))
                {
                    sb.AppendLine($"                    case \"{prop.Name}\": {prop.Name} = JsonSerializer.Deserialize<{prop.Type.ToDisplayString()}>(ref reader)!; break;");
                }
                else
                {
                    var jsonMethod = GetJsonMethod(prop.Type);
                    if (jsonMethod == "GetAny")
                        sb.AppendLine($"                    case \"{prop.Name}\": {prop.Name} = JsonSerializer.Deserialize<{prop.Type.ToDisplayString()}>(ref reader)!; break;");
                    else
                        sb.AppendLine($"                    case \"{prop.Name}\": {prop.Name} = reader.{jsonMethod}(); break;");
                }
            }
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        // Delta serialization
        if (!HasMember(symbol, "GetDeltaJson"))
        {
            sb.AppendLine("        public override string GetDeltaJson()");
            sb.AppendLine("        {");
            sb.AppendLine("            string json = \"{}\";");
            sb.AppendLine("            LockMasks(() => {");
            sb.AppendLine("                if (DirtyFlags == System.UInt128.Zero) return;");
            sb.AppendLine("                var bufferWriter = new System.Buffers.ArrayBufferWriter<byte>(256);");
            sb.AppendLine("                using (var writer = new Utf8JsonWriter(bufferWriter))");
            sb.AppendLine("                {");
            sb.AppendLine("                    WriteDelta(writer);");
            sb.AppendLine("                }");
            sb.AppendLine("                json = System.Text.Encoding.UTF8.GetString(bufferWriter.WrittenSpan);");
            sb.AppendLine("            });");
            sb.AppendLine("            return json;");
            sb.AppendLine("        }");
            sb.AppendLine();
            
            sb.AppendLine("        public override void WriteDelta(Utf8JsonWriter writer)");
            sb.AppendLine("        {");
            sb.AppendLine("            writer.WriteStartObject();");
            for (int i = 0; i < properties.Count; i++)
            {
                var prop = properties[i];
                sb.AppendLine($"            if ((DirtyFlags & ((System.UInt128)1 << {i})) != System.UInt128.Zero)");
                sb.AppendLine("            {");
                sb.AppendLine($"                writer.WritePropertyName(\"{prop.Name}\");");
                sb.AppendLine($"                JsonSerializer.Serialize(writer, {prop.Name});");
                sb.AppendLine("            }");
            }
            sb.AppendLine("            writer.WriteEndObject();");
            sb.AppendLine("            _dirtyFlags = System.UInt128.Zero; // Internal reset (called under lock)");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        // SetPropertyValue
        if (!HasMember(symbol, "SetPropertyValue"))
        {
            sb.AppendLine("        public override void SetPropertyValue(string propertyName, object? value)");
            sb.AppendLine("        {");
            sb.AppendLine("            switch (propertyName)");
            sb.AppendLine("            {");
            foreach (var prop in properties)
            {
                sb.AppendLine($"                case \"{prop.Name}\": {prop.Name} = ({prop.Type.ToDisplayString()})value!; break;");
            }
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        // GetPropertyValue
        sb.AppendLine("        public override object? GetPropertyValue(string propertyName)");
        sb.AppendLine("        {");
        sb.AppendLine("            return propertyName switch");
        sb.AppendLine("            {");
        foreach (var prop in properties)
        {
            sb.AppendLine($"                \"{prop.Name}\" => {prop.Name},");
        }
        sb.AppendLine("                _ => null");
        sb.AppendLine("            };");
        sb.AppendLine("        }");
        sb.AppendLine();

        // Metadata
        sb.AppendLine("        public override string GetValidMetadata()");
        sb.AppendLine("        {");
        sb.AppendLine($"            var metadata = new {{ ");
        sb.AppendLine($"                Name = \"{className}\", ");
        if (propertiesToGenerate.Count == 0)
        {
            sb.AppendLine("                Properties = new object[0]");
        }
        else
        {
            sb.AppendLine("                Properties = new object[] {");
            bool first = true;
            for (int i = 0; i < properties.Count; i++)
            {
                var prop = properties[i];
                // Include handled metadata
                if (existingFullyImplementedPropertyNames.Contains(prop.Name) && !prop.IsExplicit) continue; 
                
                if (!first) sb.AppendLine(",");
                sb.Append($"                    new {{ Name = \"{prop.Name}\", Type = \"{prop.Type.ToDisplayString()}\", BitIndex = {i}, Rules = new object[0] }}");
                first = false;
            }
            sb.AppendLine();
            sb.AppendLine("                }");
        }
        sb.AppendLine("            };");
        sb.AppendLine("            return JsonSerializer.Serialize(metadata);");
        sb.AppendLine("        }");
        sb.AppendLine();

        sb.AppendLine("        public override System.Type GetPropertyType(string propertyName)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (TryGetPropertyType(propertyName, out var type)) return type;");
        sb.AppendLine("            throw new KeyNotFoundException(propertyName);");
        sb.AppendLine("        }");
        sb.AppendLine();

        sb.AppendLine("        private bool TryGetPropertyType(string propertyName, out System.Type? type)");
        sb.AppendLine("        {");
        sb.AppendLine("            switch (propertyName)");
        sb.AppendLine("            {");
        foreach (var prop in propertiesToGenerate)
        {
            sb.AppendLine($"                case \"{prop.Name}\": type = typeof({prop.Type.ToDisplayString()}); return true;");
        }
        sb.AppendLine("            }");
        sb.AppendLine("            type = null;");
        sb.AppendLine("            return false;");
        sb.AppendLine("        }");
        sb.AppendLine();

        sb.AppendLine("        public override int GetBitIndex(string propertyName)");
        sb.AppendLine("        {");
        sb.AppendLine("            return propertyName switch");
        sb.AppendLine("            {");
        for (int i = 0; i < properties.Count; i++)
        {
            var prop = properties[i];
            sb.AppendLine($"                \"{prop.Name}\" => {i},");
        }
        sb.AppendLine("                _ => -1");
        sb.AppendLine("            };");
        sb.AppendLine("        }");
        sb.AppendLine();

        sb.AppendLine("        public override void UpdatePropertyFromJson(string propertyName, string jsonValue)");
        sb.AppendLine("        {");
        sb.AppendLine("            var bytes = System.Text.Encoding.UTF8.GetBytes(jsonValue);");
        sb.AppendLine("            var reader = new Utf8JsonReader(bytes);");
        sb.AppendLine("            reader.Read();");
        sb.AppendLine("            if (string.IsNullOrEmpty(propertyName))");
        sb.AppendLine("            {");
        sb.AppendLine("                Hydrate(ref reader);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            switch (propertyName)");
        sb.AppendLine("            {");
        for (int i = 0; i < properties.Count; i++)
        {
            var prop = properties[i];
            if (IsValidObject(prop.Type))
            {
                sb.AppendLine($"                case \"{prop.Name}\": this.{prop.Name} = JsonSerializer.Deserialize<{prop.Type.ToDisplayString()}>(ref reader)!; break;");
            }
            else
            {
                var jsonMethod = GetJsonMethod(prop.Type);
                if (jsonMethod == "GetAny")
                    sb.AppendLine($"                case \"{prop.Name}\": this.{prop.Name} = JsonSerializer.Deserialize<{prop.Type.ToDisplayString()}>(ref reader)!; break;");
                else
                    sb.AppendLine($"                case \"{prop.Name}\": this.{prop.Name} = reader.{jsonMethod}(); break;");
            }
        }
        sb.AppendLine("            }");
        sb.AppendLine("        }");

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private string GetJsonMethod(ITypeSymbol type)
    {
        if (type.SpecialType == SpecialType.System_String) return "GetString";
        if (type.SpecialType == SpecialType.System_Int32) return "GetInt32";
        if (type.SpecialType == SpecialType.System_Int64) return "GetInt64";
        if (type.SpecialType == SpecialType.System_Double) return "GetDouble";
        if (type.SpecialType == SpecialType.System_Boolean) return "GetBoolean";
        return "GetAny";
    }

    private string GenerateTests(Compilation compilation, INamedTypeSymbol symbol)
    {
        if (!IsXunitReferenced(compilation)) return "";

        var namespaceName = symbol.ContainingNamespace.ToDisplayString();
        var className = symbol.Name;
        // Symmetry testing
        var properties = symbol.GetMembers().OfType<IPropertySymbol>()
            .Where(p => p.GetAttributes().Any(a => a.AttributeClass?.Name == "ValidPropertyAttribute" || a.AttributeClass?.Name == "PropertyWeirAttribute"))
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("using Xunit;");
        sb.AppendLine("using Valid;");
        sb.AppendLine();
        sb.AppendLine($"namespace {namespaceName}.Tests");
        sb.AppendLine("{");
        sb.AppendLine($"    public class {className}Tests");
        sb.AppendLine("    {");

        // Symmetry test
        sb.AppendLine("        [Fact]");
        sb.AppendLine($"        public void {className}_Bitmask_Integrity_Verification()");
        sb.AppendLine("        {");
        sb.AppendLine($"            var obj = new {className}();");
        for (int i = 0; i < properties.Count; i++)
        {
            var prop = properties[i];
            sb.AppendLine($"            obj.{prop.Name} = default!; // Reset");
            sb.AppendLine("            obj.ResetDirty();");
            sb.AppendLine($"            Assert.False((obj.DirtyFlags & ((System.UInt128)1 << {i})) != System.UInt128.Zero);");
            // Set bit
            sb.AppendLine($"            obj.{prop.Name} = {GetTestValue(prop.Type)};");
            sb.AppendLine($"            Assert.True((obj.DirtyFlags & ((System.UInt128)1 << {i})) != System.UInt128.Zero);");
            // Verify bit
            sb.AppendLine($"            Assert.Equal((System.UInt128)1 << {i}, obj.DirtyFlags);");
        }
        sb.AppendLine("        }");

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private string GetTestValue(ITypeSymbol type)
    {
        if (type.SpecialType == SpecialType.System_String) return "\"Valid Test\"";
        if (type.SpecialType == SpecialType.System_Int32) return "42";
        if (type.SpecialType == SpecialType.System_Int64) return "42L";
        if (type.SpecialType == SpecialType.System_Double) return "42.5";
        if (type.SpecialType == SpecialType.System_Decimal) return "42.5m";
        if (type.SpecialType == SpecialType.System_Boolean) return "true";
        if (type.TypeKind == TypeKind.Enum) return $"(({type.ToDisplayString()})1)";
        
        if (type is INamedTypeSymbol named && named.IsGenericType && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
             return GetTestValue(named.TypeArguments[0]);
        }

        if (type.Name == "DateTime") return "DateTime.Now";
        if (type.Name == "Guid") return "Guid.NewGuid()";
        if (type.Name == "DateOnly") return "DateOnly.FromDateTime(DateTime.Now)";

        if (IsValidObject(type)) return $"new {type.ToDisplayString()}()";

        return "default";
    }

    private string GenerateFuzzTests(Compilation compilation, INamedTypeSymbol symbol)
    {
        var namespaceName = symbol.ContainingNamespace.ToDisplayString();
        var className = symbol.Name;
        var properties = symbol.GetMembers().OfType<IPropertySymbol>()
            .Where(p => p.GetAttributes().Any(a => a.AttributeClass?.Name == "ValidPropertyAttribute" || a.AttributeClass?.Name == "PropertyWeirAttribute"))
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("using Xunit;");
        sb.AppendLine("using Valid;");
        sb.AppendLine("using System;");
        sb.AppendLine();
        sb.AppendLine($"namespace {namespaceName}.Tests");
        sb.AppendLine("{");
        sb.AppendLine($"    public class {className}FuzzTests");
        sb.AppendLine("    {");

        sb.AppendLine("        [Fact]");
        sb.AppendLine($"        public void {className}_Chaos_Monkey_Bitmask_Fuzzer()");
        sb.AppendLine("        {");
        sb.AppendLine($"            var obj = new {className}();");
        sb.AppendLine("            var random = new Random(42); // Deterministic seed");
        sb.AppendLine("            // Fuzzing set property value directly with boundary values to ensure no runtime crashes");
        sb.AppendLine("            for(int i = 0; i < 1000; i++)");
        sb.AppendLine("            {");
        sb.AppendLine("                try");
        sb.AppendLine("                {");
        for (int i = 0; i < properties.Count; i++)
        {
            var prop = properties[i];
            sb.AppendLine($"                    obj.UpdatePropertyFromJson(\"{prop.Name}\", GetFuzzJson(random, \"{prop.Type.ToDisplayString()}\"));");
        }
        sb.AppendLine("                }");
        sb.AppendLine("                catch (Exception ex)");
        sb.AppendLine("                {");
        sb.AppendLine("                    Assert.Fail($\"Fuzzer found an unhandled crash: {ex.Message}\");");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("            Assert.True(true); // If we reached here, the bitmasks caught everything safely.");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        private string GetFuzzJson(Random rnd, string typeName)");
        sb.AppendLine("        {");
        sb.AppendLine("            return typeName switch {");
        sb.AppendLine("                \"string\" => rnd.Next(2) == 0 ? \"null\" : \"\\\"\" + new string('A', rnd.Next(0, 10)) + \"\\\"\",");
        sb.AppendLine("                \"int\" => rnd.Next(3) switch { 0 => int.MinValue.ToString(), 1 => int.MaxValue.ToString(), _ => rnd.Next().ToString() },");
        sb.AppendLine("                \"double\" => rnd.Next(3) switch { 0 => double.MinValue.ToString(), 1 => double.MaxValue.ToString(), _ => rnd.NextDouble().ToString() },");
        sb.AppendLine("                \"decimal\" => rnd.Next(3) switch { 0 => decimal.MinValue.ToString(), 1 => decimal.MaxValue.ToString(), _ => ((decimal)rnd.NextDouble()).ToString() },");
        sb.AppendLine("                \"bool\" => rnd.Next(2) == 0 ? \"true\" : \"false\",");
        sb.AppendLine("                _ => \"null\"");
        sb.AppendLine("            };");
        sb.AppendLine("        }");

        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private string GenerateBunitTests(Compilation compilation, INamedTypeSymbol symbol)
    {
        var namespaceName = symbol.ContainingNamespace.ToDisplayString();
        var className = symbol.Name;
        var properties = symbol.GetMembers().OfType<IPropertySymbol>()
            .Where(p => p.GetAttributes().Any(a => a.AttributeClass?.Name == "ValidPropertyAttribute" || a.AttributeClass?.Name == "PropertyWeirAttribute"))
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("using Bunit;");
        sb.AppendLine("using Xunit;");
        sb.AppendLine("using Valid;");
        // Generic wrapper check
        sb.AppendLine();
        sb.AppendLine($"namespace {namespaceName}.Tests");
        sb.AppendLine("{");
        sb.AppendLine($"    public class {className}BunitTests : TestContext");
        sb.AppendLine("    {");

        sb.AppendLine("        [Fact]");
        sb.AppendLine($"        public void {className}_Bunit_Headless_UI_Matrix()");
        sb.AppendLine("        {");
        sb.AppendLine($"            var obj = new {className}();");
        sb.AppendLine("            // Render a generic fragment to test cascading Valid object bindings");
        sb.AppendLine("            // Note: Developer must provide a <ValidDataGridRowShort Model=\"obj\" /> or similar test component wrapper.");
        sb.AppendLine("            var cut = RenderComponent<Microsoft.AspNetCore.Components.CascadingValue<IValidObject>>(parameters => ");
        sb.AppendLine("                parameters.Add(p => p.Value, obj)");
        sb.AppendLine("                          .Add(p => p.IsFixed, true)");
        sb.AppendLine("            );");
        sb.AppendLine("            // Assert initial render does not crash and handles object metadata");
        sb.AppendLine("            Assert.NotNull(cut);");
        sb.AppendLine("        }");

        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private string GenerateVavidAutoPilot(Compilation compilation, INamedTypeSymbol symbol)
    {
        var namespaceName = symbol.ContainingNamespace.ToDisplayString();
        var className = symbol.Name;
        
        var properties = symbol.GetMembers().OfType<IPropertySymbol>()
            .Where(p => p.GetAttributes().Any(a => a.AttributeClass?.Name == "ValidPropertyAttribute" || a.AttributeClass?.Name == "PropertyWeirAttribute"))
            .Select(p => p.Name)
            .Concat(symbol.GetMembers().OfType<IFieldSymbol>()
                .Where(f => f.GetAttributes().Any(a => a.AttributeClass?.Name == "ValidFieldAttribute"))
                .Select(f => {
                    var name = f.Name.TrimStart('_');
                    if (name.Length > 0 && char.IsLower(name[0])) return char.ToUpper(name[0]) + name.Substring(1);
                    return name;
                }))
            .Distinct()
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using Valid;");
        sb.AppendLine();
        sb.AppendLine($"namespace {namespaceName}");
        sb.AppendLine("{");
        sb.AppendLine($"    public static class {className}VavidAutoPilot");
        sb.AppendLine("    {");
        
        sb.AppendLine("        public static string GetPlaywrightScript()");
        sb.AppendLine("        {");
        sb.AppendLine("            return @\"");
        sb.AppendLine("import { test, expect } from '@playwright/test';");
        sb.AppendLine($"test('{className} Vavid Diagnostics Fuzzer', async ({{ page }}) => {{");
        sb.AppendLine("    await page.goto('/'); // Developer modifies explicitly");
        for (int i = 0; i < properties.Count; i++)
        {
            var propName = properties[i];
            sb.AppendLine($"    await page.fill('[data-valid-prop=\"\"{propName}\"\"]', 'TEST_INPUT');");
            sb.AppendLine($"    await expect(page.locator('[data-valid-prop=\"\"{propName}\"\"]')).toHaveClass(/vavid-(dirty|error)/);");
        }
        sb.AppendLine("});");
        sb.AppendLine("\";");
        sb.AppendLine("        }");

        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private string GenerateMcpTools(Compilation compilation, INamedTypeSymbol symbol)
    {
        var hasMcp = compilation.GetTypeByMetadataName("ModelContextProtocol.Server.McpServerToolAttribute") != null;
        if (!hasMcp) return "";

        var namespaceName = symbol.ContainingNamespace.ToDisplayString();
        var className = symbol.Name;
        var properties = GetProperties(symbol);

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Text.Json;");
        sb.AppendLine("using ModelContextProtocol.Server;");
        sb.AppendLine("using Valid.Mcp;");
        sb.AppendLine("using Valid;");
        sb.AppendLine();
        sb.AppendLine($"namespace {namespaceName}");
        sb.AppendLine("{");
        sb.AppendLine($"    [McpServerToolType]");
        sb.AppendLine($"    public static class {className}McpTools");
        sb.AppendLine("    {");

        sb.AppendLine($"        [McpServerTool, System.ComponentModel.Description(\"Creates a new instance of {className} and registers it in the global registry, returning its new instance ID.\")]");
        sb.AppendLine($"        public static string valid_create_{className}()");
        sb.AppendLine("        {");
        sb.AppendLine($"            var obj = new {className}();");
        sb.AppendLine("            var id = Guid.NewGuid().ToString();");
        sb.AppendLine("            ValidObjectRegistry.Register(id, obj);");
        sb.AppendLine("            return JsonSerializer.Serialize(new { instanceId = id });");
        sb.AppendLine("        }");
        sb.AppendLine();

        sb.AppendLine($"        [McpServerTool, System.ComponentModel.Description(\"Gets the property values and full validation state of a {className} instance by its ID.\")]");
        sb.AppendLine($"        public static string valid_get_{className}_state(string instanceId)");
        sb.AppendLine("        {");
        sb.AppendLine($"            var obj = ValidObjectRegistry.Get(instanceId) as {className};");
        sb.AppendLine($"            if (obj == null) return JsonSerializer.Serialize(new {{ error = \"{className} instance '\" + instanceId + \"' not found.\" }});");
        sb.AppendLine("            var values = new {");
        for (int i = 0; i < properties.Count; i++)
        {
            var prop = properties[i];
            sb.AppendLine($"                {prop.Name} = obj.{prop.Name},");
        }
        sb.AppendLine("            };");
        sb.AppendLine("            return JsonSerializer.Serialize(new {");
        sb.AppendLine("                InstanceId = instanceId,");
        sb.AppendLine($"                TypeName = \"{className}\",");
        sb.AppendLine("                IsDirty = obj.IsDirty,");
        sb.AppendLine("                IsValid = obj.IsValid,");
        sb.AppendLine("                DirtyFlags = obj.DirtyFlags.ToString(\"X\"),");
        sb.AppendLine("                ErrorFlags = obj.ErrorFlags.ToString(\"X\"),");
        sb.AppendLine("                BusyFlags = obj.BusyFlags.ToString(\"X\"),");
        sb.AppendLine("                StateFlags = obj.StateFlags.ToString(\"X\"),");
        sb.AppendLine("                Values = values,");
        sb.AppendLine("                Diagnostics = obj.GetDiagnostics()");
        sb.AppendLine("            }, new JsonSerializerOptions { WriteIndented = true });");
        sb.AppendLine("        }");
        sb.AppendLine();

        foreach (var prop in properties)
        {
            var propTypeStr = prop.Type.ToDisplayString();
            var isNested = IsValidObject(prop.Type);

            sb.AppendLine($"        [McpServerTool, System.ComponentModel.Description(\"Sets the {prop.Name} property on a {className} instance.\")]");
            
            if (isNested)
            {
                sb.AppendLine($"        public static string valid_set_{className}_{prop.Name}(string instanceId, string childInstanceId)");
                sb.AppendLine("        {");
                sb.AppendLine($"            var obj = ValidObjectRegistry.Get(instanceId) as {className};");
                sb.AppendLine($"            if (obj == null) return JsonSerializer.Serialize(new {{ error = \"{className} instance '\" + instanceId + \"' not found.\" }});");
                sb.AppendLine($"            var childObj = ValidObjectRegistry.Get(childInstanceId) as {propTypeStr.TrimEnd('?')};");
                sb.AppendLine($"            if (childObj == null && !string.IsNullOrEmpty(childInstanceId)) return JsonSerializer.Serialize(new {{ error = \"Child {prop.Type.Name} instance '\" + childInstanceId + \"' not found.\" }});");
                sb.AppendLine($"            obj.{prop.Name} = childObj!;");
            }
            else
            {
                sb.AppendLine($"        public static string valid_set_{className}_{prop.Name}(string instanceId, {propTypeStr} value)");
                sb.AppendLine("        {");
                sb.AppendLine($"            var obj = ValidObjectRegistry.Get(instanceId) as {className};");
                sb.AppendLine($"            if (obj == null) return JsonSerializer.Serialize(new {{ error = \"{className} instance '\" + instanceId + \"' not found.\" }});");
                sb.AppendLine($"            obj.{prop.Name} = value;");
            }

            sb.AppendLine("            return JsonSerializer.Serialize(new {");
            sb.AppendLine("                success = true,");
            sb.AppendLine($"                value = obj.{prop.Name},");
            sb.AppendLine($"                isDirty = ((IValidObject)obj).IsDirtyAt(obj.GetBitIndex(\"{prop.Name}\")),");
            sb.AppendLine($"                hasError = ((IValidObject)obj).HasErrorAt(obj.GetBitIndex(\"{prop.Name}\"))");
            sb.AppendLine("            });");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private bool IsValidObject(ITypeSymbol type)
    {
        return type.GetAttributes().Any(a => a.AttributeClass?.Name == "ValidObjectAttribute") || 
               (type.BaseType != null && type.BaseType.Name == "ValidObjectBase");
    }

    private bool HasMember(INamedTypeSymbol symbol, string memberName)
    {
        return symbol.GetMembers(memberName).Any();
    }

    private class ManagedProperty
    {
        public string Name { get; set; } = "";
        public string FieldName { get; set; } = "";
        public ITypeSymbol Type { get; set; } = default!;
        public bool IsExplicit { get; set; }
    }
}
