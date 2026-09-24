using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Prophet.Client.Services.Security;

/// <summary>
/// 密码加密服务 - 使用本机密钥文件 + AES-256-CBC 保护敏感信息。
/// 密钥不再硬编码在源码中，首次使用时生成并保存在用户配置目录。
/// 历史版本硬编码密钥仅用于解密迁移老数据，不再用于加密。
/// </summary>
public class PasswordEncryptionService
{
    private const string NewCipherPrefix = "DPv1.";
    private static readonly object _keyLock = new();
    private static byte[]? _cachedKey;

    // 历史遗留密钥：仅 DecryptLegacy 用来迁移 2025 版老数据，禁止用于加密。
    // 开源后新数据不再使用它，将在未来版本彻底移除。
    private static readonly byte[] _legacyKey = Encoding.UTF8.GetBytes("ProphetEncryption2025KeyFor"); // 27 bytes
    private static readonly byte[] _legacyIv = Encoding.UTF8.GetBytes("ProphetIV16Bytes"); // 16 bytes

    private static string KeyFilePath
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Prophet");
            return Path.Combine(dir, ".dataprotection.key");
        }
    }

    private static byte[] GetOrCreateKey()
    {
        lock (_keyLock)
        {
            if (_cachedKey != null)
                return _cachedKey;

            var path = KeyFilePath;
            try
            {
                if (File.Exists(path))
                {
                    var raw = Convert.FromBase64String(File.ReadAllText(path).Trim());
                    if (raw.Length == 32)
                    {
                        _cachedKey = raw;
                        return _cachedKey;
                    }
                }
            }
            catch
            {
                // 密钥文件损坏则重新生成
            }

            var fresh = RandomNumberGenerator.GetBytes(32);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, Convert.ToBase64String(fresh));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PasswordEncryption] 密钥文件写入失败: {ex.Message}");
            }
            _cachedKey = fresh;
            return _cachedKey;
        }
    }

    /// <summary>
    /// 加密字符串（新数据：本机密钥 + 随机 IV）
    /// </summary>
    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        try
        {
            var key = GetOrCreateKey();
            using var aes = Aes.Create();
            aes.Key = key;
            aes.GenerateIV();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var encryptor = aes.CreateEncryptor();
            using var ms = new MemoryStream();
            ms.Write(aes.IV, 0, aes.IV.Length);
            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            using (var sw = new StreamWriter(cs))
            {
                sw.Write(plainText);
            }

            return NewCipherPrefix + Convert.ToBase64String(ms.ToArray());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PasswordEncryption] 加密失败: {ex.Message}");
            throw new InvalidOperationException("密码加密失败", ex);
        }
    }

    /// <summary>
    /// 解密字符串：优先新格式，其次历史硬编码密钥（迁移），最后按明文兼容返回。
    /// </summary>
    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
            return string.Empty;

        // 新格式
        if (cipherText.StartsWith(NewCipherPrefix, StringComparison.Ordinal))
        {
            try
            {
                var raw = Convert.FromBase64String(cipherText.Substring(NewCipherPrefix.Length));
                if (raw.Length > 16)
                {
                    var key = GetOrCreateKey();
                    var iv = raw[..16];
                    var data = raw[16..];
                    using var aes = Aes.Create();
                    aes.Key = key;
                    aes.IV = iv;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    using var decryptor = aes.CreateDecryptor();
                    using var ms = new MemoryStream(data);
                    using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
                    using var sr = new StreamReader(cs);
                    return sr.ReadToEnd();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PasswordEncryption] 新格式解密失败: {ex.Message}");
                throw new InvalidOperationException("密码解密失败", ex);
            }
        }

        // 历史格式：用遗留密钥尝试解密，成功则调用方应重新 Encrypt 迁移
        try
        {
            var cipherBytes = Convert.FromBase64String(cipherText);

            using var aes = Aes.Create();
            aes.Key = _legacyKey;
            aes.IV = _legacyIv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            using var ms = new MemoryStream(cipherBytes);
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var sr = new StreamReader(cs);

            return sr.ReadToEnd();
        }
        catch (FormatException)
        {
            // 如果Base64解码失败，可能是明文密码（向后兼容）
            Console.WriteLine("[PasswordEncryption] 解密失败（可能是明文密码），返回原文");
            return cipherText;
        }
        catch (CryptographicException)
        {
            // 如果解密失败，可能是明文密码（向后兼容）
            Console.WriteLine("[PasswordEncryption] 解密失败（密码学异常），返回原文");
            return cipherText;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PasswordEncryption] 解密失败: {ex.Message}");
            throw new InvalidOperationException("密码解密失败", ex);
        }
    }

    /// <summary>
    /// 判断字符串是否已加密（新前缀直接判定；否则尝试历史密钥解密验证）
    /// </summary>
    public bool IsEncrypted(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        if (text.StartsWith(NewCipherPrefix, StringComparison.Ordinal))
            return true;

        try
        {
            // 尝试Base64解码
            var bytes = Convert.FromBase64String(text);
            
            // 如果解码成功且长度合理（AES加密后的数据通常是16的倍数）
            if (bytes.Length > 0 && bytes.Length % 16 == 0)
            {
                // 尝试用历史密钥解密验证
                try
                {
                    using var aes = Aes.Create();
                    aes.Key = _legacyKey;
                    aes.IV = _legacyIv;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    using var decryptor = aes.CreateDecryptor();
                    using var ms = new MemoryStream(bytes);
                    using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
                    using var sr = new StreamReader(cs);
                    
                    _ = sr.ReadToEnd();
                    return true; // 解密成功，确认是加密数据
                }
                catch
                {
                    return false; // 解密失败，不是加密数据
                }
            }
            
            return false;
        }
        catch
        {
            return false; // Base64解码失败，不是加密数据
        }
    }

    /// <summary>
    /// 安全地加密（如果尚未加密）
    /// </summary>
    public string EncryptIfNeeded(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        // 如果已经加密，直接返回
        if (IsEncrypted(text))
            return text;

        // 否则加密
        return Encrypt(text);
    }

    /// <summary>
    /// 迁移旧密码到加密格式
    /// </summary>
    /// <param name="plainPassword">明文密码</param>
    /// <returns>加密后的密码</returns>
    public string MigratePlainPassword(string? plainPassword)
    {
        if (string.IsNullOrEmpty(plainPassword))
            return string.Empty;

        // 如果已经加密，不再加密
        if (IsEncrypted(plainPassword))
        {
            Console.WriteLine("[PasswordEncryption] 密码已加密，跳过迁移");
            return plainPassword;
        }

        // 加密明文密码
        var encrypted = Encrypt(plainPassword);
        Console.WriteLine("[PasswordEncryption] 密码已迁移到加密格式");
        return encrypted;
    }
}

