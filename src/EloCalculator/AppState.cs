using System;
using System.IO;
using EloCalculator.Core.Players;
using EloCalculator.Core.Rating;
using EloCalculator.Core.Storage;

namespace EloCalculator
{
    /// <summary>
    /// The open player list and where it lives on disk. Every change is saved
    /// straight away, so there is no "unsaved changes" state to lose.
    /// </summary>
    internal sealed class AppState
    {
        public static readonly string DataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Player Lists");

        public static readonly string BracketFolder = Path.Combine(DataFolder, "Brackets");

        public static readonly string DefaultRosterPath = Path.Combine(DataFolder, "players.xml");

        public AppState()
        {
            FilePath = DefaultRosterPath;
        }

        public PlayerRoster Roster { get; private set; } = new PlayerRoster();

        public string FilePath { get; private set; }

        public EloRatingCalculator Calculator { get; } = new EloRatingCalculator();

        /// <summary>Raised after the roster changes or a different roster is opened.</summary>
        public event EventHandler? RosterChanged;

        /// <summary>Raised when saving fails; the change is kept in memory.</summary>
        public event EventHandler<Exception>? SaveFailed;

        public void LoadDefault()
        {
            if (File.Exists(DefaultRosterPath))
                Open(DefaultRosterPath);
        }

        public void Open(string path)
        {
            Roster = XmlStore.Load<PlayerRoster>(path);
            FilePath = path;
            RosterChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SaveAs(string path)
        {
            XmlStore.Save(path, Roster);
            FilePath = path;
            RosterChanged?.Invoke(this, EventArgs.Empty);
        }

        public void New(string path)
        {
            Roster = new PlayerRoster();
            FilePath = path;
            Commit();
        }

        /// <summary>Saves the roster and tells listeners it changed.</summary>
        public void Commit()
        {
            try
            {
                XmlStore.Save(FilePath, Roster);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException)
            {
                SaveFailed?.Invoke(this, ex);
            }

            RosterChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
