using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Prophet.Client.Controls;

public partial class SvgIcon : UserControl
{
    public static readonly StyledProperty<string> DataProperty =
        AvaloniaProperty.Register<SvgIcon, string>(nameof(Data), "");

    public string Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public SvgIcon()
    {
        InitializeComponent();
        
        DataProperty.Changed.AddClassHandler<SvgIcon>((sender, e) =>
        {
            if (sender.IconPath != null && e.NewValue is string data && !string.IsNullOrEmpty(data))
            {
                sender.IconPath.Data = Geometry.Parse(data);
            }
        });
    }
}

