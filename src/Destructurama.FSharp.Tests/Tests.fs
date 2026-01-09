module Tests

open Xunit
open Destructurama.FSharp

type Decision = Yes | No | MaybeSo

type MyRecord =
    { FieldA : int
      OtherField: bool
      AnotherOne: Decision }

// dummy converter that always returns true
let alwaysTrueConverter =
    { new Serilog.Core.ILogEventPropertyValueFactory with
        member __.CreatePropertyValue(data, shouldDestructure) = Serilog.Events.ScalarValue(true) :> _ }

let policy =
    FSharpTypesDestructuringPolicy() :> Serilog.Core.IDestructuringPolicy

let inline doDestructuring data =
    match policy.TryDestructure(data, alwaysTrueConverter) with
    | true, property ->
        printfn "rendered data \n%A as \n%s" data (string property)
    | false, _ ->
        failwithf "was unable to destructure an object of type %s" (data.GetType().FullName)

[<Fact>]
let ``can destructure tuple`` () = doDestructuring (1,2)

[<Fact>]
let ``can destructure struct tuple`` () = doDestructuring (struct (1,2))

[<Fact>]
#nowarn FS3261
let ``can destructure union`` () = doDestructuring (Some "thing")
#warnon FS3261

[<Fact>]
let ``can destructure struct union`` () = doDestructuring (ValueSome "thing")

[<Fact>]
let ``can destructure record`` () = doDestructuring { FieldA = 10
                                                      OtherField = true
                                                      AnotherOne = MaybeSo }
