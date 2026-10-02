using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace KikitanTranslator.Utility;

public static class CredentialProtection
{
    public const string Prefix = "dpapi:v1:";
    [StructLayout(LayoutKind.Sequential)]
    private struct Blob { public int Length; public IntPtr Data; }
    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptProtectData(ref Blob input, string? description, IntPtr entropy,
        IntPtr reserved, IntPtr prompt, int flags, out Blob output);
    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptUnprotectData(ref Blob input, IntPtr description, IntPtr entropy,
        IntPtr reserved, IntPtr prompt, int flags, out Blob output);
    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);

    public static string Protect(string value)
        => string.IsNullOrEmpty(value) || !OperatingSystem.IsWindows() ? value :
            Prefix + Convert.ToBase64String(Transform(Encoding.UTF8.GetBytes(value), true));
    public static string Unprotect(string value)
    {
        if (!value.StartsWith(Prefix, StringComparison.Ordinal)) return value;
        if (!OperatingSystem.IsWindows()) throw new InvalidOperationException("Windows credentials can only be opened by the Windows user who saved them.");
        return Encoding.UTF8.GetString(Transform(Convert.FromBase64String(value[Prefix.Length..]), false));
    }
    private static byte[] Transform(byte[] bytes, bool protect)
    {
        var input = new Blob { Length = bytes.Length, Data = Marshal.AllocHGlobal(bytes.Length) };
        Blob output = default;
        try
        {
            Marshal.Copy(bytes, 0, input.Data, bytes.Length);
            var success = protect ? CryptProtectData(ref input, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output)
                : CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output);
            if (!success) throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not access Windows credential protection. Use the original Windows account or reconfigure credentials.");
            var result = new byte[output.Length];
            Marshal.Copy(output.Data, result, 0, output.Length);
            return result;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
            Marshal.Copy(new byte[input.Length], 0, input.Data, input.Length);
            Marshal.FreeHGlobal(input.Data);
            if (output.Data != IntPtr.Zero)
            {
                Marshal.Copy(new byte[output.Length], 0, output.Data, output.Length);
                LocalFree(output.Data);
            }
        }
    }
}
