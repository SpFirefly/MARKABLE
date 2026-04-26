/*
 * Copyright (c) 2026 SpFirefly
 * Licensed under the MIT License.
 * 本项目由SpFirefly开发，保留所有著作权。
 */
using Microsoft.Win32;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace MARKABLE
{
    public partial class MainWindow : Window
    {
        //数据
        private List<PhotoInfo> _allPhotos = new();
        private List<PhotoInfo> _photos = new();
        private List<string> _filters = new();
        private int _currentIndex = -1;
        private int _currentRating = 0;

        //缓存、并发
        private readonly ConcurrentDictionary<string, BitmapImage> _thumbCache = new();
        private readonly SemaphoreSlim _thumbSemaphore = new(4);  // 同时解码4张
        private readonly SemaphoreSlim _highResSemaphore = new(2); // 同时解码2张大图

        public MainWindow()
        {
            InitializeComponent();
            this.PreviewKeyDown += MainWindow_PreviewKeyDown;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Normal ? WindowState.Maximized : WindowState.Normal;
            }
            else
            {
                DragMove();
            }
        }

        private void MinimizeWindow_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void MaximizeWindow_Click(object sender, RoutedEventArgs e) =>
            WindowState = WindowState == WindowState.Normal ? WindowState.Maximized : WindowState.Normal;

        private void CloseWindow_Click(object sender, RoutedEventArgs e) => Close();
        private async void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog { Title = "选择文件夹" };
            if (dialog.ShowDialog() == true)
            {
                await LoadPhotosFromFolderAsync(dialog.FolderName);
            }
        }
        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_photos.Count == 0) return;

            switch (e.Key)
            {
                case Key.Left:
                    PrevImage_Click(null, null);
                    break;
                case Key.Right:
                    NextImage_Click(null, null);
                    break;
                case Key.D1:
                case Key.NumPad1:
                    SetRating(1);
                    break;
                case Key.D2:
                case Key.NumPad2:
                    SetRating(2);
                    break;
                case Key.D3:
                case Key.NumPad3:
                    SetRating(3);
                    break;
                case Key.D4:
                case Key.NumPad4:
                    SetRating(4);
                    break;
                case Key.D5:
                case Key.NumPad5:
                    SetRating(5);
                    break;
                case Key.D0:
                case Key.Delete:
                    ClearRating_Click(null, null);
                    break;
            }
        }

        private void SetRating(int rating)
        {
            if (_currentIndex < 0) return;
            _currentRating = rating;
            UpdateStarDisplay(rating);
            SaveRating(_photos[_currentIndex].Path, rating);
        }
        private void FilterExtension_Click(object sender, RoutedEventArgs e)
        {
            if (!_allPhotos.Any())
            {
                MessageBox.Show("先打开文件夹再筛选", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var exts = _allPhotos.Select(p => Path.GetExtension(p.Path).ToLower()).Distinct().OrderBy(x => x).ToList();

            var win = new Window
            {
                Title = "筛选扩展名",
                Width = 280,
                Height = 350,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ShowInTaskbar = false
            };

            var panel = new StackPanel { Margin = new Thickness(12) };
            panel.Children.Add(new TextBlock { Text = "显示格式：", Margin = new Thickness(0, 0, 0, 10), FontWeight = FontWeights.Bold });

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            var selectAll = new Button { Content = "全选", Width = 60, Margin = new Thickness(0, 0, 5, 0) };
            var clearAll = new Button { Content = "清空", Width = 60 };
            btnPanel.Children.Add(selectAll);
            btnPanel.Children.Add(clearAll);
            panel.Children.Add(btnPanel);

            var listBox = new ListBox { Height = 200 };
            var checks = new List<CheckBox>();
            foreach (var ext in exts)
            {
                var cb = new CheckBox { Content = ext.ToUpper(), Tag = ext, IsChecked = _filters.Contains(ext), Margin = new Thickness(5, 2, 0, 2) };
                checks.Add(cb);
                listBox.Items.Add(cb);
            }
            panel.Children.Add(listBox);

            selectAll.Click += (_, _) => checks.ForEach(c => c.IsChecked = true);
            clearAll.Click += (_, _) => checks.ForEach(c => c.IsChecked = false);

            var okBtn = new Button { Content = "确定", Width = 70, Margin = new Thickness(0, 0, 10, 0) };
            var cancelBtn = new Button { Content = "取消", Width = 70 };
            var actionPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 15, 0, 0) };
            actionPanel.Children.Add(okBtn);
            actionPanel.Children.Add(cancelBtn);
            panel.Children.Add(actionPanel);

            win.Content = panel;

            okBtn.Click += (_, _) =>
            {
                _filters = checks.Where(c => c.IsChecked == true).Select(c => c.Tag.ToString()).ToList();
                ApplyFilter();
                win.Close();
            };
            cancelBtn.Click += (_, _) => win.Close();

            win.ShowDialog();
        }

        private void SupportAuthor_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("作者：SpFirefly https://github.com/SpFirefly", "支持作者", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void PrevImage_Click(object sender, RoutedEventArgs e)
        {
            if (!_photos.Any()) return;
            _currentIndex--;
            if (_currentIndex < 0) _currentIndex = _photos.Count - 1;
            _ = LoadCurrentPhotoAsync();
            SyncListSelection();
        }

        private void NextImage_Click(object sender, RoutedEventArgs e)
        {
            if (!_photos.Any()) return;
            _currentIndex++;
            if (_currentIndex >= _photos.Count) _currentIndex = 0;
            _ = LoadCurrentPhotoAsync();
            SyncListSelection();
        }

        private void Star_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null || _currentIndex < 0) return;
            _currentRating = int.Parse(btn.Tag.ToString());
            UpdateStarDisplay(_currentRating);
            SaveRating(_photos[_currentIndex].Path, _currentRating);
        }

        private void ClearRating_Click(object sender, RoutedEventArgs e)
        {
            if (_currentIndex < 0) return;
            _currentRating = 0;
            UpdateStarDisplay(0);
            ClearRating(_photos[_currentIndex].Path);
        }

        private void PhotoListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PhotoListBox.SelectedItem is PhotoInfo photo)
            {
                int newIdx = _photos.IndexOf(photo);
                if (newIdx != _currentIndex)
                {
                    _currentIndex = newIdx;
                    _ = LoadCurrentPhotoAsync();
                }
            }
        }

        // 核心 core 

        private async Task LoadPhotosFromFolderAsync(string folder)
        {
            // 后台 thread_scan
            var files = await Task.Run(() =>
            {
                var exts = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
                return Directory.GetFiles(folder)
                    .Where(f => exts.Contains(Path.GetExtension(f).ToLower()))
                    .Select(f => new PhotoInfo { Path = f, Name = Path.GetFileName(f) })
                    .ToList();
            });

            if (!files.Any())
            {
                await Dispatcher.InvokeAsync(() =>
                    MessageBox.Show("没有找到支持的图片", "提示", MessageBoxButton.OK, MessageBoxImage.Information));
                return;
            }

            _allPhotos = files;
            _filters.Clear();
            _thumbCache.Clear();

            ApplyFilter();

            // 异步 缩略图 后台preload
            _ = Task.Run(() => LoadAllThumbnailsAsync());
        }

        private async Task LoadAllThumbnailsAsync()
        {
            foreach (var photo in _allPhotos)
            {
                await LoadThumbnailAsync(photo);
            }
        }

        private async Task LoadThumbnailAsync(PhotoInfo photo)
        {
            if (photo.Thumbnail != null) return;

            if (_thumbCache.TryGetValue(photo.Path, out var cached))
            {
                await Dispatcher.InvokeAsync(() => photo.Thumbnail = cached);
                return;
            }

            await _thumbSemaphore.WaitAsync();
            try
            {
                if (_thumbCache.TryGetValue(photo.Path, out cached))
                {
                    await Dispatcher.InvokeAsync(() => photo.Thumbnail = cached);
                    return;
                }

                var thumb = await Task.Run(() => DecodeThumbnail(photo.Path, 50));
                if (thumb != null)
                {
                    _thumbCache[photo.Path] = thumb;
                    await Dispatcher.InvokeAsync(() => photo.Thumbnail = thumb);
                }
            }
            finally
            {
                _thumbSemaphore.Release();
            }
        }

        //缩略图
        private BitmapImage DecodeThumbnail(string path, int size)
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(path);
                bitmap.DecodePixelWidth = size;
                bitmap.DecodePixelHeight = size;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        // 大图
        private BitmapImage DecodeImage(string path, int targetWidth)
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(path);
                bitmap.DecodePixelWidth = targetWidth;
                // 不设 DecodePixelHeight
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        private void ApplyFilter()
        {
            _photos = _filters.Any()
                ? _allPhotos.Where(p => _filters.Contains(Path.GetExtension(p.Path).ToLower())).ToList()
                : _allPhotos.ToList();

            PhotoListBox.ItemsSource = null;
            PhotoListBox.ItemsSource = _photos;

            if (_photos.Any())
            {
                _currentIndex = 0;
                _ = LoadCurrentPhotoAsync();
                PhotoListBox.SelectedIndex = 0;
                Title = _filters.Any()
                    ? $"MARKABLE - 筛选: {string.Join(", ", _filters)} ({_photos.Count}/{_allPhotos.Count})"
                    : $"MARKABLE - {_photos.Count} 张照片";
            }
            else
            {
                PreviewImage.Source = null;
                FileInfoText.Text = "没有符合条件的照片";
                Title = "MARKABLE - 空";
            }
        }

        private async Task LoadCurrentPhotoAsync()
        {
            if (_currentIndex < 0 || _currentIndex >= _photos.Count) return;

            var photo = _photos[_currentIndex];

            // 显示缩略图占位
            await Dispatcher.InvokeAsync(() =>
            {
                PreviewImage.Source = photo.Thumbnail;
                FileInfoText.Text = $"{photo.Name} ({_currentIndex + 1}/{_photos.Count})";
            });

            // 异步Load 高清缩略图
            await _highResSemaphore.WaitAsync();
            try
            {
                var highRes = await Task.Run(() => DecodeImage(photo.Path, 1200));
                if (highRes != null)
                {
                    await Dispatcher.InvokeAsync(() => PreviewImage.Source = highRes);
                }
            }
            finally
            {
                _highResSemaphore.Release();
            }

            LoadRating(photo.Path);
        }

        private void SyncListSelection()
        {
            if (PhotoListBox.SelectedIndex != _currentIndex)
                Dispatcher.InvokeAsync(() => PhotoListBox.SelectedIndex = _currentIndex);
        }

        //TagLib操作 

        private void LoadRating(string path)
        {
            try
            {
                using var tag = TagLib.File.Create(path);
                var img = tag as TagLib.Image.File;
                var rating = (int)(img?.ImageTag?.Rating ?? 0);
                Dispatcher.InvokeAsync(() =>
                {
                    _currentRating = rating;
                    UpdateStarDisplay(rating);
                });
            }
            catch
            {
                Dispatcher.InvokeAsync(() =>
                {
                    _currentRating = 0;
                    UpdateStarDisplay(0);
                });
            }
        }

        private void SaveRating(string path, int rating)
        {
            try
            {
                using var tag = TagLib.File.Create(path);
                var img = tag as TagLib.Image.File;
                if (img?.ImageTag != null)
                {
                    img.ImageTag.Rating = (uint)rating;
                    tag.Save();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存失败: {ex.Message}");
            }
        }

        private void ClearRating(string path)
        {
            try
            {
                using var tag = TagLib.File.Create(path);
                var img = tag as TagLib.Image.File;
                if (img?.ImageTag != null)
                {
                    img.ImageTag.Rating = null;
                    tag.Save();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"清除失败: {ex.Message}");
            }
        }

        private void UpdateStarDisplay(int rating)
        {
            var stars = new[] { Star1, Star2, Star3, Star4, Star5 };
            for (int i = 0; i < stars.Length; i++)
            {
                if (stars[i] != null)
                    stars[i].Content = i < rating ? "★" : "☆";
            }
        }
    }

    //PhotoInfo 数据类
    public class PhotoInfo : System.ComponentModel.INotifyPropertyChanged
    {
        private BitmapImage _thumbnail;

        public string Path { get; set; }
        public string Name { get; set; }

        public BitmapImage Thumbnail
        {
            get => _thumbnail;
            set
            {
                _thumbnail = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Thumbnail)));
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    }
}