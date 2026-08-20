namespace LiveCaptionsTranslator.models
{
    public class TranslationTaskQueue
    {
        private readonly object _lock = new object();
        private TranslationTask? activeTask;
        private TranslationTask? pendingTask;

        private (string translatedText, bool isChoke) output;
        public (string translatedText, bool isChoke) Output => output;

        public TranslationTaskQueue()
        {
            output = (string.Empty, false);
        }

        public void Enqueue(Func<CancellationToken, Task<(string, bool)>> worker, string originalText)
        {
            var newTranslationTask = new TranslationTask(worker, originalText, new CancellationTokenSource());
            lock (_lock)
            {
                // Captions are continuously revised.  Keep only the newest revision that has
                // not started, but never cancel an in-flight Ollama request: cancelling a cold
                // model load prevents Ollama from ever reaching its keep-alive state.
                pendingTask?.CTS.Cancel();
                pendingTask?.CTS.Dispose();
                pendingTask = newTranslationTask;

                if (activeTask == null)
                    StartNextLocked();
            }
        }

        private void StartNextLocked()
        {
            if (activeTask != null || pendingTask == null)
                return;

            activeTask = pendingTask;
            pendingTask = null;
            _ = ProcessTaskAsync(activeTask);
        }

        private async Task ProcessTaskAsync(TranslationTask translationTask)
        {
            try
            {
                var result = await translationTask.Worker(translationTask.CTS.Token);
                bool publish;
                lock (_lock)
                {
                    // A newer caption arrived while this request was running.  Its result is
                    // stale, so skip both display and history logging.
                    publish = ReferenceEquals(activeTask, translationTask) && pendingTask == null;
                }

                if (!publish)
                    return;

                output = result;
                var translatedText = result.Item1;

                // Log after translation.
                bool isOverwrite = await Translator.IsOverwrite(translationTask.OriginalText);
                if (!isOverwrite)
                    await Translator.AddContexts();
                await Translator.Log(translationTask.OriginalText, translatedText, isOverwrite);
            }
            catch (OperationCanceledException)
            {
                // Pending tasks are replaced before they start.  This is expected during
                // rapidly changing captions.
            }
            finally
            {
                lock (_lock)
                {
                    if (ReferenceEquals(activeTask, translationTask))
                    {
                        activeTask = null;
                        translationTask.CTS.Dispose();
                        StartNextLocked();
                    }
                }
            }
        }
    }

    public class TranslationTask
    {
        public Func<CancellationToken, Task<(string, bool)>> Worker { get; }
        public string OriginalText { get; }
        public CancellationTokenSource CTS { get; }

        public TranslationTask(Func<CancellationToken, Task<(string, bool)>> worker,
            string originalText, CancellationTokenSource cts)
        {
            Worker = worker;
            OriginalText = originalText;
            CTS = cts;
        }
    }
}
