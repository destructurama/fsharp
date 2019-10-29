module Tests

open Xunit
open Destructurama.FSharp

// dummy converter that always returns true
let alwaysTrueConverter = 
    { new Serilog.Core.ILogEventPropertyValueFactory with 
        member __.CreatePropertyValue(data, shouldDestructure) = Serilog.Events.ScalarValue(true) :> _ }

let policy = 
    FSharpTypesDestructuringPolicy() :> Serilog.Core.IDestructuringPolicy

let inline doDestructuring data = 
    match policy.TryDestructure(data, alwaysTrueConverter) with
    | true, _property -> ()
    | false, _ -> failwithf "was unable to destructure an object of type %s" (data.GetType().FullName)

[<Fact>]
let ``can destructure tuple`` () = doDestructuring (1,2)

[<Fact>]
let ``can destructure struct tuple`` () = doDestructuring (struct (1,2))

[<Fact>]
let ``can destructure union`` () = doDestructuring (Some "thing")
    
[<Fact>]
let ``can destructure struct union`` () = doDestructuring (ValueSome "thing")
