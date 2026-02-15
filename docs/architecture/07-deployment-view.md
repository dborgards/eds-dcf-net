# 7. Deployment View

## 7.1 Infrastructure

EdsDcfNet is a pure library without its own runtime infrastructure. Distribution is via NuGet package.

```mermaid
graph TB
    subgraph Development ["Development"]
        Dev["Developer Machine"]
        GH["GitHub Repository<br/>dborgards/eds-dcf-net"]
    end

    subgraph CI ["CI/CD (GitHub Actions)"]
        Build["Build & Test<br/>(build.yml)"]
        Release["Semantic Release<br/>(semantic-release.yml)"]
    end

    subgraph Distribution ["Distribution"]
        NuGet["NuGet.org<br/>EdsDcfNet"]
        GHR["GitHub Releases<br/>+ .nupkg Assets"]
    end

    subgraph Consumers ["Consumers"]
        App1[".NET Framework 4.6.1+<br/>Application"]
        App2[".NET 8/10<br/>Application"]
        App3["Unity / Xamarin<br/>Application"]
    end

    Dev -->|git push| GH
    GH -->|Push to feature branch| Build
    GH -->|Push to main/develop/alpha| Release
    Release -->|dotnet pack + push| NuGet
    Release -->|Create GitHub Release| GHR
    NuGet -->|dotnet add package| App1
    NuGet -->|dotnet add package| App2
    NuGet -->|dotnet add package| App3

    style Dev fill:#4A90D9,color:#fff
    style GH fill:#333,color:#fff
    style Build fill:#7AB648,color:#fff
    style Release fill:#7AB648,color:#fff
    style NuGet fill:#004880,color:#fff
    style GHR fill:#333,color:#fff
```

## 7.2 NuGet Package Structure

The NuGet package contains two target frameworks:

```
EdsDcfNet.1.3.0.nupkg
├── lib/
│   ├── netstandard2.0/
│   │   ├── EdsDcfNet.dll
│   │   └── EdsDcfNet.xml        (XML documentation)
│   └── net10.0/
│       ├── EdsDcfNet.dll
│       └── EdsDcfNet.xml
├── README.md
└── EdsDcfNet.nuspec

EdsDcfNet.1.3.0.snupkg            (Symbol package for Source Link)
```

## 7.3 CI/CD Pipeline

```mermaid
flowchart LR
    subgraph Trigger ["Trigger"]
        Push["Push to main, develop, alpha"]
    end

    subgraph Pipeline ["GitHub Actions: Semantic Release"]
        Checkout["Checkout"]
        Setup[".NET + Node.js Setup"]
        Restore["dotnet restore"]
        BuildStep["dotnet build"]
        Test["dotnet test<br/>+ Codecov Upload"]
        SR["semantic-release"]
    end

    subgraph SR_Steps ["semantic-release Steps"]
        Analyze["commit-analyzer<br/><i>Determine release type</i>"]
        Notes["release-notes-generator<br/><i>Generate changelog</i>"]
        CL["changelog<br/><i>Update CHANGELOG.md</i>"]
        Exec["exec<br/><i>Set version in .csproj<br/>dotnet pack + push</i>"]
        Git["git<br/><i>Version commit</i>"]
        GHRelease["github<br/><i>GitHub Release</i>"]
    end

    Push --> Checkout --> Setup --> Restore --> BuildStep --> Test --> SR
    SR --> Analyze --> Notes --> CL --> Exec --> Git --> GHRelease

    style Push fill:#E74C3C,color:#fff
    style SR fill:#9B59B6,color:#fff
```

### Release Rules

| Commit Type    | Release Impact     |
|----------------|--------------------|
| `feat`         | Minor release      |
| `fix`, `perf`  | Patch release      |
| `docs`         | **No release**     |
| `chore`, `ci`  | **No release**     |
| `BREAKING CHANGE` | Major release   |

## 7.4 Platform Compatibility

| Target Framework     | Supported Platforms                                              |
|----------------------|------------------------------------------------------------------|
| `netstandard2.0`     | .NET Framework 4.6.1+, .NET Core 2.0+, Mono 5.4+, Xamarin, Unity 2018+ |
| `net10.0`            | .NET 10 (Linux, macOS, Windows)                                  |
