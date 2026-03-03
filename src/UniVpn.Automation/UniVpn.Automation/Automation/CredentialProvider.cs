using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;
using UniVpn.Automation.Core.Configuration;

namespace UniVpn.Automation.Automation;

/// <summary>
/// Retrieves VPN credentials from environment variables (default) or
/// from the Windows Credential Manager, depending on
/// <see cref="AppConfig.CredentialMode"/>.
/// </summary>
public sealed class CredentialProvider : ICredentialProvider
{
    private readonly AppConfig _config;
    private readonly ILogger<CredentialProvider> _logger;

    public CredentialProvider(AppConfig config, ILogger<CredentialProvider> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // ── ICredentialProvider ────────────────────────────────────────────────────

    public (string Username, string Password) GetCredentials()
    {
        return _config.CredentialMode switch
        {
            "WindowsCredentialManager" => ReadFromCredentialManager(),
            _                          => ReadFromEnvironmentVariables()
        };
    }

    public string? GetTwoFactorToken()
    {
        // 2FA token is always read from an environment variable (short-lived, not stored).
        var token = Environment.GetEnvironmentVariable("UNI_VPN_2FA_TOKEN");
        if (string.IsNullOrEmpty(token))
            _logger.LogWarning("UNI_VPN_2FA_TOKEN environment variable is not set.");
        return token;
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private (string, string) ReadFromEnvironmentVariables()
    {
        var username = Environment.GetEnvironmentVariable(_config.UsernameEnvVar) ?? string.Empty;
        var password = Environment.GetEnvironmentVariable(_config.PasswordEnvVar) ?? string.Empty;

        if (string.IsNullOrEmpty(username))
            _logger.LogWarning("Env var {Var} is not set.", _config.UsernameEnvVar);
        if (string.IsNullOrEmpty(password))
            _logger.LogWarning("Env var {Var} is not set.", _config.PasswordEnvVar);

        return (username, password);
    }

    private (string, string) ReadFromCredentialManager()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _logger.LogError("Windows Credential Manager is only available on Windows.");
            return (string.Empty, string.Empty);
        }

        return WindowsCredentialStore.Read(_config.CredentialManagerTarget, _logger);
    }
}

/// <summary>
/// Thin P/Invoke wrapper around the Windows Credential Manager (advapi32.dll).
/// Isolates all unsafe / platform-specific code.
/// </summary>
internal static class WindowsCredentialStore
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIAL
    {
        public uint Flags;
        public uint Type;
        [MarshalAs(UnmanagedType.LPWStr)] public string TargetName;
        [MarshalAs(UnmanagedType.LPWStr)] public string Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        [MarshalAs(UnmanagedType.LPWStr)] public string TargetAlias;
        [MarshalAs(UnmanagedType.LPWStr)] public string UserName;
    }

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredRead(string target, uint type, uint reservedFlag, out IntPtr credentialPtr);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern void CredFree(IntPtr buffer);

    internal static (string Username, string Password) Read(string target, ILogger logger)
    {
        const uint CRED_TYPE_GENERIC = 1;

        if (!CredRead(target, CRED_TYPE_GENERIC, 0, out var ptr))
        {
            logger.LogError(
                "Windows Credential Manager: could not read credential '{Target}'. " +
                "Error code: {Code}",
                target, Marshal.GetLastWin32Error());
            return (string.Empty, string.Empty);
        }

        try
        {
            var cred = Marshal.PtrToStructure<CREDENTIAL>(ptr);
            var username = cred.UserName ?? string.Empty;
            var password = cred.CredentialBlobSize > 0
                ? Encoding.Unicode.GetString(
                    GetBytes(cred.CredentialBlob, (int)cred.CredentialBlobSize))
                : string.Empty;
            return (username, password);
        }
        finally
        {
            CredFree(ptr);
        }
    }

    private static byte[] GetBytes(IntPtr ptr, int length)
    {
        var bytes = new byte[length];
        Marshal.Copy(ptr, bytes, 0, length);
        return bytes;
    }
}
