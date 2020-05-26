// Learn more about F# at http://fsharp.org

open System
open Serilog
open Serilog.Configuration
open Serilog.Sinks

let goodLogger =
    LoggerConfiguration()
        .Destructure.FSharpTypes()
        .WriteTo.Console()
        .CreateLogger()

let badLogger = 
    LoggerConfiguration()
        .WriteTo.Console()
        .CreateLogger()

type Union = A | B of bool | C of quantity: int * label: string

type MyRecord = 
    { FieldA : int 
      OtherField: bool
      AnotherOne: Union }

[<EntryPoint>]
let main argv =

    let union = C (10, "hi")

    badLogger.Information("Printing a {@union} with poor destructuring", union)
    goodLogger.Information("Printing a {@union} with better destructuring", union)
    
    let record = 
        { FieldA = 10
          OtherField = true
          AnotherOne = union }

    badLogger.Information("Printing a {@record} with poor destructuring", record)
    goodLogger.Information("Printing a {@record} with better destructuring", record)

    let tuple = 1, "hi"

    badLogger.Information("Printing a {@tuple} with poor destructuring", tuple)
    goodLogger.Information("Printing a {@tuple} with better destructuring", tuple)

    0 // return an integer exit code
