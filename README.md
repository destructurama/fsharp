# Destructurama.FSharp

![License](https://img.shields.io/github/license/destructurama/fsharp)

[![codecov](https://codecov.io/gh/destructurama/fsharp/graph/badge.svg?token=Ma2sUoqqb1)](https://codecov.io/gh/destructurama/fsharp)
[![Nuget](https://img.shields.io/nuget/dt/Destructurama.FSharp)](https://www.nuget.org/packages/Destructurama.FSharp)
[![Nuget](https://img.shields.io/nuget/v/Destructurama.FSharp)](https://www.nuget.org/packages/Destructurama.FSharp)

[![GitHub Release Date](https://img.shields.io/github/release-date/destructurama/fsharp?label=released)](https://github.com/destructurama/fsharp/releases)
[![GitHub commits since latest release (by date)](https://img.shields.io/github/commits-since/destructurama/fsharp/latest?label=new+commits)](https://github.com/destructurama/fsharp/commits/master)
![Size](https://img.shields.io/github/repo-size/destructurama/fsharp)

[![GitHub contributors](https://img.shields.io/github/contributors/destructurama/fsharp)](https://github.com/destructurama/fsharp/graphs/contributors)
![Activity](https://img.shields.io/github/commit-activity/w/destructurama/fsharp)
![Activity](https://img.shields.io/github/commit-activity/m/destructurama/fsharp)
![Activity](https://img.shields.io/github/commit-activity/y/destructurama/fsharp)

[![Run unit tests](https://github.com/destructurama/fsharp/actions/workflows/test.yml/badge.svg)](https://github.com/destructurama/fsharp/actions/workflows/test.yml)
[![Publish preview to GitHub registry](https://github.com/destructurama/fsharp/actions/workflows/publish-preview.yml/badge.svg)](https://github.com/destructurama/fsharp/actions/workflows/publish-preview.yml)
[![Publish release to Nuget registry](https://github.com/destructurama/fsharp/actions/workflows/publish-release.yml/badge.svg)](https://github.com/destructurama/fsharp/actions/workflows/publish-release.yml)
[![CodeQL analysis](https://github.com/destructurama/fsharp/actions/workflows/codeql-analysis.yml/badge.svg)](https://github.com/destructurama/fsharp/actions/workflows/codeql-analysis.yml)

Native support for destructuring F# types when logging to [Serilog](https://serilog.net).

# Installation

Install from NuGet:

```
dotnet add package Destructurama.FSharp
```

# Usage

Add `Destructure.FSharpTypes()` to your logger configuration:

```fsharp
open Serilog

[<EntryPoint>]
let main argv = 

    Log.Logger <- LoggerConfiguration()
        .Destructure.FSharpTypes()
        .WriteTo.Console()
        .CreateLogger()

    Log.Information("Drawing a {@Shape}", Circle 5.)

    0
```

Discriminated unions like:

```fsharp
type Shape =
    | Circle of Radius : double
    | Rectangle of Width : double *  Height : double
    | Arc // ...
let shape = Circle 5.
```

When logged with Serilog:

```fsharp
Log.Information("Drawing a {@Shape}", shape)
```

Are printed nicely like:

```
2015-01-14 16:58:31 [Information] Drawing a Circle { Radius: 5 }
```

Depending on the log storage you use you will be able to query on the tag as well
as the fields (like Radius) from the union.

More samples can be seen by running the project in the `Samples` folder using
`dotnet run`. Doing so will destructure a union, record, and a tuple with and
without this package to highlight the difference:

```
➜  samples git:(records) ✗ dotnet run
[13:35:19 INF] Printing a {"quantity": 10, "label": "hi", "Tag": 2, "IsA": false, "IsB": false, "IsC": true, "$type": "C"} with poor destructuring
[13:35:19 INF] Printing a {"quantity": 10, "label": "hi", "$type": "C"} with better destructuring

[13:35:19 INF] Printing a {"FieldA": 10, "OtherField": true, "AnotherOne": {"quantity": 10, "label": "hi", "Tag": 2, "IsA": false, "IsB": false, "IsC": true, "$type": "C"}, "$type": "MyRecord"} with poor destructuring
[13:35:19 INF] Printing a {"FieldA": 10, "OtherField": true, "AnotherOne": {"quantity": 10, "label": "hi", "$type": "C"}, "$type": "MyRecord"} with better destructuring

[13:35:19 INF] Printing a {"Item1": 1, "Item2": "hi", "$type": "Tuple`2"} with poor destructuring
[13:35:19 INF] Printing a [1, "hi"] with better destructuring
```
