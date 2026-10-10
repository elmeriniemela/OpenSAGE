using System.Numerics;
using System.Runtime.InteropServices;

namespace OpenSage.Audio;

internal static class OpenALListener
{
    // SharpAudio 1.0.27 passes ref float[] to alListenerfv, but OpenAL expects
    // a pointer to the values, not a pointer to the array pointer.
    internal static void SetOrientation(Vector3 up, Vector3 front)
    {
        SetListenerValues(0x100F, [front.X, front.Y, front.Z, up.X, up.Y, up.Z]);
    }

    [DllImport("libopenal.so.1", EntryPoint = "alListenerfv", CallingConvention = CallingConvention.Cdecl)]
    private static extern void SetListenerValues(int parameter, [In] float[] values);
}
