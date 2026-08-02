using UnityEngine;
using NoteType = REmind.Gameplay.Chart.Data.NoteType;
public class ChartBuilder : MonoBehaviour
{
    [SerializeField] GameObject LinePrefab;
    [SerializeField] GameObject TapNotePrefab;
    [SerializeField] GameObject HoldNotePrefab;
    [SerializeField] GameObject AirNotePrefab;
    [SerializeField] GameObject FlickNotePrefab;
    [SerializeField] GameObject SpeedNotePrefab;
    [SerializeField] GameObject ActionNotePrefab;

    public bool isPreviewing = false;
    private NoteType currentNoteType = NoteType.Unknown;

    private GameObject previewNote = new GameObject();

    private void Start()
    {
    }

    public void SetPreviewing(bool previewing)
    {
        isPreviewing = previewing;
    }

    private void Update()
    {
        if (isPreviewing)
        {
            
        }
    }

    public void SetCurrentNoteType(NoteType noteType)
    {
        Destroy(previewNote);

        GameObject newNote = null;
        if (noteType == NoteType.Tap) newNote = Instantiate(TapNotePrefab);
        else if (noteType == NoteType.Hold) newNote = Instantiate(HoldNotePrefab);
        else if (noteType == NoteType.Air) newNote = Instantiate(AirNotePrefab);
        else if (noteType == NoteType.Flick) newNote = Instantiate(FlickNotePrefab);
        else if (noteType == NoteType.Speed) newNote = Instantiate(SpeedNotePrefab);
        else if (noteType == NoteType.Action) newNote = Instantiate(ActionNotePrefab);
        else newNote = new GameObject();


        previewNote = newNote;

        currentNoteType = noteType;
    }
}