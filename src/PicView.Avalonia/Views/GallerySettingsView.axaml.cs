﻿using Avalonia.Controls;
using Avalonia.Media;
using PicView.Avalonia.Gallery;
using PicView.Avalonia.ViewModels;
using PicView.Core.Config;

namespace PicView.Avalonia.Views;

public partial class GalleryView : UserControl
{
    public GalleryView()
    {
        InitializeComponent();

        Loaded += (_, _) =>
        {
            if (DataContext is not MainViewModel vm)
            {
                return;
            }

            if (vm.IsUniformFullChecked)
            {
                FullGalleryComboBox.SelectedIndex = 0;
            }
            else if (vm.IsUniformToFillFullChecked)
            {
                FullGalleryComboBox.SelectedIndex = 1;
            }
            else if (vm.IsFillFullChecked)
            {
                FullGalleryComboBox.SelectedIndex = 2;
            }
            else if (vm.IsNoneFullChecked)
            {
                FullGalleryComboBox.SelectedIndex = 3;
            }
            else if (vm.IsSquareFullChecked)
            {
                FullGalleryComboBox.SelectedIndex = 4;
            }
            else if (vm.IsFillSquareFullChecked)
            {
                FullGalleryComboBox.SelectedIndex = 5;
            }
            else
            {
                if (SettingsHelper.Settings.Gallery.FullGalleryStretchMode.Equals("Square",
                        StringComparison.OrdinalIgnoreCase))
                {
                    FullGalleryComboBox.SelectedIndex = 4;
                }
                else if (SettingsHelper.Settings.Gallery.FullGalleryStretchMode.Equals("FillSquare",
                             StringComparison.OrdinalIgnoreCase))
                {
                    FullGalleryComboBox.SelectedIndex = 5;
                }
                else if (Enum.TryParse<Stretch>(SettingsHelper.Settings.Gallery.FullGalleryStretchMode,
                             out var stretchMode))
                {
                    FullGalleryComboBox.SelectedIndex = stretchMode switch
                    {
                        Stretch.Uniform => 0,
                        Stretch.UniformToFill => 1,
                        Stretch.Fill => 2,
                        Stretch.None => 3,
                        _ => FullGalleryComboBox.SelectedIndex
                    };
                }
            }

            if (vm.IsUniformBottomChecked)
            {
                BottomGalleryComboBox.SelectedIndex = 0;
            }
            else if (vm.IsUniformToFillBottomChecked)
            {
                BottomGalleryComboBox.SelectedIndex = 1;
            }
            else if (vm.IsFillBottomChecked)
            {
                BottomGalleryComboBox.SelectedIndex = 2;
            }
            else if (vm.IsNoneBottomChecked)
            {
                BottomGalleryComboBox.SelectedIndex = 3;
            }
            else if (vm.IsSquareBottomChecked)
            {
                BottomGalleryComboBox.SelectedIndex = 4;
            }
            else if (vm.IsFillSquareBottomChecked)
            {
                BottomGalleryComboBox.SelectedIndex = 5;
            }
            else
            {
                if (SettingsHelper.Settings.Gallery.BottomGalleryStretchMode.Equals("Square",
                        StringComparison.OrdinalIgnoreCase))
                {
                    BottomGalleryComboBox.SelectedIndex = 4;
                }
                else if (SettingsHelper.Settings.Gallery.BottomGalleryStretchMode.Equals("FillSquare",
                             StringComparison.OrdinalIgnoreCase))
                {
                    BottomGalleryComboBox.SelectedIndex = 5;
                }
                else if (Enum.TryParse<Stretch>(SettingsHelper.Settings.Gallery.BottomGalleryStretchMode,
                             out var stretchMode))
                {
                    BottomGalleryComboBox.SelectedIndex = stretchMode switch
                    {
                        Stretch.Uniform => 0,
                        Stretch.UniformToFill => 1,
                        Stretch.Fill => 2,
                        Stretch.None => 3,
                        _ => FullGalleryComboBox.SelectedIndex
                    };
                }
            }

            FullGalleryComboBox.SelectionChanged += async (_, _) => await FullGalleryComboBox_SelectionChanged();
            BottomGalleryComboBox.SelectionChanged += async (_, _) => await BottomGalleryComboBox_SelectionChanged();
        };
    }

    private async Task FullGalleryComboBox_SelectionChanged()
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        if (FullGalleryUniformItem.IsSelected)
        {
            await GalleryStretchMode.ChangeFullGalleryItemStretch(vm, Stretch.Uniform);
        }
        else if (FullGalleryUniformToFillItem.IsSelected)
        {
            await GalleryStretchMode.ChangeFullGalleryItemStretch(vm, Stretch.UniformToFill);
        }
        else if (FullGalleryFillItem.IsSelected)
        {
            await GalleryStretchMode.ChangeFullGalleryItemStretch(vm, Stretch.Fill);
        }
        else if (FullGalleryNoneItem.IsSelected)
        {
            await GalleryStretchMode.ChangeFullGalleryItemStretch(vm, Stretch.None);
        }
        else if (FullGallerySquareItem.IsSelected)
        {
            await GalleryStretchMode.ChangeFullGalleryStretchSquare(vm);
        }
        else if (FullGalleryFillSquareItem.IsSelected)
        {
            await GalleryStretchMode.ChangeFullGalleryStretchSquareFill(vm);
        }
    }

    private async Task BottomGalleryComboBox_SelectionChanged()
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        if (BottomGalleryUniformItem.IsSelected)
        {
            await GalleryStretchMode.ChangeBottomGalleryItemStretch(vm, Stretch.Uniform);
        }
        else if (BottomGalleryUniformToFillItem.IsSelected)
        {
            await GalleryStretchMode.ChangeBottomGalleryItemStretch(vm, Stretch.UniformToFill);
        }
        else if (BottomGalleryFillItem.IsSelected)
        {
            await GalleryStretchMode.ChangeBottomGalleryItemStretch(vm, Stretch.Fill);
        }
        else if (BottomGalleryNoneItem.IsSelected)
        {
            await GalleryStretchMode.ChangeBottomGalleryItemStretch(vm, Stretch.None);
        }
        else if (BottomGallerySquareItem.IsSelected)
        {
            await GalleryStretchMode.ChangeBottomGalleryStretchSquare(vm);
        }
        else if (BottomGalleryFillSquareItem.IsSelected)
        {
            await GalleryStretchMode.ChangeBottomGalleryStretchSquareFill(vm);
        }
    }
}