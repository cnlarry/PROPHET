using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Prophet.Client.Controls;

public partial class HoverTooltip : UserControl
{
    private TextBlock? _titleText;
    private Border? _separator;
    private TextBlock? _contentText;
    private Border? _exampleContainer;
    private TextBlock? _exampleText;

    public HoverTooltip()
    {
        InitializeComponent();
        FindControls();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void FindControls()
    {
        _titleText = this.FindControl<TextBlock>("TitleText");
        _separator = this.FindControl<Border>("Separator");
        _contentText = this.FindControl<TextBlock>("ContentText");
        _exampleContainer = this.FindControl<Border>("ExampleContainer");
        _exampleText = this.FindControl<TextBlock>("ExampleText");
    }

    public void SetContent(string title, string content, string? example = null)
    {
        if (_titleText != null)
            _titleText.Text = title;

        if (_contentText != null)
            _contentText.Text = content;

        if (_exampleContainer != null && _exampleText != null)
        {
            if (!string.IsNullOrEmpty(example))
            {
                _exampleContainer.IsVisible = true;
                _exampleText.Text = example;
            }
            else
            {
                _exampleContainer.IsVisible = false;
            }
        }
    }
}

