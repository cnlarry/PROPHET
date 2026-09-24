using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace Prophet.Client.Controls;

public partial class ParameterHint : UserControl
{
    private TextBlock? _functionNameText;
    private TextBlock? _parametersText;
    private TextBlock? _descriptionText;

    public ParameterHint()
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
        _functionNameText = this.FindControl<TextBlock>("FunctionNameText");
        _parametersText = this.FindControl<TextBlock>("ParametersText");
        _descriptionText = this.FindControl<TextBlock>("DescriptionText");
        
        // 确保 ParametersText 没有默认 Foreground，让 Inlines 的颜色生效
        if (_parametersText != null)
        {
            _parametersText.Foreground = null;
        }
    }

    public void SetParameters(string functionName, List<ParameterInfo> parameters, int currentParameterIndex, string? description = null)
    {
        if (_functionNameText != null)
        {
            _functionNameText.Text = functionName;
        }

        if (_parametersText != null)
        {
            // 使用 Inlines 来设置不同颜色
            if (_parametersText.Inlines == null)
            {
                _parametersText.Inlines = new InlineCollection();
            }
            _parametersText.Inlines.Clear();
            
            for (int i = 0; i < parameters.Count; i++)
            {
                var p = parameters[i];
                // 如果有描述，显示描述；否则显示类型
                var descriptionOrType = !string.IsNullOrEmpty(p.Description)
                    ? p.Description
                    : $"({p.Type})";
                
                // 如果不是第一个参数，添加换行
                if (i > 0)
                {
                    _parametersText.Inlines.Add(new Run("\n"));
                }
                
                // 参数名（当前参数高亮）
                var isCurrent = i == currentParameterIndex;
                var nameColor = isCurrent 
                    ? new SolidColorBrush(Color.Parse("#FFDCDCAA")) // 高亮当前参数
                    : new SolidColorBrush(Color.Parse("#DCDCDC"));  // 普通参数
                
                var nameRun = new Run($"{p.Name}: ")
                {
                    Foreground = nameColor
                };
                _parametersText.Inlines.Add(nameRun);
                
                // 描述或类型
                var descRun = new Run(descriptionOrType)
                {
                    Foreground = new SolidColorBrush(Color.Parse("#CCCCCC"))
                };
                _parametersText.Inlines.Add(descRun);
            }
        }

        if (_descriptionText != null)
        {
            if (!string.IsNullOrEmpty(description))
            {
                _descriptionText.Text = description;
                _descriptionText.IsVisible = true;
            }
            else
            {
                _descriptionText.IsVisible = false;
            }
        }
    }
}

public class ParameterInfo
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string? DefaultValue { get; set; }
    public string? Description { get; set; }
}

public class ParameterViewModel : INotifyPropertyChanged
{
    private bool _isFirst;
    private bool _isCurrent;
    private bool _hasDescription;
    private IBrush _nameForeground = Brushes.White;

    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string? DefaultValue { get; set; }
    public string? Description { get; set; }
    public string DescriptionOrType { get; set; } = "";
    public bool HasDefaultValue { get; set; }
    
    public bool IsFirst 
    { 
        get => _isFirst;
        set
        {
            _isFirst = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsNotFirst));
        }
    }
    
    public bool IsNotFirst => !_isFirst;
    
    public bool IsCurrent
    {
        get => _isCurrent;
        set
        {
            _isCurrent = value;
            OnPropertyChanged();
        }
    }
    
    public bool HasDescription
    {
        get => _hasDescription;
        set
        {
            _hasDescription = value;
            OnPropertyChanged();
        }
    }
    
    public IBrush NameForeground 
    { 
        get => _nameForeground;
        set
        {
            _nameForeground = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

