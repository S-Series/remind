using System;
using System.Collections.Generic;
using UnityEngine;

namespace REmind.Gameplay
{
    /// <summary>Game-owned challenge definitions; player progress remains outside the asset.</summary>
    [CreateAssetMenu(fileName = "MusicUnlockLibrary",
        menuName = "REmind/Gameplay/Music Unlock Library")]
    public sealed class MusicUnlockLibrary : ScriptableObject
    {
        [SerializeField] private SongUnlockChallengeDefinition[] entries =
            Array.Empty<SongUnlockChallengeDefinition>();

        public IReadOnlyList<SongUnlockChallengeDefinition> Entries =>
            Array.AsReadOnly(entries ?? Array.Empty<SongUnlockChallengeDefinition>());

        public void ValidateForUse()
        {
            if (entries == null)
                throw new InvalidOperationException(
                    "Music unlock library has a missing definition list.");
            var definitions = new HashSet<SongUnlockChallengeDefinition>();
            foreach (SongUnlockChallengeDefinition entry in entries)
            {
                if (!entry)
                    throw new InvalidOperationException(
                        "Music unlock library contains a missing definition.");
                if (!definitions.Add(entry))
                    throw new InvalidOperationException(
                        "Music unlock library contains a duplicate definition.");
                entry.ValidateForUse();
            }
        }

        public bool TrySelectChallenge(string musicId, string difficultyId,
            Func<string, string, bool> isDifficultyUnlocked,
            out SongUnlockChallengeDefinition selected, out string error)
        {
            ValidateForUse();
            if (isDifficultyUnlocked == null)
                throw new ArgumentNullException(nameof(isDifficultyUnlocked));
            selected = null;
            error = null;
            foreach (SongUnlockChallengeDefinition entry in entries)
            {
                if (string.Equals(musicId, entry.TargetMusicId,
                        StringComparison.Ordinal) &&
                    !isDifficultyUnlocked(musicId, difficultyId))
                {
                    selected = null;
                    error = "아직 해금되지 않은 난이도입니다.";
                    return false;
                }
                if (!entry.AppliesTo(musicId, difficultyId,
                    isDifficultyUnlocked(entry.TargetMusicId, difficultyId)))
                    continue;
                if (selected)
                {
                    selected = null;
                    error = "여러 해금 도전이 동시에 적용되어 시작할 수 없습니다. 도전 조건을 확인해주세요.";
                    return false;
                }
                selected = entry;
            }
            return true;
        }
    }
}
