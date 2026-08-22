using WinUpdate.Engine;

namespace WinUpdate.UI;

public static class ConsoleRenderer
{
    private const string Reset = "\u001b[0m";
    private const string Bold = "\u001b[1m";
    private const string Dim = "\u001b[2m";
    private const string Cyan = "\u001b[36m";
    private const string Blue = "\u001b[34m";
    private const string Green = "\u001b[32m";
    private const string Yellow = "\u001b[33m";
    private const string Red = "\u001b[31m";
    private const string Magenta = "\u001b[35m";

    public static void RenderBanner()
    {
        Console.WriteLine();
        Console.WriteLine($"{Cyan}{Bold}==================================================================={Reset}");
        Console.WriteLine($"{Cyan}{Bold}   ⚡ WinUpdate - Native .NET Windows Orchestrator ⚡           {Reset}");
        Console.WriteLine($"{Dim}         Modular Topgrade Alternative for Windows (C# .NET 10)         {Reset}");
        Console.WriteLine($"{Cyan}{Bold}==================================================================={Reset}");
        Console.WriteLine();
    }

    public static void RenderStepStart(IUpdateStep step)
    {
        var categoryTag = $"[{step.Category}]";
        Console.WriteLine($"{Blue}{Bold}▶ [{step.Name}]{Reset} {Dim}{categoryTag} - {step.Description}{Reset}");
    }

    public static void RenderStepLog(IUpdateStep step, string logLine)
    {
        if (string.IsNullOrWhiteSpace(logLine)) return;
        Console.WriteLine($"  {Dim}│{Reset} {logLine}");
    }

    public static void RenderStepResult(IUpdateStep step, StepResult result)
    {
        var durationStr = $"{result.Duration.TotalSeconds:F1}s";
        switch (result.Status)
        {
            case StepStatus.Success:
                Console.WriteLine($"  {Green}{Bold}✔ SUCCESS{Reset} {Dim}({durationStr}){Reset} - {result.Message}");
                break;
            case StepStatus.Warning:
                Console.WriteLine($"  {Yellow}{Bold}⚠ WARNING{Reset} {Dim}({durationStr}){Reset} - {result.Message}");
                break;
            case StepStatus.Failed:
                Console.WriteLine($"  {Red}{Bold}✖ FAILED{Reset} {Dim}({durationStr}){Reset} - {result.Message}");
                break;
            case StepStatus.Skipped:
                Console.WriteLine($"  {Dim}○ SKIPPED - {result.Message}{Reset}");
                break;
        }
        Console.WriteLine();
    }

    public static void RenderSummaryTable(List<StepResult> results)
    {
        Console.WriteLine($"{Cyan}{Bold}───────────────────────────────────────────────────────────────────{Reset}");
        Console.WriteLine($"{Bold}                      EXECUTION SUMMARY                            {Reset}");
        Console.WriteLine($"{Cyan}{Bold}───────────────────────────────────────────────────────────────────{Reset}");
        Console.WriteLine($"{"Step",-32} {"Category",-16} {"Status",-12} {"Duration",-10}");
        Console.WriteLine($"{Dim}───────────────────────────────────────────────────────────────────{Reset}");

        var totalDuration = TimeSpan.Zero;
        var successCount = 0;
        var warningCount = 0;
        var failedCount = 0;
        var skippedCount = 0;

        foreach (var res in results)
        {
            totalDuration += res.Duration;

            var statusStr = res.Status switch
            {
                StepStatus.Success => $"{Green}SUCCESS{Reset}",
                StepStatus.Warning => $"{Yellow}WARNING{Reset}",
                StepStatus.Failed => $"{Red}FAILED{Reset}",
                StepStatus.Skipped => $"{Dim}SKIPPED{Reset}",
                _ => res.Status.ToString()
            };

            switch (res.Status)
            {
                case StepStatus.Success: successCount++; break;
                case StepStatus.Warning: warningCount++; break;
                case StepStatus.Failed: failedCount++; break;
                case StepStatus.Skipped: skippedCount++; break;
            }

            Console.WriteLine($"{res.StepName,-32} {res.Category,-16} {statusStr,-20} {$"{res.Duration.TotalSeconds:F1}s",-10}");
        }

        Console.WriteLine($"{Cyan}{Bold}───────────────────────────────────────────────────────────────────{Reset}");
        Console.WriteLine($"{Bold}Total Duration:{Reset} {totalDuration.TotalSeconds:F1}s | " +
                          $"{Green}Success: {successCount}{Reset} | " +
                          $"{Yellow}Warnings: {warningCount}{Reset} | " +
                          $"{Red}Failed: {failedCount}{Reset} | " +
                          $"{Dim}Skipped: {skippedCount}{Reset}");
        Console.WriteLine($"{Cyan}{Bold}==================================================================={Reset}");
        Console.WriteLine();
    }
}
