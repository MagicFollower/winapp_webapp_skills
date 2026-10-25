using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace QuickPanel.Services
{
    /// <summary>
    /// 应用设置持久化（%APPDATA%\QuickPanel\settings.json，kv1 纯文本格式；文件名沿用历史叫法）。
    ///
    /// Load 与 Save 对外永不抛异常：损坏文件改名留档后回默认值，写失败只记诊断日志——
    /// 设置出问题不该让应用起不来，更不该在 UI 线程上重试等待（那会直接冻住界面）。
    /// </summary>
    public class SettingsService
    {
        /// <summary>配色档的稳定键：light / dark / system / 套装键（如 paper-grey）。由 ThemeService 校验。</summary>
        public string ThemeKey { get; set; }
        /// <summary>字体族名；空串表示用出厂族（Typography.DefaultFontFamilySource）。</summary>
        public string FontFamilyName { get; set; }
        /// <summary>正文基准字号，其余层级由 Typography 按差值派生。</summary>
        public double BaseFontSize { get; set; }
        /// <summary>全局缩放百分比，50–200。</summary>
        public double ZoomPercent { get; set; }

        public double WindowWidth { get; set; }
        public double WindowHeight { get; set; }
        public double WindowLeft { get; set; }
        public double WindowTop { get; set; }
        public bool WindowMaximized { get; set; }

        /// <summary>
        /// 收藏的命令原文，按加入顺序。身份用 Command 而不是标题或索引：命令全表唯一由自测钉住，
        /// 而索引会在数据源增删项时静默指向另一张卡。
        /// </summary>
        public List<string> Favorites { get; private set; }

        /// <summary>收藏落盘的键名前缀，实际键是 fav0 / fav1 …（一行一项，见 ToMap 处的说明）。</summary>
        internal const string FavoriteKeyPrefix = "fav";

        /// <summary>
        /// 收藏条数上限。设置文件是用户能自己开编辑器改的，超上限的部分直接丢——
        /// 与其让一份改坏的文件吃掉 MaxSettingsBytes 配额，不如只认前 200 条。
        /// </summary>
        internal const int MaxFavorites = 200;

        /// <summary>健康文件约 200 字节；超限即判损坏，避免脏数据拖死启动。</summary>
        internal const int MaxSettingsBytes = 64 * 1024;

        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        private static readonly string SettingsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QuickPanel");

        private static readonly string SettingsFile = Path.Combine(SettingsDir, "settings.json");

        /// <summary>设置文件所在目录，只给"查看"用；不保证已存在（首次保存时才创建）。</summary>
        public static string SettingsDirectory { get { return SettingsDir; } }

        private string _lastWrittenText;

        /// <summary>构造即默认值，且不碰磁盘——这样 --selftest 里顺手 new 一个也不会读到用户那份文件。</summary>
        public SettingsService()
        {
            ApplyDefaults();
        }

        public void Load()
        {
            ApplyDefaults();
            _lastWrittenText = null;

            try
            {
                if (!File.Exists(SettingsFile)) return;

                var info = new FileInfo(SettingsFile);
                if (info.Length > MaxSettingsBytes)
                {
                    Quarantine("oversized: " + info.Length + " bytes");
                    return;
                }

                var map = SettingsCodec.Parse(File.ReadAllText(SettingsFile, Utf8NoBom));
                ApplyMap(map);
                _lastWrittenText = SettingsCodec.Serialize(ToMap());
            }
            catch (Exception ex)
            {
                Quarantine("parse failed: " + ex.GetType().Name + " " + ex.Message);
                ApplyDefaults();
            }
        }

        public void Save()
        {
            string text;
            try
            {
                text = SettingsCodec.Serialize(ToMap());
            }
            catch (Exception ex)
            {
                TryAppendDiagnostics("serialize", ex);
                return;
            }

            if (text.Length > MaxSettingsBytes)
            {
                TryAppendDiagnostics("refuse-write",
                    new IOException("serialized settings too large: " + text.Length));
                return;
            }

            // 内容没变就不写盘：滑杆每动一格都重新 Save 一次会把手动改的其它键一起冲掉，也是无谓的 IO
            if (text == _lastWrittenText && File.Exists(SettingsFile)) return;

            try
            {
                if (!Directory.Exists(SettingsDir)) Directory.CreateDirectory(SettingsDir);

                var tempFile = SettingsFile + ".tmp";
                File.WriteAllText(tempFile, text, Utf8NoBom);
                File.Copy(tempFile, SettingsFile, true);
                TryDelete(tempFile);

                _lastWrittenText = text;
            }
            catch (Exception ex)
            {
                TryDelete(SettingsFile + ".tmp");
                TryAppendDiagnostics("save", ex);
            }
        }

        internal List<KeyValuePair<string, string>> ToMap()
        {
            var ci = CultureInfo.InvariantCulture;
            var entries = new List<KeyValuePair<string, string>>();

            entries.Add(Pair("format", "kv1"));
            entries.Add(Pair("themeKey", ThemeKey));
            entries.Add(Pair("fontFamilyName", FontFamilyName));
            entries.Add(Pair("baseFontSize", BaseFontSize.ToString(ci)));
            entries.Add(Pair("zoomPercent", ZoomPercent.ToString(ci)));
            entries.Add(Pair("windowWidth", WindowWidth.ToString(ci)));
            entries.Add(Pair("windowHeight", WindowHeight.ToString(ci)));
            entries.Add(Pair("windowLeft", WindowLeft.ToString(ci)));
            entries.Add(Pair("windowTop", WindowTop.ToString(ci)));
            entries.Add(Pair("windowMaximized", WindowMaximized ? "true" : "false"));

            // 收藏一行一项、键带序号：kv1 的值原样存不转义，拼接式（逗号分隔）会撞上
            // 「文本服务和输入语言」那条命令里的逗号，序号键则天然无歧义且能手删中间一行。
            for (int i = 0; i < Favorites.Count; i++)
                entries.Add(Pair(FavoriteKeyPrefix + i, Favorites[i]));

            return entries;
        }

        internal void ApplyMap(IDictionary<string, string> map)
        {
            if (map == null) return;

            string value;
            if (map.TryGetValue("themeKey", out value) && !string.IsNullOrWhiteSpace(value)) ThemeKey = value.Trim();
            if (map.TryGetValue("fontFamilyName", out value)) FontFamilyName = value;

            double number;
            var ci = CultureInfo.InvariantCulture;
            if (map.TryGetValue("baseFontSize", out value) &&
                double.TryParse(value, NumberStyles.Float, ci, out number))
                BaseFontSize = Helpers.Typography.ClampBase(number);
            if (map.TryGetValue("zoomPercent", out value) &&
                double.TryParse(value, NumberStyles.Float, ci, out number))
                ZoomPercent = Helpers.Typography.ClampZoom(number);

            // 字号与缩放一律钳回区间：设置文件是用户能自己开编辑器改的，
            // 500% 或 4 px 这种值不能让界面直接吃下去；解析不了的保留已生效值不覆盖
            if (map.TryGetValue("windowWidth", out value) && TrySize(value, ci, out number)) WindowWidth = number;
            if (map.TryGetValue("windowHeight", out value) && TrySize(value, ci, out number)) WindowHeight = number;
            if (map.TryGetValue("windowLeft", out value) && TryOffset(value, ci, out number)) WindowLeft = number;
            if (map.TryGetValue("windowTop", out value) && TryOffset(value, ci, out number)) WindowTop = number;

            bool flag;
            if (map.TryGetValue("windowMaximized", out value) && bool.TryParse(value, out flag))
                WindowMaximized = flag;

            // 收藏整表重建而不是增量合并：文件里没有"删掉某一条"的表示法，
            // 合并会让手删的一行在下次保存时复活。
            // 但"map 里一个 fav 键都没有"要留着现值不清——那正是"null / 空 map 不清现值"这条不变量；
            // 用户真的清空过收藏时，落盘的文件本来就没有 fav 键，此时现值来自 Load 开头的 ApplyDefaults。
            if (HasFavoriteKeys(map)) Favorites = ReadFavorites(map);
        }

        private static bool HasFavoriteKeys(IDictionary<string, string> map)
        {
            if (map == null) return false;
            foreach (var entry in map)
            {
                int index;
                if (TryFavoriteIndex(entry.Key, out index)) return true;
            }
            return false;
        }

        /// <summary>
        /// 读侧：只认 <c>fav&lt;非负整数&gt;</c> 这类键，按键尾序号排序后取值。
        /// 不靠 Dictionary 的枚举顺序——那是没有契约的；手改过的文件（乱序、删了中间一行、
        /// 序号不连续）也要能按用户看到的顺序回正。空值、重复项与超上限的部分丢弃。
        /// </summary>
        internal static List<string> ReadFavorites(IDictionary<string, string> map)
        {
            var result = new List<string>();
            if (map == null) return result;

            var picked = new List<KeyValuePair<int, string>>();
            foreach (var entry in map)
            {
                int index;
                if (!TryFavoriteIndex(entry.Key, out index)) continue;
                string command = (entry.Value ?? string.Empty).Trim();
                if (command.Length == 0) continue;
                picked.Add(new KeyValuePair<int, string>(index, command));
            }
            picked.Sort(delegate(KeyValuePair<int, string> a, KeyValuePair<int, string> b)
            {
                return a.Key.CompareTo(b.Key);
            });

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in picked)
            {
                if (result.Count >= MaxFavorites) break;
                if (seen.Add(entry.Value)) result.Add(entry.Value);
            }
            return result;
        }

        /// <summary>favX / favorite / fav-1 都不算收藏键：宁可少读，也不把手改坏的键当数据吃进来。</summary>
        private static bool TryFavoriteIndex(string key, out int index)
        {
            index = 0;
            if (string.IsNullOrEmpty(key)) return false;
            if (key.Length <= FavoriteKeyPrefix.Length) return false;
            if (key.IndexOf(FavoriteKeyPrefix, StringComparison.Ordinal) != 0) return false;
            return int.TryParse(key.Substring(FavoriteKeyPrefix.Length), NumberStyles.None,
                CultureInfo.InvariantCulture, out index) && index >= 0;
        }

        /// <summary>这一项当前在不在收藏里。命令为空回 false。</summary>
        public bool IsFavorite(string command)
        {
            return IndexOfFavorite(command) >= 0;
        }

        /// <summary>
        /// 切换这一项的收藏状态，返回<b>切换后它是否处于收藏中</b>（不是"这次加了还是删了"）：
        /// 达到 MaxFavorites 时不加，回的就是 false，调用方拿它回写卡片镜像正好自洽。
        /// </summary>
        public bool ToggleFavorite(string command)
        {
            string key = (command ?? string.Empty).Trim();
            if (key.Length == 0) return false;

            int at = IndexOfFavorite(key);
            if (at >= 0)
            {
                Favorites.RemoveAt(at);
                return false;
            }
            if (Favorites.Count >= MaxFavorites) return false;

            Favorites.Add(key);
            return true;
        }

        private int IndexOfFavorite(string command)
        {
            if (string.IsNullOrEmpty(command)) return -1;
            for (int i = 0; i < Favorites.Count; i++)
                if (string.Equals(Favorites[i], command, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        private static KeyValuePair<string, string> Pair(string key, string value)
        {
            return new KeyValuePair<string, string>(key, value ?? string.Empty);
        }

        private static bool TrySize(string value, CultureInfo ci, out double result)
        {
            return double.TryParse(value, NumberStyles.Float, ci, out result)
                && result > 0 && result <= 20000;
        }

        private static bool TryOffset(string value, CultureInfo ci, out double result)
        {
            return double.TryParse(value, NumberStyles.Float, ci, out result)
                && result > -20000 && result < 20000;
        }

        private void ApplyDefaults()
        {
            // Policy = both：出厂跟随系统档，用户手选过就以手选的为准（Load 会覆盖这一行）
            ThemeKey = "system";
            FontFamilyName = string.Empty;
            BaseFontSize = Helpers.Typography.DefaultBaseSize;
            ZoomPercent = Helpers.Typography.DefaultZoomPercent;
            WindowWidth = 1120;
            WindowHeight = 760;
            WindowLeft = double.NaN;
            WindowTop = double.NaN;
            WindowMaximized = false;
            Favorites = new List<string>();
        }

        /// <summary>只改名不删除，保留用户数据的取证可能。</summary>
        private static void Quarantine(string reason)
        {
            TryAppendDiagnostics("quarantine", new IOException(reason + " -> " + SettingsFile));
            try
            {
                if (!File.Exists(SettingsFile)) return;
                var target = SettingsFile + ".corrupt-"
                    + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".bak";
                File.Move(SettingsFile, target);
            }
            catch
            {
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
            }
        }

        private static void TryAppendDiagnostics(string stage, Exception ex)
        {
            try
            {
                if (!Directory.Exists(SettingsDir)) Directory.CreateDirectory(SettingsDir);

                var log = Path.Combine(SettingsDir, "diagnostics.log");
                if (File.Exists(log) && new FileInfo(log).Length > 256 * 1024) File.Delete(log);

                File.AppendAllText(log,
                    DateTime.Now.ToString("o") + " [" + stage + "] " + ex.Message + "\r\n",
                    Utf8NoBom);
            }
            catch
            {
            }
        }
    }
}
