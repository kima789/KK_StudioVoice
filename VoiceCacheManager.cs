using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Studio;

namespace KK_StudioVoice
{
    public class VoiceCacheManager : MonoBehaviour
    {
        private static BepInEx.Logging.ManualLogSource Logger =
            BepInEx.Logging.Logger.CreateLogSource("VoiceCache");
        // キーを「性格番号_ファイル名」にすることでパスの表記揺れ問題を完全に回避する
        private Dictionary<string, AssetBundle> _bundleCache;
        private HashSet<int> _loadingPersonalities = new HashSet<int>();
        private HashSet<int> _loadedPersonalities = new HashSet<int>();
        private Dictionary<int, float> _lastUsedTime = new Dictionary<int, float>();
        private const float UNLOAD_DELAY = 10f; // 秒


        private void Awake()
        {
            _bundleCache = new Dictionary<string, AssetBundle>();
            ScanAndLoadCharacters();
        }

        private void Update()
        {
            if (Time.frameCount % 60 == 0)
            {
                ScanAndLoadCharacters();
            }
        }

        private void ScanAndLoadCharacters()
        {
            if (Singleton<Studio.Studio>.Instance?.dicObjectCtrl == null) return;

            var dict = Singleton<Studio.Studio>.Instance.dicObjectCtrl;
            HashSet<int> active = new HashSet<int>();

            foreach (var pair in dict)
            {
                if (pair.Value is OCIChar ociChar && ociChar.charInfo != null)
                {
                    var cha = ociChar.charInfo;

                    // ★ここ追加：女性キャラだけ通す
                    if (cha.sex != 1) // 0=男, 1=女
                        continue;

                    int pId = ociChar.charInfo.chaFile.parameter.personality;

                    active.Add(pId);
                    _lastUsedTime[pId] = Time.time;

                    if (!_loadingPersonalities.Contains(pId)
                        && !_loadedPersonalities.Contains(pId))
                    {
                        _loadingPersonalities.Add(pId);
                        StartCoroutine(LoadAllBundlesForPersonality(pId));
                    }
                }
            }

            UnloadUnusedBundles(active);
        }
        private IEnumerator LoadAllBundlesForPersonality(int pId)
        {
            try
            {
                string pStr = $"c{pId:00}";
                string baseDir = Path.GetDirectoryName(Application.dataPath);

                string[] subFolders = new string[]
                {
            "h",
            "adm"
                };
                foreach (var sub in subFolders)
                {
                    string folderPath = Path.Combine(
                        baseDir,
                        $"abdata/sound/data/pcm/{pStr}/{sub}/"
                    );
                    if (!Directory.Exists(folderPath))
                    {
                        Logger.LogWarning($"[Cache] フォルダ未検出: {folderPath}");
                        continue;
                    }
                    string[] files = Directory.GetFiles(folderPath, "*.unity3d");

                    foreach (string path in files)
                    {
                        string fName =
                            Path.GetFileNameWithoutExtension(path).ToLower();

                        string key = $"{pId}_{fName}";

                        if (_bundleCache.ContainsKey(key))
                            continue;

                        AssetBundleCreateRequest req =
                            AssetBundle.LoadFromFileAsync(path);

                        yield return req;

                        if (req.assetBundle != null)
                        {
                            _bundleCache[key] = req.assetBundle;

                            Logger.LogInfo(
                                $"[Cache] ロード成功: Key={key} ({sub})"
                            );
                        }
                    }
                }
            }
            finally
            {
                _loadingPersonalities.Remove(pId);

                if (GetAllBundles(pId).Count > 0)
                {
                    _loadedPersonalities.Add(pId);
                }
            }
        }
        public bool IsLoading(int pId)
        {
            return _loadingPersonalities.Contains(pId);
        }
        // プレイヤーから呼ばれるメソッド
        public List<AssetBundle> GetAllBundles(int pId)
        {
            List<AssetBundle> result = new List<AssetBundle>();

            foreach (var kv in _bundleCache)
            {
                // keyは「pId_filename」形式
                if (kv.Key.StartsWith(pId.ToString() + "_"))
                {
                    if (kv.Value != null)
                        result.Add(kv.Value);
                }
            }
            return result;
        }
        public AssetBundle GetBundle(int pId, string fileName)
        {
            // 検索時も小文字にして「性格番号_ファイル名」で引く
            string fName = fileName.ToLower();
            string key = $"{pId}_{fName}";

            if (_bundleCache.TryGetValue(key, out var bundle))
            {
                return bundle;
            }

            // 失敗した場合のデバッグ情報
            Logger.LogWarning($"[Cache Miss] 登録キーが見つかりません: {key}");
            Logger.LogInfo("現在キャッシュ済みのキー: " + string.Join(", ", _bundleCache.Keys.ToArray()));

            return null;
        }
        private void UnloadUnusedBundles(HashSet<int> active)
        {
            var keys = _bundleCache.Keys.ToList();
            HashSet<int> unloadedPIds = new HashSet<int>();

            foreach (var key in keys)
            {
                int pId = int.Parse(key.Split('_')[0]);

                if (active.Contains(pId)) continue;

                if (_lastUsedTime.TryGetValue(pId, out float lastTime))
                {
                    if (Time.time - lastTime < UNLOAD_DELAY) continue;
                }

                var bundle = _bundleCache[key];
                if (bundle != null)
                {
                    bundle.Unload(false);
                }

                _bundleCache.Remove(key);
                unloadedPIds.Add(pId);
            }

            foreach (var pId in unloadedPIds)
            {
                _loadingPersonalities.Remove(pId);
                _loadedPersonalities.Remove(pId);
                Logger.LogInfo($"[Unload] 性格 {pId} のバンドル解放");
            }
        }
        public bool IsLoaded(int pId)
        {
            return _loadedPersonalities.Contains(pId);
        }
        private void OnDestroy()
        {
            foreach (var b in _bundleCache.Values) if (b != null) b.Unload(true);
            _bundleCache.Clear();
        }
    }
}