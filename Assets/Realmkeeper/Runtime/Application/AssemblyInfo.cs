using System.Runtime.CompilerServices;

// Tests may read a copy of the committed world (WorldSession.CurrentWorldSnapshotForTests) to assert
// exact state; presentation never can, it only gets query snapshots.
[assembly: InternalsVisibleTo("Realmkeeper.Tests.EditMode")]
