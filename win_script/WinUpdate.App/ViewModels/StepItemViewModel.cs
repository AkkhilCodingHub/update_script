using WinUpdate.Engine;

namespace WinUpdate.App.ViewModels;

public class StepItemViewModel : ViewModelBase
{
    private readonly IUpdateStep _step;
    private StepStatus _status = StepStatus.Pending;
    private bool _isAvailable;
    private bool _isSelected = true;
    private TimeSpan _duration = TimeSpan.Zero;
    private string _message = string.Empty;

    public StepItemViewModel(IUpdateStep step)
    {
        _step = step;
    }

    public IUpdateStep Step => _step;
    public string Id => _step.Id;
    public string Name => _step.Name;
    public string Description => _step.Description;
    public StepCategory Category => _step.Category;
    public int Order => _step.Order;

    public bool IsAvailable
    {
        get => _isAvailable;
        set => SetProperty(ref _isAvailable, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public StepStatus Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(StatusDisplay));
                OnPropertyChanged(nameof(IsRunning));
            }
        }
    }

    public bool IsRunning => _status == StepStatus.Running;

    public TimeSpan Duration
    {
        get => _duration;
        set
        {
            if (SetProperty(ref _duration, value))
            {
                OnPropertyChanged(nameof(DurationDisplay));
            }
        }
    }

    public string DurationDisplay => _duration == TimeSpan.Zero ? string.Empty : $"{_duration.TotalSeconds:F1}s";

    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public string StatusDisplay => _status switch
    {
        StepStatus.Pending => "Pending",
        StepStatus.Running => "Running...",
        StepStatus.Success => "Updated",
        StepStatus.Warning => "Warning",
        StepStatus.Failed => "Failed",
        StepStatus.Skipped => "Skipped",
        _ => _status.ToString()
    };

    public void Reset()
    {
        Status = StepStatus.Pending;
        Duration = TimeSpan.Zero;
        Message = string.Empty;
    }
}
