using System.Drawing;
using System.Windows.Forms;

namespace EloCalculator.UI.Pages
{
    /// <summary>A content page with a heading, a subtitle and a body area.</summary>
    internal abstract class Page : UserControl
    {
        private readonly Label subtitle;

        protected Page(AppState state, string title)
        {
            State = state;
            Dock = DockStyle.Fill;
            BackColor = Theme.Background;
            Padding = new Padding(32, 24, 32, 24);

            Body = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background, Padding = new Padding(0, 16, 0, 0) };

            var header = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = Theme.Background };
            var heading = Theme.CreateLabel(title, Theme.Heading);
            heading.Location = new Point(0, 0);
            subtitle = Theme.CreateLabel(string.Empty, Theme.Body, Theme.TextMuted);
            subtitle.Location = new Point(2, 40);
            header.Controls.Add(heading);
            header.Controls.Add(subtitle);

            Controls.Add(Body);
            Controls.Add(header);

            State.RosterChanged += (s, e) =>
            {
                if (Visible)
                    RefreshView();
            };
        }

        protected AppState State { get; }

        protected Panel Body { get; }

        protected string Subtitle
        {
            get => subtitle.Text;
            set => subtitle.Text = value;
        }

        /// <summary>Re-reads the roster into the page's controls.</summary>
        public abstract void RefreshView();

        protected void ShowError(string message) =>
            MessageBox.Show(FindForm(), message, "Elo Calculator", MessageBoxButtons.OK, MessageBoxIcon.Warning);

        protected bool Confirm(string message) =>
            MessageBox.Show(FindForm(), message, "Elo Calculator", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
    }
}
