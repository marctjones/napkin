using System.Runtime.CompilerServices;

// The reader's JSON field bookkeeping (duplicate and unknown fields) and the name normaliser are
// implementation details with no public surface, but they are exactly what the strictness tests
// need to exercise one case at a time.
[assembly: InternalsVisibleTo("Napkin.Core.Materials.Tests")]
