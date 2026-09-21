using System.Runtime.CompilerServices;

// The Propagator (docs/design/geometry-model.md §4.4) is internal on purpose: it is the engine
// inside the direct updater and, later, the repair pass of the solver (#28). Its tests are part
// of #5's test plan (§8 step 8), so the test assembly sees internals.
[assembly: InternalsVisibleTo("Napkin.Core.Geometry.Tests")]
