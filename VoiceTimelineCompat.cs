using KKAPI.Utilities;
using Studio;
using System.Linq;
using System.Xml;
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
        //  ヘルパー関数（oci からコンポーネントを取り出す）
        private VoiceCharComponent GetVoiceComponent(ObjectCtrlInfo oci)
        {
            var ociChar = oci as OCIChar;
            if (ociChar == null) return null;

            // キャラクターの本体からコンポーネントを取得
            var targetGo = ociChar.guideObject.transformTarget.gameObject;
            return targetGo.GetComponent<VoiceCharComponent>() ?? targetGo.AddComponent<VoiceCharComponent>();
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
                // 【修正】読み取りデリゲートをchara本人のプロパティ参照にする
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
    }
}