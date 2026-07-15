using BepInEx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace KK_StudioVoice
{
    // --- 1. データ構造の定義 ---
    public class VoiceData
    {
        public string AnimeMode;   // Anime Mode
        public string VoiceFallback;// Voiceが見つからないときのFallback
        public string BreathFallback;// Breathが見つからないときのFallback
        public string AnimeClip;   // Anime Clip
        public string Option;      // Option
        public string Excited;     // Excited (1 or empty)
        public string NotLoop;     // Not Loop
        public string VoicePrefix; // Voice Prefix
        public string VoiceID;    // Voice ID (解析前の文字列)
        public string BreathPrefix;// Breath Prefix
        public string BreathID;   // Breath ID

        // 解析済みのIDリスト（後の再生処理で使いやすくするため）
        public List<string> ParsedVoiceIDs = new List<string>();
    }

    // --- 2. 読み込みロジックの定義 ---
    public class VoiceDataLoader
    {
        private List<VoiceData> _voiceDataList = new List<VoiceData>();
        public List<VoiceData> VoiceDataList => _voiceDataList;

        private BepInEx.Logging.ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource("VoiceDataLoader");

        public void LoadCsv(string csvPath)
        {
            _voiceDataList.Clear();

            if (!File.Exists(csvPath))
            {
                Logger.LogError($"CSVファイルが見つかりません: {csvPath}");
                return;
            }

            try
            {
                // UTF-8で読み込み
                string[] lines = File.ReadAllLines(csvPath, Encoding.UTF8);

                // 1行目はヘッダーなので i=1 から開始
                for (int i = 1; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (string.IsNullOrEmpty(line) || line.Trim().Length == 0) continue;

                    // カンマで分割
                    string[] values = SplitCsvLine(line);

                    // CSVの各カラムをVoiceDataクラスにマッピング
                    VoiceData data = new VoiceData
                    {
                        AnimeMode = GetValue(values, 0),
                        VoiceFallback = GetValue(values, 1),
                        BreathFallback = GetValue(values, 2),
                        AnimeClip = GetValue(values, 3),
                        Option = GetValue(values, 4),
                        Excited = GetValue(values, 5),
                        NotLoop = GetValue(values, 6),
                        VoicePrefix = GetValue(values, 7),
                        VoiceID = GetValue(values, 8),
                        BreathPrefix = GetValue(values, 9),
                        BreathID = GetValue(values, 10)
                    };
                    // IDの範囲指定（000~002など）を個別のIDリストに分解
                    data.ParsedVoiceIDs = ParseIDs(data.VoiceID);

                    _voiceDataList.Add(data);
                }
                Logger.LogInfo($"CSV読み込み完了: {_voiceDataList.Count}件のデータをロードしました。");
            }
            catch (Exception e)
            {
                Logger.LogError($"CSVの読み込み中にエラーが発生しました: {e.Message}");
            }
        }
        private string NormalizeOption(string opt)
        {
            if (opt == null)
                return "None";
            opt = opt.Trim();
            if (opt == "")
                return "None";
            return opt;
        }
        public VoiceData Get(string mode, string clip, string option, bool excited)
        {
            option = NormalizeOption(option);

            // ① Option一致 + Excited一致
            var data = FindInternal(mode, clip, option, excited);
            if (data != null)
                return data;
            // ② Option一致 + 非Excited
            data = FindInternal(mode, clip, option, !excited);
            if (data != null)
                return data;

            // ③ None + Excited一致
            if (option != "None")
            {
                data = FindInternal(mode, clip, "None", excited);
                if (data != null)
                    return data;
            }
            // ④ None + 非Excited
            if (option != "None")
            {
                data = FindInternal(mode, clip, "None", !excited);
                if (data != null)
                    return data;
            }

            return null;
        }
        private VoiceData FindInternal(string mode, string clip, string option, bool excited)
        {
            option = NormalizeOption(option);

            VoiceData fallback = null;

            foreach (var data in _voiceDataList)
            {
                if (data.AnimeMode != mode || data.AnimeClip != clip)
                    continue;
                // ★Option一致
                string dataOpt = NormalizeOption(data.Option);
                if (dataOpt != option)
                    continue;
                if (MatchExcited(data.Excited, excited))
                    return data;
                // Excited未指定はfallback候補
                if ((data.Excited == null) || data.Excited.Trim() == "")
                    fallback = data;
            }
            return fallback;
        }
        private bool MatchExcited(string csv, bool excited)
        {
            string ex = (csv ?? "").Trim();
            if (excited)
                return ex == "1";
            return string.IsNullOrEmpty(ex) || ex == "0";
        }
        public string GetVoiceFallback(string mode, string clip)
        {
            if (string.IsNullOrEmpty(mode) || string.IsNullOrEmpty(clip)) return null;

            foreach (var data in _voiceDataList)
            {
                // モードとクリップの両方が一致し、かつFallbackが空でない行のみを対象にする
                if (data.AnimeMode == mode && data.AnimeClip == clip && !string.IsNullOrEmpty(data.VoiceFallback))
                {
                    return data.VoiceFallback;
                }
            }
            // 一致する特定のクリップ設定がない場合は、安易に他から拾わず null を返す
            return null;
        }
        public string GetBreathFallback(string mode, string clip)
        {
            if (string.IsNullOrEmpty(mode) || string.IsNullOrEmpty(clip)) return null;

            foreach (var data in _voiceDataList)
            {
                if (data.AnimeMode == mode && data.AnimeClip == clip && !string.IsNullOrEmpty(data.BreathFallback))
                {
                    return data.BreathFallback;
                }
            }
            return null;
        }
        private string[] SplitCsvLine(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            var current = new StringBuilder();

            foreach (char c in line)
            {
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current.ToString());
                    current.Length = 0;
                }
                else
                {
                    current.Append(c);
                }
            }
            result.Add(current.ToString());
            return result.ToArray();
        }
        // 「000~002」や「001,002」といった形式を解析してリスト化する
        private List<string> ParseIDs(string rawId)
        {
            List<string> result = new List<string>();
            if (string.IsNullOrEmpty(rawId)) return result;

            // まずカンマで区切る
            string[] commaSplit = rawId.Split(',');
            foreach (var part in commaSplit)
            {
                string cleanPart = part.Trim();
                if (cleanPart.Contains("~"))
                {
                    // 範囲指定 (000~002) の処理
                    string[] range = cleanPart.Split('~');
                    if (range.Length == 2 && int.TryParse(range[0], out int start) && int.TryParse(range[1], out int end))
                    {
                        for (int i = start; i <= end; i++)
                        {
                            // 3桁の文字列（001など）として追加
                            result.Add(i.ToString("D3"));
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(cleanPart))
                {
                    // 単体指定の処理
                    result.Add(cleanPart);
                }
            }
            return result;
        }

        private string GetValue(string[] array, int index)
        {
            if (index >= array.Length) return "";
            return array[index].Trim('\"', ' ', '\r', '\n');
        }
    }
}