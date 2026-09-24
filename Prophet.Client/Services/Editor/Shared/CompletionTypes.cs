namespace Prophet.Client.Services.Editor.Shared;

/// <summary>
/// 补全上下文类型
/// </summary>
public enum CompletionContextType
{
    General,                    // 通用（显示所有）
    Indicator,                  // 指标名称 $(5m).
    Timeframe,                  // 时间框架 $(
    IndicatorField,             // 指标字段 $(5m).MACD().
    IndicatorParameter,         // 指标参数补全 $(5m).MACD( 或 $(5m).MACD(FAST_PERIOD=12,
    FieldValue,                 // 指标字段值赋值 $(5m).MACD().trend = 
    DataFunctionFieldValue,     // 数据函数字段值赋值 KLINE(5m).open = 
    ThreeLevelFieldValue,       // 三级字段值赋值 FVG(5m).filled().high = 
    TimeSeriesFunctionMethodValue, // 时间序列函数方法返回值枚举值补全 FUNDINGRATE().trend() == 
    EnvVariable,                // 环境变量 @
    DataFunctionField,          // 数据函数字段 KLINE(5m).
    SignalFunctionOrCustom,     // 根部：信号函数或自定义函数
    InsideSignalFunction,       // 信号函数内部：函数调用
    ChainedMethodCall,          // 链式方法调用：KLINE(5m). 或 PRICE(5m). 或 HT(5m).PHASOR. 等
    ChainedMethodParam,         // 链式方法参数：KLINE(5m).high( 或 PRICE(5m).AVG(
    ThreeLevelChainedCall,      // 三级链式调用：CHANGE(5m).close(10). 或 FVG(5m).filled. 等
    SignalValue,                // 信号赋值 } = （触发信号枚举：BUY/SELL/HOLD）
    Operator,                   // 运算符提示（空格后）
    VariableTypeDeclaration,    // 变量类型声明 @varname:
}

/// <summary>
/// 补全上下文
/// </summary>
public class CompletionContext
{
    public CompletionContextType Type { get; set; }
    public string? IndicatorName { get; set; }
    public string? FieldName { get; set; }
    public string? FunctionName { get; set; }
    public string? ParentFieldName { get; set; } // 三级链式调用的父字段名（如 CHANGE().close().value 中的 "close"）
    public string FilterPrefix { get; set; } = ""; // 用户已输入的过滤前缀
    public bool IsInsideFunctionBody { get; set; } = false; // 是否在函数体内
    public object? Tag { get; set; } // 用于存储额外数据（如已输入的参数列表）
}

/// <summary>
/// 参数上下文类型
/// </summary>
public enum ParameterContextType
{
    DataFunctionField,    // 数据函数字段：KLINE(5m).high(
    DataFunctionSubField, // 数据函数子字段：CHANGE(5m).close(10).value(
    Indicator             // 指标参数：$(5m).MACD(
}

