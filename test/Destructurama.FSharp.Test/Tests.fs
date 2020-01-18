module Tests

open Xunit
open Destructurama.FSharp
open Serilog.Events
open Serilog.Core

[<CustomEquality; NoComparison>]
type SValue =
| Scalar of obj
| Sequence of SValue seq
| Structure of tag: string option * values: (string * SValue) seq
  interface System.IEquatable<SValue> with
     member x.Equals(y) =
        match x, y with
        | Scalar l, Scalar r ->
            l = r
        | Sequence l, Sequence r ->
            Seq.zip l r
            |> Seq.forall (fun (l, r) -> (l :> System.IEquatable<SValue>).Equals r)
        | Structure(lt, lv) , Structure(rt, rv) ->
            lt = rt
            && Seq.zip lv rv |> Seq.forall (fun ((ln, lv), (rn, rv)) -> ln = rn && (lv :> System.IEquatable<SValue>).Equals rv)
        | l, r -> false


let rec toSValue (l: LogEventPropertyValue) =
    match l with
    | :? ScalarValue as s -> Scalar s.Value
    | :? SequenceValue as s -> s.Elements |> Seq.map toSValue |> Sequence
    | :? StructureValue as s ->
        Structure(
            (match s.TypeTag with | null -> None | str -> Some str),
            s.Properties |> Seq.map (fun p -> p.Name, toSValue p.Value)
        )
    | t -> failwithf "unknown serilog value type %s" (t.GetType().FullName)

// let rec fromSValue s =
//     match s with
//     | Scalar o -> ScalarValue o :> LogEventPropertyValue
//     | Sequence s -> SequenceValue(s |> Seq.map fromSValue) :> _
//     | Structure(t, vs) ->
//         let vs = vs |> Seq.map (fun (name, v) -> LogEventProperty(name, fromSValue v))
//         StructureValue(vs, (match t with | None -> null | Some s -> s)) :> _

// dummy converter that always returns true
let alwaysScalarConverter =
    { new Serilog.Core.ILogEventPropertyValueFactory with
        member __.CreatePropertyValue(data, shouldDestructure) = Serilog.Events.ScalarValue(data) :> _ }

let policy =
    FSharpTypesDestructuringPolicy() :> Serilog.Core.IDestructuringPolicy

let inline doDestructuring data expected =
    match policy.TryDestructure(data, alwaysScalarConverter) with
    | true, property ->
        let actual = toSValue property
        if (expected :> System.IEquatable<SValue>).Equals actual
        then ()
        else
            failwithf "Expected %A to match %A" actual expected
    | false, _ -> failwithf "was unable to destructure an object of type %s" (data.GetType().FullName)

let structure tag (props: (string * LogEventPropertyValue) seq) =
    StructureValue(props |> Seq.map LogEventProperty, tag)
let scalar v = ScalarValue v :> LogEventPropertyValue


[<Fact>]
let ``can destructure lists`` () = doDestructuring [1;2;3] (Sequence(seq { Scalar 1; Scalar 2; Scalar 3; }))

[<Fact>]
let ``can destructure maps`` () = doDestructuring (Map.ofList ["1",1; "2",2; "3",3]) (Structure(None, seq { "1", Scalar 1
                                                                                                            "2", Scalar 2
                                                                                                            "3", Scalar 3 }))

type U =
| First
| Second of int
| Third of named: bool

// commented out because it's hard to test the 'correct' destructuring without the actual property formatter instance...
//[<Fact>]
let ``can destructure unions`` () =
    let expected = Sequence(seq {
        Scalar First
        Structure(Some "Second", seq { "Item", Scalar 2 })
        Structure(Some "Third", seq { "named", Scalar false })
    })
    doDestructuring [First; Second 2; Third false] expected

[<Fact>]
let ``can destructure tuple`` () = doDestructuring (1,2) (Structure(None, seq { "Item1", Scalar 1
                                                                                "Item2", Scalar 2 }))

#if NETCOREAPP2_2
[<Fact>]
let ``can destructure struct tuple`` () = doDestructuring (struct (1,2)) (Structure(None, seq { "Item1", Scalar 1
                                                                                                "Item2", Scalar 2}))
#endif

[<Fact>]
let ``can destructure option`` () = doDestructuring (Some "thing") (Structure(None, seq { "Some", Scalar "thing" }))

#if NETCOREAPP2_2
[<Fact>]
let ``can destructure struct option`` () = doDestructuring (ValueSome "thing") (Structure(None, seq { "Some", Scalar "thing"}))
#endif