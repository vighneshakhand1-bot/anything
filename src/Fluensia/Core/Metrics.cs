using System;
using System.Collections.Generic;

namespace Fluensia.Core
{
    /// <summary>
    /// Inter metrics (advance widths in per-mille of em, plus vertical metrics) baked from the bundled
    /// Inter-Medium face. Used by the GPU-less layout preview so text runs measure like the real atlas.
    /// Regenerate with:  python3 tools/gen_metrics.py
    /// </summary>
    public static class Metrics
    {
        public const float XHeight = 1111 / 1000f;
        public const float CapHeight = 1490 / 1000f;
        public const float Ascender = 1984 / 1000f;
        public const float Descender = -494 / 1000f;
        public const float LineHeight = 2478 / 1000f;

        private static readonly ushort[] Codepoints =
        {
            32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47,
            48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63,
            64, 65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79,
            80, 81, 82, 83, 84, 85, 86, 87, 88, 89, 90, 91, 92, 93, 94, 95,
            96, 97, 98, 99, 100, 101, 102, 103, 104, 105, 106, 107, 108, 109, 110, 111,
            112, 113, 114, 115, 116, 117, 118, 119, 120, 121, 122, 123, 124, 125, 126, 1024,
            1025, 1026, 1027, 1028, 1029, 1030, 1031, 1032, 1033, 1034, 1035, 1036, 1037, 1038, 1039, 1040,
            1041, 1042, 1043, 1044, 1045, 1046, 1047, 1048, 1049, 1050, 1051, 1052, 1053, 1054, 1055, 1056,
            1057, 1058, 1059, 1060, 1061, 1062, 1063, 1064, 1065, 1066, 1067, 1068, 1069, 1070, 1071, 1072,
            1073, 1074, 1075, 1076, 1077, 1078, 1079, 1080, 1081, 1082, 1083, 1084, 1085, 1086, 1087, 1088,
            1089, 1090, 1091, 1092, 1093, 1094, 1095, 1096, 1097, 1098, 1099, 1100, 1101, 1102, 1103, 1104,
            1105, 1106, 1107, 1108, 1109, 1110, 1111, 1112, 1113, 1114, 1115, 1116, 1117, 1118, 1119, 8212,
            8211, 8470, 171, 187, 8230, 215, 8984, 183, 8594, 8226, 10003,
        };

        private static readonly ushort[] Advances =
        {
            263, 296, 487, 635, 644, 980, 650, 307, 362, 362, 517, 664, 295, 459, 295, 366,
            643, 410, 612, 625, 653, 601, 626, 566, 625, 626, 295, 306, 664, 664, 664, 528,
            983, 705, 655, 732, 719, 602, 586, 746, 741, 269, 572, 684, 563, 908, 752, 765,
            639, 767, 647, 644, 649, 736, 705, 999, 697, 692, 638, 362, 366, 362, 473, 462,
            328, 563, 614, 572, 614, 583, 374, 615, 597, 248, 248, 555, 248, 884, 597, 599,
            614, 614, 381, 533, 339, 597, 569, 823, 553, 570, 552, 437, 342, 437, 664, 602,
            602, 844, 576, 732, 644, 269, 269, 572, 1111, 1122, 884, 684, 752, 646, 720, 705,
            643, 655, 576, 830, 602, 991, 625, 752, 752, 684, 745, 908, 741, 765, 741, 639,
            732, 649, 646, 829, 697, 733, 699, 964, 983, 817, 888, 650, 732, 1022, 647, 563,
            594, 564, 440, 627, 583, 824, 500, 598, 598, 552, 583, 780, 596, 599, 593, 614,
            572, 480, 570, 713, 553, 614, 582, 831, 850, 652, 753, 564, 572, 845, 559, 583,
            583, 597, 440, 572, 533, 248, 248, 248, 927, 902, 597, 552, 598, 570, 593, 1000,
            500, 1092, 603, 603, 885, 664, 987, 295, 951, 527, 880,
        };

        private static Dictionary<char, float> _map;

        /// <summary>Advance width of one character in em; unknown characters fall back to 0.6 em.</summary>
        public static float Advance(char c)
        {
            if (_map == null)
            {
                _map = new Dictionary<char, float>(Codepoints.Length);
                for (int i = 0; i < Codepoints.Length; i++) _map[(char)Codepoints[i]] = Advances[i] / 1000f;
            }
            return _map.TryGetValue(c, out var a) ? a : 0.6f;
        }

        /// <summary>Width of a string in em (no kerning — Inter's UI faces barely need it at this size).</summary>
        public static float Width(string s)
        {
            float w = 0f;
            if (s != null) foreach (var c in s) w += Advance(c);
            return w;
        }
    }
}
