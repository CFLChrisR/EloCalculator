using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using EloCalculator.UI.Pages;

namespace EloCalculator.UI
{
    internal sealed class MainForm : Form
    {
        private readonly AppState state;
        private readonly Panel content;
        private readonly Label fileLabel;
        private NavButton? selectedNav;

        public MainForm(AppState state)
        {
            this.state = state;

            Text = "Elo Calculator";
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            Font = Theme.Body;
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(1280, 800);
            MinimumSize = new Size(980, 640);
            StartPosition = FormStartPosition.CenterScreen;

            content = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background };

            var pages = new Page[]
            {
                new LeaderboardPage(state),
                new RecordMatchPage(state),
                new HistoryPage(state),
                new BracketPage(state),
            };
            var navs = new[]
            {
                new NavButton("≡", "Leaderboard"),
                new NavButton("⚔", "Record Match"),
                new NavButton("↺", "Match History"),
                new NavButton("⌥", "Bracket"),
            };

            var sidebar = new Panel { Dock = DockStyle.Left, Width = 220, BackColor = Theme.Sidebar, Padding = new Padding(0, 0, 0, 16) };
            var brand = new Panel { Dock = DockStyle.Top, Height = 92, BackColor = Theme.Sidebar };
            var brandTitle = Theme.CreateLabel("Elo Calculator", new Font("Segoe UI Semibold", 14f));
            brandTitle.Location = new Point(22, 26);
            var brandSubtitle = Theme.CreateLabel("Ratings & brackets", Theme.Small, Theme.TextFaint);
            brandSubtitle.Location = new Point(24, 54);
            brand.Controls.Add(brandTitle);
            brand.Controls.Add(brandSubtitle);

            var navPanel = new Panel { Dock = DockStyle.Top, Height = navs.Length * 42, BackColor = Theme.Sidebar };
            for (var i = navs.Length - 1; i >= 0; i--)
            {
                var nav = navs[i];
                var page = pages[i];
                nav.Click += (s, e) => Show(nav, page);
                navPanel.Controls.Add(nav);
            }

            fileLabel = Theme.CreateLabel(string.Empty, Theme.Small, Theme.TextFaint);
            fileLabel.AutoSize = false;
            fileLabel.Dock = DockStyle.Bottom;
            fileLabel.Height = 48;
            fileLabel.Padding = new Padding(22, 6, 12, 0);

            var fileButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 92,
                FlowDirection = FlowDirection.TopDown,
                Padding = new Padding(20, 0, 20, 0),
                BackColor = Theme.Sidebar,
                WrapContents = false,
            };
            fileButtons.Controls.Add(SidebarButton("Open Player List…", OpenRoster));
            fileButtons.Controls.Add(SidebarButton("Save Player List As…", SaveRosterAs));

            sidebar.Controls.Add(navPanel);
            sidebar.Controls.Add(brand);
            sidebar.Controls.Add(fileButtons);
            sidebar.Controls.Add(fileLabel);

            Controls.Add(content);
            Controls.Add(sidebar);

            foreach (var page in pages)
            {
                page.Visible = false;
                content.Controls.Add(page);
            }

            state.RosterChanged += (s, e) => UpdateFileLabel();
            state.SaveFailed += (s, ex) => MessageBox.Show(this,
                "Your changes couldn't be saved to " + state.FilePath + Environment.NewLine + Environment.NewLine + ex.Message,
                "Elo Calculator", MessageBoxButtons.OK, MessageBoxIcon.Warning);

            try
            {
                state.LoadDefault();
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(this, "The default player list couldn't be read: " + (ex.InnerException?.Message ?? ex.Message),
                    "Elo Calculator", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            UpdateFileLabel();
            Show(navs[0], pages[0]);
        }

        private Button SidebarButton(string text, Action action)
        {
            var button = Theme.CreateButton(text);
            button.AutoSize = false;
            button.Size = new Size(180, 34);
            button.Margin = new Padding(0, 0, 0, 8);
            button.Font = Theme.Small;
            button.Click += (s, e) => action();
            return button;
        }

        private void Show(NavButton nav, Page page)
        {
            if (selectedNav != null)
                selectedNav.Selected = false;
            nav.Selected = true;
            selectedNav = nav;

            foreach (Control control in content.Controls)
                control.Visible = ReferenceEquals(control, page);
            page.RefreshView();
        }

        private void UpdateFileLabel()
        {
            var name = Path.GetFileName(state.FilePath);
            fileLabel.Text = File.Exists(state.FilePath) ? "Saving to " + name : "Will save to " + name;
        }

        private void OpenRoster()
        {
            using (var dialog = new OpenFileDialog
            {
                Filter = "Player lists (*.xml;*.txt)|*.xml;*.txt|All files (*.*)|*.*",
                InitialDirectory = Directory.Exists(AppState.DataFolder) ? AppState.DataFolder : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Title = "Open Player List",
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                try
                {
                    state.Open(dialog.FileName);
                }
                catch (Exception ex) when (ex is InvalidOperationException || ex is IOException || ex is UnauthorizedAccessException)
                {
                    MessageBox.Show(this, "That file couldn't be opened as a player list: " + (ex.InnerException?.Message ?? ex.Message),
                        "Elo Calculator", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void SaveRosterAs()
        {
            using (var dialog = new SaveFileDialog
            {
                Filter = "Player lists (*.xml)|*.xml",
                InitialDirectory = AppState.DataFolder,
                FileName = Path.GetFileName(state.FilePath),
                Title = "Save Player List As",
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                try
                {
                    state.SaveAs(dialog.FileName);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    MessageBox.Show(this, "The player list couldn't be saved: " + ex.Message, "Elo Calculator", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }
    }
}
