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
            new JudgeWindows(30, 60, 100, 150);

        public ChartNoteKind NoteKind => noteKind;
        public JudgeWindows Windows => windows;
    }
}
