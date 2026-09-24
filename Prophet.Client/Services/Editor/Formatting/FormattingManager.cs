using System;
using AvaloniaEdit;

namespace Prophet.Client.Services.Editor.Formatting;

/// <summary>
/// 代码格式化管理器
/// </summary>
/// <remarks>
/// <para>
/// 负责 Prophet DSL 代码的格式化，包括缩进、空格、换行等。
/// 提供智能代码格式化，优化代码可读性。
/// </para>
/// <para>
/// 格式化规则：
/// <list type="bullet">
/// <item><description>自动缩进：根据代码块层级自动缩进</description></item>
/// <item><description>空格规范：运算符前后添加空格</description></item>
/// <item><description>行分隔：关键字句前后添加空行</description></item>
/// <item><description>括号配对：自动对齐括号</description></item>
/// </list>
/// </para>
/// </remarks>
public class FormattingManager
{
    private readonly TextEditor _editor;

    /// <summary>
    /// 初始化 <see cref="FormattingManager"/> 类的新实例
    /// </summary>
    /// <param name="editor">文本编辑器实例</param>
    public FormattingManager(TextEditor editor)
    {
        _editor = editor;
    }

    /// <summary>
    /// 格式化代码
    /// </summary>
    /// <remarks>
    /// <para>
    /// 对编辑器中的代码进行格式化，包括：
    /// <list type="bullet">
    /// <item><description>运算符前后添加空格</description></item>
    /// <item><description>逗号后添加空格</description></item>
    /// <item><description>移除多余空格</description></item>
    /// <item><description>统一换行符</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// 格式化完成后会自动触发语法验证。
    /// </para>
    /// </remarks>
    public void FormatCode()
    {
        try
        {
            var code = _editor.Text;
            var formatted = DSLFormatter.Format(code);

            if (formatted != code)
            {
                _editor.Document.BeginUpdate();
                _editor.Document.Text = formatted;
                _editor.Document.EndUpdate();
                
                Console.WriteLine("[FormattingManager] Code formatted successfully");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FormattingManager] Format error: {ex.Message}");
        }
    }
}

