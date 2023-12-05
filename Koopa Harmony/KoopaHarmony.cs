﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using CitraFileLoader;
using KoopaLib;
using NAudio.Wave;
using System.Threading;
using Syroot.BinaryData;
using Softpae.Media;
using CSCore;
using System.Reflection;
using System.Diagnostics;

namespace KoopaHarmony
{
    public partial class KoopaHarmony : Form
    {
        public KoopaHarmony()
        {
            InitializeComponent();
            Thread loopThread = new Thread(loop);
            loopThread.IsBackground = true;
            loopThread.Start();

            //Initialize splitters.
            /*
            for (int i = 0; i < eventSplitters.Count(); i++) {

                Splitter s = eventSplitters[i];
                s = new Splitter();
                s.BorderStyle = BorderStyle.None;
                s.BackColor = Color.Red;
                s.Enabled = false;
                s.SplitPosition = 0;
                eventSplitters[i] = s;
                //if (i > 0) { s.Visible = false; }
                eventPanel.Controls.Add(s);
                List<int> h = new int[64].ToList();

            }
            */

        }

        public bool fileOpen = false; //If a file is open.
        public CISP file; //The file opened. I'm using CISP for simplicity's sake.
        public string fileNamePath = ""; //File name.
        public channelPlayer[] players; //Players.
        public bool playing; //If playing.
        int timer = 0; //Timer.
        public Mixer soundMixer; //Sound mixer.
        public bool scrolling = false; //No scrolling.
        public bool scrollingLeft = false;
        public bool scrollingRight = false;
        public string isabellePath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
        public bool idFileOpen = false; //File ID Open.
        public string idFileNamePath = ""; //Id File Name Path.
        public bbid idFile; //Id File.
        public int koopaAnimationCounter = 0; //0=Idle, 1=Left Swing 2=Cheer 3=Right Swing
        public Image idleImg = Bitmap.FromFile("Data/Image/anim/idle.png");
        public Image leftSwingImg = Bitmap.FromFile("Data/Image/anim/left.png");
        public Image cheerImg = Bitmap.FromFile("Data/Image/anim/cheer.png");
        public Image rightSwingImg = Bitmap.FromFile("Data/Image/anim/right.png");
        public bool fast = false;
        public Splitter[] eventSplitters = new Splitter[64]; //Event splitters.

        //Channel player.
        public struct channelPlayer {
            public byte[] file; //File.
            public WaveOutEvent player; //Player.
            public CSCore.WaveFormat player2;
            public IWaveProvider playerFile; //Audio File.
            public ISampleSource playerFile2;
            public CSCore.IWaveSource source; //Source.
            public CSCore.SoundOut.WasapiOut soundOut; //Sound out.
        }


        //New file.
        #region newFile

        /// <summary>
        /// Make a new file.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void newToolStripMenuItem_Click(object sender, EventArgs e)
        {

            int j = 0;
            if (idFileOpen)
            {
                SaveCloseDialog c = new SaveCloseDialog();
                j = c.getValue();
                if (j == 0) { save(); }
            }

            if (j == 0 || j == 1)
            {
                projectPanel.Hide();
                channelPanel.Hide();
                trackPanel.Hide();
                noInfoPanel.Show();

                //General stuff.
                idFileNamePath = "";
                idFileOpen = true;
                this.Text = "Koopa Harmony - New Beat.bbid";

                //Make new BBID.
                idFile = new bbid();
                idFile.actionNumbers = new UInt32[64];
                idFile.danceMoves = new List<byte>();
                idFile.padding = 0;
                idFile.count = 0;
                idFile.sampleNumbers = new List<UInt32>();
                idFile.tempo = 0x64;
                idFile.unknown = 4;

                idFileOpen = true;
                updateNodes();
            }

        }
        #endregion



        //Info stuff.
        #region infoStuff
        public void doInfoStuff() {

            if (tree.SelectedNode.Parent != null && idFileOpen)
            {

                //Channel.
                if (tree.SelectedNode.Parent.Index == 1)
                {

                    trackPanel.Hide();
                    noInfoPanel.Hide();
                    projectPanel.Hide();
                    channelPanel.Show();

                    sampleIdBox.Value = idFile.sampleNumbers[tree.SelectedNode.Index];
                    danceBox.SelectedIndex = idFile.danceMoves[tree.SelectedNode.Index] - 1;
                    if (fileOpen)
                    {
                        if (sampleIdBox.Value > file.stream.loopEnd) { afterLoopBox.Checked = true; sampleIdBox.Value = sampleIdBox.Value - file.stream.loopEnd + file.stream.loopStart; } else { afterLoopBox.Checked = false; }
                    }
                    selectedEvent.Text = "Selected Event: Event " + (tree.SelectedNode.Index+1);

                }

            }
            else if (idFileOpen) {

                //Sound Project Info.
                if (tree.SelectedNode.Index == 0)
                {
                    trackPanel.Hide();
                    noInfoPanel.Hide();
                    projectPanel.Show();
                    channelPanel.Hide();

                    projectPanel.Enabled = true;
                    unknownValueBox.Value = idFile.unknown;
                    tempoBox.Value = idFile.tempo;
                    selectedEvent.Text =  "Selected Event: NULL (Select an event first!)";

                }
                else {

                    noInfoPanel.Show();
                    projectPanel.Hide();
                    trackPanel.Hide();
                    channelPanel.Hide();
                    selectedEvent.Text = "Selected Event: NULL (Select an event first!)";

                }

            }

        }

        #region trackStuff
        public void volumeTrack(object sender, EventArgs e) {
            CISP.trackInfo t = file.tracks[tree.SelectedNode.Index];
            t.volume = (byte)volumeBox.Value;
            file.tracks[tree.SelectedNode.Index] = t;
        }

        public void panTrack(object sender, EventArgs e)
        {
            CISP.trackInfo t = file.tracks[tree.SelectedNode.Index];
            t.pan = (byte)panBox.Value;
            file.tracks[tree.SelectedNode.Index] = t;
        }

        public void flagTrack(object sender, EventArgs e)
        {
            CISP.trackInfo t = file.tracks[tree.SelectedNode.Index];
            if (flagBox.Checked) { t.flags = 1; } else { t.flags = 0; }
            file.tracks[tree.SelectedNode.Index] = t;
        }

        public void channelTrack(object sender, EventArgs e)
        {
            try
            {
                CISP.trackInfo t = file.tracks[tree.SelectedNode.Index];
                string channelString = channelTextBox.Text;
                t.channels = new List<byte>();
                if (channelString.Length > 0)
                {
                    t.channelCount = (UInt32)channelString.Length / 2 + 1;
                    for (int i = 0; i < t.channelCount; i += 1) {
                        t.channels.Add((byte)(byte.Parse(new string(channelString[i*2], 1))-1));
                    }
                }
                file.tracks[tree.SelectedNode.Index] = t;
            }
            catch {
                MessageBox.Show("Enter like this: 1;2");
            }
        }
        #endregion

        private void updateProjectInfoButton_Click(object sender, EventArgs e)
        {

            idFile.tempo = (byte)tempoBox.Value;
            idFile.unknown = (byte)unknownValueBox.Value;

        }

        #endregion



        //Update nodes.
        #region updateNodes
        public void updateNodes() {

            //Start stuff.
            tree.BeginUpdate();

            tree.SelectedNode = tree.Nodes[0];
            trackPanel.Hide();

            List<string> expandedNodes = collectExpandedNodes(tree.Nodes);

            foreach (TreeNode e in tree.Nodes[1].Nodes)
            {
                tree.Nodes[1].Nodes.RemoveAt(0);
            }
            

            //Only if file is open.
            if (idFileOpen)
            {
                
                for (int i = 0; i < idFile.sampleNumbers.Count; i++) {
                    tree.Nodes[1].Nodes.Add("Event " + (i+1), "Event " + (i+1), 2, 2);
                    tree.Nodes[1].Nodes[i].ContextMenuStrip = nodeMenu;
                }
                tree.Nodes[1].ContextMenuStrip = rootMenu;

                //Show events.
                showEvents();

            }
            else {
                tree.Nodes[1].ContextMenuStrip = null;

                /*
                eventSplitters[0].Visible = true;
                eventSplitters[0].SplitPosition = 0;
                for (int i = 1; i < eventSplitters.Count(); i++) {
                    eventSplitters[i].Visible = false;
                    eventSplitters[i].SplitPosition = 0;
                }*/

            }

            //Restore the nodes if they exist.
            if (expandedNodes.Count > 0)
            {
                TreeNode IamExpandedNode;
                for (int i = 0; i < expandedNodes.Count; i++)
                {
                    IamExpandedNode = FindNodeByName(tree.Nodes, expandedNodes[i]);
                    expandNodePath(IamExpandedNode);
                }

            }

            tree.EndUpdate();

        }
        #endregion

        public void showEvents() {

            eventPanel.Refresh();

        }

        //Node shit.
        #region nodeShit

        //Expand node and parents.
        void expandNodePath(TreeNode node)
        {
            if (node == null)
                return;
            if (node.Level != 0) //check if it is not root
            {
                node.Expand();
                expandNodePath(node.Parent);
            }
            else
            {
                node.Expand(); // this is root 
            }



        }

        //Make right click actually select, and show infoViewer.
        void tree_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                // Select the clicked node
                tree.SelectedNode = tree.GetNodeAt(e.X, e.Y);
            }
            else if (e.Button == MouseButtons.Left)
            {
                // Select the clicked node
                tree.SelectedNode = tree.GetNodeAt(e.X, e.Y);
            }

            doInfoStuff();

        }

        void tree_NodeKey(object sender, KeyEventArgs e)
        {

            doInfoStuff();

        }

        //Get expanded nodes.
        List<string> collectExpandedNodes(TreeNodeCollection Nodes)
        {
            List<string> _lst = new List<string>();
            foreach (TreeNode checknode in Nodes)
            {
                if (checknode.IsExpanded)
                    _lst.Add(checknode.Name);
                if (checknode.Nodes.Count > 0)
                    _lst.AddRange(collectExpandedNodes(checknode.Nodes));
            }
            return _lst;
        }


        /// <summary>
        /// Find nodes by name.
        /// </summary>
        /// <param name="NodesCollection"></param>
        /// <param name="Name"></param>
        /// <returns></returns>
        TreeNode FindNodeByName(TreeNodeCollection NodesCollection, string Name)
        {
            TreeNode returnNode = null; // Default value to return
            foreach (TreeNode checkNode in NodesCollection)
            {
                if (checkNode.Name == Name)  //checks if this node name is correct
                    returnNode = checkNode;
                else if (checkNode.Nodes.Count > 0) //node has child
                {
                    returnNode = FindNodeByName(checkNode.Nodes, Name);
                }

                if (returnNode != null) //check if founded do not continue and break
                {
                    return returnNode;
                }

            }
            //not found
            return returnNode;
        }

        #endregion



        //Root nodes. ADDING NEW CHANNELS INCOMPLETE!!!
        #region rootNodes

        private void add_Click(object sender, EventArgs e)
        {
            if (idFile.sampleNumbers.Count == 64)
            {
                MessageBox.Show("You can only have 64 events per song!");
            }
            else {
                idFile.sampleNumbers.Add(0);
                idFile.danceMoves.Add(2);
                updateNodes();
            }
        }

        private void expand_Click(object sender, EventArgs e)
        {
            tree.SelectedNode.Expand();
        }

        private void collapse_Click(object sender, EventArgs e)
        {
            tree.SelectedNode.Collapse();
        }

        #endregion



        //Child nodes. INCOMPLETE!!!
        #region childNodes

        private void moveUpToolStripMenuItem_Click(object sender, EventArgs e)
        {

            if (tree.SelectedNode.Index != 0)
            {
                UInt32 temp1 = idFile.sampleNumbers[tree.SelectedNode.Index];
                byte temp12 = idFile.danceMoves[tree.SelectedNode.Index];

                UInt32 temp2 = idFile.sampleNumbers[tree.SelectedNode.Index-1];
                byte temp22 = idFile.danceMoves[tree.SelectedNode.Index-1];

                idFile.sampleNumbers[tree.SelectedNode.Index] = temp2;
                idFile.sampleNumbers[tree.SelectedNode.Index-1] = temp1;
                idFile.danceMoves[tree.SelectedNode.Index] = temp22;
                idFile.danceMoves[tree.SelectedNode.Index - 1] = temp12;
                updateNodes();
            }

        }

        private void moveDownItem_Click(object sender, EventArgs e)
        {
            if (tree.SelectedNode.Index != idFile.sampleNumbers.Count - 1 && idFile.sampleNumbers.Count > 1)
            {
                UInt32 temp1 = idFile.sampleNumbers[tree.SelectedNode.Index];
                byte temp12 = idFile.danceMoves[tree.SelectedNode.Index];

                UInt32 temp2 = idFile.sampleNumbers[tree.SelectedNode.Index + 1];
                byte temp22 = idFile.danceMoves[tree.SelectedNode.Index + 1];

                idFile.sampleNumbers[tree.SelectedNode.Index] = temp2;
                idFile.sampleNumbers[tree.SelectedNode.Index + 1] = temp1;
                idFile.danceMoves[tree.SelectedNode.Index] = temp22;
                idFile.danceMoves[tree.SelectedNode.Index + 1] = temp12;
                updateNodes();
            }
        }

        private void replaceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            
        }

        private void deleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            idFile.sampleNumbers.RemoveAt(tree.SelectedNode.Index);
            idFile.danceMoves.RemoveAt(tree.SelectedNode.Index);
            updateNodes();
        }

        #endregion


        //Buttons that do small things.
        #region otherButtons

        /// <summary>
        /// Convert wav to game wave.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void simpleWaveToGameWaveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            b_wav b = new b_wav();
            waveSelectorBox.ShowDialog();
            if (waveSelectorBox.FileName != "")
            {
                RIFF r = new RIFF();
                r.load(File.ReadAllBytes(waveSelectorBox.FileName));
                waveSelectorBox.FileName = "";
                b = r.toGameWav();


                saveGameWaveBox.ShowDialog();
                if (saveGameWaveBox.FileName != "")
                {

                    if (saveGameWaveBox.FilterIndex == 1)
                    {

                        File.WriteAllBytes(saveGameWaveBox.FileName, b.toBytes(endianNess.big));

                    }
                    else
                    {

                        File.WriteAllBytes(saveGameWaveBox.FileName, b.toBytes(endianNess.little));

                    }

                    saveGameWaveBox.FileName = "";

                }
            }

        }



        /// <summary>
        /// Convert game file to wave.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void simpleGameFileToWaveToolStripMenuItem_Click(object sender, EventArgs e)
        {

            gameFileSelectorBox.ShowDialog();
            if (gameFileSelectorBox.FileName != "") {

                if (gameFileSelectorBox.FilterIndex == 1)
                {

                    //Have user select output.
                    saveWaveBox.ShowDialog();
                    if (saveWaveBox.FileName != "") {

                        //Do actual conversions.
                        b_wav b = new b_wav();
                        b.load(File.ReadAllBytes(gameFileSelectorBox.FileName));
                        File.WriteAllBytes(saveWaveBox.FileName, b.toRiff().toBytes());

                    }

                }
                else
                {

                    //Have user select output.
                    saveWaveBox.ShowDialog();
                    if (saveWaveBox.FileName != "")
                    {

                        //Do actual conversions.
                        b_stm b = new b_stm();
                        b.load(File.ReadAllBytes(gameFileSelectorBox.FileName));
                        File.WriteAllBytes(saveWaveBox.FileName, b.toRiff().toBytes());

                    }

                }

                gameFileSelectorBox.FileName = "";
                saveWaveBox.FileName = "";

            }

        }



        /// <summary>
        /// About menu.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void aboutIsabelleSoundEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            KoopaAbout a = new KoopaAbout();
            a.Show();
        }



        #endregion



        //Channel player.
        #region channelPlayer
        private void playButtonChannel_Click(object sender, EventArgs e)
        {

            for (int i = 0; i < players.Length; i++)
            {
                if (players[i].player.PlaybackState == PlaybackState.Playing)
                {
                    players[i].player.Stop();

                    players[i].playerFile = new RawSourceWaveStream(new MemoryStream(players[i].file), new NAudio.Wave.WaveFormat((int)file.stream.sampleRate, 1));
                    players[i].player.Init(players[i].playerFile);
                }

            }

            foreach (channelPlayer p in players)
            {
                if (p.player != null)
                {
                    p.player.Volume = (float)((float)volume.Value / (float)100);
                }
            }

            players[tree.SelectedNode.Index].player.Play();

            playPauseButton.Image = new Bitmap("Data/Image/play4.png");
            playing = false;

        }

        private void pauseButtonChannel_Click(object sender, EventArgs e)
        {
            players[tree.SelectedNode.Index].player.Pause();
            playPauseButton.Image = new Bitmap("Data/Image/play4.png");
            playing = false;
        }

        private void stopButtonChannel_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < players.Length; i++)
            {
                players[i].player.Stop();

                players[i].playerFile = new RawSourceWaveStream(new MemoryStream(players[i].file), new NAudio.Wave.WaveFormat((int)file.stream.sampleRate, 1));
                players[i].player.Init(players[i].playerFile);

            }
            playPauseButton.Image = new Bitmap("Data/Image/play4.png");
            playing = false;
        }


        #endregion



        //Saving
        #region saving

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (idFileOpen) save();
        }

        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (idFileOpen) saveAs();
        }

        public void save() {
            if (idFileNamePath == "")
            {
                saveAs();
            }
            else {
                File.WriteAllBytes(idFileNamePath, idFile.toBytes());
            }
        }

        public void saveAs() {
            saveID.ShowDialog();
            if (saveID.FileName != "") {
                idFileNamePath = saveID.FileName;
                saveID.FileName = "";
                this.Text = "Koopa Harmony - " + Path.GetFileName(idFileNamePath);
                save();
            }
        }

        #endregion



        //Opening
        #region opening

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {

            int j = 0;
            if (idFileOpen)
            {
                SaveCloseDialog c = new SaveCloseDialog();
                j = c.getValue();
                if (j == 0) { save(); }
            }

            if (j == 0 || j == 1)
            {

                openID.ShowDialog();
                if (openID.FileName != "")
                {

                    projectPanel.Hide();
                    channelPanel.Hide();
                    trackPanel.Hide();
                    noInfoPanel.Show();

                    idFile = new bbid();
                    idFile.load(File.ReadAllBytes(openID.FileName));

                    idFileOpen = true;
                    idFileNamePath = openID.FileName;
                    this.Text = "Koopa Harmony - " + Path.GetFileName(idFileNamePath);
                    updateNodes();
                    openID.FileName = "";

                }

            }

        }

        #endregion



        //Track Player.
        #region trackPlayer

        private void playSoundTrack_Click(object sender, EventArgs e)
        {

            try
            {

                for (int i = 0; i < players.Length; i++)
                {
                    if (players[i].player.PlaybackState == PlaybackState.Playing)
                    {
                        players[i].player.Stop();

                        players[i].playerFile = new RawSourceWaveStream(new MemoryStream(players[i].file), new NAudio.Wave.WaveFormat((int)file.stream.sampleRate, 1));
                        players[i].player.Init(players[i].playerFile);
                    }
                }

                foreach (channelPlayer p in players)
                {
                    if (p.player != null)
                    {
                        p.player.Volume = (float)volume.Value / (float)100;
                    }
                }

                foreach (byte b in file.tracks[tree.SelectedNode.Index].channels)
                {
                    players[(int)b].player.Play();
                }

            }
            catch {
                MessageBox.Show("You can't play channels that don't exist! :p");
            }

            playPauseButton.Image = new Bitmap("Data/Image/play4.png");
            playing = false;
        }

        private void pauseSoundTrack_Click(object sender, EventArgs e)
        {
            foreach (byte b in file.tracks[tree.SelectedNode.Index].channels)
            {
                players[(int)b].player.Pause();
            }
            playPauseButton.Image = new Bitmap("Data/Image/play4.png");
            playing = false;
        }

        private void stopSoundTrack_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < players.Length; i++)
            {
                players[i].player.Stop();

                players[i].playerFile = new RawSourceWaveStream(new MemoryStream(players[i].file), new NAudio.Wave.WaveFormat((int)file.stream.sampleRate, 1));
                players[i].player.Init(players[i].playerFile);

            }
            playPauseButton.Image = new Bitmap("Data/Image/play4.png");
            playing = false;
        }

        #endregion



        //Load channel files.
        #region loadChannelFiles
        public void loadChannelFiles() {

            if (players != null)
            {
                for (int i = 0; i < players.Count(); i++)
                {
                    try { players[i].source.Dispose(); } catch { }
                    try { players[i].soundOut.Dispose(); } catch { }
                }
            }

            players = new channelPlayer[file.channelData.Count];
            for (int i = 0; i < file.channelData.Count; i++) {

                //Write the RIFF.
                MemoryStream channelData = new MemoryStream();
                BinaryDataWriter bw = new BinaryDataWriter(channelData);
                bw.Write(file.channelData[i]);

                players[i].file = channelData.ToArray();
                players[i].player = new WaveOutEvent();
                players[i].playerFile = new RawSourceWaveStream(new MemoryStream(channelData.ToArray()), new NAudio.Wave.WaveFormat((int)file.stream.sampleRate, 1));
                players[i].player.Init(players[i].playerFile);

                //CSCore.WaveFormat wf = new CSCore.WaveFormat((int)file.stream.sampleRate, 16, 1, AudioEncoding.Pcm);

                try { players[i].source.Dispose(); } catch { }
                players[i].source = null;

                RIFF r = new RIFF();
                r.fmt = new RIFF.fmtBlock();
                r.fmt.bitsPerSample = 16;
                r.fmt.sampleRate = file.stream.sampleRate;
                r.fmt.numChannels = 1;
                r.fmt.chunkFormat = 1;
                r.fmt.restOfData = new byte[0];
                r.data = new RIFF.dataBlock();
                r.data.data = players[i].file;
                r.fixOffsets();
                Directory.CreateDirectory("Data/TEMP");
                File.WriteAllBytes("Data/TEMP/tmp" + i + ".wav", r.toBytes());
                players[i].source = CSCore.Codecs.CodecFactory.Instance.GetCodec("Data/TEMP/tmp" + i + ".wav");
                File.Delete("tmp" + i + ".wav");

            }

        }
        #endregion



        //Volume Stuff.
        #region volumeStuff 

        private void volume_Scroll(object sender, EventArgs e)
        {
            try {
                foreach (channelPlayer p in players)
                {
                    if (p.player != null)
                    {
                        p.player.Volume = (float)volume.Value / (float)100;
                    }
                }
            } catch { }
        }

        #endregion



        //Main Player.
        #region mainPlayer

        //Play pause.
        private void playPauseButton_Click(object sender, EventArgs e)
        {
            if (playing)
            {

                foreach (channelPlayer p in players)
                {
                    if (p.soundOut != null)
                    {
                        p.soundOut.Pause();
                    }
                }

                playPauseButton.Image = new Bitmap("Data/Image/play4.png");
                playing = false;

            }
            else
            {

                for (int i = 0; i < players.Length; i++)
                {

                    if (players[i].soundOut == null) {

                        players[i].soundOut = new CSCore.SoundOut.WasapiOut();
                        players[i].soundOut.Initialize(players[i].source);

                    }

                    else if (players[i].soundOut.PlaybackState == CSCore.SoundOut.PlaybackState.Playing)
                    {
                        try { players[i].soundOut.Stop(); } catch { }
                        try { players[i].soundOut.Dispose(); } catch { }

                        //players[i].playerFile = new RawSourceWaveStream(new MemoryStream(players[i].file), new NAudio.Wave.WaveFormat((int)file.stream.sampleRate, 1));
                        players[i].soundOut = new CSCore.SoundOut.WasapiOut();
                        players[i].soundOut.Initialize(players[i].source);
                    }
                }

                foreach (channelPlayer p in players)
                {
                    if (p.soundOut != null)
                    {
                        p.soundOut.Volume = (float)volume.Value / (float)100;
                    }
                }

                for (int i = 0; i < file.channelData.Count; i++)
                {
                    players[i].soundOut.Play();
                }

                playPauseButton.Image = new Bitmap("Data/Image/pause.png");
                playing = true;

            }
        }


        //Stop.
        private void stopButton_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < players.Length; i++)
            {
                try { players[i].soundOut.Stop(); } catch { }
                try { players[i].soundOut.Dispose(); } catch { }
                players[i].soundOut = null;
                players[i].source.Position = 0;

            }

            playPauseButton.Image = new Bitmap("Data/Image/play4.png");
            playing = false;

        }

        #endregion



        //Loop
        public void loop() {

            while (true) {

                if (!scrolling) try { timeBar.Value = (int)(((decimal)players[0].source.Position / ((decimal)file.channelData[0].Length * 2)) * 1440); if (playLikeGameBox.Checked) { if (players[0].source.Position >= file.stream.loopEnd*2 && players[0].soundOut.PlaybackState == CSCore.SoundOut.PlaybackState.Playing) { for (int i = 0; i < players.Count(); i++) { players[i].source.Position = (long)file.stream.loopStart*2; } } } } catch { }
                
                try
                {
                    if (!scrollingLeft) spltStart.SplitPosition = (int)((decimal)file.stream.loopStart / (decimal)file.channelData[0].Length * (decimal)pnlLoop.Size.Width);
                    if (!scrollingRight) spltEnd.SplitPosition = pnlLoop.Size.Width - (int)((decimal)file.stream.loopEnd / (decimal)file.channelData[0].Length * (decimal)pnlLoop.Size.Width);
                }
                catch { }

                try { if (file.channelData.Count() == 0) { playPauseButton.Enabled = false; stopButton.Enabled = false; timeBar.Enabled = false; spltStart.Enabled = false; spltEnd.Enabled = false; } else { playPauseButton.Enabled = true; stopButton.Enabled = true; timeBar.Enabled = true; } } catch { }

                //Current sample.
                if (fileOpen && players != null) {
                    if (players[0].source != null)
                    {
                        currentSample.Text = "Current Sample: " + players[0].source.Position / 2;
                        gotoSampleBox.Enabled = true;
                        gotoSampleButton.Enabled = true;
                        gotoSampleBox.Maximum = file.stream.loopEnd;

                        try
                        {

                            if (idFile != null)
                            {
                                newEventButton.Enabled = true;
                                setEventButton.Enabled = true;
                                jumpToSampleButton.Enabled = true;
                                afterLoopBox.Enabled = true;
                                for (int i = idFile.sampleNumbers.Count - 1; i >= 0; i--)
                                {

                                    long pos = players[0].source.Position / 2;
                                    if (pos > file.stream.loopEnd) { pos = pos - file.stream.loopEnd + file.stream.loopStart; }
                                    UInt32 sam = idFile.sampleNumbers[i];
                                    if (sam > file.stream.loopEnd) { sam = sam - file.stream.loopEnd + file.stream.loopStart; }
                                    if ((UInt32)pos >= sam - file.stream.sampleRate / 50 && (UInt32)pos <= sam + file.stream.sampleRate / 50)
                                    {
                                        switch (idFile.danceMoves[i])
                                        {

                                            case 1:
                                                leftSwingBox.BringToFront();
                                                koopaAnimationCounter = 0;
                                                fast = false;
                                                break;
                                            case 4:
                                                fast = true;
                                                cheerBox.BringToFront();
                                                koopaAnimationCounter = 0;
                                                break;
                                            case 2:
                                                cheerBox.BringToFront();
                                                koopaAnimationCounter = 0;
                                                fast = false;
                                                break;
                                            case 3:
                                                rightSwingBox.BringToFront();
                                                koopaAnimationCounter = 0;
                                                fast = false;
                                                break;

                                        }
                                    }
                                    else
                                    {
                                        koopaAnimationCounter += 1;

                                        if (fast)
                                        {
                                            if (idFile.tempo >= 100)
                                            {
                                                if (koopaAnimationCounter > (file.stream.sampleRate * (decimal)(10 + (decimal)(100 - idFile.tempo) / (decimal)6.5)) / (decimal)1.7)
                                                {
                                                    idleBox.BringToFront();
                                                }
                                            }
                                            else
                                            {
                                                if (koopaAnimationCounter > (file.stream.sampleRate * (decimal)(3) * (decimal)(1 - (100 - idFile.tempo) * .01)) / (decimal)1.7)
                                                {
                                                    idleBox.BringToFront();
                                                }
                                            }
                                        }
                                        else
                                        {

                                            if (idFile.tempo >= 100)
                                            {
                                                if (koopaAnimationCounter > file.stream.sampleRate * (decimal)(10 + (decimal)(100 - idFile.tempo) / (decimal)6.5))
                                                {
                                                    idleBox.BringToFront();
                                                }
                                            }
                                            else
                                            {
                                                if (koopaAnimationCounter > file.stream.sampleRate * (decimal)(3) * (decimal)(1 - (100 - idFile.tempo) * .01))
                                                {
                                                    idleBox.BringToFront();
                                                }
                                            }
                                        }
                                    }

                                }
                            }
                            else
                            {
                                newEventButton.Enabled = false;
                                setEventButton.Enabled = false;
                                jumpToSampleButton.Enabled = false;
                                afterLoopBox.Enabled = false;
                            }

                        }
                        catch { }

                    }
                    else {
                        gotoSampleBox.Enabled = false;
                        gotoSampleButton.Enabled = false;
                        gotoSampleBox.Maximum = 100;
                    }
                }
                else
                {
                    currentSample.Text = "Current Sample: NULL (Import a sound file first!)";
                    gotoSampleBox.Enabled = false;
                    gotoSampleButton.Enabled = false;
                    newEventButton.Enabled = false;
                    setEventButton.Enabled = false;
                    jumpToSampleButton.Enabled = false;
                    afterLoopBox.Enabled = false;
                }

            }

        }



        //Loop buttons.
        #region loopButtons

        private void setLoopStartButton_Click(object sender, EventArgs e)
        {
            file.stream.loop = 1;
            file.stream.loopStart = (UInt32)(players[0].source.Position/2);
            doInfoStuff();

            spltStart.SplitPosition = (int)((decimal)file.stream.loopStart / (decimal)file.channelData[0].Length * (decimal)pnlLoop.Size.Width);

            if (file.stream.loopStart >= file.channelData[0].Length) { file.stream.loopStart = (UInt32)file.channelData[0].Length; }

        }

        private void setLoopEndButton_Click(object sender, EventArgs e)
        {
            file.stream.loop = 1;
            file.stream.loopEnd = (UInt32)(players[0].source.Position / 2);
            doInfoStuff();

            spltEnd.SplitPosition = pnlLoop.Size.Width - (int)((decimal)file.stream.loopEnd / (decimal)file.channelData[0].Length * (decimal)pnlLoop.Size.Width);

            if (file.stream.loopEnd >= file.channelData[0].Length) { file.stream.loopEnd = (UInt32)file.channelData[0].Length; }

        }








        #endregion



        //To RIFF.
        private void exportWavToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (file.channelData.Count > 0 && fileOpen)
            {
                saveWaveBox.ShowDialog();
                if (saveWaveBox.FileName != "")
                {

                    File.WriteAllBytes(saveWaveBox.FileName, file.toRIFF().toBytes());
                    saveWaveBox.FileName = "";

                }
            }
            else {

                MessageBox.Show("You need at least one channel to save first!");

            }
        }



        //Import file.
        private void importFileToolStripMenuItem_Click(object sender, EventArgs e)
        {

            int j = 0;
            if (j == 0 || j == 1) {

                try
                {

                    for (int i = 0; i < players.Length; i++)
                    {
                        players[i].player.Stop();

                        players[i].playerFile = new RawSourceWaveStream(new MemoryStream(players[i].file), new NAudio.Wave.WaveFormat((int)file.stream.sampleRate, 1));
                        players[i].player.Init(players[i].playerFile);

                    }



                    playPauseButton.Image = new Bitmap("Data/Image/play4.png");
                    playing = false;

                }

                catch { }


                //General stuff.
                fileNamePath = "";
                fileOpen = true;

                //Make new CISP.
                file = new CISP();
                file.stream = new CISP.streamInfo();
                file.stream.loop = 0;
                file.stream.loopStart = 0;
                file.stream.loopEnd = 0;
                file.stream.sampleRate = 0xFFFFFFFF;
                file.stream.sampleSize = 0;
                byte[] seek = { 2 };
                file.seekBlock = seek;
                file.tracks = new List<CISP.trackInfo>();
                file.channelData = new List<UInt16[]>();

                fileOpen = false;

                updateNodes();

                projectPanel.Hide();
                channelPanel.Hide();
                trackPanel.Hide();
                noInfoPanel.Show();
                
                anyFileSelectorSound.ShowDialog();
                if (anyFileSelectorSound.FileName != "")
                {

                    file = new CISP();
                    fileOpen = true;


                    if (anyFileSelectorSound.FileName.EndsWith(".wav")) { 
                            file.seekSize = 0;
                    file.seekBlock = new byte[0];
                    file.tracks = new List<CISP.trackInfo>();
                    file.channelData = new List<UInt16[]>();
                    RIFF r = new RIFF();
                    r.load(File.ReadAllBytes(anyFileSelectorSound.FileName));
                    file.stream.sampleRate = r.fmt.sampleRate;
                    int channelLength = r.data.data.Length / r.fmt.numChannels;
                    if (r.fmt.bitsPerSample != 16 || r.fmt.chunkFormat != 1)
                    {
                        MessageBox.Show("Not PCM16 Wave!");
                    }
                    else
                    {

                        file.stream.sampleRate = r.fmt.sampleRate;

                        //Add the channels.
                        List<UInt16>[] soundData = new List<UInt16>[r.fmt.numChannels];
                        for (int i = 0; i < soundData.Count(); i++)
                        {
                            soundData[i] = new List<UInt16>();

                            MemoryStream src = new MemoryStream(r.data.data);
                            BinaryDataReader br = new BinaryDataReader(src);

                            br.Position = i * 2;

                            while (br.Position < r.data.chunkSize)
                            {

                                soundData[i].Add(br.ReadUInt16());
                                try { for (int j2 = 1; j2 < r.fmt.numChannels; j2++) { br.ReadUInt16(); } } catch { }

                            }
                        }

                        //Now convert the corrected data per channel to the samples.
                        file.channelData = new List<UInt16[]>();
                        foreach (List<UInt16> x in soundData)
                        {
                            file.channelData.Add(x.ToArray());
                        }

                        file.stream.loopEnd = (UInt32)file.channelData[0].Length;

                    }

                }

                    if (anyFileSelectorSound.FileName.EndsWith(".bcwav") || anyFileSelectorSound.FileName.EndsWith(".bfwav")) {
                            b_wav b = new b_wav();
                    b.load(File.ReadAllBytes(anyFileSelectorSound.FileName));
                    file.seekSize = 0;
                    file.seekBlock = new byte[0];
                    file.stream.loop = b.info.loop;
                    file.stream.loopEnd = b.info.loopEnd;
                    file.stream.loopStart = b.info.loopStart;
                    file.stream.sampleRate = b.info.samplingRate;
                    file.tracks = new List<CISP.trackInfo>();
                    file.channelData = new List<UInt16[]>();

                    //Import due to encoding.
                    switch (b.info.soundEncoding)
                    {

                        case 0:
                            MessageBox.Show("Unsupported Data type! Must be PCM16 or DSPADPCM!");
                            break;

                        case 1:
                            file.stream.sampleRate = b.info.samplingRate;
                            file.stream.loopEnd = b.info.loopEnd;
                            file.stream.loop = b.info.loop;
                            file.stream.loopStart = b.info.loopStart;
                            for (int i = 0; i < b.data.pcm16.Count; i++)
                            {
                                file.channelData.Add(b.data.pcm16[i]);
                            }
                            break;

                        case 2:
                            b_wav v2 = b;
                            b = b.toRiff().toGameWavPCM();
                            b.update(endianNess.big);
                            file.stream.sampleRate = b.info.samplingRate;
                            file.stream.loopEnd = v2.info.loopEnd;
                            file.stream.loop = v2.info.loop;
                            file.stream.loopStart = v2.info.loopStart;
                            foreach (UInt16[] u in b.data.pcm16)
                            {
                                file.channelData.Add(u);
                            }

                            break;

                        case 3:
                            MessageBox.Show("Unsupported Data type! Must be PCM16 or DSPADPCM!");
                            break;

                    }


                }

                            if (anyFileSelectorSound.FileName.EndsWith("stm"))
                            {
                                b_stm s = new b_stm();
                                s.load(File.ReadAllBytes(anyFileSelectorSound.FileName));
                                b_wav v = new b_wav();
                                v = s.toRiff().toGameWavPCM();
                                v.update(endianNess.big);
                                file.stream.sampleRate = v.info.samplingRate;
                                file.stream.loopEnd = s.info.stream.loopEnd;
                                file.stream.loop = s.info.stream.loop;
                                file.stream.loopStart = s.info.stream.loopStart;
                                file.tracks = new List<CISP.trackInfo>();
                                file.channelData = new List<UInt16[]>();
                                foreach (UInt16[] u in v.data.pcm16)
                                {
                                    file.channelData.Add(u);
                                }

                                for (int i = 0; i < s.info.track.Count; i++)
                                {
                                    CISP.trackInfo t = new CISP.trackInfo();
                                    b_stm.infoBlock.trackInfo t2 = s.info.track[i];
                                    t.channelCount = t2.byteTable.count;
                                    t.channels = t2.byteTable.channelIndexes;
                                    t.flags = t2.flags;
                                    t.magic = "TRAC".ToCharArray();
                                    t.pan = t2.pan;
                                    t.volume = t2.volume;
                                    file.tracks.Add(t);
                                }

                            }

                    if (anyFileSelectorSound.FileName.EndsWith(".cisp"))
                    {
                        file = new CISP();
                        file.load(File.ReadAllBytes(anyFileSelectorSound.FileName));
                    }


                    loadChannelFiles();
                    updateNodes();

                }

                anyFileSelectorSound.FileName = "";

            }
        }



        //Fix resizing.
        private void formResized(object sender, EventArgs e) {
            if (playing)
            {

                try { timeBar.Value = (int)(((decimal)players[0].player.GetPosition() / ((decimal)file.channelData[0].Length * 2)) * 1440); } catch { }

            }
            else
            {

                try { if (players[0].player.PlaybackState == PlaybackState.Paused) { timeBar.Value = (int)(((decimal)players[0].player.GetPosition() / ((decimal)file.channelData[0].Length * 2)) * 1440); } } catch { }

            }

            try
            {
                spltStart.SplitPosition = (int)((decimal)file.stream.loopStart / (decimal)file.channelData[0].Length * (decimal)pnlLoop.Size.Width);
                spltEnd.SplitPosition = pnlLoop.Size.Width - (int)((decimal)file.stream.loopEnd / (decimal)file.channelData[0].Length * (decimal)pnlLoop.Size.Width);
            }
            catch { }
        }



        /// <summary>
        /// Export binary button.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void exportBinaryToolStripMenuItem_Click(object sender, EventArgs e)
        {

            saveGameFile.ShowDialog();
            if (saveGameFile.FileName != "") {

                //See what file to save.
                switch (saveGameFile.FilterIndex) {

                    case 1:
                        file.update();
                        b_wav b = file.toB_wav();
                        File.WriteAllBytes(saveGameFile.FileName, b.toBytes(endianNess.big));
                        break;

                    case 2:
                        file.update();
                        b_wav b2 = file.toB_wav();
                        File.WriteAllBytes(saveGameFile.FileName, b2.toBytes(endianNess.little));
                        break;

                    case 3:

                        List<UInt16[]> bak = file.channelData;

                        for (int i = 0; i < file.channelData.Count; i++) {

                            List<UInt16> sb22 = file.channelData[i].ToList();
                            sb22.RemoveRange((int)file.stream.loopEnd, sb22.Count - (int)file.stream.loopEnd);
                            file.channelData[i] = sb22.ToArray();

                        }

                        file.update();
                        Directory.SetCurrentDirectory(isabellePath + "/Data/Tools/Pack");

                        File.WriteAllBytes("tmp.wav", file.toRIFF().toBytes(true));
                        loadChannelFiles();
                        Process p = new Process();
                        p.StartInfo.CreateNoWindow = true;
                        p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                        p.StartInfo.FileName = "BCSTM.bat";
                        p.StartInfo.Arguments = "tmp.wav";
                        p.Start();
                        p.WaitForExit();

                        b_stm h = new b_stm();
                        h.load(File.ReadAllBytes("tmp.bcstm"));
                        b_stm s = file.toB_stm();
                        h.info.stream.loop = s.info.stream.loop;
                        h.info.stream.loopStart = s.info.stream.loopStart;
                        h.info.track = s.info.track;
                        Directory.SetCurrentDirectory(isabellePath);

                        File.WriteAllBytes(saveGameFile.FileName, h.toBytes2(endianNess.big));
                        break;

                    case 4:

                        List<UInt16[]> bak2 = file.channelData;

                        for (int i = 0; i < file.channelData.Count; i++)
                        {

                            List<UInt16> sb22 = file.channelData[i].ToList();
                            sb22.RemoveRange((int)file.stream.loopEnd, sb22.Count - (int)file.stream.loopEnd);
                            file.channelData[i] = sb22.ToArray();

                        }

                        file.update();
                        Directory.SetCurrentDirectory(isabellePath + "/Data/Tools/Pack");

                        File.WriteAllBytes("tmp.wav", file.toRIFF().toBytes(true));
                        loadChannelFiles();
                        Process p2 = new Process();
                        p2.StartInfo.CreateNoWindow = true;
                        p2.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                        p2.StartInfo.FileName = "BCSTM.bat";
                        p2.StartInfo.Arguments = "tmp.wav";
                        p2.Start();
                        p2.WaitForExit();

                        b_stm h2 = new b_stm();
                        h2.load(File.ReadAllBytes("tmp.bcstm"));
                        b_stm s2 = file.toB_stm();
                        h2.info.stream.loop = s2.info.stream.loop;
                        h2.info.stream.loopStart = s2.info.stream.loopStart;
                        h2.info.track = s2.info.track;
                        Directory.SetCurrentDirectory(isabellePath);

                        File.WriteAllBytes(saveGameFile.FileName, h2.toBytes2(endianNess.little));
                        break;

                }


                saveGameFile.FileName = "";

            }

        }



        /// <summary>
        /// Close the file.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void closeToolStripMenuItem_Click(object sender, EventArgs e)
        {

            int result = 2;
            if (idFileOpen)
            {

                SaveCloseDialog s = new SaveCloseDialog();
                result = s.getValue();

                if (result == 0)
                {
                    save();
                }

            }

            if (idFileOpen && (result == 1 || result == 0))
            {

                //General stuff.
                idFileNamePath = "";
                idFileOpen = true;
                this.Text = "Koopa Harmony";

                //Make new BBID.
                idFile = new bbid();
                idFile.actionNumbers = new UInt32[64];
                idFile.danceMoves = new List<byte>();
                idFile.padding = 0;
                idFile.count = 0;
                idFile.sampleNumbers = new List<UInt32>();
                idFile.tempo = 0x64;
                idFile.unknown = 4;
                idFileOpen = false;

                updateNodes();

                projectPanel.Hide();
                channelPanel.Hide();
                trackPanel.Hide();
                noInfoPanel.Show();

            }

        }


        //Quit.
        private void quitToolStripMenuItem_Click(object sender, EventArgs e)
        {

            if (idFileOpen)
            {

                SaveCloseDialog s = new SaveCloseDialog();
                int result = s.getValue();

                if (result == 0)
                {
                    save();
                }
                else if (result == 1)
                {
                    this.Close();
                }

            }
            else {

                this.Close();

            }

        }

        //Save stream from wave.
        private void simpleWaveToGameStreamToolStripMenuItem_Click(object sender, EventArgs e)
        {
            waveSelectorBox.ShowDialog();
            if (waveSelectorBox.FileName != "")
            {
                RIFF r = new RIFF();
                r.load(File.ReadAllBytes(waveSelectorBox.FileName));
                waveSelectorBox.FileName = "";

                Directory.SetCurrentDirectory(isabellePath + "/Data/Tools/Pack");

                File.WriteAllBytes("tmp.wav", r.toBytes(false));
                Process p = new Process();
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                p.StartInfo.FileName = "BCSTM.bat";
                p.StartInfo.Arguments = "tmp.wav";
                p.Start();
                p.WaitForExit();

                b_stm h = new b_stm();
                h.load(File.ReadAllBytes("tmp.bcstm"));
                Directory.SetCurrentDirectory(isabellePath);

                saveGameStreamBox.ShowDialog();
                if (saveGameStreamBox.FileName != "")
                {

                    if (saveGameStreamBox.FilterIndex == 1)
                    {

                        File.WriteAllBytes(saveGameStreamBox.FileName, h.toBytes2(endianNess.big));

                    }
                    else
                    {

                        File.WriteAllBytes(saveGameStreamBox.FileName, h.toBytes2(endianNess.little));

                    }

                    saveGameStreamBox.FileName = "";

                }
            }
        }

        //Game file to game file.
        private void gameFileToGameFileToolStripMenuItem_Click(object sender, EventArgs e)
        {

            gameFileSelectorBox.ShowDialog();

            if (gameFileSelectorBox.FileName != "") {

                switch (gameFileSelectorBox.FilterIndex) {

                    case 1:

                        b_wav b = new b_wav();
                        b.load(File.ReadAllBytes(gameFileSelectorBox.FileName));

                        saveGameFile.ShowDialog();

                        if (saveGameFile.FileName != "") {

                            switch (saveGameFile.FilterIndex) {

                                case 1:
                                    File.WriteAllBytes(saveGameFile.FileName, b.toBytes(endianNess.big));
                                    break;

                                case 2:
                                    File.WriteAllBytes(saveGameFile.FileName, b.toBytes(endianNess.little));
                                    break;

                                case 3:
                                    RIFF r = b.toRiff();
                                    Directory.SetCurrentDirectory(isabellePath + "/Data/Tools/Pack");

                                    File.WriteAllBytes("tmp.wav", r.toBytes(true));
                                    Process p = new Process();
                                    p.StartInfo.CreateNoWindow = true;
                                    p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                                    p.StartInfo.FileName = "BCSTM.bat";
                                    p.StartInfo.Arguments = "tmp.wav";
                                    p.Start();
                                    p.WaitForExit();

                                    b_stm h = new b_stm();
                                    h.load(File.ReadAllBytes("tmp.bcstm"));
                                    h.info.stream.loop = b.info.loop;
                                    h.info.stream.loopStart = b.info.loopStart;
                                    Directory.SetCurrentDirectory(isabellePath);

                                    File.WriteAllBytes(saveGameFile.FileName, h.toBytes2(endianNess.big));
                                    break;

                                case 4:
                                    RIFF r2 = b.toRiff();
                                    Directory.SetCurrentDirectory(isabellePath + "/Data/Tools/Pack");

                                    File.WriteAllBytes("tmp.wav", r2.toBytes(true));
                                    Process p2 = new Process();
                                    p2.StartInfo.CreateNoWindow = true;
                                    p2.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                                    p2.StartInfo.FileName = "BCSTM.bat";
                                    p2.StartInfo.Arguments = "tmp.wav";
                                    p2.Start();
                                    p2.WaitForExit();

                                    b_stm h2 = new b_stm();
                                    h2.load(File.ReadAllBytes("tmp.bcstm"));
                                    h2.info.stream.loop = b.info.loop;
                                    h2.info.stream.loopStart = b.info.loopStart;
                                    Directory.SetCurrentDirectory(isabellePath);

                                    File.WriteAllBytes(saveGameFile.FileName, h2.toBytes2(endianNess.little));
                                    break;

                            }

                        }

                        saveGameFile.FileName = "";

                        break;

                    case 2:

                        b_stm s = new b_stm();
                        s.load(File.ReadAllBytes(gameFileSelectorBox.FileName));

                        saveGameFile.ShowDialog();

                        if (saveGameFile.FileName != "")
                        {

                            switch (saveGameFile.FilterIndex)
                            {

                                case 1:
                                    File.WriteAllBytes(saveGameFile.FileName, s.toB_wav().toBytes(endianNess.big));
                                    break;

                                case 2:
                                    File.WriteAllBytes(saveGameFile.FileName, s.toB_wav().toBytes(endianNess.little));
                                    break;

                                case 3:

                                    File.WriteAllBytes(saveGameFile.FileName, s.toBytes2(endianNess.big));
                                    break;

                                case 4:
                                    File.WriteAllBytes(saveGameFile.FileName, s.toBytes2(endianNess.little));
                                    break;

                            }

                        }

                        saveGameFile.FileName = "";

                        break;

                }

            }

            gameFileSelectorBox.FileName = "";

        }

        //Help.
        private void getHelpToolStripMenuItem_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("http://discord.gg/6VDPGne");
        }

        //Form closing.
        public void formClosing(object sender, System.EventArgs e) {

            try
            {
                for (int i = 0; i < players.Count(); i++)
                {
                    players[i].soundOut.Dispose();
                    players[i].soundOut = null;
                }
            }
            catch { }

        }


        //Allow scrolling.
        private void timeBar_MouseDown(object sender, MouseEventArgs e)
        {

            scrolling = true;

        }

        private void timeBar_MouseUp(object sender, MouseEventArgs e)
        {

            scrolling = false;

            if (true) {

                try
                {

                    //timeBar.Value = (int)(((decimal)players[0].source.Position / ((decimal)file.channelData[0].Length * 2)) * 1440);
                    for (int i = 0; i < players.Count(); i++)
                    {
                        players[i].source.Position = (long)(((decimal)file.channelData[0].Length) * 2 * ((decimal)timeBar.Value / (decimal)timeBar.Maximum));
                    }

                }
                catch { }

            }

        }


        private void loopStart_MouseDown(object sender, MouseEventArgs e)
        {

            scrollingLeft = true;

        }

        private void loopStart_MouseUp(object sender, MouseEventArgs e)
        {

            scrollingLeft = false;

            if (true)
            {

                try
                {
                    //if (!scrollingLeft) spltStart.SplitPosition = (int)((decimal)file.stream.loopStart / (decimal)file.channelData[0].Length * (decimal)pnlLoop.Size.Width);
                    //if (!scrollingRight) spltEnd.SplitPosition = pnlLoop.Size.Width - (int)((decimal)file.stream.loopEnd / (decimal)file.channelData[0].Length * (decimal)pnlLoop.Size.Width);

                    file.stream.loopStart = (UInt32)(spltStart.SplitPosition * (decimal)file.channelData[0].Length / (decimal)pnlLoop.Size.Width);
                    doInfoStuff();

                }
                catch { }

            }

        }


        private void loopEnd_MouseDown(object sender, MouseEventArgs e)
        {

            scrollingRight = true;

        }

        private void loopEnd_MouseUp(object sender, MouseEventArgs e)
        {

            scrollingRight = false;

            if (true)
            {

                try
                {

                    try
                    {
                        //if (!scrollingLeft) spltStart.SplitPosition = (int)((decimal)file.stream.loopStart / (decimal)file.channelData[0].Length * (decimal)pnlLoop.Size.Width);
                        //if (!scrollingRight) spltEnd.SplitPosition = pnlLoop.Size.Width - (int)((decimal)file.stream.loopEnd / (decimal)file.channelData[0].Length * (decimal)pnlLoop.Size.Width);

                        file.stream.loopEnd = (UInt32)((decimal)file.channelData[0].Length * (pnlLoop.Size.Width - spltEnd.SplitPosition) / (decimal)pnlLoop.Size.Width);
                        doInfoStuff();

                    }
                    catch { }

                }
                catch { }

            }

        }

        private void fileToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void channelPanel_Paint(object sender, PaintEventArgs e)
        {

        }



        private void changeSample_ButtonClick(object sender, EventArgs e)
        {

            //Current sample.
            if (fileOpen && players != null)
            {
                if (players[0].source != null)
                {
                    //players[0].source.Position = ;
                }
            }

        }

        //Jump to sample.
        private void jumpToSampleButton_Click(object sender, EventArgs e)
        {

            if (fileOpen && players != null)
            {
                if (players[0].source != null)
                {
                    for (int i = 0; i < players.Length; i++) {

                        UInt32 sampleNumber = idFile.sampleNumbers[tree.SelectedNode.Index];
                        if (sampleNumber > file.stream.loopEnd) { sampleNumber = sampleNumber - file.stream.loopEnd + file.stream.loopStart; }
                        players[i].source.Position = sampleNumber*2;

                    }
                }
            }

        }

        private void gotoSampleButton_Click(object sender, EventArgs e)
        {
            if (fileOpen && players != null)
            {
                if (players[0].source != null)
                {
                    for (int i = 0; i < players.Length; i++)
                    {

                        players[i].source.Position = (long)gotoSampleBox.Value * 2;

                    }
                }
            }
        }


        //Change Event.
        private void changeEventBox_Click(object sender, EventArgs e)
        {

            idFile.sampleNumbers[tree.SelectedNode.Index] = (UInt32)sampleIdBox.Value;
            if (afterLoopBox.Checked && afterLoopBox.Enabled) { idFile.sampleNumbers[tree.SelectedNode.Index] += file.stream.loopEnd - file.stream.loopStart; }
            idFile.danceMoves[tree.SelectedNode.Index] = (byte)(danceBox.SelectedIndex+1);
            showEvents();

        }


        //Make new event.
        private void newEventButton_Click(object sender, EventArgs e)
        {
            if (idFile.sampleNumbers.Count == 64)
            {
                MessageBox.Show("You can only have 64 events per song!");
            }
            else {
                idFile.danceMoves.Add(2);
                idFile.sampleNumbers.Add((UInt32)(players[0].source.Position / 2));
                updateNodes();
            }
        }


        //Get selected event.
        private void setEventButton_Click(object sender, EventArgs e)
        {
            if (!selectedEvent.Text.Contains("NULL"))
            {

                int index = int.Parse(selectedEvent.Text.Substring(22)) - 1;
                idFile.sampleNumbers[index] = (UInt32)(players[0].source.Position / 2);
                showEvents();

            }
            else {
                MessageBox.Show("No event selected!");
            }
        }

        private void eventPanel_Paint(object sender, PaintEventArgs e)
        {
            Pen p = new Pen(Color.LimeGreen, 5);

            Graphics g = e.Graphics;

            if (idFileOpen && fileOpen)
            {
                p = new Pen(Color.LimeGreen, 2);
                for (int i = 0; i < idFile.sampleNumbers.Count(); i++)
                {
                    int x = (int)(((decimal)idFile.sampleNumbers[i] / ((decimal)file.channelData[0].Length)) * eventPanel.Size.Width);
                    g.DrawLine(p, x, 0, x, 200);
                }

            }
            else {
                g.DrawLine(p, 0, 0, 0, 200);
            }

            g.Dispose();
        }
    }
}
