using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

// DPAPI 대신 AES + 기기 고유 ID 기반 키 파생 방식 사용.
// 파일을 다른 기기로 복사해도 복호화 불가 (기기 종속성 유지).
public static class SecureStorage
{
    private const string AppSalt = "ConstellationTrace.v1";

    private static byte[] DeriveKey()
    {
        string deviceId = SystemInfo.deviceUniqueIdentifier + AppSalt;
        using var sha = SHA256.Create();
        return sha.ComputeHash(Encoding.UTF8.GetBytes(deviceId));
    }

    public static byte[] Encrypt(string plainText)
    {
        byte[] key = DeriveKey();
        byte[] iv = new byte[16];
        using var rng = new RNGCryptoServiceProvider();
        rng.GetBytes(iv);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;

        byte[] data = Encoding.UTF8.GetBytes(plainText);
        byte[] encrypted;
        using (var encryptor = aes.CreateEncryptor())
            encrypted = encryptor.TransformFinalBlock(data, 0, data.Length);

        byte[] result = new byte[iv.Length + encrypted.Length];
        Buffer.BlockCopy(iv, 0, result, 0, iv.Length);
        Buffer.BlockCopy(encrypted, 0, result, iv.Length, encrypted.Length);
        return result;
    }

    public static string Decrypt(byte[] encryptedWithIV)
    {
        byte[] key = DeriveKey();
        byte[] iv = new byte[16];
        byte[] encrypted = new byte[encryptedWithIV.Length - 16];

        Buffer.BlockCopy(encryptedWithIV, 0, iv, 0, 16);
        Buffer.BlockCopy(encryptedWithIV, 16, encrypted, 0, encrypted.Length);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        byte[] data = decryptor.TransformFinalBlock(encrypted, 0, encrypted.Length);
        return Encoding.UTF8.GetString(data);
    }
}
