using System.Windows;

namespace LiveCaptionsTranslator
{
    public partial class App : Application
    {
        App()
        {
            AppDomain.CurrentDomain.ProcessExit += (_, _) => Translator.StopCaptionSource();
            Translator.Setting?.Save();
            Translator.StartCaptionSource();
            Translator.StartTranslationApiWarmup();

            Task.Run(() => Translator.SyncLoop());
            Task.Run(() => Translator.TranslateLoop());
            Task.Run(() => Translator.DisplayLoop());
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Translator.StopCaptionSource();
            base.OnExit(e);
        }
    }
}
