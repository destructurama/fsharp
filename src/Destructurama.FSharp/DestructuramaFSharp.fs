// Copyright 2015 Destructurama Contributors, Serilog Contributors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace Destructurama.FSharp

open Microsoft.FSharp.Reflection
open Serilog.Core
open Serilog.Events
open System.Reflection

#if NETSTANDARD2_0
open TypeShape
open TypeShape.Core
open System.Collections.Concurrent
open System.Collections
#endif

module Impls =

#if NETSTANDARD1_6

    type System.Type with
        member x.IsGenericType = x.GetTypeInfo().IsGenericType
        member x.GetProperty(name, flags: BindingFlags) = x.GetTypeInfo().GetProperty(name, flags)

    let destructureNetstandard16(value: obj, factory: ILogEventPropertyValueFactory, result: byref<LogEventPropertyValue>): bool =
        let inline cpv obj = factory.CreatePropertyValue(obj, true)
        let inline lep (n: PropertyInfo) (v:obj) = LogEventProperty(n.Name, cpv v)

        match value.GetType() with
        | t when FSharpType.IsTuple t ->
            let fields = FSharpValue.GetTupleFields value
            let properties = 
                match fields with
                | [||] -> Seq.empty
                | [| f |] -> Seq.singleton (LogEventProperty("Item", cpv f))
                | fields -> fields |> Seq.mapi (fun i f -> LogEventProperty(sprintf "Item%d" (i + 1), cpv f))
            result <- StructureValue(properties)
            true

        // TODO: support for Maps and Sets? Why do Lists here when surely some IEnumerable-binder can handle them?
        | t when t.IsConstructedGenericType && t.GetGenericTypeDefinition() = typedefof<List<_>> ->
            let objEnumerable = value :?> System.Collections.IEnumerable |> Seq.cast<obj>
            result <- SequenceValue(objEnumerable |> Seq.map cpv)
            true

        | t when t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<option<obj>> -> 
            let optionTy = typedefof<Option<obj>>.MakeGenericType [| t.GenericTypeArguments.[0] |]
            // dirty hack because options have CompilationRepresentation.Null on the none case
            let isNone v = obj.ReferenceEquals(v, null)

            let valueGetter =
                let valueMember = optionTy.GetProperty("Value", BindingFlags.Public ||| BindingFlags.Instance)
                fun v -> valueMember.GetValue(v)

            if isNone value
            then
                result <- StructureValue(Seq.empty, "None")
                true
            else
                result <- StructureValue(Seq.singleton (LogEventProperty("Some", (factory.CreatePropertyValue(valueGetter value, true)))))
                true

        | t when t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<Map<string, string>> ->
            let keyType = t.GenericTypeArguments.[0]
            let valueType = t.GenericTypeArguments.[1]
            if keyType <> typeof<string>
            then
                false
            else
                let kvpType = typedefof<System.Collections.Generic.KeyValuePair<int, int>>.MakeGenericType([| keyType; valueType |])
                let getKey =
                    let p = kvpType.GetProperty("Key", BindingFlags.Public ||| BindingFlags.Instance)
                    fun o -> p.GetValue(o, [| |]) :?> string
                let getValue = 
                    let v = kvpType.GetProperty("Value", BindingFlags.Public ||| BindingFlags.Instance)
                    fun o -> v.GetValue(o, [| |])
                let fields = 
                    unbox<System.Collections.IEnumerable> value
                    |> Seq.cast<obj>
                    |> Seq.map (fun kvp -> LogEventProperty(getKey kvp, factory.CreatePropertyValue(getValue kvp, true)))
                result <- StructureValue(fields)
                true

        | t when FSharpType.IsUnion t ->
            let case, fields = FSharpValue.GetUnionFields(value, t)
            let properties = (case.GetFields(), fields) ||> Seq.map2 lep
            result <- StructureValue(properties, case.Name)
            true
        | _ ->
            false
#endif

#if NETSTANDARD2_0
    type Destructurer<'t> = 't -> ILogEventPropertyValueFactory -> bool * LogEventPropertyValue

    // let inline getV< ^a when ^a : (member GetValue:  obj -> obj)> (info: ^a) (v: obj) =
    //     ( (^a): (member GetValue: obj -> obj) (info, v))

    let private propGetter (m: MemberInfo) =
        match m.MemberType with
        | MemberTypes.Field -> Some ((m :?> FieldInfo).GetValue)
        | MemberTypes.Property -> Some ((m :?> PropertyInfo).GetValue)
        | _ -> None

    let private getReaders (fields: IShapeReadOnlyMember []) =
        fields
        |> Array.choose (fun field -> match  propGetter field.MemberInfo with Some f -> Some(field.Label, f) | None -> None)

    let readStructure tag fields v (f: ILogEventPropertyValueFactory) =
        let values =
            fields
            |> Array.map (fun (name, getter) ->
                            let prop = f.CreatePropertyValue(getter v, true)
                            LogEventProperty(name, prop))
            |> Seq.ofArray
        StructureValue(values, tag) :> LogEventPropertyValue

    type Holder =
        static member makeDestructurer<'t> (): Destructurer<'t> =
            let wrap (thing: 'a) = unbox<'t> thing

            match shapeof<'t> with
            | Shape.Tuple tup ->
                let reader = readStructure null (getReaders tup.Elements)
                fun v f -> true, reader v f
            | Shape.FSharpOption o ->
                let optionTy = typedefof<Option<obj>>.MakeGenericType [| o.Element.Type |]
                // dirty hack because options have CompilationRepresentation.Null on the none case
                let isNone v = obj.ReferenceEquals(v, null)

                let value =
                    let valueMember = optionTy.GetProperty("Value", BindingFlags.Public ||| BindingFlags.Instance)
                    fun v -> valueMember.GetValue(v)

                fun v f ->
                    if isNone v
                    then
                        true, StructureValue(Seq.empty, "None") :> _
                    else
                        true, StructureValue(Seq.singleton (LogEventProperty("Some", (f.CreatePropertyValue(value v, true))))) :> _
            | Shape.FSharpList l -> 
                fun v f -> 
                    let result = 
                        unbox<IEnumerable> v
                        |> Seq.cast<obj>
                        |> Seq.map (fun o -> f.CreatePropertyValue(o, true))
                        |> SequenceValue
                    true, result :> _
            | Shape.FSharpUnion u ->
                // for some reason typeshape doesn't catch valueoptions as options, so we have to do this thing
                if u.UnionCases.Length = 2 && u.UnionCases |> Array.map (fun c -> c.CaseInfo.Name) = [| "ValueNone"; "ValueSome" |]
                then 
                    let optionTy = typedefof<ValueOption<obj>>.MakeGenericType [| u.UnionCases.[1].Fields.[0].Member.Type |]
                    // dirty hack because options have CompilationRepresentation.Null on the none case
                    let isNone v = obj.ReferenceEquals(v, null)

                    let value =
                        let valueMember = optionTy.GetProperty("Value", BindingFlags.Public ||| BindingFlags.Instance)
                        fun v -> valueMember.GetValue(v)

                    fun v f ->
                        if isNone v
                        then
                            true, StructureValue(Seq.empty, "None") :> _
                        else
                            true, StructureValue(Seq.singleton (LogEventProperty("Some", (f.CreatePropertyValue(value v, true))))) :> _
                else
                    let caseMap =
                        u.UnionCases
                        |> Array.map (fun u -> u.CaseInfo.Name, readStructure u.CaseInfo.Name (getReaders u.Fields))
                        |> Map.ofArray
                    fun v f ->
                        let case, _ = FSharpValue.GetUnionFields(v, v.GetType())
                        match Map.tryFind case.Name caseMap with
                        | Some r -> true, r v f
                        | None -> false, null
            | Shape.FSharpRecord r ->
                let reader = readStructure null (getReaders r.Fields)
                fun v f -> true, reader v f
            | _ ->
                fun v f -> (false, null)


            // | Shape.FSharpList l ->
    let private destructurerDict = ConcurrentDictionary<System.Type, Destructurer<obj>>()

    let private destructurerForType: System.Type -> Destructurer<obj> =
        let destructureMeth = typeof<Holder>.GetMethod("makeDestructurer", BindingFlags.Static ||| BindingFlags.Public)
        fun t ->
            // the second part of the func
            let secondFunc = FSharpType.MakeFunctionType(typeof<ILogEventPropertyValueFactory>, typeof<bool * LogEventPropertyValue>)
            let secondInvoke = secondFunc.GetMethod("Invoke", BindingFlags.Public ||| BindingFlags.Instance)
            let firstFunc = FSharpType.MakeFunctionType(t, secondFunc)
            let firstInvoke = firstFunc.GetMethod("Invoke", BindingFlags.Public ||| BindingFlags.Instance)
            let destructurer = destructureMeth.MakeGenericMethod([| t |]).Invoke(null, [||])
            fun (value: obj) (factory: ILogEventPropertyValueFactory) ->
                // TODO: fsharpfunc.adapt?
                let firstStep = firstInvoke.Invoke(destructurer, [| value |])
                secondInvoke.Invoke(firstStep, [| factory |]) :?> bool * LogEventPropertyValue

    let destructureNetstandard20(value: obj, factory: ILogEventPropertyValueFactory, result: byref<LogEventPropertyValue>): bool =
        let destTy = value.GetType()
        let destructurer = destructurerDict.GetOrAdd(destTy, System.Func<_,_>(destructurerForType))
        match destructurer value factory with
        | true, r ->
            result <- r
            true
        | false, _ ->
            false
#endif

type public FSharpTypesDestructuringPolicy() =
    interface Serilog.Core.IDestructuringPolicy with
        member __.TryDestructure(value,
                                 propertyValueFactory : ILogEventPropertyValueFactory,
                                 result: byref<LogEventPropertyValue>) =
#if NETSTANDARD1_6
            Impls.destructureNetstandard16(value, propertyValueFactory, &result)
#endif
#if NETSTANDARD2_0
            Impls.destructureNetstandard20(value, propertyValueFactory, &result)
#endif

namespace Serilog

open Serilog.Configuration
open Destructurama.FSharp

[<AutoOpen>]
module public LoggerDestructuringConfigurationExtensions =
    type public LoggerDestructuringConfiguration with
        member public this.FSharpTypes() =
            this.With<FSharpTypesDestructuringPolicy>()

