using KKAPI.Utilities;
using Studio;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KK_StudioVoice
{
    public class VoiceTimelineCompat
    {
        private MyVoicePlugin voicePlugin;

        public VoiceTimelineCompat(MyVoicePlugin plugin)
        {
            voicePlugin = plugin;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Studioロード時のみ追加
            if (scene.name == "Studio" || scene.name == "StudioVR")
            {
                RegisterTracks();
            }
        }
        public void Dispose()
        {
            try { SceneManager.sceneLoaded -= OnSceneLoaded; } catch { }
        }
        //  ヘルパー関数（oci からコンポーネントを取り出す）
        private VoiceCharComponent GetVoiceComponent(ObjectCtrlInfo oci)
        {
            try
            {
                if (oci == null) return null;

                var ociChar = oci as OCIChar;
                if (ociChar == null) return null;

                var guide = ociChar.guideObject;
                if (guide == null) return null;

                var transformTarget = guide.transformTarget;
                if (transformTarget == null) return null;

                var go = transformTarget.gameObject;
                if (go == null) return null;

                // GameObject が有効なシーンに属しているか確認（アンロード中のオブジェクト回避）
                try
                {
                    var scene = go.scene;
                    if (!scene.IsValid() || !scene.isLoaded)
                        return null;
                }
                catch
                {
                    // Scene API で例外が出る可能性があるため握る
                    return null;
                }
                // 既存のコンポーネントを返す（破棄時に AddComponent はしない）
                return go.GetComponent<VoiceCharComponent>();
            }
            catch
            {
                // ここでは例外を外に出さず安全に null を返す（Timeline 呼び出し中に落とさないため）
                return null;
            }
        }
        private void RegisterTracks()
        {
            // Anime Mode
            TimelineCompatibility.AddInterpolableModelDynamic(
                "KK_StudioVoice", "AnimeMode", "Anime Mode",
                (oci, param, left, right, f) =>
                {
                    string val = (string)(f < 1 ? left : right);
                    var chara = GetVoiceComponent(oci);
                    if (chara != null)
                    {
                        // SetModeを呼びつつ、戻り値（変更があったか）をチェック
                        if (chara.SetMode(val))
                        {
                            if (chara == voicePlugin.GetActiveCharacterComponent())
                                voicePlugin.SelectedMode = val;
                            // 値が変わった瞬間だけリフレッシュ
                            voicePlugin.RequestRefresh(chara);
                        }
                    }
                },
                null,
                oci => oci is OCIChar,
                (oci, param) =>
                {
                    var chara = GetVoiceComponent(oci);
                    return chara != null ? (object)chara.SelectedMode : (object)voicePlugin.SelectedMode;
                },
                (param, node) => node.Attributes["value"].Value,
                (param, writer, value) => writer.WriteAttributeString("value", value as string ?? ""),
                oci => null,
                (oci, node) => null,
                null, null, true,
                (currentName, oci, param) => "Anime Mode", null
            );
            // Anime Clip
            TimelineCompatibility.AddInterpolableModelDynamic(
                "KK_StudioVoice", "AnimeClip", "Anime Clip",
                (oci, param, left, right, f) =>
                {
                    string val = (string)(f < 1 ? left : right);
                    var chara = GetVoiceComponent(oci);
                    if (chara != null)
                    {
                        if (chara.SetClip(val))
                        {
                            if (chara == voicePlugin.GetActiveCharacterComponent())
                                voicePlugin.SelectedClip = val;
                            voicePlugin.RequestRefresh(chara);
                        }
                    }
                },
                null,
                oci => oci is OCIChar,
                (oci, param) =>
                {
                    var chara = GetVoiceComponent(oci);
                    return chara != null ? (object)chara.SelectedClip : (object)voicePlugin.SelectedClip;
                },
                (param, node) => node.Attributes["value"].Value,
                (param, writer, value) => writer.WriteAttributeString("value", value as string ?? ""),
                oci => null,
                (oci, node) => null,
                null, null, true,
                (currentName, oci, param) => "Anime Clip", null
            );
            // Option
            TimelineCompatibility.AddInterpolableModelDynamic(
                "KK_StudioVoice", "Option", "Option",
                (oci, param, left, right, f) =>
                {
                    string val = (string)(f < 1 ? left : right);
                    var chara = GetVoiceComponent(oci);
                    if (chara != null)
                    {
                        if (chara.SetOption(val))
                        {
                            if (chara == voicePlugin.GetActiveCharacterComponent())
                                voicePlugin.SelectedOption = val;
                            voicePlugin.RequestRefresh(chara);
                        }
                    }
                },
                null,
                oci => oci is OCIChar,
                (oci, param) =>
                {
                    var chara = GetVoiceComponent(oci);
                    return chara != null ? (object)chara.SelectedOption : (object)voicePlugin.SelectedOption;
                },
                (param, node) => node.Attributes["value"].Value,
                (param, writer, value) => writer.WriteAttributeString("value", value as string ?? ""),
                oci => null,
                (oci, node) => null,
                null, null, true,
                (currentName, oci, param) => "Option", null
            );
            // Exp
            TimelineCompatibility.AddInterpolableModelDynamic(
                "KK_StudioVoice", "Exp", "H Level",
                (oci, param, left, right, f) =>
                {
                    string val = (string)(f < 1 ? left : right);
                    var chara = GetVoiceComponent(oci);
                    if (chara != null)
                    {
                        if (chara.SetExp(val))
                        {
                            if (chara == voicePlugin.GetActiveCharacterComponent())
                                voicePlugin.SelectedExp = val;
                            voicePlugin.RequestRefresh(chara);
                        }
                    }
                },
                null,
                oci => oci is OCIChar,
                (oci, param) =>
                {
                    var chara = GetVoiceComponent(oci);
                    return chara != null ? (object)chara.SelectedExp : "";
                },
                (param, node) => node.Attributes["value"].Value, // XMLから文字列として読み込む
                (param, writer, value) => writer.WriteAttributeString("value", (string)value), // 文字列として書き込む
                 oci => null,
                (oci, node) => null,
                 null, null, true, (currentName, oci, param) => "H_Level", null
            );
            // Excited
            TimelineCompatibility.AddInterpolableModelDynamic(
                "KK_StudioVoice", "Excited", "Excited ON/OFF",
                (oci, param, left, right, f) =>
                {
                    bool val = (bool)(f < 1 ? left : right);
                    var chara = GetVoiceComponent(oci);
                    if (chara != null)
                    {
                        if (chara.SetExcited(val))
                        {
                            if (chara == voicePlugin.GetActiveCharacterComponent())
                                voicePlugin.IsExcited = val;
                            voicePlugin.RequestRefresh(chara);
                        }
                    }
                },
                null,
                oci => oci is OCIChar,
                (oci, param) =>
                {
                    var chara = GetVoiceComponent(oci);
                    return chara != null ? (object)chara.IsExcited : (object)false;
                },
                (param, node) => XmlConvert.ToBoolean(node.Attributes["value"].Value),
                (param, writer, value) => writer.WriteAttributeString("value", XmlConvert.ToString((bool)value)),
                oci => null,
                (oci, node) => null,
                null, null, true,
                (currentName, oci, param) => "Excited ON/OFF", null
            );
            // LipSync
            TimelineCompatibility.AddInterpolableModelDynamic(
                "KK_StudioVoice", "LipSync", "LipSync ON/OFF",
                (oci, param, left, right, f) =>
                {
                    bool val = (bool)(f < 1 ? left : right);
                    var chara = GetVoiceComponent(oci);
                    if (chara != null)
                    {
                        if (chara.SetLipSyncOn(val))
                        {
                            if (chara == voicePlugin.GetActiveCharacterComponent())
                                voicePlugin.IsLipSyncOn = val;
                        }
                    }
                },
                null,
                oci => oci is OCIChar,
                (oci, param) =>
                {
                    var chara = GetVoiceComponent(oci);
                    return chara != null ? (object)chara.IsLipSyncOn : (object)true;
                },
                (param, node) => XmlConvert.ToBoolean(node.Attributes["value"].Value),
                (param, writer, value) => writer.WriteAttributeString("value", XmlConvert.ToString((bool)value)),
                oci => null,
                (oci, node) => null,
                null, null, true,
                (currentName, oci, param) => "LipSync ON/OFF", null
            );
            // Voice
            TimelineCompatibility.AddInterpolableModelDynamic(
                "KK_StudioVoice", "Voice", "Voice ON/OFF",
                (oci, param, left, right, f) =>
                {
                    bool val = (bool)(f < 1 ? left : right);
                    var chara = GetVoiceComponent(oci);
                    if (chara != null)
                    {
                        if (chara.SetVoiceOn(val))
                        {
                            if (chara == voicePlugin.GetActiveCharacterComponent())
                                voicePlugin.IsVoiceOn = val;
                            voicePlugin.RequestRefresh(chara);
                        }
                    }
                },
                null,
                oci => oci is OCIChar,
                (oci, param) =>
                {
                    var chara = GetVoiceComponent(oci);
                    return chara != null ? (object)chara.IsVoiceOn : (object)true;
                },
                (param, node) => XmlConvert.ToBoolean(node.Attributes["value"].Value),
                (param, writer, value) => writer.WriteAttributeString("value", XmlConvert.ToString((bool)value)),
                oci => null,
                (oci, node) => null,
                null, null, true,
                (currentName, oci, param) => "Voice ON/OFF", null
            );
            // Breath
            TimelineCompatibility.AddInterpolableModelDynamic(
                "KK_StudioVoice", "Breath", "Breath ON/OFF",
                (oci, param, left, right, f) =>
                {
                    bool val = (bool)(f < 1 ? left : right);
                    var chara = GetVoiceComponent(oci);
                    if (chara != null)
                    {
                        if (chara.SetBreathOn(val))
                        {
                            if (chara == voicePlugin.GetActiveCharacterComponent())
                                voicePlugin.IsBreathOn = val;
                            voicePlugin.RequestRefresh(chara);
                        }
                    }
                },
                null,
                oci => oci is OCIChar,
                (oci, param) =>
                {
                    var chara = GetVoiceComponent(oci);
                    return chara != null ? (object)chara.IsBreathOn : (object)true;
                },
                (param, node) => XmlConvert.ToBoolean(node.Attributes["value"].Value),
                (param, writer, value) => writer.WriteAttributeString("value", XmlConvert.ToString((bool)value)),
                oci => null,
                (oci, node) => null,
                null, null, true,
                (currentName, oci, param) => "Breath ON/OFF", null
            );
            // Volume
            TimelineCompatibility.AddInterpolableModelDynamic(
                "KK_StudioVoice", "Volume", "Volume",
                (oci, param, left, right, f) =>
                {

                    float lVal = left != null ? Convert.ToSingle(left) : 1f;
                    float rVal = right != null ? Convert.ToSingle(right) : 1f;
                    float val = Mathf.Lerp(lVal, rVal, f);

                    var chara = GetVoiceComponent(oci);
                    if (chara != null)
                    {
                        if (chara.SetVolume(val))
                        {
                            var activeSource = chara.GetComponentInChildren<AudioSource>();
                            if (activeSource != null) activeSource.volume = val;

                            // もし現在選択中のキャラクターであれば、プラグイン側のボリューム表示も同期する
                            if (chara == voicePlugin.GetActiveCharacterComponent())
                            {
                                voicePlugin.SelectedVolumeSyncFromTimeline(chara, val);
                            }
                        }
                    }
                },
                null,
                oci => oci is OCIChar,
                (oci, param) =>
                {
                    var chara = GetVoiceComponent(oci);
                    return chara != null ? (object)chara.Volume : (object)1.0f;
                },
                (param, node) => XmlConvert.ToSingle(node.Attributes["value"].Value),
                (param, writer, value) => writer.WriteAttributeString("value", XmlConvert.ToString((float)value)),
                oci => null,
                (oci, node) => null,
                null, null, true,
                (currentName, oci, param) => "Volume", null);
            // Play (再生ON/OFF)
            TimelineCompatibility.AddInterpolableModelDynamic(
                "KK_StudioVoice", "Play", "Sound ON/OFF",
                (oci, param, left, right, f) =>
                {
                    bool play = (bool)(f < 1 ? left : right);
                    var chara = GetVoiceComponent(oci);
                    if (chara != null)
                    {
                        // 【修正】値が変わった時だけ処理するようにガード
                        if (chara.SetPlaying(play))
                        {
                            if (chara == voicePlugin.GetActiveCharacterComponent())
                            {
                                voicePlugin.IsPlaying = play;
                            }
                            // 選択・非選択に関わらず、値が変わった瞬間はRefreshVoiceが必要
                            voicePlugin.RequestRefresh(chara);
                        }
                    }
                },
                null,
                oci => oci is OCIChar,
                // 読み取りデリゲートをchara本人のプロパティ参照にする
                (oci, param) =>
                {
                    var chara = GetVoiceComponent(oci);
                    return chara != null ? (object)chara.IsPlaying : (object)false;
                },
                (param, node) => XmlConvert.ToBoolean(node.Attributes["value"].Value),
                (param, writer, value) => writer.WriteAttributeString("value", XmlConvert.ToString((bool)value)),
                oci => null,
                (oci, node) => null,
                null, null, true,
                (currentName, oci, param) => "Sound ON/OFF", null
            );
        }
        // VoiceTimelineCompat.cs - 置換: AddStudioVoiceKeyframes を遅延コルーチン呼び出しに変更
        public void AddStudioVoiceKeyframes()
        {
            try
            {
                var selected = KKAPI.Studio.StudioAPI.GetSelectedCharacters().FirstOrDefault();
                if (selected == null)
                {
                    return;
                }
                // まず GetActiveCharacterComponent を呼んで必要なら Attach を試みる（内部で安全チェック済み）
                try { voicePlugin.GetActiveCharacterComponent(); } catch { /* ignore */ }

                // 実際のキー追加処理は少し待ってから行う（Attach の反映を待つ）
                try
                {
                    voicePlugin.StartCoroutine(AddStudioVoiceKeyframes_Coroutine(selected));
                }
                catch (Exception)
                {
                    // 万が一 StartCoroutine が使えない環境なら同期的に実行（最終手段）
                    // ただし同期実行だと Attach の反映タイミング次第で失敗する可能性あり
                    AddStudioVoiceKeyframes_Immediate(selected);
                }
            }
            catch (Exception e)
            {
                SLog.Log.LogError($"[KK_StudioVoice] AddStudioVoiceKeyframes で例外: {e}");
            }
        }
        private IEnumerator AddStudioVoiceKeyframes_Coroutine(ObjectCtrlInfo selected)
        {
            // 1フレーム待機 + 少し余裕（0.05秒）を入れて Attach の反映を待つ
            yield return null;
            yield return new WaitForSeconds(0.05f);

            // その後、即時処理を行う
            AddStudioVoiceKeyframes_Immediate(selected);
        }
        private void AddStudioVoiceKeyframes_Immediate(ObjectCtrlInfo selected)
        {
            try
            {
                // GetActiveCharacterComponent を使って確実に（可能なら）コンポーネントを取得する
                var charComp = voicePlugin.GetActiveCharacterComponent();
                if (charComp == null)
                {
                    SLog.Log.LogWarning("[KK_StudioVoice] AddStudioVoiceKeyframes: VoiceCharComponent が取得できませんでした。");
                    return;
                }
                // Timeline 型とインスタンスを探す
                Assembly timelineAsm = null;
                System.Type timelineType = null;
                object timelineInstance = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        var t = asm.GetTypes().FirstOrDefault(tt =>
                            tt.FullName == "Timeline.Timeline" ||
                            (tt.Namespace == "Timeline" && tt.Name == "Timeline"));
                        if (t != null) { timelineAsm = asm; timelineType = t; break; }
                    }
                    catch { }
                }
                if (timelineType == null)
                {
                    SLog.Log.LogWarning("[KK_StudioVoice] Timeline 型が見つかりません。");
                    return;
                }
                var instProp = timelineType.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? timelineType.GetProperty("instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (instProp != null) timelineInstance = instProp.GetValue(null, null);
                if (timelineInstance == null)
                {
                    var findMethod = typeof(UnityEngine.Object).GetMethod("FindObjectOfType", new System.Type[] { typeof(System.Type) });
                    if (findMethod != null) timelineInstance = findMethod.Invoke(null, new object[] { timelineType });
                }
                if (timelineInstance == null)
                {
                    SLog.Log.LogWarning("[KK_StudioVoice] Timeline のインスタンスが見つかりません。");
                    return;
                }
                // 現在時刻を取得
                float time = 0f;
                try
                {
                    var getTime = timelineType.GetMethod("GetPlaybackTime", BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                               ?? timelineType.GetMethod("GetCurrentTime", BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                               ?? timelineType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                                   .FirstOrDefault(m => m.ReturnType == typeof(float) && m.GetParameters().Length == 0);
                    if (getTime != null)
                    {
                        var target = getTime.IsStatic ? null : timelineInstance;
                        time = (float)getTime.Invoke(target, null);
                    }
                    else
                    {
                        // TimelineCompatibility 側を参照
                        var tcType = AppDomain.CurrentDomain.GetAssemblies()
                            .SelectMany(a => { try { return a.GetTypes(); } catch { return new System.Type[0]; } })
                            .FirstOrDefault(tt => tt.Name == "TimelineCompatibility");
                        if (tcType != null)
                        {
                            var getTimeAlt = tcType.GetMethod("GetPlaybackTime", BindingFlags.Public | BindingFlags.Static)
                                           ?? tcType.GetMethod("GetCurrentTime", BindingFlags.Public | BindingFlags.Static);
                            if (getTimeAlt != null) time = (float)getTimeAlt.Invoke(null, null);
                        }
                    }
                }
                catch (Exception e)
                {
                    SLog.Log.LogWarning($"[KK_StudioVoice] タイムライン時刻取得で例外: {e.Message}（0.0 を使用）");
                    time = 0f;
                }
                // models list を取得
                object modelsListObj = null;
                var modelsField = timelineType.GetField("_interpolableModelsList", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var modelsProp = timelineType.GetProperty("_interpolableModelsList", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (modelsField != null) modelsListObj = modelsField.GetValue(timelineInstance);
                else if (modelsProp != null) modelsListObj = modelsProp.GetValue(timelineInstance, null);
                else
                {
                    var altProp = timelineType.GetProperty("interpolableModelsList", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                ?? timelineType.GetProperty("_interpolableModelList", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (altProp != null) modelsListObj = altProp.GetValue(timelineInstance, null);
                }
                if (modelsListObj == null)
                {
                    SLog.Log.LogWarning("[KK_StudioVoice] _interpolableModelsList が取得できません。");
                    return;
                }
                var modelsEnumerable = modelsListObj as IEnumerable;
                if (modelsEnumerable == null)
                {
                    SLog.Log.LogWarning("[KK_StudioVoice] interpolableModelsList が IEnumerable ではありません。");
                    return;
                }
                // 所有者またはIDでモデルを検索するヘルパー
                Func<string, object> findModel = (modelId) =>
                {
                    foreach (var m in modelsEnumerable)
                    {
                        try
                        {
                            var mType = m.GetType();
                            string owner = null;
                            var ownerProp = mType.GetProperty("owner", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            var ownerField = mType.GetField("owner", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            if (ownerProp != null) owner = ownerProp.GetValue(m, null) as string;
                            else if (ownerField != null) owner = ownerField.GetValue(m) as string;

                            string id = null;
                            var idProp = mType.GetProperty("id", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            var idField = mType.GetField("id", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            if (idProp != null) id = idProp.GetValue(m, null) as string;
                            else if (idField != null) id = idField.GetValue(m) as string;

                            if (!string.IsNullOrEmpty(owner) && !string.IsNullOrEmpty(id))
                            {
                                if (string.Equals(owner, "KK_StudioVoice", StringComparison.OrdinalIgnoreCase)
                                    && string.Equals(id, modelId, StringComparison.OrdinalIgnoreCase))
                                    return m;
                            }
                        }
                        catch { }
                    }
                    return null;
                };
                var ids = new Dictionary<string, object>()
        {
            { "AnimeMode",  (object)charComp.SelectedMode },
            { "AnimeClip",  (object)charComp.SelectedClip },
            { "Option",     (object)charComp.SelectedOption },
            { "Exp",        (object)charComp.SelectedExp },
            { "Excited",    (object)charComp.IsExcited },
            { "LipSync",    (object)charComp.IsLipSyncOn },
            { "Voice",      (object)charComp.IsVoiceOn },
            { "Breath",     (object)charComp.IsBreathOn },
            { "Volume",     (object)charComp.Volume },
            { "Play",       (object)charComp.IsPlaying }
        };
                // timeline の AddInterpolable を探す（優先して使う）
                var addInterpolableMethod = timelineType.GetMethod("AddInterpolable", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                            ?? timelineType.GetMethod("AddInterpolable", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                // timeline.AddKeyframe メソッドも探す
                MethodInfo addKeyframeMethod = timelineType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .FirstOrDefault(m => m.Name == "AddKeyframe" && m.GetParameters().Length >= 2);

                int added = 0;

                foreach (var kv in ids)
                {
                    string modelId = kv.Key;
                    object value = kv.Value;

                    var model = findModel(modelId);
                    if (model == null)
                    {
                        continue;
                    }
                    object interpolable = null;

                    // まず timeline.AddInterpolable(model) を試して、Timeline 側の正規処理に任せる（これが最も安全）
                    if (addInterpolableMethod != null)
                    {
                        try
                        {
                            var target = addInterpolableMethod.IsStatic ? null : timelineInstance;
                            var pars = addInterpolableMethod.GetParameters();
                            if (pars.Length == 1 && pars[0].ParameterType.IsAssignableFrom(model.GetType()))
                            {
                                interpolable = addInterpolableMethod.Invoke(target, new object[] { model });
                            }
                            else if (pars.Length == 2) // 稀に別シグネチャなら試す
                            {
                                interpolable = addInterpolableMethod.Invoke(target, new object[] { model, null });
                            }
                        }
                        catch{}
                    }
                    // もし AddInterpolable が無かったり失敗した場合は、model にある getParameter デリゲートを試して parameter を取得しつつ new する
                    if (interpolable == null)
                    {
                        try
                        {
                            var modelType = model.GetType();
                            MethodInfo getParamMethod = null;
                            var getParamProp = modelType.GetProperty("getParameter", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            var getParamField = modelType.GetField("getParameter", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            Delegate getParamDel = null;
                            if (getParamProp != null) getParamDel = getParamProp.GetValue(model, null) as Delegate;
                            else if (getParamField != null) getParamDel = getParamField.GetValue(model) as Delegate;

                            object parameter = null;
                            if (getParamDel != null)
                            {
                                try { parameter = getParamDel.DynamicInvoke(selected); } catch { parameter = null; }
                            }
                            else
                            {
                                getParamMethod = modelType.GetMethod("GetParameter", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                if (getParamMethod != null)
                                {
                                    try { parameter = getParamMethod.Invoke(model, new object[] { selected }); } catch { parameter = null; }
                                }
                            }
                            var interpolableType = timelineAsm.GetTypes().FirstOrDefault(tt => tt.Name == "Interpolable");
                            if (interpolableType != null)
                            {
                                var ctor = interpolableType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                    .FirstOrDefault(c => c.GetParameters().Length == 3);
                                if (ctor != null)
                                {
                                    try
                                    {
                                        object paramToUse = parameter ?? selected;
                                        interpolable = ctor.Invoke(new object[] { selected, paramToUse, model });
                                    }
                                    catch{}
                                }
                            }
                        }
                        catch{}
                    }
                    if (interpolable == null)
                    {
                        continue;
                    }
                    // --- ここから：直前キーと比較して差分のみ打ち込むロジック ---
                    if (addKeyframeMethod != null)
                    {
                        // 直前のキー（<= time）を取得して比較
                        if (TryGetLatestKeyframeAtOrBefore(interpolable, time, out float prevTime, out object prevValue))
                        {
                            // 直前キーがある場合、値が同じならスキップ
                            if (AreValuesEqual(prevValue, value))
                            {
                                continue;
                            }
                            // 同時刻のキーがあるなら削除（置換）
                            if (System.Math.Abs(prevTime - time) < 1e-6f)
                            {
                                TryRemoveKeyframeAtTime(interpolable, time);
                            }
                        }
                        // 直前キーが無ければ初回扱い→常に追加する

                        // 追加処理（AddKeyframe を試行し、失敗したらフォールバックで直接挿入）
                        try
                        {
                            var target = addKeyframeMethod.IsStatic ? null : timelineInstance;
                            var pars = addKeyframeMethod.GetParameters();
                            if (pars.Length == 2)
                                addKeyframeMethod.Invoke(target, new object[] { interpolable, time });
                            else if (pars.Length == 3)
                                addKeyframeMethod.Invoke(target, new object[] { interpolable, time, false });
                            else
                                addKeyframeMethod.Invoke(target, new object[] { interpolable, time });

                            added++;
                        }
                        catch (TargetInvocationException)
                        {
                            // フォールバック: Keyframe を自前生成して interpolable.keyframes に追加
                            try
                            {
                                var keyframeType = timelineAsm.GetTypes().FirstOrDefault(t => t.Name == "Keyframe");
                                if (keyframeType == null)
                                {
                                    AnimationCurve defaultCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
                                    object createdKeyframe = null;
                                    var ctors = keyframeType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                                    ConstructorInfo chosenCtor = ctors.FirstOrDefault(c =>
                                    {
                                        var ps = c.GetParameters();
                                        return ps.Length == 3 && ps[0].ParameterType == typeof(object);
                                    });
                                    if (chosenCtor != null)
                                    {
                                        try { createdKeyframe = chosenCtor.Invoke(new object[] { value, interpolable, defaultCurve }); } catch { createdKeyframe = null; }
                                    }
                                    if (createdKeyframe == null)
                                    {
                                        chosenCtor = ctors.FirstOrDefault(c =>
                                        {
                                            var ps = c.GetParameters();
                                            return ps.Length == 2 && ps[0].ParameterType == typeof(object);
                                        });
                                        if (chosenCtor != null)
                                        {
                                            try { createdKeyframe = chosenCtor.Invoke(new object[] { value, interpolable }); } catch { createdKeyframe = null; }
                                        }
                                    }
                                    if (createdKeyframe == null)
                                    {
                                        foreach (var c in ctors)
                                        {
                                            var ps = c.GetParameters();
                                            if (ps.Length >= 1 && ps[0].ParameterType == typeof(object))
                                            {
                                                var args = new object[ps.Length];
                                                args[0] = value;
                                                if (ps.Length >= 2) args[1] = interpolable;
                                                for (int i = 2; i < ps.Length; i++) args[i] = Type.Missing;
                                                try { createdKeyframe = c.Invoke(args); break; } catch { createdKeyframe = null; }
                                            }
                                        }
                                    }
                                    if (createdKeyframe == null)
                                    {
                                        try
                                        {
                                            createdKeyframe = System.Activator.CreateInstance(keyframeType, nonPublic: true);
                                            if (createdKeyframe != null)
                                            {
                                                var valProp = keyframeType.GetProperty("value", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                                if (valProp != null && valProp.CanWrite) valProp.SetValue(createdKeyframe, value, null);
                                                else
                                                {
                                                    var valField = keyframeType.GetField("value", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                                    if (valField != null) valField.SetValue(createdKeyframe, value);
                                                }
                                                var parentProp = keyframeType.GetProperty("parent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                                if (parentProp != null && parentProp.CanWrite) parentProp.SetValue(createdKeyframe, interpolable, null);
                                                else
                                                {
                                                    var parentField = keyframeType.GetField("parent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                                    if (parentField != null) parentField.SetValue(createdKeyframe, interpolable);
                                                }
                                            }
                                        }
                                        catch{}
                                    }
                                    if (createdKeyframe != null)
                                    {
                                        var kfProp = interpolable.GetType().GetProperty("keyframes", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                        var kfField = interpolable.GetType().GetField("keyframes", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                        object kfDict = null;
                                        if (kfProp != null) kfDict = kfProp.GetValue(interpolable, null);
                                        else if (kfField != null) kfDict = kfField.GetValue(interpolable);

                                        if (kfDict != null)
                                        {
                                            if (kfDict is System.Collections.IDictionary nonGen)
                                            {
                                                nonGen[time] = createdKeyframe;
                                            }
                                            else
                                            {
                                                var itemProp = kfDict.GetType().GetProperty("Item");
                                                if (itemProp != null && itemProp.CanWrite)
                                                {
                                                    itemProp.SetValue(kfDict, createdKeyframe, new object[] { time });
                                                }
                                                else
                                                {
                                                    var addMethod = kfDict.GetType().GetMethod("Add");
                                                    if (addMethod != null) addMethod.Invoke(kfDict, new object[] { time, createdKeyframe });
                                                }
                                            }
                                            // duration を更新して表示されるようにする
                                            try
                                            {
                                                var durField = timelineType.GetField("_duration", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                                if (durField != null)
                                                {
                                                    float curDur = (float)durField.GetValue(timelineInstance);
                                                    if (curDur < time) durField.SetValue(timelineInstance, time);
                                                }
                                                else
                                                {
                                                    var durProp = timelineType.GetProperty("duration", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                                    if (durProp != null)
                                                    {
                                                        float curDur = Convert.ToSingle(durProp.GetValue(timelineInstance, null));
                                                        if (curDur < time) durProp.SetValue(timelineInstance, time, null);
                                                    }
                                                }
                                            }
                                            catch {}
                                            added++;
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                SLog.Log.LogError($"[KK_StudioVoice] フォールバック追加で例外 (model {modelId}): {ex}");
                            }
                        }
                        catch (Exception ex)
                        {
                            SLog.Log.LogError($"[KK_StudioVoice] AddKeyframe エラー (model {modelId}): {ex.Message}");
                        }
                    }
                    else
                    {
                        break;
                    }
                    // --- ここまで差分チェック + 追加処理 ---
                }
                // 安全に Timeline のビュー更新を行う（シーン安定チェック）
                try
                {
                    var updateMethod = timelineType.GetMethod("UpdateInterpolablesView", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                    ?? timelineType.GetMethod("UpdateKeyframeWindow", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                    ?? timelineType.GetMethod("Update", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                    if (updateMethod != null)
                    {
                        // シーンが「Studio」または「StudioVR」であることを確認
                        var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                        bool sceneIsStudio = activeScene.name == "Studio" || activeScene.name == "StudioVR";

                        // timelineInstance が Unity オブジェクトとして有効かチェック
                        bool timelineInstanceValid = false;
                        try
                        {
                            if (timelineInstance is UnityEngine.Object uObj && uObj != null)
                                timelineInstanceValid = true;
                        }
                        catch { timelineInstanceValid = false; }

                        // 追加の安全チェック：Studio シングルトンが存在するか（より堅牢にするため）
                        bool studioPresent = false;
                        try { studioPresent = Singleton<Studio.Studio>.Instance != null; } catch { studioPresent = false; }

                        // 更新実行判定
                        if (sceneIsStudio && (timelineInstanceValid || studioPresent))
                        {
                            var updateTarget = updateMethod.IsStatic ? null : timelineInstance;
                            updateMethod.Invoke(updateTarget, null);
                        }
                    }
                }
                catch{}
                // selected が OCIChar なら名前を取り出し、なければ "(unknown)" を使う
                string targetName = (selected as OCIChar)?.charInfo?.name ?? "(unknown)";
            }
            catch (Exception e)
            {
                SLog.Log.LogError($"[KK_StudioVoice] AddStudioVoiceKeyframes で例外: {e}");
            }
        }
        // ====== ヘルパー群 ======
        private bool TryGetLatestKeyframeAtOrBefore(object interpolable, float time, out float foundKeyTime, out object foundValue)
        {
            foundKeyTime = 0f;
            foundValue = null;
            try
            {
                var kfProp = interpolable.GetType().GetProperty("keyframes", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var kfField = interpolable.GetType().GetField("keyframes", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                object kfDict = null;
                if (kfProp != null) kfDict = kfProp.GetValue(interpolable, null);
                else if (kfField != null) kfDict = kfField.GetValue(interpolable);

                if (kfDict == null) return false;

                float bestTime = float.NegativeInfinity;
                object bestValue = null;

                foreach (var pair in (IEnumerable)kfDict)
                {
                    try
                    {
                        var pairType = pair.GetType();
                        var keyProp = pairType.GetProperty("Key") ?? pairType.GetProperty("key");
                        var valProp = pairType.GetProperty("Value") ?? pairType.GetProperty("value");
                        if (keyProp == null || valProp == null) continue;

                        object keyObj = keyProp.GetValue(pair, null);
                        float keyTime = Convert.ToSingle(keyObj);
                        if (keyTime <= time && keyTime >= bestTime)
                        {
                            bestTime = keyTime;
                            var keyframeObj = valProp.GetValue(pair, null);
                            if (keyframeObj != null)
                            {
                                object valCandidate = null;
                                var kfValProp = keyframeObj.GetType().GetProperty("value", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                if (kfValProp != null) valCandidate = kfValProp.GetValue(keyframeObj, null);
                                else
                                {
                                    var kfValField = keyframeObj.GetType().GetField("value", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                    if (kfValField != null) valCandidate = kfValField.GetValue(keyframeObj);
                                    else valCandidate = keyframeObj;
                                }
                                bestValue = valCandidate;
                            }
                        }
                    }
                    catch {}
                }
                if (float.IsNegativeInfinity(bestTime)) return false;
                foundKeyTime = bestTime;
                foundValue = bestValue;
                return true;
            }
            catch { return false; }
        }
        private bool TryRemoveKeyframeAtTime(object interpolable, float time)
        {
            try
            {
                var kfProp = interpolable.GetType().GetProperty("keyframes", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var kfField = interpolable.GetType().GetField("keyframes", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                object kfDict = null;
                if (kfProp != null) kfDict = kfProp.GetValue(interpolable, null);
                else if (kfField != null) kfDict = kfField.GetValue(interpolable);

                if (kfDict == null) return false;

                if (kfDict is System.Collections.IDictionary nonGen)
                {
                    if (nonGen.Contains(time))
                    {
                        nonGen.Remove(time);
                        return true;
                    }
                    return false;
                }
                var containsMethod = kfDict.GetType().GetMethod("ContainsKey");
                var removeMethod = kfDict.GetType().GetMethod("Remove");
                if (containsMethod != null && removeMethod != null)
                {
                    bool has = (bool)containsMethod.Invoke(kfDict, new object[] { time });
                    if (has)
                    {
                        removeMethod.Invoke(kfDict, new object[] { time });
                        return true;
                    }
                }
                var rm = kfDict.GetType().GetMethod("Remove");
                if (rm != null)
                {
                    try { rm.Invoke(kfDict, new object[] { time }); return true; } catch { }
                }
                return false;
            }
            catch { return false; }
        }
        private bool AreValuesEqual(object a, object b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (a.Equals(b)) return true;

            if (IsNumericType(a) && IsNumericType(b))
            {
                double da = Convert.ToDouble(a);
                double db = Convert.ToDouble(b);
                return System.Math.Abs(da - db) <= 1e-4;
            }

            if (a is UnityEngine.Vector3 va3 && b is UnityEngine.Vector3 vb3)
                return (va3 - vb3).sqrMagnitude <= 1e-6f;
            if (a is UnityEngine.Vector2 va2 && b is UnityEngine.Vector2 vb2)
                return (va2 - vb2).sqrMagnitude <= 1e-6f;
            if (a is UnityEngine.Vector4 va4 && b is UnityEngine.Vector4 vb4)
                return (va4 - vb4).sqrMagnitude <= 1e-6f;
            if (a is UnityEngine.Quaternion qa && b is UnityEngine.Quaternion qb)
                return UnityEngine.Quaternion.Angle(qa, qb) <= 0.01f;
            if (a is UnityEngine.Color ca && b is UnityEngine.Color cb)
                return (System.Math.Abs(ca.r - cb.r) + System.Math.Abs(ca.g - cb.g) + System.Math.Abs(ca.b - cb.b) + System.Math.Abs(ca.a - cb.a)) <= 1e-4;

            if (a is string sa && b is string sb) return sa == sb;
            if (a is bool ba && b is bool bb) return ba == bb;

            return a.Equals(b);
        }
        private bool IsNumericType(object o)
        {
            if (o == null) return false;
            var t = o.GetType();
            return t == typeof(byte) || t == typeof(sbyte) || t == typeof(short) || t == typeof(ushort) ||
                   t == typeof(int) || t == typeof(uint) || t == typeof(long) || t == typeof(ulong) ||
                   t == typeof(float) || t == typeof(double) || t == typeof(decimal);
        }
    }
}