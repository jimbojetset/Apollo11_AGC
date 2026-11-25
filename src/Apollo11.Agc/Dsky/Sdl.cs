using System.Reflection;
using System.Runtime.InteropServices;

namespace Apollo11.Agc.Dsky;

internal static class Sdl
{
    private const string Library = "SDL2";

    static Sdl() => NativeLibrary.SetDllImportResolver(typeof(Sdl).Assembly, Resolve);

    private static nint Resolve(string name, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (name != Library)
            return 0;

        string[] names = OperatingSystem.IsMacOS()
            ? ["libSDL2.dylib", "/opt/homebrew/lib/libSDL2.dylib", "/usr/local/lib/libSDL2.dylib"]
            : OperatingSystem.IsWindows() ? ["SDL2.dll"] : ["libSDL2-2.0.so.0", "libSDL2.so"];
        foreach (var candidate in names)
            if (NativeLibrary.TryLoad(candidate, assembly, searchPath, out var handle))
                return handle;
        return 0;
    }

    public const uint QuitEvent = 0x100;
    public const uint WindowEvent = 0x200;
    public const uint KeyDown = 0x300;
    public const uint KeyUp = 0x301;
    public const uint MouseDown = 0x401;
    public const uint MouseUp = 0x402;
    public const byte FocusLost = 13;
    public const byte WindowClose = 14;
    public const uint Video = 0x20;
    public const uint WindowResizable = 0x00000020;
    public const uint WindowHighDpi = 0x00002000;
    public const int WindowCentered = 0x2fff0000;
    public const uint Accelerated = 2;
    public const uint Software = 1;
    public const uint Argb8888 = 0x16362004;
    public const int TargetTexture = 2;
    public const int Linear = 1;

    [StructLayout(LayoutKind.Sequential)]
    public readonly record struct Rect(int X, int Y, int W, int H)
    {
        public bool Contains(int x, int y)
        {
            return x >= X && x < X + W && y >= Y && y < Y + H;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly record struct Point(float X, float Y);

    [StructLayout(LayoutKind.Sequential)]
    public readonly record struct Vertex(Point Position, byte R, byte G, byte B, byte A, Point Texture);

    [StructLayout(LayoutKind.Explicit, Size = 56)]
    public struct Event
    {
        [FieldOffset(0)] public uint Type;
        [FieldOffset(12)] public byte WindowKind;
        [FieldOffset(13)] public byte KeyRepeat;
        [FieldOffset(16)] public byte MouseButton;
        [FieldOffset(16)] public int ScanCode;
        [FieldOffset(20)] public int KeyCode;
        [FieldOffset(20)] public int MouseX;
        [FieldOffset(24)] public int MouseY;
    }

    public static string Error => Marshal.PtrToStringUTF8(SDL_GetErrorNative()) ?? "Unknown SDL error";

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int SDL_Init(uint flags);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void SDL_Quit();
    [DllImport(Library, EntryPoint = "SDL_GetError", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern nint SDL_GetErrorNative();
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern nint SDL_CreateWindow(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string title, int x, int y, int w, int h, uint flags);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void SDL_DestroyWindow(nint window);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern nint SDL_CreateRenderer(nint window, int index, uint flags);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void SDL_DestroyRenderer(nint renderer);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int SDL_RenderSetLogicalSize(nint renderer, int w, int h);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int SDL_SetRenderDrawColor(nint renderer, byte r, byte g, byte b, byte a);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int SDL_RenderClear(nint renderer);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int SDL_RenderFillRect(nint renderer, in Rect rect);
    // SDL consumes the two contiguous buffers during this call.
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int SDL_RenderGeometry(
        nint renderer, nint texture, in Vertex firstVertex, int vertexCount, in int firstIndex, int indexCount);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern nint SDL_CreateTexture(nint renderer, uint format, int access, int w, int h);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void SDL_DestroyTexture(nint texture);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int SDL_SetRenderTarget(nint renderer, nint texture);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int SDL_RenderSetScale(nint renderer, float scaleX, float scaleY);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int SDL_SetTextureScaleMode(nint texture, int scaleMode);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int SDL_RenderCopy(nint renderer, nint texture, nint source, nint destination);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void SDL_RenderPresent(nint renderer);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int SDL_PollEvent(out Event input);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void SDL_Delay(uint milliseconds);
}
