using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace JournalRecall.SharedTestHelpers.Utilities;

/// <summary>
/// A test stand-in for a mobile device's DPoP key pair (ADR-0014). Defaults to an ES256 (P-256) key —
/// the shape the documented client contract prescribes: header <c>{ typ, alg, jwk }</c>, claims
/// <c>{ htm, htu, iat, jti }</c>, plus <c>ath</c> when presenting an access token. The
/// <see cref="SigningAlgorithm"/> knob lets a test mint an otherwise well-formed proof signed with an
/// algorithm the server doesn't accept (RS256), to exercise the validator's <c>UnsupportedAlgorithm</c>
/// rejection without hand-rolling a JWT (issue 0044).
/// </summary>
public sealed class TestDPoPKey : IDisposable
{
    /// <summary>The proof signing algorithm. ES256 is the only one the server accepts; RS256 exists so a
    /// test can drive the unsupported-algorithm path with a genuinely-signed proof.</summary>
    public enum SigningAlgorithm { Es256, Rs256 }

    private readonly SigningAlgorithm _algorithm;
    private readonly ECDsa? _ecdsa;
    private readonly RSA? _rsa;

    public TestDPoPKey(SigningAlgorithm algorithm = SigningAlgorithm.Es256)
    {
        _algorithm = algorithm;
        if (algorithm == SigningAlgorithm.Es256)
            _ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        else
            _rsa = RSA.Create(2048);
    }

    /// <summary>The RFC 7638 canonical JWK SHA-256 thumbprint (base64url) — the expected <c>jkt</c>.</summary>
    public string Thumbprint => Base64UrlEncoder.Encode(PublicJwk.ComputeJwkThumbprint());

    public JsonWebKey PublicJwk => _algorithm == SigningAlgorithm.Es256 ? EcPublicJwk() : RsaPublicJwk();

    private JsonWebKey EcPublicJwk()
    {
        var p = _ecdsa!.ExportParameters(includePrivateParameters: false);
        return new JsonWebKey
        {
            Kty = "EC",
            Crv = "P-256",
            X = Base64UrlEncoder.Encode(p.Q.X),
            Y = Base64UrlEncoder.Encode(p.Q.Y),
        };
    }

    private JsonWebKey RsaPublicJwk()
    {
        var p = _rsa!.ExportParameters(includePrivateParameters: false);
        return new JsonWebKey
        {
            Kty = "RSA",
            N = Base64UrlEncoder.Encode(p.Modulus),
            E = Base64UrlEncoder.Encode(p.Exponent),
        };
    }

    /// <summary>Mints a proof JWT. The knobs exist so tests can craft each invalid shape: a stale or
    /// future <paramref name="issuedAt"/>, a fixed <paramref name="jti"/> (replay), a wrong
    /// <paramref name="typ"/>, an omitted <paramref name="includeJwk"/>, or a leaked private key. The
    /// <c>alg</c> header follows the key's <see cref="SigningAlgorithm"/>.</summary>
    public string CreateProof(
        string method, string url, string? accessToken = null, DateTimeOffset? issuedAt = null,
        string? jti = null, bool omitJti = false, string typ = "dpop+jwt",
        bool includeJwk = true, bool includePrivateKey = false)
    {
        var payload = new Dictionary<string, object>
        {
            ["htm"] = method,
            ["htu"] = url,
            ["iat"] = (issuedAt ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds(),
        };
        if (!omitJti)
            payload["jti"] = jti ?? Guid.NewGuid().ToString();
        if (accessToken is not null)
            payload["ath"] = Base64UrlEncoder.Encode(SHA256.HashData(Encoding.UTF8.GetBytes(accessToken)));

        var header = new Dictionary<string, object> { ["typ"] = typ };
        if (includeJwk)
            header["jwk"] = JwkHeader(includePrivateKey);

        return new JsonWebTokenHandler().CreateToken(JsonSerializer.Serialize(payload), SigningCredentials(), header);
    }

    /// <summary>Flips the proof's signature so it no longer verifies against the embedded JWK.</summary>
    public static string TamperSignature(string proof)
    {
        var last = proof[^1];
        return proof[..^1] + (last == 'A' ? 'B' : 'A');
    }

    private SigningCredentials SigningCredentials() => _algorithm == SigningAlgorithm.Es256
        ? new SigningCredentials(new ECDsaSecurityKey(_ecdsa), SecurityAlgorithms.EcdsaSha256)
        : new SigningCredentials(new RsaSecurityKey(_rsa), SecurityAlgorithms.RsaSha256);

    private Dictionary<string, string> JwkHeader(bool includePrivateKey) =>
        _algorithm == SigningAlgorithm.Es256 ? EcJwkHeader(includePrivateKey) : RsaJwkHeader(includePrivateKey);

    private Dictionary<string, string> EcJwkHeader(bool includePrivateKey)
    {
        var p = _ecdsa!.ExportParameters(includePrivateParameters: includePrivateKey);
        var jwk = new Dictionary<string, string>
        {
            ["kty"] = "EC",
            ["crv"] = "P-256",
            ["x"] = Base64UrlEncoder.Encode(p.Q.X),
            ["y"] = Base64UrlEncoder.Encode(p.Q.Y),
        };
        if (includePrivateKey)
            jwk["d"] = Base64UrlEncoder.Encode(p.D);
        return jwk;
    }

    private Dictionary<string, string> RsaJwkHeader(bool includePrivateKey)
    {
        var p = _rsa!.ExportParameters(includePrivateParameters: includePrivateKey);
        var jwk = new Dictionary<string, string>
        {
            ["kty"] = "RSA",
            ["n"] = Base64UrlEncoder.Encode(p.Modulus),
            ["e"] = Base64UrlEncoder.Encode(p.Exponent),
        };
        if (includePrivateKey)
            jwk["d"] = Base64UrlEncoder.Encode(p.D);
        return jwk;
    }

    public void Dispose()
    {
        _ecdsa?.Dispose();
        _rsa?.Dispose();
    }
}
