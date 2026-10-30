using System.Linq;
using System.Windows.Forms;

namespace EloCalculator.UI.Pages
{
    internal sealed class HistoryPage : Page
    {
        private readonly DataGridView grid;
        private readonly Button undoButton;

        public HistoryPage(AppState state) : base(state, "Match History")
        {
            grid = Theme.CreateGrid();
            Theme.AddColumn(grid, "Played", 90);
            Theme.AddColumn(grid, "Winner", 110);
            Theme.AddColumn(grid, "Loser", 110);
            Theme.AddColumn(grid, "Change", 50, DataGridViewContentAlignment.MiddleRight);
            Theme.AddColumn(grid, "Ratings Before", 90, DataGridViewContentAlignment.MiddleRight);
            Theme.AddColumn(grid, "Note", 120);
            grid.SelectionChanged += (s, e) => undoButton!.Enabled = grid.SelectedRows.Count > 0;

            undoButton = Theme.CreateButton("Undo Selected Match", ButtonKind.Danger);
            undoButton.Click += (s, e) => UndoSelected();

            var row = Theme.CreateButtonRow();
            row.Dock = DockStyle.Bottom;
            row.Padding = new Padding(0, 12, 0, 0);
            row.Controls.Add(undoButton);

            Body.Controls.Add(grid);
            Body.Controls.Add(row);
        }

        public override void RefreshView()
        {
            var history = State.Roster.History;
            Subtitle = history.Count == 0
                ? "No games recorded yet."
                : $"{history.Count} games, newest first. Undoing a game reverses its rating change.";

            grid.Rows.Clear();
            foreach (var match in history.OrderByDescending(h => h.Id))
            {
                var index = grid.Rows.Add(
                    match.PlayedAt.ToString("g"),
                    match.Winner,
                    match.Loser,
                    "±" + match.RatingChange,
                    $"{match.WinnerRatingBefore} / {match.LoserRatingBefore}",
                    match.Note ?? string.Empty);
                grid.Rows[index].Tag = match.Id;
                grid.Rows[index].Cells[1].Style.ForeColor = Theme.Success;
            }

            grid.ClearSelection();
            undoButton.Enabled = false;
        }

        private void UndoSelected()
        {
            if (grid.SelectedRows.Count == 0)
                return;

            var id = (int)grid.SelectedRows[0].Tag!;
            var match = State.Roster.History.First(h => h.Id == id);
            if (!Confirm($"Undo {match.Winner}'s win over {match.Loser}? Both ratings move back by {match.RatingChange}."))
                return;

            State.Roster.UndoMatch(id);
            State.Commit();
        }
    }
}
