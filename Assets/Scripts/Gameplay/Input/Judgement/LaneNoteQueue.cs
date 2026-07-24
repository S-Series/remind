using System;
using System.Collections.Generic;
using REmind.Gameplay.Chart.Data;

namespace REmind.Gameplay.Input.Judgement
{
    public sealed class LaneNoteQueue
    {
        private readonly List<NoteData> notes = new List<NoteData>();
        private int currentIndex;

        public int Lane { get; }
        public int TotalCount => notes.Count;
        public int ProcessedCount => currentIndex;
        public int PendingCount => notes.Count - currentIndex;
        public bool HasPendingNote => currentIndex < notes.Count;

        public LaneNoteQueue(int lane)
        {
            if (lane < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(lane));
            }

            Lane = lane;
        }

        public bool TryPeek(out NoteData note)
        {
            if (!HasPendingNote)
            {
                note = null;
                return false;
            }

            note = notes[currentIndex];
            return true;
        }

        public void ResetProgress()
        {
            currentIndex = 0;
        }

        internal void Add(NoteData note)
        {
            if (note == null)
            {
                throw new ArgumentNullException(nameof(note));
            }

            if (note.Lane != Lane)
            {
                throw new ArgumentException(
                    $"Note lane {note.Lane} does not match queue lane {Lane}.",
                    nameof(note));
            }

            notes.Add(note);
        }

        internal void Sort()
        {
            notes.Sort(CompareNotes);
        }

        internal bool TryAdvance(out NoteData note)
        {
            if (!TryPeek(out note))
            {
                return false;
            }

            currentIndex++;
            return true;
        }

        private static int CompareNotes(NoteData left, NoteData right)
        {
            int timeComparison = left.TimeMs.CompareTo(right.TimeMs);
            return timeComparison != 0
                ? timeComparison
                : string.CompareOrdinal(left.Id, right.Id);
        }
    }
}
