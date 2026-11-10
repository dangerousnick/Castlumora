using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Jellyfin.Api.Tests.Castlumora;

/// <summary>Owned TLS fixture key usable by OpenSSL and Windows Schannel; never installed in OS trust.</summary>
internal static class ImportTestCertificate
{
    internal static X509Certificate2 Create()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=example.org", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.KeyCertSign,
            true));
        var usages = new OidCollection { new("1.3.6.1.5.5.7.3.1") };
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(usages, false));
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName("example.org");
        request.CertificateExtensions.Add(names.Build());
        using var generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        var pfx = generated.Export(X509ContentType.Pfx);
        try
        {
            // Schannel cannot use every ephemeral key handle returned by CreateSelfSigned.
            // DefaultKeySet imports an OS-usable key owned by the returned certificate; no
            // PersistKeySet flag or machine/user trust-store installation leaves fixture keys behind.
            return X509CertificateLoader.LoadPkcs12(pfx, null, X509KeyStorageFlags.DefaultKeySet);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pfx);
        }
    }
}
