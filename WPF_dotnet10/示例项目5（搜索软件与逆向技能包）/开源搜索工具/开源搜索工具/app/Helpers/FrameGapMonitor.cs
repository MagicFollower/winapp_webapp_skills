#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace IndexApp.Helpers
{
    /// <summary>
    /// 仅用于 --perf 验收：在查询/滚动开始后的短窗口内记录 CompositionTarget.Rendering 的帧间隔。
    /// 不做平均 FPS；判据是窗口内最大间隔与 &gt;100ms 的次数。
    /// </summary>
    public sealed class FrameGapMonitor
    {
        private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
        private readonly List<double> _gaps = new();
        private readonly List<double> _timerLateness = new();
        private readonly DispatcherTimer _timer;
        private double _last = -1;
        private double _lastTimer = -1;
        private double _activeUntil = -1;

        public FrameGapMonitor()
        {
            _timer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(10)
            };
            _timer.Tick += (_, _) =>
            {
                double now = _clock.Elapsed.TotalMilliseconds;
                if (_lastTimer >= 0 && now <= _activeUntil)
                {
                    double late = now - _lastTimer - 10;
                    if (late > 0 && late < 2000) _timerLateness.Add(late);
                }
                _lastTimer = now;
            };
            _timer.Start();
        }

        public void OnRendering(object? sender, EventArgs e)
        {
            double now = _clock.Elapsed.TotalMilliseconds;
            if (_last >= 0 && now <= _activeUntil)
            {
                double gap = now - _last;
                if (gap > 0 && gap < 2000) _gaps.Add(gap);
            }

            _last = now;
        }

        public void Begin(double windowMs = 2500)
        {
            _activeUntil = _clock.Elapsed.TotalMilliseconds + windowMs;
            _last = -1;
        }

        public void WriteReport()
        {
            try
            {
                if (_gaps.Count == 0) return;

                var sorted = _gaps.OrderBy(x => x).ToArray();
                double p95 = sorted[Math.Min(sorted.Length - 1, (int)Math.Ceiling(sorted.Length * 0.95) - 1)];
                int over100 = _gaps.Count(x => x > 100);
                string dir = Path.Combine(Path.GetTempPath(), "IndexAppPerf");
                Directory.CreateDirectory(dir);
                string timerPart = " timer_frames=0 timer_max_ms=0 timer_over100=0";
                if (_timerLateness.Count > 0)
                {
                    var ts = _timerLateness.OrderBy(x => x).ToArray();
                    timerPart = " timer_frames=" + ts.Length
                        + " timer_max_ms=" + ts[^1].ToString("F1")
                        + " timer_over100=" + _timerLateness.Count(x => x > 100);
                }

                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    + " tier=" + (RenderCapability.Tier >> 16)
                    + " frames=" + _gaps.Count
                    + " max_ms=" + sorted[^1].ToString("F1")
                    + " p95_ms=" + p95.ToString("F1")
                    + " over100=" + over100
                    + timerPart
                    + Environment.NewLine;
                File.AppendAllText(Path.Combine(dir, "frames.txt"), line);
            }
            catch
            {
                // 验收探针失败不得影响应用退出
            }
        }
    }
}
