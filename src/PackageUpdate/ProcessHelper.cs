using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

public static class ProcessHelper
{
    public static async Task<ProcessResult> RunProcessAsync(string command, string arguments, int timeout)
    {
        var result = new ProcessResult();

        using var process = new Process
        {
            StartInfo =
            {
                FileName = command,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        var outputBuilder = new StringBuilder();
        var outputCloseEvent = new TaskCompletionSource<bool>();

        process.OutputDataReceived += (s, e) =>
        {
            if (e.Data == null)
            {
                outputCloseEvent.SetResult(true);
            }
            else
            {
                outputBuilder.Append(e.Data);
            }
        };

        var errorBuilder = new StringBuilder();
        var errorCloseEvent = new TaskCompletionSource<bool>();

        process.ErrorDataReceived += (s, e) =>
        {
            if (e.Data == null)
            {
                errorCloseEvent.SetResult(true);
            }
            else
            {
                errorBuilder.Append(e.Data);
            }
        };

        var isStarted = process.Start();
        if (!isStarted)
        {
            result.ExitCode = process.ExitCode;
            return result;
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // Creates task to wait for process exit using timeout
        var waitForExit = WaitForExitAsync(process, timeout);

        // Create task to wait for process exit and closing all output streams
        var processTask = Task.WhenAll(waitForExit, outputCloseEvent.Task, errorCloseEvent.Task);

        // Waits process completion and then checks it was not completed by timeout
        if (await Task.WhenAny(Task.Delay(timeout), processTask) == processTask && waitForExit.Result)
        {
            result.ExitCode = process.ExitCode;
            result.Output = outputBuilder.ToString();
            result.Error = errorBuilder.ToString();
        }
        else
        {
            try
            {
                process.Kill();
            }
            catch
            {
                // ignored
            }
        }
        return result;
    }

    static Task<bool> WaitForExitAsync(Process process, int timeout)
    {
        return Task.Run(() => process.WaitForExit(timeout));
    }
}