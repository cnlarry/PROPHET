using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Avalonia.Platform;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Prophet.Client.Services.Rules;

/// <summary>
/// DSL 规则服务 - 统一规则系统的核心
/// 从 DSLRules.yaml 加载所有规则,为其他服务提供规则查询接口
/// </summary>
public class DSLRulesService
{
    private DSLRulesSchema? _rules;
    private Dictionary<string, IndicatorDefinition> _indicatorMap = new();
    private Dictionary<string, DataFunctionDefinition> _dataFunctionMap = new();
    private Dictionary<string, TimeSeriesFunctionDefinition> _timeSeriesFunctionMap = new();
    private HashSet<string> _validTimeframes = new();
    private Dictionary<string, HashSet<string>> _enumValues = new();

    public bool IsReady { get; private set; }
    public string? LoadError { get; private set; }
    public string? RulesSource { get; private set; }

    public DSLRulesService()
    {
        LoadRules();
    }

    public void ReloadRules()
    {
        LoadRules();
    }

    /// <summary>
    /// 加载规则文件
    /// </summary>
    private void LoadRules()
    {
        try
        {
            IsReady = false;
            LoadError = null;
            RulesSource = null;

            var rulesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "Resources", "Rules", "DSLRules.yaml");

            string? yaml = null;

            if (!File.Exists(rulesPath))
            {
                // 1) 输出目录没有时，尝试从 Avalonia 资源读取（用于某些运行方式）
                yaml = TryReadAssetText("avares://Prophet.Client/Resources/Rules/DSLRules.yaml");
                if (yaml == null)
                {
                    LoadError = $"规则文件不存在：{rulesPath}";
                    Console.WriteLine($"[DSLRulesService] ❌ {LoadError}");

                    // 仍然初始化为空规则，保证调用方不崩溃，但补全会降级
                    _rules = new DSLRulesSchema();
                    ClearIndexes();
                    return;
                }

                RulesSource = "avares://Prophet.Client/Resources/Rules/DSLRules.yaml";
            }
            else
            {
                yaml = File.ReadAllText(rulesPath);
                RulesSource = rulesPath;
            }

            // 不使用命名约定，完全依赖 YamlMember 属性
            var deserializer = new DeserializerBuilder()
                .IgnoreUnmatchedProperties()  // 忽略未匹配的属性
                .Build();

            _rules = deserializer.Deserialize<DSLRulesSchema>(yaml);

            if (_rules != null)
            {
                ClearIndexes();
                BuildIndexes();
                IsReady = true;
            }
            else
            {
                LoadError = "规则解析结果为 null（可能是 YAML 格式错误）";
                Console.WriteLine($"[DSLRulesService] ❌ {LoadError}");
            }
        }
        catch (Exception ex)
        {
            LoadError = $"加载规则失败：{ex.GetType().Name} - {ex.Message}";
            Console.WriteLine($"[DSLRulesService] ❌ 加载规则失败:");
            Console.WriteLine($"  异常类型: {ex.GetType().Name}");
            Console.WriteLine($"  异常消息: {ex.Message}");
            Console.WriteLine($"  堆栈跟踪: {ex.StackTrace}");

            // 仍然初始化为空规则，保证调用方不崩溃，但补全会降级
            _rules = new DSLRulesSchema();
            ClearIndexes();
        }
    }

    private void ClearIndexes()
    {
        _indicatorMap = new Dictionary<string, IndicatorDefinition>(StringComparer.OrdinalIgnoreCase);
        _dataFunctionMap = new Dictionary<string, DataFunctionDefinition>(StringComparer.OrdinalIgnoreCase);
        _timeSeriesFunctionMap = new Dictionary<string, TimeSeriesFunctionDefinition>(StringComparer.OrdinalIgnoreCase);
        _validTimeframes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _enumValues = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
    }

    private static string? TryReadAssetText(string assetUri)
    {
        try
        {
            var uri = new Uri(assetUri);
            if (!AssetLoader.Exists(uri))
                return null;

            using var stream = AssetLoader.Open(uri);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            return reader.ReadToEnd();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DSLRulesService] ⚠️ 读取资源失败: {assetUri}, {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 构建索引以提高查询性能
    /// </summary>
    private void BuildIndexes()
    {
        if (_rules == null) return;

        // 构建指标索引
        foreach (var indicator in _rules.Indicators ?? Enumerable.Empty<IndicatorDefinition>())
        {
            if (string.IsNullOrEmpty(indicator.Id))
            {
                Console.WriteLine($"  ⚠️ 指标 Id 为空！Name={indicator.Name}");
                continue;
            }
            
            _indicatorMap[indicator.Id] = indicator;
            
            // 处理别名
            if (indicator.Aliases != null)
            {
                foreach (var alias in indicator.Aliases)
                {
                    _indicatorMap[alias] = indicator;
                }
            }
        }

        // 构建数据函数索引
        foreach (var func in _rules.DataFunctions ?? Enumerable.Empty<DataFunctionDefinition>())
        {
            if (string.IsNullOrEmpty(func.Id))
            {
                Console.WriteLine($"  ⚠️ 函数 Id 为空！Name={func.Name}");
                continue;
            }
            
            _dataFunctionMap[func.Id] = func;
            
            // 处理别名
            if (func.Aliases != null)
            {
                foreach (var alias in func.Aliases)
                {
                    _dataFunctionMap[alias] = func;
                }
            }
        }
        
        // 构建时间序列函数索引
        foreach (var func in _rules.TimeSeriesFunctions ?? Enumerable.Empty<TimeSeriesFunctionDefinition>())
        {
            if (string.IsNullOrEmpty(func.Id))
            {
                Console.WriteLine($"  ⚠️ 时间序列函数 Id 为空！Name={func.Name}");
                continue;
            }
            
            _timeSeriesFunctionMap[func.Id] = func;
            
            // 处理别名
            if (func.Aliases != null)
            {
                foreach (var alias in func.Aliases)
                {
                    _timeSeriesFunctionMap[alias] = func;
                }
            }
        }
        
        // 构建时间框架集合
        foreach (var tf in _rules.Timeframes ?? Enumerable.Empty<TimeframeDefinition>())
        {
            _validTimeframes.Add(tf.Id);
        }

        // 构建枚举值集合
        foreach (var enumDef in _rules.Enums ?? Enumerable.Empty<EnumDefinition>())
        {
            _enumValues[enumDef.Id] = new HashSet<string>(enumDef.Values ?? Enumerable.Empty<string>());
        }
    }

    #region 指标查询

    /// <summary>
    /// 获取所有指标
    /// </summary>
    public IEnumerable<IndicatorDefinition> GetAllIndicators()
    {
        return _rules?.Indicators ?? Enumerable.Empty<IndicatorDefinition>();
    }

    /// <summary>
    /// 检查指标是否有效
    /// </summary>
    public bool IsValidIndicator(string name)
    {
        return _indicatorMap.ContainsKey(name);
    }

    /// <summary>
    /// 获取指标定义
    /// </summary>
    public IndicatorDefinition? GetIndicator(string name)
    {
        return _indicatorMap.TryGetValue(name, out var indicator) ? indicator : null;
    }

    /// <summary>
    /// 检查指标字段是否有效
    /// </summary>
    public bool IsValidIndicatorField(string indicatorName, string fieldName)
    {
        var indicator = GetIndicator(indicatorName);
        if (indicator?.Fields == null) return false;

        return indicator.Fields.Any(f => f.Name == fieldName);
    }

    /// <summary>
    /// 检查指标参数是否有效
    /// </summary>
    public bool IsValidIndicatorParameter(string indicatorName, string paramName)
    {
        var indicator = GetIndicator(indicatorName);
        if (indicator?.Parameters == null) return false;

        return indicator.Parameters.Any(p => p.Name == paramName);
    }

    /// <summary>
    /// 获取指标字段信息
    /// </summary>
    public FieldDefinition? GetIndicatorField(string indicatorName, string fieldName)
    {
        var indicator = GetIndicator(indicatorName);
        return indicator?.Fields?.FirstOrDefault(f => f.Name == fieldName);
    }

    /// <summary>
    /// 检查字段是否只读
    /// </summary>
    public bool IsFieldReadonly(string indicatorName, string fieldName)
    {
        var field = GetIndicatorField(indicatorName, fieldName);
        return field?.Readonly ?? true; // 默认为只读
    }

    #endregion

    #region 数据函数查询

    /// <summary>
    /// 获取所有数据函数
    /// </summary>
    public IEnumerable<DataFunctionDefinition> GetAllDataFunctions()
    {
        return _rules?.DataFunctions ?? Enumerable.Empty<DataFunctionDefinition>();
    }

    /// <summary>
    /// 检查数据函数是否有效
    /// </summary>
    public bool IsValidDataFunction(string name)
    {
        return _dataFunctionMap.ContainsKey(name);
    }

    /// <summary>
    /// 获取数据函数定义
    /// </summary>
    public DataFunctionDefinition? GetDataFunction(string name)
    {
        return _dataFunctionMap.TryGetValue(name, out var func) ? func : null;
    }

    /// <summary>
    /// 检查数据函数的方法或字段是否有效
    /// </summary>
    public bool IsValidDataFunctionMethod(string functionName, string methodName)
    {
        var func = GetDataFunction(functionName);
        if (func == null) return false;

        // 检查 methods（如 KLINE(5m).open(0)）
        if (func.Methods != null && func.Methods.Any(m => m.Name == methodName))
            return true;

        // 检查 fields（如 FVG(5m).bullish）
        if (func.Fields != null && func.Fields.Any(f => f.Name == methodName))
            return true;

        return false;
    }

    #endregion

    #region 时间框架查询

    /// <summary>
    /// 获取所有时间框架
    /// </summary>
    public IEnumerable<TimeframeDefinition> GetAllTimeframes()
    {
        return _rules?.Timeframes ?? Enumerable.Empty<TimeframeDefinition>();
    }

    /// <summary>
    /// 检查时间框架是否有效
    /// </summary>
    public bool IsValidTimeframe(string timeframe)
    {
        return _validTimeframes.Contains(timeframe);
    }

    #endregion

    #region 枚举值查询

    /// <summary>
    /// 检查枚举值是否有效
    /// </summary>
    public bool IsValidEnumValue(string enumType, string value)
    {
        return _enumValues.TryGetValue(enumType, out var values) && values.Contains(value);
    }

    /// <summary>
    /// 获取枚举的所有可能值
    /// </summary>
    public IEnumerable<string>? GetEnumValues(string enumType)
    {
        return _enumValues.TryGetValue(enumType, out var values) ? values : null;
    }

    /// <summary>
    /// 获取所有枚举定义
    /// </summary>
    public IEnumerable<EnumDefinition> GetAllEnums()
    {
        return _rules?.Enums ?? Enumerable.Empty<EnumDefinition>();
    }

    #endregion

    #region 环境变量查询

    // 环境变量概念已废弃 - 所有以 @ 开头的都是用户变量
    // IsValidEnvironmentVariable 和 GetAllEnvironmentVariables 方法已移除

    #endregion

    #region 信号函数查询

    /// <summary>
    /// 获取所有信号函数
    /// </summary>
    public IEnumerable<SignalFunctionDefinition> GetAllSignalFunctions()
    {
        return _rules?.SignalFunctions ?? Enumerable.Empty<SignalFunctionDefinition>();
    }

    /// <summary>
    /// 获取信号函数定义
    /// </summary>
    public SignalFunctionDefinition? GetSignalFunction(string name)
    {
        return _rules?.SignalFunctions?.FirstOrDefault(f => f.Id == name);
    }

    /// <summary>
    /// 检查信号函数是否有效
    /// </summary>
    public bool IsValidSignalFunction(string name)
    {
        return _rules?.SignalFunctions?.Any(f => f.Id == name) ?? false;
    }

    #endregion

    #region 数学函数查询

    /// <summary>
    /// 获取所有数学函数
    /// </summary>
    public IEnumerable<MathFunctionDefinition> GetAllMathFunctions()
    {
        return _rules?.MathFunctions ?? Enumerable.Empty<MathFunctionDefinition>();
    }

    /// <summary>
    /// 获取数学函数定义
    /// </summary>
    public MathFunctionDefinition? GetMathFunction(string name)
    {
        return _rules?.MathFunctions?.FirstOrDefault(f => f.Id == name);
    }

    /// <summary>
    /// 检查数学函数是否有效
    /// </summary>
    public bool IsValidMathFunction(string name)
    {
        return _rules?.MathFunctions?.Any(f => f.Id == name) ?? false;
    }

    #endregion

    #region 时间序列函数查询

    /// <summary>
    /// 获取所有时间序列函数
    /// </summary>
    public IEnumerable<TimeSeriesFunctionDefinition> GetAllTimeSeriesFunctions()
    {
        return _rules?.TimeSeriesFunctions ?? Enumerable.Empty<TimeSeriesFunctionDefinition>();
    }

    /// <summary>
    /// 获取时间序列函数定义
    /// </summary>
    public TimeSeriesFunctionDefinition? GetTimeSeriesFunction(string name)
    {
        return _timeSeriesFunctionMap.TryGetValue(name, out var func) ? func : null;
    }

    /// <summary>
    /// 检查时间序列函数是否有效
    /// </summary>
    public bool IsValidTimeSeriesFunction(string name)
    {
        return _timeSeriesFunctionMap.ContainsKey(name);
    }

    /// <summary>
    /// 检查时间序列函数的方法是否有效
    /// </summary>
    public bool IsValidTimeSeriesFunctionMethod(string functionName, string methodName)
    {
        var func = GetTimeSeriesFunction(functionName);
        if (func?.Methods == null) return false;

        return func.Methods.Any(m => m.Name == methodName);
    }

    #endregion

    #region 命名规范验证

    /// <summary>
    /// 验证标识符名称是否符合命名规范
    /// </summary>
    public bool ValidateNaming(string type, string name)
    {
        var conventions = _rules?.NamingConventions;
        if (conventions == null) return true;

        var convention = type.ToLower() switch
        {
            "indicator" => conventions.Indicators,
            "field" => conventions.Fields,
            "parameter" => conventions.Parameters,
            "datafunction" => conventions.DataFunctions,
            "signalfunction" => conventions.SignalFunctions,
            _ => null
        };

        if (convention?.Pattern == null) return true;

        return System.Text.RegularExpressions.Regex.IsMatch(name, convention.Pattern);
    }

    #endregion

    #region 完整性检查

    /// <summary>
    /// 获取所有指标名称（用于代码补全）
    /// </summary>
    public IEnumerable<string> GetAllIndicatorNames()
    {
        return _indicatorMap.Keys;
    }

    /// <summary>
    /// 获取所有数据函数名称（用于代码补全）
    /// </summary>
    public IEnumerable<string> GetAllDataFunctionNames()
    {
        return _dataFunctionMap.Keys;
    }

    /// <summary>
    /// 获取规则元数据
    /// </summary>
    public MetadataDefinition? GetMetadata()
    {
        return _rules?.Metadata;
    }

    #endregion
}

#region 数据模型

public class DSLRulesSchema
{
    [YamlMember(Alias = "metadata")]
    public MetadataDefinition? Metadata { get; set; }
    
    [YamlMember(Alias = "timeframes")]
    public List<TimeframeDefinition>? Timeframes { get; set; }
    
    [YamlMember(Alias = "indicators")]
    public List<IndicatorDefinition>? Indicators { get; set; }
    
    [YamlMember(Alias = "data_functions")]
    public List<DataFunctionDefinition>? DataFunctions { get; set; }
    
    [YamlMember(Alias = "timeseries_functions")]
    public List<TimeSeriesFunctionDefinition>? TimeSeriesFunctions { get; set; }
    
    [YamlMember(Alias = "signal_functions")]
    public List<SignalFunctionDefinition>? SignalFunctions { get; set; }
    
    [YamlMember(Alias = "math_functions")]
    public List<MathFunctionDefinition>? MathFunctions { get; set; }
    
    [YamlMember(Alias = "enums")]
    public List<EnumDefinition>? Enums { get; set; }
    
    // 环境变量概念已废弃 - EnvironmentVariables 属性已移除
    
    public NamingConventionsDefinition? NamingConventions { get; set; }
    public ValidationRulesDefinition? ValidationRules { get; set; }
}

public class MetadataDefinition
{
    [YamlMember(Alias = "version")]
    public string? Version { get; set; }
    
    [YamlMember(Alias = "dsl_version")]
    public string? DslVersion { get; set; }
    
    [YamlMember(Alias = "core_version")]
    public string? CoreVersion { get; set; }
    
    [YamlMember(Alias = "last_updated")]
    public string? LastUpdated { get; set; }
    
    [YamlMember(Alias = "description")]
    public string? Description { get; set; }
}

public class TimeframeDefinition
{
    [YamlMember(Alias = "id")]
    public string Id { get; set; } = string.Empty;
    
    [YamlMember(Alias = "name")]
    public string? Name { get; set; }
    
    [YamlMember(Alias = "description")]
    public string? Description { get; set; }
    
    [YamlMember(Alias = "seconds")]
    public int Seconds { get; set; }
}

public class IndicatorDefinition
{
    [YamlMember(Alias = "id")]
    public string Id { get; set; } = string.Empty;
    
    [YamlMember(Alias = "name")]
    public string? Name { get; set; }
    
    [YamlMember(Alias = "category")]
    public string? Category { get; set; }
    
    [YamlMember(Alias = "description")]
    public string? Description { get; set; }
    
    [YamlMember(Alias = "syntax")]
    public string? Syntax { get; set; }
    
    [YamlMember(Alias = "aliases")]
    public List<string>? Aliases { get; set; }
    
    [YamlMember(Alias = "parameters")]
    public List<ParameterDefinition>? Parameters { get; set; }
    
    [YamlMember(Alias = "fields")]
    public List<FieldDefinition>? Fields { get; set; }
}

public class DataFunctionDefinition
{
    [YamlMember(Alias = "id")]
    public string Id { get; set; } = string.Empty;
    
    [YamlMember(Alias = "name")]
    public string? Name { get; set; }
    
    [YamlMember(Alias = "category")]
    public string? Category { get; set; }
    
    [YamlMember(Alias = "description")]
    public string? Description { get; set; }
    
    [YamlMember(Alias = "syntax")]
    public string? Syntax { get; set; }
    
    [YamlMember(Alias = "aliases")]
    public List<string>? Aliases { get; set; }
    
    [YamlMember(Alias = "parameters")]
    public List<ParameterDefinition>? Parameters { get; set; }
    
    [YamlMember(Alias = "methods")]
    public List<MethodDefinition>? Methods { get; set; }
    
    [YamlMember(Alias = "fields")]
    public List<FieldDefinition>? Fields { get; set; }
}

public class ParameterDefinition
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = string.Empty;
    
    [YamlMember(Alias = "type")]
    public string Type { get; set; } = "double";
    
    [YamlMember(Alias = "default")]
    public object? Default { get; set; }
    
    [YamlMember(Alias = "min")]
    public double? Min { get; set; }
    
    [YamlMember(Alias = "max")]
    public double? Max { get; set; }
    
    [YamlMember(Alias = "required")]
    public bool Required { get; set; }
    
    [YamlMember(Alias = "description")]
    public string? Description { get; set; }
}

public class FieldDefinition
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = string.Empty;
    
    [YamlMember(Alias = "type")]
    public string Type { get; set; } = "double";
    
    [YamlMember(Alias = "description")]
    public string? Description { get; set; }
    
    [YamlMember(Alias = "readonly")]
    public bool Readonly { get; set; } = true;
    
    [YamlMember(Alias = "enum_values")]
    public List<string>? EnumValues { get; set; }
    
    // 子字段（用于 HT.PHASOR.inphase 等复合字段）
    [YamlMember(Alias = "subfields")]
    public List<FieldDefinition>? SubFields { get; set; }
}

public class MethodDefinition
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = string.Empty;
    
    [YamlMember(Alias = "return_type")]
    public string ReturnType { get; set; } = "double";
    
    [YamlMember(Alias = "parameters")]
    public List<ParameterDefinition>? Parameters { get; set; }
    
    [YamlMember(Alias = "description")]
    public string? Description { get; set; }
    
    // 子字段（不需要括号的属性访问，如 CHANGE().value）
    [YamlMember(Alias = "subfields")]
    public List<FieldDefinition>? SubFields { get; set; }
    
    // 子方法（需要括号的方法调用，如 CONSECUTIVE().rising()）
    [YamlMember(Alias = "submethods")]
    public List<MethodDefinition>? SubMethods { get; set; }
}

public class SignalFunctionDefinition
{
    [YamlMember(Alias = "id")]
    public string Id { get; set; } = string.Empty;
    
    [YamlMember(Alias = "name")]
    public string? Name { get; set; }
    
    [YamlMember(Alias = "description")]
    public string? Description { get; set; }
    
    [YamlMember(Alias = "syntax")]
    public string? Syntax { get; set; }
    
    [YamlMember(Alias = "return_type")]
    public string ReturnType { get; set; } = "signal";
    
    [YamlMember(Alias = "parameters")]
    public List<ParameterDefinition>? Parameters { get; set; }
}

public class MathFunctionDefinition
{
    [YamlMember(Alias = "id")]
    public string Id { get; set; } = string.Empty;
    
    [YamlMember(Alias = "name")]
    public string? Name { get; set; }
    
    [YamlMember(Alias = "category")]
    public string? Category { get; set; }
    
    [YamlMember(Alias = "description")]
    public string? Description { get; set; }
    
    [YamlMember(Alias = "syntax")]
    public string? Syntax { get; set; }
    
    [YamlMember(Alias = "return_type")]
    public string? ReturnType { get; set; }
    
    [YamlMember(Alias = "parameters")]
    public List<ParameterDefinition>? Parameters { get; set; }
    
    [YamlMember(Alias = "examples")]
    public List<string>? Examples { get; set; }
}

/// <summary>
/// 时间序列函数定义
/// 用于处理不需要时间框架参数的历史数据序列（如恐惧与贪婪指数、资金费率等）
/// </summary>
public class TimeSeriesFunctionDefinition
{
    [YamlMember(Alias = "id")]
    public string Id { get; set; } = string.Empty;
    
    [YamlMember(Alias = "name")]
    public string? Name { get; set; }
    
    [YamlMember(Alias = "category")]
    public string? Category { get; set; }
    
    [YamlMember(Alias = "description")]
    public string? Description { get; set; }
    
    [YamlMember(Alias = "syntax")]
    public string? Syntax { get; set; }
    
    [YamlMember(Alias = "rating")]
    public int? Rating { get; set; }
    
    [YamlMember(Alias = "aliases")]
    public List<string>? Aliases { get; set; }
    
    [YamlMember(Alias = "data_source")]
    public string? DataSource { get; set; }
    
    [YamlMember(Alias = "update_frequency")]
    public string? UpdateFrequency { get; set; }
    
    [YamlMember(Alias = "parameters")]
    public List<ParameterDefinition>? Parameters { get; set; }
    
    [YamlMember(Alias = "methods")]
    public List<MethodDefinition>? Methods { get; set; }
    
    [YamlMember(Alias = "examples")]
    public List<string>? Examples { get; set; }
}

public class EnumDefinition
{
    [YamlMember(Alias = "id")]
    public string Id { get; set; } = string.Empty;
    
    [YamlMember(Alias = "name")]
    public string? Name { get; set; }
    
    [YamlMember(Alias = "description")]
    public string? Description { get; set; }
    
    [YamlMember(Alias = "values")]
    public List<string>? Values { get; set; }
    
    [YamlMember(Alias = "value_descriptions")]
    public Dictionary<string, string>? ValueDescriptions { get; set; }
    
    /// <summary>
    /// 获取枚举值的描述信息
    /// </summary>
    public string? GetValueDescription(string value)
    {
        if (ValueDescriptions != null && ValueDescriptions.TryGetValue(value, out var description))
        {
            return description;
        }
        return null;
    }
}

// 环境变量概念已废弃 - EnvironmentVariableDefinition 类已移除

public class NamingConventionsDefinition
{
    public NamingConventionRule? Indicators { get; set; }
    public NamingConventionRule? Fields { get; set; }
    public NamingConventionRule? Parameters { get; set; }
    public NamingConventionRule? DataFunctions { get; set; }
    public NamingConventionRule? SignalFunctions { get; set; }
}

public class NamingConventionRule
{
    public string? Pattern { get; set; }
    public string? Description { get; set; }
    public List<string>? Examples { get; set; }
}

public class ValidationRulesDefinition
{
    public ValidationRule? IndicatorReference { get; set; }
    public ValidationRule? ParameterAssignment { get; set; }
    public ValidationRule? FieldReadonly { get; set; }
    public ValidationRule? SignalAssignment { get; set; }
}

public class ValidationRule
{
    public string? Pattern { get; set; }
    public string? Message { get; set; }
    public List<string>? AllowedContext { get; set; }
    public List<string>? ForbiddenContext { get; set; }
    public bool? Required { get; set; }
    public List<string>? ValidValues { get; set; }
}

#endregion

