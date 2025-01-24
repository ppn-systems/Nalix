using System;
using System.Security.Cryptography;

namespace Notio.Cryptography.Mode;

internal class AesGcmMode
{
    public static ReadOnlyMemory<byte> Encrypt(ReadOnlyMemory<byte> plainText, ReadOnlyMemory<byte> key)
    {
        using var aes = new AesGcm(key.Span, AesGcm.TagByteSizes.MaxSize);
        var nonce = new byte[AesGcm.NonceByteSizes.MaxSize];
        RandomNumberGenerator.Fill(nonce);

        var cipherText = new byte[plainText.Length];
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];

        aes.Encrypt(nonce, plainText.Span, cipherText, tag);

        var result = new byte[nonce.Length + cipherText.Length + tag.Length];
        nonce.CopyTo(result, 0);
        cipherText.CopyTo(result, nonce.Length);
        tag.CopyTo(result, nonce.Length + cipherText.Length);

        return result;
    }

    public static ReadOnlyMemory<byte> Decrypt(ReadOnlyMemory<byte> cipherText, ReadOnlyMemory<byte> key)
    {
        using var aes = new AesGcm(key.Span, AesGcm.TagByteSizes.MaxSize);
        var nonce = cipherText[..AesGcm.NonceByteSizes.MaxSize].Span;
        var tag = cipherText[^AesGcm.TagByteSizes.MaxSize..].Span;
        var encryptedData = cipherText.Slice(nonce.Length, cipherText.Length - nonce.Length - tag.Length).Span;

        var decrypted = new byte[encryptedData.Length];
        aes.Decrypt(nonce, encryptedData, tag, decrypted);

        return decrypted;
    }
}