using BepInEx;
using BepInEx.Configuration;
using KKAPI.Studio.UI.Toolbars;
using Studio;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KK_StudioVoice
{
    [BepInPlugin("com.example.voiceplayer", "KK Studio Voice Player", "1.1.0")]
    [BepInProcess("CharaStudio")]
    public class MyVoicePlugin : BaseUnityPlugin
    {
        private int _lastObjectCount = -1;

        private Texture2D _toolbarIcon; // 1つのテクスチャを使い回す
        private byte[] _originalIconBytes; // 元の画像を保存しておく用

        private ConfigEntry<float> _windowX;
        private ConfigEntry<float> _windowY;

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
            // --- 設定ファイルの初期化 (Configのセクション名、キー名、デフォルト値、説明) ---
            _windowX = Config.Bind("UI Settings", "WindowPositionX", 300f, "The X position of the voice player window.");
            _windowY = Config.Bind("UI Settings", "WindowPositionY", 100f, "The Y position of the voice player window.");

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
            // ★ ここに追加：自動開閉処理をスタート
            StartCoroutine(AutoOpenAndCloseWindow());
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
        private void LateUpdate()
        {
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
                        var charaComp = targetGo.GetComponent<VoiceCharComponent>();

                        if (charaComp != null)
                        {
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
                                Debug.Log($"[Voice] キャラ置換検知: {ociChar.charInfo.name}");

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
                            bool currentVisible = ociChar.treeNodeObject.visible;

                            if (currentVisible != charaComp.LastVisible)
                            {
                                charaComp.LastVisible = currentVisible;
                                if (currentVisible)
                                {
                                    Debug.Log($"表示: {ociChar.charInfo.name}");
                                    if (charaComp.IsPlaying)
                                    {
                                        _player.StopVoice(charaComp);
                                        RefreshVoice(charaComp);
                                    }
                                }
                                else
                                {
                                    Debug.Log($"非表示: {ociChar.charInfo.name}");
                                    _player.StopVoice(charaComp, true);
                                }
                            }
                        }
                    }
                }
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
                Debug.LogWarning($"[Voice] ロードタイムアウト: personality={pId}");
                yield break;
            }

            if (!charaComp.gameObject.activeInHierarchy)
                yield break;

            Debug.Log("[Voice] キャッシュロード完了 → RefreshVoice");

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
                Debug.Log("[KK_StudioVoice] Scene reload detected");

                _player.ResetAll();
            }

            _lastObjectCount = currentCount;
        }
        //　アイコンの生成
        // --- AddButtonSafe メソッド ---
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
                    "Voice Player",
                    () => _toolbarIcon, // 常に同じテクスチャを返すが、中身が書き換わる
                    this,
                    _ => {
                        _showPanel = !_showPanel;
                        UpdateToolbarIcon(); // ★クリック時に中身を書き換える
                    }
                );

                ToolbarManager.AddLeftToolbarControl(myButton);
            }
            catch (Exception e) { Logger.LogError($"Toolbar Error: {e.Message}"); }
        }

        // --- アイコンの色を直接上書きするメソッド ---
        private void UpdateToolbarIcon()
        {
            if (_toolbarIcon == null) return;

            if (_showPanel)
            {
                // ターゲットの色。RとBを少し混ぜることで、
                // 単純な真緑(0,1,0)よりも目に馴染む、他のプラグインに近い緑になります。
                Color activeGreen = new Color(0.2f, 1.0f, 0.2f, 1.0f);

                Color[] pixels = _toolbarIcon.GetPixels();
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color c = pixels[i];

                    // ★ S字補正を使わず、単純な乗算のみ。
                    // 係数を「1.1f」にすることで、薄すぎず濃すぎない中間の発光感を出します。
                    pixels[i] = new Color(
                        c.r * activeGreen.r,
                        c.g * activeGreen.g * 1.1f, // ここでわずかに強調
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
            // 現在スタジオで選択中のキャラクター（最初の1人）を取得
            var ociChar = KKAPI.Studio.StudioAPI.GetSelectedCharacters().FirstOrDefault();
            if (ociChar == null) return null;

            // キャラクターの本体（transformTarget）からコンポーネントを探す
            // ※ スタジオのOCICharは guideObject.transformTarget に GameObject が紐付いています
            var targetGo = ociChar.guideObject.transformTarget.gameObject;
            var comp = targetGo.GetComponent<VoiceCharComponent>();

            // なければ新しく付与する
            if (comp == null)
            {
                comp = targetGo.AddComponent<VoiceCharComponent>();
                // ここで初期値をセット
                comp.SetMode(_defaultMode);
                comp.SetClip(_defaultClip);
                comp.SetOption(_defaultOption);
                Logger.LogInfo($"Added VoiceCharComponent to: {ociChar.charInfo.name}");
            }
            return comp;
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
            Debug.Log($"[VoicePlayer] ★リフレッシュを実行");
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

            // 4. 再生を実行（charaを渡すことで、Player側で監視できるようにする）
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
                // 1. スタジオ本体の入力をリセット
                Input.ResetInputAxes();

                // 2. カメラ制御の停止
                var cameraCtrl = Studio.Studio.Instance != null ? Studio.Studio.Instance.cameraCtrl : null;
                if (cameraCtrl != null && cameraCtrl.enabled)
                {
                    cameraCtrl.enabled = false;
                    _cameraDisabledByMe = true;
                }
                // 3. ★KKPE方式：EventSystemを「操作中」としてマークする
                // これにより、スタジオのツールバー等の Unity UI (Canvas) が反応しなくなります
                if (UnityEngine.EventSystems.EventSystem.current != null)
                {
                    // SetSelectedGameObject に自分のウィンドウ（あるいはダミー）を指定することで、
                    // ツールバー側が「今は別の場所が操作されている」と判断し、貫通を防ぎます。
                    UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
                }
            }
            else if (_cameraDisabledByMe)
            {
                RestoreState();
            }
            // --- ウィンドウ描画 ---
            Rect oldRect = windowRect; // ドラッグ前の座標を覚えておく

            windowRect = GUI.Window(123456, windowRect, DrawVoicePanel, "🎤 Voice Player Control");

            // ★修正版：座標が変わっていて、かつ前回の保存から0.5秒以上経っている場合だけ保存
            if (windowRect.x != oldRect.x || windowRect.y != oldRect.y)
            {
                // Time.unscaledTime を使うことで、ゲームの一時停止中などでも正確に時間を測れます
                if (Time.unscaledTime - _lastSaveTime > 0.5f)
                {
                    _windowX.Value = windowRect.x;
                    _windowY.Value = windowRect.y;
                    _lastSaveTime = Time.unscaledTime;
                }
            }            // --- 4. ★最強の防衛線：イベントの完全消費 ---
            // 自分のウィンドウ内であれば、クリック（MouseDown）を含め全てのイベントをここで「Use」します。
            // GUI.Window を呼んだ「後」であれば、中のボタンは既に反応済みなので問題ありません。
            if (isMouseInWindow && Event.current.type != EventType.Layout && Event.current.type != EventType.Repaint)
            {
                Event.current.Use();
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

                        // ★ 現在Optionが存在しなければNoneへ
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
                        // ★ 現在Optionが存在しなければNoneへ
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
            {
                // プロパティを反転させるだけで、中の setter が動き 
                // RefreshVoice(chara) もしくは StopVoice(chara) が自動で呼ばれる
                IsPlaying = !IsPlaying;
            }
            // --- 6. 閉じるボタン ---
            GUI.backgroundColor = Color.white;
            currentY += 45f;
            if (GUI.Button(new Rect(x, currentY, w, 25f), "閉じる"))
            {
                _showPanel = false;
                UpdateToolbarIcon(); // ★ここでも呼び出して色を戻す
            }
            windowRect.height = currentY + 30f;
        }
    }
}