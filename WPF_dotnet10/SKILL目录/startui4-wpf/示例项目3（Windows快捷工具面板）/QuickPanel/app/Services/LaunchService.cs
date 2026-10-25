using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using QuickPanel.Models;

namespace QuickPanel.Services
{
    /// <summary>一次执行的结果。Message 直接进页脚，失败时也会被拼进错误弹窗。</summary>
    internal sealed class LaunchResult
    {
        public bool Ok;
        public string Message;
    }

    /// <summary>
    /// 拉起系统入口。走 ShellExecute（UseShellExecute=true），语义与「运行」对话框一致：
    /// 裸名 sysdm.cpl 由 Shell 在 PATH / App Paths 里解析，ms-settings:display 走协议关联，
    /// 带清单的条目（services.msc 等）由系统弹 UAC——这些都不是我们能替代也不需要模仿的。
    ///
    /// 不要用 cmd.exe /c start 兜一层：那会多一个控制台窗口闪一下。
    /// </summary>
    internal static class LaunchService
    {
        /// <summary>
        /// 命令原文按第一个空格切成「文件 + 参数」。
        /// 数据源里的三种形态都靠这一条口径覆盖：`control /name Microsoft.X`、
        /// `explorer.exe shell:Fonts`、`rundll32.exe shell32.dll,Options_RunDLL 7`。
        /// 纯 URI 与裸文件名没有空格，切完 args 为空串。
        /// </summary>
        public static void Split(string command, out string file, out string args)
        {
            file = command == null ? string.Empty : command.Trim();
            args = string.Empty;

            int space = file.IndexOf(' ');
            if (space < 0) return;

            args = file.Substring(space + 1).Trim();
            file = file.Substring(0, space);
        }

        /// <summary>内置脚本的命令前缀：<c>script:port_killer_v3</c>。卡片上显示的就是它，收藏键也不随安装位置变。</summary>
        public const string ScriptScheme = "script:";

        /// <summary>
        /// 把内置脚本释放到 <c>%LOCALAPPDATA%\QuickPanel\scripts\&lt;内容哈希&gt;\</c> 并返回路径。
        /// 目录名带内容哈希：脚本升级后落到新目录，不会覆盖正在被 cmd 逐行读取的旧副本；
        /// 资源名按后缀匹配（<c>*.&lt;名字&gt;.cmd</c>），不依赖 Link 路径推出来的命名。
        /// </summary>
        private static string MaterializeScript(string name, out string error)
        {
            error = null;
            string bare = name == null ? string.Empty : name.Trim();
            if (bare.Length == 0) { error = "内置脚本名为空"; return null; }
            if (bare.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase))
                bare = bare.Substring(0, bare.Length - 4);

            var assembly = typeof(LaunchService).Assembly;
            string resource = null;
            foreach (string candidate in assembly.GetManifestResourceNames())
            {
                if (candidate.EndsWith("." + bare + ".cmd", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(candidate, bare + ".cmd", StringComparison.OrdinalIgnoreCase))
                {
                    resource = candidate;
                    break;
                }
            }
            if (resource == null)
            {
                error = "程序集里没有内置脚本 " + bare + ".cmd";
                return null;
            }

            byte[] bytes;
            using (var stream = assembly.GetManifestResourceStream(resource))
            {
                if (stream == null) { error = "无法读取内置脚本 " + resource; return null; }
                using (var memory = new MemoryStream())
                {
                    stream.CopyTo(memory);
                    bytes = memory.ToArray();
                }
            }

            string hash;
            using (var sha = SHA256.Create())
            {
                byte[] digest = sha.ComputeHash(bytes);
                var text = new StringBuilder(8);
                for (int i = 0; i < 4; i++) text.Append(digest[i].ToString("x2"));
                hash = text.ToString();
            }

            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "QuickPanel", "scripts", hash);
            string target = Path.Combine(dir, bare + ".cmd");

            try
            {
                if (!File.Exists(target))
                {
                    Directory.CreateDirectory(dir);
                    try
                    {
                        File.WriteAllBytes(target, bytes);
                    }
                    catch (IOException)
                    {
                        // 另一个实例刚好写完：内容同哈希，谁写都是同一份
                        if (!File.Exists(target)) throw;
                    }
                }
            }
            catch (Exception ex)
            {
                error = "释放内置脚本失败：" + ex.GetType().Name + " " + ex.Message;
                return null;
            }

            return target;
        }

        public static LaunchResult Launch(ToolItem item)
        {
            var result = new LaunchResult();
            if (item == null || string.IsNullOrWhiteSpace(item.Command))
            {
                result.Ok = false;
                result.Message = "数据有问题：这一项没有可执行的命令";
                return result;
            }

            string file, args;
            if (item.Command.StartsWith(ScriptScheme, StringComparison.OrdinalIgnoreCase))
            {
                string error;
                file = MaterializeScript(item.Command.Substring(ScriptScheme.Length), out error);
                if (file == null)
                {
                    result.Ok = false;
                    result.Message = "执行「" + item.Title + "」失败：" + error;
                    return result;
                }
                args = string.Empty;
            }
            else
            {
                Split(item.Command, out file, out args);
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = file,
                    Arguments = args,
                    UseShellExecute = true
                };
                using (var process = Process.Start(psi))
                {
                    // ShellExecute 拉起已运行的单实例程序（如任务管理器）时可能返回 null，不是失败
                    result.Ok = true;
                    result.Message = "已执行「" + item.Title + "」 " + item.Command
                        + (process == null ? string.Empty : "（pid " + process.Id + "）");
                }
            }
            catch (Win32Exception ex)
            {
                result.Ok = false;
                result.Message = BuildFailureText(item, file, ex);
            }
            catch (Exception ex)
            {
                result.Ok = false;
                result.Message = "执行「" + item.Title + "」失败：" + ex.GetType().Name + " " + ex.Message;
            }

            return result;
        }

        /// <summary>
        /// 失败文案要能把"家庭版根本没这个文件"和"权限不够"分开说——
        /// 这两件事的处置完全不同，混成一句"启动失败"用户只能瞎试。
        /// </summary>
        private static string BuildFailureText(ToolItem item, string file, Win32Exception ex)
        {
            string reason;
            if (item.HomeEditionMissing)
                reason = "这个组件在家庭版里没有自带（属 SKU 差异，不是权限问题）。";
            else if (item.Availability == ToolAvailability.Missing)
                reason = "本机在预期路径下没找到这个文件。";
            else if (ex.NativeErrorCode == 1223 || ex.NativeErrorCode == 1225)
                reason = "取消提权或被系统拦住了。";
            else
                reason = "系统拒绝启动这个入口。";

            string hint = string.Empty;
            if (item.Probe == ToolProbe.None
                && !item.Command.StartsWith(ScriptScheme, StringComparison.OrdinalIgnoreCase))
                hint = "若是 ms-settings 页，未知页名会静默回到设置首页。";

            return "无法执行「" + item.Title + "」：" + reason + " " + hint
                + "命令 " + file + "，错误 " + ex.NativeErrorCode + "：" + ex.Message;
        }
    }
}
