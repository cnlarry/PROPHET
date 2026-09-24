using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Prophet.Client.Trading.Exchanges.Binance;

/// <summary>
/// 币安API签名认证辅助类
/// </summary>
public static class BinanceAuthHelper
{
    /// <summary>
    /// 生成HMAC SHA256签名
    /// </summary>
    /// <param name="queryString">查询字符串</param>
    /// <param name="apiSecret">API密钥</param>
    /// <returns>签名字符串</returns>
    public static string GenerateSignature(string queryString, string apiSecret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(apiSecret);
        var queryBytes = Encoding.UTF8.GetBytes(queryString);
        
        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(queryBytes);
        
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
    }
    
    /// <summary>
    /// 获取服务器时间戳（毫秒）
    /// </summary>
    public static long GetServerTimestamp()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
    
    /// <summary>
    /// 为参数字典添加签名
    /// </summary>
    /// <param name="parameters">参数字典</param>
    /// <param name="apiSecret">API密钥</param>
    /// <returns>包含签名的参数字典</returns>
    public static Dictionary<string, string> AddSignature(Dictionary<string, string> parameters, string apiSecret)
    {
        // 添加时间戳
        if (!parameters.ContainsKey("timestamp"))
        {
            parameters["timestamp"] = GetServerTimestamp().ToString();
        }
        
        // 按照键排序并生成查询字符串
        var queryString = string.Join("&", 
            parameters
                .OrderBy(kvp => kvp.Key)
                .Select(kvp => $"{kvp.Key}={kvp.Value}"));
        
        // 生成签名
        var signature = GenerateSignature(queryString, apiSecret);
        parameters["signature"] = signature;
        
        return parameters;
    }
    
    /// <summary>
    /// 构建查询字符串（不含签名）
    /// </summary>
    public static string BuildQueryString(Dictionary<string, string> parameters)
    {
        return string.Join("&", 
            parameters
                .OrderBy(kvp => kvp.Key)
                .Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));
    }
    
    /// <summary>
    /// 构建查询字符串（包含签名）
    /// </summary>
    public static string BuildSignedQueryString(Dictionary<string, string> parameters, string apiSecret)
    {
        var signedParams = AddSignature(new Dictionary<string, string>(parameters), apiSecret);
        return BuildQueryString(signedParams);
    }
}

