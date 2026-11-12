using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using Tessel.NET.Controls;
using Tessel.NET.Mvvm;

namespace Tessel.Gallery.Models;

public enum ShapeKind
{
    Rectangle,
    Rounded,
    Pill,
}

/// <summary>The object that the PropertyGrid demo edits.</summary>
public sealed class ShapeModel : ObservableObject
{
    private string _label = "Hello Tessel";
    private ShapeKind _kind = ShapeKind.Rounded;
    private Color _fill = Color.FromRgb(0x4F, 0x46, 0xE5);
    private double _width = 220;
    private double _height = 110;
    private double _opacity = 1;
    private bool _isVisible = true;
    private int _cornerRadius = 16;
    private DateTime _created = DateTime.Today;

    [Category("Content")]
    [Description("The text drawn on the shape")]
    public string Label
    {
        get => _label;
        set => SetProperty(ref _label, value);
    }

    [Category("Content")]
    public ShapeKind Kind
    {
        get => _kind;
        set => SetProperty(ref _kind, value);
    }

    [Category("Appearance")]
    [DisplayName("Fill color")]
    public Color Fill
    {
        get => _fill;
        set => SetProperty(ref _fill, value);
    }

    [Category("Appearance")]
    [Range(0, 1)]
    public double Opacity
    {
        get => _opacity;
        set => SetProperty(ref _opacity, value);
    }

    [Category("Appearance")]
    [Range(0, 60)]
    public int CornerRadius
    {
        get => _cornerRadius;
        set => SetProperty(ref _cornerRadius, value);
    }

    [Category("Layout")]
    [Range(40, 400)]
    public double Width
    {
        get => _width;
        set => SetProperty(ref _width, value);
    }

    [Category("Layout")]
    [Range(30, 200)]
    public double Height
    {
        get => _height;
        set => SetProperty(ref _height, value);
    }

    [Category("Layout")]
    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    [Category("Info")]
    public DateTime Created
    {
        get => _created;
        set => SetProperty(ref _created, value);
    }

    [Category("Info")]
    public string Id { get; } = "shape-01";

    [Browsable(false)]
    public string Hidden => "not shown";
}

public sealed record TeamMember(string Name, AvatarPresence Presence);
