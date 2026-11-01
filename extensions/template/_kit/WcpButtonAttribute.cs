// WCP Extensions kit - do not edit.
using System;

namespace __TOOLKIT_ID__
{
    /// <summary>
    /// Put this on a public class that implements IExternalCommand to give it a button on the ribbon.
    /// WCP Extensions reads it when you press Reload.
    /// <code>
    /// [Transaction(TransactionMode.Manual)]
    /// [WcpButton("Count\nWalls", Panel = "Walls", Tooltip = "Counts the walls in the active view.")]
    /// public class CountWallsCmd : IExternalCommand { ... }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class WcpButtonAttribute : Attribute
    {
        /// <param name="text">Button text. Use \n to split it over two lines.</param>
        public WcpButtonAttribute(string text)
        {
            Text = text;
        }

        /// <summary>Button text. Use \n to split it over two lines.</summary>
        public string Text { get; }

        /// <summary>Ribbon panel the button goes in. Default "Tools".</summary>
        public string Panel { get; set; } = "Tools";

        /// <summary>Text shown when the mouse rests on the button.</summary>
        public string Tooltip { get; set; }

        /// <summary>
        /// 32x32 PNG, path relative to the toolkit folder, e.g. "Icons/count-walls.png".
        /// Leave empty for an icon made from the button's initials.
        /// </summary>
        public string Icon { get; set; }

        /// <summary>Order of the button inside its panel (smaller first).</summary>
        public int Order { get; set; }

        /// <summary>False lets the button work with no model open. Default true.</summary>
        public bool NeedsDocument { get; set; } = true;
    }
}
