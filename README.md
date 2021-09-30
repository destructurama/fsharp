# Destructurama.FSharp

[![Build status](https://ci.appveyor.com/api/projects/status/w01f9aej35xgg8tu/branch/dev?svg=true)](https://ci.appveyor.com/project/Destructurama/fsharp/branch/dev)

Native support for destructuring F# types when logging to Serilog.

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

Depending on the log storage you use you’ll be able to query on the tag as well as the fields (like Radius) from the union.

More samples can be seen by running the project in the `samples` folder using `dotnet run`. Doing so will destructure a union, record, and a tuple with and without this package to highlight the difference:

```
➜  samples git:(records) ✗ dotnet run
[13:35:19 INF] Printing a {"quantity": 10, "label": "hi", "Tag": 2, "IsA": false, "IsB": false, "IsC": true, "$type": "C"} with poor destructuring
[13:35:19 INF] Printing a {"quantity": 10, "label": "hi", "$type": "C"} with better destructuring

[13:35:19 INF] Printing a {"FieldA": 10, "OtherField": true, "AnotherOne": {"quantity": 10, "label": "hi", "Tag": 2, "IsA": false, "IsB": false, "IsC": true, "$type": "C"}, "$type": "MyRecord"} with poor destructuring
[13:35:19 INF] Printing a {"FieldA": 10, "OtherField": true, "AnotherOne": {"quantity": 10, "label": "hi", "$type": "C"}, "$type": "MyRecord"} with better destructuring

[13:35:19 INF] Printing a {"Item1": 1, "Item2": "hi", "$type": "Tuple`2"} with poor destructuring
[13:35:19 INF] Printing a [1, "hi"] with better destructuring
```

### Enabling the package

You can enable this by installing the package from NuGet:

```
dotnet add package Destructurama.FSharp
```

and adding `Destructure.FSharpTypes()` to your logger configuration:

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

### Building the repo

You should be able to clone down the repo and build with the dotnet sdk from the repository root:

```
dotnet build
```

### Testing the repo

There are tests in the Destructurama.FSharp.Test project, which can be run like so from the repo root:

```
dotnet test
```

### Releasing the repo

Releasing involves building the nuget packages and uploading them to nuget.

The packages can be built with the following command:

```
dotnet pack -c Release -p:Version=<DESIRED_VERSION>
```

This will result in a nuget package and a symbols packages being placed in `src/Destructurama.FSharp/bin/Release`. These packages can be uploaded to nuget or the package repository of your choice.
