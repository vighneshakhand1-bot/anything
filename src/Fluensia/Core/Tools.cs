using System;
using System.Collections.Generic;
using System.IO;

namespace Fluensia.Core
{
    /// <summary>Small verification helpers used by the headless modes in Program.</summary>
    public static class Tools
    {
        /// <summary>Writes the accumulated design-space widget log (see Ui.Layout).</summary>
        public static void DumpLayout(string path)
        {
            if (Ui.Layout == null) return;
            File.WriteAllLines(path, Ui.Layout);
            Ui.Layout.Clear();
        }

        /// <summary>
        /// Asserts that a handful of expected strings made it into the draw list. Keeps the
        /// screenshot mode honest: a silent exception inside a view would show up as a missing label.
        /// </summary>
        public static void Expect(IEnumerable<string> seen, IEnumerable<string> required, out List<string> missing)
        {
            missing = new List<string>();
            var set = new HashSet<string>(seen ?? Array.Empty<string>());
            foreach (var r in required) if (!set.Contains(r)) missing.Add(r);
        }
    }
}
