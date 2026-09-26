using System.Security.Cryptography;
using Labaik.Application.Common.Interfaces;

namespace Labaik.Infrastructure.Groups;

internal sealed class JoinCodeGenerator : IJoinCodeGenerator
{
    // No ambiguous characters (no O/0, I/1).
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int Length = 6;

    public string Generate()
    {
        var chars = new char[Length];
        for (var i = 0; i < Length; i++)
        {
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }
        return new string(chars);
    }
}