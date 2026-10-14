using System.Runtime.CompilerServices;
using UnityEngine.Scripting;

// Keeps the assembly alive through IL2CPP managed stripping.
[assembly: AlwaysLinkAssembly]

// Allows the (closed-source, paid) Editor tooling assembly to reach internal
// factories / builders / backing fields without widening the public surface.
[assembly: InternalsVisibleTo("AceLand.Collection.Editor")]

// Allows the EditMode test assembly to exercise internal members.
[assembly: InternalsVisibleTo("AceLand.Collection.Tests")]

// Allows the DOTS bridge assembly (same package, second runtime assembly) to
// read the serialized snapshot of managed containers without widening the
// public surface. Used by the ToNative(...) conversion extensions.
[assembly: InternalsVisibleTo("AceLand.Collection.Dots")]
