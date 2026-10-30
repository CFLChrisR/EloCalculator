using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace EloCalculator.UI.Pages
{
    internal sealed class LeaderboardPage : Page
    {
        private readonly TextBox nameBox;
        private readonly DataGridView grid;
        private readonly Button removeButton;
        private readonly Button removeAllButton;

        public LeaderboardPage(AppState state) : base(state, "Leaderboard")
        {
            nameBox = Theme.CreateTextBox("New player name");
            nameBox.Width = 280;
            nameBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    AddPlayer();
                }
            };

            var addButton = Theme.CreateButton("Add Player", ButtonKind.Primary);
            addButton.Click += (s, e) => AddPlayer();

            var addRow = Theme.CreateButtonRow();
            addRow.Dock = DockStyle.Top;
            addRow.Margin = new Padding(0);
            addRow.Padding = new Padding(0, 0, 0, 12);
            addRow.Controls.Add(nameBox);
            addRow.Controls.Add(addButton);

            grid = Theme.CreateGrid();
            Theme.AddColumn(grid, "#", 30, DataGridViewContentAlignment.MiddleCenter);
            Theme.AddColumn(grid, "Player", 180);
            Theme.AddColumn(grid, "Rating", 60, DataGridViewContentAlignment.MiddleRight);
            Theme.AddColumn(grid, "Wins", 50, DataGridViewContentAlignment.MiddleRight);
            Theme.AddColumn(grid, "Losses", 50, DataGridViewContentAlignment.MiddleRight);
            Theme.AddColumn(grid, "Games", 50, DataGridViewContentAlignment.MiddleRight);
            Theme.AddColumn(grid, "Win %", 55, DataGridViewContentAlignment.MiddleRight);
            Theme.AddColumn(grid, "Avg. Opponent", 80, DataGridViewContentAlignment.MiddleRight);
            grid.CellFormatting += FormatRankCell;
            grid.SelectionChanged += (s, e) => UpdateButtons();
            grid.VisibleChanged += (s, e) => grid.ClearSelection();

            removeButton = Theme.CreateButton("Remove Selected", ButtonKind.Danger);
            removeButton.Click += (s, e) => RemoveSelected();
            removeAllButton = Theme.CreateButton("Remove All", ButtonKind.Danger);
            removeAllButton.Click += (s, e) => RemoveAll();

            var bottomRow = Theme.CreateButtonRow();
            bottomRow.Dock = DockStyle.Bottom;
            bottomRow.Padding = new Padding(0, 12, 0, 0);
            bottomRow.Controls.Add(removeButton);
            bottomRow.Controls.Add(removeAllButton);

            Body.Controls.Add(grid);
            Body.Controls.Add(addRow);
            Body.Controls.Add(bottomRow);
        }

        public override void RefreshView()
        {
            var standings = State.Roster.Standings.ToList();
            Subtitle = standings.Count == 0
                ? "Add players to get started. Everyone starts at a rating of 1000."
                : $"{standings.Count} players · {State.Roster.History.Count} games recorded";

            grid.Rows.Clear();
            for (var i = 0; i < standings.Count; i++)
            {
                var p = standings[i];
                var row = grid.Rows.Add(i + 1, p.Name, p.Rating, p.Wins, p.Losses, p.GamesPlayed,
                    p.GamesPlayed == 0 ? "–" : p.WinRate.ToString("P0"),
                    p.GamesPlayed == 0 ? "–" : p.AverageOpponentRating.ToString());
                grid.Rows[row].Tag = p.Name;
            }

            grid.ClearSelection();
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            removeButton.Enabled = grid.SelectedRows.Count > 0;
            removeAllButton.Enabled = grid.Rows.Count > 0;
        }

        private void FormatRankCell(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.ColumnIndex != 0 || e.RowIndex > 2 || e.CellStyle == null)
                return;

            e.CellStyle.ForeColor = e.RowIndex == 0 ? Theme.Gold : e.RowIndex == 1 ? Theme.Silver : Theme.Bronze;
            e.CellStyle.Font = Theme.BodyBold;
        }

        private void AddPlayer()
        {
            try
            {
                var player = State.Roster.Add(nameBox.Text);
                nameBox.Clear();
                State.Commit();
                SelectPlayer(player.Name);
            }
            catch (ArgumentException ex)
            {
                ShowError(ex.Message.Split(new[] { " (Parameter" }, StringSplitOptions.None)[0]);
            }

            nameBox.Focus();
        }

        private void SelectPlayer(string name)
        {
            foreach (DataGridViewRow row in grid.Rows)
            {
                if ((string?)row.Tag == name)
                {
                    row.Selected = true;
                    grid.FirstDisplayedScrollingRowIndex = row.Index;
                }
            }
        }

        private void RemoveSelected()
        {
            if (grid.SelectedRows.Count == 0)
                return;

            var name = (string)grid.SelectedRows[0].Tag!;
            if (Confirm($"Remove {name} from the player list? Their recorded games stay in the match history."))
            {
                State.Roster.Remove(name);
                State.Commit();
            }
        }

        private void RemoveAll()
        {
            if (Confirm("Remove every player and clear the match history? This can't be undone."))
            {
                State.Roster.Clear();
                State.Commit();
            }
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible)
                nameBox.Focus();
        }
    }
}
