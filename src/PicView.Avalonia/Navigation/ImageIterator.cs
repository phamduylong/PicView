﻿using PicView.Avalonia.Keybindings;
using PicView.Avalonia.ViewModels;
using PicView.Core.Config;
using PicView.Core.FileHandling;
using PicView.Core.Navigation;
using System.Diagnostics;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ImageMagick;
using PicView.Avalonia.Gallery;
using PicView.Avalonia.ImageHandling;
using PicView.Avalonia.UI;
using PicView.Core.Gallery;
using Timer = System.Timers.Timer;

namespace PicView.Avalonia.Navigation;
public sealed class ImageIterator : IDisposable
{
    #region Properties

    private bool _disposed;

    public List<string> ImagePaths { get; private set; }

    public bool IsRenamingInProgress { get; set; }

    public int Index{ get; private set; }
    
    public FileInfo FileInfo{ get; private set; } = null!;
    public bool IsReversed { get; private set; }
    private PreLoader PreLoader { get; } = new();

    private static FileSystemWatcher? _watcher;
    private static bool _isRunning;
    private readonly MainViewModel? _vm;

    #endregion

    #region Constructors

    public ImageIterator(FileInfo fileInfo, MainViewModel vm)
    {
        ArgumentNullException.ThrowIfNull(fileInfo);
        _vm = vm;
        ImagePaths = vm.PlatformService.GetFiles(fileInfo);
        Index = Directory.Exists(fileInfo.FullName) ? 0 : ImagePaths.IndexOf(fileInfo.FullName);
        InitiateFileSystemWatcher(fileInfo);
    }
    
    public ImageIterator(FileInfo fileInfo, List<string> imagePaths, int index, MainViewModel vm)
    {
        ArgumentNullException.ThrowIfNull(fileInfo);
        _vm = vm;
        ImagePaths = imagePaths;
        Index = index;
        InitiateFileSystemWatcher(fileInfo);
    }

    #endregion

    #region File Watcher
    
    private void InitiateFileSystemWatcher(FileInfo fileInfo)
    {
        FileInfo = fileInfo;
        if (_watcher is not null)
        {
            _watcher.Dispose();
            _watcher = null;
        }
        _watcher = new FileSystemWatcher();
#if DEBUG
        Debug.Assert(fileInfo.DirectoryName != null, "fileInfo.DirectoryName != null");
#endif
        _watcher.Path = fileInfo.DirectoryName;
        _watcher.EnableRaisingEvents = true;
        _watcher.Filter = "*.*";
        _watcher.IncludeSubdirectories = SettingsHelper.Settings.Sorting.IncludeSubDirectories;
        _watcher.Created += async (_, e) => await OnFileAdded(e);
        _watcher.Deleted += async (_, e) => await OnFileDeleted(e);
        _watcher.Renamed += async (_, e) => await OnFileRenamed(e);
    }
    
    private async Task OnFileAdded(FileSystemEventArgs e)
    {
        if (IsRenamingInProgress)
        {
            return;
        }
        if (ImagePaths.Contains(e.FullPath))
        {
            return;
        }
        if (e.FullPath.IsSupported() == false)
        {
            return;
        }
        var fileInfo = new FileInfo(e.FullPath);
        if (fileInfo.Exists == false) { return; }
        
        var retries = 0;
        while (_isRunning && retries < 10)
        {
            await Task.Delay(200);
            retries++;
        }
        _isRunning = true;

        var newList = await Task.FromResult(_vm.PlatformService.GetFiles(fileInfo));
        if (newList.Count == 0) { return; }
        if (newList.Count == ImagePaths.Count) { return; }

        if (fileInfo.Exists == false) { return; }
        
        ImagePaths = newList;

        _isRunning = false;

        var index = ImagePaths.IndexOf(e.FullPath);
        if (index < 0) { return; }

        var nextIndex = index + 1;
        if (index >= ImagePaths.Count)
        {
            nextIndex = 0;
        }
        var prevIndex = index - 1;
        if (prevIndex < 0)
        {
            prevIndex = ImagePaths.Count - 1;
        }

        var cleared = false;
        if (PreLoader.Contains(index, ImagePaths) || PreLoader.Contains(nextIndex, ImagePaths) || PreLoader.Contains(prevIndex, ImagePaths))
        {
            PreLoader.Clear();
            cleared = true;
        }

        SetTitleHelper.SetTitle(_vm);
        
        await GalleryFunctions.AddGalleryItem(index, fileInfo, _vm);
        if (SettingsHelper.Settings.Gallery.IsBottomGalleryShown && ImagePaths.Count > 1)
        {
            if (_vm.GalleryMode is GalleryMode.BottomToClosed or GalleryMode.FullToClosed)
            {
                _vm.GalleryMode = GalleryMode.ClosedToBottom;
            }
        }
        _vm.SelectedGalleryItemIndex = Index; // Fixes deselection bug

        if (cleared)
        {
            await Preload();
        }
    }
    
    private async Task OnFileDeleted(FileSystemEventArgs e)
    {
        if (IsRenamingInProgress)
        {
            return;
        }
        if (e.FullPath.IsSupported() == false)
        {
            return;
        }

        if (ImagePaths.Contains(e.FullPath) == false)
        {
            return;
        }

        if (_isRunning) { return; }
        _isRunning = true;
        var index = ImagePaths.IndexOf(e.FullPath);
        var sameFile = Index == index;
        if (!ImagePaths.Remove(e.FullPath))
        {
            return;
        }
        
        if (sameFile)
        {
            if (ImagePaths.Count <= 0)
            {
                ErrorHandling.ShowStartUpMenu(_vm);
                return;
            }
            await NavigationHelper.Iterate(next:false, _vm);
        }
        else
        {
            SetTitleHelper.SetTitle(_vm);
        }
        
        PreLoader.Remove(index, ImagePaths);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            GalleryFunctions.RemoveGalleryItem(index, _vm);
        });
        if (SettingsHelper.Settings.Gallery.IsBottomGalleryShown)
        {
            if (ImagePaths.Count == 1)
            {
                _vm.GalleryMode = GalleryMode.BottomToClosed;
            }
        }

        //FileHistoryNavigation.Remove(e.FullPath);
        _isRunning = false;

        SetTitleHelper.SetTitle(_vm);
    }
    
    private async Task OnFileRenamed(RenamedEventArgs e)
    {
        if (IsRenamingInProgress)
        {
            return;
        }
        if (e.FullPath.IsSupported() == false)
        {
            if (ImagePaths.Contains(e.OldFullPath))
            {
                ImagePaths.Remove(e.OldFullPath);
            }
            return;
        }
        if (_isRunning) { return; }
        _isRunning = true;

        var oldIndex = ImagePaths.IndexOf(e.OldFullPath);

        var fileInfo = new FileInfo(e.FullPath);
        if (fileInfo.Exists == false) { return; }

        var newList = FileListHelper.RetrieveFiles(fileInfo).ToList();
        if (newList.Count == 0) { return; }

        if (fileInfo.Exists == false) { return; }
        
        ImagePaths = newList;

        var index = ImagePaths.IndexOf(e.FullPath);
        if (index < 0) { return; }

        if (fileInfo.Exists == false)
        {
            return;
        }
        
        SetTitleHelper.SetTitle(_vm);

        await PreLoader.RefreshFileInfo(oldIndex, ImagePaths);

        _isRunning = false;
        //FileHistoryNavigation.Rename(e.OldFullPath, e.FullPath);
        //await ImageInfo.UpdateValuesAsync(fileInfo).ConfigureAwait(false);
        GalleryFunctions.RemoveGalleryItem(oldIndex, _vm);
        await GalleryFunctions.AddGalleryItem(index,fileInfo, _vm);
        await GalleryFunctions.SortGalleryItems(ImagePaths, _vm);
    }

    #endregion

    #region Preloader

    public void Clear()
    {
        PreLoader.Clear();
    }
    
    public async Task Preload()
    {
        await PreLoader.PreLoadAsync(Index, ImagePaths.Count, IsReversed, ImagePaths).ConfigureAwait(false);
    }

    public async Task AddAsync(int index, ImageModel imageModel)
    {
        await PreLoader.AddAsync(index, ImagePaths, imageModel).ConfigureAwait(false);
    }

    public PreLoader.PreLoadValue? GetPreLoadValue(int index)
    {
        return PreLoader.Get(index, ImagePaths);
    }
    
    public PreLoader.PreLoadValue? GetCurrentPreLoadValue()
    {
        return PreLoader.Get(Index, ImagePaths);
    }
    
    public void RemoveItemFromPreLoader(int index)
    {
        PreLoader.Remove(index, ImagePaths);
    }
    
    public void RemoveCurrentItemFromPreLoader()
    {
        PreLoader.Remove(Index, ImagePaths);
    }
    
    #endregion
    
    #region Navigation
    
    public async Task ReloadFileList()
    {
        ImagePaths = await Task.FromResult(_vm.PlatformService.GetFiles(FileInfo)).ConfigureAwait(false);
        Index = ImagePaths.IndexOf(FileInfo.FullName);
        InitiateFileSystemWatcher(FileInfo);
    }

    public int GetIteration(int index, NavigateTo navigateTo)
    {
        int next;
        switch (navigateTo)
        {
            case NavigateTo.Next:
            case NavigateTo.Previous:
                var indexChange = navigateTo == NavigateTo.Next ? 1 : -1;
                IsReversed = navigateTo == NavigateTo.Previous;
                if (SettingsHelper.Settings.UIProperties.Looping)
                {
                    next = (index + indexChange + ImagePaths.Count) % ImagePaths.Count;
                }
                else
                {
                    var newIndex = index + indexChange;
                    if (newIndex < 0)
                    {
                        return 0;
                    }
                    if (newIndex >= ImagePaths.Count)
                    {
                        return ImagePaths.Count - 1;
                    }
                    next = newIndex;
                }

                break;

            case NavigateTo.First:
            case NavigateTo.Last:
                if (ImagePaths.Count > PreLoader.MaxCount)
                    PreLoader.Clear();
                next = navigateTo == NavigateTo.First ? 0 : ImagePaths.Count - 1;
                break;

            default: return -1;
        }
        return next;
    }

    public async Task NextIteration(NavigateTo navigateTo)
    {
        var index = GetIteration(Index, navigateTo);
        if (index < 0)
        {
            return;
        }

        if (!MainKeyboardShortcuts.IsKeyHeldDown)
        {
            await IterateToIndex(index);
        }
        else
        {
            await TimerIteration(index);
        }
    }

    public async Task IterateToIndex(int index)
    {
        if (index < 0 || index >= ImagePaths.Count)
        {
            ErrorHandling.ShowStartUpMenu(_vm);
            return;
        }
        try
        {
            Index = index;

            var preLoadValue = PreLoader.Get(index, ImagePaths);
            if (preLoadValue is not null)
            {
                var showThumb = true;
                while (preLoadValue.IsLoading)
                {
                    if (showThumb)
                    {
                        await LoadingPreview(index);
                        if (Index != index)
                        {
                            return;
                        }
                        showThumb = false;
                    }
                    
                    await Task.Delay(20);
                    if (Index != index)
                    {
                        return;
                    }
                }
            }
            else
            {
                await LoadingPreview(index);
                var added = await PreLoader.AddAsync(index, ImagePaths);
                if (Index != index)
                {
                    // Skip loading if user went to next value
                    return;
                }
                if (added)
                {
                    preLoadValue = PreLoader.Get(index, ImagePaths);
                }
            }

            if (Index != index || preLoadValue is null)
            {
                return;
            }

            UpdateSource(preLoadValue);
        }
        catch (OperationCanceledException)
        {
#if DEBUG
            Console.WriteLine($"{nameof(IterateToIndex)} canceled at index {index}");
#endif
        }
        catch (Exception e)
        {
#if DEBUG
            TooltipHelper.ShowTooltipMessage(e.Message);
#endif
        }
        finally
        {
            _vm.IsLoading = false;
        }
    }

    private static Timer? _timer;

    internal async Task TimerIteration(int index)
    {
        if (_timer is null)
        {
            _timer = new Timer(TimeSpan.FromSeconds(SettingsHelper.Settings.UIProperties.NavSpeed))
            {
                AutoReset = false,
                Enabled = true
            };
        }
        else if (_timer.Enabled)
        {
            if (!MainKeyboardShortcuts.IsKeyHeldDown)
            {
                _timer = null;
            }
            return;
        }
        _timer?.Start();
        Index = index;
        var preLoadValue = PreLoader.Get(index, ImagePaths);

        if (preLoadValue != null)
        {
            if (preLoadValue.IsLoading)
            {
                SetTitleHelper.SetLoadingTitle(_vm);
                await Task.Delay(250);
                if (Index != index)
                {
                    return;
                }

                if (preLoadValue.IsLoading)
                {
                    await LoadingPreview(index);
                }

                var x = 0;
                while (preLoadValue.IsLoading)
                {
                    await Task.Delay(200);
                    x++;
                    if (x > 10)
                    {
                        break;
                    }
                }
            }
        }
        else
        {
            await LoadingPreview(index);
            await PreLoader.AddAsync(index, ImagePaths).ConfigureAwait(false);
            preLoadValue = PreLoader.Get(index, ImagePaths);
            if (preLoadValue is null)
            {
                return;
            }
        }
        UpdateSource(preLoadValue);
    }
    
    public void UpdateFileListAndIndex(List<string> fileList, int index)
    {
        ImagePaths = fileList;
        Index = index;
    }
    
    #endregion

    #region Update Source and Preview

    public void UpdateSource(PreLoader.PreLoadValue preLoadValue)
    {
        _vm.IsLoading = false;
        ExifHandling.SetImageModel(preLoadValue.ImageModel, vm: _vm);
        _vm.ImageSource = preLoadValue.ImageModel.Image;
        _vm.ImageType = preLoadValue.ImageModel.ImageType;
        WindowHelper.SetSize(preLoadValue.ImageModel.PixelWidth, preLoadValue.ImageModel.PixelHeight, preLoadValue.ImageModel.Rotation, _vm);
        if (_vm.RotationAngle != 0)
        {
            _vm.ImageViewer.Rotate(_vm.RotationAngle);
        }
        SetTitleHelper.SetTitle(_vm, preLoadValue.ImageModel);
        _vm.GetIndex = Index + 1;
        if (SettingsHelper.Settings.WindowProperties.KeepCentered)
        {
            WindowHelper.CenterWindowOnScreen(false);
        }

        if (_vm.SelectedGalleryItemIndex != Index)
        {
            _vm.SelectedGalleryItemIndex = Index;
            if (GalleryFunctions.IsBottomGalleryOpen)
            {
                GalleryNavigation.CenterScrollToSelectedItem(_vm);
            }
        }
        TooltipHelper.CloseToolTipMessage();

        ExifHandling.UpdateExifValues(preLoadValue.ImageModel, vm: _vm);
        _ = AddAsync(Index, preLoadValue.ImageModel);
        _ = Preload();
    }
    
    public async Task LoadingPreview(int index)
    {
        if (index != Index)
        {
            return;
        }
        SetTitleHelper.SetLoadingTitle(_vm);
        _vm.SelectedGalleryItemIndex = index;
        if (GalleryFunctions.IsBottomGalleryOpen)
        {
            GalleryNavigation.CenterScrollToSelectedItem(_vm);
        }
        using var image = new MagickImage();
        image.Ping(_vm.ImageIterator.ImagePaths[index]);
        var thumb = image.GetExifProfile()?.CreateThumbnail();
        if (thumb is null)
        {
            await Set();
            return;
        }

        var byteArray = thumb.ToByteArray();
        if (byteArray is null)
        {
            await Set();
            return;
        }
        var stream = new MemoryStream(byteArray);
        if (index != Index)
        {
            return;
        }

        _vm.ImageSource = new Bitmap(stream);
        _vm.ImageType = ImageType.Bitmap;
        WindowHelper.SetSize(image?.Width ?? 0, image?.Height ?? 0, 0, _vm);
        return;

        async Task Set()
        {
            if (index == Index)
            {
                _vm.IsLoading = true;
                await Dispatcher.UIThread.InvokeAsync(() =>  _vm.ImageViewer.MainImage.Source = null);
            }
        }
    }

    #endregion
    
    #region IDisposable

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            _watcher?.Dispose();
            Clear();
            _timer?.Dispose();
        }

        _disposed = true;
    }

    ~ImageIterator()
    {
        Dispose(false);
    }
    
    #endregion
}
