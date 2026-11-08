# ADR-0007: Newtonsoft JSON with DTOs and migrations

**Context.** Saves must be local, human-inspectable, versioned and robust to bad files. Unity's
`JsonUtility` needs UnityEngine (forbidden in Infrastructure) and has no dictionary or
nullable support. `System.Text.Json` is not part of Unity's default profile.

**Decision.** Newtonsoft.Json, through Unity's official `com.unity.nuget.newtonsoft-json`
package. A separate DTO layer, with an explicit hand-written mapper. A file envelope carrying
a `format` marker and `schemaVersion`. A chain of `ISaveMigration` steps working on the JSON
tree. `TypeNameHandling.None`.

**Consequences.** One third-party dependency, official and widely used. The domain stays free
of serialization attributes. A save-format change costs one migration class. IL2CPP builds
will need a `link.xml` so reflection-based deserialization keeps the DTO members.

**Alternatives.** A hand-written JSON reader: no dependency, but more code to get wrong. A
binary format: smaller and faster, but opaque and harder to migrate.
