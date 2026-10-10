using System;
using System.Reflection;
using OpenSage.Graphics;
using Xunit;

namespace OpenSage.Tests.Graphics;

public sealed class VulkanLibraryResolverTests
{
    [Fact]
    public void CanInvokeBindingsLibdlImportOnLinuxWithoutCreatingGraphicsDevice()
    {
        VulkanLibraryResolver.Initialize();
        VulkanLibraryResolver.Initialize();

        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        // Exercise the actual dependency's P/Invoke, without loading Vulkan or
        // requiring a GPU, display server or proprietary game assets.
        var assembly = typeof(Vulkan.VulkanNative).Assembly;
        var libdl = assembly.GetType("Vulkan.Libdl", throwOnError: true);
        var dlerror = libdl.GetMethod("dlerror", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(dlerror);
        var exception = Record.Exception(() => dlerror.Invoke(null, null));
        Assert.Null(exception);
    }

    [Theory]
    [InlineData("libvulkan.so.1")]
    [InlineData("kernel32")]
    public void LeavesOtherImportsToDefaultResolver(string libraryName)
    {
        Assert.Equal(IntPtr.Zero, VulkanLibraryResolver.Resolve(libraryName, typeof(Vulkan.VulkanNative).Assembly, null));
    }
}
