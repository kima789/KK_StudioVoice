using HarmonyLib;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KK_StudioVoice
{
    [HarmonyPatch(typeof(FBSCtrlMouth), "CalcMouthOpen")]
    public static class MouthPatch
    {
        public static readonly Dictionary<ChaControl, float> MouthValues = new Dictionary<ChaControl, float>();

        public static void Postfix(FBSCtrlMouth __instance, ref float __result)
        {
            // インスタンスから該当するキャラクターを探す
            var cha = MouthValues.Keys.FirstOrDefault(c => c != null && c.fbsCtrl.MouthCtrl == __instance);
            if (cha != null)
            {
                float val = MouthValues[cha];
                if (val > 0f) __result = val;
            }
        }
    }

    public class LipSyncController : MonoBehaviour
    {
        // これが「Instanceがない」エラーの解決策
        public static LipSyncController Instance { get; private set; }
        private class LipSyncState
        {
            public Coroutine Coroutine;
            public AudioSource Source;
        }
        private Dictionary<ChaControl, LipSyncState> _states = new Dictionary<ChaControl, LipSyncState>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);

                // Harmonyパッチ適用
                var harmony = new Harmony("com.yourname.kk_voice_lipsync");
                harmony.PatchAll();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void StartLipSync(ChaControl cha, AudioSource source)
        {
            if (cha == null) return;

            if (!_states.TryGetValue(cha, out var state))
            {
                state = new LipSyncState();
                state.Source = source;
                state.Coroutine = StartCoroutine(LipSyncRoutine(cha, state));
                _states[cha] = state;
            }
            else
            {
                // ★ここが重要：コルーチンは再利用
                state.Source = source;
            }
        }
        private IEnumerator LipSyncRoutine(ChaControl cha, LipSyncState state)
        {
            float[] samples = new float[128];
            float lastValue = 0f;

            bool wasPlaying = false;
            bool wasLipSyncOn = false;

            while (cha != null)
            {
                var chara = cha.GetComponent<VoiceCharComponent>();

                // =========================
                // OFF状態
                // =========================
                if (chara != null && !chara.IsLipSyncOn)
                {
                    if (wasLipSyncOn)
                    {
                        // ★この瞬間だけ解除する
                        if (cha.fbsCtrl?.MouthCtrl != null)
                            cha.fbsCtrl.MouthCtrl.SetFixedRate(-1f);

                        MouthPatch.MouthValues.Remove(cha);

                        wasLipSyncOn = false;
                    }
                    yield return null;
                    continue;
                }
                else
                {
                    wasLipSyncOn = true;
                }
                var source = state.Source;

                // =========================
                // 音声なし or 停止中
                // =========================
                if (source == null || !source.isPlaying || !source.isActiveAndEnabled)
                {
                    if (wasPlaying)
                    {
                        // ★再生終了した瞬間だけ実行
                        if (cha.fbsCtrl?.MouthCtrl != null)
                            cha.fbsCtrl.MouthCtrl.SetFixedRate(-1f);

                        MouthPatch.MouthValues.Remove(cha);

                        wasPlaying = false;
                    }
                    yield return null;
                    continue;
                }
                else
                {
                    wasPlaying = true;
                }
                // =========================
                // 音量解析
                // =========================
                source.GetOutputData(samples, 0);
                float rms = Mathf.Sqrt(samples.Select(x => x * x).Average());
                float target = Mathf.Clamp01(rms * 15.0f);

                lastValue = Mathf.Lerp(lastValue, target, Time.deltaTime * 20f);

                // =========================
                // 口適用
                // =========================
                if (cha.fbsCtrl?.MouthCtrl != null)
                {
                    cha.fbsCtrl.MouthCtrl.SetFixedRate(lastValue);
                }

                MouthPatch.MouthValues[cha] = lastValue;

                yield return null;
            }

            // キャラ消滅時だけ完全クリーンアップ
            if (cha != null && cha.fbsCtrl?.MouthCtrl != null)
                cha.fbsCtrl.MouthCtrl.SetFixedRate(-1f);

            MouthPatch.MouthValues.Remove(cha);
        }
    }
}