using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Network;

public class SslService : ISecurityService<X509Certificate2>
{
    private readonly IIdentityProvider _identityProvider;
    private readonly ILogger<SslService> _logger;
    private X509Certificate2? _certificate;

    public SslService(IIdentityProvider identityProvider, ILogger<SslService> logger)
    {
        _identityProvider = identityProvider;
        _logger = logger;
    }

    public X509Certificate2 GetSecurityContext()
    {
        if (_certificate == null) InitializeSsl();
        return _certificate!;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        InitializeSsl();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private X509Certificate2 BuildSelfSignedServerCertificate(IIdentityProvider identityProvider)
    {
        var distinguishedName =
            new X500DistinguishedName($"CN={identityProvider.GetEnvironmentId()}, OU=Kurome, O=Kurome Labs");

        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(distinguishedName, rsa, HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        var certificate = request.CreateSelfSigned(new DateTimeOffset(DateTime.UtcNow.AddDays(-1)),
            new DateTimeOffset(DateTime.UtcNow.AddYears(20)));
        certificate.FriendlyName = $"Kurome self-signed certificate for {Environment.MachineName}";

        return new X509Certificate2(certificate.Export(X509ContentType.Pfx, ""),
            "", X509KeyStorageFlags.MachineKeySet);
    }

    private void InitializeSsl()
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
                _logger.LogWarning("SSL Certificate found but not for this machine, regenerating...");
            }
            else if (!cert.Subject.Contains($"{_identityProvider.GetEnvironmentId()}"))
            {
                mustGenerateCertificate = true;
                _logger.LogWarning("SSL Certificate found but not for this application ID, regenerating...");
            }

            if (mustGenerateCertificate)
                certStore.Remove(cert);
        }

        if (mustGenerateCertificate)
        {
            var cert = BuildSelfSignedServerCertificate(_identityProvider);
            certStore.Add(cert);
            _certificate = cert;
            _logger.LogWarning("SSL Certificate generated and added to store");
        }
        else
        {
            _logger.LogInformation("SSL Certificate found and loaded from store");
            _certificate = collection[0];
        }
    }
}