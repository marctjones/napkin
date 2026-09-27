// napkin's two proposal schemas, compiled into grammar constraints at the end of every load so
// the first proposal does not pay the compile (mlx-runtime.md §2, the pre-warm).
//
// Written for slice A from llm-assistant.md §4.4 (Sketch from words) and §4.5 (Edit in words):
// every field required, no other members — the strict parser refuses unknown members anyway
// (§4.3). Slices E (#233) and F (#234) own these schemas; when they land, the .NET side must send
// these exact bytes as ModelRequest.Schema — or change them here in the same commit — or the
// pre-warm misses and the first proposal per load pays the compile. The ABI's load takes no
// schema, by design (header v1); any other schema text still works, compiled on first use.
//
// Its own target so the spike harness sends the same bytes without linking the bridge.

public enum NapkinSchemas {
    /// §4.4: rough planks. Lengths are feet-inch text; napkin snaps them.
    public static let sketch = #"""
    {"type":"object","properties":{"parts":{"type":"array","items":{"type":"object","properties":{"name":{"type":"string"},"width":{"type":"string"},"height":{"type":"string"},"depth":{"type":"string"},"x":{"type":"string"},"y":{"type":"string"},"quantity":{"type":"integer"}},"required":["name","width","height","depth","x","y","quantity"],"additionalProperties":false}},"note":{"type":"string"}},"required":["parts","note"],"additionalProperties":false}
    """#

    /// §4.5: the closed set of six edits, each naming a part as the context pack names it.
    public static let edit = #"""
    {"type":"object","properties":{"edits":{"type":"array","items":{"anyOf":[{"type":"object","properties":{"edit":{"const":"resize"},"part":{"type":"string"},"dimension":{"enum":["width","height","depth"]},"length":{"type":"string"}},"required":["edit","part","dimension","length"],"additionalProperties":false},{"type":"object","properties":{"edit":{"const":"move"},"part":{"type":"string"},"x":{"type":"string"},"y":{"type":"string"}},"required":["edit","part","x","y"],"additionalProperties":false},{"type":"object","properties":{"edit":{"const":"rename"},"part":{"type":"string"},"name":{"type":"string"}},"required":["edit","part","name"],"additionalProperties":false},{"type":"object","properties":{"edit":{"const":"stock"},"part":{"type":"string"},"stock":{"type":"string"}},"required":["edit","part","stock"],"additionalProperties":false},{"type":"object","properties":{"edit":{"const":"quantity"},"part":{"type":"string"},"quantity":{"type":"integer"}},"required":["edit","part","quantity"],"additionalProperties":false},{"type":"object","properties":{"edit":{"const":"remove"},"part":{"type":"string"}},"required":["edit","part"],"additionalProperties":false}]}}},"required":["edits"],"additionalProperties":false}
    """#

    /// Both, in the order load pre-warms them, with the names the diagnostics print.
    public static let all: [(name: String, text: String)] = [("sketch", sketch), ("edit", edit)]
}
