# Restaurant Management System (POS) — Project Foundation (Milestone 0)

A professional restaurant management and point-of-sale desktop application with offline-first capabilities, built using **C#**, **.NET 10**, **WPF**, **ASP.NET Core**, **Generic Host**, and **Serilog**.

---

## 1. Architecture Overview

The system is organized following **Clean Architecture** principles to separate business logic from presentation and infrastructure concerns:

```
                  WPF Desktop Application (MVVM)
                               │
                               ▼
                        Application Layer
                               │
                               ▼
                          Domain Layer
                               │
                               ▼
                         Infrastructure
                               │
                               ▼
                      SQLite Database (Local)
```

And in subsequent milestones, an ASP.NET Core API can expose the same application services:

```
  WPF Desktop ─────────┐
                       │
                       ▼
               Application Layer
                       │
                       ▼
                 Infrastructure
                       │
                       ▼
               SQLite Local Database

  ASP.NET Core API ────┘
```

---

## 2. Solution Structure

```
RestaurantManagement.sln
├── .gitignore
├── README.md
├── src/
│   ├── RestaurantManagement.Domain/                 # Core domain entities, value objects, domain rules
│   │   └── Common/
│   │       ├── Entity.cs
│   │       ├── ValueObject.cs
│   │       └── IAggregateRoot.cs
│   │
│   ├── RestaurantManagement.Application/            # Application services, use cases, interfaces, DTOs
│   │   ├── Common/
│   │   │   ├── Interfaces/
│   │   │   │   ├── IAppInfoService.cs
│   │   │   │   └── IDateTimeProvider.cs
│   │   │   ├── Models/
│   │   │   │   └── AppInfoResult.cs
│   │   │   └── Exceptions/
│   │   │       └── AppException.cs
│   │   └── DependencyInjection.cs
│   │
│   ├── RestaurantManagement.Infrastructure/         # External concerns, IAppInfoService, IDateTimeProvider (EF Core/SQLite in later milestones)
│   │   ├── Services/
│   │   │   ├── AppInfoService.cs
│   │   │   └── DateTimeProvider.cs
│   │   └── DependencyInjection.cs
│   │
│   ├── RestaurantManagement.Api/                    # ASP.NET Core Minimal API with Serilog & /health check
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   └── appsettings.Development.json
│   │
│   └── RestaurantManagement.Desktop/                # WPF Desktop Application with MVVM & Generic Host
│       ├── App.xaml / App.xaml.cs                   # Generic Host setup, DI container, Serilog, Centralized Error Handling
│       ├── MainWindow.xaml / MainWindow.xaml.cs     # Main UI shell (Header, Navigation sidebar, Content area, Status bar)
│       ├── appsettings.json                         # Desktop logging & configuration
│       ├── Services/
│       │   ├── INavigationService.cs
│       │   └── NavigationService.cs
│       └── ViewModels/
│           ├── ViewModelBase.cs
│           ├── RelayCommand.cs
│           ├── MainViewModel.cs
│           └── NavigationItemViewModel.cs
│
└── tests/
    ├── RestaurantManagement.Application.Tests/      # xUnit tests for Application layer & Domain base types
    └── RestaurantManagement.Infrastructure.Tests/   # xUnit tests for Infrastructure layer & DI services
```

---

## 3. Technology Stack & Key Libraries

- **Framework**: .NET 10 (`net10.0`, `net10.0-windows`)
- **Desktop UI**: WPF (Windows Presentation Foundation) with MVVM pattern
- **Host & DI**: `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.DependencyInjection`
- **Configuration**: `Microsoft.Extensions.Configuration`
- **Logging**: `Serilog` (Console & Rolling File sinks under `logs/`)
- **Testing**: `xUnit`, `FluentAssertions`, `Microsoft.NET.Test.Sdk`
- **Web API**: ASP.NET Core Minimal API

---

## 4. Build & Test Commands

### Build Solution
```powershell
dotnet build RestaurantManagement.sln
```

### Run All Tests
```powershell
dotnet test RestaurantManagement.sln
```

---

## 5. Running the Applications

### Running the WPF Desktop Application
```powershell
dotnet run --project src/RestaurantManagement.Desktop/RestaurantManagement.Desktop.csproj
```

### Running the ASP.NET Core API
```powershell
dotnet run --project src/RestaurantManagement.Api/RestaurantManagement.Api.csproj
```

Once running, navigate to:
- Root: `http://localhost:5000/` (or configured port)
- Health Check: `http://localhost:5000/health`
