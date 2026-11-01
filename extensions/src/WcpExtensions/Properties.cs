using System.Runtime.CompilerServices;

// The UI smoke test (extensions/tests/UiSmoke) opens the windows outside Revit.
[assembly: InternalsVisibleTo("WcpExtensions.UiSmoke")]
