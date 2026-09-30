using System.Diagnostics;
using System.Globalization;
using REmind.Charting;

// Standalone, deterministic ChartCore microbenchmark. It does not run Unity rendering,
// audio, the GameRule adapter, or the product build.
var cases = new[]
{
    new Case("normal-500", 500, 480, false, false, false),
    new Case("dense-2000", 2000, 120, false, false, false),
    new Case("hold-200", 200, 4800, true, false, false),
    new Case("long-scratch-200", 200, 4800, true, true, false),
    new Case("dense-auto-2000", 2000, 120, false, false, true)
};
if (args.Length != 1 || !Directory.Exists(args[0]))
    throw new ArgumentException("Pass an existing output directory.");
string outputDirectory = args[0];
var summary = new List<string>
{
    "case,notes,events,resolutions,next_calls,input_calls,auto_calls,full_scan_entries,input_scan_upper_bound,judge_calls,miss_window_calls,median_ms,min_ms,allocated_bytes,trace"
};
var trials = new List<string> { "case,trial,elapsed_ms,allocated_bytes" };

Console.WriteLine($"runtime={Environment.Version} os={Environment.OSVersion} processors={Environment.ProcessorCount}");
Console.WriteLine(summary[0]);
foreach (Case item in cases)
{
    PlayableChartSnapshot chart = BuildChart(item);
    Event[] events = BuildEvents(chart, item.AutoPlay);
    File.WriteAllLines(Path.Combine(outputDirectory, item.Name + "-inputs.csv"),
        new[] { "sequence,time_ms,lane,pressed" }.Concat(events.Select(
            (e, index) => string.Join(",", index,
                e.TimeMs.ToString("R", CultureInfo.InvariantCulture),
                e.Lane, e.Pressed ? "true" : "false"))));
    ulong trace = 0;
    int resolutions = 0;
    int judgeCalls = 0;
    int missWindowCalls = 0;
    List<string> captured = null;
    var session = new PlayableJudgementSession(chart,
        (_, point, offset) =>
        {
            judgeCalls++;
            return Math.Abs(offset) <= (point > 0 ? 100d : 50d)
                ? ChartJudgementGrade.Perfect : ChartJudgementGrade.None;
        },
        (_, _) =>
        {
            missWindowCalls++;
            return 100d;
        },
        result =>
        {
            resolutions++;
            trace = Mix(trace, result);
            captured?.Add(string.Join(",",
                result.Note.Id,
                result.Note.Kind.ToString(),
                result.Note.Lane.ToString(CultureInfo.InvariantCulture),
                result.SegmentIndex.ToString(CultureInfo.InvariantCulture),
                result.Grade.ToString(),
                result.OffsetMs.ToString("R", CultureInfo.InvariantCulture),
                result.TargetTimeMs.ToString("R", CultureInfo.InvariantCulture),
                result.EvaluationTimeMs.ToString("R", CultureInfo.InvariantCulture),
                result.IsAutomaticMiss ? "true" : "false"));
        });
    captured = new List<string> { "note_id,note_kind,lane,segment_index,grade,offset_ms,target_time_ms,evaluation_time_ms,automatic_miss" };
    trace = 14695981039346656037UL;
    Replay(session, events, item.AutoPlay);
    File.WriteAllLines(Path.Combine(outputDirectory, item.Name + "-trace.csv"), captured);
    ulong capturedTrace = trace;
    int capturedResolutions = resolutions;
    captured = null;
    session.Reset();
    for (int i = 0; i < 2; i++)
    {
        session.Reset();
        Replay(session, events, item.AutoPlay);
    }

    var elapsed = new double[9];
    var allocations = new long[9];
    Counts counts = default;
    ulong expectedTrace = 0;
    int expectedResolutions = 0;
    int expectedJudgeCalls = 0;
    int expectedMissCalls = 0;
    for (int i = 0; i < elapsed.Length; i++)
    {
        session.Reset();
        trace = 14695981039346656037UL;
        resolutions = 0;
        judgeCalls = 0;
        missWindowCalls = 0;
        long before = GC.GetAllocatedBytesForCurrentThread();
        long start = Stopwatch.GetTimestamp();
        counts = Replay(session, events, item.AutoPlay);
        elapsed[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        allocations[i] = GC.GetAllocatedBytesForCurrentThread() - before;
        trials.Add(string.Join(",", item.Name, i + 1,
            elapsed[i].ToString("F3", CultureInfo.InvariantCulture),
            allocations[i]));
        if (session.PendingNoteCount != 0)
            throw new InvalidOperationException($"Unresolved notes in {item.Name}");
        if (trace != capturedTrace || resolutions != capturedResolutions)
            throw new InvalidOperationException($"Trace changed after capture in {item.Name}");
        if (i > 0 && (trace != expectedTrace || resolutions != expectedResolutions))
            throw new InvalidOperationException($"Nondeterministic result in {item.Name}");
        if (i > 0 && (judgeCalls != expectedJudgeCalls ||
                      missWindowCalls != expectedMissCalls))
            throw new InvalidOperationException($"Nondeterministic calls in {item.Name}");
        expectedTrace = trace;
        expectedResolutions = resolutions;
        expectedJudgeCalls = judgeCalls;
        expectedMissCalls = missWindowCalls;
    }
    Array.Sort(elapsed);
    Array.Sort(allocations);
    long fullScanEntries = (long)(counts.Next + counts.Automatic) *
        chart.Notes.Count;
    long inputUpperBound = (long)counts.Input *
        chart.Notes.Count;
    string row = string.Join(",", item.Name, chart.Notes.Count, events.Length,
        resolutions, counts.Next, counts.Input, counts.Automatic,
        fullScanEntries, inputUpperBound, judgeCalls, missWindowCalls,
        elapsed[4].ToString("F3", CultureInfo.InvariantCulture),
        elapsed[0].ToString("F3", CultureInfo.InvariantCulture),
        allocations[4], trace.ToString("X16", CultureInfo.InvariantCulture));
    summary.Add(row);
    Console.WriteLine(row);
}
File.WriteAllLines(Path.Combine(outputDirectory, "summary.csv"), summary);
File.WriteAllLines(Path.Combine(outputDirectory, "trials.csv"), trials);

static PlayableChartSnapshot BuildChart(Case item)
{
    var document = new ChartDocument(4800, 4, 120d);
    for (int i = 0; i < item.NoteCount; i++)
    {
        int start = i * item.PositionStep;
        int lane = item.Scratch ? (int)ChartLane.GroundLeft + i % 2
            : i % ChartLaneLayout.LaneCount;
        ChartDocumentNote note;
        if (item.Long)
        {
            note = new ChartDocumentNote($"n{i}", item.Scratch
                ? ChartNoteKind.LongScratch : ChartNoteKind.Hold,
                lane, start, start + 2400);
            note.Points.Insert(1, new ChartNotePoint(start + 1200,
                ChartNotePointKind.Mid));
        }
        else
        {
            note = new ChartDocumentNote($"n{i}", ChartNoteKind.Tap,
                lane, start);
        }
        document.Notes.Add(note);
    }
    ChartCompileResult compiled = ChartCompiler.Compile(document);
    if (!compiled.Succeeded)
        throw new InvalidOperationException(string.Join("; ",
            compiled.Issues.Select(issue => issue.Code + ": " + issue.Message)));
    return compiled.Snapshot;
}

static Event[] BuildEvents(PlayableChartSnapshot chart, bool autoPlay)
{
    if (autoPlay) return Array.Empty<Event>();
    var events = new List<Event>();
    int order = 0;
    foreach (PlayableNoteSnapshot note in chart.Notes)
    {
        if (order++ % 4 == 0) continue; // Fixed 25% misses.
        events.Add(new Event(note.StartTimeMs, note.Lane, true));
        if (note.Points.Count > 1)
            events.Add(new Event(note.Points[^1].TimeMs, note.Lane, false));
    }
    return events.OrderBy(e => e.TimeMs).ToArray();
}

static Counts Replay(PlayableJudgementSession session, Event[] events,
    bool autoPlay)
{
    int nextCount = 0, inputCount = 0, autoCount = 0;
    int eventIndex = 0;
    double end = events.Length > 0 ? events[^1].TimeMs + 250d : 0d;
    // The auto-only case gets its duration from the session's last deadline.
    if (autoPlay) end = double.PositiveInfinity;
    for (int frame = 0; frame < 100000; frame++)
    {
        if (frame == 99999)
            throw new InvalidOperationException("Replay exceeded the frame limit.");
        double frameTime = frame * (1000d / 60d);
        double next;
        nextCount++;
        next = session.NextAutomaticTime(autoPlay);
        if (autoPlay && double.IsPositiveInfinity(next)) break;
        if (!autoPlay && eventIndex == events.Length &&
            double.IsPositiveInfinity(next) && frameTime > end) break;

        while (eventIndex < events.Length && events[eventIndex].TimeMs <= frameTime)
        {
            Event current = events[eventIndex++];
            while (next < current.TimeMs)
            {
                double processedTime = next;
                session.ProcessAutomatic(next, autoPlay);
                autoCount++;
                nextCount++;
                next = session.NextAutomaticTime(autoPlay);
                if (next <= processedTime)
                    throw new InvalidOperationException(
                        "Automatic deadline did not advance during input replay.");
            }
            session.Input(current.Lane, current.TimeMs, current.Pressed);
            inputCount++;
            nextCount++;
            next = session.NextAutomaticTime(autoPlay);
        }
        while (next <= frameTime)
        {
            double processedTime = next;
            session.ProcessAutomatic(next, autoPlay);
            autoCount++;
            nextCount++;
            next = session.NextAutomaticTime(autoPlay);
            if (next <= processedTime)
                throw new InvalidOperationException(
                    "Automatic deadline did not advance during frame replay.");
        }
    }
    return new Counts(nextCount, inputCount, autoCount);
}

static ulong Mix(ulong hash, ChartJudgementResolution result)
{
    unchecked
    {
        foreach (char c in result.Note.Id)
            hash = (hash ^ c) * 1099511628211UL;
        hash = (hash ^ (uint)result.Note.Kind) * 1099511628211UL;
        hash = (hash ^ (uint)result.Note.Lane) * 1099511628211UL;
        hash = (hash ^ (uint)result.SegmentIndex) * 1099511628211UL;
        hash = (hash ^ (uint)result.Grade) * 1099511628211UL;
        hash = (hash ^ (ulong)BitConverter.DoubleToInt64Bits(
            result.OffsetMs)) * 1099511628211UL;
        hash = (hash ^ (ulong)BitConverter.DoubleToInt64Bits(
            result.TargetTimeMs)) * 1099511628211UL;
        hash = (hash ^ (ulong)BitConverter.DoubleToInt64Bits(
            result.EvaluationTimeMs)) * 1099511628211UL;
        return (hash ^ (result.IsAutomaticMiss ? 1UL : 0UL)) *
            1099511628211UL;
    }
}

readonly record struct Case(string Name, int NoteCount, int PositionStep,
    bool Long, bool Scratch, bool AutoPlay);
readonly record struct Event(double TimeMs, int Lane, bool Pressed);
readonly record struct Counts(int Next, int Input, int Automatic);
