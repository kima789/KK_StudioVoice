using Studio;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static ADV.Info;

namespace KK_StudioVoice
{
    public class VoiceCharComponent : MonoBehaviour
    {
        public System.Action ReservedNextAction;

        public bool IsVoicePlaying = false;
        public bool HasExternalRequest = false;

        public string LastPlayedVoiceId;
        public string LastPlayedBreathId;
        
        public string LastMode;
        public string LastClip;
        public string LastOption;
        public int LastExp;
        public bool LastExcited;

        public bool LastVisible;

        public int LastPersonality;

        private string _selectedMode = "";
        private string _selectedClip = "";
        private string _selectedOption = "None";
        private string _selectedExp = "First Time";
        private bool _isExcited = false;
        private bool _isLipSyncOn = true;
        private bool _isVoiceOn = true;
        private bool _isBreathOn = true;
        private bool _isPlaying = false;

        // プロパティは大文字開始
        public string SelectedMode => _selectedMode;
        public string SelectedClip => _selectedClip;
        public string SelectedOption => _selectedOption;
        public string SelectedExp => _selectedExp;
        public bool IsExcited => _isExcited;
        public bool IsLipSyncOn => _isLipSyncOn;
        public bool IsVoiceOn => _isVoiceOn;
        public bool IsBreathOn => _isBreathOn;
        public bool IsPlaying => _isPlaying;
        public bool SetMode(string value) => SetProperty(ref _selectedMode, value);
        public bool SetClip(string value) => SetProperty(ref _selectedClip, value);
        public bool SetOption(string value) => SetProperty(ref _selectedOption, value);
        public bool SetExp(string value) => SetProperty(ref _selectedExp, value);
        public bool SetExcited(bool value) => SetProperty(ref _isExcited, value);
        public bool SetLipSyncOn(bool value) => SetProperty(ref _isLipSyncOn, value);
        public bool SetVoiceOn(bool value) => SetProperty(ref _isVoiceOn, value);
        public bool SetBreathOn(bool value) => SetProperty(ref _isBreathOn, value);
        public bool SetPlaying(bool value) => SetProperty(ref _isPlaying, value);
        public bool IsCurrentNotLoop { get; set; } = false;

        // ---  汎用ヘルパーメソッド ---
        // 値を比較し、変化があれば更新して true を返す

        private bool SetProperty<T>(ref T storage, T value)
        {
            if (Equals(storage, value)) return false;
            storage = value;
            return true;
        }
        private void Awake()
        {
            IsVoicePlaying = false;
            IsCurrentNotLoop = false;
            ReservedNextAction = null;
        }
    }
}