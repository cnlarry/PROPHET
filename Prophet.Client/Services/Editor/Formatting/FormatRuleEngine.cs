using System;
using System.IO;
using System.Text.Json;
using Avalonia.Platform;

namespace Prophet.Client.Services.Editor.Formatting;

/// <summary>
/// 格式化规则引擎 - 加载和管理格式化规则配置
/// </summary>
public class FormatRuleEngine
{
    private static FormatRuleEngine? _instance;
    private FormatRules? _rules;

    public static FormatRuleEngine Instance => _instance ??= new FormatRuleEngine();

    private FormatRuleEngine()
    {
        LoadRules();
    }

    /// <summary>
    /// 获取格式化规则
    /// </summary>
    public FormatRules GetRules()
    {
        return _rules ?? CreateDefaultRules();
    }

    /// <summary>
    /// 加载格式化规则配置
    /// </summary>
    private void LoadRules()
    {
        try
        {
            // 从资源文件加载 FormatRules.json
            var uri = new Uri("avares://Prophet.Client/Resources/FormatRules.json");
            using var stream = AssetLoader.Open(uri);
            
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };
            
            _rules = JsonSerializer.Deserialize<FormatRules>(stream, options);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ 加载格式化规则失败: {ex.Message}");
            _rules = CreateDefaultRules();
        }
    }

    /// <summary>
    /// 创建默认规则（如果加载失败）
    /// </summary>
    private FormatRules CreateDefaultRules()
    {
        return new FormatRules
        {
            Indentation = new IndentationRules { Size = 4, Type = "spaces" },
            Spacing = new SpacingRules
            {
                BeforeOpenBrace = true,
                AfterOpenBrace = false,
                BeforeCloseBrace = false,
                AfterCloseBrace = false,
                BeforeSemicolon = false,
                AfterSemicolon = true,
                BeforeComma = false,
                AfterComma = true,
                BeforeColon = false,
                AfterColon = true,
                AroundOperators = true,
                AroundAssignment = true,
                InsideParentheses = false
            },
            Braces = new BracesRules
            {
                OpeningOnSameLine = true,
                ClosingOnNewLine = true,
                EmptyBlockOnOneLine = false
            },
            LineBreaks = new LineBreaksRules
            {
                MaxEmptyLines = 1,
                BeforeBlockComment = true,
                AfterBlockComment = false,
                BeforeSignal = false,
                AfterSignal = true,
                BetweenConditions = false
            },
            Alignment = new AlignmentRules
            {
                AlignComments = true,
                AlignAssignments = false,
                AlignParameters = false
            },
            Wrapping = new WrappingRules
            {
                MaxLineLength = 120,
                WrapLongConditions = true,
                WrapOperator = "end"
            },
            Comments = new CommentsRules
            {
                PreserveFormat = true,
                AddSpaceAfterSlash = true,
                AlignInlineComments = false
            }
        };
    }
}

