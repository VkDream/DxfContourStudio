#nullable enable

using System.Reflection;
using DxfContourStudio.Application.Documents;

namespace DxfContourStudio.Application.Tests;

/// <summary>
/// Guards the single-source versioning setup (ADR-006): the root
/// Directory.Build.props pins 0.2.0 across Version / AssemblyVersion /
/// FileVersion / InformationalVersion, and the shipped assemblies must agree
/// with it. Keeps a future accidental bump or split from going unnoticed.
/// </summary>
public class VersioningTests
{
    private const string Version5 = "0.2.0.0";
    private const string Version3 = "0.2.0";

    [Fact]
    public void ApplicationAssembly_AssemblyVersion_Is_0_2_0_0()
    {
        var assembly = typeof(CadDocument).Assembly;
        Assert.Equal(0, assembly.GetName().Version!.Major);
        Assert.Equal(2, assembly.GetName().Version!.Minor);
        Assert.Equal(0, assembly.GetName().Version!.Build);
        Assert.Equal(0, assembly.GetName().Version!.Revision);
        Assert.Equal(Version5, assembly.GetName().Version!.ToString());
    }

    [Fact]
    public void ApplicationAssembly_InformationalVersion_Is_0_2_0()
    {
        var informational = typeof(CadDocument).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion;
        Assert.StartsWith(Version3, informational);
    }

    [Fact]
    public void ApplicationAssembly_FileVersion_Is_0_2_0_0()
    {
        var fileVersion = typeof(CadDocument).Assembly
            .GetCustomAttribute<AssemblyFileVersionAttribute>()!;
        Assert.Equal(Version5, fileVersion.Version);
    }

    [Fact]
    public void CoreAssembly_MatchesSameVersion()
    {
        var core = Assembly.Load("DxfContourStudio.Core");
        // InformationalVersion may carry a CI SourceRevisionId suffix ("+<sha>"),
        // so assert the pinned version prefix only (same as the Application check above).
        Assert.StartsWith(Version3, core.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion);
        Assert.Equal(Version5, core.GetName().Version!.ToString());
    }
}
