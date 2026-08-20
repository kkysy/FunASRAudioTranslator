using System.Diagnostics;
using System.IO;
using System.Net.Http;

namespace LiveCaptionsTranslator.asr
{
    public sealed class FunAsrRuntimeManager : IDisposable
    {
        private readonly object stateLock = new();
        private readonly HttpClient client = new();
        private Process? ownedProcess;
        private CancellationTokenSource? startupCancellation;
        private string status = "FunASR service is stopped.";
        private string lastRuntimeLog = string.Empty;
        private bool disposed;

        public event Action<string>? StatusChanged;

        public string Status
        {
            get
            {
                lock (stateLock)
                    return status;
            }
        }

        public bool OwnsRuntime => ownedProcess is { HasExited: false };

        public async Task<bool> EnsureStartedAsync(string serverUrl)
        {
            ObjectDisposedException.ThrowIf(disposed, this);

            if (await ProbeAsync(serverUrl, TimeSpan.FromSeconds(2)))
            {
                SetStatus("Connected to existing FunASR service.");
                return true;
            }

            if (!IsLocalServer(serverUrl))
            {
                SetStatus("Remote FunASR is unavailable. Automatic startup is only supported for localhost.");
                return false;
            }

            string? pythonPath = FindPython();
            string? serverScript = FindServerScript();
            string? modelPath = FindAsrModel();
            string? vadModelPath = FindVadModel();

            var missing = new List<string>();
            if (pythonPath == null)
                missing.Add("Python environment");
            if (serverScript == null)
                missing.Add("fun_asr_server.py");
            if (modelPath == null)
                missing.Add("complete Fun-ASR-Nano model");
            if (vadModelPath == null)
                missing.Add("FSMN-VAD model");

            if (missing.Count > 0)
            {
                SetStatus($"Cannot auto-start FunASR: missing {string.Join(", ", missing)}.");
                return false;
            }

            StopOwnedProcess();
            startupCancellation?.Cancel();
            startupCancellation?.Dispose();
            startupCancellation = new CancellationTokenSource();

            Uri uri = new(serverUrl);
            int port = uri.IsDefaultPort ? 8177 : uri.Port;
            string host = uri.Host is "localhost" or "::1" ? "127.0.0.1" : uri.Host;

            var startInfo = new ProcessStartInfo
            {
                FileName = pythonPath,
                WorkingDirectory = Path.GetDirectoryName(serverScript)!,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            startInfo.ArgumentList.Add(serverScript);
            startInfo.ArgumentList.Add("--host");
            startInfo.ArgumentList.Add(host);
            startInfo.ArgumentList.Add("--port");
            startInfo.ArgumentList.Add(port.ToString());
            startInfo.ArgumentList.Add("--model");
            startInfo.ArgumentList.Add(modelPath);
            startInfo.ArgumentList.Add("--device");
            startInfo.ArgumentList.Add(Environment.GetEnvironmentVariable("FUN_ASR_DEVICE") ?? "cuda:0");
            startInfo.ArgumentList.Add("--hub");
            startInfo.ArgumentList.Add("hf");
            startInfo.ArgumentList.Add("--vad-model");
            startInfo.ArgumentList.Add(vadModelPath);
            startInfo.ArgumentList.Add("--disable-update");

            try
            {
                ownedProcess = new Process
                {
                    StartInfo = startInfo,
                    EnableRaisingEvents = true
                };
                ownedProcess.OutputDataReceived += OnRuntimeOutput;
                ownedProcess.ErrorDataReceived += OnRuntimeOutput;
                ownedProcess.Exited += OnRuntimeExited;

                SetStatus("Starting FunASR and loading the local model...");
                if (!ownedProcess.Start())
                    throw new InvalidOperationException("Python process could not be started.");

                ownedProcess.BeginOutputReadLine();
                ownedProcess.BeginErrorReadLine();

                using var startupTimeout = CancellationTokenSource.CreateLinkedTokenSource(
                    startupCancellation.Token);
                startupTimeout.CancelAfter(TimeSpan.FromMinutes(10));

                while (!startupTimeout.IsCancellationRequested)
                {
                    if (ownedProcess.HasExited)
                    {
                        SetStatus($"FunASR exited during startup. {lastRuntimeLog}".Trim());
                        return false;
                    }

                    if (await ProbeAsync(serverUrl, TimeSpan.FromSeconds(2)))
                    {
                        SetStatus("FunASR model loaded and ready.");
                        return true;
                    }

                    await Task.Delay(1000, startupTimeout.Token);
                }
            }
            catch (OperationCanceledException)
            {
                if (startupCancellation?.IsCancellationRequested == true)
                    return false;
                SetStatus("FunASR model loading timed out.");
            }
            catch (Exception ex)
            {
                SetStatus($"Failed to start FunASR: {ex.Message}");
            }

            StopOwnedProcess();
            return false;
        }

        public async Task<bool> ProbeAsync(string serverUrl, TimeSpan timeout)
        {
            try
            {
                using var cancellation = new CancellationTokenSource(timeout);
                using HttpResponseMessage response = await client.GetAsync(
                    serverUrl.TrimEnd('/') + "/health",
                    cancellation.Token);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public void StopOwnedProcess()
        {
            startupCancellation?.Cancel();

            Process? process = ownedProcess;
            ownedProcess = null;
            if (process == null)
                return;

            process.OutputDataReceived -= OnRuntimeOutput;
            process.ErrorDataReceived -= OnRuntimeOutput;
            process.Exited -= OnRuntimeExited;
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5000);
                }
            }
            catch
            {
            }
            finally
            {
                process.Dispose();
            }

            SetStatus("FunASR service stopped; model memory released.");
        }

        private void OnRuntimeOutput(object sender, DataReceivedEventArgs args)
        {
            if (string.IsNullOrWhiteSpace(args.Data))
                return;

            lastRuntimeLog = args.Data.Trim();
            if (lastRuntimeLog.Contains("loading", StringComparison.OrdinalIgnoreCase))
                SetStatus("Loading FunASR model into GPU memory...");
            else if (lastRuntimeLog.Contains("listening on", StringComparison.OrdinalIgnoreCase))
                SetStatus("FunASR service is ready.");
        }

        private void OnRuntimeExited(object? sender, EventArgs args)
        {
            if (ReferenceEquals(sender, ownedProcess))
                SetStatus($"FunASR service exited. {lastRuntimeLog}".Trim());
        }

        private static bool IsLocalServer(string serverUrl)
        {
            return Uri.TryCreate(serverUrl, UriKind.Absolute, out Uri? uri) &&
                   uri.Host is "127.0.0.1" or "localhost" or "::1";
        }

        private static string? FindPython()
        {
            string applicationDirectory = GetApplicationDirectory();
            return FirstExistingFile(
                Environment.GetEnvironmentVariable("FUN_ASR_PYTHON"),
                Path.Combine(applicationDirectory, "funasr", ".venv", "Scripts", "python.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "LiveCaptionsTranslator", "FunASR", ".venv", "Scripts", "python.exe"));
        }

        private static string? FindServerScript()
        {
            string applicationDirectory = GetApplicationDirectory();
            return FirstExistingFile(
                Environment.GetEnvironmentVariable("FUN_ASR_SERVER"),
                Path.Combine(applicationDirectory, "fun_asr_server.py"),
                Path.Combine(applicationDirectory, "funasr", "fun_asr_server.py"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "LiveCaptionsTranslator", "FunASR", "fun_asr_server.py"));
        }

        private static string GetApplicationDirectory()
        {
            string? executablePath = Environment.ProcessPath;
            return string.IsNullOrWhiteSpace(executablePath)
                ? AppContext.BaseDirectory
                : Path.GetDirectoryName(executablePath) ?? AppContext.BaseDirectory;
        }

        private static string? FindAsrModel()
        {
            string? configured = Environment.GetEnvironmentVariable("FUN_ASR_MODEL");
            if (IsCompleteAsrModel(configured))
                return configured;

            string root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".cache", "huggingface", "hub",
                "models--FunAudioLLM--Fun-ASR-Nano-2512", "snapshots");
            if (!Directory.Exists(root))
                return null;

            return Directory.EnumerateDirectories(root)
                .Where(IsCompleteAsrModel)
                .OrderByDescending(path => new FileInfo(Path.Combine(path, "model.pt")).Length)
                .FirstOrDefault();
        }

        private static string? FindVadModel()
        {
            string? configured = Environment.GetEnvironmentVariable("FUN_ASR_VAD_MODEL");
            if (IsCompleteVadModel(configured))
                return configured;

            string root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".cache", "huggingface", "hub",
                "models--funasr--fsmn-vad", "snapshots");
            if (!Directory.Exists(root))
                return null;

            return Directory.EnumerateDirectories(root)
                .Where(IsCompleteVadModel)
                .OrderByDescending(Directory.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }

        private static bool IsCompleteAsrModel(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;
            string modelFile = Path.Combine(path, "model.pt");
            return File.Exists(modelFile) && new FileInfo(modelFile).Length > 1_000_000_000;
        }

        private static bool IsCompleteVadModel(string? path)
        {
            return !string.IsNullOrWhiteSpace(path) &&
                   File.Exists(Path.Combine(path, "model.pt"));
        }

        private static string? FirstExistingFile(params string?[] candidates)
        {
            return candidates.FirstOrDefault(path =>
                !string.IsNullOrWhiteSpace(path) && File.Exists(path));
        }

        private void SetStatus(string value)
        {
            lock (stateLock)
                status = value;
            StatusChanged?.Invoke(value);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            StopOwnedProcess();
            startupCancellation?.Dispose();
            client.Dispose();
        }
    }
}
