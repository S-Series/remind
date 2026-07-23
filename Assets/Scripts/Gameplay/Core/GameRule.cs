using UnityEngine;

public abstract class GameRule : MonoBehaviour
{
    public abstract int[] NoteJudgeTimings { get; set; }
}
