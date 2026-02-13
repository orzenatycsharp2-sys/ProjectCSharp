using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Telhai.DotNet.PlayerProject.Models;
using Telhai.DotNet.PlayerProject.Services;

namespace Telhai.DotNet.PlayerProject
{
    public partial class MusicPlayer : Window
    {
        private MediaPlayer mediaPlayer = new MediaPlayer();
        private DispatcherTimer timer = new DispatcherTimer();
        private List<MusicTrack> library = new List<MusicTrack>();
        private bool isDragging = false;
        private const string FILE_NAME = "library.json";

        private readonly ItunesService _itunesService = new ItunesService();
        private CancellationTokenSource? _cts;

        private DispatcherTimer slideshowTimer = new DispatcherTimer();
        private int currentImageIndex = 0;
        private MusicTrack? currentlyPlayingTrack = null;

        public MusicPlayer()
        {
            InitializeComponent();
            timer.Interval = TimeSpan.FromMilliseconds(500);
            timer.Tick += new EventHandler(Timer_Tick);
            this.Loaded += MusicPlayer_Loaded;

            slideshowTimer.Interval = TimeSpan.FromSeconds(3);
            slideshowTimer.Tick += SlideshowTimer_Tick;
            mediaPlayer.MediaEnded += MediaPlayer_MediaEnded;
        }

        private void MusicPlayer_Loaded(object sender, RoutedEventArgs e)
        {
            this.LoadLibrary();
        }

        private void LstLibrary_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstLibrary.SelectedItem is MusicTrack track)
            {
                txtCurrentSong.Text = track.Title;
                txtFilePath.Text = track.FilePath;

                if (track == currentlyPlayingTrack && track.LocalImages != null && track.LocalImages.Count > 0)
                {
                    imgAlbumArt.Source = new BitmapImage(new Uri(track.LocalImages[currentImageIndex]));
                }
                else
                {
                    UpdateUIWithTrackData(track);
                }
            }
        }

        private async void LstLibrary_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {

            if (lstLibrary.SelectedItem is MusicTrack track)
            {
                currentlyPlayingTrack = track;
                PlayTrack(track);

                currentImageIndex = 0;

                if (!string.IsNullOrEmpty(track.Artist))
                {
                    UpdateUIWithTrackData(track);
                    if (track.LocalImages != null && track.LocalImages.Count > 0)
                    {
                        imgAlbumArt.Source = new BitmapImage(new Uri(track.LocalImages[0]));
                    }
                    txtStatus.Text = "Playing (Loaded from Cache)";
                }
                else
                {
                    _cts?.Cancel();
                    _cts = new CancellationTokenSource();
                    await LoadSongExtraInfoAsync(track, _cts.Token);

                    if (track.LocalImages != null && track.LocalImages.Count > 0)
                    {
                        imgAlbumArt.Source = new BitmapImage(new Uri(track.LocalImages[0]));
                    }
                }

                slideshowTimer.Start();
            }
        }

        private void UpdateUIWithTrackData(MusicTrack track)
        {
            txtArtist.Text = track.Artist;
            txtAlbum.Text = track.Album;

            if (!string.IsNullOrEmpty(track.ArtworkUrl))
            {
                imgAlbumArt.Source = new BitmapImage(new Uri(track.ArtworkUrl));
            }
            else if (track.LocalImages != null && track.LocalImages.Count > 0)
            {
                imgAlbumArt.Source = new BitmapImage(new Uri(track.LocalImages[0]));
            }
            else
            {
                imgAlbumArt.Source = null;
            }
        }

        private void PlayTrack(MusicTrack track)
        {
            mediaPlayer.Open(new Uri(track.FilePath));
            mediaPlayer.Play();
            timer.Start();
            txtStatus.Text = "Playing";
        }

        private async Task LoadSongExtraInfoAsync(MusicTrack track, CancellationToken token)
        {
            try
            {
                txtStatus.Text = "Searching Info...";
                var info = await _itunesService.SearchOneAsync(track.Title, token);

                if (info != null)
                {
                    track.Artist = info.ArtistName;
                    track.Album = info.AlbumName;
                    track.ArtworkUrl = info.ArtworkUrl;

                    UpdateUIWithTrackData(track);
                    SaveLibrary();
                    txtStatus.Text = "Playing (Info Loaded & Saved)";
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception) { txtStatus.Text = "API Error"; }
        }

        private void SlideshowTimer_Tick(object? sender, EventArgs e)
        {
            if (currentlyPlayingTrack != null &&
                lstLibrary.SelectedItem == currentlyPlayingTrack &&
                currentlyPlayingTrack.LocalImages != null &&
                currentlyPlayingTrack.LocalImages.Count > 1)
            {
                currentImageIndex = (currentImageIndex + 1) % currentlyPlayingTrack.LocalImages.Count;
                imgAlbumArt.Source = new BitmapImage(new Uri(currentlyPlayingTrack.LocalImages[currentImageIndex]));
            }
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (lstLibrary.SelectedItem is MusicTrack track)
            {
                int selectedIndex = lstLibrary.SelectedIndex;

                var vm = new EditSongViewModel(track);
                var editWin = new EditSongWindow { DataContext = vm, Owner = this };
                editWin.ShowDialog();

                UpdateLibraryUI();

                lstLibrary.SelectedIndex = selectedIndex;

                SaveLibrary();
                UpdateUIWithTrackData(track);
                txtCurrentSong.Text = track.Title;
            }
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (mediaPlayer.NaturalDuration.HasTimeSpan && !isDragging)
            {
                sliderProgress.Minimum = 0;
                sliderProgress.Maximum = mediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
                sliderProgress.Value = mediaPlayer.Position.TotalSeconds;
            }
        }

        private void BtnPlay_Click(object sender, RoutedEventArgs e)
        {
            if (lstLibrary.SelectedItem is MusicTrack track)
            {
                currentlyPlayingTrack = track;
                mediaPlayer.Play();
                timer.Start();

                if (track.LocalImages != null && track.LocalImages.Count > 0)
                {
                    currentImageIndex = 0;
                    imgAlbumArt.Source = new BitmapImage(new Uri(track.LocalImages[0]));
                }

                slideshowTimer.Start();
                txtStatus.Text = "Playing";
            }
        }

        private void BtnPause_Click(object sender, RoutedEventArgs e)
        {
            mediaPlayer.Pause();
            slideshowTimer.Stop();
            txtStatus.Text = "Paused";
        }

        private void SliderVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => mediaPlayer.Volume = sliderVolume.Value;

        private void SliderProgress_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) => isDragging = true;

        private void SliderProgress_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            isDragging = false;
            mediaPlayer.Position = TimeSpan.FromSeconds(sliderProgress.Value);
        }

        private void SliderProgress_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog { Filter = "MP3 files (*.mp3)|*.mp3" };
            if (openFileDialog.ShowDialog() == true)
            {
                var track = new MusicTrack { Title = Path.GetFileNameWithoutExtension(openFileDialog.FileName), FilePath = openFileDialog.FileName };
                library.Add(track);
                UpdateLibraryUI();
                SaveLibrary();
            }
        }

        private void BtnRemove_Click(object sender, RoutedEventArgs e)
        {
            if (lstLibrary.SelectedItem is MusicTrack track)
            {
                library.Remove(track);
                UpdateLibraryUI();
                SaveLibrary();

                txtCurrentSong.Text = "No Song Selected";
                txtArtist.Text = "";
                txtAlbum.Text = "";
                txtFilePath.Text = "";
                txtStatus.Text = "Ready";
                imgAlbumArt.Source = null;

                mediaPlayer.Stop();
                slideshowTimer.Stop();
            }
        }

        private void UpdateLibraryUI()
        {
            lstLibrary.ItemsSource = null;
            lstLibrary.ItemsSource = library;
        }

        private void SaveLibrary() => File.WriteAllText(FILE_NAME, JsonSerializer.Serialize(library));

        private void LoadLibrary()
        {
            if (File.Exists(FILE_NAME))
            {
                library = JsonSerializer.Deserialize<List<MusicTrack>>(File.ReadAllText(FILE_NAME)) ?? new List<MusicTrack>();
                UpdateLibraryUI();
            }
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            Settings settingsWin = new Settings();
            settingsWin.OnScanCompleted += (newTracks) => {
                foreach (var t in newTracks) if (!library.Any(x => x.FilePath == t.FilePath)) library.Add(t);
                UpdateLibraryUI();
                SaveLibrary();
            };
            settingsWin.ShowDialog();
        }

        private void MediaPlayer_MediaEnded(object? sender, EventArgs e)
        {
            currentlyPlayingTrack = null;

            mediaPlayer.Stop();
            mediaPlayer.Position = TimeSpan.Zero;

            slideshowTimer.Stop();
            timer.Stop();

            txtStatus.Text = "Finished";
            sliderProgress.Value = 0;

            if (lstLibrary.SelectedItem is MusicTrack track)
            {
                UpdateUIWithTrackData(track);
            }
        }
    }
}
