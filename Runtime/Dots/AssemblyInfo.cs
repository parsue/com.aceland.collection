using System.Runtime.CompilerServices;
using UnityEngine.Scripting;

// Keeps the assembly alive through IL2CPP managed stripping.
[assembly: AlwaysLinkAssembly]

// Allows the EditMode / PlayMode test assembly to exercise internal members.
[assembly: InternalsVisibleTo("AceLand.Collection.Dots.Tests")]
