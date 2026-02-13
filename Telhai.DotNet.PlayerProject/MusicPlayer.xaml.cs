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

        // שירות ה-API וביטול קריאות (דרישת המטלה)
        private readonly ItunesService _itunesService = new ItunesService();
        private CancellationTokenSource? _cts;

        public MusicPlayer()
        {
            InitializeComponent();
            timer.Interval = TimeSpan.FromMilliseconds(500);
            timer.Tick += new EventHandler(Timer_Tick);
            this.Loaded += MusicPlayer_Loaded;
        }

        private void MusicPlayer_Loaded(object sender, RoutedEventArgs e)
        {
            this.LoadLibrary();
        }

        // דרישה: לחיצה רגילה מציגה שם ומסלול קובץ
        private void LstLibrary_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstLibrary.SelectedItem is MusicTrack track)
            {
                txtCurrentSong.Text = track.Title;
                txtFilePath.Text = track.FilePath;

                // איפוס שדות ה-API בבחירה חדשה כדי למנוע בלבול
                txtArtist.Text = "";
                txtAlbum.Text = "";
                imgAlbumArt.Source = new BitmapImage(new Uri("https://via.placeholder.com/180?text=Selected"));
            }
        }

        // דרישה: לחיצה כפולה מנגנת ומפעילה API אסינכרוני
        private async void LstLibrary_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (lstLibrary.SelectedItem is MusicTrack track)
            {
                PlayTrack(track);

                // ביטול קריאה קודמת (דרישה: CancellationToken)
                _cts?.Cancel();
                _cts = new CancellationTokenSource();

                // הפעלת API במקביל לניגון (ללא חסימת ממשק)
                await LoadSongExtraInfoAsync(track.Title, _cts.Token);
            }
        }

        private void PlayTrack(MusicTrack track)
        {
            mediaPlayer.Open(new Uri(track.FilePath));
            mediaPlayer.Play();
            timer.Start();
            txtStatus.Text = "Playing";
        }

        private async Task LoadSongExtraInfoAsync(string songTitle, CancellationToken token)
        {
            try
            {
                txtStatus.Text = "Searching Info...";

                // קריאה לשירות ה-API
                var info = await _itunesService.SearchOneAsync(songTitle, token);

                if (info != null)
                {
                    txtArtist.Text = info.ArtistName;
                    txtAlbum.Text = info.AlbumName;
                    if (!string.IsNullOrEmpty(info.ArtworkUrl))
                    {
                        imgAlbumArt.Source = new BitmapImage(new Uri(info.ArtworkUrl));
                    }
                    txtStatus.Text = "Playing (Info Loaded)";
                }
                else
                {
                    txtStatus.Text = "No API Info Found";
                    imgAlbumArt.Source = new BitmapImage(new Uri("https://via.placeholder.com/180?text=No+Info"));
                }
            }
            catch (OperationCanceledException)
            {
                // קריאה בוטלה עקב מעבר שיר - אין צורך בפעולה
            }
            catch (Exception)
            {
                txtStatus.Text = "API Error";
            }
        }

        // --- פונקציות קיימות בנגן ---

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
            if (lstLibrary.SelectedItem != null)
            {
                mediaPlayer.Play();
                timer.Start();
                txtStatus.Text = "Playing";
            }
        }

        private void BtnPause_Click(object sender, RoutedEventArgs e)
        {
            mediaPlayer.Pause();
            txtStatus.Text = "Paused";
        }

        private void SliderVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            mediaPlayer.Volume = sliderVolume.Value;
        }

        private void SliderProgress_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) => isDragging = true;

        private void SliderProgress_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            isDragging = false;
            mediaPlayer.Position = TimeSpan.FromSeconds(sliderProgress.Value);
        }

        private void SliderProgress_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { }

        // --- ניהול ספרייה ---

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
    }
}