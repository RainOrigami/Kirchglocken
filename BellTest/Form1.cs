using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Media;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Media;

namespace BellTest
{
    public partial class Form1 : Form
    {
        private Dictionary<string, string> bells = new Dictionary<string, string>()
        {
            { "a0.wav", "A0" },
            { "a1.wav", "A1" },
            { "a2.wav", "A2" },
            { "ais-b0.wav", "B0" },
            { "ais-b1.wav", "B1" },
            { "ais-b2.wav", "B2" },
            { "C0.wav", "C0" },
            { "c1.wav", "C1" },
            { "c2.wav", "C2" },
            { "cis-des0.wav", "C#0" },
            { "cis-des1.wav", "C#1" },
            { "cis-des2.wav", "C#2" },
            { "d0.wav", "D0" },
            { "d1.wav", "D1" },
            { "d2.wav", "D2" },
            { "dis-es0.wav", "D#0" },
            { "dis-es1.wav", "D#1" },
            { "dis-es2.wav", "D#2" },
            { "e0.wav", "E0" },
            { "e1.wav", "E1" },
            { "e2.wav", "E2" },
            { "f0.wav", "F0" },
            { "f1.wav", "F1" },
            { "f2.wav", "F2" },
            { "fis-ges0.wav", "F#0" },
            { "fis-ges1.wav", "F#1" },
            { "fis-ges2.wav", "F#2" },
            { "g0.wav", "G0" },
            { "g1.wav", "G1" },
            { "g2.wav", "G2" },
            { "gis-as0.wav", "G#0" },
            { "gis-as1.wav", "G#1" },
            { "gis-as2.wav", "G#2" },
            { "h0.wav", "H0" },
            { "h1.wav", "H1" },
            { "h2.wav", "H2" }
        };

        private Dictionary<string, MediaPlayer> players;

        public Form1()
        {
            InitializeComponent();

            this.trackBar1.Value = 60;
            _ = this.loadBells();
        }

        private async Task loadBells()
        {
            this.players = this.bells.ToDictionary(kvp => kvp.Key, kvp => new MediaPlayer());
            foreach (string path in this.players.Keys)
            {
                this.players[path].Stop();
                this.players[path].Volume = 0.0;
                this.players[path].Open(new Uri(path, UriKind.Relative));
            }

            while (this.players.Values.Count(p => p.BufferingProgress < 1.0) > 0)
                await Task.Delay(500);

            foreach (string path in this.players.Keys)
            {
                this.players[path].Volume = 1.0;

                this.flowLayoutPanel1.Controls.Add(new CheckBox()
                {
                    Text = this.bells[path]
                });
            }

            this.button1.Enabled = true;
            this.button2.Enabled = true;
        }


        private string GetBellByNote(BellNote note) => this.bells.First(b => b.Value == note.ToString().Replace('s', '#')).Key;
        private BellNote GetNoteByName(string name) => (BellNote)Enum.Parse(typeof(BellNote), Enum.GetNames(typeof(BellNote)).First(n => n.Replace('s', '#') == name));
        private static readonly char[] NOTE_ORDER = new[]
        {
            'C',
            'D',
            'E',
            'F',
            'G',
            'A',
            'B',
            'H'
        };

        CancellationTokenSource primaryCancellation;
        CancellationTokenSource cancellation;

        private static BellNote[] OrderNotes(BellNote[] notes) => notes.OrderBy(b => int.Parse(b.ToString().Substring(b.ToString().Length - 1)) * 10 + Array.IndexOf(NOTE_ORDER, b.ToString()[0])).ToArray();

        private async Task ring(BellNote[] notes, TimeSpan duration)
        {
            BellNote[] orderedNotes = OrderNotes(notes).Reverse().ToArray();
            BellNote startingBell = orderedNotes[0];
            if (orderedNotes.Length > 1)
                startingBell = orderedNotes[1];
            orderedNotes = orderedNotes.Where(n => n != startingBell).ToArray();

            this.primaryCancellation = new CancellationTokenSource(duration + WINDDOWN_DELAY);
            _ = this.loopBell(startingBell, this.primaryCancellation.Token);
            int delay = 2500;
            await Task.Delay(delay);

            if (orderedNotes.Length > 0)
            {
                this.cancellation = new CancellationTokenSource(duration);
                int leftElement = 0;
                int rightElement = orderedNotes.Length - 1;

                for (int i = 0; i < orderedNotes.Length; i++)
                {
                    int noteIndex = i % 2 == 0 ? leftElement++ : rightElement--;
                    _ = this.loopBell(orderedNotes[noteIndex], cancellation.Token);
                    delay = Math.Max(650, delay - 500);
                    await Task.Delay(delay);
                }
            }

            while (!this.primaryCancellation.IsCancellationRequested)
                await Task.Delay(500);

            Console.WriteLine("This is where we stop");
            this.button1.Enabled = true;
        }

        private async Task loopBell(BellNote note, CancellationToken token)
        {
            Console.WriteLine($"Playing {note}");
            MediaPlayer player = this.players[GetBellByNote(note)];
            while (!token.IsCancellationRequested)
            {
                player.Position = new TimeSpan(0);
                player.Play();
                await Task.Delay((int)player.NaturalDuration.TimeSpan.TotalMilliseconds);
            }
            Console.WriteLine($"Winding down {note}");
            player.Position = new TimeSpan(0);
            player.Play();
            while (true)
            {
                player.Volume = Math.Max(player.Volume - 0.025, 0);
                Console.WriteLine($"Reduced {note} to {player.Volume * 100.0}% volume");
                await Task.Delay(250);
                if (player.Volume == 0)
                    break;
            }
            Console.WriteLine($"Ended {note}");
        }

        private void Button1_Click(object sender, EventArgs e)
        {
            BellNote[] selectedBells = this.flowLayoutPanel1.Controls.OfType<CheckBox>().Where(c => c.Checked).Select(c => GetNoteByName(c.Text)).ToArray();

            this.button1.Enabled = false;
            _ = this.ring(selectedBells, new TimeSpan(0, 0, this.trackBar1.Value));
        }

        private void TrackBar1_ValueChanged(object sender, EventArgs e)
        {
            TimeSpan duration = new TimeSpan(0, 0, this.trackBar1.Value);
            this.label1.Text = $"Duration: {duration.Minutes} minutes and {duration.Seconds} seconds";
        }

        private void Button3_Click(object sender, EventArgs e)
        {
            this.applyTemplate(new[] { BellNote.F1, BellNote.B1, BellNote.C2, BellNote.D2, BellNote.F2 });
        }

        private static readonly TimeSpan WINDDOWN_DELAY = new TimeSpan(0, 0, 3);

        private void Button2_Click(object sender, EventArgs e)
        {
            this.cancellation?.Cancel();
            this.primaryCancellation?.CancelAfter(WINDDOWN_DELAY);
        }

        private void Button4_Click(object sender, EventArgs e)
        {
            this.applyTemplate(new[] { BellNote.F2, BellNote.Ds1, BellNote.Gs1 });
        }

        private void applyTemplate(BellNote[] template)
        {
            List<CheckBox> bellBoxes = this.flowLayoutPanel1.Controls.OfType<CheckBox>().ToList();
            bellBoxes.ForEach(b => b.Checked = false);
            bellBoxes.Where(b => template.Contains(GetNoteByName(b.Text))).ToList().ForEach(b => b.Checked = true);
        }
    }

    enum BellNote
    {
        A0,
        A1,
        A2,
        B0,
        B1,
        B2,
        C0,
        C1,
        C2,
        Cs0,
        Cs1,
        Cs2,
        D0,
        D1,
        D2,
        Ds0,
        Ds1,
        Ds2,
        E0,
        E1,
        E2,
        F0,
        F1,
        F2,
        Fs0,
        Fs1,
        Fs2,
        G0,
        G1,
        G2,
        Gs0,
        Gs1,
        Gs2,
        H0,
        H1,
        H2
    }
}
