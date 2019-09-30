﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.IO;

namespace EloCalculator
{
    [Serializable]
    public partial class mainForm : Form
    {
        peopleList newpeeps = new peopleList();
        OpenFileDialog openFileDialog1 = new OpenFileDialog();
        bool loadedFile;
        //battle over time set total of matches, oppopnentmmrs, losses, and wins to 0

        public mainForm()
        {
            InitializeComponent();
            nameLabel.Hide();
            nameTextBox.Hide();
            HomeButton.Hide();
            textBox1.Hide();
            PlayerListBox.Hide();
            PlayerListBox2.Hide();
            addPlayerButton.Hide();
            battleButton.Hide();
            deleteSinglePlayerButton.Hide();
            deleteAllPlayersButton.Hide();
        }

        private void hideComponents()
        {
            newPlayerButton.Hide();
            removePlayerButton.Hide();
            AddResultsButton.Hide();
            ViewCurrentButton.Hide();
            addPlayerButton.Hide();
            PlayerListBox.Hide();
            HomeButton.Show();
        }

        private void newPlayerButton_Click(object sender, EventArgs e)
        {
            hideComponents();
            nameLabel.Show();
            nameTextBox.Show();
            addPlayerButton.Show();
        }

        private void HomeButton_Click(object sender, EventArgs e)
        {
            newPlayerButton.Show();
            removePlayerButton.Show();
            AddResultsButton.Show();
            ViewCurrentButton.Show();
            battleButton.Hide();
            nameLabel.Hide();
            nameTextBox.Hide();
            addPlayerButton.Hide();
            textBox1.Clear();
            textBox1.Hide();
            HomeButton.Hide();
            PlayerListBox.Hide();
            PlayerListBox2.Hide();
            deleteSinglePlayerButton.Hide();
            deleteAllPlayersButton.Hide();
            foreach (BasePlayerClass loadPlayer in newpeeps.peeps)
            {
                PlayerListBox.ClearSelected();
                PlayerListBox2.ClearSelected();
                PlayerListBox2.Items.Clear();
            }
        }

        private void addPlayerButton_Click(object sender, EventArgs e)
        {

            try {

                string pName = nameTextBox.Text;
                if (string.IsNullOrWhiteSpace(nameTextBox.Text)) {
                    textBox1.Show();
                    textBox1.Clear();
                    textBox1.AppendText("You can't enter a blank name.");
                }

                else
                {
                    BasePlayerClass newPlayer = new BasePlayerClass();
                    newPlayer.name = pName;
                    newPlayer.MMR = 1000;
                    newPlayer.gamesplayed = 0;
                    newPlayer.winPercent = 100.00M;
                    newPlayer.varianceFactor = 1.00M;
                    newPlayer.TotalofAllOpponentRatings = 0;
                    newPlayer.BasePlayerWins = 0;
                    newPlayer.BasePlayerLosses = 0;
                    newpeeps.peeps.Add(newPlayer);

                    string dir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    StringBuilder ns = new StringBuilder();
                    ns.Append(dir + "\\Player Lists\\person.txt");
                    XmlSerialization.WriteToXmlFile<peopleList>(ns.ToString(), newpeeps);

                    textBox1.Show();
                    textBox1.AppendText(Environment.NewLine + "File written to location: " + ns);
                    textBox1.AppendText(Environment.NewLine + pName);
                    textBox1.AppendText(Environment.NewLine + newPlayer.MMR.ToString());
                    textBox1.AppendText(Environment.NewLine + newPlayer.gamesplayed.ToString());
                    textBox1.AppendText(Environment.NewLine + newPlayer.varianceFactor.ToString());
                    textBox1.AppendText(Environment.NewLine + newPlayer.winPercent);
                    nameTextBox.Clear();
                }


            }
            catch (NullReferenceException) { }
            }

        private void ViewCurrentButton_Click(object sender, EventArgs e)
        {

            hideComponents();
            textBox1.Show();
            try
            {
                if (newpeeps.peeps.Count < 1)
                {
                    textBox1.Clear();
                    textBox1.Show();
                    textBox1.AppendText("Please load a player list first!");
                }

                else
                {
                    foreach (BasePlayerClass loadPlayer in newpeeps.peeps)
                    {


                        PlayerListBox.Items.Remove(loadPlayer.name);
                        
                        textBox1.AppendText("Name: " + loadPlayer.name.ToString());
                        textBox1.AppendText(Environment.NewLine + "MMR: " + loadPlayer.MMR.ToString());
                        textBox1.AppendText(Environment.NewLine + "Total games played: " + loadPlayer.gamesplayed.ToString());
                        textBox1.AppendText(Environment.NewLine + "Win %  " + loadPlayer.winPercent);
                        textBox1.AppendText(Environment.NewLine);
                        textBox1.AppendText(Environment.NewLine);

                        PlayerListBox.Show();
                        PlayerListBox.ClearSelected();
                        PlayerListBox.Items.Add(loadPlayer.name);

                    }
                }

            }
            catch (ArgumentOutOfRangeException) { }
        }


        private void PlayerListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                textBox1.Clear();
                textBox1.AppendText("Name: " + newpeeps.peeps[PlayerListBox.SelectedIndex].name.ToString());
                textBox1.AppendText(Environment.NewLine + "MMR: " + newpeeps.peeps[PlayerListBox.SelectedIndex].MMR.ToString());
                textBox1.AppendText(Environment.NewLine + "Total games played: " + newpeeps.peeps[PlayerListBox.SelectedIndex].gamesplayed.ToString());
                textBox1.AppendText(Environment.NewLine + "Win Percentage: " + newpeeps.peeps[PlayerListBox.SelectedIndex].winPercent);
            }
            catch (IndexOutOfRangeException) { PlayerListBox.SelectedIndex = 0; }
            catch (ArgumentOutOfRangeException) { PlayerListBox.SelectedIndex = 0; }

        }
        public void AddResultsButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (newpeeps.peeps.Count < 1)
                {
                    hideComponents();
                    textBox1.Clear();
                    textBox1.Show();
                    textBox1.AppendText("Please load a player list first!");
                }

                else
                {
                    hideComponents();
                    textBox1.Clear();
                    battleButton.Show();
                    PlayerListBox.Show();
                    PlayerListBox2.Show();
                    foreach (BasePlayerClass loadPlayer in newpeeps.peeps)
                    {
                        PlayerListBox.Items.Remove(loadPlayer.name);
                        PlayerListBox2.Items.Add(loadPlayer.name);
                        textBox1.AppendText("Name: " + loadPlayer.name.ToString());
                        textBox1.AppendText(Environment.NewLine + "MMR: " + loadPlayer.MMR.ToString());
                        textBox1.AppendText(Environment.NewLine + "Total games played: " + loadPlayer.gamesplayed.ToString());
                        textBox1.AppendText(Environment.NewLine + "Win %  " + loadPlayer.winPercent);
                        textBox1.AppendText(Environment.NewLine);
                        textBox1.AppendText(Environment.NewLine);

                        PlayerListBox.Show();
                        PlayerListBox.Items.Add(loadPlayer.name);
                    }
                }
            }
            catch (ArgumentOutOfRangeException) { }
        }

        private void battleButton_Click(object sender, EventArgs e)
        {
            try
            {
                Battle newBattle = new Battle();
                if (PlayerListBox.SelectedIndex == PlayerListBox2.SelectedIndex)
                {
                    PlayerListBox.ClearSelected();
                    PlayerListBox2.ClearSelected();
                    textBox1.Show();
                    textBox1.Clear();
                    textBox1.AppendText("Players can't be same");

                }
                else
                {
                    newBattle.getResults(newpeeps.peeps[PlayerListBox.SelectedIndex], newpeeps.peeps[PlayerListBox2.SelectedIndex]);

                    string dir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    StringBuilder ns = new StringBuilder();
                    ns.Append(dir + "\\Player Lists\\person.txt");
                    XmlSerialization.WriteToXmlFile<peopleList>(ns.ToString(), newpeeps);

                    textBox1.Clear();
                    textBox1.AppendText(newpeeps.peeps[PlayerListBox.SelectedIndex].name.ToString() + " after " + "a win now has a rating of: " + newpeeps.peeps[PlayerListBox.SelectedIndex].MMR.ToString());
                    textBox1.AppendText(Environment.NewLine);
                    textBox1.AppendText(Environment.NewLine + newpeeps.peeps[PlayerListBox2.SelectedIndex].name.ToString() + " after a loss now has a rating of: " + newpeeps.peeps[PlayerListBox2.SelectedIndex].MMR.ToString());

                    textBox1.Show();
                }

            }
            catch (IndexOutOfRangeException) { PlayerListBox.SelectedIndex = 0; PlayerListBox2.SelectedIndex = 0; }
            catch (ArgumentOutOfRangeException) { PlayerListBox.SelectedIndex = 0; PlayerListBox2.SelectedIndex = 0; }
        }


        private void removePlayerButton_Click(object sender, EventArgs e)
        {
            hideComponents();
            try
            {
                textBox1.Show();
                if (newpeeps.peeps.Count < 1)
                {
                    textBox1.Clear();
                    textBox1.AppendText("Please load a player list first!");
                }

                else
                {
                    deleteSinglePlayerButton.Show();
                    deleteAllPlayersButton.Show();

                    foreach (BasePlayerClass loadPlayer in newpeeps.peeps)
                    {
                        PlayerListBox.Items.Remove(loadPlayer.name);

                        textBox1.AppendText("Name: " + loadPlayer.name.ToString());
                        textBox1.AppendText(Environment.NewLine + "MMR: " + loadPlayer.MMR.ToString());
                        textBox1.AppendText(Environment.NewLine + "Total games played: " + loadPlayer.gamesplayed.ToString());
                        textBox1.AppendText(Environment.NewLine + "Win %  " + loadPlayer.winPercent);
                        textBox1.AppendText(Environment.NewLine);
                        textBox1.AppendText(Environment.NewLine);

                        PlayerListBox.Show();
                        PlayerListBox.ClearSelected();
                        PlayerListBox.Items.Add(loadPlayer.name);
                    }
                }

            }
            catch (ArgumentOutOfRangeException) { }

        }


        private void deleteSinglePlayerButton_Click(object sender, EventArgs e)
        {
            try
            {
                string dir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                StringBuilder ns = new StringBuilder();
                ns.Append(dir + "\\Player Lists\\person.txt");
                textBox1.Clear();
                textBox1.AppendText("Player " + newpeeps.peeps[PlayerListBox.SelectedIndex].name.ToString() + " deleted");
                newpeeps.peeps.RemoveAt(PlayerListBox.SelectedIndex);
                XmlSerialization.WriteToXmlFile<peopleList>(ns.ToString(), newpeeps);
            }
            catch (IndexOutOfRangeException) { PlayerListBox.SelectedIndex = 0; }
        }

        private void deleteAllPlayersButton_Click(object sender, EventArgs e)
        {
            newpeeps.peeps.Clear();
            string dir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            StringBuilder ns = new StringBuilder();
            ns.Append(dir + "\\Player Lists\\person.txt");
            XmlSerialization.WriteToXmlFile<peopleList>(ns.ToString(), newpeeps);
        }
        

        private void loadFileButton_Click(object sender, EventArgs e)
        {
            PlayerListBox.Items.Clear();
            Stream myStream = null;

            openFileDialog1.InitialDirectory = "My Documents";
            openFileDialog1.Filter = "txt files (*.txt)|*.txt|All files (*.*)|*.*";
            openFileDialog1.FilterIndex = 1;
            openFileDialog1.RestoreDirectory = true;

            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    if ((myStream = openFileDialog1.OpenFile()) != null)
                    {
                        using (myStream)
                        {
                            peopleList newpeepsXml = XmlSerialization.ReadFromXmlFile<peopleList>(openFileDialog1.FileName);
                            newpeeps = newpeepsXml;
                            loadedFile = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error: Could not read file from disk. Original error: " + ex.Message);
                }      
        }
        }

        private void button1_Click(object sender, EventArgs e)
        {

        }
    }
}