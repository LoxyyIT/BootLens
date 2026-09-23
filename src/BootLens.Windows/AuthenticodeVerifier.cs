using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using BootLens.Core.Domain;

namespace BootLens.Windows;

internal static class AuthenticodeVerifier
{
    private static readonly Guid GenericVerifyAction = new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
    private const uint UiNone = 2;
    private const uint RevokeNone = 0;
    private const uint ChoiceFile = 1;

    public static (bool IsValid, string? Publisher, SignatureStatus Status, string Detail) Verify(string path)
    {
        if (!OperatingSystem.IsWindows()) return (false, null, SignatureStatus.Unavailable, "Windows trust services are unavailable.");

        var filePath = Marshal.StringToCoTaskMemUni(path);
        var fileInfo = new WinTrustFileInfo
        {
            StructSize = (uint)Marshal.SizeOf<WinTrustFileInfo>(),
            FilePath = filePath
        };
        var fileInfoPtr = Marshal.AllocCoTaskMem(Marshal.SizeOf<WinTrustFileInfo>());
        Marshal.StructureToPtr(fileInfo, fileInfoPtr, false);
        var trustData = new WinTrustData
        {
            StructSize = (uint)Marshal.SizeOf<WinTrustData>(),
            UiChoice = UiNone,
            RevocationChecks = RevokeNone,
            UnionChoice = ChoiceFile,
            FileInfo = fileInfoPtr
        };
        var trustDataPtr = Marshal.AllocCoTaskMem(Marshal.SizeOf<WinTrustData>());
        Marshal.StructureToPtr(trustData, trustDataPtr, false);

        try
        {
            var action = GenericVerifyAction;
            var status = WinVerifyTrust(IntPtr.Zero, ref action, trustDataPtr);
            if (status == 0) return (true, ReadPublisher(path), SignatureStatus.Valid, "WinVerifyTrust accepted the file.");
            if (status == 0x800B0100) return (false, null, SignatureStatus.Unsigned, "No Authenticode signature was found.");
            return (false, null, SignatureStatus.Invalid, $"WinVerifyTrust returned 0x{status:X8}.");
        }
        catch (CryptographicException)
        {
            return (false, null, SignatureStatus.Unavailable, "The signature could not be inspected.");
        }
        catch (Win32Exception)
        {
            return (false, null, SignatureStatus.Unavailable, "Windows could not complete signature verification.");
        }
        finally
        {
            Marshal.DestroyStructure<WinTrustData>(trustDataPtr);
            Marshal.FreeCoTaskMem(trustDataPtr);
            Marshal.DestroyStructure<WinTrustFileInfo>(fileInfoPtr);
            Marshal.FreeCoTaskMem(fileInfoPtr);
            Marshal.FreeCoTaskMem(filePath);
        }
    }

    private static string? ReadPublisher(string path)
    {
        try
        {
#pragma warning disable SYSLIB0057
            using var certificate = new X509Certificate2(X509Certificate.CreateFromSignedFile(path));
#pragma warning restore SYSLIB0057
            return certificate.GetNameInfo(X509NameType.SimpleName, false);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    [DllImport("wintrust.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern uint WinVerifyTrust(IntPtr windowHandle, ref Guid actionIdentifier, IntPtr trustData);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WinTrustFileInfo
    {
        public uint StructSize;
        public IntPtr FilePath;
        public IntPtr FileHandle;
        public IntPtr KnownSubject;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WinTrustData
    {
        public uint StructSize;
        public IntPtr PolicyCallbackData;
        public IntPtr SipClientData;
        public uint UiChoice;
        public uint RevocationChecks;
        public uint UnionChoice;
        public IntPtr FileInfo;
        public uint StateAction;
        public IntPtr StateData;
        public IntPtr UrlReference;
        public uint ProviderFlags;
        public uint UiContext;
        public IntPtr SignatureSettings;
    }
}
