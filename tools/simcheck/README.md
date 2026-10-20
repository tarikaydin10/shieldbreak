# simcheck: Sim-Tests ohne Unity

Kompiliert `Sim/`, die Kampf- und Formations-Definitionen aus `Data/` und die EditMode-Tests, die keine Szene und kein Projekt-Asset brauchen, mit normalem .NET 8. Statt Unity stehen Stubs für `Unity.Collections`, `Unity.Jobs`, `Unity.Burst` und den kleinen Teil von `UnityEngine`, den die Definitionen nutzen (ScriptableObjects sind hier normale Objekte). Gedacht für Umgebungen ohne Unity (Cloud-Sessions von Claude Code, Linux, CI). **Ersetzt `.\sb.ps1 test` nicht:** Burst, Szenen, Assets, Eingaben, Performance und alles in `Presentation/` laufen nur in Unity.

| Was | Befehl |
|---|---|
| Tests laufen lassen | `tools/simcheck/run.sh` (braucht `dotnet` 8; auf Ubuntu: `apt-get install dotnet-sdk-8.0`) |
| nur eine Klasse | `tools/simcheck/run.sh --filter FormationTests` |

Dabei: Sim-Kern, Determinismus, Grid, Agenten, Formationen, Verhalten, Wege, alle Kampf-Tests (Duell, Verben, Zweihänder, Angriffe, Kampf in Formation), Moral, Sandbox-Szenario. Übersprungen wird nur ein Test, der ein echtes Projekt-Asset lädt.

| Datei | Inhalt |
|---|---|
| `UnityStubs.cs` | `NativeArray`, `NativeList`, Jobs (laufen sequenziell), Burst-Attribute (ohne Wirkung); nur, was `Sim/` heute nutzt |
| `UnityEngineStubs.cs` | `ScriptableObject` (`CreateInstance` = `new`), `Vector2`/`Vector3` (mit Umwandlung nach `float2`/`float3`), `Mathf` (gleiche Formeln), Inspector-Attribute; `AssetDatabase` meldet Tests mit Projekt-Assets als übersprungen |
| `SimCheck.csproj` | bindet `Sim/**`, die Definitionen und die Testdateien ein; `Unity.Mathematics` kommt als NuGet-Paket `UnityMathematics` |

Neue Testdatei ohne Szene oder Projekt-Asset: in `SimCheck.csproj` eintragen. Braucht der Code eine neue Unity-API, den passenden Stub ergänzen.
