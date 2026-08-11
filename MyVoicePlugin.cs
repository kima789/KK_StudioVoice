using BepInEx;
using BepInEx.Configuration;
using KKAPI.Utilities;
using KKAPI.Studio.UI.Toolbars;
using Studio;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KK_StudioVoice
{
    [BepInPlugin("KK_StudioVoice", "KK_StudioVoice", "1.2.0")]
    [BepInProcess("CharaStudio")]
    public class MyVoicePlugin : BaseUnityPlugin
    {
        private int _lastObjectCount = -1;

        private Texture2D _toolbarIcon; // 1つのテクスチャを使い回す
        private byte[] _originalIconBytes; // 元の画像を保存しておく用

        // --- Config設定 ---
        private ConfigEntry<bool> _pluginEnabled;
        private ConfigEntry<KeyboardShortcut> _toggleKey;

        private ConfigEntry<bool> _defaultExcited;
        private ConfigEntry<bool> _defaultLipSync;
        private ConfigEntry<bool> _defaultVoice;
        private ConfigEntry<bool> _defaultBreath;

        private ConfigEntry<float> _windowX;
        private ConfigEntry<float> _windowY;

        // 前回のフレームでの有効フラグの状態を保持する変数
        private bool _lastPluginEnabledState = true;

        private float _lastSaveTime = 0f;

        private bool _showPanel = false;
        private Rect windowRect;
        private bool _cameraDisabledByMe = false;

        public VoicePlayer _player;
        public VoiceDataLoader _dataLoader;
        private VoiceCacheManager _cacheManager;

        private HashSet<VoiceCharComponent> _refreshRequests = new HashSet<VoiceCharComponent>();

        private string _defaultMode = "";
        private string _defaultClip = "";
        private string _defaultOption = "None";
        public readonly string[] ExpList = { "First Time", "Amateur", "Pro", "Lewd" };

        // --- タイムラインと連動するプロパティ群 ---
        // これらは「代入された瞬間に変化をチェックし、変化があれば再生（RefreshVoice）を叩く」
        public string SelectedMode
        {
            get => GetActiveCharacterComponent()?.SelectedMode ?? _defaultMode;
            set
            {
                var chara = GetActiveCharacterComponent();
                if (chara != null && chara.SetMode(value))
                {
                    if (_isInitialized) RequestRefresh(chara);
                }
            }
        }
        public string SelectedClip
        {
            get => GetActiveCharacterComponent()?.SelectedClip ?? _defaultClip;
            set
            {
                var chara = GetActiveCharacterComponent();
                if (chara != null && chara.SetClip(value))
                {
                    if (_isInitialized) RequestRefresh(chara);
                }
            }
        }
        public string SelectedOption
        {
            get => GetActiveCharacterComponent()?.SelectedOption ?? _defaultOption;
            set
            {
                var chara = GetActiveCharacterComponent();
                if (chara != null && chara.SetOption(value))
                {
                    if (_isInitialized) RequestRefresh(chara);
                }
            }
        }
        public string SelectedExp
        {
            get => GetActiveCharacterComponent()?.SelectedExp ?? "";
            set
            {
                var chara = GetActiveCharacterComponent();
                if (chara != null && chara.SetExp(value))
                {
                    if (_isInitialized) RequestRefresh(chara);
                }
            }
        }
        public bool IsExcited
        {
            get => GetActiveCharacterComponent()?.IsExcited ?? false;
            set
            {
                var chara = GetActiveCharacterComponent();
                if (chara != null && chara.SetExcited(value))
                {
                    if (_isInitialized) RequestRefresh(chara);
                }
            }
        }
        public bool IsLipSyncOn
        {
            get => GetActiveCharacterComponent()?.IsLipSyncOn ?? false;
            set
            {
                var chara = GetActiveCharacterComponent();
                if (chara != null && chara.SetLipSyncOn(value))
                {
                }
            }
        }
        public bool IsVoiceOn
        {
            get => GetActiveCharacterComponent()?.IsVoiceOn ?? false;
            set
            {
                var chara = GetActiveCharacterComponent();
                if (chara != null && chara.SetVoiceOn(value))
                {
                    if (_isInitialized) RequestRefresh(chara);
                }
            }
        }
        public bool IsBreathOn
        {
            get => GetActiveCharacterComponent()?.IsBreathOn ?? false;
            set
            {
                var chara = GetActiveCharacterComponent();
                if (chara != null && chara.SetBreathOn(value))
                {
                    if (_isInitialized) RequestRefresh(chara);
                }
            }
        }
        public void RequestRefresh(VoiceCharComponent chara)
        {
            if (!_isInitialized) return;
            if (!_refreshRequests.Contains(chara))
            {
                _refreshRequests.Add(chara);
            }
        }
        public bool IsPlaying
        {
            get => GetActiveCharacterComponent()?.IsPlaying ?? false;
            set
            {
                var chara = GetActiveCharacterComponent();
                if (chara == null) return;

                // 値が変わったかどうかに関わらず、今の命令が「停止(false)」なら即座に叩く
                if (value == false)
                {
                    _player.StopVoice(chara);
                }
                // 内部変数の更新と、再生開始時のリフレッシュ
                if (chara.SetPlaying(value))
                {
                    if (value && _isInitialized)
                    {
                        RefreshVoice(chara);
                    }
                }
            }
        }
        private bool showModeList = false;
        private bool showClipList = false;
        private bool showOptionList = false;

        private Vector2 scrollPosMode;
        private Vector2 scrollPosClip;
        private Vector2 scrollPosOption;

        private VoiceTimelineCompat _timelineCompat;

        private bool _isInitialized = false; // 初期化完了フラグ
        private void Awake()
        {
            // 1. プラグイン自体の有効・無効 ＆ ショートカットキー
            _pluginEnabled = Config.Bind("General", "Enabled", true, "プラグインの有効/無効の切り替え");
            _toggleKey = Config.Bind("General", "Hot Key", new KeyboardShortcut(KeyCode.None), "切り替えのショートカットキー");

            // 2. 各トグルの初期値設定（Order を指定して画面上の並び順を上から固定）
            _defaultExcited = Config.Bind("Default", "Excited", false, new ConfigDescription("Excited の初期値", null, new ConfigurationManagerAttributes { Order = 4 }));
            _defaultLipSync = Config.Bind("Default", "LipSync", true, new ConfigDescription("LipSync の初期値", null, new ConfigurationManagerAttributes { Order = 3 }));
            _defaultVoice = Config.Bind("Default", "Voice", true, new ConfigDescription("Voice の初期値", null, new ConfigurationManagerAttributes { Order = 2 }));
            _defaultBreath = Config.Bind("Default", "Breath", true, new ConfigDescription("Breath の初期値", null, new ConfigurationManagerAttributes { Order = 1 }));

            // --- 3. UI Window 位置 (F1メニュー非表示設定) ---
            var hideInF1 = new ConfigDescription("UI Window Position", null, new ConfigurationManagerAttributes { Browsable = false });

            _windowX = Config.Bind("UI", "WindowPositionX", 300f, hideInF1);
            _windowY = Config.Bind("UI", "WindowPositionY", 100f, hideInF1);

            // 保存されていた座標を適用（幅320、高さ50は既存のまま）
            windowRect = new Rect(_windowX.Value, _windowY.Value, 320f, 50f);

            _dataLoader = new VoiceDataLoader();
            string dllPath = System.IO.Path.GetDirectoryName(Info.Location);
            _dataLoader.LoadCsv(System.IO.Path.Combine(dllPath, "KK_StudioVoice.csv"));
            _cacheManager = gameObject.AddComponent<VoiceCacheManager>();
            _player = gameObject.AddComponent<VoicePlayer>();
            _player.Init(_cacheManager);

            StartCoroutine(AddButtonSafe());

            _timelineCompat = new VoiceTimelineCompat(this);

            _isInitialized = true;

            gameObject.AddComponent<LipSyncController>();

            // CSVの最初にあるデータを「プラグインのバックアップ変数」に入れる
            var firstData = _dataLoader.VoiceDataList.FirstOrDefault();
            if (firstData != null)
            {
                _defaultMode = firstData.AnimeMode;
                _defaultClip = firstData.AnimeClip;
            }
            // 自動開閉処理をスタート
            StartCoroutine(AutoOpenAndCloseWindow());

            SceneManager.sceneUnloaded += OnSceneUnloadedForVoice;
        }
        private void OnSceneUnloadedForVoice(Scene scene)
        {
            try
            {
                // プレイヤーが MonoBehaviour ならコルーチンを止める
                if (_player != null)
                {
                    try { _player.StopAllCoroutines(); } catch { }
                    try { _player.ResetAll(); } catch { } // ResetAll があれば内部状態をクリア
                }
            }
            catch (Exception e)
            {
                SLog.Log.LogWarning($"[KK_StudioVoice] OnSceneUnloadedForVoice error: {e}");
            }
        }
        private IEnumerator AutoOpenAndCloseWindow()
        {
            // スタジオが起動し、画面が落ち着くまで少し待つ（1.5秒）
            yield return new WaitForSeconds(1.5f);

            // ウィンドウを一瞬だけ開く
            _showPanel = true;
            UpdateToolbarIcon();

            // 1フレーム待つ（これにより OnGUI が1回以上走り、レイアウトが確定する）
            yield return null;

            // ウィンドウを閉じる
            _showPanel = false;
            UpdateToolbarIcon();
        }
        private void Update()
        {
            // 設定キーのショートカット判定
            if (_toggleKey.Value.IsDown())
            {
                _pluginEnabled.Value = !_pluginEnabled.Value;
                SLog.Log.LogMessage("StudioVoice " + (_pluginEnabled.Value ? "Enabled" : "Disabled"));
            }
            // 1. 有効 → 無効 に切り替わった瞬間（OFFにした時）
            if (_lastPluginEnabledState && !_pluginEnabled.Value)
            {
                if (_player != null)
                {
                    _player.ResetAll(); // 即座に音声・コルーチンを物理停止
                }
            }
            // 2. 無効 → 有効 に切り替わった瞬間（ONに戻した時）
            else if (!_lastPluginEnabledState && _pluginEnabled.Value)
            {
                // 現在スタジオにいる全キャラから IsPlaying == true のキャラを探して再開
                if (Singleton<Studio.Studio>.Instance != null)
                {
                    foreach (var pair in Singleton<Studio.Studio>.Instance.dicObjectCtrl)
                    {
                        // 1イテレーションを try/catch で保護して、1つのオブジェクトの例外で処理が止まらないようにする
                        try
                        {
                            if (pair.Value == null) continue;

                            if (!(pair.Value is OCIChar ociChar)) continue;

                            // null 安全に guideObject / transformTarget / gameObject をたどる
                            var guide = ociChar.guideObject;
                            if (guide == null) continue;

                            var transformTarget = guide.transformTarget;
                            if (transformTarget == null) continue;

                            var targetGo = transformTarget.gameObject;
                            if (targetGo == null) continue;

                            // キャラコンポーネントを取得（無ければスキップ）
                            var charaComp = targetGo.GetComponent<VoiceCharComponent>();
                            if (charaComp == null) continue;

                            // 追加の安全チェック：charInfo や LastVisible 参照で NRE を防ぐ
                            bool lastVisible = false;
                            try { lastVisible = charaComp.LastVisible; } catch { lastVisible = false; }

                            // キャラが存在し、表示中で、再生フラグ（IsPlaying）がONなら再生を開始
                            if (charaComp.IsPlaying && lastVisible)
                            {
                                RefreshVoice(charaComp);
                            }
                        }
                        catch (Exception ex)
                        {
                            // ここではログを出すが処理は続行する
                            SLog.Log.LogWarning($"[KK_StudioVoice]dicObjectCtrlのスキャン中にエラーが発生しました。: {ex}");
                            continue;
                        }
                    }
                }
            }
            // 今のフレームの状態を記憶
            _lastPluginEnabledState = _pluginEnabled.Value;
        }
        private void LateUpdate()
        {
            // プラグインが無効の場合は処理しない
            if (!_pluginEnabled.Value) return;

            DetectSceneReload();

            if (_refreshRequests.Count > 0)
            {
                // リクエストのコピーを作成してから処理する（処理中のリスト変更によるエラーを防止）
                var targets = _refreshRequests.ToList();
                _refreshRequests.Clear();

                foreach (var chara in targets)
                {
                    // 1. コンポーネント自体が消えていないか
                    // 2. キャラクターのGameObjectがまだ存在しているか
                    // 3. キャラクターがアクティブ（有効）な状態か
                    if (chara != null && chara.gameObject != null && chara.gameObject.activeInHierarchy)
                    {
                        RefreshVoice(chara);
                    }
                }
            }
            if (Singleton<Studio.Studio>.Instance != null)
            {
                var studio = Singleton<Studio.Studio>.Instance;

                // 全てのオブジェクトからキャラクター(OCIChar)を探す
                foreach (var pair in studio.dicObjectCtrl)
                {
                    if (pair.Value is OCIChar ociChar)
                    {
                        // キャラクターに付いているコンポーネントを取得
                        var targetGo = ociChar.guideObject.transformTarget.gameObject;
                        // まず既存のコンポーネントを取得
                        var charaComp = targetGo.GetComponent<VoiceCharComponent>();

                        // 無ければ安全条件を満たすときだけ自動付与を試みる
                        if (charaComp == null)
                        {
                            charaComp = AttachVoiceCharComponentSafely(targetGo);
                        }

                        // charaComp が取れなければスキップ（元と同等の動作）
                        if (charaComp == null)
                            continue;
                        int currentPersonality =
                                ociChar.charInfo.chaFile.parameter.personality;

                            // 初回
                            if (charaComp.LastPersonality == 0)
                            {
                                charaComp.LastPersonality = currentPersonality;
                            }
                            // 置換検知
                            else if (charaComp.LastPersonality != currentPersonality)
                            {
                                charaComp.LastPersonality = currentPersonality;

                                bool shouldResume = charaComp.IsPlaying;

                                charaComp.ReservedNextAction = null;

                                _player.StopVoice(charaComp, false);

                                charaComp.IsVoicePlaying = false;
                                charaComp.IsCurrentNotLoop = false;
                                charaComp.ReservedNextAction = null;

                                StartCoroutine(RefreshVoiceDelayed(charaComp, shouldResume));
                            }
                            // スタジオ上の現在の表示フラグを取得
                            bool currentVisible = true;
                            TreeNodeObject node = ociChar.treeNodeObject;

                            // 親ノードを辿って、1つでも非表示（visible == false）があれば false にする
                            while (node != null)
                            {
                                if (!node.visible)
                                {
                                    currentVisible = false;
                                    break;
                                }
                                node = node.parent;
                            }
                            // 判定結果を使って処理
                            if (currentVisible != charaComp.LastVisible)
                            {
                                charaComp.LastVisible = currentVisible;
                                if (currentVisible)
                                {
                                    if (charaComp.IsPlaying)
                                    {
                                        _player.StopVoice(charaComp);
                                        RefreshVoice(charaComp);
                                    }
                                }
                                else
                                {
                                    _player.StopVoice(charaComp, true);
                                }
                            }
                        }
                    }
                }
            }
        private void OnDestroy()
        {
            try { SceneManager.sceneUnloaded -= OnSceneUnloadedForVoice; } catch { }
            try { _timelineCompat?.Dispose(); } catch { }
        }
        private VoiceCharComponent AttachVoiceCharComponentSafely(GameObject go)
        {
            if (go == null) return null;
            // アクティブでないオブジェクトには付けない
            if (!go.activeInHierarchy) return null;

            // scene チェック（アンロード中のオブジェクトを避ける）
            try
            {
                var scene = go.scene;
                if (!scene.IsValid() || !scene.isLoaded) return null;
            }
            catch
            {
                return null;
            }

            // 既に付いていればそれを返す
            var comp = go.GetComponent<VoiceCharComponent>();
            if (comp != null) return comp;

            // ここで安全に追加して初期化
            try
            {
                comp = go.AddComponent<VoiceCharComponent>();
                // 初期値を反映（既存 GetActiveCharacterComponent と同じ初期化）
                comp.SetMode(_defaultMode);
                comp.SetClip(_defaultClip);
                comp.SetOption(_defaultOption);
                comp.SetExcited(_defaultExcited.Value);
                comp.SetLipSyncOn(_defaultLipSync.Value);
                comp.SetVoiceOn(_defaultVoice.Value);
                comp.SetBreathOn(_defaultBreath.Value);
                return comp;
            }
            catch (Exception e)
            {
                SLog.Log.LogWarning($"[KK_StudioVoice]AttachVoiceCharComponentSafelyが失敗しました。: {e}");
                return null;
            }
        }
        private IEnumerator RefreshVoiceDelayed(
            VoiceCharComponent charaComp,
            bool shouldResume)
        {
            if (!shouldResume || charaComp == null)
                yield break;

            var cha = charaComp.GetComponent<ChaControl>();
            if (cha == null)
                yield break;

            int pId = cha.chaFile.parameter.personality;

            float timeout = Time.realtimeSinceStartup + 30f;

            while (Time.realtimeSinceStartup < timeout)
            {
                if (charaComp == null ||
                    charaComp.gameObject == null)
                    yield break;

                if (_cacheManager.IsLoaded(pId))
                    break;

                yield return null;
            }
            if (!_cacheManager.IsLoaded(pId))
            {
                SLog.Log.LogWarning($"[Voice] ロードタイムアウト: personality={pId}");
                yield break;
            }

            if (!charaComp.gameObject.activeInHierarchy)
                yield break;

            RefreshVoice(charaComp);
        }
        private void DetectSceneReload()
        {
            if (Singleton<Studio.Studio>.Instance == null)
                return;

            int currentCount =
                Singleton<Studio.Studio>.Instance.dicObjectCtrl.Count;

            // 初回
            if (_lastObjectCount == -1)
            {
                _lastObjectCount = currentCount;
                return;
            }
            // オブジェクト数が激変したらシーンロード扱い
            if (Mathf.Abs(currentCount - _lastObjectCount) > 5)
            {
                _player.ResetAll();
            }
            _lastObjectCount = currentCount;
        }
        //　アイコンの生成
        private IEnumerator AddButtonSafe()
        {
            yield return new WaitForSeconds(1.0f);

            _toolbarIcon = new Texture2D(32, 32);
            try
            {
                string dllPath = System.IO.Path.GetDirectoryName(Info.Location);
                string iconPath = System.IO.Path.Combine(dllPath, "KK_StudioVoice.png");

                if (System.IO.File.Exists(iconPath))
                {
                    _originalIconBytes = System.IO.File.ReadAllBytes(iconPath);
                    _toolbarIcon.LoadImage(_originalIconBytes);
                }
                else
                {
                    // 代替画像
                    for (int x = 0; x < 32; x++)
                        for (int y = 0; y < 32; y++)
                            _toolbarIcon.SetPixel(x, y, Color.gray);
                    _toolbarIcon.Apply();
                }
                // ボタン作成
                var myButton = new SimpleToolbarButton(
                    "VoicePlayerButton",
                    "KK_StudioVoice",
                    () => _toolbarIcon, // 常に同じテクスチャを返すが、中身が書き換わる
                    this,
                    _ => {
                        _showPanel = !_showPanel;
                        UpdateToolbarIcon(); // クリック時に中身を書き換える
                    }
                );
                ToolbarManager.AddLeftToolbarControl(myButton);
            }
            catch (Exception e) { SLog.Log.LogError($"Toolbar Error: {e.Message}"); }
        }
        // --- アイコンの色を直接上書きするメソッド ---
        private void UpdateToolbarIcon()
        {
            if (_toolbarIcon == null) return;

            if (_showPanel)
            {
                Color activeGreen = new Color(0.2f, 1.0f, 0.2f, 1.0f);

                Color[] pixels = _toolbarIcon.GetPixels();
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color c = pixels[i];

                    pixels[i] = new Color(
                        c.r * activeGreen.r,
                        c.g * activeGreen.g * 1.1f,
                        c.b * activeGreen.b,
                        c.a
                    );
                }
                _toolbarIcon.SetPixels(pixels);
            }
            else
            {
                if (_originalIconBytes != null)
                    _toolbarIcon.LoadImage(_originalIconBytes);
            }
            _toolbarIcon.Apply();
        }
        /// 現在選択されているキャラクターから VoiceCharComponent を取得します。
        /// /// コンポーネントが存在しない場合は、その場で新しく追加します。
        public VoiceCharComponent GetActiveCharacterComponent()
        {
            var ociChar = KKAPI.Studio.StudioAPI.GetSelectedCharacters().FirstOrDefault();
            if (ociChar == null) return null;

            var targetGo = ociChar?.guideObject?.transformTarget?.gameObject;
            if (targetGo == null) return null;

            // まず既存のコンポーネントを返す（副作用なし）
            var comp = targetGo.GetComponent<VoiceCharComponent>();
            if (comp != null) return comp;

            // 無ければ安全付与して返す（Attach 内で安全チェック済み）
            return AttachVoiceCharComponentSafely(targetGo);
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
        public void RefreshVoice(VoiceCharComponent chara)
        {
            // プラグインが無効の場合は一切再生を受け付けない
            if (!_pluginEnabled.Value)
                return;

            if (chara == null || !chara.gameObject.activeInHierarchy)
                return;
            // 非表示なら再生しない
            if (!chara.LastVisible)
                return;

            // そのキャラが再生フラグを立てていないなら止める
            if (!chara.IsPlaying)
            {
                _player.StopVoice(chara); // 誰の音を止めるか指定
                return;
            }
            // ボイスもブレスもOFFなら何もしない
            if (!chara.IsVoiceOn && !chara.IsBreathOn) return;

            // そのキャラの設定値を取得
            int expValue = Array.IndexOf(ExpList, chara.SelectedExp);
            if (expValue == -1) expValue = 0;

            var chaCtrl = chara.GetComponentInParent<ChaControl>();

            // 再生を実行（charaを渡すことで、Player側で監視できるようにする）
            _player.PlaySelectedVoice(
                chara,
                chaCtrl,
                chara.SelectedMode,
                chara.SelectedClip,
                chara.SelectedOption,
                chara.IsExcited,
                expValue,_dataLoader,
                chara.IsVoiceOn,
                chara.IsBreathOn
            );
        }
        private void OnGUI()
        {
            if (!_showPanel)
            {
                if (_cameraDisabledByMe) RestoreState();
                return;
            }
            Vector2 mousePos = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            bool isMouseInWindow = windowRect.Contains(mousePos);

            if (isMouseInWindow || GUIUtility.hotControl != 0)
            {
                // 1. カメラ制御の停止（マウスでのカメラ回転・移動を防ぐ）
                var cameraCtrl = Studio.Studio.Instance != null ? Studio.Studio.Instance.cameraCtrl : null;
                if (cameraCtrl != null && cameraCtrl.enabled)
                {
                    cameraCtrl.enabled = false;
                    _cameraDisabledByMe = true;
                }
                // 2. スタジオUI（Canvas）への貫通防止
                if (UnityEngine.EventSystems.EventSystem.current != null)
                {
                    UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
                }
            }
            else if (_cameraDisabledByMe)
            {
                RestoreState();
            }
            // --- ウィンドウ描画 ---
            Rect oldRect = windowRect; // ドラッグ前の座標を覚えておく

            windowRect = GUI.Window(123456, windowRect, DrawVoicePanel, "KK_StudioVoice");

            // 座標が変わっていて、かつ前回の保存から0.5秒以上経っている場合だけ保存
            if (windowRect.x != oldRect.x || windowRect.y != oldRect.y)
            {
                // Time.unscaledTime を使うことで、ゲームの一時停止中などでも正確に時間を測れます
                if (Time.unscaledTime - _lastSaveTime > 0.5f)
                {
                    _windowX.Value = windowRect.x;
                    _windowY.Value = windowRect.y;
                    _lastSaveTime = Time.unscaledTime;
                }
            }
            // --- マウス操作（クリック等）だけをブロックし、キー入力は一切触らない ---
            if (isMouseInWindow)
            {
                // キーボード関連イベント（isKey）は Use() せずスルーさせる
                if (!Event.current.isKey && Event.current.type != EventType.Layout && Event.current.type != EventType.Repaint)
                {
                    Event.current.Use();
                }
            }
        }
        private void RestoreState()
        {
            var cameraCtrl = Studio.Studio.Instance != null ? Studio.Studio.Instance.cameraCtrl : null;
            if (cameraCtrl != null) cameraCtrl.enabled = true;
            _cameraDisabledByMe = false;
        }
        private void DrawVoicePanel(int windowID)
        {
            // マウス操作が行われた際、このウィンドウにフォーカスを当ててイベントを消費させる
            if (Event.current.type == EventType.MouseDown)
            {
                GUI.FocusWindow(windowID);
            }
            GUI.DragWindow(new Rect(0, 0, 320, 20));
            const float x = 15f, w = 290f, itemH = 22f, listH = 150f, space = 5f;
            float currentY = 25f;

            // --- 1. Anime Mode ---
            GUI.Label(new Rect(x, currentY, w, itemH), "【Anime Mode】");
            currentY += itemH;
            if (GUI.Button(new Rect(x, currentY, w, itemH), SelectedMode)) { showModeList = !showModeList; showClipList = false; }
            currentY += itemH + space;

            if (showModeList)
            {
                var modes = _dataLoader.VoiceDataList.Select(d => d.AnimeMode).Distinct().ToList();
                Rect viewRect = new Rect(0, 0, w - 20f, modes.Count * 24f);
                scrollPosMode = GUI.BeginScrollView(new Rect(x, currentY, w, listH), scrollPosMode, viewRect);
                float ly = 0;
                foreach (var m in modes)
                {
                    if (GUI.Button(new Rect(0, ly, viewRect.width, itemH), m))
                    {
                        SelectedMode = m;
                        showModeList = false;

                        if (!_dataLoader.VoiceDataList.Any(d => d.AnimeMode == m && d.AnimeClip == SelectedClip))
                            SelectedClip = _dataLoader.VoiceDataList.First(d => d.AnimeMode == m).AnimeClip;

                        // 現在Optionが存在しなければNoneへ
                        bool optionExists = _dataLoader.VoiceDataList.Any(d =>
                            d.AnimeMode == SelectedMode &&
                            d.AnimeClip == SelectedClip &&
                            NormalizeOption(d.Option) == NormalizeOption(SelectedOption));

                        if (!optionExists)
                            SelectedOption = "None";
                    }
                    ly += 24f;
                }
                GUI.EndScrollView(); currentY += listH + space;
            }

            // --- 2. Anime Clip ---
            GUI.Label(new Rect(x, currentY, w, itemH), "【Anime Clip】");
            currentY += itemH;
            if (GUI.Button(new Rect(x, currentY, w, itemH), string.IsNullOrEmpty(SelectedClip) ? "(Select Mode First)" : SelectedClip))
            {
                showClipList = !showClipList; showModeList = false;
            }
            currentY += itemH + space;

            if (showClipList)
            {
                var query = _dataLoader.VoiceDataList.Where(d => d.AnimeMode == SelectedMode);
                var clips = query.Select(d => d.AnimeClip).Distinct().ToList();

                Rect viewRect = new Rect(0, 0, w - 20f, clips.Count * 24f);
                scrollPosClip = GUI.BeginScrollView(new Rect(x, currentY, w, listH), scrollPosClip, viewRect);
                float ly = 0;
                foreach (var c in clips)
                {
                    if (GUI.Button(new Rect(0, ly, viewRect.width, itemH), c))
                    {
                        SelectedClip = c;
                        showClipList = false;
                        // 現在Optionが存在しなければNoneへ
                        bool optionExists = _dataLoader.VoiceDataList.Any(d =>
                            d.AnimeMode == SelectedMode &&
                            d.AnimeClip == SelectedClip &&
                            NormalizeOption(d.Option) == NormalizeOption(SelectedOption));

                        if (!optionExists)
                            SelectedOption = "None";
                    }
                    ly += 24f;
                }
                GUI.EndScrollView(); currentY += listH + space;
            }

            // --- 3. Option ---
            GUI.Label(new Rect(x, currentY, w, itemH), "【Option】");
            currentY += itemH;

            if (GUI.Button(new Rect(x, currentY, w, itemH),
                string.IsNullOrEmpty(SelectedClip) ? "(Select Clip First)" : SelectedOption))
            {
                showOptionList = !showOptionList;
                showModeList = false;
                showClipList = false;
            }
            currentY += itemH + space;

            if (showOptionList)
            {
                var query = _dataLoader.VoiceDataList
                    .Where(d => d.AnimeMode == SelectedMode && d.AnimeClip == SelectedClip);

                var options = query
                    .Select(d => NormalizeOption(d.Option))
                    .Distinct()
                    .ToList();

                // 念のためNone保証
                if (!options.Contains("None"))
                    options.Insert(0, "None");

                Rect viewRect = new Rect(0, 0, w - 20f, options.Count * 24f);
                scrollPosOption = GUI.BeginScrollView(new Rect(x, currentY, w, listH), scrollPosOption, viewRect);

                float ly = 0;
                foreach (var o in options)
                {
                    if (GUI.Button(new Rect(0, ly, viewRect.width, itemH), o))
                    {
                        SelectedOption = o;
                        showOptionList = false;
                    }
                    ly += 24f;
                }
                GUI.EndScrollView();
                currentY += listH + space;
            }
            // --- 3. Exp ---
            GUI.Label(new Rect(x, currentY, w, itemH), "【Exp】");
            currentY += itemH;
            float ew = (w - space) / 2f;
            for (int i = 0; i < ExpList.Length; i++)
            {
                string expName = ExpList[i];
                GUI.color = (SelectedExp == expName) ? Color.yellow : Color.white;
                if (GUI.Button(new Rect(x + (i % 2) * (ew + space), currentY + (i / 2) * 27f, ew, 25f), expName))
                {
                    SelectedExp = expName;
                }
            }
            GUI.color = Color.white; currentY += 58f;

            // --- 4. Toggles ---
            float tw = (w - space) / 2f;
            IsExcited = GUI.Toggle(new Rect(x, currentY, tw, itemH), IsExcited, " Excited");
            IsLipSyncOn = GUI.Toggle(new Rect(x + tw + space, currentY, tw, itemH), IsLipSyncOn, " LipSync");
            currentY += itemH + space;
            IsVoiceOn = GUI.Toggle(new Rect(x, currentY, tw, itemH), IsVoiceOn, " Voice");
            IsBreathOn = GUI.Toggle(new Rect(x + tw + space, currentY, tw, itemH), IsBreathOn, " Breath");
            currentY += itemH + space;

            // --- 5. 再生ボタン ---
            GUI.backgroundColor = IsPlaying ? new Color(1.0f, 0.3f, 0.3f) : Color.white;
            string btnText = IsPlaying ? "STOP ■" : "PLAY ▶";

            if (GUI.Button(new Rect(x, currentY, w, 40f), btnText))
            {// プロパティを反転させるだけで、中の setter が動き 
                // RefreshVoice(chara) もしくは StopVoice(chara) が自動で呼ばれる
                IsPlaying = !IsPlaying;
            }
            currentY += 45f;

            // --- 6. タイムラインへのキーフレーム追加ボタン ---
            GUI.backgroundColor = Color.white;
            if (GUI.Button(new Rect(x, currentY, w, 25f), "Add Timeline Keys"))
            {
                _timelineCompat?.AddStudioVoiceKeyframes();
            }
            currentY += 30f;

            // --- 7. 閉じるボタン ---
            if (GUI.Button(new Rect(x, currentY, w, 25f), "Close"))
            {
                _showPanel = false;
                UpdateToolbarIcon(); // ここでも呼び出して色を戻す
            }
            windowRect.height = currentY + 30f;
        }
    }
}