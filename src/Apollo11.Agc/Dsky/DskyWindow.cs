using System.Diagnostics;
using Apollo11.Agc.Hardware;
using static Apollo11.Agc.Dsky.Sdl;

namespace Apollo11.Agc.Dsky;

internal static class DskyWindow
{
    public static void Run(AgcCpu cpu)
    {
        if (SDL_Init(Video) != 0)
            throw new InvalidOperationException(Error);

        nint window = 0;
        nint renderer = 0;
        nint faceBuffer = 0;
        try
        {
            window = SDL_CreateWindow("Apollo 11 LM DSKY", WindowCentered, WindowCentered,
                DskyFace.Width, DskyFace.Height, WindowResizable | WindowHighDpi);
            if (window == 0)
                throw new InvalidOperationException(Error);

            renderer = SDL_CreateRenderer(window, -1, Accelerated);
            if (renderer == 0)
                renderer = SDL_CreateRenderer(window, -1, Software);
            if (renderer == 0 || SDL_RenderSetLogicalSize(renderer, DskyFace.Width, DskyFace.Height) != 0)
                throw new InvalidOperationException(Error);

            // Downsample the engraved strokes and electrode edges when presenting the face.
            faceBuffer = SDL_CreateTexture(renderer, Argb8888, TargetTexture, DskyFace.Width * 2, DskyFace.Height * 2);
            if (faceBuffer != 0)
                _ = SDL_SetTextureScaleMode(faceBuffer, Linear);
            RunPanel(cpu, new DskyFace(new SdlDskyDrawing(renderer)), renderer, faceBuffer);
        }
        finally
        {
            if (faceBuffer != 0)
                SDL_DestroyTexture(faceBuffer);
            if (renderer != 0)
                SDL_DestroyRenderer(renderer);
            if (window != 0)
                SDL_DestroyWindow(window);
            SDL_Quit();
        }
    }

    private static void RunPanel(AgcCpu cpu, DskyFace face, nint renderer, nint faceBuffer)
    {
        var panel = new DskyPanel(cpu);
        var keyboard = new Dictionary<int, DskyKey>();
        var keysToSend = new Queue<DskyKey>();
        DskyKey? mouseKey = null;
        var clock = Stopwatch.StartNew();
        var previousFrame = TimeSpan.Zero;
        var cycleTarget = (double)cpu.CycleCount;
        var nextKeyCycle = cpu.CycleCount;

        try
        {
            while (true)
            {
                var frame = clock.Elapsed;
                cycleTarget += Math.Min((frame - previousFrame).TotalSeconds, 0.1) * (1_024_000.0 / 12);
                previousFrame = frame;

                while (SDL_PollEvent(out var input) != 0)
                {
                    switch (input.Type)
                    {
                        case QuitEvent:
                        case WindowEvent when input.WindowKind == WindowClose:
                        case KeyDown when input.KeyCode == 27:
                            return;
                        case WindowEvent when input.WindowKind == FocusLost:
                            keyboard.Clear();
                            mouseKey = null;
                            keysToSend.Clear();
                            break;
                        case KeyDown when input.KeyRepeat == 0:
                            if (KeyboardKey(input.KeyCode) is { } key && keyboard.TryAdd(input.ScanCode, key))
                                QueueKey(key);
                            break;
                        case KeyUp:
                            _ = keyboard.Remove(input.ScanCode);
                            break;
                        case MouseDown when input.MouseButton == 1:
                            mouseKey = DskyFace.KeyAt(input.MouseX, input.MouseY);
                            if (mouseKey is { } clicked)
                                QueueKey(clicked);
                            break;
                        case MouseUp when input.MouseButton == 1:
                            mouseKey = null;
                            break;
                    }

                    if (mouseKey == DskyKey.Proceed || keyboard.ContainsValue(DskyKey.Proceed))
                        panel.Press(DskyKey.Proceed);
                    else
                        panel.Release(DskyKey.Proceed);
                }

                var remaining = (long)cycleTarget - cpu.CycleCount;
                while (remaining > 0)
                {
                    if (keysToSend.Count == 0)
                    {
                        cpu.Run(remaining);
                        break;
                    }
                    if (cpu.CycleCount >= nextKeyCycle)
                    {
                        panel.Press(keysToSend.Dequeue());
                        // Allow KEYRUPT to sample the latch before another host event replaces it.
                        nextKeyCycle = cpu.CycleCount + AgcCpu.MachineCyclesPerSecond / 50;
                    }
                    cpu.Run(Math.Min(remaining, nextKeyCycle - cpu.CycleCount));
                    remaining = (long)cycleTarget - cpu.CycleCount;
                }

                if (faceBuffer != 0 && SDL_SetRenderTarget(renderer, faceBuffer) == 0)
                {
                    _ = SDL_RenderSetScale(renderer, 2, 2);
                    face.Draw(panel, keyboard.Values, mouseKey);
                    _ = SDL_SetRenderTarget(renderer, 0);
                    _ = SDL_RenderClear(renderer);
                    _ = SDL_RenderCopy(renderer, faceBuffer, 0, 0);
                }
                else
                {
                    face.Draw(panel, keyboard.Values, mouseKey);
                }
                SDL_RenderPresent(renderer);
                var sleep = 16 - (clock.Elapsed - frame).TotalMilliseconds;
                if (sleep >= 1)
                    SDL_Delay((uint)sleep);
            }
        }
        finally
        {
            panel.Release(DskyKey.Proceed);
        }

        void QueueKey(DskyKey key)
        {
            if (key != DskyKey.Proceed)
                keysToSend.Enqueue(key);
        }
    }

    private static DskyKey? KeyboardKey(int code)
    {
        const int keypad = 1 << 30; // SDL keycodes for non-printable keys encode a scancode.
        if (code is >= '1' and <= '9')
            return (DskyKey)(code - '0');
        if (code is >= (keypad | 89) and <= (keypad | 97))
            return (DskyKey)(code - (keypad | 89) + 1);

        return code switch
        {
            '0' or (keypad | 98) => DskyKey.Zero,
            'v' => DskyKey.Verb,
            'n' => DskyKey.Noun,
            'c' or 8 => DskyKey.Clear,
            'k' => DskyKey.KeyRelease,
            'p' => DskyKey.Proceed,
            'r' => DskyKey.Reset,
            'e' or 13 or (keypad | 88) => DskyKey.Enter,
            '+' or '=' or (keypad | 87) => DskyKey.Plus,
            '-' or (keypad | 86) => DskyKey.Minus,
            _ => null
        };
    }
}
