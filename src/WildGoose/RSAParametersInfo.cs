using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace WildGoose;

// ReSharper disable once InconsistentNaming
public class RSAParametersInfo
{
    /// <summary>
    /// 
    /// </summary>
    public string? Alg { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public string? D { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public string? Dp { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public string? Dq { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public string? E { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public string? Kid { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public string? Kty { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public string? N { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public string? P { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public string? Q { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public string? Qi { get; set; }

    public RSAParameters GetRsaParameters()
    {
        if (!"RSA".Equals(Kty, StringComparison.InvariantCultureIgnoreCase))
        {
            throw new NotSupportedException("RSA keys are not supported");
        }

        return new RSAParameters
        {
            Modulus = Base64UrlEncoder.DecodeBytes(N),
            Exponent = Base64UrlEncoder.DecodeBytes(E),
            D = string.IsNullOrEmpty(D)
                ? null
                : Base64UrlEncoder.DecodeBytes(D),
            P = string.IsNullOrEmpty(P)
                ? null
                : Base64UrlEncoder.DecodeBytes(P),
            Q = string.IsNullOrEmpty(Q)
                ? null
                : Base64UrlEncoder.DecodeBytes(Q),
            DP = string.IsNullOrEmpty(Dp)
                ? null
                : Base64UrlEncoder.DecodeBytes(Dp),
            DQ = string.IsNullOrEmpty(Dq)
                ? null
                : Base64UrlEncoder.DecodeBytes(Dq),
            InverseQ = string.IsNullOrEmpty(Qi)
                ? null
                : Base64UrlEncoder.DecodeBytes(Qi)
        };
    }

    public RsaSecurityKey GetRsaSecurityKey()
    {
        return new RsaSecurityKey(GetRsaParameters());
    }
}