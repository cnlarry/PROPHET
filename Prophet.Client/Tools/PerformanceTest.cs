using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Prophet.Client.Models;

namespace Prophet.Client.Tools;

/// <summary>
/// K线性能测试工具（离线版已禁用）
/// 
/// 注意：此工具依赖已删除的 API 服务，离线版本中已不可用
/// </summary>
public class PerformanceTest
{
    // 离线版：整个类已禁用
    public PerformanceTest()
    {
        Console.WriteLine("⚠️ PerformanceTest 在离线版中已禁用");
    }
}

// 辅助类（保留以避免其他地方的引用错误）
public class CacheTestResult { }
public class PreloadTestResult { }
public class UpdateTestResult { }
