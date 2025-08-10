using System;
using System.Runtime.InteropServices;

namespace AiVox
{
    // Minimal P/Invoke bindings for PortAudio (blocking I/O path).
    // We use default device streams to exactly mirror Go's usage:
    //  - Input: int16 @ 16000Hz, mono
    //  - Output: float32 @ 24000Hz, mono
    internal static class PortAudioNative
    {
        // PortAudio error codes: paNoError (0) and negatives are errors.
        public const int paNoError = 0;

        [Flags]
        public enum PaSampleFormat : ulong
        {
            paFloat32 = 0x00000001,
            paInt16   = 0x00000008,
        }

        // Note: We rely on dynamic loader to resolve "libportaudio" to the proper dylib/so/dll.
        // On macOS via Homebrew, it's typically /opt/homebrew/lib/libportaudio.dylib (Apple Silicon).
        private const string PortAudioLib = "/opt/homebrew/lib/libportaudio.dylib";

        [DllImport(PortAudioLib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Pa_Initialize();

        [DllImport(PortAudioLib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Pa_Terminate();

        [DllImport(PortAudioLib, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr Pa_GetErrorText(int errorCode);

        [DllImport(PortAudioLib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Pa_OpenDefaultStream(
            out IntPtr stream,
            int numInputChannels,
            int numOutputChannels,
            PaSampleFormat sampleFormat,
            double sampleRate,
            ulong framesPerBuffer,
            IntPtr streamCallback, // we use blocking I/O, so null
            IntPtr userData        // null
        );

        [DllImport(PortAudioLib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Pa_CloseStream(IntPtr stream);

        [DllImport(PortAudioLib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Pa_StartStream(IntPtr stream);

        [DllImport(PortAudioLib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Pa_StopStream(IntPtr stream);

        [DllImport(PortAudioLib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Pa_ReadStream(IntPtr stream, IntPtr buffer, ulong frames);

        [DllImport(PortAudioLib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Pa_WriteStream(IntPtr stream, IntPtr buffer, ulong frames);

        public static string ErrorText(int code)
        {
            try
            {
                var ptr = Pa_GetErrorText(code);
                return ptr == IntPtr.Zero ? $"error {code}" : Marshal.PtrToStringAnsi(ptr) ?? $"error {code}";
            }
            catch
            {
                return $"error {code}";
            }
        }
    }
}
