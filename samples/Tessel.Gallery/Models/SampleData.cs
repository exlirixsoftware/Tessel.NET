using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Tessel.NET.Mvvm;

namespace Tessel.Gallery.Models;

public sealed class Employee : ObservableObject
{
    private bool _isActive;

    public required string Name { get; init; }
    public required string Role { get; init; }
    public required string Team { get; init; }
    public required DateTime StartDate { get; init; }
    public required decimal Salary { get; init; }

    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }
}

public sealed class ActivityEvent
{
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required DateTime Time { get; init; }
    public Tessel.NET.Controls.BadgeKind Kind { get; init; } = Tessel.NET.Controls.BadgeKind.Accent;
    public string Glyph { get; init; } = "";
}

public sealed class FolderNode
{
    public required string Name { get; init; }
    public string Glyph { get; init; } = "";
    public List<FolderNode> Children { get; init; } = [];
}

public static class SampleData
{
    public static ObservableCollection<Employee> Employees { get; } =
    [
        new() { Name = "Ada Lovelace", Role = "Principal Engineer", Team = "Platform", StartDate = new(2019, 3, 4), Salary = 182000, IsActive = true },
        new() { Name = "Grace Hopper", Role = "Engineering Manager", Team = "Compilers", StartDate = new(2017, 9, 18), Salary = 175000, IsActive = true },
        new() { Name = "Alan Turing", Role = "Research Scientist", Team = "AI", StartDate = new(2020, 1, 13), Salary = 168000, IsActive = true },
        new() { Name = "Katherine Johnson", Role = "Data Scientist", Team = "Analytics", StartDate = new(2021, 6, 1), Salary = 149000, IsActive = false },
        new() { Name = "Linus Torvalds", Role = "Staff Engineer", Team = "Kernel", StartDate = new(2018, 11, 26), Salary = 171000, IsActive = true },
        new() { Name = "Margaret Hamilton", Role = "Director", Team = "Flight Software", StartDate = new(2015, 4, 7), Salary = 198000, IsActive = true },
        new() { Name = "Dennis Ritchie", Role = "Senior Engineer", Team = "Languages", StartDate = new(2022, 2, 21), Salary = 158000, IsActive = false },
        new() { Name = "Barbara Liskov", Role = "Architect", Team = "Platform", StartDate = new(2016, 8, 15), Salary = 189000, IsActive = true },
    ];

    public static ObservableCollection<ActivityEvent> Activity { get; } =
    [
        new() { Title = "Ada opened a pull request", Description = "Add Timeline control (#42)", Time = new(2026, 10, 27, 9, 12, 0), Kind = Tessel.NET.Controls.BadgeKind.Accent, Glyph = "\uE8FB" },
        new() { Title = "Build succeeded", Description = "All 97 checks passed on the CI agent.", Time = new(2026, 10, 27, 9, 31, 0), Kind = Tessel.NET.Controls.BadgeKind.Success, Glyph = "\uE73E" },
        new() { Title = "Grace requested changes", Description = "Please add a vertical alternate layout.", Time = new(2026, 10, 27, 11, 5, 0), Kind = Tessel.NET.Controls.BadgeKind.Warning, Glyph = "\uE90A" },
        new() { Title = "Merged into main", Description = "Squashed 3 commits.", Time = new(2026, 10, 28, 8, 47, 0), Kind = Tessel.NET.Controls.BadgeKind.Info, Glyph = "\uE8AB" },
    ];

    public static List<FolderNode> Folders { get; } =
    [
        new()
        {
            Name = "Documents",
            Children =
            [
                new() { Name = "Invoices", Children = [new() { Name = "2025.pdf", Glyph = "" }, new() { Name = "2026.pdf", Glyph = "" }] },
                new() { Name = "Resume.docx", Glyph = "" },
            ],
        },
        new()
        {
            Name = "Pictures",
            Glyph = "",
            Children = [new() { Name = "Vacation", Children = [new() { Name = "beach.jpg", Glyph = "" }] }, new() { Name = "avatar.png", Glyph = "" }],
        },
        new() { Name = "Projects", Children = [new() { Name = "Tessel.NET", Glyph = "" }] },
    ];
}
