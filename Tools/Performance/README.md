# Chart judgement baseline

Run from the repository root:

```powershell
./Tools/Performance/Run-ChartJudgementBenchmark.ps1
```

Pass `-RunName some-unique-name` to name the run. The script creates an isolated
project and results under ignored `Logs/Performance-<name>/`. It compiles the
current pure `ChartCoreDomain` sources directly; no generated Unity project or
gameplay source is changed. The script fixes tiered JIT compilation to off for
the process. Use `-PrepareOnly` to record the environment and generated project
without compiling or measuring while Unity/build work is active. Use the same
machine, SDK, script, and JIT setting for comparisons.

The cases compile deterministic synthetic charts, then replay 60 Hz frames.
Every fourth note receives no input. Other notes receive a press at Start; Long
notes also receive a release at End. Auto Play receives no input. All cases use
a fixed window delegate and no GameRule or Effect adapter. Chart compilation,
session creation, and trace writing are outside the timed region. Each session
is warmed up twice and replayed nine times. `summary.csv` gives the median and
minimum whole-replay time and median current-thread allocations. `trials.csv`
keeps every timing and allocation sample. Replay aborts if automatic deadlines
stop advancing or the frame limit is reached.

`full_scan_entries` is exact: `NextAutomaticTime` and `ProcessAutomatic` each
walk every session entry once per call. `input_scan_upper_bound` is input calls
times note count; `Input` can return early. Delegate call counts are measured.
Each `*-inputs.csv` stores input order and timestamps; each `*-trace.csv` stores
resolution order, note identity/kind/lane, segment, grade, offsets, evaluation
time, and automatic-Miss flag. Compare complete traces after an optimization:

```powershell
./Tools/Performance/Compare-ChartJudgementTraces.ps1 `
  -BaselineDirectory Logs/Performance-baseline `
  -CandidateDirectory Logs/Performance-candidate
```

The comparison checks every input and trace file byte for byte; the hash in
`summary.csv` is a quick summary only. `environment.json` records SHA-256 for
every compiled ChartCore source and the harness, processor name, SDK, and
Unity/.NET/MSBuild process counts at both ends. A source change during a run
invalidates its timing result. Process interference marks timing provisional.

The initial pre-ability run is in `Logs/Performance-initial-20260930-v4/`:

| Case | Whole replay median | Full-scan entries | Miss-window calls | Measured allocations |
| --- | ---: | ---: | ---: | ---: |
| 500 regular notes | 10.436 ms | 3,315,000 | 1,654,625 | 0 B |
| 2,000 dense notes | 61.774 ms | 17,028,000 | 8,501,500 | 0 B |
| 200 Hold notes | 30.990 ms | 4,951,400 | 6,600 | 0 B |
| 200 Long Scratch notes | 31.082 ms | 4,951,400 | 6,600 | 0 B |
| 2,000 dense Auto Play notes | 64.490 ms | 19,998,000 | 0 | 0 B |

That run used .NET 10.0.10 on Windows with 16 logical processors, no Unity
process, and 20 other `dotnet` processes. CPU times are therefore provisional.
The earlier harness also differs from the current harness by its bounded-replay
guards; its input and resolution traces still match the current runs.

Two later controlled runs are in `Logs/Performance-controlled-20260930-a/` and
`Logs/Performance-controlled-20260930-b/`. They used the same ChartCore source
manifest and harness hash, .NET 10.0.10, and the AMD Ryzen 7 9800X3D with 16
logical processors. Both environment logs recorded zero Unity, `dotnet`, and
MSBuild processes before and after measurement and no source change during a
run. Each cell below is the median of nine whole-replay trials in that run:

| Case | Run A | Run B | Timed allocation |
| --- | ---: | ---: | ---: |
| 500 regular notes | 15.031 ms | 11.994 ms | 0 B |
| 2,000 dense notes | 68.915 ms | 66.098 ms | 0 B |
| 200 Hold notes | 34.070 ms | 33.316 ms | 0 B |
| 200 Long Scratch notes | 33.686 ms | 31.541 ms | 0 B |
| 2,000 dense Auto Play notes | 78.230 ms | 72.651 ms | 0 B |

All ten input/trace files match byte for byte between the old baseline and run
A, and again between runs A and B. CPU time differences between the old and
new runs are environment variation, not an optimization result.

The zero allocation result applies only to the timed pure-core replay. Whole
replay times are not per-frame times or Unity Player performance. D2 character
flow integration must finish before refreshing this baseline and changing the
judgement search. Target frame and memory budgets
still require the D1 device/environment decision. Use Unity Profiler in the
target build for rendering, audio, frame time, and GC measurements.

For E2b, preserve the original entry-order tie break for candidate selection
and automatic results. `NextAutomaticTime` evaluates `missWindow` on each call,
so a cached deadline needs a defined invalidation point when a rule changes.
Keep same-time Effect, input, and automatic ordering in `NoteJudgementSystem`.
Refresh the baseline after the character ability path is integrated, then
compare full traces plus Game score/health/combo/ability results in E2c.
