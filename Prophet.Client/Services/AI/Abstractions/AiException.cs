using System;

namespace Prophet.Client.Services.AI.Abstractions;

/// <summary>
/// AI 服务异常基类
/// </summary>
public class AiException : Exception
{
    public AiException(string message) : base(message) { }
    public AiException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// AI Provider 未初始化异常
/// </summary>
public class AiProviderNotInitializedException : AiException
{
    public AiProviderNotInitializedException(string providerName)
        : base($"AI Provider '{providerName}' 尚未初始化，请先配置 API 密钥") { }
}

/// <summary>
/// AI API 密钥无效异常
/// </summary>
public class AiInvalidApiKeyException : AiException
{
    public AiInvalidApiKeyException(string providerName)
        : base($"AI Provider '{providerName}' 的 API 密钥无效，请检查配置") { }
}

/// <summary>
/// AI 配额不足异常
/// </summary>
public class AiQuotaExceededException : AiException
{
    public AiQuotaExceededException(string providerName)
        : base($"AI Provider '{providerName}' 的配额不足或余额不足，请充值后重试") { }
}

/// <summary>
/// AI 请求超时异常
/// </summary>
public class AiRequestTimeoutException : AiException
{
    public AiRequestTimeoutException(string providerName, int timeoutSeconds)
        : base($"AI Provider '{providerName}' 请求超时（{timeoutSeconds}秒），请稍后重试") { }
}

/// <summary>
/// AI 网络异常
/// </summary>
public class AiNetworkException : AiException
{
    public AiNetworkException(string providerName, string details)
        : base($"AI Provider '{providerName}' 网络请求失败: {details}") { }
}
