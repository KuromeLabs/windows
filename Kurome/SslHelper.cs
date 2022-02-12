using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Kurome;

public class SslHelper
{
    public static X509Certificate2 Certificate;

    private static X509Certificate2 BuildSelfSignedServerCertificate()
    {
        var distinguishedName = new X500DistinguishedName($"CN={IdentityProvider.GetGuid()}, OU=Kurome, O=Kurome Labs");

        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(distinguishedName, rsa, HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        var certificate = request.CreateSelfSigned(new DateTimeOffset(DateTime.UtcNow.AddDays(-1)),
            new DateTimeOffset(DateTime.UtcNow.AddYears(20)));
        certificate.FriendlyName = $"Kurome self-signed certificate for {Environment.MachineName}";

        return new X509Certificate2(certificate.Export(X509ContentType.Pfx, ""),
            "", X509KeyStorageFlags.MachineKeySet);
    }

    public static void InitializeSsl()
    {
        using var certStore = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        var mustGenerateCertificate = false;
        certStore.Open(OpenFlags.ReadWrite);
        var collection = certStore.Certificates.Find(X509FindType.FindBySubjectName, "Kurome", false);
        if (collection.Count == 0)
        {
            mustGenerateCertificate = true;
        }
        else
        {
            var cert = collection[0];
            if (cert.FriendlyName != $"Kurome self-signed certificate for {Environment.MachineName}")
            {
                mustGenerateCertificate = true;
                Console.WriteLine("Certificate found but not for this machine. Regenerating.");
            }
            else if (!cert.Subject.Contains($"{IdentityProvider.GetGuid()}"))
            {
                mustGenerateCertificate = true;
                Console.WriteLine("Certificate found but not for this application ID. Regenerating.");
            }

            if (mustGenerateCertificate)
                certStore.Remove(cert);
        }

        if (mustGenerateCertificate)
        {
            var cert = BuildSelfSignedServerCertificate();
            certStore.Add(cert);
            Certificate = cert;
            Console.WriteLine("Certificate generated and added to store.");
        }
        else
        {
            Console.WriteLine("Certificate found in store.");
            Certificate = collection[0];
        }
    }
}