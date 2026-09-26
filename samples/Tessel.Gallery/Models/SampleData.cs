using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Tessel.UI.Mvvm;

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
