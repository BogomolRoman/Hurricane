﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hurricane.ViewModelBase;
using System.IO;
using System.Windows;

namespace Hurricane.ViewModels
{
    class MainViewModel : PropertyChangedBase
    {
        #region Singleton & Constructor
        private static MainViewModel _instance;
        public static MainViewModel Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new MainViewModel();
                return _instance;
            }
        }

        private MainViewModel()
        {

        }

        private MainWindow BaseWindow;
        public Settings.HurricaneSettings MySettings { get; protected set; }
        private Utilities.KeyboardListener KListener;
        
        public void Loaded(MainWindow window)
        {
            this.BaseWindow = window;
            MySettings = Settings.HurricaneSettings.Instance;

            MusicManager = new Music.MusicManager();
            MusicManager.CSCoreEngine.StartVisualization += CSCoreEngine_StartVisualization;
            MusicManager.CSCoreEngine.TrackChanged += CSCoreEngine_TrackChanged;
            MusicManager.CSCoreEngine.PositionChanged += CSCoreEngine_PositionChanged;
            MusicManager.LoadFromSettings();
            BaseWindow.LocationChanged += (s, e) => {
                if (EqualizerIsOpen) {
                    var rect = Utilities.WindowHelper.GetWindowRectangle(BaseWindow);
                    equalizerwindow.SetPosition(rect, BaseWindow.ActualWidth);
                };
            };
            KListener = new Utilities.KeyboardListener();
            KListener.KeyDown += KListener_KeyDown;
            Updater = new Settings.UpdateService(MySettings.Config.Language == "de" ? Settings.UpdateService.Language.German : Settings.UpdateService.Language.English);
            Updater.CheckForUpdates(BaseWindow);
        }
        #endregion

        #region Events
        public event EventHandler StartVisualization; //This is ok so, trust me ;)
        void CSCoreEngine_StartVisualization(object sender, EventArgs e)
        {
            if (StartVisualization != null) StartVisualization(sender, e);
        }

        public event EventHandler<Music.TrackChangedEventArgs> TrackChanged;
        void CSCoreEngine_TrackChanged(object sender, Music.TrackChangedEventArgs e)
        {
            if (TrackChanged != null) TrackChanged(sender, e);
        }

        public event EventHandler<Music.PositionChangedEventArgs> PositionChanged;
        void CSCoreEngine_PositionChanged(object sender, Music.PositionChangedEventArgs e)
        {
            if (PositionChanged != null) PositionChanged(sender, e);
        }

        void KListener_KeyDown(object sender, Utilities.RawKeyEventArgs args)
        {
            switch (args.Key)
            {
                case System.Windows.Input.Key.MediaPlayPause:
                    Application.Current.Dispatcher.Invoke(() => MusicManager.CSCoreEngine.TogglePlayPause());
                    break;
                case System.Windows.Input.Key.MediaPreviousTrack:
                    Application.Current.Dispatcher.Invoke(() => MusicManager.GoBackward());
                    break;
                case System.Windows.Input.Key.MediaNextTrack:
                    Application.Current.Dispatcher.Invoke(() => MusicManager.GoForward());
                    break;
            }
        }
        #endregion

        #region Methods
        async Task ImportFiles(string[] paths, Music.Playlist playlist, EventHandler finished = null)
        {
            var controller = BaseWindow.Messages.CreateProgressDialog(string.Empty, false);

            await playlist.AddFiles((s, e) =>
            {
                controller.SetProgress(e.Percentage);
                controller.SetMessage(e.CurrentFile);
                controller.SetTitle(string.Format(Application.Current.FindResource("filesgetimported").ToString(), e.FilesImported, e.TotalFiles));
            }, paths);

            MusicManager.SaveToSettings();
            MySettings.Save();
            await controller.Close();
            if (finished != null) Application.Current.Dispatcher.Invoke(() => finished(this, EventArgs.Empty));
        }

        public async void DragDropFiles(string[] files)
        {
            List<string> paths = new List<string>();
            foreach (string file in files)
            {
                if (Music.Track.IsSupported(new FileInfo(file)))
                {
                    paths.Add(file);
                }
            }
            await ImportFiles(paths.ToArray(), MusicManager.SelectedPlaylist);
        }

        public void Closing()
        {
            MusicManager.CSCoreEngine.StopPlayback();
            if (EqualizerIsOpen) equalizerwindow.Close();
            if (MusicManager != null)
            {
                MusicManager.SaveToSettings();
                MySettings.Save();
                MusicManager.Dispose();
            }
            if (KListener != null)
                KListener.Dispose();
        }

        private bool remember = false;
        private Music.Playlist rememberedplaylist;

        public async void OpenFile(FileInfo file, bool play)
        {
            foreach (var playlist in MusicManager.Playlists)
            {
                foreach (var track in playlist.Tracks)
                {
                    if (track.Path == file.FullName)
                    {
                        if (play) MusicManager.PlayTrack(track, playlist);
                        return;
                    }
                }
            }

            Music.Playlist selectedplaylist = null;
            var config = Hurricane.Settings.HurricaneSettings.Instance.Config;

            if (config.RememberTrackImportPlaylist)
            {
                var items = MusicManager.Playlists.Where((x) => x.Name == config.PlaylistToImportTrack);
                if (items.Any())
                {
                    selectedplaylist = items.First();
                }
                else { config.RememberTrackImportPlaylist = false; config.PlaylistToImportTrack = null; }
            }

            if (selectedplaylist == null)
            {
                if (remember && MusicManager.Playlists.Contains(rememberedplaylist))
                {
                    selectedplaylist = rememberedplaylist;
                }
                else
                {
                    Views.TrackImportWindow window = new Views.TrackImportWindow(musicmanager.Playlists, musicmanager.SelectedPlaylist, file.Name) { Owner = BaseWindow };
                    if (window.ShowDialog() == false) return;
                    selectedplaylist = window.SelectedPlaylist;
                    if (window.RememberChoice)
                    {
                        remember = true;
                        rememberedplaylist = window.SelectedPlaylist;
                        if (window.RememberAlsoAfterRestart)
                        {
                            config.RememberTrackImportPlaylist = true;
                            config.PlaylistToImportTrack = selectedplaylist.Name;
                        }
                    }
                }
            }

            await ImportFiles(new string[] { file.FullName }, selectedplaylist, (s, e) => OpenFile(file, play));
        }

        public void MoveOut()
        {
            if (EqualizerIsOpen) { equalizerwindow.Close(); EqualizerIsOpen = false; }
        }

        #endregion

        #region Commands
        private RelayCommand openequalizer;
        private bool EqualizerIsOpen;
        Views.EqualizerWindow equalizerwindow;
        public RelayCommand OpenEqualizer
        {
            get
            {
                if (openequalizer == null)
                    openequalizer = new RelayCommand((object parameter) =>
                    {
                        if (!EqualizerIsOpen)
                        {
                            var rect = Utilities.WindowHelper.GetWindowRectangle(BaseWindow);
                            equalizerwindow = new Views.EqualizerWindow(MusicManager.CSCoreEngine, rect, BaseWindow.ActualWidth);
                            equalizerwindow.Closed += (s, e) => EqualizerIsOpen = false;
                            equalizerwindow.BeginCloseAnimation += (s, e) => BaseWindow.Activate();
                            equalizerwindow.Show();
                            EqualizerIsOpen = true;
                        }
                        else
                        {
                            equalizerwindow.Activate();
                        }
                    });
                return openequalizer;
            }
        }

        private RelayCommand reloadtrackinformations;
        public RelayCommand ReloadTrackInformations
        {
            get
            {
                if (reloadtrackinformations == null)
                    reloadtrackinformations = new RelayCommand(async(object parameter) => {
                        var controller = BaseWindow.Messages.CreateProgressDialog(string.Empty, false);

                        await MusicManager.SelectedPlaylist.ReloadTrackInformations((s, e) =>
                        {
                            controller.SetProgress(e.Percentage);
                            controller.SetMessage(e.CurrentFile);
                            controller.SetTitle(string.Format(Application.Current.FindResource("loadtrackinformation").ToString(), e.FilesImported, e.TotalFiles));
                        }, true);

                        MusicManager.SaveToSettings(); MySettings.Save(); await controller.Close();
                    });
                return reloadtrackinformations;
            }
        }

        private RelayCommand removemissingtracks;
        public RelayCommand RemoveMissingTracks
        {
            get
            {
                if (removemissingtracks == null)
                    removemissingtracks = new RelayCommand(async(object parameter) => {
                        if (await BaseWindow.ShowMessage(Application.Current.FindResource("suredeleteallmissingtracks").ToString(), Application.Current.FindResource("removemissingtracks").ToString(), true))
                        {
                            MusicManager.SelectedPlaylist.RemoveMissingTracks();
                            MusicManager.SaveToSettings();
                            MySettings.Save();
                        }
                    });
                return removemissingtracks;
            }
        }

        private RelayCommand removeduplicatetracks;
        public RelayCommand RemoveDuplicateTracks
        {
            get
            {
                if (removeduplicatetracks == null)
                    removeduplicatetracks = new RelayCommand(async(object parameter) => {
                        if (await BaseWindow.ShowMessage(Application.Current.FindResource("removeduplicatetracksmessage").ToString(), Application.Current.FindResource("removeduplicates").ToString(), true))
                        {
                            var controller = BaseWindow.Messages.CreateProgressDialog(Application.Current.FindResource("removeduplicates").ToString(), true);
                            controller.SetMessage(Application.Current.FindResource("searchingforduplicates").ToString());

                            var counter = await MusicManager.SelectedPlaylist.RemoveDuplicates();
                            await controller.Close();
                            await BaseWindow.ShowMessage(counter == 0 ? Application.Current.FindResource("noduplicatesmessage").ToString() : string.Format(Application.Current.FindResource("tracksremoved").ToString(), counter), Application.Current.FindResource("removeduplicates").ToString(), false);
                        }
                    });
                return removeduplicatetracks;
            }
        }

        private RelayCommand openqueuemanager;
        public RelayCommand OpenQueueManager
        {
            get
            {
                if (openqueuemanager == null)
                    openqueuemanager = new RelayCommand((object parameter) =>
                    {
                        Views.QueueManager window = new Views.QueueManager() { Owner = BaseWindow };
                        window.ShowDialog();
                    });
                return openqueuemanager;
            }
        }

        private RelayCommand addfilestoplaylist;
        public RelayCommand AddFilesToPlaylist
        {
            get
            {
                if (addfilestoplaylist == null)
                    addfilestoplaylist = new RelayCommand(async(object parameter) =>
                    {
                        Ookii.Dialogs.Wpf.VistaOpenFileDialog ofd = new Ookii.Dialogs.Wpf.VistaOpenFileDialog();
                        ofd.CheckFileExists = true;
                        ofd.Title = System.Windows.Application.Current.FindResource("selectfiles").ToString();
                        ofd.Filter = CSCore.Codecs.CodecFactory.SupportedFilesFilterEn;
                        ofd.Multiselect = true;
                        if (ofd.ShowDialog(BaseWindow) == true)
                        {
                            await ImportFiles(ofd.FileNames, MusicManager.SelectedPlaylist);
                        }
                    });
                return addfilestoplaylist;
            }
        }

        private RelayCommand addfoldertoplaylist;
        public RelayCommand AddFolderToPlaylist
        {
            get
            {
                if (addfoldertoplaylist == null)
                    addfoldertoplaylist = new RelayCommand(async(object parameter) =>
                    {
                        Views.FolderImportWindow window = new Views.FolderImportWindow();
                        window.Owner = BaseWindow;
                        if (window.ShowDialog() == true)
                        {
                            DirectoryInfo di = new DirectoryInfo(window.SelectedPath);
                            List<string> filestoadd = new List<string>();
                            foreach (FileInfo fi in di.GetFiles("*.*", window.IncludeSubfolder ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
                            {
                                if (Music.Track.IsSupported(fi))
                                {
                                    filestoadd.Add(fi.FullName);
                                }
                            }

                            await ImportFiles(filestoadd.ToArray(), MusicManager.SelectedPlaylist);
                        }
                    });
                return addfoldertoplaylist;
            }
        }

        private RelayCommand addnewplaylist;
        public RelayCommand AddNewPlaylist
        {
            get
            {
                if (addnewplaylist == null)
                    addnewplaylist = new RelayCommand(async(object parameter) =>
                    {
                        string result = await BaseWindow.ShowInputDialog(Application.Current.FindResource("newplaylist").ToString(), Application.Current.FindResource("nameofplaylist").ToString(), Application.Current.FindResource("create").ToString(), string.Empty);
                        if (!string.IsNullOrEmpty(result))
                        {
                            Music.Playlist newplaylist = new Music.Playlist() { Name = result };
                            MusicManager.Playlists.Add(newplaylist);
                            MusicManager.RegisterPlaylist(newplaylist);
                            MusicManager.SelectedPlaylist = newplaylist;
                            MusicManager.SaveToSettings();
                            MySettings.Save();
                        }
                    });
                return addnewplaylist;
            }
        }

        private RelayCommand removeselectedtracks;
        public RelayCommand RemoveSelectedTracks
        {
            get
            {
                if (removeselectedtracks == null)
                    removeselectedtracks = new RelayCommand(async(object parameter) =>
                    {
                        Music.Track track = MusicManager.SelectedTrack;
                        if (track == null) return;

                        List<Music.Track> tracksToRemove = new List<Music.Track>();
                        foreach (var t in MusicManager.SelectedPlaylist.Tracks)
                        {
                            if (t.IsSelected)
                                tracksToRemove.Add(t);
                        }

                        if (await BaseWindow.ShowMessage(string.Format(Application.Current.FindResource("removetracksmessage").ToString(), tracksToRemove.Count > 0 ? string.Format("{0} {1}", tracksToRemove.Count, Application.Current.FindResource("tracks").ToString()) : string.Format("\"{0}\"", track.Title)), Application.Current.FindResource("removetracks").ToString(), true))
                        {
                            foreach (var t in tracksToRemove)
                            {
                                if (t.IsPlaying)
                                {
                                    MusicManager.CSCoreEngine.StopPlayback();
                                    MusicManager.CSCoreEngine.KickTrack();
                                }
                                MusicManager.SelectedPlaylist.RemoveTrackWithAnimation(t);
                            }
                        }
                    });
                return removeselectedtracks;
            }
        }

        private RelayCommand opensettings;
        public RelayCommand OpenSettings
        {
            get
            {
                if (opensettings == null)
                    opensettings = new RelayCommand((object parameter) => { Views.SettingsWindow window = new Views.SettingsWindow() { Owner = BaseWindow }; window.ShowDialog(); });
                return opensettings;
            }
        }

        private RelayCommand removeplaylist;
        public RelayCommand RemovePlaylist
        {
            get
            {
                if (removeplaylist == null)
                    removeplaylist = new RelayCommand(async(object parameter) =>
                    {
                        if (MusicManager.Playlists.Count == 1)
                        {
                            await BaseWindow.ShowMessage(Application.Current.FindResource("errorcantdeleteplaylist").ToString(), Application.Current.FindResource("error").ToString(), false);
                            return;
                        }
                        if (await BaseWindow.ShowMessage(string.Format(Application.Current.FindResource("reallydeleteplaylist").ToString(), MusicManager.SelectedPlaylist.Name), Application.Current.FindResource("removeplaylist").ToString(), true))
                        {
                            Music.Playlist PlaylistToDelete = MusicManager.SelectedPlaylist;
                            Music.Playlist NewPlaylist = MusicManager.Playlists[0];
                            bool nexttrack = MusicManager.CurrentPlaylist == PlaylistToDelete;
                            MusicManager.CurrentPlaylist = NewPlaylist;
                            if (nexttrack)
                            { MusicManager.CSCoreEngine.StopPlayback(); MusicManager.CSCoreEngine.KickTrack(); MusicManager.GoForward(); }
                            MusicManager.Playlists.Remove(PlaylistToDelete);
                            MusicManager.SelectedPlaylist = NewPlaylist;
                        }
                    });
                return removeplaylist;
            }
        }

        private RelayCommand renameplaylist;
        public RelayCommand RenamePlaylist
        {
            get
            {
                if (renameplaylist == null)
                    renameplaylist = new RelayCommand(async(object parameter) =>
                    {
                        string result = await BaseWindow.ShowInputDialog(Application.Current.FindResource("renameplaylist").ToString(), Application.Current.FindResource("nameofplaylist").ToString(), Application.Current.FindResource("rename").ToString(), MusicManager.SelectedPlaylist.Name);
                        if (!string.IsNullOrEmpty(result)) { MusicManager.SelectedPlaylist.Name = result; }
                    });
                return renameplaylist;
            }
        }

        private RelayCommand opentrackinformations;
        public RelayCommand OpenTrackInformations
        {
            get
            {
                if (opentrackinformations == null)
                    opentrackinformations = new RelayCommand(async(object parameter) =>
                    {
                        await BaseWindow.ShowTrackInformations(MusicManager.SelectedTrack);
                    });
                return opentrackinformations;
            }
        }

        private RelayCommand openupdater;
        public RelayCommand OpenUpdater
        {
            get
            {
                if (openupdater == null)
                    openupdater = new RelayCommand((object parameter) =>
                    {
                        Views.UpdateWindow window = new Views.UpdateWindow(Updater) { Owner = BaseWindow };
                        window.ShowDialog();
                    });
                return openupdater;
            }
        }

        private RelayCommand clearselectedplaylist;
        public RelayCommand ClearSelectedPlaylist
        {
            get
            {
                if (clearselectedplaylist == null)
                    clearselectedplaylist = new RelayCommand(async(object parameter) =>
                    {
                        if (await BaseWindow.ShowMessage(string.Format(Application.Current.FindResource("sureremovealltracks").ToString(), MusicManager.SelectedPlaylist.Name), Application.Current.FindResource("removealltracks").ToString(), true))
                        {
                            MusicManager.SelectedPlaylist.Tracks.Clear();
                        }
                    });
                return clearselectedplaylist;
            }
        }
        #endregion

        #region Properties
        private Music.MusicManager musicmanager;
        public Music.MusicManager MusicManager
        {
            get { return musicmanager; }
            set
            {
                SetProperty(value, ref musicmanager);
            }
        }
        
        private Settings.UpdateService updater;
        public Settings.UpdateService Updater
        {
            get { return updater; }
            set
            {
                SetProperty(value, ref updater);
            }
        }

        #endregion

    }
}
