using System;
using System.Security.Cryptography;
using System.Text;

namespace AmbientWeatherWigiDash
{
    internal static class SecretProtector
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("AmbientWeatherWigiDash-v1");

        public static string Protect(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return string.Empty;
            byte[] data = Encoding.UTF8.GetBytes(plainText);
            return Convert.ToBase64String(ProtectedData.Protect(data, Entropy, DataProtectionScope.CurrentUser));
        }

        public static string Unprotect(string protectedText)
        {
            if (string.IsNullOrEmpty(protectedText)) return string.Empty;
            try
            {
                byte[] data = Convert.FromBase64String(protectedText);
                return Encoding.UTF8.GetString(ProtectedData.Unprotect(data, Entropy, DataProtectionScope.CurrentUser));
            }
            catch { return string.Empty; }
        }
    }
}
