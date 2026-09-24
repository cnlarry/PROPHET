using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Prophet.Client.Services.Editor.Formatting;

/// <summary>
/// 格式化规则配置数据模型
/// </summary>
public class FormatRules
{
    [JsonPropertyName("indentation")]
    public IndentationRules? Indentation { get; set; }

    [JsonPropertyName("spacing")]
    public SpacingRules? Spacing { get; set; }

    [JsonPropertyName("braces")]
    public BracesRules? Braces { get; set; }

    [JsonPropertyName("lineBreaks")]
    public LineBreaksRules? LineBreaks { get; set; }

    [JsonPropertyName("alignment")]
    public AlignmentRules? Alignment { get; set; }

    [JsonPropertyName("wrapping")]
    public WrappingRules? Wrapping { get; set; }

    [JsonPropertyName("comments")]
    public CommentsRules? Comments { get; set; }

    [JsonPropertyName("customRules")]
    public List<CustomRule>? CustomRules { get; set; }
}

/// <summary>
/// 缩进规则
/// </summary>
public class IndentationRules
{
    [JsonPropertyName("size")]
    public int Size { get; set; } = 4;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "spaces";
}

/// <summary>
/// 空格规则
/// </summary>
public class SpacingRules
{
    [JsonPropertyName("beforeOpenBrace")]
    public bool BeforeOpenBrace { get; set; } = true;

    [JsonPropertyName("afterOpenBrace")]
    public bool AfterOpenBrace { get; set; } = false;

    [JsonPropertyName("beforeCloseBrace")]
    public bool BeforeCloseBrace { get; set; } = false;

    [JsonPropertyName("afterCloseBrace")]
    public bool AfterCloseBrace { get; set; } = false;

    [JsonPropertyName("beforeSemicolon")]
    public bool BeforeSemicolon { get; set; } = false;

    [JsonPropertyName("afterSemicolon")]
    public bool AfterSemicolon { get; set; } = true;

    [JsonPropertyName("beforeComma")]
    public bool BeforeComma { get; set; } = false;

    [JsonPropertyName("afterComma")]
    public bool AfterComma { get; set; } = true;

    [JsonPropertyName("beforeColon")]
    public bool BeforeColon { get; set; } = false;

    [JsonPropertyName("afterColon")]
    public bool AfterColon { get; set; } = true;

    [JsonPropertyName("aroundOperators")]
    public bool AroundOperators { get; set; } = true;

    [JsonPropertyName("aroundAssignment")]
    public bool AroundAssignment { get; set; } = true;

    [JsonPropertyName("insideParentheses")]
    public bool InsideParentheses { get; set; } = false;
}

/// <summary>
/// 花括号规则
/// </summary>
public class BracesRules
{
    [JsonPropertyName("openingOnSameLine")]
    public bool OpeningOnSameLine { get; set; } = true;

    [JsonPropertyName("closingOnNewLine")]
    public bool ClosingOnNewLine { get; set; } = true;

    [JsonPropertyName("emptyBlockOnOneLine")]
    public bool EmptyBlockOnOneLine { get; set; } = false;
}

/// <summary>
/// 换行规则
/// </summary>
public class LineBreaksRules
{
    [JsonPropertyName("maxEmptyLines")]
    public int MaxEmptyLines { get; set; } = 1;

    [JsonPropertyName("beforeBlockComment")]
    public bool BeforeBlockComment { get; set; } = true;

    [JsonPropertyName("afterBlockComment")]
    public bool AfterBlockComment { get; set; } = false;

    [JsonPropertyName("beforeSignal")]
    public bool BeforeSignal { get; set; } = false;

    [JsonPropertyName("afterSignal")]
    public bool AfterSignal { get; set; } = true;

    [JsonPropertyName("betweenConditions")]
    public bool BetweenConditions { get; set; } = false;
}

/// <summary>
/// 对齐规则
/// </summary>
public class AlignmentRules
{
    [JsonPropertyName("alignComments")]
    public bool AlignComments { get; set; } = true;

    [JsonPropertyName("alignAssignments")]
    public bool AlignAssignments { get; set; } = false;

    [JsonPropertyName("alignParameters")]
    public bool AlignParameters { get; set; } = false;
}

/// <summary>
/// 换行策略规则
/// </summary>
public class WrappingRules
{
    [JsonPropertyName("maxLineLength")]
    public int MaxLineLength { get; set; } = 120;

    [JsonPropertyName("wrapLongConditions")]
    public bool WrapLongConditions { get; set; } = true;

    [JsonPropertyName("wrapOperator")]
    public string WrapOperator { get; set; } = "end";
}

/// <summary>
/// 注释规则
/// </summary>
public class CommentsRules
{
    [JsonPropertyName("preserveFormat")]
    public bool PreserveFormat { get; set; } = true;

    [JsonPropertyName("addSpaceAfterSlash")]
    public bool AddSpaceAfterSlash { get; set; } = true;

    [JsonPropertyName("alignInlineComments")]
    public bool AlignInlineComments { get; set; } = false;
}

/// <summary>
/// 自定义规则
/// </summary>
public class CustomRule
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("pattern")]
    public string? Pattern { get; set; }

    [JsonPropertyName("action")]
    public string? Action { get; set; }

    [JsonPropertyName("level")]
    public int? Level { get; set; }
}

