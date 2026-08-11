using BepInEx.Logging;

namespace KK_StudioVoice
{
    public static class SLog
    {
        // アセンブリ読み込み時に確実に作られる（どのタイミングでも安全に使える）
        public static readonly ManualLogSource Log = Logger.CreateLogSource("KK_StudioVoice");
    }
}