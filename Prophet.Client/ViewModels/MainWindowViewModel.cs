using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Prophet.Client.ViewModels;

public class MainWindowViewModel : INotifyPropertyChanged
{
    private string _title = "Prophet Trading Platform";

    public string Title
    {
        get => _title;
        set
        {
            if (_title != value)
            {
                _title = value;
                OnPropertyChanged();
            }
        }
    }

    public MainWindowViewModel()
    {
        // 构造函数 - 将来在这里初始化数据
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

