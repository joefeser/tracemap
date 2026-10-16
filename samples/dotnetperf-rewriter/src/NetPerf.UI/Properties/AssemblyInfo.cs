using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using PostSharp.Extensibility;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("Netperf.UI")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]

[assembly: InternalsVisibleTo("NetPerf.UI.Tests.Unit, PublicKey="
    + "0024000004800000940000000602000000240000525341310004000001000100b3dd4b312a1d84"
    + "d1babcf4d1b1885b6d3a6b17eea865e5de17610fcaf7ea3bf44f1b064dc6cad8a93b33c163d18c"
    + "34c130151f4e205b6ec11ca2573b784b6111c38ae16f5f432eced2834eeacc66f2c45f78b2b802"
    + "b466d0abab7927e7b1b9fa40b9223b6d58eba3e9d024f9594ef387a71268d7a79e1c098723d224"
    + "42387fd3"
    )]

//In order to begin building localizable applications, set 
//<UICulture>CultureYouAreCodingWith</UICulture> in your .csproj file
//inside a <PropertyGroup>.  For example, if you are using US english
//in your source files, set the <UICulture> to en-US.  Then uncomment
//the NeutralResourceLanguage attribute below.  Update the "en-US" in
//the line below to match the UICulture setting in the project file.

//[assembly: NeutralResourcesLanguage("en-US" , UltimateResourceFallbackLocation.MainAssembly)]

[assembly:ThemeInfo(
    ResourceDictionaryLocation.None, //where theme specific resource dictionaries are located
                             //(used if a resource is not found in the page, 
                             // or application resource dictionaries)
    ResourceDictionaryLocation.SourceAssembly //where the generic resource dictionary is located
                             //(used if a resource is not found in the page, 
                             // app, or any theme specific resource dictionaries)
)]

[assembly: MetadataDashboard.NetperfUI.Infrastructure.Aspects.CaliburnPropertyChangedAspect(
        AttributeTargetTypes = "*", // apply to all classes, depend on the filter within the aspect to filter out the unsuitable classes/properties
        AttributeTargetTypeAttributes = MulticastAttributes.AnyVisibility
)]
