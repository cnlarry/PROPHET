using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Prophet.Client.Core;
using Prophet.Client.Services.AI.Abstractions;
using Prophet.Client.Services.Rules;

namespace Prophet.Client.Services.AI.Core;

/// <summary>
/// AI提示词模板管理器
/// </summary>
public class PromptTemplates
{
    private static readonly Lazy<PromptTemplates> _instance = new Lazy<PromptTemplates>(() => new PromptTemplates());
    private readonly DSLRulesService _rulesService = ServiceContainer.GetService<DSLRulesService>();
    private string? _cachedStrategyPrompt; // 缓存会在首次调用时自动生成

    /// <summary>
    /// 单例实例
    /// </summary>
    public static PromptTemplates Instance => _instance.Value;

    // 私有构造函数
    private PromptTemplates() { }

    #region Markdown文件加载

    /// <summary>
    /// 从Resources/AIPrompts目录加载Markdown文件
    /// </summary>
    /// <param name="fileName">文件名（不含路径）</param>
    /// <returns>文件内容</returns>
    /// <exception cref="FileNotFoundException">当文件不存在时抛出异常</exception>
    private string LoadMarkdownFile(string fileName)
    {
        // 尝试多个可能的路径
        var possiblePaths = new[]
        {
            // 开发时路径（相对于可执行文件）
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "AIPrompts", fileName),
            // 备用路径1
            Path.Combine(Directory.GetCurrentDirectory(), "Resources", "AIPrompts", fileName),
            // 备用路径2（Prophet.Client目录）
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Resources", "AIPrompts", fileName),
        };

        foreach (var path in possiblePaths)
        {
            var normalizedPath = Path.GetFullPath(path);
            if (File.Exists(normalizedPath))
            {
                Console.WriteLine($"[PromptTemplates] 成功加载提示词文件: {fileName} (路径: {normalizedPath})");
                return File.ReadAllText(normalizedPath, Encoding.UTF8);
            }
        }

        // 如果所有路径都找不到文件，抛出异常
        var searchedPaths = string.Join("\n  - ", possiblePaths.Select(Path.GetFullPath));
        throw new FileNotFoundException(
            $"未找到提示词文件: {fileName}\n" +
            $"已搜索以下路径:\n  - {searchedPaths}\n" +
            $"请确保文件存在于 Prophet.Client/Resources/AIPrompts/ 目录下。",
            fileName);
    }

    #endregion

    #region 提示词生成

    /// <summary>
    /// 获取策略生成系统提示词（动态生成，包含所有规则信息）
    /// </summary>
    private string GetStrategyGenerationPrompt()
    {
        if (_cachedStrategyPrompt != null)
            return _cachedStrategyPrompt;

        var sb = new StringBuilder();
        
        // 1. 加载基础提示词（从md文件）
        var basePrompt = LoadMarkdownFile("AI策略生成提示词.md");
        sb.AppendLine(basePrompt);
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        
        // 2. 添加动态生成的DSL规则信息（从DSLRules.yaml自动生成）
        sb.AppendLine("## Prophet DSL 可用组件清单（自动生成）");
        sb.AppendLine();
        sb.AppendLine("以下信息从系统配置自动生成，包含所有可用的指标、函数和参数：");
        sb.AppendLine();
        
        sb.AppendLine(GenerateSignalFunctionsSection());
        sb.AppendLine(GenerateIndicatorsSection());
        sb.AppendLine(GenerateDataFunctionsSection());
        sb.AppendLine(GenerateTimeSeriesFunctionsSection());
        sb.AppendLine(GenerateMathFunctionsSection());
        sb.AppendLine(GenerateEnvironmentVariablesSection());
        sb.AppendLine(GenerateEnumsSection());
        
        sb.AppendLine("---");
        sb.AppendLine();
        
        // 3. 加载常见错误避免（从md文件）
        var errorsGuide = LoadMarkdownFile("AI常见错误避免.md");
        sb.AppendLine(errorsGuide);
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        
        // 4. 加载代码检查清单（从md文件）
        var checklistGuide = LoadMarkdownFile("AI代码检查清单.md");
        sb.AppendLine(checklistGuide);
        
        _cachedStrategyPrompt = sb.ToString();
        return _cachedStrategyPrompt;
    }

    /// <summary>
    /// 生成信号函数部分
    /// </summary>
    private string GenerateSignalFunctionsSection()
    {
        var sb = new StringBuilder();
        sb.AppendLine("### Prophet DSL 核心语法规则（重要！）");
        sb.AppendLine();
        sb.AppendLine("#### 1. 信号函数（Signal Functions）- 策略的核心构造");
        sb.AppendLine("Prophet DSL使用信号函数来组合条件，生成交易信号。**所有策略必须以信号函数开始**：");
        sb.AppendLine();
        
        var signalFunctions = _rulesService.GetAllSignalFunctions().ToList();
        sb.AppendLine($"**Prophet DSL 支持 {signalFunctions.Count} 个信号函数：**");
        sb.AppendLine();
        
        foreach (var func in signalFunctions.OrderBy(f => f.Id))
        {
            sb.AppendLine($"**{func.Id}** - {func.Description ?? func.Name ?? ""}");
            if (!string.IsNullOrEmpty(func.Syntax))
            {
                sb.AppendLine($"  - 语法：`{func.Syntax}`");
            }
            sb.AppendLine();
        }
        
        sb.AppendLine("**⚠️ 关键语法规则：**");
        sb.AppendLine("- 条件之间使用**分号 `;`** 分隔（不是逗号！）");
        sb.AppendLine("- 每个条件必须以分号结尾（包括最后一个条件）");
        sb.AppendLine("- 规则之间使用分号分隔");
        sb.AppendLine();
        
        sb.AppendLine("**⚠️ WEIGHTED 信号函数的重要约束：**");
        sb.AppendLine("- `threshold` 参数范围：**0.00 ~ 1.00**（即 0% ~ 100%）");
        sb.AppendLine("- ✅ 正确示例：`WEIGHTED(0.6)` 或 `WEIGHTED(0.75)`");
        sb.AppendLine("- ❌ 错误示例：`WEIGHTED(60)` 或 `WEIGHTED(1.5)` - 超出范围");
        sb.AppendLine("- 含义：0.6 表示需要满足 60% 的总权重才触发信号");
        sb.AppendLine();
        
        return sb.ToString();
    }

    /// <summary>
    /// 生成指标部分
    /// </summary>
    private string GenerateIndicatorsSection()
    {
        var sb = new StringBuilder();
        var indicators = _rulesService.GetAllIndicators().ToList();
        
        sb.AppendLine($"#### 2. 技术指标（Indicators）- 共 {indicators.Count} 个指标");
        sb.AppendLine();
        sb.AppendLine("**指标引用语法：** `$(timeframe).INDICATOR(params).field(offset)`");
        sb.AppendLine();
        sb.AppendLine("**重要说明：**");
        sb.AppendLine("- **✅ 推荐：直接在指标调用时传递参数**（内联参数）：");
        sb.AppendLine("  - `$(5m).RSI(14).value` - 直接指定RSI周期为14");
        sb.AppendLine("  - `$(5m).MACD(12,26,9).trend` - 直接指定MACD的快线、慢线、信号线周期");
        sb.AppendLine("  - `$(5m).BOLL(20,2.0).upper` - 直接指定布林带周期和标准差");
        sb.AppendLine("- 也可以使用默认参数：`$(5m).RSI().value`（使用配置中的参数或默认值）");
        sb.AppendLine("- **字段支持offset参数**：`$(5m).MACD().histogram(-1)` 表示前一根K线的值");
        sb.AppendLine("  - offset范围：`[-100, 0]`，0表示最新值（默认），-1表示前一根，以此类推");
        sb.AppendLine("- 使用 `()` 调用指标，即使没有参数也要加括号");
        sb.AppendLine("- 字段名使用小写（如 `value`, `trend`, `crossover_type`）");
        sb.AppendLine();
        sb.AppendLine("**⚠️ trend字段的枚举值（极其重要！）**");
        sb.AppendLine("- **大部分指标**的trend字段值为：`BULLISH`（看涨）、`BEARISH`（看跌）、`NEUTRAL`（中性）");
        sb.AppendLine("  - 包括：MACD、RSI、EMA、MA、OBV、ADX等绝大多数指标");
        sb.AppendLine("  - ✅ 正确：`$(5m).OBV().trend == BULLISH`");
        sb.AppendLine("  - ✅ 正确：`$(5m).MACD().trend == BEARISH`");
        sb.AppendLine("  - ❌ 错误：`$(5m).OBV().trend == RISING` - 没有RISING值！");
        sb.AppendLine("- **例外**：只有特殊数据函数使用RISING/FALLING/STABLE");
        sb.AppendLine("  - `FEARGREED().trend(7)` - 可以是RISING、FALLING、STABLE");
        sb.AppendLine("  - `FUNDINGRATE().trend(24)` - 可以是RISING、FALLING、STABLE");
        sb.AppendLine("- **请务必区分**：指标的trend字段 ≠ 数据函数的trend()方法");
        sb.AppendLine();
        sb.AppendLine("**⚠️ 指标字段的数据类型（重要！）**");
        sb.AppendLine("- **没有任何指标字段的数据类型是String**");
        sb.AppendLine("- trend、crossover_type、volatility 等枚举字段在比较时**不需要加双引号**");
        sb.AppendLine("- ✅ 正确：`$(5m).ATR(14).volatility == HIGH` - 直接使用枚举值");
        sb.AppendLine("- ✅ 正确：`$(5m).MACD().trend == BULLISH` - 直接使用枚举值");
        sb.AppendLine("- ❌ 错误：`$(5m).ATR(14).volatility == \"HIGH\"` - 不要加双引号");
        sb.AppendLine("- ❌ 错误：`$(5m).MACD().trend == \"BULLISH\"` - 不要加双引号");
        sb.AppendLine("- **规则**：枚举值是常量，不是字符串，使用时不加引号");
        sb.AppendLine();
        
        // 按类别分组显示指标
        var categories = indicators.GroupBy(i => i.Category ?? "other").OrderBy(g => g.Key);
        foreach (var category in categories)
        {
            var categoryName = category.Key switch
            {
                "momentum" => "动量指标",
                "trend" => "趋势指标",
                "volatility" => "波动率指标",
                "volume" => "成交量指标",
                "price" => "价格指标",
                "pattern" => "形态指标",
                "statistical" => "统计指标",
                _ => category.Key
            };
            
            sb.AppendLine($"**{categoryName}（{category.Count()}个）：**");
            foreach (var indicator in category.OrderBy(i => i.Id).Take(10)) // 每个类别最多显示10个
            {
                sb.AppendLine($"- **{indicator.Id}** ({indicator.Name ?? ""})");
                
                if (indicator.Fields != null && indicator.Fields.Count > 0)
                {
                    var fields = indicator.Fields.Take(5).Select(f => f.Name).ToList();
                    sb.AppendLine($"  字段：{string.Join(", ", fields)}{(indicator.Fields.Count > 5 ? "..." : "")}");
                }
                
                if (indicator.Parameters != null && indicator.Parameters.Count > 0)
                {
                    var paramsList = indicator.Parameters.Select(p => $"{p.Name}({p.Default?.ToString() ?? "默认"})").ToList();
                    sb.AppendLine($"  参数：{string.Join(", ", paramsList)}");
                }
            }
            if (category.Count() > 10)
            {
                sb.AppendLine($"  ... 还有 {category.Count() - 10} 个指标");
            }
            sb.AppendLine();
        }
        
        sb.AppendLine("**常用指标字段快速参考**");
        sb.AppendLine();
        sb.AppendLine("| 指标 | 常用字段 | trend字段值 | crossover_type字段值 |");
        sb.AppendLine("|------|----------|-------------|---------------------|");
        sb.AppendLine("| MACD | value, signal, histogram, trend | BULLISH/BEARISH/NEUTRAL | GOLDEN_CROSS/DEATH_CROSS/NONE |");
        sb.AppendLine("| RSI | value, trend | BULLISH/BEARISH/NEUTRAL | - |");
        sb.AppendLine("| EMA | value, slope, trend | BULLISH/BEARISH/NEUTRAL | - |");
        sb.AppendLine("| OBV | value, trend, change | BULLISH/BEARISH/NEUTRAL | - |");
        sb.AppendLine("| ADX | value, trend | BULLISH/BEARISH/NEUTRAL | - |");
        sb.AppendLine("| BOLL | upper, middle, lower, percent_b | - | - |");
        sb.AppendLine();
        sb.AppendLine("**✅ 正确示例（优先使用内联参数）：**");
        sb.AppendLine("```dsl");
        sb.AppendLine("// ✅ 推荐：内联参数传递");
        sb.AppendLine("$(5m).RSI(14).value > 70                          // RSI值，直接指定周期14");
        sb.AppendLine("$(1h).MACD(12,26,9).crossover_type == GOLDEN_CROSS // MACD金叉，直接指定参数");
        sb.AppendLine("$(15m).EMA(20).slope > 0                          // EMA斜率，直接指定周期20");
        sb.AppendLine("$(5m).BOLL(20,2.0).lower < 50000                 // 布林带下轨，直接指定参数");
        sb.AppendLine("$(5m).OBV().trend == BULLISH                      // ✅ OBV趋势使用BULLISH");
        sb.AppendLine();
        sb.AppendLine("// offset 访问历史值");
        sb.AppendLine("$(5m).RSI(14).value(-1) > 70                     // 前一根K线的RSI值");
        sb.AppendLine("$(5m).MACD(12,26,9).histogram(-1) > 0            // 前一根K线的MACD柱");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("**❌ 常见错误示例：**");
        sb.AppendLine("```dsl");
        sb.AppendLine("// ❌ 错误：OBV的trend字段没有RISING值");
        sb.AppendLine("$(5m).OBV().trend == RISING  // 错误！");
        sb.AppendLine();
        sb.AppendLine("// ✅ 正确：使用BULLISH");
        sb.AppendLine("$(5m).OBV().trend == BULLISH  // 正确");
        sb.AppendLine("```");
        sb.AppendLine();
        
        return sb.ToString();
    }

    /// <summary>
    /// 生成数据函数部分
    /// </summary>
    private string GenerateDataFunctionsSection()
    {
        var sb = new StringBuilder();
        var dataFunctions = _rulesService.GetAllDataFunctions().ToList();
        
        sb.AppendLine($"#### 3. 数据函数（Data Functions）- 共 {dataFunctions.Count} 个函数");
        sb.AppendLine();
        sb.AppendLine("用于动态计算K线历史数据的统计值，无需预先配置：");
        sb.AppendLine();
        
        // 按类别分组
        var categories = dataFunctions.GroupBy(f => f.Category ?? "other").OrderBy(g => g.Key);
        foreach (var category in categories)
        {
            var categoryName = category.Key switch
            {
                "data_access" => "数据访问",
                "statistical" => "统计分析",
                "sequence" => "序列条件",
                "pattern" => "K线形态",
                "advanced" => "高级分析",
                _ => category.Key
            };
            
            sb.AppendLine($"**{categoryName}：**");
            foreach (var func in category.OrderBy(f => f.Id).Take(8))
            {
                sb.Append($"- **{func.Id}**");
                if (!string.IsNullOrEmpty(func.Description))
                {
                    sb.Append($" - {func.Description}");
                }
                sb.AppendLine();
                
                if (!string.IsNullOrEmpty(func.Syntax))
                {
                    sb.AppendLine($"  语法：`{func.Syntax}`");
                }
            }
            sb.AppendLine();
        }
        
        return sb.ToString();
    }

    /// <summary>
    /// 生成时间序列函数部分
    /// </summary>
    private string GenerateTimeSeriesFunctionsSection()
    {
        var sb = new StringBuilder();
        var timeSeriesFunctions = _rulesService.GetAllTimeSeriesFunctions().ToList();
        
        if (timeSeriesFunctions.Count > 0)
        {
            sb.AppendLine($"#### 4. 时间序列函数（Time Series Functions）- 共 {timeSeriesFunctions.Count} 个函数");
            sb.AppendLine();
            
            foreach (var func in timeSeriesFunctions.OrderBy(f => f.Id))
            {
                sb.Append($"- **{func.Id}**");
                if (!string.IsNullOrEmpty(func.Description))
                {
                    sb.Append($" - {func.Description}");
                }
                sb.AppendLine();
                
                if (!string.IsNullOrEmpty(func.Syntax))
                {
                    sb.AppendLine($"  语法：`{func.Syntax}`");
                }
            }
            sb.AppendLine();
        }
        
        return sb.ToString();
    }

    /// <summary>
    /// 生成数学函数部分
    /// </summary>
    private string GenerateMathFunctionsSection()
    {
        var sb = new StringBuilder();
        var mathFunctions = _rulesService.GetAllMathFunctions().ToList();
        
        sb.AppendLine($"#### 5. 数学函数（Math Functions）- 共 {mathFunctions.Count} 个函数");
        sb.AppendLine();
        sb.AppendLine("用于数学运算和逻辑运算：");
        sb.AppendLine();
        
        var functionNames = mathFunctions.Select(f => f.Id).OrderBy(f => f).ToList();
        sb.AppendLine(string.Join(", ", functionNames));
        sb.AppendLine();
        
        return sb.ToString();
    }

    /// <summary>
    /// 生成环境变量部分
    /// </summary>
    private string GenerateEnvironmentVariablesSection()
    {
        var sb = new StringBuilder();
        
        // 环境变量概念已废弃，返回说明信息
        sb.AppendLine($"#### 6. 用户变量（User Variables）");
        sb.AppendLine();
        sb.AppendLine("使用 `@` 前缀声明和访问用户变量（仅在自定义函数内）：");
        sb.AppendLine();
        sb.AppendLine("- 命名规范：全小写 + 下划线（如 `@score`, `@my_var`, `@rsi_value`）");
        sb.AppendLine("- 作用域：仅在声明的代码块内可见");
        sb.AppendLine("- 示例：`@score: Double = 100.0;`");
        sb.AppendLine();
        
        return sb.ToString();
    }

    /// <summary>
    /// 生成枚举值部分
    /// </summary>
    private string GenerateEnumsSection()
    {
        var sb = new StringBuilder();
        var enums = _rulesService.GetAllEnums().ToList();
        
        sb.AppendLine($"#### 7. 枚举值（Enums）- 共 {enums.Count} 个枚举类型");
        sb.AppendLine();
        sb.AppendLine("用于指标字段的比较，必须使用正确的枚举值：");
        sb.AppendLine();
        
        foreach (var enumDef in enums.OrderBy(e => e.Id))
        {
            sb.Append($"- **{enumDef.Id}** ({enumDef.Name ?? ""})");
            if (enumDef.Values != null && enumDef.Values.Count > 0)
            {
                sb.AppendLine($"：{string.Join(", ", enumDef.Values)}");
            }
            else
            {
                sb.AppendLine();
            }
        }
        sb.AppendLine();
        
        return sb.ToString();
    }


    /// <summary>
    /// 回测分析 - 系统提示词
    /// </summary>
    private const string SystemPrompt_BacktestAnalysis = @"你是一个资深的量化交易策略分析师，精通Prophet DSL语法，擅长深度分析回测结果并提供专业的优化建议。

### 核心任务
- 深入分析回测结果的关键绩效指标，识别策略的优劣势
- 基于数据洞察提供具体的、可执行的优化建议
- 生成符合Prophet DSL语法的优化代码
- 考虑风险控制、市场适应性、参数敏感性等多维度因素

### 回测指标深度解读

#### 核心收益指标
- **总收益率**：策略的整体表现，需结合回测周期评估
- **年化收益率**：标准化后的收益水平，便于不同周期策略对比
- **盈亏比（Profit Factor）**：平均盈利 / 平均亏损，>1.5为良好，>2.0为优秀

#### 风险指标
- **最大回撤（Max Drawdown）**：策略净值从峰值到谷值的最大跌幅
  - <20%：低风险
  - 20%-40%：中等风险
  - >40%：高风险，需要优化
- **夏普比率（Sharpe Ratio）**：风险调整后收益
  - <1：较差
  - 1-2：一般
  - 2-3：良好
  - >3：优秀

#### 交易质量指标
- **胜率（Win Rate）**：盈利交易次数 / 总交易次数
  - 高胜率（>60%）+ 低盈亏比：可能过度优化
  - 低胜率（<40%）+ 高盈亏比：趋势跟踪策略常见
- **总交易次数**：评估策略活跃度
  - 过少（<10次）：可能信号过于严格
  - 过多（>100次）：可能信号过于宽松，交易成本高
- **平均持仓时间**：评估策略类型（日内/波段/趋势）

### 分析框架

#### 1. 整体表现评估
- 收益是否达到预期？
- 风险是否可控？
- 收益风险比是否合理？

#### 2. 问题诊断
- **收益问题**：
  - 收益为负：策略逻辑可能有问题，需要重新审视入场/出场条件
  - 收益过低：可能信号过于保守，或市场环境不匹配
- **风险问题**：
  - 回撤过大：需要加强止损机制，或增加信号过滤
  - 波动剧烈：可能需要平滑信号，或降低仓位
- **交易质量问题**：
  - 胜率过低：可能需要更严格的入场条件
  - 交易次数过多：可能需要增加信号过滤，减少假信号
  - 交易次数过少：可能需要放宽条件，或检查是否有语法错误

#### 3. 优化方向

**参数优化：**
- 调整指标周期（如RSI周期、MACD参数）
- 调整阈值（如RSI超买超卖阈值）
- 使用参数赋值语法动态调整：`$(5m).RSI().PERIOD = 16;`

**信号过滤优化：**
- 增加多指标共振：使用 `ALL{}` 或 `VOTE(0.6){}`
- 增加多时间框架确认：使用多时间框架语法 `$.RSI[5m, 15m, 1h].value > 70`
- 增加成交量确认：`$(5m).OBV().trend == UP`
- 增加趋势强度过滤：`$(1h).ADX().value > 25`

**风险控制优化：**
- 添加止损条件：`@CURRENT_PRICE < $(5m).EMA().value * 0.95`
- 添加止盈条件：`@CURRENT_PRICE > $(5m).EMA().value * 1.1`
- 添加时间过滤：`@CURRENT_HOUR BETWEEN(9, 21)`
- 添加市场状态过滤：避免在震荡市场交易

**策略结构优化：**
- 使用 `WEIGHTED{}` 加权评分，为重要条件分配更高权重
- 使用 `VOTE{}` 投票机制，提高策略灵活性
- 分离买入和卖出逻辑，使用不同的信号函数组合

### 输出格式要求

1. **回测结果分析**
   - 整体表现评估（收益、风险、交易质量）
   - 关键指标解读（指出异常值和潜在问题）
   - 策略优劣势总结

2. **问题诊断**
   - 识别主要问题（收益、风险、交易质量）
   - 分析问题根源（策略逻辑、参数设置、市场环境）

3. **优化建议**（至少3条，按优先级排序）
   - 每条建议包含：问题描述、优化方案、预期效果
   - 提供具体的DSL代码片段示例

4. **优化后的完整策略代码**
   - 使用 ```dsl 和 ``` 包裹
   - 添加注释说明优化点
   - 确保语法完全正确，可直接执行

5. **后续优化方向**
   - 参数敏感性分析建议
   - 不同市场环境的适应性建议
   - 进一步优化空间

### 优化示例

**问题：** 胜率低（35%），但盈亏比较高（2.5），交易次数偏少（15次）

**分析：** 
- 策略逻辑可能是趋势跟踪型，需要更严格的入场条件
- 交易次数少可能是因为信号过于严格，错过了部分机会

**优化建议：**
1. **增加多时间框架确认**：使用多时间框架语法提高信号质量
2. **添加成交量过滤**：确保在成交量配合下交易
3. **优化出场条件**：使用追踪止损，提高盈亏比

**优化代码：**
```dsl
// 优化：多时间框架共振 + 成交量确认
ALL{
  // 多时间框架趋势共振
  $[5m, 15m, 1h].MACD.trend == BULLISH;
  
  // 成交量确认
  $(5m).OBV().trend == UP;
  
  // 趋势强度确认
  $(1h).ADX().value > 25;
} = BUY;

// 优化：追踪止损出场
ANY{
  $(5m).MACD().crossover_type == DEATH_CROSS;
  @CURRENT_PRICE < HIGHEST(5m).high(10) * 0.97;  // 3%追踪止损
} = SELL;
```

### 注意事项

- 基于客观数据进行分析，避免主观臆断
- 优化建议要具体、可执行，不要泛泛而谈
- 考虑优化后的策略可能带来的新问题
- 提供多个优化方向，让用户可以根据实际情况选择
- 确保优化后的代码语法完全正确

### ⚠️ 生成优化代码时的关键约束（必须遵守！）

1. **自定义函数参数类型**：只能使用 Integer、Double、Boolean、String，不能使用 TrendType 等枚举类型名称
2. **trend字段枚举值**：大部分指标的 trend 字段只有 BULLISH/BEARISH/NEUTRAL，不要使用 RISING/FALLING
3. **信号函数内容**：信号函数（ALL/ANY/WEIGHTED等）内只能有条件表达式，不能定义变量
4. **变量声明**：所有用户变量必须使用 @ 前缀，不要使用 var 或 let 关键字
5. **信号类型**：只有 BUY、SELL、HOLD 三种，不要使用 ENTRY_LONG、EXIT_SHORT 等
6. **条件分隔符**：条件之间使用分号 ; 分隔，不是逗号
7. **逻辑运算符**：必须使用 OR/AND/NOT 关键字，不支持 ||/&&/! 符号";

    /// <summary>
    /// 行情预测 - 系统提示词
    /// </summary>
    private const string SystemPrompt_MarketPrediction = @"你是一个专业的金融市场分析师，擅长基于历史数据和技术指标进行短期走势预测。

### 核心任务
- 基于提供的当前行情数据和技术指标，预测未来24小时内的价格走势
- 分析市场趋势、支撑位和阻力位
- 提供明确的预测方向（上涨、下跌或震荡）和置信度
- 给出相应的交易建议

### 输入数据说明
- 当前K线数据：包含开盘价、收盘价、最高价、最低价、成交量
- 技术指标：包含常用指标的当前值

### 输出格式要求
1. 简要分析当前市场状况
2. 预测未来24小时走势（方向和幅度）
3. 支撑位和阻力位分析
4. 交易建议（入场、出场、止损）
5. 预测置信度（0-100%）

### 注意事项
- 基于客观数据进行分析，避免主观臆断
- 明确说明预测的局限性
- 保持语言简洁明了，避免使用过于专业的术语";

    #endregion

    #region 模板生成方法

    /// <summary>
    /// 获取策略生成提示词
    /// </summary>
    /// <param name="userQuery">用户查询</param>
    /// <param name="contextCode">上下文代码（当前策略）</param>
    /// <returns>完整的提示词列表</returns>
    public List<AiMessage> GetStrategyGenerationPrompt(string userQuery, string? contextCode = null)
    {
        var messages = new List<AiMessage>
        {
            new AiMessage
            {
                Role = "system",
                Content = GetStrategyGenerationPrompt()
            },
            new AiMessage
            {
                Role = "user",
                Content = BuildUserPromptWithContext(userQuery, contextCode)
            }
        };

        return messages;
    }

    /// <summary>
    /// 获取回测分析提示词
    /// </summary>
    /// <param name="backtestResultSummary">回测结果摘要</param>
    /// <param name="currentStrategyCode">当前策略代码</param>
    /// <returns>完整的提示词列表</returns>
    public List<AiMessage> GetBacktestAnalysisPrompt(string backtestResultSummary, string currentStrategyCode)
    {
        var messages = new List<AiMessage>
        {
            new AiMessage
            {
                Role = "system",
                Content = SystemPrompt_BacktestAnalysis
            },
            new AiMessage
            {
                Role = "user",
                Content = $@"请分析以下回测结果并优化策略：

## 回测结果摘要
{backtestResultSummary}

## 当前策略代码
```dsl
{currentStrategyCode}
```

请提供优化建议和优化后的策略代码。"}
        };

        return messages;
    }

    /// <summary>
    /// 获取行情预测提示词
    /// </summary>
    /// <param name="symbol">交易对</param>
    /// <param name="timeframe">时间框架</param>
    /// <param name="marketDataSummary">市场数据摘要</param>
    /// <returns>完整的提示词列表</returns>
    public List<AiMessage> GetMarketPredictionPrompt(string symbol, string timeframe, string marketDataSummary)
    {
        var messages = new List<AiMessage>
        {
            new AiMessage
            {
                Role = "system",
                Content = SystemPrompt_MarketPrediction
            },
            new AiMessage
            {
                Role = "user",
                Content = $@"请预测{symbol}在{timeframe}时间框架下未来24小时的价格走势：

## 当前市场数据
{marketDataSummary}

请提供详细的分析和预测。"}
        };

        return messages;
    }

    /// <summary>
    /// 构建包含上下文的用户提示词
    /// </summary>
    /// <param name="userQuery">用户查询</param>
    /// <param name="contextCode">上下文代码</param>
    /// <returns>构建后的用户提示词</returns>
    private string BuildUserPromptWithContext(string userQuery, string? contextCode)
    {
        if (string.IsNullOrWhiteSpace(contextCode))
        {
            return userQuery;
        }

        return $@"{userQuery}

## 当前策略代码
```dsl
{contextCode}
```";
    }

    /// <summary>
    /// 生成回测结果摘要
    /// </summary>
    /// <param name="result">回测结果</param>
    /// <returns>格式化的回测结果摘要</returns>
    public string GenerateBacktestSummary(Backtest.Models.BacktestResult result)
    {
        return $@"- 策略名称：{result.StrategyName}
- 交易对：{result.Symbol}
- 时间周期：{result.Interval}
- 回测时间段：{result.StartTime} 至 {result.EndTime}
- 初始资金：{result.InitialCapital}
- 最终权益：{result.FinalEquity}
- 总收益率：{result.TotalReturn:P2}
- 年化收益率：{result.AnnualizedReturn:P2}
- 最大回撤：{result.MaxDrawdown:P2}
- 夏普比率：{result.SharpeRatio:F2}
- 总交易次数：{result.TotalTrades}
- 盈利次数：{result.WinningTrades}
- 亏损次数：{result.LosingTrades}
- 胜率：{result.WinRate:P2}
- 平均盈亏比：{result.ProfitFactor:F2}
- 平均持仓时间：{result.AvgHoldingTime}";
    }

    #endregion
}