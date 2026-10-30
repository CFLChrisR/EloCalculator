using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using EloCalculator.Core.Brackets;
using EloCalculator.Core.Storage;
using EloCalculator.UI.Bracket;

namespace EloCalculator.UI.Pages
{
    internal sealed class BracketPage : Page
    {
        private readonly BracketView view;
        private readonly Label statusLabel;
        private readonly Label zoomLabel;
        private readonly Control[] bracketOnlyButtons;
        private Tournament? tournament;
        private string? tournamentPath;

        public BracketPage(AppState state) : base(state, "Bracket")
        {
            Padding = new Padding(32, 24, 32, 16);

            view = new BracketView { Dock = DockStyle.Fill };
            view.MatchClicked += (s, e) => ShowMatchMenu(e.Match, e.Location);
            view.ZoomChanged += (s, e) => zoomLabel!.Text = $"{view.Zoom:P0}";

            var newButton = Theme.CreateButton("＋  New Bracket", ButtonKind.Primary);
            newButton.Click += (s, e) => CreateBracket();
            var openButton = Theme.CreateButton("Open…");
            openButton.Click += (s, e) => OpenBracket();
            var exportButton = Theme.CreateButton("Export Image…");
            exportButton.Click += (s, e) => ExportImage();
            var undoButton = Theme.CreateButton("↶  Undo");
            undoButton.Click += (s, e) => UndoLast();
            var nextButton = Theme.CreateButton("Next Match");
            nextButton.Click += (s, e) => FocusNextMatch();
            var winnersButton = Theme.CreateButton("Winners");
            winnersButton.Click += (s, e) => view.ShowSection(BracketSide.Winners);
            var losersButton = Theme.CreateButton("Losers");
            losersButton.Click += (s, e) => view.ShowSection(BracketSide.Losers);
            var finalsButton = Theme.CreateButton("Finals");
            finalsButton.Click += (s, e) => view.ShowSection(BracketSide.Finals);
            var zoomOutButton = Theme.CreateButton("−");
            zoomOutButton.Click += (s, e) => view.ZoomBy(0.8f);
            zoomLabel = Theme.CreateLabel("100%", Theme.Body, Theme.TextMuted);
            zoomLabel.AutoSize = false;
            zoomLabel.Size = new Size(52, 34);
            zoomLabel.TextAlign = ContentAlignment.MiddleCenter;
            zoomLabel.Margin = new Padding(0, 0, 8, 0);
            var zoomInButton = Theme.CreateButton("+");
            zoomInButton.Click += (s, e) => view.ZoomBy(1.25f);
            var fitButton = Theme.CreateButton("Fit");
            fitButton.Click += (s, e) => view.FitToView();

            var toolbar = Theme.CreateButtonRow();
            toolbar.Dock = DockStyle.Top;
            toolbar.Margin = new Padding(0);
            toolbar.Padding = new Padding(0, 0, 0, 8);
            toolbar.Controls.AddRange(new Control[]
            {
                newButton, openButton, exportButton, Spacer(), undoButton, nextButton, Spacer(),
                winnersButton, losersButton, finalsButton, Spacer(), zoomOutButton, zoomLabel, zoomInButton, fitButton,
            });
            bracketOnlyButtons = new Control[] { exportButton, undoButton, nextButton, winnersButton, losersButton, finalsButton, zoomOutButton, zoomInButton, fitButton };

            statusLabel = Theme.CreateLabel(string.Empty, Theme.Body, Theme.TextMuted);
            statusLabel.Dock = DockStyle.Top;
            statusLabel.AutoSize = false;
            statusLabel.Height = 30;
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;

            var hint = Theme.CreateLabel("Click a match to report the winner  ·  Drag to pan  ·  Ctrl + scroll to zoom  ·  Hover a player to trace their path",
                Theme.Small, Theme.TextFaint);
            hint.Dock = DockStyle.Bottom;
            hint.AutoSize = false;
            hint.Height = 24;
            hint.TextAlign = ContentAlignment.MiddleLeft;

            var frame = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Border, Padding = new Padding(1) };
            frame.Controls.Add(view);

            Body.Padding = new Padding(0, 8, 0, 0);
            Body.Controls.Add(frame);
            Body.Controls.Add(statusLabel);
            Body.Controls.Add(toolbar);
            Body.Controls.Add(hint);
        }

        private static Control Spacer() => new Panel { Width = 1, Height = 34, BackColor = Theme.Border, Margin = new Padding(4, 0, 12, 0) };

        public override void RefreshView()
        {
            foreach (var button in bracketOnlyButtons)
                button.Enabled = tournament != null;

            if (tournament == null)
            {
                Subtitle = "Run a double-elimination tournament: lose twice and you're out.";
                statusLabel.Text = string.Empty;
                return;
            }

            var bracket = tournament.Bracket;
            Subtitle = $"{tournament.Name}  ·  {bracket.Entrants.Count} entrants  ·  {(tournament.IsRated ? "rated" : "unrated")}";

            if (bracket.IsComplete)
            {
                statusLabel.Text = $"🏆  {bracket.Champion!.Name} wins {tournament.Name}!   Runner-up: {bracket.RunnerUp?.Name}";
                statusLabel.ForeColor = Theme.Gold;
            }
            else
            {
                var next = bracket.ReadyMatches.FirstOrDefault();
                statusLabel.Text = $"{tournament.PlayedCount} played  ·  {tournament.RemainingCount} to go" +
                    (next != null ? $"   ·   Next up: {next.Label}  {next.EntrantA!.Name} vs {next.EntrantB!.Name}" : string.Empty);
                statusLabel.ForeColor = Theme.TextMuted;
            }

            view.RefreshBracket();
        }

        private void ShowMatchMenu(BracketMatch match, Point location)
        {
            if (tournament == null)
                return;

            var menu = Theme.CreateContextMenu();
            var title = new ToolStripMenuItem($"{match.Label}  ·  {tournament.Bracket.GetRoundName(match.Side, match.Round)}") { Enabled = false };
            menu.Items.Add(title);
            menu.Items.Add(new ToolStripSeparator());

            switch (match.Status)
            {
                case MatchStatus.Ready:
                    for (var slot = 0; slot < 2; slot++)
                    {
                        var winnerSlot = slot;
                        var entrant = match.GetEntrant(slot)!;
                        var item = new ToolStripMenuItem($"✓  {entrant.Name} wins") { Tag = Theme.Success };
                        item.Click += (s, e) => Report(match, winnerSlot);
                        menu.Items.Add(item);
                    }

                    break;

                case MatchStatus.Completed:
                    menu.Items.Add(new ToolStripMenuItem($"{match.Winner!.Name} beat {match.Loser!.Name}") { Enabled = false });
                    var canClear = tournament.CanClearResult(match.Id);
                    var clear = new ToolStripMenuItem(canClear ? "Clear result" : "Clear result (later matches depend on it)")
                    {
                        Enabled = canClear,
                        Tag = Theme.Danger,
                    };
                    clear.Click += (s, e) => ClearResult(match);
                    menu.Items.Add(clear);
                    break;

                case MatchStatus.Pending:
                    menu.Items.Add(new ToolStripMenuItem("Waiting on earlier matches") { Enabled = false });
                    break;

                case MatchStatus.Bye:
                    menu.Items.Add(new ToolStripMenuItem(match.Winner != null ? $"{match.Winner.Name} advances on a bye" : "Empty match") { Enabled = false });
                    break;

                default:
                    menu.Items.Add(new ToolStripMenuItem("Not needed: the winners-bracket champion won") { Enabled = false });
                    break;
            }

            menu.Closed += (s, e) => BeginInvoke(new Action(menu.Dispose));
            menu.Show(view, location);
        }

        private void Report(BracketMatch match, int winnerSlot)
        {
            tournament!.ReportResult(match.Id, winnerSlot, State.Roster, State.Calculator);
            AfterBracketChange();
        }

        private void ClearResult(BracketMatch match)
        {
            tournament!.ClearResult(match.Id, State.Roster);
            AfterBracketChange();
        }

        private void UndoLast()
        {
            if (tournament?.UndoLastResult(State.Roster) is BracketMatch undone)
            {
                AfterBracketChange();
                view.FocusMatch(undone);
            }
        }

        private void FocusNextMatch()
        {
            var next = tournament?.Bracket.ReadyMatches.FirstOrDefault();
            if (next != null)
                view.FocusMatch(next);
            else if (tournament != null)
                view.ShowSection(BracketSide.Finals);
        }

        /// <summary>Saves the bracket and, for rated tournaments, the updated ratings.</summary>
        private void AfterBracketChange()
        {
            SaveTournament();
            if (tournament!.IsRated)
                State.Commit();
            else
                RefreshView();
        }

        private void SaveTournament()
        {
            if (tournament == null || tournamentPath == null)
                return;

            try
            {
                XmlStore.Save(tournamentPath, tournament.ToFile());
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                ShowError("The bracket couldn't be saved: " + ex.Message);
            }
        }

        private void CreateBracket()
        {
            if (State.Roster.Players.Count < 2)
            {
                ShowError("Add at least two players on the Leaderboard before creating a bracket.");
                return;
            }

            using (var dialog = new NewBracketDialog(State.Roster))
            {
                if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
                    return;

                var created = new Tournament(dialog.TournamentName, dialog.SeededNames, dialog.IsRated);
                var fileName = string.Concat(created.Name.Split(Path.GetInvalidFileNameChars())).Trim();
                tournamentPath = Path.Combine(AppState.BracketFolder, $"{fileName} {DateTime.Now:yyyy-MM-dd HHmm}.xml");
                SetTournament(created);
                SaveTournament();
            }
        }

        private void OpenBracket()
        {
            using (var dialog = new OpenFileDialog
            {
                Filter = "Bracket files (*.xml)|*.xml|All files (*.*)|*.*",
                InitialDirectory = Directory.Exists(AppState.BracketFolder) ? AppState.BracketFolder : AppState.DataFolder,
                Title = "Open Bracket",
            })
            {
                if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
                    return;

                try
                {
                    var loaded = Tournament.FromFile(XmlStore.Load<BracketFile>(dialog.FileName));
                    tournamentPath = dialog.FileName;
                    SetTournament(loaded);
                }
                catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException || ex is IOException)
                {
                    ShowError("That file isn't a bracket this app can open: " + (ex.InnerException?.Message ?? ex.Message));
                }
            }
        }

        private void ExportImage()
        {
            if (tournament == null)
                return;

            using (var dialog = new SaveFileDialog
            {
                Filter = "PNG image (*.png)|*.png",
                FileName = tournament.Name + ".png",
                Title = "Export Bracket Image",
            })
            {
                if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
                    return;

                using (var image = view.RenderImage())
                    image?.Save(dialog.FileName, ImageFormat.Png);
            }
        }

        private void SetTournament(Tournament value)
        {
            tournament = value;
            view.SetBracket(value.Bracket);
            RefreshView();
            view.Focus();
        }
    }
}
