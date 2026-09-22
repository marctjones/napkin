using System.Runtime.CompilerServices;

// The container's atomic save has one seam that cannot be exercised from outside: how the
// temporary file is created. A test injects a stream that fails part way through, to watch the
// previous file survive a save that dies mid-write — which is the whole promise of writing to a
// temporary and moving it (#6 stage 2).
[assembly: InternalsVisibleTo("Napkin.Core.Project.Tests")]
