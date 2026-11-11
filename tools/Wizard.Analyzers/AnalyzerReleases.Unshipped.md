; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
WZ0001 | Determinism | Error | Floating-point type in integer-only assembly
WZ0002 | Determinism | Error | System.Random in deterministic core
WZ0003 | Determinism | Error | Wall clock in deterministic or clockless code
WZ0004 | Determinism | Error | Guid.NewGuid in deterministic core
WZ0005 | Determinism | Error | Iteration over hash-ordered collection
WZ0006 | Determinism | Error | Unstable or unspecified sort in deterministic core
WZ0007 | Determinism | Error | Culture-dependent string operation in deterministic core
WZ0008 | Determinism | Error | Threading in deterministic core
WZ0009 | Determinism | Error | GetHashCode in deterministic core logic
WZ0010 | Determinism | Error | Mutable static state in deterministic core
WZ0011 | Determinism | Error | ThreadStatic in deterministic core
WZ0012 | Determinism | Error | Unsafe code in deterministic core
WZ0013 | Determinism | Error | Memory reinterpretation in deterministic core
WZ0014 | Determinism | Error | Raw division by a non-constant
WZ0015 | Determinism | Error | checked arithmetic in deterministic core
WZ0016 | Determinism | Error | Diagnostic suppression in deterministic core
WZ0017 | Layering | Error | Reference to code outside the core version hash
WZ0019 | Layering | Error | Socket in netcode core
WZ0020 | Payments | Error | Paid currency value in enhance, match or league code
WZ0021 | Language | Error | C# feature not available to Unity
WZ0022 | Language | Error | API not available in shared packages
WZ0023 | Layering | Error | Forbidden assembly reference
