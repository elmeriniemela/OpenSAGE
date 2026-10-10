using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace OpenSage.Graphics;

internal static class VulkanLibraryResolver
{
    static VulkanLibraryResolver()
    {
        if (OperatingSystem.IsLinux())
        {
            NativeLibrary.SetDllImportResolver(typeof(Vulkan.VulkanNative).Assembly, Resolve);
        }
    }

    // The explicit static constructor registers the resolver once, before Vulkan
    // initializes its native bindings, in both Debug and Release builds.
    public static void Initialize()
    {
    }

    internal static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        // Vk 1.0.25 imports "libdl", but glibc installations need not provide an
        // unversioned libdl.so. Use the runtime SONAME for this import only.
        return OperatingSystem.IsLinux() && libraryName == "libdl"
            ? NativeLibrary.Load("libdl.so.2", assembly, searchPath)
            : IntPtr.Zero;
    }
}
