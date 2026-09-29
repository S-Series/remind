using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace REmind.Gameplay
{
    /// <summary>Player preferences, kept separate from scores and favorites.</summary>
    public sealed class LocalGameSettingsStore
    {
        public enum HitSoundStyle { Default, Soft, Sharp, None }
        public const int LaneCount = 10;
        public const double MaximumJudgementOffsetMs = 200d;

        private static readonly string[] DefaultBindings =
        {
            "<Keyboard>/z", "<Keyboard>/x", "<Keyboard>/c",
            "<Keyboard>/v", "<Keyboard>/m", "<Keyboard>/comma",
            "<Keyboard>/period", "<Keyboard>/slash",
            "<Keyboard>/leftShift", "<Keyboard>/rightShift"
        };

        [Serializable]
        private sealed class SaveDocument
        {
            public int version = 2;
            public float musicVolume = 1f;
            public float masterVolume = 1f;
            public float sfxVolume = 1f;
            public float voiceVolume = 1f;
            public HitSoundStyle hitSoundStyle;
            public bool spatialAudio;
            public bool muteWhenUnfocused;
            public bool reduceBgmDuringVoice = true;
            public bool keepAudioInBackground = true;
            public double judgementOffsetMs;
            public string[] laneBindings = (string[])DefaultBindings.Clone();
        }

        private readonly string path;
        private readonly SaveDocument document;

        private LocalGameSettingsStore(string path, SaveDocument document)
        {
            this.path = path;
            this.document = document;
        }

        public float MusicVolume => document.musicVolume;
        public float MasterVolume => document.masterVolume;
        public float SfxVolume => document.sfxVolume;
        public float VoiceVolume => document.voiceVolume;
        public HitSoundStyle HitSound => document.hitSoundStyle;
        public bool SpatialAudio => document.spatialAudio;
        public bool MuteWhenUnfocused => document.muteWhenUnfocused;
        public bool ReduceBgmDuringVoice => document.reduceBgmDuringVoice;
        public bool KeepAudioInBackground => document.keepAudioInBackground;
        public double JudgementOffsetMs => document.judgementOffsetMs;

        public void SetMasterVolume(float value) =>
            document.masterVolume = ValidateVolume(value);

        public void SetSfxVolume(float value) =>
            document.sfxVolume = ValidateVolume(value);

        public void SetVoiceVolume(float value) =>
            document.voiceVolume = ValidateVolume(value);

        public void SetHitSoundStyle(HitSoundStyle value)
        {
            if (!Enum.IsDefined(typeof(HitSoundStyle), value))
                throw new ArgumentOutOfRangeException(nameof(value));
            document.hitSoundStyle = value;
        }

        public void SetSpatialAudio(bool value) => document.spatialAudio = value;
        public void SetMuteWhenUnfocused(bool value) =>
            document.muteWhenUnfocused = value;
        public void SetReduceBgmDuringVoice(bool value) =>
            document.reduceBgmDuringVoice = value;
        public void SetKeepAudioInBackground(bool value) =>
            document.keepAudioInBackground = value;

        public static LocalGameSettingsStore Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A settings path is required.",
                    nameof(path));
            string fullPath = Path.GetFullPath(path);
            return new LocalGameSettingsStore(fullPath,
                Read(fullPath) ?? Read(fullPath + ".bak") ??
                new SaveDocument());
        }

        public string GetLaneBinding(int lane)
        {
            if (lane < 0 || lane >= LaneCount)
                throw new ArgumentOutOfRangeException(nameof(lane));
            return document.laneBindings[lane];
        }

        public void SetMusicVolume(float value)
        {
            document.musicVolume = ValidateVolume(value);
        }

        private static float ValidateVolume(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) ||
                value < 0f || value > 1f)
                throw new ArgumentOutOfRangeException(nameof(value));
            return value;
        }

        public void SetJudgementOffsetMs(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) ||
                Math.Abs(value) > MaximumJudgementOffsetMs)
                throw new ArgumentOutOfRangeException(nameof(value));
            document.judgementOffsetMs = value;
        }

        public void SetLaneBinding(int lane, string path)
        {
            if (lane < 0 || lane >= LaneCount)
                throw new ArgumentOutOfRangeException(nameof(lane));
            if (!IsValidBinding(path))
                throw new ArgumentException("A valid gameplay key is required.",
                    nameof(path));
            for (int i = 0; i < LaneCount; i++)
                if (i != lane && string.Equals(document.laneBindings[i], path,
                        StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("That key is already assigned to another lane.",
                        nameof(path));
            document.laneBindings[lane] = path;
        }

        public void ResetBindings() =>
            document.laneBindings = (string[])DefaultBindings.Clone();

        public void Save()
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(document, true));
            if (File.Exists(path))
                File.Replace(temporary, path, path + ".bak");
            else
                File.Move(temporary, path);
        }

        public static string DefaultLaneBinding(int lane)
        {
            if (lane < 0 || lane >= LaneCount)
                throw new ArgumentOutOfRangeException(nameof(lane));
            return DefaultBindings[lane];
        }

        private static bool IsValidBinding(string path)
        {
            const string prefix = "<Keyboard>/";
            if (string.IsNullOrWhiteSpace(path) ||
                !path.StartsWith(prefix, StringComparison.Ordinal) ||
                path.Length == prefix.Length ||
                string.Equals(path, prefix + "escape",
                    StringComparison.OrdinalIgnoreCase))
                return false;
            for (int i = prefix.Length; i < path.Length; i++)
                if (!char.IsLetterOrDigit(path[i])) return false;
            return Enum.TryParse(path.Substring(prefix.Length), true,
                       out Key key) && Enum.IsDefined(typeof(Key), key) &&
                   key != Key.None && key != Key.Escape;
        }

        private static SaveDocument Read(string path)
        {
            if (!File.Exists(path)) return null;
            try
            {
                SaveDocument result = JsonUtility.FromJson<SaveDocument>(
                    File.ReadAllText(path));
                if (result == null || (result.version != 1 && result.version != 2) ||
                    float.IsNaN(result.musicVolume) ||
                    float.IsInfinity(result.musicVolume) ||
                    result.musicVolume < 0f || result.musicVolume > 1f ||
                    double.IsNaN(result.judgementOffsetMs) ||
                    double.IsInfinity(result.judgementOffsetMs) ||
                    Math.Abs(result.judgementOffsetMs) >
                        MaximumJudgementOffsetMs ||
                    result.laneBindings == null ||
                    result.laneBindings.Length != LaneCount)
                    throw new FormatException("Unsupported player settings format.");
                if (result.version == 1)
                {
                    result.masterVolume = 1f;
                    result.sfxVolume = 1f;
                    result.voiceVolume = 1f;
                    result.hitSoundStyle = HitSoundStyle.Default;
                    result.spatialAudio = false;
                    result.muteWhenUnfocused = false;
                    result.reduceBgmDuringVoice = true;
                    result.keepAudioInBackground = true;
                    result.version = 2;
                }
                if (!IsValidVolume(result.masterVolume) ||
                    !IsValidVolume(result.sfxVolume) ||
                    !IsValidVolume(result.voiceVolume) ||
                    !Enum.IsDefined(typeof(HitSoundStyle),
                        result.hitSoundStyle))
                    throw new FormatException("Invalid audio preferences.");
                for (int i = 0; i < LaneCount; i++)
                {
                    if (!IsValidBinding(result.laneBindings[i]))
                        throw new FormatException("Invalid lane binding.");
                    for (int j = 0; j < i; j++)
                        if (string.Equals(result.laneBindings[i],
                                result.laneBindings[j],
                                StringComparison.OrdinalIgnoreCase))
                            throw new FormatException("Duplicate lane binding.");
                }
                return result;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Could not read local player settings at " +
                    path + ": " + exception.Message);
                return null;
            }
        }

        private static bool IsValidVolume(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) &&
            value >= 0f && value <= 1f;
    }
}
