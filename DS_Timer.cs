using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Media;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Speech.Synthesis;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DSTimer
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TimerForm());
        }
    }

    sealed class TimerForm : Form
    {
        const int HotkeyId = 0x4453;
        const int MOD_ALT = 0x0001;
        const int VK_F = 0x46;
        const int WM_HOTKEY = 0x0312;
        static readonly TimeSpan MainDuration = TimeSpan.FromSeconds(60);
        static readonly TimeSpan ShortDuration = TimeSpan.FromSeconds(15);

        readonly CountdownPanel[] panels = new CountdownPanel[4];
        readonly SlotState[] slots = new SlotState[4];
        readonly List<ShortVoiceCountdown> shortCountdowns = new List<ShortVoiceCountdown>();
        readonly Timer uiTimer = new Timer();
        readonly Label statusLabel = new Label();

        [DllImport("user32.dll")]
        static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

        [DllImport("user32.dll")]
        static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public TimerForm()
        {
            Text = "DS Timer";
            MinimumSize = new Size(760, 560);
            Size = new Size(860, 620);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(13, 18, 28);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            DoubleBuffered = true;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24),
                BackColor = BackColor,
                ColumnCount = 1,
                RowCount = 3
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
            Controls.Add(root);

            var header = BuildHeader();
            root.Controls.Add(header, 0, 0);

            var topGrid = BuildGrid(2, 1);
            var bottomGrid = BuildGrid(2, 1);
            root.Controls.Add(topGrid, 0, 1);
            root.Controls.Add(bottomGrid, 0, 2);

            panels[0] = new CountdownPanel(1, true);
            panels[1] = new CountdownPanel(2, true);
            panels[2] = new CountdownPanel(3, false);
            panels[3] = new CountdownPanel(4, false);

            topGrid.Controls.Add(panels[0], 0, 0);
            topGrid.Controls.Add(panels[1], 1, 0);
            bottomGrid.Controls.Add(panels[2], 0, 0);
            bottomGrid.Controls.Add(panels[3], 1, 0);

            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = new SlotState();
                panels[i].SetIdle();
            }

            uiTimer.Interval = 100;
            uiTimer.Tick += OnUiTick;
            uiTimer.Start();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            RegisterHotKey(Handle, HotkeyId, MOD_ALT, VK_F);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            UnregisterHotKey(Handle, HotkeyId);
            base.OnHandleDestroyed(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HotkeyId)
            {
                StartNextTimer();
                return;
            }

            base.WndProc(ref m);
        }

        Control BuildHeader()
        {
            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = BackColor
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));

            var title = new Label
            {
                Dock = DockStyle.Fill,
                Text = "DS Timer",
                Font = new Font("Segoe UI Semibold", 25F, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = Color.FromArgb(245, 248, 255),
                TextAlign = ContentAlignment.MiddleLeft
            };
            header.Controls.Add(title, 0, 0);

            statusLabel.Dock = DockStyle.Fill;
            statusLabel.Text = "Alt + F";
            statusLabel.Font = new Font("Segoe UI Semibold", 14F, FontStyle.Bold, GraphicsUnit.Point);
            statusLabel.ForeColor = Color.FromArgb(137, 221, 255);
            statusLabel.TextAlign = ContentAlignment.MiddleRight;
            header.Controls.Add(statusLabel, 1, 0);

            return header;
        }

        static TableLayoutPanel BuildGrid(int columns, int rows)
        {
            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                ColumnCount = columns,
                RowCount = rows,
                Padding = new Padding(0, 8, 0, 8)
            };

            for (int i = 0; i < columns; i++)
            {
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / columns));
            }

            for (int i = 0; i < rows; i++)
            {
                grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100F / rows));
            }

            return grid;
        }

        void StartNextTimer()
        {
            int index = Array.FindIndex(slots, slot => !slot.Active);
            if (index < 0)
            {
                statusLabel.Text = "All timers active";
                FlashStatus(Color.FromArgb(255, 205, 113));
                return;
            }

            slots[index].Start(DateTime.UtcNow, MainDuration);
            panels[index].SetRunning(MainDuration);
            shortCountdowns.Add(new ShortVoiceCountdown(DateTime.UtcNow, ShortDuration));
            statusLabel.Text = "Started timer " + (index + 1);
            FlashStatus(Color.FromArgb(124, 255, 193));
            PlayEmbeddedSound("DSTimer.Audio.down.wav");
        }

        void OnUiTick(object sender, EventArgs e)
        {
            var now = DateTime.UtcNow;

            for (int i = 0; i < slots.Length; i++)
            {
                if (!slots[i].Active)
                {
                    continue;
                }

                var remaining = slots[i].EndTime - now;
                if (remaining <= TimeSpan.Zero)
                {
                    slots[i].Active = false;
                    panels[i].SetIdle();
                    statusLabel.Text = "Timer " + (i + 1) + " complete";
                    FlashStatus(Color.FromArgb(137, 221, 255));
                    PlayEmbeddedSound("DSTimer.Audio.megabeam.wav");
                }
                else
                {
                    panels[i].SetRunning(remaining);
                }
            }

            for (int i = shortCountdowns.Count - 1; i >= 0; i--)
            {
                if (shortCountdowns[i].Tick(now))
                {
                    shortCountdowns.RemoveAt(i);
                }
            }
        }

        async void FlashStatus(Color color)
        {
            var original = statusLabel.ForeColor;
            statusLabel.ForeColor = color;
            await Task.Delay(450);
            if (!IsDisposed)
            {
                statusLabel.ForeColor = original;
            }
        }

        static void PlayEmbeddedSound(string resourceName)
        {
            Task.Run(() =>
            {
                try
                {
                    var assembly = Assembly.GetExecutingAssembly();
                    using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                    {
                        if (stream == null)
                        {
                            return;
                        }

                        using (var player = new SoundPlayer(stream))
                        {
                            player.PlaySync();
                        }
                    }
                }
                catch
                {
                    // Audio should never stop the timers.
                }
            });
        }
    }

    sealed class SlotState
    {
        public bool Active;
        public DateTime EndTime;

        public void Start(DateTime now, TimeSpan duration)
        {
            Active = true;
            EndTime = now + duration;
        }
    }

    sealed class ShortVoiceCountdown
    {
        readonly DateTime startTime;
        readonly TimeSpan duration;
        readonly bool[] spoken = new bool[4];
        static readonly string[] Words = { "three", "two", "one", "go" };
        static readonly TimeSpan[] Offsets =
        {
            TimeSpan.FromSeconds(12),
            TimeSpan.FromSeconds(13),
            TimeSpan.FromSeconds(14),
            TimeSpan.FromSeconds(15)
        };

        public ShortVoiceCountdown(DateTime startTime, TimeSpan duration)
        {
            this.startTime = startTime;
            this.duration = duration;
        }

        public bool Tick(DateTime now)
        {
            var elapsed = now - startTime;
            for (int i = 0; i < Words.Length; i++)
            {
                if (!spoken[i] && elapsed >= Offsets[i])
                {
                    spoken[i] = true;
                    Speak(Words[i]);
                }
            }

            return elapsed >= duration + TimeSpan.FromMilliseconds(600);
        }

        static void Speak(string word)
        {
            Task.Run(() =>
            {
                try
                {
                    using (var synth = new SpeechSynthesizer())
                    {
                        synth.Volume = 100;
                        synth.Rate = 2;
                        try
                        {
                            synth.SelectVoiceByHints(VoiceGender.Female, VoiceAge.Teen);
                        }
                        catch
                        {
                            try
                            {
                                synth.SelectVoiceByHints(VoiceGender.Female);
                            }
                            catch
                            {
                            }
                        }

                        synth.Speak(word);
                    }
                }
                catch
                {
                    SystemSounds.Beep.Play();
                }
            });
        }
    }

    sealed class CountdownPanel : Panel
    {
        readonly Label nameLabel = new Label();
        readonly Label timeLabel = new Label();
        readonly Label stateLabel = new Label();
        readonly bool large;
        float progress;
        bool active;

        public CountdownPanel(int number, bool large)
        {
            this.large = large;
            Margin = new Padding(8);
            Padding = new Padding(18);
            Dock = DockStyle.Fill;
            DoubleBuffered = true;
            BackColor = Color.Transparent;

            nameLabel.Text = "Timer " + number;
            nameLabel.Dock = DockStyle.Top;
            nameLabel.Height = large ? 34 : 28;
            nameLabel.TextAlign = ContentAlignment.MiddleLeft;
            nameLabel.Font = new Font("Segoe UI Semibold", large ? 14F : 12F, FontStyle.Bold, GraphicsUnit.Point);
            nameLabel.ForeColor = Color.FromArgb(174, 188, 214);

            timeLabel.Dock = DockStyle.Fill;
            timeLabel.TextAlign = ContentAlignment.MiddleCenter;
            timeLabel.Font = new Font("Segoe UI Semibold", large ? 52F : 38F, FontStyle.Bold, GraphicsUnit.Point);
            timeLabel.ForeColor = Color.FromArgb(245, 248, 255);

            stateLabel.Dock = DockStyle.Bottom;
            stateLabel.Height = large ? 38 : 32;
            stateLabel.TextAlign = ContentAlignment.MiddleCenter;
            stateLabel.Font = new Font("Segoe UI Semibold", large ? 14F : 12F, FontStyle.Bold, GraphicsUnit.Point);
            stateLabel.ForeColor = Color.FromArgb(137, 221, 255);

            Controls.Add(timeLabel);
            Controls.Add(stateLabel);
            Controls.Add(nameLabel);
        }

        public void SetIdle()
        {
            active = false;
            progress = 0F;
            timeLabel.Text = large ? "Not Started" : "Idle";
            timeLabel.Font = new Font("Segoe UI Semibold", large ? 29F : 23F, FontStyle.Bold, GraphicsUnit.Point);
            stateLabel.Text = "Not Started";
            Invalidate();
        }

        public void SetRunning(TimeSpan remaining)
        {
            active = true;
            progress = Math.Max(0F, Math.Min(1F, (float)(remaining.TotalSeconds / 60D)));
            timeLabel.Font = new Font("Segoe UI Semibold", large ? 52F : 38F, FontStyle.Bold, GraphicsUnit.Point);
            timeLabel.Text = Math.Ceiling(remaining.TotalSeconds).ToString("00") + "s";
            stateLabel.Text = "Running";
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            var rect = ClientRectangle;
            rect.Width -= 1;
            rect.Height -= 1;

            using (var path = RoundedRect(rect, 18))
            using (var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                rect,
                active ? Color.FromArgb(34, 48, 76) : Color.FromArgb(24, 31, 46),
                active ? Color.FromArgb(22, 90, 94) : Color.FromArgb(22, 27, 39),
                35F))
            using (var pen = new Pen(active ? Color.FromArgb(93, 230, 190) : Color.FromArgb(48, 61, 84), 1.5F))
            {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }

            var bar = new Rectangle(18, Height - 16, Width - 36, 5);
            using (var back = new SolidBrush(Color.FromArgb(45, 56, 78)))
            {
                e.Graphics.FillRectangle(back, bar);
            }

            if (active)
            {
                var fill = new Rectangle(bar.X, bar.Y, (int)(bar.Width * progress), bar.Height);
                using (var front = new SolidBrush(Color.FromArgb(124, 255, 193)))
                {
                    e.Graphics.FillRectangle(front, fill);
                }
            }
        }

        static System.Drawing.Drawing2D.GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
