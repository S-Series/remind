using REmind.Charting;
using UnityEngine;

namespace REmind.Gameplay.Input.Judgement
{
    [CreateAssetMenu(fileName = "Note Judge Windows",
        menuName = "REmind/Gameplay/Note Judge Windows")]
    public sealed class NoteJudgeWindowProfile : ScriptableObject
    {
        [SerializeField] private ChartNoteKind noteKind;
        [SerializeField] private JudgeWindows windows =
            new JudgeWindows(50, 50, 100, 100);
        [SerializeField, Min(0)] private int longScratchMidPerfectMs = 100;
        [SerializeField, Min(0)] private int longScratchEndPerfectMs = 75;

        public ChartNoteKind NoteKind => noteKind;
        public JudgeWindows Windows => windows;
        public int LongScratchMidPerfectMs => longScratchMidPerfectMs;
        public int LongScratchEndPerfectMs => longScratchEndPerfectMs;
    }
}
