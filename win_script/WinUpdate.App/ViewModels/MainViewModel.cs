using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using WinUpdate.Configuration;
using WinUpdate.Engine;

namespace WinUpdate.App.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly AppConfig _config;
    private readonly UpdateOrchestrator _orchestrator;
    private CancellationTokenSource? _runCts;
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _stopwatch = new();

    private bool _isRunning;
    private string _statusText = "Ready to update";
    private string _activeCategory = "All";
    private string _logContent = "";
    private string _elapsedTime = "0.0s";
    private int _completedCount = 0;
    private int _totalAvailableCount = 0;
    private double _progressPercentage = 0;

    public ObservableCollection<StepItemViewModel> Steps { get; } = new();
    public ObservableCollection<string> LogLines { get; } = new();

    public string OsDescription => RuntimeInformation.OSDescription;
    public string MachineName => Environment.MachineName;
    public string UserName => Environment.UserName;
    public string FrameworkDescription => RuntimeInformation.FrameworkDescription;

    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            if (SetProperty(ref _isRunning, value))
            {
                OnPropertyChanged(nameof(IsNotRunning));
                RunAllCommand.RaiseCanExecuteChanged();
                DryRunCommand.RaiseCanExecuteChanged();
                CancelCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsNotRunning => !IsRunning;

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public string ActiveCategory
    {
        get => _activeCategory;
        set
        {
            if (SetProperty(ref _activeCategory, value))
            {
                ApplyCategoryFilter();
            }
        }
    }

    public string LogContent
    {
        get => _logContent;
        set => SetProperty(ref _logContent, value);
    }

    public string ElapsedTime
    {
        get => _elapsedTime;
        set => SetProperty(ref _elapsedTime, value);
    }

    public int CompletedCount
    {
        get => _completedCount;
        set => SetProperty(ref _completedCount, value);
    }

    public int TotalAvailableCount
    {
        get => _totalAvailableCount;
        set => SetProperty(ref _totalAvailableCount, value);
    }

    public double ProgressPercentage
    {
        get => _progressPercentage;
        set => SetProperty(ref _progressPercentage, value);
    }

    public RelayCommand RunAllCommand { get; }
    public RelayCommand DryRunCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand ClearLogsCommand { get; }
    public RelayCommand SelectAllCommand { get; }
    public RelayCommand DeselectAllCommand { get; }
    public RelayCommand FilterCategoryCommand { get; }

    public MainViewModel()
    {
        _config = AppConfig.Load();
        _orchestrator = new UpdateOrchestrator(_config);

        RunAllCommand = new RelayCommand(async () => await StartUpdateAsync(false), () => IsNotRunning);
        DryRunCommand = new RelayCommand(async () => await StartUpdateAsync(true), () => IsNotRunning);
        CancelCommand = new RelayCommand(CancelUpdate, () => IsRunning);
        ClearLogsCommand = new RelayCommand(ClearLogs);
        SelectAllCommand = new RelayCommand(() => SetAllSelected(true));
        DeselectAllCommand = new RelayCommand(() => SetAllSelected(false));
        FilterCategoryCommand = new RelayCommand(param =>
        {
            if (param is string cat) ActiveCategory = cat;
        });

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _timer.Tick += (_, _) =>
        {
            ElapsedTime = $"{_stopwatch.Elapsed.TotalSeconds:F1}s";
        };

        InitializeSteps();
    }

    private void InitializeSteps()
    {
        var allSteps = _orchestrator.GetAllSteps();
        foreach (var step in allSteps)
        {
            var item = new StepItemViewModel(step);
            Steps.Add(item);
        }

        // Check availability in background
        _ = Task.Run(async () =>
        {
            foreach (var item in Steps)
            {
                var avail = await item.Step.IsAvailableAsync(_config);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    item.IsAvailable = avail;
                    item.IsSelected = avail;
                });
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                TotalAvailableCount = Steps.Count(s => s.IsAvailable);
                ApplyCategoryFilter(); // Refresh to display only installed tools
            });
        });
    }

    private async Task StartUpdateAsync(bool dryRun)
    {
        if (IsRunning) return;

        IsRunning = true;
        _runCts = new CancellationTokenSource();
        _stopwatch.Restart();
        _timer.Start();

        CompletedCount = 0;
        ProgressPercentage = 0;
        StatusText = dryRun ? "Running Dry-Run check..." : "Executing system updates...";
        AppendLog($"[System] {(dryRun ? "Dry-Run" : "Full Update")} initiated at {DateTime.Now:T}\n");

        foreach (var s in Steps)
        {
            s.Reset();
        }

        var runConfig = AppConfig.Load();
        runConfig.DryRun = dryRun;

        var selectedIds = Steps.Where(s => s.IsSelected && s.IsAvailable).Select(s => s.Id).ToList();
        runConfig.OnlySteps = selectedIds;

        var orchestrator = new UpdateOrchestrator(runConfig);

        orchestrator.StepStarting += step =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var item = Steps.FirstOrDefault(s => s.Id == step.Id);
                if (item != null) item.Status = StepStatus.Running;
                AppendLog($"▶ [{step.Name}] Started ({step.Category})...");
            });
        };

        orchestrator.StepLog += (step, line) =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                AppendLog($"  │ {line}");
            });
        };

        orchestrator.StepCompleted += (step, result) =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var item = Steps.FirstOrDefault(s => s.Id == step.Id);
                if (item != null)
                {
                    item.Status = result.Status;
                    item.Duration = result.Duration;
                    item.Message = result.Message;
                }

                CompletedCount++;
                var total = selectedIds.Count > 0 ? selectedIds.Count : TotalAvailableCount;
                if (total > 0)
                {
                    ProgressPercentage = ((double)CompletedCount / total) * 100.0;
                }

                var statusEmoji = result.Status switch
                {
                    StepStatus.Success => "✔ SUCCESS",
                    StepStatus.Warning => "⚠ WARNING",
                    StepStatus.Failed => "✖ FAILED",
                    _ => "○ SKIPPED"
                };

                AppendLog($"  {statusEmoji} ({result.Duration.TotalSeconds:F1}s) - {result.Message}\n");
            });
        };

        try
        {
            var results = await Task.Run(() => orchestrator.RunAsync(_runCts.Token));
            StatusText = $"Completed ({results.Count(r => r.Status == StepStatus.Success)} succeeded, {results.Count(r => r.Status == StepStatus.Failed)} failed)";
            AppendLog($"===================================================================");
            AppendLog($"⚡ Finished in {_stopwatch.Elapsed.TotalSeconds:F1}s | Success: {results.Count(r => r.Status == StepStatus.Success)} | Warnings: {results.Count(r => r.Status == StepStatus.Warning)} | Failed: {results.Count(r => r.Status == StepStatus.Failed)}");
            AppendLog($"===================================================================\n");
        }
        catch (OperationCanceledException)
        {
            StatusText = "Update cancelled by user.";
            AppendLog("⚠ Update process was cancelled.");
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            AppendLog($"❌ Error: {ex.Message}");
        }
        finally
        {
            _stopwatch.Stop();
            _timer.Stop();
            IsRunning = false;
            _runCts?.Dispose();
            _runCts = null;
        }
    }

    private void CancelUpdate()
    {
        if (IsRunning && _runCts != null)
        {
            StatusText = "Cancelling...";
            AppendLog("[System] Cancellation requested...");
            _runCts.Cancel();
        }
    }

    private void ClearLogs()
    {
        LogContent = string.Empty;
        LogLines.Clear();
    }

    private void SetAllSelected(bool selected)
    {
        foreach (var s in Steps)
        {
            if (s.IsAvailable) s.IsSelected = selected;
        }
    }

    private void AppendLog(string text)
    {
        var sb = new StringBuilder(LogContent);
        sb.AppendLine(text);
        LogContent = sb.ToString();
        LogLines.Add(text);
    }

    private void ApplyCategoryFilter()
    {
        var view = CollectionViewSource.GetDefaultView(Steps);
        if (view == null) return;

        view.Filter = obj =>
        {
            if (obj is StepItemViewModel step)
            {
                if (!step.IsAvailable) return false;
                if (ActiveCategory == "All") return true;
                return step.Category.ToString().Equals(ActiveCategory, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        };
    }
}
