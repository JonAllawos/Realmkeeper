# Running the tests

## Without Unity (works today)

Requirements: the .NET 8 SDK. NuGet access is needed on the first run (NUnit, Newtonsoft.Json).

```bash
Tools/DotNet/verify.sh
```

Run a subset:

```bash
Tools/DotNet/verify.sh --filter "FullyQualifiedName~Persistence"
```

What this does:

1. Builds `Realmkeeper.Domain`, `.Simulation`, `.Application` and `.Infrastructure` as
   C# 9 / netstandard2.1, warnings treated as errors, with the same references as the `.asmdef` files.
2. Compiles `Assets/Realmkeeper/Tests/EditMode/**/*.cs` into a net8.0 NUnit 3.14 test project.
3. Writes a TRX report to `Tools/DotNet/TestResults/editmode-results.trx`.

## In Unity (once the project exists)

EditMode: **Window → General → Test Runner → EditMode → Run All**.

Command line (CI):

```bash
Unity -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults Logs/editmode.xml -logFile Logs/editmode.log
```

```bash
Unity -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults Logs/playmode.xml -logFile Logs/playmode.log
```

The same EditMode sources run in both places. They use only NUnit APIs that Unity's bundled
NUnit (3.5) provides: no `Assert.Multiple`, no NUnit 4 `ClassicAssert`.
