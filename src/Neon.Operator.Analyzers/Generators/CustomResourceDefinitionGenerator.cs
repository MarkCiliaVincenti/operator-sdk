using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Serialization;

using k8s;
using k8s.Models;

using Microsoft.CodeAnalysis;

using Neon.Common;
using Neon.Operator.Analyzers.Receivers;
using Neon.Operator.Attributes;
using Neon.Operator.Webhooks;
using Neon.Roslyn;

using MetadataLoadContext = Neon.Roslyn.MetadataLoadContext;

namespace Neon.Operator.Analyzers
{
    [Generator]
    public class CustomResourceDefinitionGenerator : ISourceGenerator
    {
        private static readonly DiagnosticDescriptor TooManyStorageVersionsError = new DiagnosticDescriptor(id: "NO10001",
                                                                                              title: "One and only one version must be marked as the storage version",
                                                                                              messageFormat: "'{0}' has {1} versions marked for storage",
                                                                                              category: "NeonOperatorSdk",
                                                                                              DiagnosticSeverity.Error,
                                                                                              isEnabledByDefault: true);

        private static readonly string[] IgnoredProperties = { "metadata", "apiversion", "kind" };

        private Dictionary<string, StringBuilder> logs;
        public void Initialize(GeneratorInitializationContext context)
        {
            //System.Diagnostics.Debugger.Launch();
            AppDomain.CurrentDomain.AssemblyResolve += OnResolveAssembly;

            context.RegisterForSyntaxNotifications(() => new CustomResourceReceiver());
        }

        public Assembly OnResolveAssembly(object sender, ResolveEventArgs args)
        {
            var assemblyName = new AssemblyName(args.Name);
            Assembly assembly = null;
            try
            {
                var runtimeDependencies = Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll");
                var targetAssembly = runtimeDependencies
                    .FirstOrDefault(ass => Path.GetFileNameWithoutExtension(ass).Equals(assemblyName.Name, StringComparison.InvariantCultureIgnoreCase));

                if (!String.IsNullOrEmpty(targetAssembly))
                    assembly = Assembly.LoadFrom(targetAssembly);
            }
            catch (Exception)
            {
            }
            return assembly;
        }

        public void Execute(GeneratorExecutionContext context)
        {
            //System.Diagnostics.Debugger.Launch();
            logs = new Dictionary<string, StringBuilder>();

            if (context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.NeonOperatorGenerateCrds", out var generateCrds))
            {
                if (bool.TryParse(generateCrds, out bool generateCrdsBool))
                {
                    if (!generateCrdsBool)
                    {
                        return;
                    }
                }
            }

            string crdOutputDirectory = null;
            if (context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.MSBuildProjectDirectory", out var projectDirectory))
            {
                crdOutputDirectory = projectDirectory;
            }
            else if (context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.projectdir", out var projectDir))
            {
                crdOutputDirectory = projectDir;
            }

            if (context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.NeonOperatorManifestOutputDir", out var manifestOutDir))
            {
                if (!string.IsNullOrEmpty(manifestOutDir))
                {
                    if (Path.IsPathRooted(manifestOutDir))
                    {
                        crdOutputDirectory = manifestOutDir;
                    }
                    else
                    {
                        crdOutputDirectory = Path.Combine(projectDirectory, manifestOutDir);
                    }
                }
            }

            if (context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.NeonOperatorCrdOutputDir", out var crdOutDir))
            {
                crdOutputDirectory = crdOutDir;

                if (!string.IsNullOrEmpty(crdOutDir))
                {
                    if (Path.IsPathRooted(crdOutDir))
                    {
                        crdOutputDirectory = crdOutDir;
                    }
                    else
                    {
                        crdOutputDirectory = Path.Combine(projectDirectory, crdOutDir);
                    }
                }
            }

            if (crdOutputDirectory == null)
            {
                throw new Exception("CRD output directory not defined.");
            }

            try
            {
                Directory.CreateDirectory(crdOutputDirectory);

                var metadataLoadContext       = new MetadataLoadContext(context.Compilation);
                var customResources           = ((CustomResourceReceiver)context.SyntaxReceiver)?.ClassesToRegister;
                var namedTypeSymbols          = context.Compilation.GetNamedTypeSymbols();
                var customResourceDefinitions = new Dictionary<string, V1CustomResourceDefinition>();

                foreach (var cr in customResources)
                {
                    try
                    {
                        var crTypeIdentifier     = namedTypeSymbols.Where(ntm => ntm.MetadataName == cr.Identifier.ValueText).SingleOrDefault();
                        var crFullyQualifiedName = crTypeIdentifier.ToDisplayString(DisplayFormat.NameAndContainingTypesAndNamespaces);
                        var fn                   = crTypeIdentifier.GetFullMetadataName();

                        if (crFullyQualifiedName.StartsWith("k8s."))
                        {

                        }
                        var desc     = crTypeIdentifier.GetDocumentationCommentXml();

                        var crSystemType = metadataLoadContext.ResolveType(crTypeIdentifier);
                        // var description = crSystemType.GetXmlDocsElement();

                        try
                        {
                            if (crSystemType.GetCustomAttribute<IgnoreAttribute>() != null)
                            {
                                continue;
                            }
                        }
                        catch
                        {
                            // not ignoring
                        }

                        var k8sAttr     = crSystemType.GetCustomAttribute<KubernetesEntityAttribute>();
                        var versionAttr = crSystemType.GetCustomAttribute<EntityVersionAttribute>();
                        var scaleAttr   = crSystemType.GetCustomAttribute<ScaleAttribute>();
                        var scopeAttr   = crSystemType.GetCustomAttribute<EntityScopeAttribute>();
                        var shortNames  = crSystemType.GetCustomAttributes<ShortNameAttribute>()?.Select(sn => sn.Name).Distinct().ToList();

                        var scope = EntityScope.Namespaced;
                        if (scopeAttr != null)
                        {
                            scope = scopeAttr.Scope;
                        }
                        var additionalPrinterColumns = new List<V1CustomResourceColumnDefinition>();

                        var schema           = new V1CustomResourceValidation(MapType(namedTypeSymbols, crSystemType, additionalPrinterColumns, string.Empty));
                        var pluralNameGroup  = string.IsNullOrEmpty(k8sAttr.Group) ? k8sAttr.PluralName : $"{k8sAttr.PluralName}.{k8sAttr.Group}";
                        var implementsStatus = crSystemType.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition().Equals(typeof(IStatus<> )));

                        var version = new V1CustomResourceDefinitionVersion(
                        name:         k8sAttr.ApiVersion,
                        served:       versionAttr?.Served ?? true,
                        storage:      versionAttr?.Storage ?? true,
                        schema:       schema,
                        subresources: new V1CustomResourceSubresources()
                        {
                            Status = implementsStatus ? new object() : null,
                            Scale  = scaleAttr != null
                                ? new V1CustomResourceSubresourceScale()
                                {
                                    LabelSelectorPath  = scaleAttr.LabelSelectorPath,
                                    SpecReplicasPath   = scaleAttr.SpecReplicasPath,
                                    StatusReplicasPath = scaleAttr.StatusReplicasPath,
                                }
                            : null
                        },
                        additionalPrinterColumns: additionalPrinterColumns);

                        if (customResourceDefinitions.ContainsKey(pluralNameGroup))
                        {
                            customResourceDefinitions[pluralNameGroup].Spec.Versions.Add(version);

                            if (version.Storage)
                            {
                                customResourceDefinitions[pluralNameGroup].Spec.Names.Singular = k8sAttr.Kind.ToLowerInvariant();
                            }

                            customResourceDefinitions[pluralNameGroup].Spec.Names.ShortNames = customResourceDefinitions[pluralNameGroup].Spec.Names.ShortNames.Union(shortNames).ToList();
                        }
                        else
                        {
                            var crd = new V1CustomResourceDefinition(
                                apiVersion: $"{V1CustomResourceDefinition.KubeGroup}/{V1CustomResourceDefinition.KubeApiVersion}",
                                kind:       V1CustomResourceDefinition.KubeKind,
                                metadata:   new V1ObjectMeta(name: pluralNameGroup),
                                spec:       new V1CustomResourceDefinitionSpec(
                                group:      k8sAttr.Group,
                                names:      new V1CustomResourceDefinitionNames(
                                kind:       k8sAttr.Kind,
                                plural:     k8sAttr.PluralName,
                                singular:   k8sAttr.Kind.ToLowerInvariant(),
                                shortNames: shortNames),
                                scope:      scope.ToMemberString(),
                                versions:   new List<V1CustomResourceDefinitionVersion>
                                {
                                    version,
                                }));

                            customResourceDefinitions.Add(pluralNameGroup, crd);
                        }
                    }
                    catch (Exception e)
                    {
                        Log(context, e);
                    }
                }

                foreach (var crd in customResourceDefinitions)
                {
                    try
                    {
                        if (crd.Value.Spec.Versions.Where(v => v.Storage).Count() > 1)
                        {
                            context.ReportDiagnostic(
                                Diagnostic.Create(TooManyStorageVersionsError,
                                Location.None,
                                crd.Value.Name(),
                                crd.Value.Spec.Versions.Where(v => v.Storage).Count().ToString()));
                        }
                        else
                        {
                            var yaml = KubernetesYaml.Serialize(crd.Value);

                            var outputPath = Path.Combine(crdOutputDirectory, crd.Value.Name() + ".yaml");

                            if (!File.Exists(outputPath) || File.ReadAllText(outputPath) != yaml)
                            {
                                File.WriteAllText(outputPath, yaml);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Log(context, e);
                    }
                }

            }
            catch (Exception e)
            {
                Log(context, e);
            }

            if (context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.NeonOperatorAnalyzerLoggingEnabled", out var logEnabledString))
            {
                if (bool.TryParse(logEnabledString, out var logEnabledbool) == true)
                {
                    if (!logs.ContainsKey(context.Compilation.AssemblyName))
                    {
                        return;
                    }

                    var log                = logs[context.Compilation.AssemblyName];
                    var logOutputDirectory = projectDirectory;

                    if (context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.NeonOperatorAnalyzerLoggingDir", out var logsOutDir))
                    {
                        if (!string.IsNullOrEmpty(logsOutDir))
                        {
                            if (Path.IsPathRooted(logsOutDir))
                            {
                                logOutputDirectory = Path.Combine(logsOutDir, nameof(CustomResourceDefinitionGenerator));
                            }
                            else
                            {
                                logOutputDirectory = Path.Combine(projectDirectory, logsOutDir, nameof(CustomResourceDefinitionGenerator));
                            }
                        }
                    }

                    Directory.CreateDirectory(logOutputDirectory);

                    File.WriteAllText(Path.Combine(logOutputDirectory, $"{context.Compilation.AssemblyName}.log"), log.ToString());
                }
            }
        }

        private V1JSONSchemaProps MapProperty(
            IEnumerable<INamedTypeSymbol>           namedTypeSymbols,
            PropertyInfo                            info,
            IList<V1CustomResourceColumnDefinition> additionalColumns,
            string                                  jsonPath)
        {
            V1JSONSchemaProps props = null;
            try
            {
                props = MapType(namedTypeSymbols, info.PropertyType, additionalColumns, jsonPath);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }

            //if (props.Description == null)
            //{
            //    props.Description = info.GetPropertySymbol().GetSummary();
            //}

            // get additional printer column information
            var additionalColumn = info.GetCustomAttribute<AdditionalPrinterColumnAttribute>();
            if (additionalColumn != null)
            {
                additionalColumns.Add(
                    new V1CustomResourceColumnDefinition
                    {
                        Name        = additionalColumn.Name ?? info.Name,
                        Description = props.Description,
                        JsonPath    = jsonPath,
                        Priority    = additionalColumn.Priority,
                        Type        = props.Type,
                        Format      = props.Format,
                    });
            }

            var patternAttribute = info.GetCustomAttribute<PatternAttribute>();
            if (patternAttribute != null)
            {
                props.Pattern = patternAttribute.Pattern;
            }

            if (info.GetCustomAttribute<PreserveUnknownFieldsAttribute>() != null)
            {
                props.XKubernetesPreserveUnknownFields = true;
            }

            var rangeAttribute = info.GetCustomAttribute<Attributes.RangeAttribute>();
            if (rangeAttribute != null)
            {
                props.Minimum          = rangeAttribute.minimum;
                props.Maximum          = rangeAttribute.maximum;
                props.ExclusiveMinimum = rangeAttribute.exclusiveMinimum;
                props.ExclusiveMaximum = rangeAttribute.exclusiveMaximum;
            }

            return props;
        }

        private V1JSONSchemaProps MapType(
            IEnumerable<INamedTypeSymbol>           namedTypeSymbols,
            Type                                    type,
            IList<V1CustomResourceColumnDefinition> additionalColumns,
            string                                  jsonPath)
        {
            var typeSymbol = namedTypeSymbols.Where(nts => nts.GetFullMetadataName() == type.FullName).FirstOrDefault();
            
            var props = new V1JSONSchemaProps();

            var interfaces = type.GetInterfaces();

            if (type.Equals(typeof(V1ObjectMeta)))
            {
                props.Type = Constants.ObjectTypeString;
            }
            else if (type.IsGenericType && type.GetGenericTypeDefinition().Equals(typeof(IDictionary<,>)))
            {
                props.Type = Constants.ObjectTypeString;
                props.AdditionalProperties = MapType(namedTypeSymbols, type.GenericTypeArguments[1], additionalColumns, jsonPath);
            }
            else if (type.IsGenericType &&
                type.GetGenericTypeDefinition().Equals(typeof(IEnumerable<>)) &&
                type.GenericTypeArguments.Length == 1 &&
                type.GenericTypeArguments.Single().IsGenericType &&
                type.GenericTypeArguments.Single().GetGenericTypeDefinition().Equals(typeof(KeyValuePair<,>)))
            {
                props.Type = Constants.ObjectTypeString;
                props.AdditionalProperties = MapType(namedTypeSymbols, type.GenericTypeArguments.Single().GenericTypeArguments[1], additionalColumns, jsonPath);
            }
            else if (type.IsGenericType && type.IsEnumerableType(out Type typeParameter))
            {
                props.Type = Constants.ArrayTypeString;
                props.Items = MapType(namedTypeSymbols, typeParameter, additionalColumns, jsonPath);
            }

            else if (typeof(IKubernetesObject).IsAssignableFrom(type) &&
                !type.IsAbstract &&
                !type.IsInterface &&
                type.Assembly == typeof(IKubernetesObject).Assembly)
            {
                SetEmbeddedResourceProperties(props);
            }
            else if (type.IsArray)
            {
                props.Type = Constants.ArrayTypeString;
                props.Items = MapType(
                    namedTypeSymbols,
                    type.GetElementType() ?? throw new NullReferenceException("No Array Element Type found"),
                    additionalColumns,
                    jsonPath);
                //props.Description ??= typeSymbol.GetSummary();
            }
            else if (type.Equals(typeof(IntstrIntOrString)))
            {
                props.XKubernetesIntOrString = true;
            }
            else if (type.Equals(typeof(int)) | type.Equals(typeof(int?)))
            {
                props.Type   = Constants.IntegerTypeString;
                props.Format = Constants.Int32TypeString;
            }
            else if (type.Equals(typeof(long)) | type.Equals(typeof(long?)))
            {
                props.Type   = Constants.IntegerTypeString;
                props.Format = Constants.Int64TypeString;
            }
            else if (type.Equals(typeof(float)) | type.Equals(typeof(float?)))
            {
                props.Type   = Constants.NumberTypeString;
                props.Format = Constants.FloatTypeString;
            }
            else if (type.Equals(typeof(double)) | type.Equals(typeof(double?)))
            {
                props.Type   = Constants.NumberTypeString;
                props.Format = Constants.DoubleTypeString;
            }
            else if (type.Equals(typeof(string)))
            {
                props.Type = Constants.StringTypeString;
            }
            else if (type.Equals(typeof(bool)) | type.Equals(typeof(bool?)))
            {
                props.Type = Constants.BooleanTypeString;
            }
            else if (type.Equals(typeof(DateTime)) | type.Equals(typeof(DateTime?)))
            {
                props.Type   = Constants.StringTypeString;
                props.Format = Constants.DateTimeTypeString;
            }
            else if (type.IsEnum)
            {
                props.Type = Constants.StringTypeString;
                //props.EnumProperty = new List<object>(Enum.GetNames(type));
            }
            else if (Nullable.GetUnderlyingType(type)?.IsEnum == true)
            {
                props.Type = Constants.StringTypeString;
                //props.EnumProperty = new List<object>(Enum.GetNames(Nullable.GetUnderlyingType(type)!));
            }
            else 
            {
                props.Type = Constants.ObjectTypeString;

                props.Properties = new Dictionary<string, V1JSONSchemaProps>();

                foreach (var prop in type.GetProperties())
                {
                    if (//string.IsNullOrEmpty(jsonPath) ||
                        IgnoredProperties.Contains(prop.Name.ToLowerInvariant()))
                    {
                        continue;
                    }

                    props.Properties.Add(GetPropertyName(prop), MapProperty(namedTypeSymbols, prop, additionalColumns, $"{jsonPath}.{GetPropertyName(prop)}"));
                };

                props.Required = type.GetProperties()
                    .Where(prop => prop.GetCustomAttribute<RequiredAttribute>() != null)
                    .Select(GetPropertyName)
                    .ToList();
                if (props.Required.Count == 0)
                {
                    props.Required = null;
                }
                //props.Description ??= typeSymbol.GetSummary();
            }

            return props;
        }

        private static void SetEmbeddedResourceProperties(V1JSONSchemaProps props)
        {
            props.Type                             = Constants.ObjectTypeString;
            props.Properties                       = null;
            props.XKubernetesPreserveUnknownFields = true;
            props.XKubernetesEmbeddedResource      = true;
        }
        
        private static string GetPropertyName(PropertyInfo property)
        {
            var attribute    = property.GetCustomAttribute<JsonPropertyNameAttribute>();
            var propertyName = attribute?.Name ?? property.Name;

            return ToCamelCase(propertyName);
        }

        public static string ToCamelCase(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || char.IsLower(value[0]))
            {
                return value;
            }
            else
            {
                return char.ToLowerInvariant(value[0]) + value.Substring(1, value.Length - 1);
            }
        }

        private void Log(GeneratorExecutionContext context, string message)
        {
            if (!logs.ContainsKey(context.Compilation.AssemblyName))
            {
                logs[context.Compilation.AssemblyName] = new StringBuilder();
            }

            logs[context.Compilation.AssemblyName].AppendLine(message);
        }

        private void Log(GeneratorExecutionContext context, Exception e)
        {
            Log(context, e.Message);
            Log(context, e.StackTrace);
        }
    }
}
