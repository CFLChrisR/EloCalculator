namespace EloCalculator
{
    partial class mainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.newPlayerButton = new System.Windows.Forms.Button();
            this.ViewCurrentButton = new System.Windows.Forms.Button();
            this.removePlayerButton = new System.Windows.Forms.Button();
            this.nameTextBox = new System.Windows.Forms.TextBox();
            this.nameLabel = new System.Windows.Forms.Label();
            this.AddResultsButton = new System.Windows.Forms.Button();
            this.HomeButton = new System.Windows.Forms.Button();
            this.addPlayerButton = new System.Windows.Forms.Button();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.PlayerListBox = new System.Windows.Forms.ListBox();
            this.PlayerListBox2 = new System.Windows.Forms.ListBox();
            this.battleButton = new System.Windows.Forms.Button();
            this.deleteSinglePlayerButton = new System.Windows.Forms.Button();
            this.deleteAllPlayersButton = new System.Windows.Forms.Button();
            this.loadFileButton = new System.Windows.Forms.Button();
            this.forceSaveButton = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // newPlayerButton
            // 
            this.newPlayerButton.Location = new System.Drawing.Point(212, 197);
            this.newPlayerButton.Name = "newPlayerButton";
            this.newPlayerButton.Size = new System.Drawing.Size(131, 23);
            this.newPlayerButton.TabIndex = 0;
            this.newPlayerButton.Text = "New Player";
            this.newPlayerButton.UseVisualStyleBackColor = true;
            this.newPlayerButton.Click += new System.EventHandler(this.newPlayerButton_Click);
            // 
            // ViewCurrentButton
            // 
            this.ViewCurrentButton.Location = new System.Drawing.Point(420, 197);
            this.ViewCurrentButton.Name = "ViewCurrentButton";
            this.ViewCurrentButton.Size = new System.Drawing.Size(131, 23);
            this.ViewCurrentButton.TabIndex = 1;
            this.ViewCurrentButton.Text = "View Current Players";
            this.ViewCurrentButton.UseVisualStyleBackColor = true;
            this.ViewCurrentButton.Click += new System.EventHandler(this.ViewCurrentButton_Click);
            // 
            // removePlayerButton
            // 
            this.removePlayerButton.Location = new System.Drawing.Point(212, 268);
            this.removePlayerButton.Name = "removePlayerButton";
            this.removePlayerButton.Size = new System.Drawing.Size(131, 23);
            this.removePlayerButton.TabIndex = 2;
            this.removePlayerButton.Text = "Remove Player";
            this.removePlayerButton.UseVisualStyleBackColor = true;
            this.removePlayerButton.Click += new System.EventHandler(this.removePlayerButton_Click);
            // 
            // nameTextBox
            // 
            this.nameTextBox.Location = new System.Drawing.Point(304, 39);
            this.nameTextBox.Name = "nameTextBox";
            this.nameTextBox.Size = new System.Drawing.Size(131, 20);
            this.nameTextBox.TabIndex = 3;
            // 
            // nameLabel
            // 
            this.nameLabel.AutoSize = true;
            this.nameLabel.BackColor = System.Drawing.SystemColors.Control;
            this.nameLabel.Location = new System.Drawing.Point(239, 39);
            this.nameLabel.Name = "nameLabel";
            this.nameLabel.Size = new System.Drawing.Size(35, 13);
            this.nameLabel.TabIndex = 4;
            this.nameLabel.Text = "Name";
            // 
            // AddResultsButton
            // 
            this.AddResultsButton.Location = new System.Drawing.Point(420, 268);
            this.AddResultsButton.Name = "AddResultsButton";
            this.AddResultsButton.Size = new System.Drawing.Size(131, 23);
            this.AddResultsButton.TabIndex = 5;
            this.AddResultsButton.Text = "Add Results";
            this.AddResultsButton.UseVisualStyleBackColor = true;
            this.AddResultsButton.Click += new System.EventHandler(this.AddResultsButton_Click);
            // 
            // HomeButton
            // 
            this.HomeButton.BackColor = System.Drawing.Color.LimeGreen;
            this.HomeButton.ForeColor = System.Drawing.Color.Beige;
            this.HomeButton.Location = new System.Drawing.Point(304, 596);
            this.HomeButton.Name = "HomeButton";
            this.HomeButton.Size = new System.Drawing.Size(131, 23);
            this.HomeButton.TabIndex = 6;
            this.HomeButton.Text = "Home";
            this.HomeButton.UseVisualStyleBackColor = false;
            this.HomeButton.Click += new System.EventHandler(this.HomeButton_Click);
            // 
            // addPlayerButton
            // 
            this.addPlayerButton.Location = new System.Drawing.Point(304, 267);
            this.addPlayerButton.Name = "addPlayerButton";
            this.addPlayerButton.Size = new System.Drawing.Size(131, 23);
            this.addPlayerButton.TabIndex = 7;
            this.addPlayerButton.Text = "Add Player";
            this.addPlayerButton.UseVisualStyleBackColor = true;
            this.addPlayerButton.Click += new System.EventHandler(this.addPlayerButton_Click);
            // 
            // textBox1
            // 
            this.textBox1.Location = new System.Drawing.Point(12, 409);
            this.textBox1.Multiline = true;
            this.textBox1.Name = "textBox1";
            this.textBox1.Size = new System.Drawing.Size(738, 205);
            this.textBox1.TabIndex = 8;
            // 
            // PlayerListBox
            // 
            this.PlayerListBox.FormattingEnabled = true;
            this.PlayerListBox.Location = new System.Drawing.Point(12, 12);
            this.PlayerListBox.Name = "PlayerListBox";
            this.PlayerListBox.Size = new System.Drawing.Size(331, 394);
            this.PlayerListBox.TabIndex = 9;
            this.PlayerListBox.SelectedIndexChanged += new System.EventHandler(this.PlayerListBox_SelectedIndexChanged);
            // 
            // PlayerListBox2
            // 
            this.PlayerListBox2.FormattingEnabled = true;
            this.PlayerListBox2.Location = new System.Drawing.Point(419, 12);
            this.PlayerListBox2.Name = "PlayerListBox2";
            this.PlayerListBox2.Size = new System.Drawing.Size(331, 394);
            this.PlayerListBox2.TabIndex = 10;
            // 
            // battleButton
            // 
            this.battleButton.Location = new System.Drawing.Point(349, 351);
            this.battleButton.Name = "battleButton";
            this.battleButton.Size = new System.Drawing.Size(64, 23);
            this.battleButton.TabIndex = 11;
            this.battleButton.Text = "Battle";
            this.battleButton.UseVisualStyleBackColor = true;
            this.battleButton.Click += new System.EventHandler(this.battleButton_Click);
            // 
            // deleteSinglePlayerButton
            // 
            this.deleteSinglePlayerButton.Location = new System.Drawing.Point(419, 12);
            this.deleteSinglePlayerButton.Name = "deleteSinglePlayerButton";
            this.deleteSinglePlayerButton.Size = new System.Drawing.Size(132, 23);
            this.deleteSinglePlayerButton.TabIndex = 12;
            this.deleteSinglePlayerButton.Text = "Delete Player";
            this.deleteSinglePlayerButton.UseVisualStyleBackColor = true;
            this.deleteSinglePlayerButton.Click += new System.EventHandler(this.deleteSinglePlayerButton_Click);
            // 
            // deleteAllPlayersButton
            // 
            this.deleteAllPlayersButton.BackColor = System.Drawing.Color.Red;
            this.deleteAllPlayersButton.ForeColor = System.Drawing.SystemColors.ControlText;
            this.deleteAllPlayersButton.Location = new System.Drawing.Point(618, 380);
            this.deleteAllPlayersButton.Name = "deleteAllPlayersButton";
            this.deleteAllPlayersButton.Size = new System.Drawing.Size(132, 23);
            this.deleteAllPlayersButton.TabIndex = 13;
            this.deleteAllPlayersButton.Text = "Remove All";
            this.deleteAllPlayersButton.UseVisualStyleBackColor = false;
            this.deleteAllPlayersButton.Click += new System.EventHandler(this.deleteAllPlayersButton_Click);
            // 
            // loadFileButton
            // 
            this.loadFileButton.Location = new System.Drawing.Point(675, 557);
            this.loadFileButton.Name = "loadFileButton";
            this.loadFileButton.Size = new System.Drawing.Size(75, 23);
            this.loadFileButton.TabIndex = 14;
            this.loadFileButton.Text = "Load File";
            this.loadFileButton.UseVisualStyleBackColor = true;
            this.loadFileButton.Click += new System.EventHandler(this.loadFileButton_Click);
            // 
            // forceSaveButton
            // 
            this.forceSaveButton.Location = new System.Drawing.Point(675, 591);
            this.forceSaveButton.Name = "forceSaveButton";
            this.forceSaveButton.Size = new System.Drawing.Size(75, 23);
            this.forceSaveButton.TabIndex = 15;
            this.forceSaveButton.Text = "Force Save";
            this.forceSaveButton.UseVisualStyleBackColor = true;
            this.forceSaveButton.Click += new System.EventHandler(this.button1_Click);
            // 
            // mainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(762, 631);
            this.Controls.Add(this.forceSaveButton);
            this.Controls.Add(this.loadFileButton);
            this.Controls.Add(this.deleteAllPlayersButton);
            this.Controls.Add(this.deleteSinglePlayerButton);
            this.Controls.Add(this.battleButton);
            this.Controls.Add(this.addPlayerButton);
            this.Controls.Add(this.HomeButton);
            this.Controls.Add(this.AddResultsButton);
            this.Controls.Add(this.nameLabel);
            this.Controls.Add(this.nameTextBox);
            this.Controls.Add(this.removePlayerButton);
            this.Controls.Add(this.ViewCurrentButton);
            this.Controls.Add(this.newPlayerButton);
            this.Controls.Add(this.textBox1);
            this.Controls.Add(this.PlayerListBox);
            this.Controls.Add(this.PlayerListBox2);
            this.ForeColor = System.Drawing.SystemColors.ControlText;
            this.Name = "mainForm";
            this.Text = "EloCalculator";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button newPlayerButton;
        private System.Windows.Forms.Button ViewCurrentButton;
        private System.Windows.Forms.Button removePlayerButton;
        private System.Windows.Forms.TextBox nameTextBox;
        private System.Windows.Forms.Label nameLabel;
        private System.Windows.Forms.Button AddResultsButton;
        private System.Windows.Forms.Button HomeButton;
        private System.Windows.Forms.Button addPlayerButton;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.ListBox PlayerListBox;
        private System.Windows.Forms.ListBox PlayerListBox2;
        private System.Windows.Forms.Button battleButton;
        private System.Windows.Forms.Button deleteSinglePlayerButton;
        private System.Windows.Forms.Button deleteAllPlayersButton;
        private System.Windows.Forms.Button loadFileButton;
        private System.Windows.Forms.Button forceSaveButton;
    }
}

