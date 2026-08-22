using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using WinUpdate.Engine;

namespace WinUpdate.App.Converters;

public class StatusToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush PendingBrush = new(Color.FromRgb(100, 116, 139)); // Slate
    private static readonly SolidColorBrush RunningBrush = new(Color.FromRgb(0, 229, 255));  // Cyan
    private static readonly SolidColorBrush SuccessBrush = new(Color.FromRgb(16, 185, 129)); // Emerald
    private static readonly SolidColorBrush WarningBrush = new(Color.FromRgb(245, 158, 11)); // Amber
    private static readonly SolidColorBrush FailedBrush = new(Color.FromRgb(239, 68, 68));   // Crimson
    private static readonly SolidColorBrush SkippedBrush = new(Color.FromRgb(71, 85, 105));  // Dark Slate

    static StatusToBrushConverter()
    {
        PendingBrush.Freeze();
        RunningBrush.Freeze();
        SuccessBrush.Freeze();
        WarningBrush.Freeze();
        FailedBrush.Freeze();
        SkippedBrush.Freeze();
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is StepStatus status)
        {
            return status switch
            {
                StepStatus.Pending => PendingBrush,
                StepStatus.Running => RunningBrush,
                StepStatus.Success => SuccessBrush,
                StepStatus.Warning => WarningBrush,
                StepStatus.Failed => FailedBrush,
                StepStatus.Skipped => SkippedBrush,
                _ => PendingBrush
            };
        }
        return PendingBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
