# Destructurama.FSharp

[![Build status](https://ci.appveyor.com/api/projects/status/w01f9aej35xgg8tu/branch/master?svg=true)](https://ci.appveyor.com/project/Destructurama/fsharp/branch/master)

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

### Enabling the package

You can enable this by installing the package from NuGet:

```powershell
Install-Package Destructurama.FSharp
```

and adding `Destructure.FSharpTypes()` to your logger configuration:

```fsharp
open Serilog

[<EntryPoint>]
let main argv = 

    Log.Logger <- LoggerConfiguration()
        .Destructure.FSharpTypes()
        .WriteTo.ColoredConsole()
        .CreateLogger()

    Log.Information("Drawing a {@Shape}", Circle 5.)

    0
```
