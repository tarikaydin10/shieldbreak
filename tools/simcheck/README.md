# simcheck: Sim-Tests ohne Unity

Kompiliert `Sim/` und die Unity-freien EditMode-Tests mit normalem .NET 8 gegen Stubs für `Unity.Collections`, `Unity.Jobs` und `Unity.Burst`. Gedacht für Umgebungen ohne Unity (Cloud-Sessions von Claude Code, Linux, CI). **Ersetzt `.\sb.ps1 test` nicht:** Burst, die Kampf-Tests mit ScriptableObjects und alles außerhalb von `Sim/` laufen nur in Unity.

| Was | Befehl |
|---|---|
| Tests laufen lassen | `tools/simcheck/run.sh` (braucht `dotnet` 8) |
| nur eine Klasse | `tools/simcheck/run.sh --filter FormationTests` |

| Datei | Inhalt |
|---|---|
| `UnityStubs.cs` | `NativeArray`, `NativeList`, Jobs (laufen sequenziell), Burst-Attribute (ohne Wirkung); nur, was `Sim/` heute nutzt |
| `SimCheck.csproj` | bindet `Sim/**` und die Testdateien ohne UnityEngine ein; `Unity.Mathematics` kommt als NuGet-Paket `UnityMathematics` |

Neue Testdatei ohne UnityEngine-Bezug: in `SimCheck.csproj` eintragen. Braucht `Sim/` eine neue Collections-API, den Stub ergänzen.
