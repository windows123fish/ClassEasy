using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;

#if NIX
[assembly: AssemblyVersion("0.0.0.0")]
[assembly: AssemblyInformationalVersion("NIXBUILD+NIXBUILD_LONG_HASH")]
#else
[assembly: AssemblyVersion("2.0.0.0")]
[assembly: AssemblyInformationalVersion("2.0.0+classeasy")]
#endif

[assembly: AssemblyTitle("ClassEasy")]
[assembly: AssemblyProduct("ClassEasy")]
[assembly: InternalsVisibleTo("ClassIsland.Desktop")]
[assembly: InternalsVisibleTo("ClassIsland")]
[assembly: InternalsVisibleTo("ClassIsland.Core")]
[assembly: InternalsVisibleTo("ClassIsland.Platforms.Windows")]
#if NETCOREAPP
// [assembly: SupportedOSPlatform("Windows")]
#endif
#if Platforms_MacOs
[assembly:SupportedOSPlatform("macos")]
#endif
