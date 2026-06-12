using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace JournalRecall.Api.Auth;

/// <summary>Why a DPoP proof was rejected (RFC 9449 §4.3). The vocabulary a bearer client must
/// distinguish: freshness failures are retryable with a new proof; the rest are not.</summary>
public enum DPoPProofFailure
{
    None = 0,
    /// <summary>Not a parseable JWT, wrong <c>typ</c> (must be <c>dpop+jwt</c>), or a required claim is absent.</summary>
    MalformedProof,
    /// <summary>The <c>jwk</c> header is missing, unparsable, or carries a private key.</summary>
    InvalidJwk,
    /// <summary>The proof is signed with an algorithm other than ES256.</summary>
    UnsupportedAlgorithm,
    /// <summary>The signature does not verify against the embedded JWK.</summary>
    InvalidSignature,
    /// <summary>The <c>htm</c> claim does not match the request method.</summary>
    MethodMismatch,
    /// <summary>The <c>htu</c> claim does not match the request URL (scheme + host + path).</summary>
    UrlMismatch,
    /// <summary>The <c>iat</c> is older than the proof lifetime (plus clock skew) — regenerate and retry.</summary>
    StaleProof,
    /// <summary>The <c>iat</c> is further in the future than the allowed clock skew — regenerate and retry.</summary>
    FutureProof,
    /// <summary>The <c>jti</c> was already presented within the proof lifetime (issue 0038).</summary>
    ReplayedProof,
}

/// <summary>
/// Token-endpoint DPoP proof validation (ADR-0014): the first-party half of the two-halves split.
/// Given the inbound <c>DPoP</c> header value plus the request method and absolute URL, validates the
/// proof per RFC 9449 §4.3 and returns the canonical JWK SHA-256 thumbprint (<c>jkt</c>, RFC 7638) that
/// the minted token and refresh chain are bound to. Per-request enforcement on protected resources is
/// the Duende library's job, not this module's.
/// </summary>
public sealed class DPoPProofValidator(TimeProvider timeProvider, IDPoPReplayCache replayCache)
{
    /// <summary>The request header carrying the proof JWT.</summary>
    public const string HeaderName = "DPoP";

    public const string ProofTokenType = "dpop+jwt";

    /// <summary>How long a proof stays presentable after its <c>iat</c>. Matches the Duende
    /// resource-server default so both halves enforce one freshness contract.</summary>
    public static readonly TimeSpan ProofLifetime = TimeSpan.FromSeconds(5);

    /// <summary>Tolerated client/server clock disagreement on <c>iat</c> (Duende resource-server default).</summary>
    public static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(25);

    /// <summary>On success carries the bound-key thumbprint (<c>jkt</c>); on failure the typed reason.</summary>
    public sealed record ProofValidation(bool Succeeded, string? Thumbprint, DPoPProofFailure Failure)
    {
        public static ProofValidation Ok(string thumbprint) => new(true, thumbprint, DPoPProofFailure.None);
        public static ProofValidation Fail(DPoPProofFailure failure) => new(false, null, failure);
    }

    public async Task<ProofValidation> ValidateAsync(
        string proof, string httpMethod, string url, CancellationToken cancellationToken = default)
    {
        var handler = new JsonWebTokenHandler();

        JsonWebToken token;
        try
        {
            token = handler.ReadJsonWebToken(proof);
        }
        catch (Exception)
        {
            return ProofValidation.Fail(DPoPProofFailure.MalformedProof);
        }

        if (!string.Equals(token.Typ, ProofTokenType, StringComparison.Ordinal))
            return ProofValidation.Fail(DPoPProofFailure.MalformedProof);

        if (!string.Equals(token.Alg, SecurityAlgorithms.EcdsaSha256, StringComparison.Ordinal))
            return ProofValidation.Fail(DPoPProofFailure.UnsupportedAlgorithm);

        // The proof is self-signed: the verification key is the JWK embedded in its own header. It must
        // be a public key — a private key in the header is a client bug worth rejecting outright.
        if (!token.TryGetHeaderValue<JsonElement>(JwtHeaderParameterNames.Jwk, out var jwkElement))
            return ProofValidation.Fail(DPoPProofFailure.InvalidJwk);

        JsonWebKey jwk;
        try
        {
            jwk = new JsonWebKey(jwkElement.GetRawText());
        }
        catch (Exception)
        {
            return ProofValidation.Fail(DPoPProofFailure.InvalidJwk);
        }

        if (jwk.HasPrivateKey)
            return ProofValidation.Fail(DPoPProofFailure.InvalidJwk);

        var validation = await handler.ValidateTokenAsync(proof, new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = false, // freshness is the iat window below, not exp/nbf
            ValidTypes = [ProofTokenType],
            ValidAlgorithms = [SecurityAlgorithms.EcdsaSha256],
            IssuerSigningKey = jwk,
        });
        if (!validation.IsValid)
            return ProofValidation.Fail(DPoPProofFailure.InvalidSignature);

        if (!token.TryGetPayloadValue<string>("htm", out var htm) || !string.Equals(htm, httpMethod, StringComparison.Ordinal))
            return ProofValidation.Fail(DPoPProofFailure.MethodMismatch);

        if (!token.TryGetPayloadValue<string>("htu", out var htu) || !HtuMatches(url, htu))
            return ProofValidation.Fail(DPoPProofFailure.UrlMismatch);

        if (!token.TryGetPayloadValue<long>(JwtRegisteredClaimNames.Iat, out var iat))
            return ProofValidation.Fail(DPoPProofFailure.MalformedProof);

        var issuedAt = DateTimeOffset.FromUnixTimeSeconds(iat);
        var now = timeProvider.GetUtcNow();
        if (issuedAt > now + ClockSkew)
            return ProofValidation.Fail(DPoPProofFailure.FutureProof);
        if (issuedAt + ProofLifetime + ClockSkew < now)
            return ProofValidation.Fail(DPoPProofFailure.StaleProof);

        if (!token.TryGetPayloadValue<string>(JwtRegisteredClaimNames.Jti, out var jti) || string.IsNullOrEmpty(jti))
            return ProofValidation.Fail(DPoPProofFailure.MalformedProof);

        // Replay is checked last (mirroring the Duende half) so only otherwise-valid proofs occupy the
        // cache. The entry outlives the proof by twice the skew, because the clock may sit either side.
        if (!await replayCache.TryAddAsync(jti, ProofLifetime + ClockSkew * 2, cancellationToken))
            return ProofValidation.Fail(DPoPProofFailure.ReplayedProof);

        // RFC 7638 canonical JWK thumbprint, base64url — the same computation the Duende resource-server
        // half performs, so the cnf.jkt stamped here matches the proof key it checks on every request.
        return ProofValidation.Ok(Base64UrlEncoder.Encode(jwk.ComputeJwkThumbprint()));
    }

    /// <summary>htu comparison per RFC 9449: scheme, host/port, and path — query and fragment ignored.</summary>
    private static bool HtuMatches(string requestUrl, string htu)
    {
        if (!Uri.TryCreate(requestUrl, UriKind.Absolute, out var expected) ||
            !Uri.TryCreate(htu, UriKind.Absolute, out var presented))
            return false;

        return Uri.Compare(expected, presented,
            UriComponents.Scheme | UriComponents.HostAndPort | UriComponents.Path,
            UriFormat.SafeUnescaped, StringComparison.OrdinalIgnoreCase) == 0;
    }
}
