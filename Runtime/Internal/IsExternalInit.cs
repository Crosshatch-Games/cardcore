// Polyfill for `init`-only setters on .NET runtimes that don't ship this type.
// Modern .NET (5+) provides this in the BCL. Some Unity .NET profiles do not.
// The compiler binds to whichever type is visible first.

using System.ComponentModel;

namespace System.Runtime.CompilerServices;

[EditorBrowsable(EditorBrowsableState.Never)]
internal static class IsExternalInit { }
