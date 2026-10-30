using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using EloCalculator.Core.Players;

namespace EloCalculator.UI
{
    /// <summary>Picks and seeds the entrants for a new double-elimination bracket.</summary>
    internal sealed class NewBracketDialog : Form
    {
        private readonly PlayerRoster roster;
        private readonly TextBox nameBox;
        private readonly ThemedListBox availableList;
        private readonly ThemedListBox entrantList;
        private readonly CheckBox ratedBox;
        private readonly Label summaryLabel;
        private readonly Button createButton;
        private readonly List<Player> entrants = new List<Player>();
        private readonly Random random = new Random();

        public NewBracketDialog(PlayerRoster roster)
        {
            this.roster = roster;

            Text = "New Double-Elimination Bracket";
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            Font = Theme.Body;
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(760, 600);
            MinimumSize = new Size(640, 520);
            Padding = new Padding(24);

            nameBox = Theme.CreateTextBox("Tournament name");
            nameBox.Dock = DockStyle.Fill;
            nameBox.Text = $"Tournament {DateTime.Now:MMM d}";

            availableList = new ThemedListBox { Dock = DockStyle.Fill, SelectionMode = SelectionMode.MultiExtended, FormattingEnabled = true };
            availableList.DoubleClick += (s, e) => AddSelected();
            entrantList = new ThemedListBox { Dock = DockStyle.Fill, FormattingEnabled = true };
            entrantList.DoubleClick += (s, e) => RemoveSelected();
            entrantList.Format += (s, e) =>
            {
                var player = (Player)e.ListItem!;
                e.Value = $"{entrants.IndexOf(player) + 1}.  {player.Name}   ({player.Rating})";
            };
            availableList.Format += (s, e) =>
            {
                var player = (Player)e.ListItem!;
                e.Value = $"{player.Name}   ({player.Rating})";
            };

            var moveButtons = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                AutoSize = true,
                Anchor = AnchorStyles.None,
                WrapContents = false,
                BackColor = Theme.Background,
            };
            moveButtons.Controls.Add(MakeButton("Add  →", AddSelected));
            moveButtons.Controls.Add(MakeButton("Add All  ⇉", AddAll));
            moveButtons.Controls.Add(MakeButton("←  Remove", RemoveSelected));
            moveButtons.Controls.Add(MakeButton("Clear", Clear));
            foreach (Control c in moveButtons.Controls)
            {
                c.Margin = new Padding(0, 0, 0, 8);
                c.MinimumSize = new Size(120, 34);
            }

            var seedButtons = Theme.CreateButtonRow();
            seedButtons.Controls.Add(MakeButton("▲  Up", () => MoveEntrant(-1)));
            seedButtons.Controls.Add(MakeButton("▼  Down", () => MoveEntrant(1)));
            seedButtons.Controls.Add(MakeButton("Seed by Rating", SeedByRating));
            seedButtons.Controls.Add(MakeButton("Shuffle", Shuffle));

            var lists = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 3, BackColor = Theme.Background };
            lists.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            lists.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            lists.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            lists.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            lists.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            lists.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            lists.Controls.Add(Theme.CreateLabel("Players", Theme.BodyBold, Theme.TextMuted), 0, 0);
            lists.Controls.Add(Theme.CreateLabel("Entrants in seed order", Theme.BodyBold, Theme.TextMuted), 2, 0);
            lists.Controls.Add(availableList, 0, 1);
            lists.Controls.Add(moveButtons, 1, 1);
            lists.Controls.Add(entrantList, 2, 1);
            lists.Controls.Add(seedButtons, 2, 2);

            ratedBox = new CheckBox
            {
                Text = "Update Elo ratings with bracket results",
                Checked = true,
                AutoSize = true,
                ForeColor = Theme.Text,
                Margin = new Padding(0, 12, 0, 0),
            };

            summaryLabel = Theme.CreateLabel(string.Empty, Theme.Body, Theme.TextMuted);
            summaryLabel.Anchor = AnchorStyles.Left;

            createButton = Theme.CreateButton("Create Bracket", ButtonKind.Primary);
            createButton.Click += (s, e) => Create();
            var cancelButton = Theme.CreateButton("Cancel");
            cancelButton.DialogResult = DialogResult.Cancel;
            CancelButton = cancelButton;
            AcceptButton = createButton;

            var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, AutoSize = true, BackColor = Theme.Background, Margin = new Padding(0, 12, 0, 0) };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footer.Controls.Add(summaryLabel, 0, 0);
            footer.Controls.Add(cancelButton, 1, 0);
            footer.Controls.Add(createButton, 2, 0);
            createButton.Margin = new Padding(0);

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, BackColor = Theme.Background };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(Theme.CreateLabel("Tournament name", Theme.BodyBold, Theme.TextMuted), 0, 0);
            root.Controls.Add(nameBox, 0, 1);
            root.Controls.Add(lists, 0, 2);
            root.Controls.Add(ratedBox, 0, 3);
            root.Controls.Add(footer, 0, 4);
            Controls.Add(root);

            entrants.AddRange(roster.Standings);
            RefreshLists();
        }

        public string TournamentName => nameBox.Text.Trim();

        public bool IsRated => ratedBox.Checked;

        public IReadOnlyList<string> SeededNames => entrants.Select(p => p.Name).ToList();

        private Button MakeButton(string text, Action action)
        {
            var button = Theme.CreateButton(text);
            button.Click += (s, e) => action();
            return button;
        }

        private void RefreshLists(IEnumerable<Player>? selectEntrants = null)
        {
            availableList.BeginUpdate();
            availableList.Items.Clear();
            foreach (var player in roster.Standings.Where(p => !entrants.Contains(p)))
                availableList.Items.Add(player);
            availableList.EndUpdate();

            entrantList.BeginUpdate();
            entrantList.Items.Clear();
            foreach (var player in entrants)
                entrantList.Items.Add(player);
            if (selectEntrants != null)
                foreach (var player in selectEntrants)
                    entrantList.SelectedItem = player;
            entrantList.EndUpdate();

            var count = entrants.Count;
            if (count < 2)
            {
                summaryLabel.Text = "Add at least two entrants.";
            }
            else
            {
                var size = 2;
                while (size < count)
                    size *= 2;
                var byes = size - count;
                summaryLabel.Text = $"{count} entrants · {size}-slot bracket" + (byes > 0 ? $" · {byes} bye{(byes == 1 ? "" : "s")} for the top seeds" : string.Empty);
            }

            createButton.Enabled = count >= 2;
        }

        private void AddSelected()
        {
            entrants.AddRange(availableList.SelectedItems.Cast<Player>());
            RefreshLists();
        }

        private void AddAll()
        {
            entrants.AddRange(availableList.Items.Cast<Player>());
            RefreshLists();
        }

        private void RemoveSelected()
        {
            if (entrantList.SelectedItem is Player player)
            {
                entrants.Remove(player);
                RefreshLists();
            }
        }

        private void Clear()
        {
            entrants.Clear();
            RefreshLists();
        }

        private void MoveEntrant(int direction)
        {
            if (!(entrantList.SelectedItem is Player player))
                return;

            var index = entrants.IndexOf(player);
            var target = index + direction;
            if (target < 0 || target >= entrants.Count)
                return;

            entrants.RemoveAt(index);
            entrants.Insert(target, player);
            RefreshLists(new[] { player });
        }

        private void SeedByRating()
        {
            var ordered = entrants.OrderByDescending(p => p.Rating).ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
            entrants.Clear();
            entrants.AddRange(ordered);
            RefreshLists();
        }

        private void Shuffle()
        {
            for (var i = entrants.Count - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                var swap = entrants[i];
                entrants[i] = entrants[j];
                entrants[j] = swap;
            }

            RefreshLists();
        }

        private void Create()
        {
            if (entrants.Count < 2)
                return;
            if (TournamentName.Length == 0)
                nameBox.Text = "Tournament";

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
