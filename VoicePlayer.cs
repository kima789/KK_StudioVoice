using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace KK_StudioVoice
{
    public class VoicePlayer : MonoBehaviour
    {
        private BepInEx.Logging.ManualLogSource Logger => BepInEx.Logging.Logger.CreateLogSource("VoicePlayer");
        private readonly Dictionary<VoiceCharComponent, Coroutine> _activeCoroutines = new Dictionary<VoiceCharComponent, Coroutine>();
        private VoiceCacheManager _cache;


        public void Init(VoiceCacheManager cache) => _cache = cache;

        // --- 再生処理の入口 ---
        public void PlaySelectedVoice(
            VoiceCharComponent charaComp,
            ChaControl chara,
            string mode,
            string clip,
            string option,
            bool isExcited,
            int baseExp,
            VoiceDataLoader loader,
            bool playVoice,
            bool playBreath)
        {
            var voiceData = GetLatestVoiceData(
                charaComp,
                loader,
                chara,
                _cache.GetAllBundles(chara.chaFile.parameter.personality),
                baseExp,
                true
            );

            var breathData = GetLatestVoiceData(
                charaComp,
                loader,
                chara,
                _cache.GetAllBundles(chara.chaFile.parameter.personality),
                baseExp,
                false
            );

            // ★どちらも無ければ再生しない
            if (voiceData == null && breathData == null)
                return;

            bool requestHasVoice =
                playVoice &&
                voiceData != null &&
                !string.IsNullOrEmpty(voiceData.VoiceID);

            bool modeChanged = (charaComp.LastMode ?? "") != (mode ?? "");
            charaComp.LastMode = mode;

            bool clipChanged = (charaComp.LastClip ?? "") != (clip ?? "");
            charaComp.LastClip = clip;

            bool optionChanged = (charaComp.LastOption ?? "None") != (option ?? "None");
            charaComp.LastOption = option;

            bool expChanged = charaComp.LastExp != baseExp;
            charaComp.LastExp = baseExp;

            bool excitedChanged = charaComp.LastExcited != isExcited;
            charaComp.LastExcited = isExcited;

            Action nextPlayAction = () =>
            {
                if (_activeCoroutines.TryGetValue(charaComp, out var routine))
                {
                    StopCoroutine(_activeCoroutines[charaComp]);
                    _activeCoroutines.Remove(charaComp);
                }
                var co = StartCoroutine(VoiceSequence(
                    charaComp,
                    chara,
                    voiceData,
                    breathData,
                    baseExp,
                    playVoice,
                    playBreath,
                    loader));

                _activeCoroutines[charaComp] = co;
            };
            // NotLoop中（完全ロック）
            if (charaComp.IsCurrentNotLoop)
            {
                charaComp.ReservedNextAction = nextPlayAction;
                return;
            }
            // ボイス再生中
            if (charaComp.IsVoicePlaying)
            {
                bool isNotLoopRequest =
                        playBreath &&
                        breathData != null &&
                        (breathData.NotLoop ?? "").Trim() == "1";

                bool shouldOverrideVoice =
                    isNotLoopRequest ||
                    (
                        requestHasVoice &&
                        (
                            expChanged ||
                            optionChanged ||
                            modeChanged ||
                            clipChanged ||
                            excitedChanged
                        )
                    );
                if (shouldOverrideVoice)
                {
                    // ★ 古い予約を破棄
                    charaComp.ReservedNextAction = null;

                    StopVoice(charaComp);
                    nextPlayAction();
                }
                else
                {
                    charaComp.ReservedNextAction = nextPlayAction;
                }
                return;
            }            
            // ブレス中 or 再生中
            if (charaComp.IsPlaying)
            {
                // ★ 古い予約を破棄
                charaComp.ReservedNextAction = null;

                StopVoice(charaComp);
                nextPlayAction();
                return;
            }
            // 停止中
            nextPlayAction();
        }
        // --- 実際の再生シーケンス ---
        private IEnumerator VoiceSequence(
            VoiceCharComponent charaComp,
            ChaControl chara,
            VoiceData voiceData,
            VoiceData breathData,
            int baseExp,
            bool playVoice,
            bool playBreath,
            VoiceDataLoader loader)
        {
            int pId = chara.chaFile.parameter.personality;
            string pNum = $"{pId:00}";
            var bundles = _cache.GetAllBundles(pId);

            AudioSource asVoice = charaComp.GetComponent<AudioSource>();
            if (asVoice == null)
                asVoice = charaComp.gameObject.AddComponent<AudioSource>();

            Setup3DSound(asVoice);

            // ★ 両方なければ終了
            if (voiceData == null && breathData == null)
                yield break;

            // ボイス
            if (playVoice &&
                voiceData != null &&
                !string.IsNullOrEmpty(voiceData.VoiceID))
            {
                charaComp.IsVoicePlaying = true;
                charaComp.IsCurrentNotLoop = false;

                yield return StartCoroutine(PlayClipInternal(
                    charaComp, 
                    chara, 
                    asVoice, 
                    bundles,
                    voiceData.VoicePrefix, 
                    pNum, baseExp, 
                    voiceData.VoiceID,
                    true
                    ));
                while (asVoice != null && asVoice.isPlaying && charaComp.IsPlaying)
                {
                    yield return null;
                }

                charaComp.IsVoicePlaying = false;
            }
            // ブレスループ
            if (playBreath)
            {
                while (charaComp.IsPlaying)
                {
                    // ★毎回最新状態から取得
                    breathData = GetLatestVoiceData(
                        charaComp,
                        loader,
                        chara,
                        bundles,
                        charaComp.LastExp,
                        false
                    );
                    if (breathData == null)
                        yield break;

                    bool isNotLoop =
                        (breathData.NotLoop ?? "").Trim() == "1";

                    charaComp.IsCurrentNotLoop = isNotLoop;

                    yield return StartCoroutine(PlayClipInternal(
                        charaComp,
                        chara,
                        asVoice,
                        bundles,
                        breathData.BreathPrefix,
                        pNum,
                        charaComp.LastExp,
                        breathData.BreathID,
                        false));

                    // NotLoop
                    if (isNotLoop)
                    {
                        break;
                    }
                }
            }
            charaComp.IsCurrentNotLoop = false;

            // ★先に自分を管理解除
            _activeCoroutines.Remove(charaComp);

            if (charaComp.ReservedNextAction != null)
            {
                var next = charaComp.ReservedNextAction;
                charaComp.ReservedNextAction = null;

                next.Invoke();
            }
        }
        private bool ExistsClip(
            List<AssetBundle> bundles,
            string prefix,
            string pNum,
            int baseExp,
            string rawId)
        {
            var ids = ParseExtendedIDs(rawId);

            foreach (var id in ids)
            {
                foreach (var b in bundles)
                {
                    var clip = FindClip(b, prefix, pNum, baseExp, id);
                    if (clip != null)
                        return true;
                }
            }
            return false;
        }
        private VoiceData GetLatestVoiceData(
            VoiceCharComponent charaComp,
            VoiceDataLoader loader,
            ChaControl chara,
            List<AssetBundle> bundles,
            int baseExp,
            bool forVoice
            )
        {
            if (charaComp == null) return null;

            string currentMode = charaComp.SelectedMode;
            string clip = charaComp.SelectedClip;
            string option = charaComp.SelectedOption;
            bool excited = charaComp.IsExcited;

            string pNum = $"{chara.chaFile.parameter.personality:00}";

            // 1段階フォールバックのみ
            for (int i = 0; i < 2; i++)
            {
                // ★ここで順番に試す
                string[] optionsToTry = option == "None"
                    ? new[] { "None" }
                    : new[] { option, "None" };

                foreach (var opt in optionsToTry)
                {
                    // ★Excitedの2パターン取得（元とフォールバック）
                    var dataPrimary = loader.Get(currentMode, clip, opt, excited);
                    var dataFallback = loader.Get(currentMode, clip, opt, false);

                    Debug.Log($"[DEBUG②] mode:{currentMode} opt:{opt} ex:{excited} → {(dataPrimary == null ? "NULL" : "OK")}");
                    Debug.Log($"[DEBUG②] mode:{currentMode} opt:{opt} ex:false → {(dataFallback == null ? "NULL" : "OK")}");

                    // ★ Voice探索
                    if (forVoice)
                    {
                        // ===== excited 現在値 =====
                        if (dataPrimary != null &&
                            !string.IsNullOrEmpty(dataPrimary.VoiceID))
                        {
                            bool exists = ExistsClip(
                                bundles,
                                dataPrimary.VoicePrefix,
                                pNum,
                                baseExp,
                                dataPrimary.VoiceID);

                            if (exists)
                                return dataPrimary;
                        }
                        // ===== excited fallback =====
                        if (dataFallback != null &&
                            !string.IsNullOrEmpty(dataFallback.VoiceID))
                        {
                            bool exists = ExistsClip(
                                bundles,
                                dataFallback.VoicePrefix,
                                pNum,
                                baseExp,
                                dataFallback.VoiceID);

                            if (exists)
                                return dataFallback;
                        }
                    }
                    // ★ Breath探索
                    else
                    {
                        // ===== excited 現在値 =====
                        if (dataPrimary != null &&
                            !string.IsNullOrEmpty(dataPrimary.BreathID))
                        {
                            bool exists = ExistsClip(
                                bundles,
                                dataPrimary.BreathPrefix,
                                pNum,
                                baseExp,
                                dataPrimary.BreathID);

                            if (exists)
                                return dataPrimary;
                        }
                        // ===== excited fallback =====
                        if (dataFallback != null &&
                            !string.IsNullOrEmpty(dataFallback.BreathID))
                        {
                            bool exists = ExistsClip(
                                bundles,
                                dataFallback.BreathPrefix,
                                pNum,
                                baseExp,
                                dataFallback.BreathID);

                            if (exists)
                                return dataFallback;
                        }
                    }
                }
                // fallback
                currentMode = forVoice
                    ? loader.GetVoiceFallback(currentMode, clip) // 引数に clip を追加
                    : loader.GetBreathFallback(currentMode, clip); // 引数に clip を追加
            }
            return null;
        }
        private IEnumerator PlayClipInternal(
            VoiceCharComponent charaComp,
            ChaControl chara,
            AudioSource asVoice,
            List<AssetBundle> bundles, 
            string prefix, 
            string pNum, 
            int baseExp, 
            string rawId,
            bool isVoice)
        {
            List<string> ids = ParseExtendedIDs(rawId);
            Debug.Log($"[DEBUG⑥] rawId:{rawId} → ids:{string.Join(",", ids.ToArray())}"); if (ids.Count == 0)
            {
                Debug.LogWarning($"[PlayClipInternal] 再生対象のIDが空です (Prefix: {prefix})");
                yield break;
            }
            // ★前回ID取得（prefixで判定してもOK）
            string lastId = isVoice ? charaComp.LastPlayedVoiceId : charaComp.LastPlayedBreathId;

            // ★有効ID抽出（Exp完全一致のみ）
            List<string> validIds = new List<string>();

            foreach (var id in ids)
            {
                foreach (var b in bundles)
                {
                    if (ExistsClipExact(b, prefix, pNum, baseExp, id))
                    {
                        validIds.Add(id);
                        break;
                    }
                }
            }
            // ★1つでもあればそれだけ使う（fallbackしない）
            var workingIds = validIds.Count > 0 ? validIds : ids;


            // ★同じIDを除外（ただし1個しかない場合は除外しない）
            var candidates = workingIds.Count > 1
                ? workingIds.Where(id => id != lastId).ToList()
                : new List<string>(workingIds);

            // ★シャッフル
            var shuffledIds = candidates.OrderBy(_ => UnityEngine.Random.value).ToList();
            AudioClip clip = null;
            string selectedId = null;

            foreach (var id in shuffledIds)
            {
                foreach (var b in bundles)
                {
                    clip = FindClip(b, prefix, pNum, baseExp, id);
                    Debug.Log($"[DEBUG⑦] clip結果: {(clip == null ? "NULL" : clip.name)}");
                    if (clip != null)
                    {
                        selectedId = id;
                        if (isVoice)
                            charaComp.LastPlayedVoiceId = selectedId;
                        else
                            charaComp.LastPlayedBreathId = selectedId;

                        break;
                    }
                }
                if (clip != null)
                    break;
            }
            if (clip != null)
            {
                Debug.Log($"[PlayClipInternal] Clip再生: {clip.name}");
                asVoice.Stop();
                asVoice.clip = clip;

                asVoice.pitch = GetVoicePitch(chara);
                asVoice.Play();

                // 別クラスのコントローラーにリップシンクを丸投げする
                // StartCoroutine ではなく、コントローラー側のメソッドを呼ぶだけ
                if (LipSyncController.Instance != null)
                {
                    LipSyncController.Instance.StartLipSync(chara, asVoice);
                }
                // 「投げっぱなしにしない」場合は、ここで音声終了を待つ
                while (asVoice.isPlaying) yield return null;
                Debug.Log($"[PlayClipInternal] Clip終了: {clip.name}");
            }
            else
            {
                Debug.LogError($"[PlayClipInternal] Clipが見つかりませんでした: {prefix}_{rawId}");
                yield break;
            }
        }
        private bool ExistsClipExact(
            AssetBundle bundle,
            string prefix,
            string pNum,
            int baseExp,
            string clipId)
        {
            if (bundle == null) return false;

            bool isAdm = prefix.StartsWith("adm");

            if (isAdm)
            {
                return bundle.LoadAsset<AudioClip>($"{prefix}_{pNum}_{clipId}") != null;
            }

            return bundle.LoadAsset<AudioClip>($"{prefix}_{pNum}_{baseExp:D2}_{clipId}") != null;
        }
        // --- 停止処理 ---
        public void StopVoice(VoiceCharComponent chara = null, bool clearManagement = true)
        {
            if (chara == null)
            {
                foreach (var c in _activeCoroutines.Keys.ToList()) StopVoice(c, clearManagement);
                return;
            }
            if (chara.gameObject == null)
                return;

            chara.IsVoicePlaying = false;
            chara.IsCurrentNotLoop = false;
            // 管理（コルーチン）を消す場合のみ実行
            if (clearManagement)
            {
                if (_activeCoroutines.TryGetValue(chara, out var routine))
                {
                    if (routine != null) StopCoroutine(routine);
                    _activeCoroutines.Remove(chara);
                }
            }
            var cha = chara.GetComponentInParent<ChaControl>() ?? chara.GetComponent<ChaControl>();
            if (cha != null)
            {
                // --- 徹底的な物理停止（ここが復活した「止める」ための核） ---
                foreach (var source in cha.gameObject.GetComponentsInChildren<AudioSource>(true))
                {
                    if (source != null)
                    {
                        source.Stop();
                        source.clip = null;
                    }
                }
                // LipSyncの内部状態だけリセット
                MouthPatch.MouthValues.Remove(cha);
            }
        }
        private void Setup3DSound(AudioSource s)
        {
            if (s == null) return;
            s.spatialBlend = 1.0f;
            s.minDistance = 1.0f;
            s.maxDistance = 30.0f;
            s.loop = false;
        }
        // CSVの ID範囲(001~005等)を個別のIDリストに変換
        private List<string> ParseExtendedIDs(string rawId)
        {
            List<string> result = new List<string>();

            if (string.IsNullOrEmpty(rawId))
                return result;

            foreach (var part in rawId.Split(',').Select(p => p.Trim()))
            {
                // 範囲指定
                if (part.Contains("~"))
                {
                    string[] r = part.Split('~');

                    string start = r[0];
                    string end = r[1];

                    // XXX_00~XXX_05
                    int us = start.LastIndexOf('_');
                    int ue = end.LastIndexOf('_');

                    if (us >= 0 && ue >= 0)
                    {
                        string prefixS = start.Substring(0, us);
                        string prefixE = end.Substring(0, ue);

                        string numS = start.Substring(us + 1);
                        string numE = end.Substring(ue + 1);

                        // prefix一致時のみ展開
                        if (prefixS == prefixE &&
                            int.TryParse(numS, out int sN) &&
                            int.TryParse(numE, out int eN))
                        {
                            int digits = numS.Length;

                            for (int i = sN; i <= eN; i++)
                            {
                                result.Add($"{prefixS}_{i.ToString($"D{digits}")}");
                            }
                            continue;
                        }
                    }
                    // 通常数値範囲
                    if (int.TryParse(start, out int sV) &&
                        int.TryParse(end, out int eV))
                    {
                        int digits = start.Length;

                        for (int i = sV; i <= eV; i++)
                        {
                            result.Add(i.ToString($"D{digits}"));
                        }
                        continue;
                    }
                    // 展開失敗時はそのまま
                    result.Add(part);
                }
                else
                {
                    result.Add(part);
                }
            }
            return result;
        }
        // アセットバンドルから適切な経験値(Exp)の音声を探す
        private AudioClip FindClip(AssetBundle bundle, string prefix, string pNum, int baseExp, string clipId)
        {
            if (bundle == null || string.IsNullOrEmpty(prefix) || string.IsNullOrEmpty(clipId))
                return null;

            // adm判定
            bool isAdm = prefix.StartsWith("adm");
            // adm系（Expなし）
            if (isAdm)
            {
                var clip = bundle.LoadAsset<AudioClip>($"{prefix}_{pNum}_{clipId}");
                return clip;
            }
            // 通常（Expあり）
            int min = 0;
            int max = 3;

            // ① 下方向（base → 0）
            for (int e = baseExp; e >= min; e--)
            {
                var clip = bundle.LoadAsset<AudioClip>($"{prefix}_{pNum}_{e:D2}_{clipId}");
                if (clip != null) return clip;
            }
            // ② 上方向（base+1 → max）
            for (int e = baseExp + 1; e <= max; e++)
            {
                var clip = bundle.LoadAsset<AudioClip>($"{prefix}_{pNum}_{e:D2}_{clipId}");
                if (clip != null) return clip;
            }
            return null;
        }
        private float GetVoicePitch(ChaControl chara)
        {
            if (chara == null || chara.fileParam == null)
                return 1f;
            // ★コイカツのボイスピッチ
            return chara.fileParam.voicePitch;
        }
        public void ResetAll()
        {
            foreach (var pair in _activeCoroutines)
            {
                if (pair.Value != null)
                    StopCoroutine(pair.Value);
            }
            _activeCoroutines.Clear();
        }
    }
}
