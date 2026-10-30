using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using EloCalculator.Core.Players;
using EloCalculator.Core.Rating;

namespace EloCalculator.UI.Pages
{
    internal sealed class RecordMatchPage : Page
    {
        private readonly ComboBox winnerBox;
        private readonly ComboBox loserBox;
        private readonly Label previewLabel;
        private readonly Label resultLabel;
        private readonly Button recordButton;

        public RecordMatchPage(AppState state) : base(state, "Record Match")
        {
            winnerBox = Theme.CreateComboBox();
            loserBox = Theme.CreateComboBox();
            winnerBox.SelectedIndexChanged += (s, e) => UpdatePreview();
            loserBox.SelectedIndexChanged += (s, e) => UpdatePreview();

            var swapButton = Theme.CreateButton("⇄  Swap");
            swapButton.Anchor = AnchorStyles.Bottom;
            swapButton.Margin = new Padding(12, 0, 12, 0);
            swapButton.Click += (s, e) =>
            {
                var winner = winnerBox.SelectedItem;
                winnerBox.SelectedItem = loserBox.SelectedItem;
                loserBox.SelectedItem = winner;
            };

            var pickers = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = Theme.Background,
                Margin = new Padding(0),
            };
            pickers.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            pickers.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pickers.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            pickers.Controls.Add(CreatePickerCard("WINNER", Theme.Success, winnerBox), 0, 0);
            pickers.Controls.Add(swapButton, 1, 0);
            pickers.Controls.Add(CreatePickerCard("LOSER", Theme.Danger, loserBox), 2, 0);

            previewLabel = Theme.CreateLabel(string.Empty, Theme.Body, Theme.TextMuted);
            previewLabel.Dock = DockStyle.Top;
            previewLabel.AutoSize = false;
            previewLabel.Height = 56;
            previewLabel.Padding = new Padding(2, 16, 0, 0);

            recordButton = Theme.CreateButton("Record Result", ButtonKind.Primary);
            recordButton.Click += (s, e) => Record();
            var buttonRow = Theme.CreateButtonRow();
            buttonRow.Dock = DockStyle.Top;
            buttonRow.Controls.Add(recordButton);

            resultLabel = Theme.CreateLabel(string.Empty, Theme.SectionHeading, Theme.Success);
            resultLabel.Dock = DockStyle.Top;
            resultLabel.AutoSize = false;
            resultLabel.Height = 70;
            resultLabel.Padding = new Padding(2, 18, 0, 0);

            Body.Controls.Add(resultLabel);
            Body.Controls.Add(buttonRow);
            Body.Controls.Add(previewLabel);
            Body.Controls.Add(pickers);
        }

        private static Control CreatePickerCard(string caption, Color accent, ComboBox box)
        {
            var card = new Panel
            {
                Dock = DockStyle.Fill,
                Height = 92,
                BackColor = Theme.Surface,
                Padding = new Padding(16, 14, 16, 14),
                Margin = new Padding(0),
            };
            var label = Theme.CreateLabel(caption, Theme.BodyBold, accent);
            label.Dock = DockStyle.Top;
            box.Dock = DockStyle.Bottom;
            card.Controls.Add(box);
            card.Controls.Add(label);
            return card;
        }

        public override void RefreshView()
        {
            var names = State.Roster.Players.Select(p => p.Name).OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).Cast<object>().ToArray();
            Repopulate(winnerBox, names);
            Repopulate(loserBox, names);
            Subtitle = names.Length < 2
                ? "You need at least two players on the leaderboard to record a match."
                : $"Ratings use the Elo system with a K-factor of {State.Calculator.KFactor}.";
            UpdatePreview();
        }

        private static void Repopulate(ComboBox box, object[] names)
        {
            var previous = box.SelectedItem;
            box.BeginUpdate();
            box.Items.Clear();
            box.Items.AddRange(names);
            box.SelectedItem = previous != null && names.Contains(previous) ? previous : null;
            box.EndUpdate();
        }

        private bool TryGetPlayers(out Player winner, out Player loser)
        {
            winner = loser = null!;
            if (winnerBox.SelectedItem is string w && loserBox.SelectedItem is string l && w != l)
            {
                winner = State.Roster.Find(w)!;
                loser = State.Roster.Find(l)!;
                return winner != null && loser != null;
            }

            return false;
        }

        private void UpdatePreview()
        {
            recordButton.Enabled = TryGetPlayers(out var winner, out var loser);
            if (!recordButton.Enabled)
            {
                previewLabel.Text = winnerBox.SelectedItem != null && winnerBox.SelectedItem == loserBox.SelectedItem
                    ? "Pick two different players."
                    : "Pick the winner and the loser.";
                return;
            }

            var chance = EloRatingCalculator.ExpectedScore(winner.Rating, loser.Rating);
            var change = State.Calculator.RatingChange(winner.Rating, loser.Rating);
            previewLabel.Text =
                $"{winner.Name} was expected to win {chance:P0} of the time.{Environment.NewLine}" +
                $"{winner.Name}: {winner.Rating} → {winner.Rating + change} (+{change})     " +
                $"{loser.Name}: {loser.Rating} → {loser.Rating - change} (−{change})";
        }

        private void Record()
        {
            if (!TryGetPlayers(out var winner, out var loser))
                return;

            var record = State.Roster.RecordMatch(winner, loser, State.Calculator);
            State.Commit();
            resultLabel.Text = $"✓  {record.Winner} beat {record.Loser}  ·  {winner.Rating} (+{record.RatingChange})  /  {loser.Rating} (−{record.RatingChange})";
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible)
                resultLabel.Text = string.Empty;
        }
    }
}
