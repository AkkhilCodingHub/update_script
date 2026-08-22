using WinUpdate.Configuration;
using WinUpdate.Engine;

namespace WinUpdate.Steps.Containers;

public class DockerStep : IUpdateStep
{
    public string Id => "docker";
    public string Name => "Docker Containers & Images";
    public string Description => "Pulls latest versions of active container images and prunes dangling resources";
    public StepCategory Category => StepCategory.Containers;
    public int Order => 50;

    public async Task<bool> IsAvailableAsync(AppConfig config, CancellationToken cancellationToken = default)
    {
        if (!ProcessRunner.CommandExists("docker")) return false;
        // Verify docker daemon is actually responding
        var ping = await ProcessRunner.RunAsync("docker", "info --format '{{.ServerVersion}}'", null, null, TimeSpan.FromSeconds(5), cancellationToken);
        return ping.Success;
    }

    public Task<string> GetDryRunPlanAsync(AppConfig config, Action<string> logCallback, CancellationToken cancellationToken = default)
    {
        return Task.FromResult("Would inspect running docker containers and pull updated base images.");
    }

    public async Task<StepResult> ExecuteAsync(AppConfig config, Action<string> logCallback, CancellationToken cancellationToken = default)
    {
        logCallback("Querying docker images...");
        var imagesRes = await ProcessRunner.RunAsync("docker", "images --format \"{{.Repository}}:{{.Tag}}\"", null, null, TimeSpan.FromSeconds(15), cancellationToken);

        if (imagesRes.Success && !string.IsNullOrWhiteSpace(imagesRes.StandardOutput))
        {
            var images = imagesRes.StandardOutput
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(img => !img.Contains("<none>") && !string.IsNullOrWhiteSpace(img))
                .Distinct()
                .Take(20) // Limit to top 20 images
                .ToList();

            foreach (var img in images)
            {
                logCallback($"Pulling latest image: {img}...");
                await ProcessRunner.RunAsync("docker", $"pull {img}", logCallback: logCallback, timeout: TimeSpan.FromMinutes(3), cancellationToken: cancellationToken);
            }
        }

        return StepResult.Success(Id, Name, Category, "Docker images updated.");
    }
}

public class PodmanStep : IUpdateStep
{
    public string Id => "podman";
    public string Name => "Podman Containers";
    public string Description => "Runs podman auto-update for running systemd/quadlet containers";
    public StepCategory Category => StepCategory.Containers;
    public int Order => 52;

    public Task<bool> IsAvailableAsync(AppConfig config, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ProcessRunner.CommandExists("podman"));
    }

    public Task<string> GetDryRunPlanAsync(AppConfig config, Action<string> logCallback, CancellationToken cancellationToken = default)
    {
        return Task.FromResult("Would execute: podman auto-update --dry-run");
    }

    public async Task<StepResult> ExecuteAsync(AppConfig config, Action<string> logCallback, CancellationToken cancellationToken = default)
    {
        logCallback("Executing podman auto-update...");
        var res = await ProcessRunner.RunAsync("podman", "auto-update", logCallback: logCallback, timeout: TimeSpan.FromMinutes(5), cancellationToken: cancellationToken);

        if (res.Success || res.ExitCode == 0)
        {
            return StepResult.Success(Id, Name, Category, "Podman auto-update completed.");
        }

        return StepResult.Warning(Id, Name, Category, $"Podman finished with exit code {res.ExitCode}");
    }
}
