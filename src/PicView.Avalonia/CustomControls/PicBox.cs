﻿using System.Numerics;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Automation.Peers;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Utilities;
using PicView.Avalonia.Navigation;
using ReactiveUI;
using Avalonia.Media.Imaging;
using Avalonia.Rendering.Composition;
using Avalonia.Svg.Skia;
using ImageMagick;
using PicView.Avalonia.AnimatedImage;
using PicView.Avalonia.UI;
using PicView.Avalonia.ViewModels;
using PicView.Core.FileHandling;
using Vector = Avalonia.Vector;


namespace PicView.Avalonia.CustomControls;
public class PicBox : Control
{
    #region Fields and Properties
    
    private CompositionCustomVisual? _customVisual;
    private readonly IDisposable? _imageTypeSubscription;
    private FileStream? _stream;
    private IGifInstance? _animInstance;
    public string? InitialAnimatedSource;

    private static readonly Lock Lock = new();
    
    /// <summary>
    /// Defines the <see cref="Source"/> property.
    /// </summary>
    public static readonly StyledProperty<object?> SourceProperty =
        AvaloniaProperty.Register<PicBox, object?>(nameof(Source));

    /// <summary>
    /// Gets or sets the image that will be displayed.
    /// </summary>
    [Content]
    public object? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>
    /// Defines the <see cref="SecondarySource"/> property.
    /// </summary>
    public static readonly StyledProperty<object?> SecondarySourceProperty =
        AvaloniaProperty.Register<PicBox, object?>(nameof(SecondarySource));

    /// <summary>
    /// Gets or sets the second image that will be displayed, when side by side view is enabled
    /// </summary>
    [Content]
    public object? SecondarySource
    {
        get => GetValue(SecondarySourceProperty);
        set => SetValue(SecondarySourceProperty, value);
    }

    /// <summary>
    /// Defines the <see cref="ImageType"/> property.
    /// </summary>
    public static readonly AvaloniaProperty<ImageType> ImageTypeProperty =
        AvaloniaProperty.Register<PicBox, ImageType>(nameof(ImageType));

    /// <summary>
    /// Gets or sets the image type.
    /// Determines if <see cref="Source"/> is an animated image, scalable vector graphics (SVG) or raster image.
    /// </summary>
    public ImageType ImageType
    {
        get => (ImageType)(GetValue(ImageTypeProperty) ?? false);
        set => SetValue(ImageTypeProperty, value);
    }
    
    #endregion
    
    #region Constructors
    
    static PicBox()
    {
        // Registers the SourceProperty to render when the source changes
        AffectsRender<PicBox>(SourceProperty);
    }
    public PicBox()
    {
        _imageTypeSubscription = this.WhenAnyValue(x => x.ImageType)
            .Subscribe(UpdateSource);
    }

    #endregion

    #region Rendering

    /// <summary>
    /// Renders the control.
    /// </summary>
    /// <param name="context">The drawing context.</param>
    public sealed override void Render(DrawingContext context)
    {
        base.Render(context);

        switch (Source)
        {
            case IImage source:
                RenderBasedOnSettings(context, source);
                RenderAnimatedImageIfRequired(context);
                break;
            case string svg:
            {
                RenderSvgImage(context, svg);
                break;
            }
            default:
                // Handle invalid source or log error
                if (Source is null)
                {
                    return;
                }
#if DEBUG
                Console.WriteLine("Invalid source type.");
#endif
                break;
        }
    }

    private void RenderSvgImage(DrawingContext context, string svg)
    {
        var svgSource = SvgSource.Load(svg);
        var svgImage = new SvgImage { Source = svgSource };
        RenderBasedOnSettings(context, svgImage);
    }

    private void RenderAnimatedImageIfRequired(DrawingContext context)
    {
        if (ImageType is not (ImageType.AnimatedGif or ImageType.AnimatedWebp))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(InitialAnimatedSource))
        {
            return;
        }

        context.Dispose(); // Fixes transparent images
        _stream = new FileStream(InitialAnimatedSource, FileMode.Open, FileAccess.Read);
        UpdateAnimationInstance(_stream);
        AnimationUpdate();
    }

    private void RenderBasedOnSettings(DrawingContext context, IImage source)
    {
        var viewPort = DetermineViewPort();
        Size sourceSize;
        try
        {
            sourceSize = source.Size;
        }
        catch (Exception e)
        {
            // https://github.com/AvaloniaUI/Avalonia/issues/8515
#if DEBUG
            Console.WriteLine(e);
#endif
            if (DataContext is not MainViewModel vm)
            {
                return;
            }

            var preloadValue = vm.ImageIterator?.GetCurrentPreLoadValue();
            if (preloadValue?.ImageModel != null)
            {
                sourceSize = new Size(preloadValue.ImageModel.PixelWidth, preloadValue.ImageModel.PixelHeight);
            }
            else
            {
                if (vm.FileInfo is not null)
                {
                    using var magickImage = new MagickImage();
                    magickImage.Ping(vm.FileInfo);
                    sourceSize = new Size(magickImage.Width, magickImage.Height);
                }
                else return;
            }
        }
    
        var is1To1 = false; // TODO: replace with settings value
        var isSideBySide = false; // TODO: replace with settings value
    
        if (is1To1)
        {
            RenderImage1To1(context, source, viewPort, sourceSize);
        }
        else if (isSideBySide)
        {
            if (SecondarySource is IImage secondarySource)
            {
                RenderImageSideBySide(context, source, secondarySource, viewPort);
            }
            else
            {
                // Handle invalid secondary source
#if DEBUG
                Console.WriteLine("Invalid secondary source type.");
                TooltipHelper.ShowTooltipMessage("Invalid secondary source type.", true);
#endif
            }
        }
        else
        {
            RenderImage(context, source, viewPort, sourceSize);
        }
    }

    private void RenderImage1To1(DrawingContext context, IImage source, Rect viewPort, Size sourceSize)
    {
        var scale = 1.0;
        var scaledSize = sourceSize * scale;
        var destRect = viewPort.CenterRect(new Rect(scaledSize)).Intersect(viewPort);
        var sourceRect = new Rect(sourceSize).CenterRect(new Rect(destRect.Size / scale));

        context.DrawImage(source, sourceRect, destRect);
    }

    private void RenderImage(DrawingContext context, IImage source, Rect viewPort, Size sourceSize)
    {
        var scale = CalculateScaling(viewPort.Size, sourceSize);
        var scaledSize = sourceSize * scale;
        var destRect = viewPort.CenterRect(new Rect(scaledSize)).Intersect(viewPort);
        var sourceRect = new Rect(sourceSize).CenterRect(new Rect(destRect.Size / scale));

        context.DrawImage(source, sourceRect, destRect);
    }

    private void RenderImageSideBySide(DrawingContext context, IImage source, IImage secondarySource, Rect viewPort)
    {
        // Get the aspect ratios of the images
        var sourceAspectRatio = source.Size.Width / source.Size.Height;
        var secondarySourceAspectRatio = secondarySource.Size.Width / secondarySource.Size.Height;

        // Calculate the width for each image
        var halfViewportWidth = viewPort.Width / 2;

        // Calculate heights based on the aspect ratio
        var sourceHeight = halfViewportWidth / sourceAspectRatio;
        var secondarySourceHeight = halfViewportWidth / secondarySourceAspectRatio;

        // Calculate the rectangles for each image
        var sourceRect = new Rect(0, 0, halfViewportWidth, sourceHeight);
        var secondarySourceRect = new Rect(halfViewportWidth, 0, halfViewportWidth, secondarySourceHeight);

        // Draw the first image
        context.DrawImage(source, new Rect(source.Size), sourceRect);

        // Draw the second image
        context.DrawImage(secondarySource, new Rect(secondarySource.Size), secondarySourceRect);
    }
    
    #endregion

    #region Measurement and Layout
    /// <summary>
    /// Measures the control.
    /// </summary>
    /// <param name="availableSize">The available size.</param>
    /// <returns>The desired size of the control.</returns>
    protected override Size MeasureOverride(Size availableSize)
    {
        try
        {
            return Source is not IImage source ? new Size() : CalculateSize(availableSize, source.Size);
        }
        catch (Exception e)
        {
#if DEBUG
            Console.WriteLine(e);
#endif
            if (DataContext is not MainViewModel vm)
            {
                return new Size();
            }

            var preloadValue = vm.ImageIterator?.GetCurrentPreLoadValue();
            if (preloadValue is not null)
            {
                return new Size(preloadValue.ImageModel.PixelWidth, preloadValue.ImageModel.PixelHeight);
            }

            if (vm.FileInfo is null)
            {
                return new Size();
            }

            using var magickImage = new MagickImage();
            magickImage.Ping(vm.FileInfo);
            return new Size(magickImage.Width, magickImage.Height);
        }
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        UpdateLayout();
        return base.ArrangeOverride(finalSize);
    }

    #endregion
    
    #region Calculations
    private static Vector CalculateScaling(Size destinationSize, Size sourceSize)
    {
        var isConstrainedWidth = !double.IsPositiveInfinity(destinationSize.Width);
        var isConstrainedHeight = !double.IsPositiveInfinity(destinationSize.Height);

        // Compute scaling factors for both axes
        var scaleX = MathUtilities.IsZero(sourceSize.Width) ? 0.0 : destinationSize.Width / sourceSize.Width;
        var scaleY = MathUtilities.IsZero(sourceSize.Height) ? 0.0 : destinationSize.Height / sourceSize.Height;

        if (!isConstrainedWidth)
        {
            scaleX = scaleY;
        }
        else if (!isConstrainedHeight)
        {
            scaleY = scaleX;
        }

        return new Vector(scaleX, scaleY);
    }
    
    public static Size CalculateSize(Size destinationSize, Size sourceSize)
    {
        return sourceSize * CalculateScaling(destinationSize, sourceSize);
    }
    #endregion
    
    #region Helper Methods

    private void UpdateSource(ImageType imageType)
    {
        switch (imageType)
        {
            case ImageType.Svg:
                if (Source is not string svg)
                {
                    goto default;
                }
                var svgSource = SvgSource.Load(svg);
                Source = new SvgImage { Source = svgSource };
                lock (Lock)
                {
                    _animInstance?.Dispose();
                }
                _stream?.Dispose();
                break;
            case ImageType.AnimatedGif:
            case ImageType.AnimatedWebp:
                Source = Source as Bitmap;
                lock (Lock)
                {
                    _animInstance?.Dispose();
                }
                
                break;
            case ImageType.Bitmap:
                Source = Source as Bitmap;
                lock (Lock)
                {
                    _animInstance?.Dispose();
                }
                _stream?.Dispose();
                break;
            case ImageType.Invalid:
            default:
                // TODO: Add invalid image graphic
                break;
        }
    }
    
    private Rect DetermineViewPort()
    {
        if (!(Bounds.Width <= 0) && !(Bounds.Height <= 0))
        {
            return new Rect(Bounds.Size);
        }

        var mainView = UIHelper.GetMainView;
        return mainView == null ? new Rect() : new Rect(Bounds.X, Bounds.Y, mainView.Bounds.Width, mainView.Bounds.Height);
    }

    #endregion

    #region Animation
    

    private void UpdateAnimationInstance(FileStream fileStream)
    {
        lock (Lock)
        {
            _animInstance?.Dispose();
            if (ImageType == ImageType.AnimatedGif)
            {
                _animInstance = new GifInstance(fileStream);
            }
            else
            {
                _animInstance = new WebpInstance(fileStream);
            }
            _animInstance.IterationCount = IterationCount.Infinite;
            _customVisual?.SendHandlerMessage(_animInstance);
        }
        AnimationUpdate();
    }
    
    private void AnimationUpdate()
    {
        if (_customVisual is null)
            return;

        var sourceSize = Bounds.Size;
        var viewPort = DetermineViewPort();

        var scale = CalculateScaling(viewPort.Size, sourceSize);
        var scaledSize = sourceSize * scale;
        var destRect = viewPort.CenterRect(new Rect(scaledSize)).Intersect(viewPort);

        _customVisual.Size = new Vector2((float)sourceSize.Width, (float)sourceSize.Height);
        _customVisual.Offset = new Vector3((float)destRect.Position.X, (float)destRect.Position.Y, 0);
    }

    #endregion
    
    #region Visual Tree
    
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _imageTypeSubscription.Dispose();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        var compositor = ElementComposition.GetElementVisual(this)?.Compositor;
        if (compositor == null || _customVisual?.Compositor == compositor) return;

        _customVisual = compositor.CreateCustomVisual(new CustomVisualHandler());
        ElementComposition.SetElementChildVisual(this, _customVisual);
        _customVisual.SendHandlerMessage(CustomVisualHandler.StartMessage);

        base.OnAttachedToVisualTree(e);
    }

    protected override AutomationPeer OnCreateAutomationPeer()
    {
        return new ImageAutomationPeer(this);
    }

    #endregion
}